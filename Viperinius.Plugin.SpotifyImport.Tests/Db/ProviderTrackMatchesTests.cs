#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    public class ProviderTrackMatchesTests
    {
        private void PrepareOtherTablesData(DbRepositoryWrapper db)
        {
            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderTrackCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "2a");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "3a");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "4a");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());
        }

        [Fact]
        public void CanInsert()
        {
            var correctTrackDbId = 1;
            var correctJfId = Guid.NewGuid();
            var correctMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var correctMatchCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            db.InsertProviderTrackMatch(correctTrackDbId, correctJfId.ToString(), correctMatchLevel, correctMatchCriteria);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM ProviderTrackMatches";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctTrackDbId, reader.GetInt64(1));
                    Assert.Equal(correctJfId, reader.GetGuid(2));
                    Assert.Equal(correctMatchLevel, (ItemMatchLevel)reader.GetInt32(3));
                    Assert.Equal(correctMatchCriteria, (ItemMatchCriteria)reader.GetInt32(4));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanRetrieveTracks()
        {
            var correctTrackDbId = 1;
            var correctJfId = Guid.NewGuid();
            var correctMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var correctMatchCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderTrackMatchCmd();
            cmd.Parameters.AddWithValue("$TrackId", 2);
            cmd.Parameters.AddWithValue("$JellyfinMatchId", correctJfId);
            cmd.Parameters.AddWithValue("$MatchLevel", ItemMatchLevel.Fuzzy);
            cmd.Parameters.AddWithValue("$MatchCriteria", correctMatchCriteria);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$JellyfinMatchId", correctJfId);
            cmd.Parameters.AddWithValue("$MatchLevel", correctMatchLevel);
            cmd.Parameters.AddWithValue("$MatchCriteria", correctMatchCriteria);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$TrackId", 3);
            cmd.Parameters.AddWithValue("$JellyfinMatchId", "xyz");
            cmd.Parameters.AddWithValue("$MatchLevel", 0);
            cmd.Parameters.AddWithValue("$MatchCriteria", 0);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var tracks = db.GetProviderTrackMatch(123);
            Assert.Empty(tracks);

            tracks = db.GetProviderTrackMatch(correctTrackDbId);
            foreach (var track in tracks)
            {
                Assert.Equal(correctJfId, track.MatchId);
                Assert.Equal(correctMatchLevel, track.Level);
                Assert.Equal(correctMatchCriteria, track.Criteria);
            }
            Assert.NotEmpty(tracks);
        }

        [Fact]
        public void CanRetrieveMatchesBySharedIsrc()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var matchA = Guid.NewGuid();

            // two provider tracks (different track ids) sharing one ISRC, and a third with a different ISRC
            var trackA = (long)db.InsertProviderTrack("Spotify", new ProviderTrackInfo { Id = "a", IsrcId = "ISRC1" })!;
            var trackB = (long)db.InsertProviderTrack("Spotify", new ProviderTrackInfo { Id = "b", IsrcId = "ISRC1" })!;
            var trackC = (long)db.InsertProviderTrack("Spotify", new ProviderTrackInfo { Id = "c", IsrcId = "ISRC2" })!;

            var configLevel = ItemMatchLevel.IgnoreCase;
            var configCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName;

            // compatible match on A (stricter level, superset criteria)
            db.InsertProviderTrackMatch(trackA, matchA.ToString(), ItemMatchLevel.Default, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            db.InsertProviderTrackMatch(trackC, Guid.NewGuid().ToString(), ItemMatchLevel.Default, configCriteria);

            // resolving B reuses A's match via the shared ISRC
            var results = db.GetProviderTrackMatchesByIsrc("ISRC1", trackB, configLevel, configCriteria).ToList();
            Assert.Single(results);
            Assert.Equal(matchA, results[0].MatchId);

            // excluding A (the only track with a match for ISRC1) yields nothing
            Assert.Empty(db.GetProviderTrackMatchesByIsrc("ISRC1", trackA, configLevel, configCriteria));

            // a different ISRC must not surface A's match
            Assert.DoesNotContain(db.GetProviderTrackMatchesByIsrc("ISRC2", trackB, configLevel, configCriteria), m => m.MatchId == matchA);

            // empty / null ISRC returns nothing
            Assert.Empty(db.GetProviderTrackMatchesByIsrc(string.Empty, trackB, configLevel, configCriteria));
            Assert.Empty(db.GetProviderTrackMatchesByIsrc(null, trackB, configLevel, configCriteria));
        }

        [Fact]
        public void GetMatchesBySharedIsrcFiltersIncompatible()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var trackA = (long)db.InsertProviderTrack("Spotify", new ProviderTrackInfo { Id = "a", IsrcId = "ISRC1" })!;
            var trackB = (long)db.InsertProviderTrack("Spotify", new ProviderTrackInfo { Id = "b", IsrcId = "ISRC1" })!;

            var configLevel = ItemMatchLevel.Default;
            var configCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName;

            // looser level -> filtered out
            db.InsertProviderTrackMatch(trackA, Guid.NewGuid().ToString(), ItemMatchLevel.Fuzzy, configCriteria);
            // missing a configured criterion (no AlbumName) -> filtered out (also exercises the bitwise-AND parens)
            db.InsertProviderTrackMatch(trackA, Guid.NewGuid().ToString(), ItemMatchLevel.Default, ItemMatchCriteria.TrackName);

            Assert.Empty(db.GetProviderTrackMatchesByIsrc("ISRC1", trackB, configLevel, configCriteria));
        }
    }
}
