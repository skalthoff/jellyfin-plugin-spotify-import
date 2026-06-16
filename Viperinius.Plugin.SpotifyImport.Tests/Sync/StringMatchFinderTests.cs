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
    }
}
