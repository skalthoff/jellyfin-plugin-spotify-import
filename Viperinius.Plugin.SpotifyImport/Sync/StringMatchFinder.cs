using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class StringMatchFinder : ITrackMatchFinder
    {
        private const int MaxSearchChars = 5;

        // The provably best-possible string match: TrySplitParensContents seeds contentPrio=1 for each side and Equal
        // sums them, so (prio 2, level Default) is the floor under the (prio, level) sort comparator below — nothing
        // can sort before it. Once a candidate hits it we can stop scanning the remaining artists/albums without
        // changing which (prio, level) is returned.
        private const int BestPossiblePrio = 2;

        // When the provider supplies a duration, two exact-name candidates are ranked by duration closeness, so an
        // exact-name hit is only truly unbeatable (safe to stop on) if its duration also matches near-exactly.
        private const long PerfectDurationGraceMs = 2000;

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        // per-sync memoisation: GetArtist/GetAlbum used to re-issue the same library query once per candidate row.
        // StringMatchFinder is constructed once per sync run, so these caches are naturally scoped to a single sync.
        private readonly Dictionary<string, IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem>> _artistCandidatesCache = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem>> _artistAlbumsCache = new();

        // MusicAlbum.Tracks calls Folder.GetRecursiveChildren on every access (db-backed, re-resolves each time), so
        // multiple provider tracks resolving to the same album would re-query its track list repeatedly. Memoise per
        // sync, keyed by album reference (album.Id can be empty/duplicated on transient instances).
        private readonly Dictionary<MusicAlbum, IReadOnlyList<Audio>> _albumTracksCache = new(ReferenceEqualityComparer.Instance);

        public StringMatchFinder(
            ILogger logger,
            ILibraryManager libraryManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
        }

        public ItemMatchCriteria LastFailedCriteria { get; private set; }

        public bool IsEnabled => true;

        public Task<Audio?> FindTrackAsync(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            return Task.FromResult(FindTrackSync(providerId, providerTrackInfo));
        }

        private Audio? FindTrackSync(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled)
            {
                return null;
            }

            LastFailedCriteria = ItemMatchCriteria.None;
            var matchCandidates = new List<(int, ItemMatchLevel, Audio)>();

            var foundBestPossible = false;
            var artistProviderNextIndex = 0;
            var artistJfNextIndex = 0;
            while (artistProviderNextIndex >= 0 && !foundBestPossible)
            {
                var artist = GetArtist(providerTrackInfo, ref artistProviderNextIndex, ref artistJfNextIndex);
                if (artist == null)
                {
                    LastFailedCriteria |= ItemMatchCriteria.Artists;
                    continue;
                }

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Found matching artist {Name} {Id}", artist.Name, artist.Id);
                }

                var albumNextIndex = 0;
                while (albumNextIndex >= 0 && !foundBestPossible)
                {
                    var album = GetAlbum(artist, providerTrackInfo, ref albumNextIndex);
                    if (album == null)
                    {
                        LastFailedCriteria |= ItemMatchCriteria.AlbumName;
                        continue;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(">> Found matching album {Name} {Id}", album.Name, album.Id);
                    }

                    if (!CheckAlbumArtist(album, providerTrackInfo))
                    {
                        LastFailedCriteria |= ItemMatchCriteria.AlbumArtists;
                        continue;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(">> Album artists ok");
                    }

                    // materialise once: AddRange + the emptiness check would otherwise enumerate (and re-run every
                    // TrackComparison in) this lazy sequence twice
                    var tracks = GetTrack(album, providerTrackInfo).ToList();
                    matchCandidates.AddRange(tracks);
                    if (tracks.Count == 0)
                    {
                        LastFailedCriteria |= ItemMatchCriteria.TrackName;
                    }
                    else if (tracks.Any(t => IsUnbeatableMatch(t, providerTrackInfo)))
                    {
                        // best-possible match found; no later candidate can sort before it, so stop scanning
                        foundBestPossible = true;
                    }
                }
            }

            if (matchCandidates.Count > 0)
            {
                // sort by prio, then match level, then closeness to the provider track duration (tie-breaker)
                matchCandidates.Sort((a, b) => TrackComparison.CompareMatchCandidates(a, b, providerTrackInfo));
                LastFailedCriteria = ItemMatchCriteria.None;
                return matchCandidates.First().Item3;
            }

            return null;
        }

        private MusicArtist? GetArtist(ProviderTrackInfo providerTrackInfo, ref int nextProviderArtistIndex, ref int nextJfArtistIndex)
        {
            var artistName = providerTrackInfo.ArtistNames.ElementAtOrDefault(nextProviderArtistIndex);
            if (string.IsNullOrEmpty(artistName))
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Reached end of provider artist list");
                }

                nextProviderArtistIndex = -1;
                return null;
            }

            // only search for the first few characters to increase the chances of finding artists with slightly differing names between provider and jellyfin
            // (memoised per truncated search term so walking K candidates does not re-run the same query K+1 times)
            var candidates = GetArtistCandidates(artistName);

            if (candidates.Count == nextJfArtistIndex || candidates.Count == 0)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Reached end of jellyfin artist list");
                    if (candidates.Count == 0)
                    {
                        _logger.LogInformation("> Did not find any artists for the name {Name}", artistName);
                    }
                }

                nextProviderArtistIndex++;
                nextJfArtistIndex = 0;
                return null;
            }

            var item = candidates[nextJfArtistIndex];
            nextJfArtistIndex++;

            if (item is not MusicArtist artist)
            {
                return null;
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false)
            {
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                if (!TrackComparison.ArtistOneContained(artist, providerTrackInfo, level))
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(
                            "> Artist did not match: {JName} [Jellyfin, {Id}], {PName} [Provider]",
                            artist.Name,
                            artist.Id,
                            string.Join("#", providerTrackInfo.ArtistNames));
                    }

                    return null;
                }
            }

            return artist;
        }

        private IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> GetArtistCandidates(string artistName)
        {
            var searchTerm = artistName[0..Math.Min(artistName.Length, MaxSearchChars)];
            if (!_artistCandidatesCache.TryGetValue(searchTerm, out var candidates))
            {
                var queryResult = _libraryManager.GetArtists(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    SearchTerm = searchTerm,
                });
                candidates = queryResult.Items.Select(i => i.Item).ToList();
                _artistCandidatesCache[searchTerm] = candidates;
            }

            return candidates;
        }

        private IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> GetArtistAlbums(MusicArtist artist)
        {
            if (_artistAlbumsCache.TryGetValue(artist.Id, out var albums))
            {
                return albums;
            }

            // materialise once; Folder.Children is db-backed and re-resolves on every access
            IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> resolved = artist.Children.ToList();
            if (resolved.Count == 0)
            {
                // for whatever reason albums are apparently not always set as children of the artist... so try to find them using album artist
                resolved = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    AlbumArtistIds = new[] { artist.Id },
                    IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }
                }) ?? new List<MediaBrowser.Controller.Entities.BaseItem>();
            }

            _artistAlbumsCache[artist.Id] = resolved;
            return resolved;
        }

        private bool CheckAlbumArtist(MusicAlbum album, ProviderTrackInfo providerTrackInfo)
        {
            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumArtists) ?? false)
            {
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                return TrackComparison.AlbumArtistOneContained(album, providerTrackInfo, level);
            }

            return true;
        }

        private MusicAlbum? GetAlbum(MusicArtist artist, ProviderTrackInfo providerTrackInfo, ref int nextAlbumIndex)
        {
            // memoised per artist: the album list (incl. the by-name-artist fallback query) was previously re-resolved on every album index
            var albums = GetArtistAlbums(artist);

            var item = albums.ElementAtOrDefault(nextAlbumIndex);
            nextAlbumIndex++;
            if (item == null)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation(">> Reached end of album list (has {Count} entries)", albums.Count);
                }

                nextAlbumIndex = -1;
                return null;
            }

            if (item is not MusicAlbum album)
            {
                return null;
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false)
            {
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                if (!TrackComparison.AlbumNameEqual(album, providerTrackInfo, level).ComparisonResult)
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(
                            ">> Album did not match: {JName} [Jellyfin, {Id}], {PName} [Provider]",
                            album.Name,
                            album.Id,
                            providerTrackInfo.AlbumName);
                    }

                    return null;
                }
            }

            return album;
        }

        private static bool IsUnbeatableMatch((int Prio, ItemMatchLevel Level, Audio Item) candidate, ProviderTrackInfo providerTrackInfo)
        {
            if (candidate.Prio != BestPossiblePrio || candidate.Level != ItemMatchLevel.Default)
            {
                return false;
            }

            // Exact-name match. With duration as a tie-breaker, only stop early if duration cannot improve the
            // result: either the provider has no duration (tie-break inactive) or this candidate already matches
            // it near-exactly. Otherwise keep scanning for a candidate with a closer duration.
            if (providerTrackInfo.DurationMs == null || providerTrackInfo.DurationMs <= 0)
            {
                return true;
            }

            var delta = TrackComparison.DurationDeltaMs(candidate.Item, providerTrackInfo);
            return delta != null && delta.Value <= PerfectDurationGraceMs;
        }

        private IReadOnlyList<Audio> GetAlbumTracks(MusicAlbum album)
        {
            if (!_albumTracksCache.TryGetValue(album, out var tracks))
            {
                // materialise once; MusicAlbum.Tracks re-resolves its children from the db on every enumeration
                tracks = album.Tracks.ToList();
                _albumTracksCache[album] = tracks;
            }

            return tracks;
        }

        private IEnumerable<(int Prio, ItemMatchLevel Level, Audio Item)> GetTrack(MusicAlbum album, ProviderTrackInfo providerTrackInfo)
        {
            var enableDurationLimit = Plugin.Instance?.Configuration.EnableDurationLimit ?? false;
            var maxDurationDiff = Plugin.Instance?.Configuration.MaxDurationDifferenceSeconds ?? 0;

            foreach (var item in GetAlbumTracks(album))
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation(
                        ">>> Checking server track {Name} [{Album}][{AlbumArtist}][{Artist}]",
                        item.Name,
                        album.Name,
                        string.Join("#", item.AlbumArtists),
                        string.Join("#", item.Artists));
                }

                var prio = int.MaxValue;
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false)
                {
                    var checkResult = TrackComparison.TrackNameEqual(item, providerTrackInfo, level);
                    if (!checkResult.ComparisonResult || checkResult.MatchedLevel == null || checkResult.MatchedPrio == null)
                    {
                        continue;
                    }
                    else
                    {
                        prio = (int)checkResult.MatchedPrio;
                        level = (ItemMatchLevel)checkResult.MatchedLevel;
                        if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                        {
                            _logger.LogInformation(">>> Found matching potential track {Name} {Id}", item.Name, item.Id);
                        }
                    }
                }

                // optional hard gate: drop candidates whose known duration is too far from the provider track
                if (TrackComparison.DurationExceedsLimit(item, providerTrackInfo, enableDurationLimit, maxDurationDiff))
                {
                    continue;
                }

                yield return (prio, level, item);
            }

            yield break;
        }
    }
}
