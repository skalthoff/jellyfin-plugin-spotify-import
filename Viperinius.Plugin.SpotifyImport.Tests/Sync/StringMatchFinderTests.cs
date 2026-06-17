using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    public class StringMatchFinderTests
    {
        private static MediaBrowser.Controller.Library.ILibraryManager MakeLibManager(
            IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> artists,
            IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> albumFallback)
        {
            var artistTuples = artists
                .Select(a => (a, new MediaBrowser.Model.Dto.ItemCounts()))
                .ToList();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetArtists(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => new MediaBrowser.Model.Querying.QueryResult<(MediaBrowser.Controller.Entities.BaseItem, MediaBrowser.Model.Dto.ItemCounts)>(artistTuples));
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => albumFallback);
            return libManagerMock;
        }

        [Fact]
        public async Task ArtistQuery_RunsOnce_WhenWalkingMultipleCandidates()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            // single provider artist -> a single search term; library returns several non-matching candidates
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "AlbArt" }, new List<string> { "ZZZ" });
            var artists = new List<MediaBrowser.Controller.Entities.BaseItem>
            {
                ArtistHelper.CreateJfItem("AAA", new List<MusicAlbum>()),
                ArtistHelper.CreateJfItem("BBB", new List<MusicAlbum>()),
                ArtistHelper.CreateJfItem("CCC", new List<MusicAlbum>()),
            };

            var libManagerMock = MakeLibManager(artists, new List<MediaBrowser.Controller.Entities.BaseItem>());
            var finder = new StringMatchFinder(Substitute.For<ILogger>(), libManagerMock);

            var result = await finder.FindTrackAsync("prov", prov);

            Assert.Null(result);

            // before memoisation this query ran once per candidate (K + 1 times); now it must run exactly once
            libManagerMock.Received(1).GetArtists(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>());
        }

        [Fact]
        public async Task AlbumFallbackQuery_RunsOncePerArtist()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            // one matching artist with no children -> forces the album-artist fallback query
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "AlbArt" }, new List<string> { "Match" });
            var artists = new List<MediaBrowser.Controller.Entities.BaseItem>
            {
                ArtistHelper.CreateJfItem("Match", new List<MusicAlbum>()),
            };
            var albumFallback = new List<MediaBrowser.Controller.Entities.BaseItem>
            {
                AlbumHelper.CreateJfItem("AlbumA", "AlbArt", new List<Audio>()),
                AlbumHelper.CreateJfItem("AlbumB", "AlbArt", new List<Audio>()),
            };

            var libManagerMock = MakeLibManager(artists, albumFallback);
            var finder = new StringMatchFinder(Substitute.For<ILogger>(), libManagerMock);

            var result = await finder.FindTrackAsync("prov", prov);

            Assert.Null(result);

            // before memoisation this fallback ran once per album index (B + 1 times); now it must run exactly once
            libManagerMock.Received(1).GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>());
        }

        [Fact]
        public async Task BestPossibleMatch_StopsTraversal_AndReturnsFirstInOrder()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.Artists | ItemMatchCriteria.TrackName);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "AlbArt" }, new List<string> { "Match" });

            // two albums, each containing an exact "Track" match -> both score the (prio 2, Default) floor
            var firstAudio = TrackHelper.CreateJfItem("Track", null, null, null);
            var albumA = AlbumHelper.CreateJfItem("AlbumA", "AlbArt", new List<Audio> { firstAudio });
            var albumB = AlbumHelper.CreateJfItem("AlbumB", "AlbArt", new List<Audio> { TrackHelper.CreateJfItem("Track", null, null, null) });

            var artist = ArtistHelper.CreateJfItem("Match", new List<MusicAlbum> { albumA, albumB });
            var libManagerMock = MakeLibManager(
                new List<MediaBrowser.Controller.Entities.BaseItem> { artist },
                new List<MediaBrowser.Controller.Entities.BaseItem>());
            var finder = new StringMatchFinder(Substitute.For<ILogger>(), libManagerMock);

            var result = await finder.FindTrackAsync("prov", prov);

            // the best-possible match in the first album is returned ...
            Assert.Same(firstAudio, result);
            // ... and the scan stops: the second album's (expensive, db-backed) track list is never resolved
            _ = albumB.DidNotReceive().Tracks;
        }

        private const long TicksPerSecond = 10_000_000;

        private static Audio TrackWithRuntime(string name, long seconds)
        {
            var audio = TrackHelper.CreateJfItem(name, null, null, null);
            audio.RunTimeTicks = seconds * TicksPerSecond;
            return audio;
        }

        [Fact]
        public async Task Duration_BreaksTie_BetweenEqualNameMatchesAcrossAlbums()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.Artists | ItemMatchCriteria.TrackName);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;
            Plugin.Instance!.Configuration.EnableDurationLimit = false;

            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "AlbArt" }, new List<string> { "Match" });
            prov.DurationMs = 200_000;

            // first album's exact-name match is far from the provider duration (so no early-exit fires);
            // second album's exact-name match is near it and must win the tie-break
            var farAudio = TrackWithRuntime("Track", 100);
            var closeAudio = TrackWithRuntime("Track", 201);
            var albumA = AlbumHelper.CreateJfItem("AlbumA", "AlbArt", new List<Audio> { farAudio });
            var albumB = AlbumHelper.CreateJfItem("AlbumB", "AlbArt", new List<Audio> { closeAudio });

            var artist = ArtistHelper.CreateJfItem("Match", new List<MusicAlbum> { albumA, albumB });
            var finder = new StringMatchFinder(
                Substitute.For<ILogger>(),
                MakeLibManager(new List<MediaBrowser.Controller.Entities.BaseItem> { artist }, new List<MediaBrowser.Controller.Entities.BaseItem>()));

            var result = await finder.FindTrackAsync("prov", prov);

            Assert.Same(closeAudio, result);
        }

        [Fact]
        public async Task Duration_HardLimit_RejectsTooFarMatch_WhenEnabled()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.Artists | ItemMatchCriteria.TrackName);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;
            Plugin.Instance!.Configuration.MaxDurationDifferenceSeconds = 30;

            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "AlbArt" }, new List<string> { "Match" });
            prov.DurationMs = 200_000; // 200 s

            var onlyAudio = TrackWithRuntime("Track", 100); // 100 s -> 100 s delta, well over the 30 s limit
            var album = AlbumHelper.CreateJfItem("AlbumA", "AlbArt", new List<Audio> { onlyAudio });
            var artist = ArtistHelper.CreateJfItem("Match", new List<MusicAlbum> { album });
            var finder = new StringMatchFinder(
                Substitute.For<ILogger>(),
                MakeLibManager(new List<MediaBrowser.Controller.Entities.BaseItem> { artist }, new List<MediaBrowser.Controller.Entities.BaseItem>()));

            // with the hard limit on, the over-long candidate is rejected ...
            Plugin.Instance!.Configuration.EnableDurationLimit = true;
            Assert.Null(await finder.FindTrackAsync("prov", prov));

            // ... and accepted again once the limit is disabled (duration then only tie-breaks, never rejects)
            var finder2 = new StringMatchFinder(
                Substitute.For<ILogger>(),
                MakeLibManager(new List<MediaBrowser.Controller.Entities.BaseItem> { artist }, new List<MediaBrowser.Controller.Entities.BaseItem>()));
            Plugin.Instance!.Configuration.EnableDurationLimit = false;
            Assert.Same(onlyAudio, await finder2.FindTrackAsync("prov", prov));
        }
    }
}
