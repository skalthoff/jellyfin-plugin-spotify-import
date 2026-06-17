using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities.Audio;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Matchers
{
    public sealed class MatchingFeatureTests : IDisposable
    {
        private const long TicksPerSecond = 10_000_000;

        public void Dispose()
        {
            TrackHelper.ClearAlbums();
        }

        private static Audio JfWithDuration(string name, long? runtimeSeconds)
        {
            var audio = TrackHelper.CreateJfItem(name, "Album", "Artist On Album", "Just Artist");
            audio.RunTimeTicks = runtimeSeconds == null ? null : runtimeSeconds * TicksPerSecond;
            return audio;
        }

        private static ProviderTrackInfo ProvWithDuration(string name, long? durationMs)
        {
            var prov = TrackHelper.CreateProviderItem(name, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            prov.DurationMs = durationMs;
            return prov;
        }

        // ---------- #1 length-normalised fuzzy ratio ----------

        [Theory]
        // long titles: tolerance scales with length (min ratio 0.85 by default)
        [InlineData("aaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaab", true)]   // 20 chars, 1 edit  -> 0.95
        [InlineData("aaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaXYZ", true)]   // 20 chars, 3 edits -> 0.85 (boundary)
        [InlineData("aaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaWXYZ1", false)]  // 20 chars, 5 edits -> 0.75
        // short titles: keep the absolute character budget (MaxFuzzyCharDifference defaults to 2)
        [InlineData("aaaa", "aaab", true)]                                   // 1 edit  <= 2
        [InlineData("aaaa", "abcd", false)]                                  // 3 edits >  2
        public void Fuzzy_ScalesWithTitleLength(string jfName, string provName, bool shouldMatch)
        {
            TrackHelper.SetValidPluginInstance();
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            Assert.Equal(shouldMatch, TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Fuzzy).ComparisonResult);
        }

        // ---------- #2 duration helpers ----------

        [Fact]
        public void DurationDeltaMs_ReturnsAbsoluteDelta_OrNullWhenUnknown()
        {
            var prov = ProvWithDuration("Track", 181_000);
            Assert.Equal(1_000, TrackComparison.DurationDeltaMs(JfWithDuration("Track", 180), prov));

            // either side unknown -> null (caller must treat as "do not gate")
            Assert.Null(TrackComparison.DurationDeltaMs(JfWithDuration("Track", null), prov));
            Assert.Null(TrackComparison.DurationDeltaMs(JfWithDuration("Track", 180), ProvWithDuration("Track", null)));
        }

        [Fact]
        public void CompareMatchCandidates_BreaksTiesByClosestDuration_UnknownLast()
        {
            var prov = ProvWithDuration("Track", 200_000);
            var close = (2, ItemMatchLevel.Default, JfWithDuration("Track", 200));
            var far = (2, ItemMatchLevel.Default, JfWithDuration("Track", 100));
            var unknown = (2, ItemMatchLevel.Default, JfWithDuration("Track", null));

            Assert.True(TrackComparison.CompareMatchCandidates(far, close, prov) > 0);   // close sorts first
            Assert.True(TrackComparison.CompareMatchCandidates(close, far, prov) < 0);
            Assert.True(TrackComparison.CompareMatchCandidates(close, unknown, prov) < 0); // unknown sorts last
            Assert.True(TrackComparison.CompareMatchCandidates(unknown, close, prov) > 0);

            // a better (prio, level) still wins regardless of duration
            var betterPrioFar = (1, ItemMatchLevel.Default, JfWithDuration("Track", 100));
            Assert.True(TrackComparison.CompareMatchCandidates(betterPrioFar, close, prov) < 0);
        }

        [Fact]
        public void DurationExceedsLimit_OnlyWhenEnabled_AndDeltaKnownAndOverMax()
        {
            var prov = ProvWithDuration("Track", 250_000); // 250 s
            var jf = JfWithDuration("Track", 180);          // 180 s -> 70 s delta

            Assert.True(TrackComparison.DurationExceedsLimit(jf, prov, enabled: true, maxSeconds: 30));
            Assert.False(TrackComparison.DurationExceedsLimit(jf, prov, enabled: true, maxSeconds: 120));
            Assert.False(TrackComparison.DurationExceedsLimit(jf, prov, enabled: false, maxSeconds: 30));
            // unknown duration is never rejected
            Assert.False(TrackComparison.DurationExceedsLimit(JfWithDuration("Track", null), prov, enabled: true, maxSeconds: 30));
        }

        // ---------- #7 music-aware noise level ----------

        [Fact]
        public void IgnoreMusicNoiseLevel_MatchesDashRemaster_WhereFuzzyDoesNot()
        {
            TrackHelper.SetValidPluginInstance();
            var prov = TrackHelper.CreateProviderItem("Bohemian Rhapsody - 2011 Remaster", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem("Bohemian Rhapsody", "Album", "Artist On Album", "Just Artist");

            // fuzzy alone cannot bridge the long appended remaster tag ...
            Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Fuzzy).ComparisonResult);

            // ... but the noise level strips it and matches at exactly that level
            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreMusicNoise);
            Assert.True(result.ComparisonResult);
            Assert.Equal(ItemMatchLevel.IgnoreMusicNoise, result.MatchedLevel);
        }
    }
}
