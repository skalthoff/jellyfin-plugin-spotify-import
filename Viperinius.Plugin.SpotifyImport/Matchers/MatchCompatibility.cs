namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    /// <summary>
    /// Shared rules deciding whether a previously stored match is acceptable under the currently configured
    /// match level and criteria. Kept in a single place so the in-memory check and the SQL filter that both
    /// reuse cached matches cannot drift apart.
    /// </summary>
    internal static class MatchCompatibility
    {
        /// <summary>
        /// Determines whether a cached match (made at <paramref name="candidateLevel"/> /
        /// <paramref name="candidateCriteria"/>) is acceptable under the configured requirements.
        /// </summary>
        /// <param name="candidateLevel">The match level the cached match was made at.</param>
        /// <param name="candidateCriteria">The criteria the cached match satisfied.</param>
        /// <param name="configuredLevel">The currently configured match level.</param>
        /// <param name="configuredCriteria">The currently configured match criteria.</param>
        /// <returns>True if the cached match is at least as strict as the current configuration.</returns>
        internal static bool IsApplicable(ItemMatchLevel candidateLevel, ItemMatchCriteria candidateCriteria, ItemMatchLevel configuredLevel, ItemMatchCriteria configuredCriteria)
        {
            // a cached match qualifies if it was made at the same or a stricter level (lower enum value = stricter)
            // and it satisfied at least the configured set of criteria (its criteria is a superset of the configured one)
            return candidateLevel <= configuredLevel && (candidateCriteria & configuredCriteria) == configuredCriteria;
        }

        /// <summary>
        /// Builds the SQL equivalent of <see cref="IsApplicable"/> for use in a WHERE clause.
        /// </summary>
        /// <param name="levelColumn">The column holding the stored match level.</param>
        /// <param name="criteriaColumn">The column holding the stored match criteria.</param>
        /// <param name="levelParam">The bound parameter holding the configured level.</param>
        /// <param name="criteriaParam">The bound parameter holding the configured criteria.</param>
        /// <returns>A parenthesised SQL boolean expression.</returns>
        internal static string BuildSqlPredicate(string levelColumn, string criteriaColumn, string levelParam, string criteriaParam)
        {
            // mirrors IsApplicable in SQL. SQLite binds '&' tighter than '=', so "criteria & param = param" already
            // groups as "(criteria & param) = param"; the parentheses are kept for clarity and to match the C#
            // IsApplicable above, where they ARE required (C# gives '&' lower precedence than '==').
            return $"({levelColumn} <= {levelParam} AND ({criteriaColumn} & {criteriaParam}) = {criteriaParam})";
        }
    }
}
