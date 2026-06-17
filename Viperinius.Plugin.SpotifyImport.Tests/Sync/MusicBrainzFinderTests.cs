using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.Db;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    public class MusicBrainzFinderTests
    {
        [Fact]
        public void IsNotEnabledByDefault()
        {
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();

            var finder = new MusicBrainzFinder(libManagerMock, db);

            Plugin.SetInstance(null);
            Assert.False(finder.IsEnabled);

            TrackHelper.SetValidPluginInstance();
            Assert.False(finder.IsEnabled);
        }

        [Fact]
        public void GetsEnabledByConfig()
        {
            TrackHelper.SetValidPluginInstance();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();

            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.IsEnabled);
        }

        [Fact]
        public async Task FindTrackNoLibUsesMB()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => []);

            using var db = DbRepositoryWrapper.GetInstance();

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.False(finder.AnyLibraryUsesMusicBrainz);
            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public async Task FindTrackNoExistingMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);
            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public async Task FindTrackNoExistingNonPlaceholderMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);
            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public async Task FindTrackNoTrackNameMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };
            var correctMbReleaseId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [Guid.NewGuid()], [correctMbReleaseId], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzAlbum") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { Name = $"ad873frasd{correctProviderTrackInfo.Name}AEF$Iasu", ProviderIds = new Dictionary<string, string> { { "MusicBrainzAlbum", correctMbReleaseId.ToString() } } }];
                    }

                    return [];
                });

            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public async Task FindTrackOkDirectHitRecording()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };
            var correctMbRecordingId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [correctMbRecordingId], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzRecording") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { ProviderIds = new Dictionary<string, string> { { "MusicBrainzRecording", correctMbRecordingId.ToString() } } }];
                    }

                    return [];
                });

            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzRecording"));
            Assert.Equal(correctMbRecordingId.ToString(), result.ProviderIds["MusicBrainzRecording"]);
        }

        [Fact]
        public async Task FindTrackOkDirectHitTrack()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };
            var correctMbTrackId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [correctMbTrackId], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzTrack") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { ProviderIds = new Dictionary<string, string> { { "MusicBrainzTrack", correctMbTrackId.ToString() } } }];
                    }

                    return [];
                });

            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzTrack"));
            Assert.Equal(correctMbTrackId.ToString(), result.ProviderIds["MusicBrainzTrack"]);
        }

        [Fact]
        public async Task FindTrackOkTrackNameMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };
            var correctMbReleaseId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [Guid.NewGuid()], [correctMbReleaseId], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzAlbum") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio
                        {
                            Name = correctProviderTrackInfo.Name,
                            ProviderIds = new Dictionary<string, string>
                            {
                                { "MusicBrainzAlbum", correctMbReleaseId.ToString() },
                                { "MusicBrainzReleaseGroup", Guid.NewGuid().ToString() },
                            }
                        }];
                    }

                    return [];
                });

            var result = await finder.FindTrackAsync(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzAlbum"));
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzReleaseGroup"));
            Assert.Equal(correctMbReleaseId.ToString(), result.ProviderIds["MusicBrainzAlbum"]);
        }

        [Fact]
        public async Task FindTrackBuildsIndexOnlyOnce()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var providerInfo = new ProviderTrackInfo { Name = "48agWO$ga", IsrcId = "aeioda98r" };
            var mbRecordingId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var q = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0);
                    if (q.IncludeItemTypes != null && q.IncludeItemTypes.Contains(Jellyfin.Data.Enums.BaseItemKind.Audio))
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { ProviderIds = new Dictionary<string, string> { { "MusicBrainzRecording", mbRecordingId.ToString() } } }];
                    }

                    return [];
                });

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, providerInfo.IsrcId, DateTime.UtcNow, [mbRecordingId], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            for (var ii = 0; ii < 3; ii++)
            {
                var result = await finder.FindTrackAsync("provider", providerInfo);
                Assert.NotNull(result);
            }

            // the bulk index query (only Audio, no Limit) must run exactly once across all lookups;
            // the AnyLibraryUsesMusicBrainz probe uses Limit = 1 so it is not counted here
            libManagerMock.Received(1).GetItemList(Arg.Is<MediaBrowser.Controller.Entities.InternalItemsQuery>(
                q => q.Limit == null && q.IncludeItemTypes != null && q.IncludeItemTypes.Contains(Jellyfin.Data.Enums.BaseItemKind.Audio)));
        }

        [Fact]
        public async Task FindTrackDirectHitTieBreakIsDeterministic()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var providerInfo = new ProviderTrackInfo { Name = "48agWO$ga", IsrcId = "aeioda98r" };
            var mbRecordingId = Guid.NewGuid();
            var idA = Guid.NewGuid();
            var idB = Guid.NewGuid();
            var expectedId = new[] { idA, idB }.OrderBy(g => g).First();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var q = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0);
                    if (q.IncludeItemTypes != null && q.IncludeItemTypes.Contains(Jellyfin.Data.Enums.BaseItemKind.Audio))
                    {
                        return
                        [
                            new MediaBrowser.Controller.Entities.Audio.Audio { Id = idA, ProviderIds = new Dictionary<string, string> { { "MusicBrainzRecording", mbRecordingId.ToString() } } },
                            new MediaBrowser.Controller.Entities.Audio.Audio { Id = idB, ProviderIds = new Dictionary<string, string> { { "MusicBrainzRecording", mbRecordingId.ToString() } } },
                        ];
                    }

                    return [];
                });

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, providerInfo.IsrcId, DateTime.UtcNow, [mbRecordingId], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            var first = await finder.FindTrackAsync("provider", providerInfo);
            var second = await finder.FindTrackAsync("provider", providerInfo);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(expectedId, first.Id);
            Assert.Equal(expectedId, second.Id);
        }

        [Fact]
        public async Task FindTrackFallsBackToLiveQueriesWhenIndexBuildFails()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var providerInfo = new ProviderTrackInfo { Name = "48agWO$ga", IsrcId = "aeioda98r" };
            var mbRecordingId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var q = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0);

                    // the bulk index query is the only one carrying all four MusicBrainz keys: make it fail
                    if (q.HasAnyProviderId != null && q.HasAnyProviderId.Count == 4)
                    {
                        throw new InvalidOperationException("Simulated bulk index query failure");
                    }

                    // both the probe and the per-id live fallback look up by MusicBrainzRecording
                    if (q.HasAnyProviderId != null && q.HasAnyProviderId.ContainsKey("MusicBrainzRecording"))
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { ProviderIds = new Dictionary<string, string> { { "MusicBrainzRecording", mbRecordingId.ToString() } } }];
                    }

                    return [];
                });

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, providerInfo.IsrcId, DateTime.UtcNow, [mbRecordingId], [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            // index build throws -> finder falls back to the original per-id queries instead of aborting
            var result = await finder.FindTrackAsync("provider", providerInfo);
            Assert.NotNull(result);
            Assert.Equal(mbRecordingId.ToString(), result.ProviderIds["MusicBrainzRecording"]);
        }
    }
}
