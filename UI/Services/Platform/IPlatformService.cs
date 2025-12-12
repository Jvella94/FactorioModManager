using System.Threading.Tasks;

namespace FactorioModManager.Services.Platform
{
    public interface IPlatformService
    {
        Task<string?> PickFileAsync(string title, string[] patterns);
        Task<string?> PickFolderAsync(string title);
        Task<bool> LaunchProcessAsync(string path);
    }
}
