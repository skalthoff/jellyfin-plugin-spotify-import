#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    public class InitTests
    {
        private static readonly Dictionary<string, List<(string, string)>> _tableCols = new Dictionary<string, List<(string, string)>>
        {
            { "ProviderPlaylists", [("Id", "INTEGER"), ("ProviderId", "TEXT"), ("PlaylistId", "TEXT"), ("LastState", "TEXT"), ("LastTimestamp", "TEXT")] },
            { "ProviderTracks", [("Id", "INTEGER"), ("ProviderId", "TEXT"), ("TrackId", "TEXT"), ("Name", "TEXT"), ("AlbumName", "TEXT"), ("AlbumArtistNames", "TEXT"), ("ArtistNames", "TEXT"), ("Number", "INTEGER"), ("IsrcId", "TEXT")] },
            { "ProviderPlaylistTracks", [("Id", "INTEGER"), ("PlaylistId", "INTEGER"), ("TrackId", "INTEGER"), ("Position", "INTEGER")] },
            { "ProviderTrackMatches", [("Id", "INTEGER"), ("TrackId", "INTEGER"), ("JellyfinMatchId", "TEXT"), ("MatchLevel", "INTEGER"), ("MatchCriteria", "INTEGER")] },
            { "IsrcMusicBrainzChecks", [("Id", "INTEGER"), ("Isrc", "TEXT"), ("LastCheck", "TEXT")] },
            { "IsrcMusicBrainzRecordingMapping", [("Id", "INTEGER"), ("Isrc", "TEXT"), ("MusicBrainzRecordingId", "TEXT")] },
            { "IsrcMusicBrainzReleaseMapping", [("Id", "INTEGER"), ("Isrc", "TEXT"), ("MusicBrainzReleaseId", "TEXT"), ("MusicBrainzTrackId", "TEXT")] },
            { "IsrcMusicBrainzReleaseGroupMapping", [("Id", "INTEGER"), ("Isrc", "TEXT"), ("MusicBrainzReleaseGroupId", "TEXT")] },
        };

        [Fact]
        public void ReturnsPath()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            Assert.Equal(":memory:", db.Path);
        }

        [Fact]
        public void SupportsForeignKeys()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys;";
            var result = (long?)cmd.ExecuteScalar();
            Assert.Equal(1, result);
        }

        [Fact]
        public void SetupTables()
        {
            using var db = DbRepositoryWrapper.GetInstance();
            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            var tableRowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tableRowCount++;
                }
            }

            Assert.Equal(0, tableRowCount);

            db.InitDb();

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tableRowCount++;
                    var colRowCount = 0;
                    var name = reader.GetString(0);
                    Assert.True(_tableCols.TryGetValue(name, out var colVal));

                    using var colCmd = db.WrappedConnection.CreateCommand();
                    colCmd.CommandText = $"SELECT name, type FROM pragma_table_info('{name}')";
                    using var colReader = colCmd.ExecuteReader();
                    while (colReader.Read())
                    {
                        colRowCount++;
                        Assert.Contains((colReader.GetString(0), colReader.GetString(1)), colVal);
                    }
                }
            }

            Assert.Equal(_tableCols.Count, tableRowCount);
        }

        [Fact]
        public void SetupIndexes()
        {
            var expectedIndexes = new HashSet<string>
            {
                "IX_ProviderTracks_Provider_Track",
                "IX_ProviderPlaylists_Provider_Playlist",
                "IX_ProviderTrackMatches_TrackId",
                "IX_ProviderPlaylistTracks_PlaylistId",
                "IX_IsrcMusicBrainzRecordingMapping_Isrc",
                "IX_IsrcMusicBrainzReleaseMapping_Isrc",
                "IX_IsrcMusicBrainzReleaseGroupMapping_Isrc",
            };

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var actualIndexes = new HashSet<string>();
            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'index' AND name LIKE 'IX_%'";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    actualIndexes.Add(reader.GetString(0));
                }
            }

            Assert.Equal(expectedIndexes, actualIndexes);

            // re-running InitDb must stay idempotent (CREATE INDEX IF NOT EXISTS)
            db.InitDb();
        }

        [Fact]
        public void AppliesPerformancePragmas()
        {
            using var db = DbRepositoryWrapper.GetInstance();

            using var syncCmd = db.WrappedConnection.CreateCommand();
            syncCmd.CommandText = "PRAGMA synchronous;";
            Assert.Equal(1L, (long?)syncCmd.ExecuteScalar()); // NORMAL

            using var tempStoreCmd = db.WrappedConnection.CreateCommand();
            tempStoreCmd.CommandText = "PRAGMA temp_store;";
            Assert.Equal(2L, (long?)tempStoreCmd.ExecuteScalar()); // MEMORY
        }

        [Fact]
        public void EnablesWalJournalOnFileDatabase()
        {
            // journal_mode=WAL is a no-op for in-memory dbs, so verify it on a real file db
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"spotifyimport_test_{Guid.NewGuid():N}.db");
            try
            {
                using (var db = new DbRepositoryWrapper(path))
                {
                    using var cmd = db.WrappedConnection.CreateCommand();
                    cmd.CommandText = "PRAGMA journal_mode;";
                    Assert.Equal("wal", ((string?)cmd.ExecuteScalar())?.ToLowerInvariant());
                }
            }
            finally
            {
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
                {
                    if (System.IO.File.Exists(path + suffix))
                    {
                        System.IO.File.Delete(path + suffix);
                    }
                }
            }
        }
    }
}
