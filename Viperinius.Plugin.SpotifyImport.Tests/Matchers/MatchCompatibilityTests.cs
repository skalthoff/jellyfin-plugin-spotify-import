using Viperinius.Plugin.SpotifyImport.Matchers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Matchers
{
    public class MatchCompatibilityTests
    {
        [Fact]
        public void IsApplicableStricterLevelAccepted()
        {
            // candidate matched at a stricter level (Default) than configured (IgnoreCase) -> acceptable
            Assert.True(MatchCompatibility.IsApplicable(
                ItemMatchLevel.Default,
                ItemMatchCriteria.TrackName,
                ItemMatchLevel.IgnoreCase,
                ItemMatchCriteria.TrackName));
        }

        [Fact]
        public void IsApplicableLooserLevelRejected()
        {
            // candidate matched at a looser level (Fuzzy) than configured (Default) -> not acceptable
            Assert.False(MatchCompatibility.IsApplicable(
                ItemMatchLevel.Fuzzy,
                ItemMatchCriteria.TrackName,
                ItemMatchLevel.Default,
                ItemMatchCriteria.TrackName));
        }

        [Fact]
        public void IsApplicableExactMatchAccepted()
        {
            Assert.True(MatchCompatibility.IsApplicable(
                ItemMatchLevel.IgnoreCase,
                ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName,
                ItemMatchLevel.IgnoreCase,
                ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName));
        }

        [Fact]
        public void IsApplicableCriteriaSupersetAccepted()
        {
            // candidate satisfied more criteria than required -> acceptable
            Assert.True(MatchCompatibility.IsApplicable(
                ItemMatchLevel.Default,
                ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName,
                ItemMatchLevel.Default,
                ItemMatchCriteria.TrackName));
        }

        [Fact]
        public void IsApplicableMissingCriteriaRejected()
        {
            // candidate is missing a configured criterion (AlbumName) -> not acceptable
            Assert.False(MatchCompatibility.IsApplicable(
                ItemMatchLevel.Default,
                ItemMatchCriteria.TrackName,
                ItemMatchLevel.Default,
                ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName));
        }

        [Fact]
        public void BuildSqlPredicateWrapsBitwiseAndInParentheses()
        {
            var predicate = MatchCompatibility.BuildSqlPredicate("m.MatchLevel", "m.MatchCriteria", "$Level", "$Criteria");
            Assert.Equal("(m.MatchLevel <= $Level AND (m.MatchCriteria & $Criteria) = $Criteria)", predicate);
        }
    }
}
