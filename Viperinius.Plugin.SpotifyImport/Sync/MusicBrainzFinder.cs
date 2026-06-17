using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class MusicBrainzFinder : ITrackMatchFinder
    {
        private const string ProviderRecording = "MusicBrainzRecording";
        private const string ProviderTrack = "MusicBrainzTrack";
        private const string ProviderAlbum = "MusicBrainzAlbum";
        private const string ProviderReleaseGroup = "MusicBrainzReleaseGroup";

        private readonly ILibraryManager _libraryManager;
        private readonly DbRepository _dbRepository;

        // per-sync index of the library's MusicBrainz-tagged audio, keyed by each kind of MusicBrainz id.
        // MusicBrainzFinder is constructed once per sync run, so this is naturally scoped to a single sync.
        // Previously every track issued one library query per MusicBrainz id (O(tracks * ids) round-trips);
        // this collapses that into a single enumeration plus constant-time dictionary lookups.
        private readonly object _indexLock = new();
        private readonly Dictionary<string, List<Audio>> _recordingIndex = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Audio>> _trackIndex = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Audio>> _releaseIndex = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Audio>> _releaseGroupIndex = new(StringComparer.Ordinal);
        private bool _indexBuilt;
        private bool _indexUnavailable;

        private bool? _anyLibraryUsesMusicBrainz;

        public MusicBrainzFinder(
            ILibraryManager libraryManager,
            DbRepository dbRepository)
        {
            _libraryManager = libraryManager;
            _dbRepository = dbRepository;
        }

        public bool AnyLibraryUsesMusicBrainz
        {
            get
            {
                _anyLibraryUsesMusicBrainz ??= Utils.MusicBrainz.MusicBrainzHelper.IsServerUsingMusicBrainz(_libraryManager);
                return (bool)_anyLibraryUsesMusicBrainz;
            }
        }

        public bool IsEnabled => Plugin.Instance?.Configuration.EnabledTrackMatchFinders.HasFlag(EnabledTrackMatchFinders.MusicBrainz) ?? false;

        public Task<Audio?> FindTrackAsync(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled || !AnyLibraryUsesMusicBrainz || string.IsNullOrWhiteSpace(providerTrackInfo.IsrcId))
            {
                return Task.FromResult<Audio?>(null);
            }

            var foundIsrcMappings = _dbRepository.GetIsrcMusicBrainzMapping(isrc: providerTrackInfo.IsrcId, hasAnyMbIdsSet: true);
            var mbRecordings = new HashSet<string>();
            var mbReleases = new HashSet<string>();
            var mbTracks = new HashSet<string>();
            var mbReleaseGroups = new HashSet<string>();
            foreach (var mapping in foundIsrcMappings)
            {
                mbRecordings.UnionWith(mapping.MusicBrainzRecordingIds.Select(r => r.ToString()));
                mbReleases.UnionWith(mapping.MusicBrainzReleaseIds.Select(r => r.ToString()));
                mbTracks.UnionWith(mapping.MusicBrainzTrackIds.Select(t => t.ToString()));
                mbReleaseGroups.UnionWith(mapping.MusicBrainzReleaseGroupIds.Select(r => r.ToString()));
            }

            // no MusicBrainz ids for this ISRC: nothing to look up, do not touch the library at all
            if (mbRecordings.Count == 0 && mbTracks.Count == 0 && mbReleases.Count == 0 && mbReleaseGroups.Count == 0)
            {
                return Task.FromResult<Audio?>(null);
            }

            if (!_indexUnavailable)
            {
                EnsureIndexBuilt();
            }

            // if the bulk index could not be built, fall back to the original per-id library queries so matching still works
            if (_indexUnavailable)
            {
                return FindTrackLiveAsync(mbRecordings, mbTracks, mbReleases, mbReleaseGroups, providerTrackInfo);
            }

            return Task.FromResult(FindTrackInIndex(mbRecordings, mbTracks, mbReleases, mbReleaseGroups, providerTrackInfo));
        }

        private void EnsureIndexBuilt()
        {
            if (_indexBuilt)
            {
                return;
            }

            lock (_indexLock)
            {
                if (_indexBuilt || _indexUnavailable)
                {
                    return;
                }

                try
                {
                    // single enumeration of every audio item carrying any MusicBrainz id. The empty values mean
                    // "has this provider key with any value" (same shape IsServerUsingMusicBrainz uses).
                    var allItems = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        HasAnyProviderId = new Dictionary<string, string>
                        {
                            { ProviderRecording, string.Empty },
                            { ProviderTrack, string.Empty },
                            { ProviderAlbum, string.Empty },
                            { ProviderReleaseGroup, string.Empty },
                        },
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                    });

                    foreach (var item in allItems)
                    {
                        if (item is not Audio audio)
                        {
                            continue;
                        }

                        AddToIndex(_recordingIndex, audio, ProviderRecording);
                        AddToIndex(_trackIndex, audio, ProviderTrack);
                        AddToIndex(_releaseIndex, audio, ProviderAlbum);
                        AddToIndex(_releaseGroupIndex, audio, ProviderReleaseGroup);
                    }

                    _indexBuilt = true;
                }
                catch (Exception)
                {
                    // building the bulk index failed; mark it unavailable so all lookups fall back to live queries
                    _indexUnavailable = true;
                }
            }
        }

        private static void AddToIndex(Dictionary<string, List<Audio>> index, Audio audio, string providerKey)
        {
            if (audio.ProviderIds == null || !audio.ProviderIds.TryGetValue(providerKey, out var value) || string.IsNullOrEmpty(value))
            {
                return;
            }

            if (!index.TryGetValue(value, out var list))
            {
                list = new List<Audio>();
                index[value] = list;
            }

            list.Add(audio);
        }

        private static void CollectFromIndex(Dictionary<string, List<Audio>> index, HashSet<string> ids, List<Audio> into)
        {
            foreach (var id in ids)
            {
                if (index.TryGetValue(id, out var list))
                {
                    into.AddRange(list);
                }
            }
        }

        private static Audio? PickDeterministicDirectHit(List<Audio> candidates)
        {
            // exact recording/track id is the strongest signal, so a direct hit wins without a name check.
            // order by Id so the result is stable when several library items share one MusicBrainz id.
            return candidates.Count == 0 ? null : candidates.OrderBy(a => a.Id).First();
        }

        private Audio? FindTrackInIndex(HashSet<string> mbRecordings, HashSet<string> mbTracks, HashSet<string> mbReleases, HashSet<string> mbReleaseGroups, ProviderTrackInfo providerTrackInfo)
        {
            // phase 1: direct hits by recording / track id
            if (mbRecordings.Count > 0 || mbTracks.Count > 0)
            {
                var directHits = new List<Audio>();
                CollectFromIndex(_recordingIndex, mbRecordings, directHits);
                CollectFromIndex(_trackIndex, mbTracks, directHits);

                var directHit = PickDeterministicDirectHit(directHits);
                if (directHit != null)
                {
                    return directHit;
                }
            }

            // phase 2: release / release-group candidates disambiguated by name match
            if (mbReleases.Count > 0 || mbReleaseGroups.Count > 0)
            {
                var candidates = new List<Audio>();
                CollectFromIndex(_releaseIndex, mbReleases, candidates);
                CollectFromIndex(_releaseGroupIndex, mbReleaseGroups, candidates);

                var matchCandidates = new List<(int Prio, ItemMatchLevel Level, Audio Item)>();
                foreach (var track in candidates.GroupBy(a => a.Id).Select(g => g.First()))
                {
                    var matchInfo = MatchTrack(track, providerTrackInfo);
                    if (matchInfo != null)
                    {
                        matchCandidates.Add(((int, ItemMatchLevel, Audio))matchInfo);
                    }
                }

                if (matchCandidates.Count > 0)
                {
                    return SortAndPick(matchCandidates);
                }
            }

            return null;
        }

        private async Task<Audio?> FindTrackLiveAsync(HashSet<string> mbRecordings, HashSet<string> mbTracks, HashSet<string> mbReleases, HashSet<string> mbReleaseGroups, ProviderTrackInfo providerTrackInfo)
        {
            if (mbRecordings.Count > 0 || mbTracks.Count > 0)
            {
                // library manager does not seem to support querying multiple ProviderIds with same key, so every different MB id has to be done in a separate query...
                // to speed this up in some way, try to fill query with one of each "direct hit" ProviderId types if available
                var tasks = new List<Task<IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem>>>();
                for (var ii = 0; ii < Math.Max(mbRecordings.Count, mbTracks.Count); ii++)
                {
                    var idDict = new Dictionary<string, string>();
                    if (ii < mbRecordings.Count)
                    {
                        idDict.Add(ProviderRecording, mbRecordings.ElementAt(ii));
                    }

                    if (ii < mbTracks.Count)
                    {
                        idDict.Add(ProviderTrack, mbTracks.ElementAt(ii));
                    }

                    tasks.Add(Task.Run(() => _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        HasAnyProviderId = idDict,
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = 1,
                    })));
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var directHits in results)
                {
                    if (directHits.Count > 0 && directHits[0] is Audio directHit)
                    {
                        return directHit;
                    }
                }
            }

            // library manager does not seem to support querying multiple ProviderIds with same key, so every different MB id has to be done in a separate query...
            // to speed this up in some way, try to fill query with one of each ProviderId types if available
            var matchCandidates = new List<(int, ItemMatchLevel, Audio)>();
            if (mbReleases.Count > 0 || mbReleaseGroups.Count > 0)
            {
                var tasks = new List<Task<IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem>>>();
                for (var ii = 0; ii < Math.Max(mbReleases.Count, mbReleaseGroups.Count); ii++)
                {
                    var idDict = new Dictionary<string, string>();
                    if (ii < mbReleases.Count)
                    {
                        idDict.Add(ProviderAlbum, mbReleases.ElementAt(ii));
                    }

                    if (ii < mbReleaseGroups.Count)
                    {
                        idDict.Add(ProviderReleaseGroup, mbReleaseGroups.ElementAt(ii));
                    }

                    tasks.Add(Task.Run(() => _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        HasAnyProviderId = idDict,
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                    })));
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var tracksWithMbIds in results)
                {
                    foreach (var track in tracksWithMbIds)
                    {
                        var matchInfo = MatchTrack((track as Audio)!, providerTrackInfo);
                        if (matchInfo != null)
                        {
                            matchCandidates.Add(((int, ItemMatchLevel, Audio))matchInfo);
                        }
                    }
                }
            }

            if (matchCandidates.Count > 0)
            {
                return SortAndPick(matchCandidates);
            }

            return null;
        }

        private static Audio SortAndPick(List<(int Prio, ItemMatchLevel Level, Audio Item)> matchCandidates)
        {
            // sort by prio first, then match level
            matchCandidates.Sort((a, b) =>
            {
                var result = a.Prio.CompareTo(b.Prio);
                if (result == 0)
                {
                    result = a.Level.CompareTo(b.Level);
                }

                return result;
            });
            return matchCandidates.First().Item;
        }

        private (int Prio, ItemMatchLevel Level, Audio Item)? MatchTrack(Audio track, ProviderTrackInfo providerTrackInfo)
        {
            var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
            var checkResult = TrackComparison.TrackNameEqual(track, providerTrackInfo, level);
            if (!checkResult.ComparisonResult || checkResult.MatchedLevel == null || checkResult.MatchedPrio == null)
            {
                return null;
            }

            var prio = (int)checkResult.MatchedPrio;
            level = (ItemMatchLevel)checkResult.MatchedLevel;
            return (prio, level, track);
        }
    }
}
