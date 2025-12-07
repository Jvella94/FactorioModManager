using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FactorioModManager.Services
{
    public static class ModPathHelper
    {
        public static string GetModsDirectory()
        {
            string baseDir;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(baseDir, "Factorio", "mods");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(baseDir, ".factorio", "mods");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(baseDir, "Library", "Application Support", "factorio", "mods");
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system");
            }
        }

        public static string GetModListPath()
        {
            return Path.Combine(GetModsDirectory(), "mod-list.json");
        }
    }
}
