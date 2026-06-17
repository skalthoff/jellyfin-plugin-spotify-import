#pragma warning disable CA1819

using System;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Sync;

namespace Viperinius.Plugin.SpotifyImport.Configuration
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            Version = string.Empty;
            SpotifyClientId = string.Empty;
            SpotifyCookie = string.Empty;
            SpotifyTotpSecretsUrl = "https://raw.githubusercontent.com/xyloflake/spot-secrets-go/refs/heads/main/secrets/secretBytes.json";
            Playlists = Array.Empty<TargetPlaylistConfiguration>();
            Users = Array.Empty<TargetUserConfiguration>();
            SpotifySavedTracksDisplayName = "Liked Songs";
            SpotifySavedTracksImageUrl = "https://misc.scdn.co/liked-songs/liked-songs-300.jpg";
            ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            ItemMatchLevel = ItemMatchLevel.Default;
            MaxFuzzyCharDifference = 2;
            MinFuzzyMatchRatio = 0.85;
            MaxDurationDifferenceSeconds = 30;
            MissingTrackListsDateFormat = "yyyy-MM-dd_HH-mm";
        }

        /// <summary>
        /// Gets or sets the config version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable verbose logging (ex: spotify requests).
        /// </summary>
        public bool EnableVerboseLogging { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show completeness information.
        /// </summary>
        public bool ShowCompletenessInformation { get; set; }

        /// <summary>
        /// Gets or sets the Spotify client ID.
        /// </summary>
        public string SpotifyClientId { get; set; }

        /// <summary>
        /// Gets or sets the Spotify session cookies.
        /// </summary>
        public string SpotifyCookie { get; set; }

        /// <summary>
        /// Gets or sets the URL to retrieve Spotify TOTP secrets from.
        /// </summary>
        public string SpotifyTotpSecretsUrl { get; set; }

        /// <summary>
        /// Gets or sets the targeted playlists.
        /// </summary>
        public TargetPlaylistConfiguration[] Playlists { get; set; }

        /// <summary>
        /// Gets or sets the target users.
        /// </summary>
        public TargetUserConfiguration[] Users { get; set; }

        /// <summary>
        /// Gets or sets the display name to use when importing saved tracks / liked songs from Spotify.
        /// </summary>
        public string SpotifySavedTracksDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the playlist cover image to use when importing saved tracks / liked songs from Spotify.
        /// </summary>
        public string SpotifySavedTracksImageUrl { get; set; }

        /// <summary>
        /// Gets the track comparison criteria.
        /// </summary>
        internal ItemMatchCriteria ItemMatchCriteria => (ItemMatchCriteria)ItemMatchCriteriaRaw;

        /// <summary>
        /// Gets or sets the track comparison criteria.
        /// </summary>
        public int ItemMatchCriteriaRaw { get; set; }

        /// <summary>
        /// Gets or sets the track comparison level.
        /// </summary>
        public ItemMatchLevel ItemMatchLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum amount of character differences to be acceptable as fuzzy match.
        /// Used for short titles (where a length-normalised ratio is unstable).
        /// </summary>
        public int MaxFuzzyCharDifference { get; set; }

        /// <summary>
        /// Gets or sets the minimum length-normalised similarity ratio (0..1) required for a fuzzy match of longer
        /// titles. Higher is stricter; 1.0 requires an exact match. Defaults to 0.85.
        /// </summary>
        public double MinFuzzyMatchRatio { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the legacy way of comparing tracks.
        /// </summary>
        public bool UseLegacyMatching { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to reject matches whose duration differs from the Spotify
        /// track by more than <see cref="MaxDurationDifferenceSeconds"/>. Duration is always used as a tie-breaker
        /// regardless of this flag; this only enables the additional hard rejection.
        /// </summary>
        public bool EnableDurationLimit { get; set; }

        /// <summary>
        /// Gets or sets the maximum acceptable difference (in seconds) between the Spotify track duration and a
        /// candidate match when <see cref="EnableDurationLimit"/> is enabled.
        /// </summary>
        public int MaxDurationDifferenceSeconds { get; set; }

        /// <summary>
        /// Gets the enabled track match finders.
        /// </summary>
        internal EnabledTrackMatchFinders EnabledTrackMatchFinders => (EnabledTrackMatchFinders)EnabledTrackMatchFindersRaw;

        /// <summary>
        /// Gets or sets the enabled track match finders.
        /// </summary>
        public int EnabledTrackMatchFindersRaw { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the creation of files containing missing tracks on the server.
        /// </summary>
        public bool GenerateMissingTrackLists { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep created missing track files (instead of storing in tmp dir).
        /// </summary>
        public bool KeepMissingTrackLists { get; set; }

        /// <summary>
        /// Gets or sets the date time format for the filenames of missing tracks.
        /// </summary>
        public string MissingTrackListsDateFormat { get; set; }

        /// <summary>
        /// Gets the list of existing files with missing tracks.
        /// </summary>
        [XmlIgnore]
        public string[] MissingTrackListPaths => MissingTrackStore.GetFileList().ToArray();

        /// <summary>
        /// Gets or sets the Spotify auth token.
        /// </summary>
        [XmlElement(IsNullable = true)]
        public SpotifyAPI.Web.PKCETokenResponse? SpotifyAuthToken { get; set; }
    }
}
