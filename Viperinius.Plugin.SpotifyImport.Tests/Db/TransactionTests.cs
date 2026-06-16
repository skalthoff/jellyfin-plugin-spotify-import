using System;
using System.Collections.Generic;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    public class TransactionTests
    {
        private static ProviderTrackInfo MakeTrack(string id)
        {
            return new ProviderTrackInfo
            {
                Id = id,
                Name = $"Name {id}",
                AlbumName = "Album",
                AlbumArtistNames = new List<string> { "AlbumArtist" },
                ArtistNames = new List<string> { "Artist" },
                TrackNumber = 1,
            };
        }

        [Fact]
        public void RunInTransaction_CommitsWholeBatch()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.RunInTransaction(() =>
            {
                db.InsertProviderTrack("prov", MakeTrack("a"));
                db.InsertProviderTrack("prov", MakeTrack("b"));
            });

            Assert.NotNull(db.GetProviderTrackDbId("prov", "a"));
            Assert.NotNull(db.GetProviderTrackDbId("prov", "b"));
        }

        [Fact]
        public void RunInTransaction_RollsBackOnException()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            // committed before the failing batch -> must survive
            db.InsertProviderTrack("prov", MakeTrack("keep"));

            Assert.Throws<InvalidOperationException>(() =>
                db.RunInTransaction(() =>
                {
                    db.InsertProviderTrack("prov", MakeTrack("rollback"));
                    throw new InvalidOperationException("boom");
                }));

            Assert.NotNull(db.GetProviderTrackDbId("prov", "keep"));
            Assert.Null(db.GetProviderTrackDbId("prov", "rollback"));
        }

        [Fact]
        public void RunInTransaction_NullBodyThrows()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            Assert.Throws<ArgumentNullException>(() => db.RunInTransaction(null!));
        }
    }
}
