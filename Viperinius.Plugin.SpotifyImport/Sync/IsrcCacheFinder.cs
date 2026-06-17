using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    /// <summary>
    /// Reuses a match already resolved for a different provider track that shares the same ISRC.
    /// The same recording often appears under several provider track ids (across playlists, or single vs album),
    /// so once any of them is resolved the rest can reuse that result instead of re-running the finder pipeline.
    /// Works regardless of whether MusicBrainz matching is enabled; each reuse is re-validated by name.
    /// </summary>
    internal class IsrcCacheFinder : ITrackMatchFinder
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly DbRepository _dbRepository;

        public IsrcCacheFinder(
            ILogger logger,
            ILibraryManager libraryManager,
            DbRepository dbRepository)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _dbRepository = dbRepository;
        }

        public bool IsEnabled => true;

        public Task<Audio?> FindTrackAsync(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled || Plugin.Instance == null || string.IsNullOrWhiteSpace(providerTrackInfo.IsrcId))
            {
                return Task.FromResult<Audio?>(null);
            }

            var ownDbId = _dbRepository.GetProviderTrackDbId(providerId, providerTrackInfo.Id) ?? -1;
            var level = Plugin.Instance.Configuration.ItemMatchLevel;
            var criteria = Plugin.Instance.Configuration.ItemMatchCriteria;

            // materialise before touching the library manager so the sqlite reader/command are disposed first
            var candidates = _dbRepository.GetProviderTrackMatchesByIsrc(
                providerTrackInfo.IsrcId,
                ownDbId,
                level,
                criteria).ToList();

            foreach (var candidate in candidates)
            {
                var item = _libraryManager.GetItemById<Audio>(candidate.MatchId);
                if (item == null)
                {
                    continue;
                }

                // the ISRC identifies the recording, but ISRCs are not perfectly unique in practice (re-issues,
                // compilations, label data errors). Re-validate the reused item against the FULL configured
                // criteria before accepting it, so cross-track reuse honours the user's configured strictness
                // (e.g. an album mismatch is rejected and resolution falls through to the regular finders).
                if (TrackCriteriaMatcher.Matches(item, providerTrackInfo, level, criteria, out _))
                {
                    if (Plugin.Instance.Configuration.EnableVerboseLogging)
                    {
                        _logger.LogInformation(
                            "Reused match (item id {Id}) from another track sharing ISRC {Isrc}",
                            item.Id,
                            providerTrackInfo.IsrcId);
                    }

                    return Task.FromResult<Audio?>(item);
                }
            }

            return Task.FromResult<Audio?>(null);
        }
    }
}
