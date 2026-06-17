using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Viperinius.Plugin.SpotifyImport.Matchers;
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
                potentialMatch => potentialMatch != null && MatchCompatibility.IsApplicable(
                    potentialMatch.Level,
                    potentialMatch.Criteria,
                    Plugin.Instance.Configuration.ItemMatchLevel,
                    Plugin.Instance.Configuration.ItemMatchCriteria),
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
