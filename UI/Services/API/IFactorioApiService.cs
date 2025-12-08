using FactorioModManager.Models.API;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public interface IFactorioApiService
    {
        Task<ModDetailsShort?> GetModDetailsAsync(string modName);
        Task<ModDetailsFull?> GetModDetailsFullAsync(string modName);
        Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo);
        void ClearCache(); 
    }
}
