using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.Db;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    public class IsrcCacheFinderTests
    {
        private const string ProviderName = "Spotify";

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

            var resolvedGuid = Guid.NewGuid();
            var trackA = new ProviderTrackInfo { Id = "a", Name = "Song", IsrcId = "ISRCX" };
            var trackB = new ProviderTrackInfo { Id = "b", Name = "Song", IsrcId = "ISRCX" };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => new MediaBrowser.Controller.Entities.Audio.Audio { Id = info.ArgAt<Guid>(0), Name = "Song" });

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

            var resolvedGuid = Guid.NewGuid();
            var trackA = new ProviderTrackInfo { Id = "a", Name = "Song", IsrcId = "ISRCX" };
            var trackB = new ProviderTrackInfo { Id = "b", Name = "Totally Different", IsrcId = "ISRCX" };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => new MediaBrowser.Controller.Entities.Audio.Audio { Id = info.ArgAt<Guid>(0), Name = "Song" });

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
        public async Task ReturnsNullWhenNoOtherTrackSharesIsrc()
        {
            TrackHelper.SetValidPluginInstance();

            var trackB = new ProviderTrackInfo { Id = "b", Name = "Song", IsrcId = "ISRCX" };

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

            var trackB = new ProviderTrackInfo { Id = "b", Name = "Song", IsrcId = null };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var finder = new IsrcCacheFinder(Substitute.For<ILogger>(), libManagerMock, db);
            var result = await finder.FindTrackAsync(ProviderName, trackB);

            Assert.Null(result);
        }
    }
}
