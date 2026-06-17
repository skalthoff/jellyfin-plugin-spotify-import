using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.Db;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    public class IsrcCacheFinderTests
    {
        private const string ProviderName = "Spotify";

        // Plugin.Instance.Configuration is a shared singleton mutated by other test classes; pin the matching
        // config explicitly so these tests do not depend on whatever the previously run test left behind.
        private static void UseDefaultMatching()
        {
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists | ItemMatchCriteria.AlbumArtists);
        }

        private static ProviderTrackInfo MakeTrack(string id, string name, string isrc) => new()
        {
            Id = id,
            Name = name,
            AlbumName = "Album",
            ArtistNames = new List<string> { "Artist" },
            AlbumArtistNames = new List<string> { "Artist" },
            IsrcId = isrc,
        };

        private static MediaBrowser.Controller.Entities.Audio.Audio MakeLibraryAudio(Guid id, string name, string album) => new()
        {
            Id = id,
            Name = name,
            Album = album,
            Artists = new[] { "Artist" },
            AlbumArtists = new[] { "Artist" },
        };

        [Fact]
        public void IsEnabledByDefault()
        {
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            Assert.True(finder.IsEnabled);
        }

        [Fact]
        public async Task ReusesMatchFromTrackSharingIsrc()
        {
            TrackHelper.SetValidPluginInstance();
            UseDefaultMatching();

            var resolvedGuid = Guid.NewGuid();
            var trackA = MakeTrack("a", "Song", "ISRCX");
            var trackB = MakeTrack("b", "Song", "ISRCX");

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => MakeLibraryAudio(info.ArgAt<Guid>(0), "Song", "Album"));

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            var dbA = (long)db.InsertProviderTrack(ProviderName, trackA)!;
            db.InsertProviderTrack(ProviderName, trackB);
            db.InsertProviderTrackMatch(dbA, resolvedGuid.ToString(), Plugin.Instance!.Configuration.ItemMatchLevel, Plugin.Instance!.Configuration.ItemMatchCriteria);

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            var result = await finder.FindTrackAsync(ProviderName, trackB);

            Assert.NotNull(result);
            Assert.Equal(resolvedGuid, result.Id);
        }

        [Fact]
        public async Task DoesNotReuseWhenNameDoesNotMatch()
        {
            TrackHelper.SetValidPluginInstance();
            UseDefaultMatching();

            var resolvedGuid = Guid.NewGuid();
            var trackA = MakeTrack("a", "Song", "ISRCX");
            var trackB = MakeTrack("b", "Totally Different", "ISRCX");

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => MakeLibraryAudio(info.ArgAt<Guid>(0), "Song", "Album"));

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            var dbA = (long)db.InsertProviderTrack(ProviderName, trackA)!;
            db.InsertProviderTrack(ProviderName, trackB);
            db.InsertProviderTrackMatch(dbA, resolvedGuid.ToString(), Plugin.Instance!.Configuration.ItemMatchLevel, Plugin.Instance!.Configuration.ItemMatchCriteria);

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            var result = await finder.FindTrackAsync(ProviderName, trackB);

            // ISRC matches but the reused item's name fails re-validation against track B -> no reuse
            Assert.Null(result);
        }

        [Fact]
        public async Task DoesNotReuseWhenConfiguredAlbumDoesNotMatch()
        {
            TrackHelper.SetValidPluginInstance();
            UseDefaultMatching(); // default config enforces AlbumName among the criteria

            var resolvedGuid = Guid.NewGuid();
            var trackA = MakeTrack("a", "Song", "ISRCX");
            var trackB = MakeTrack("b", "Song", "ISRCX");
            trackB.AlbumName = "A Different Album"; // same recording/ISRC, different album metadata

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => MakeLibraryAudio(info.ArgAt<Guid>(0), "Song", "Album"));

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            var dbA = (long)db.InsertProviderTrack(ProviderName, trackA)!;
            db.InsertProviderTrack(ProviderName, trackB);
            db.InsertProviderTrackMatch(dbA, resolvedGuid.ToString(), Plugin.Instance!.Configuration.ItemMatchLevel, Plugin.Instance!.Configuration.ItemMatchCriteria);

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            var result = await finder.FindTrackAsync(ProviderName, trackB);

            // album is a configured criterion and the reused item's album does not match track B -> no reuse
            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullWhenNoOtherTrackSharesIsrc()
        {
            TrackHelper.SetValidPluginInstance();

            var trackB = MakeTrack("b", "Song", "ISRCX");

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            db.InsertProviderTrack(ProviderName, trackB);

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            var result = await finder.FindTrackAsync(ProviderName, trackB);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullWhenNoIsrc()
        {
            TrackHelper.SetValidPluginInstance();

            var trackB = MakeTrack("b", "Song", null!);

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            var result = await finder.FindTrackAsync(ProviderName, trackB);

            Assert.Null(result);
        }
    }
}
