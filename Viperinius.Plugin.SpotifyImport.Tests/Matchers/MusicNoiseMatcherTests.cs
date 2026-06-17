using Viperinius.Plugin.SpotifyImport.Matchers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Matchers
{
    public class MusicNoiseMatcherTests
    {
        private readonly MusicNoiseMatcher _matcher = new MusicNoiseMatcher();

        [Theory]
        // remaster tags (incl. the unparenthesised dash form the existing parens matcher cannot strip)
        [InlineData("Bohemian Rhapsody", "Bohemian Rhapsody - 2011 Remaster", true)]
        [InlineData("Bohemian Rhapsody", "Bohemian Rhapsody - Remastered 2011", true)]
        [InlineData("Bohemian Rhapsody", "Bohemian Rhapsody (Remastered)", true)]
        [InlineData("Imagine", "Imagine - Digitally Remastered", true)]
        // featuring credits
        [InlineData("Stay", "Stay (feat. Justin Bieber)", true)]
        [InlineData("Stay", "Stay feat. Justin Bieber", true)]
        [InlineData("Stay", "Stay [ft. Justin Bieber]", true)]
        // Pt. <-> Part canonicalisation
        [InlineData("Sad Song, Part 1", "Sad Song, Pt. 1", true)]
        // genuinely different recordings must NOT be stripped (conservative by design)
        [InlineData("Song", "Song (Remix)", false)]
        [InlineData("Song", "Song (Live)", false)]
        [InlineData("Song", "Song (Acoustic)", false)]
        [InlineData("Song", "Song (Instrumental)", false)]
        // not the same track
        [InlineData("Some Song", "Another Song", false)]
        public void Matches_StripsConservativeNoiseOnly(string jfName, string provName, bool shouldMatch)
        {
            Assert.Equal(shouldMatch, _matcher.Matches(jfName, provName));
        }

        [Fact]
        public void StripNoise_DoesNotTouchUnrelatedWords()
        {
            // "apt"/"empty" contain "pt" but are not standalone Part tokens
            Assert.Equal("Apt Pupil", MusicNoiseMatcher.StripNoise("Apt Pupil"));
        }
    }
}
