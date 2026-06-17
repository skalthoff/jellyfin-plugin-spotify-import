namespace Viperinius.Plugin.SpotifyImport.Sync
{
    /// <summary>
    /// In-memory counters describing how a single sync run resolved its provider tracks.
    /// Aggregated per playlist and across the whole run so the outcome of the match pipeline
    /// (cache vs each finder vs missing) is visible without per-track logging.
    /// </summary>
    internal sealed class MatchStats
    {
        /// <summary>
        /// Gets or sets the total number of (non-null) provider tracks processed.
        /// </summary>
        public int TotalTracks { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks served from the result cache (includes tracks already in the playlist).
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks resolved via a manual mapping.
        /// </summary>
        public int ManualMapHits { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks resolved by reusing a match from another track sharing the same ISRC.
        /// </summary>
        public int IsrcReuseHits { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks resolved via the MusicBrainz / ISRC finder.
        /// </summary>
        public int MusicBrainzHits { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks resolved via string comparison (incl. legacy matching).
        /// </summary>
        public int StringMatchHits { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks that resolved to an item already present in the target playlist
        /// (whether via the cache short-circuit or any finder), and so were not added again.
        /// </summary>
        public int AlreadyInPlaylist { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks newly added to the target playlist this run.
        /// </summary>
        public int NewlyAdded { get; set; }

        /// <summary>
        /// Gets or sets the number of tracks that could not be matched.
        /// </summary>
        public int Missing { get; set; }

        /// <summary>
        /// Gets the total number of tracks that resolved to a library item, regardless of finder.
        /// </summary>
        public int Matched => CacheHits + ManualMapHits + IsrcReuseHits + MusicBrainzHits + StringMatchHits;

        /// <summary>
        /// Adds the counters of another instance into this one.
        /// </summary>
        /// <param name="other">The stats to accumulate.</param>
        public void Add(MatchStats other)
        {
            if (other == null)
            {
                return;
            }

            TotalTracks += other.TotalTracks;
            CacheHits += other.CacheHits;
            ManualMapHits += other.ManualMapHits;
            IsrcReuseHits += other.IsrcReuseHits;
            MusicBrainzHits += other.MusicBrainzHits;
            StringMatchHits += other.StringMatchHits;
            AlreadyInPlaylist += other.AlreadyInPlaylist;
            NewlyAdded += other.NewlyAdded;
            Missing += other.Missing;
        }
    }
}
