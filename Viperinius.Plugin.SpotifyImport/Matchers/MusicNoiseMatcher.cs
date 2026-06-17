using System.Text.RegularExpressions;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    /// <summary>
    /// Most permissive matcher: strips a conservative, well-defined set of "noise" tokens that commonly differ
    /// between Spotify and a local library for the <em>same</em> recording, then delegates to the punctuation/case
    /// matcher. Deliberately does NOT touch qualifiers that denote a genuinely different recording — (Remix),
    /// (Live), (Acoustic), (Instrumental), (Demo) — because stripping those is the main source of false matches.
    /// </summary>
    internal partial class MusicNoiseMatcher : IItemMatcher<string>
    {
        // stateless matcher: reuse one instance instead of allocating a new IgnorePunctuationMatcher on every comparison
        private static readonly IgnorePunctuationMatcher _punctuationMatcher = new IgnorePunctuationMatcher();

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            return _punctuationMatcher.Matches(StripNoise(target), StripNoise(item));
        }

        /// <summary>
        /// Removes the conservative noise token set from a title. Order matters: featuring clauses (which can wrap a
        /// remaster tag) are removed first.
        /// </summary>
        /// <param name="value">The title to normalise.</param>
        /// <returns>The title with noise tokens stripped.</returns>
        public static string StripNoise(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // "Song (feat. X)" / "Song [ft. X]" / "Song feat. X ..." -> "Song"
            var result = FeaturingRegex().Replace(value, " ");

            // "Song - 2009 Remaster" / "Song (Remastered)" / "Song - Digitally Remastered Version 2011" -> "Song"
            result = RemasterRegex().Replace(result, " ");

            // "Song, Pt. 1" / "Song Pt 2" -> "Song part 1" (so it matches a local "Song, Part 1")
            result = PartRegex().Replace(result, "part ");

            // stripping a trailing tag leaves a dangling space; trim so the punctuation matcher (which collapses
            // but does not trim whitespace) sees an exact edge match
            return result.Trim();
        }

        // parenthesised feat block anywhere, or an unparenthesised feat clause running to the end of the title
        [GeneratedRegex(@"\s*[\(\[]\s*(feat\.?|ft\.?|featuring)\s+[^\)\]]*[\)\]]|\s+(feat\.?|ft\.?|featuring)\s+.*$", RegexOptions.IgnoreCase)]
        private static partial Regex FeaturingRegex();

        // trailing remaster tag, optionally year-prefixed/suffixed, optionally "digital(ly) ... version", optionally wrapped in ()/[]
        [GeneratedRegex(@"\s*[-\(\[]\s*(\d{2,4}\s+)?(digital(ly)?\s+)?remaster(ed)?(\s+version)?(\s+\d{2,4})?\s*[\)\]]?\s*$", RegexOptions.IgnoreCase)]
        private static partial Regex RemasterRegex();

        // "pt." / "pt" as a standalone token -> canonical "part"
        [GeneratedRegex(@"\bpt\.?(?=\s|$)", RegexOptions.IgnoreCase)]
        private static partial Regex PartRegex();
    }
}
