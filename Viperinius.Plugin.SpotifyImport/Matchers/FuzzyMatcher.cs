using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fastenshtein;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class FuzzyMatcher : IItemMatcher<string>
    {
        // Strings at or below this length keep the legacy absolute-distance behaviour. A length-normalised ratio is
        // meaningless on very short fragments (one edit on a 4-char string is already a 0.75 ratio), and the comparer
        // is fed parens-split fragments, not just whole titles — so a tiny absolute budget stays the right tool there.
        private const int ShortFragmentMaxLength = 7;

        private const double DefaultMinRatio = 0.85;

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var distance = Levenshtein.Distance(target, item);
            var maxLength = Math.Max(target.Length, item.Length);
            if (maxLength == 0)
            {
                return true;
            }

            // Short fragments: keep the absolute character budget (length-normalised ratios are unstable here).
            if (maxLength <= ShortFragmentMaxLength)
            {
                var maxDistance = Plugin.Instance?.Configuration.MaxFuzzyCharDifference ?? 0;
                return distance <= maxDistance;
            }

            // Longer strings: require a length-normalised similarity ratio, so the tolerance scales with title length
            // instead of a fixed budget that is too lenient for short titles and too strict for long ones.
            var minRatio = Plugin.Instance?.Configuration.MinFuzzyMatchRatio ?? DefaultMinRatio;
            if (minRatio <= 0 || minRatio > 1)
            {
                // guard against an unset / out-of-range config value matching everything
                minRatio = DefaultMinRatio;
            }

            var ratio = 1.0 - ((double)distance / maxLength);
            return ratio >= minRatio;
        }
    }
}
