using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class CacheFinder : ITrackMatchFinder
    {
        private readonly ILibraryManager _libraryManager;
        private readonly DbRepository _dbRepository;

        public CacheFinder(
            ILibraryManager libraryManager,
            DbRepository dbRepository)
        {
            _libraryManager = libraryManager;
            _dbRepository = dbRepository;
        }

        public bool IsEnabled => true;

        public Task<Audio?> FindTrackAsync(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled || Plugin.Instance == null)
            {
                return Task.FromResult<Audio?>(null);
            }

            var trackId = _dbRepository.GetProviderTrackDbId(providerId, providerTrackInfo.Id);
            if (trackId == null)
            {
                return Task.FromResult<Audio?>(null);
            }

            var matches = _dbRepository.GetProviderTrackMatch((long)trackId);
            var match = matches.FirstOrDefault(
                potentialMatch =>
                {
                    // check if the cached match has compatible match level and criteria (meaning same or stricter requirements)
                    var isLevelApplicable = potentialMatch?.Level <= Plugin.Instance.Configuration.ItemMatchLevel;
                    var isCritApplicable = (potentialMatch?.Criteria & Plugin.Instance.Configuration.ItemMatchCriteria) == Plugin.Instance.Configuration.ItemMatchCriteria;
                    return isLevelApplicable && isCritApplicable;
                },
                null);

            if (match != null)
            {
                var item = _libraryManager.GetItemById<Audio>(match.MatchId);
                return Task.FromResult(item);
            }

            return Task.FromResult<Audio?>(null);
        }
    }
}
