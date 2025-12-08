using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public interface IFactorioApiService
    {
        Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName);

        Task<ModDetailsFullDTO?> GetModDetailsFullAsync(string modName);

        Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo);

        void ClearCache();
    }
}