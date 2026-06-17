using Viperinius.Plugin.SpotifyImport.Sync;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    public class MatchStatsTests
    {
        [Fact]
        public void MatchedSumsFinderHitsOnly()
        {
            var stats = new MatchStats
            {
                TotalTracks = 15,
                CacheHits = 2,
                ManualMapHits = 1,
                MusicBrainzHits = 3,
                StringMatchHits = 4,
                AlreadyInPlaylist = 1,
                NewlyAdded = 9,
                Missing = 5,
            };

            // Matched counts only the finder buckets; AlreadyInPlaylist/NewlyAdded/Missing are not added in
            Assert.Equal(10, stats.Matched);
        }

        [Fact]
        public void AddAccumulatesEveryCounter()
        {
            var a = new MatchStats { TotalTracks = 1, CacheHits = 1, ManualMapHits = 2, MusicBrainzHits = 3, StringMatchHits = 4, AlreadyInPlaylist = 5, NewlyAdded = 6, Missing = 7 };
            var b = new MatchStats { TotalTracks = 10, CacheHits = 10, ManualMapHits = 20, MusicBrainzHits = 30, StringMatchHits = 40, AlreadyInPlaylist = 50, NewlyAdded = 60, Missing = 70 };

            a.Add(b);

            Assert.Equal(11, a.TotalTracks);
            Assert.Equal(11, a.CacheHits);
            Assert.Equal(22, a.ManualMapHits);
            Assert.Equal(33, a.MusicBrainzHits);
            Assert.Equal(44, a.StringMatchHits);
            Assert.Equal(55, a.AlreadyInPlaylist);
            Assert.Equal(66, a.NewlyAdded);
            Assert.Equal(77, a.Missing);
        }
    }
}
