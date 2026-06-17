using MediaBrowser.Controller.Entities.Audio;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    /// <summary>
    /// Checks whether a library audio item satisfies a configured set of match criteria against a provider track.
    /// Shared by the legacy matcher and the ISRC reuse finder so both enforce the user's configured strictness
    /// (track name, album, artists, album artists) the same way.
    /// </summary>
    internal static class TrackCriteriaMatcher
    {
        /// <summary>
        /// Determines whether <paramref name="item"/> matches <paramref name="trackInfo"/> for every criterion in
        /// <paramref name="criteria"/> at the given <paramref name="level"/>.
        /// </summary>
        /// <param name="item">The candidate library item.</param>
        /// <param name="trackInfo">The provider track being matched.</param>
        /// <param name="level">The configured match level.</param>
        /// <param name="criteria">The configured criteria to enforce.</param>
        /// <param name="failedCriterium">The first criterion that failed, or None if all passed.</param>
        /// <returns>True if all configured criteria match.</returns>
        internal static bool Matches(Audio item, ProviderTrackInfo trackInfo, ItemMatchLevel level, ItemMatchCriteria criteria, out ItemMatchCriteria failedCriterium)
        {
            failedCriterium = ItemMatchCriteria.None;

            if (criteria.HasFlag(ItemMatchCriteria.Artists) && !TrackComparison.ArtistOneContained(item, trackInfo, level))
            {
                failedCriterium = ItemMatchCriteria.Artists;
                return false;
            }

            if (criteria.HasFlag(ItemMatchCriteria.AlbumName) && !TrackComparison.AlbumNameEqual(item, trackInfo, level).ComparisonResult)
            {
                failedCriterium = ItemMatchCriteria.AlbumName;
                return false;
            }

            if (criteria.HasFlag(ItemMatchCriteria.AlbumArtists) && !TrackComparison.AlbumArtistOneContained(item, trackInfo, level))
            {
                failedCriterium = ItemMatchCriteria.AlbumArtists;
                return false;
            }

            if (criteria.HasFlag(ItemMatchCriteria.TrackName) && !TrackComparison.TrackNameEqual(item, trackInfo, level).ComparisonResult)
            {
                failedCriterium = ItemMatchCriteria.TrackName;
                return false;
            }

            return true;
        }
    }
}
