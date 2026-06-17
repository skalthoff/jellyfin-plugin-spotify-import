using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal partial class IgnoreParensMatcher : IItemMatcher<string>
    {
        // stateless matcher: reuse one instance instead of allocating a new IgnorePunctuationMatcher on every comparison
        private static readonly IgnorePunctuationMatcher _punctuationMatcher = new IgnorePunctuationMatcher();

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var i = TheRegex().Replace(item, string.Empty);
            var t = TheRegex().Replace(target, string.Empty);
            return _punctuationMatcher.Matches(t, i);
        }

        [GeneratedRegex(@"\s*(?:\([^\)]*\)|\[[^\]]*\])\s*")] // find all occurences of (foo) or [foo]
        private static partial Regex TheRegex();
    }
}
