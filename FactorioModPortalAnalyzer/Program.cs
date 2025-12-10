using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FactorioModPortalAnalyzer
{
    internal class Program
    {
        private static readonly HttpClient _client = new();
        private const string _bASE_URL = "https://mods.factorio.com/api/mods";
        private const int _tHROTTLE_DELAY_MS = 500;
        private const string _jSON_FILE = "factorio_2.0_mods_releases.json";
        private const string _cSV_FILE = "factorio_2.0_mods_summary.csv";

#pragma warning disable IDE0060 // Remove unused parameter

        private static async Task Main(string[] args)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            Console.WriteLine("Factorio 2.0 Mod Portal Analyzer\n");

            try
            {
                List<ModReleaseInfo> modReleases;

                // Check if data files exist
                if (File.Exists(_jSON_FILE))
                {
                    Console.WriteLine($"Found existing data file: {_jSON_FILE}");
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("  1 - Load from existing file (fast)");
                    Console.WriteLine("  2 - Fetch fresh data from API (slow)");
                    Console.Write("\nChoice [1/2]: ");

                    var choice = Console.ReadLine()?.Trim();

                    if (choice == "1" || string.IsNullOrEmpty(choice))
                    {
                        modReleases = await LoadFromFile();
                    }
                    else
                    {
                        modReleases = await FetchFromAPI();
                        await SaveDataset(modReleases, GetOptions());
                    }
                }
                else
                {
                    Console.WriteLine("No existing data found. Fetching from API...\n");
                    modReleases = await FetchFromAPI();
                    await SaveDataset(modReleases, GetOptions());
                }

                // Analyze and display trends
                AnalyzeTrends(modReleases);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _client.Dispose();
            }
        }

        private static async Task<List<ModReleaseInfo>> LoadFromFile()
        {
            Console.WriteLine($"\n📂 Loading data from {_jSON_FILE}...");

            var json = await File.ReadAllTextAsync(_jSON_FILE);
            var modReleases = JsonSerializer.Deserialize<List<ModReleaseInfo>>(json);

            Console.WriteLine($"✅ Loaded {modReleases?.Count ?? 0} mods from cache");
            return modReleases ?? [];
        }

        private static async Task<List<ModReleaseInfo>> FetchFromAPI()
        {
            Console.WriteLine("🌐 Fetching fresh data from Factorio Mod Portal...\n");

            // Get all 2.0 mods
            var allMods = await GetAll20Mods();
            Console.WriteLine($"Found {allMods.Count} mods compatible with Factorio 2.0\n");

            // Get releases for each mod
            var modReleases = await GetModReleases(allMods);

            return modReleases;
        }

        private static async Task<List<ModInfo>> GetAll20Mods()
        {
            var url = $"{_bASE_URL}?version=2.0&page_size=max";
            var response = await _client.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<ModListResponse>(response);

            return result?.Results ?? [];
        }

        private static async Task<List<ModReleaseInfo>> GetModReleases(List<ModInfo> mods)
        {
            var modReleases = new List<ModReleaseInfo>();
            int count = 0;

            foreach (var mod in mods)
            {
                count++;
                Console.WriteLine($"[{count}/{mods.Count}] Fetching releases for {mod.Name}");

                try
                {
                    var url = $"{_bASE_URL}/{mod.Name}";
                    var response = await _client.GetStringAsync(url);
                    var shortMod = JsonSerializer.Deserialize<ShortModInfo>(response);
                    if (shortMod != null)
                    {
                        modReleases.Add(new ModReleaseInfo
                        {
                            Name = mod.Name,
                            Title = mod.Title ?? shortMod.Title,
                            Owner = mod.Owner ?? shortMod.Owner,
                            Downloads = mod.DownloadsCount,
                            Releases = shortMod.Releases ?? []
                        });
                    }
                    await Task.Delay(_tHROTTLE_DELAY_MS);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Failed: {ex.Message}");
                }
            }

            return modReleases;
        }

        private static JsonSerializerOptions GetOptions()
        {
            return new JsonSerializerOptions { WriteIndented = true };
        }

        private static async Task SaveDataset(List<ModReleaseInfo> modReleases, JsonSerializerOptions options)
        {
            Console.WriteLine("\n💾 Saving dataset...");
            await File.WriteAllTextAsync(_jSON_FILE,
                JsonSerializer.Serialize(modReleases, options));
            Console.WriteLine($"  ✅ {_jSON_FILE}");

            // Save CSV summary
            var csv = new List<string> { "Name,Title,Owner,Downloads,ReleaseCount" };
            csv.AddRange(modReleases.Select(m =>
                $"\"{m.Name}\",\"{m.Title}\",\"{m.Owner}\",{m.Downloads},{m.Releases.Count}"));

            await File.WriteAllLinesAsync(_cSV_FILE, csv);
            Console.WriteLine($"  ✅ {_cSV_FILE}");

            Console.WriteLine($"\n✅ Dataset saved: {modReleases.Count} mods analyzed");
        }

        private static void AnalyzeTrends(List<ModReleaseInfo> modReleases)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("                    MOD PORTAL 2.0 ANALYSIS");
            Console.WriteLine(new string('=', 60));

            var allReleases = modReleases.SelectMany(m => m.Releases).ToList();

            // Basic stats
            Console.WriteLine($"\n📊 BASIC STATISTICS");
            Console.WriteLine($"Total Mods:          {modReleases.Count:N0}");
            Console.WriteLine($"Total Releases:      {allReleases.Count:N0}");
            Console.WriteLine($"Avg Releases/Mod:    {allReleases.Count / (double)modReleases.Count:F1}");
            Console.WriteLine($"Total Downloads:     {modReleases.Sum(m => m.Downloads):N0}");

            // Top mods by downloads
            Console.WriteLine($"\n🏆 TOP 10 DOWNLOADS");
            foreach (var mod in modReleases.OrderByDescending(m => m.Downloads).Take(10))
            {
                Console.WriteLine($"  {mod.Title,-40} {mod.Downloads:N0}");
            }

            // Top mods by release count
            Console.WriteLine($"\n🔥 TOP 10 MOST RELEASES");
            foreach (var mod in modReleases.OrderByDescending(m => m.Releases.Count).Take(10))
            {
                Console.WriteLine($"  {mod.Title,-40} {mod.Releases.Count} releases");
            }

            // Daily trends
            if (allReleases.Count != 0)
            {
                var dailyCounts = allReleases
                    .Where(r => !string.IsNullOrEmpty(r.ReleasedAt))
                    .Select(r => DateTime.Parse(r.ReleasedAt).Date)
                    .GroupBy(d => d.ToString("yyyy-MM-dd"))
                    .ToDictionary(g => g.Key, g => g.Count());

                Console.WriteLine($"\n📅 DAILY RELEASE TRENDS (Top 15)");
                foreach (var day in dailyCounts.OrderByDescending(kvp => kvp.Value).Take(15))
                {
                    Console.WriteLine($"  {day.Key,-12} {day.Value,4} releases");
                }

                // Monthly trends
                var monthlyCounts = allReleases
                    .Where(r => !string.IsNullOrEmpty(r.ReleasedAt))
                    .Select(r => DateTime.Parse(r.ReleasedAt))
                    .GroupBy(d => d.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());

                Console.WriteLine($"\n📈 MONTHLY RELEASE TRENDS (Last 12 months)");
                foreach (var month in monthlyCounts.OrderByDescending(kvp => kvp.Key).Take(12))
                {
                    Console.WriteLine($"  {month.Key,-12} {month.Value,4} releases");
                }

                // Save daily CSV
                var dailyCsv = new List<string> { "Date,Count" };
                dailyCsv.AddRange(dailyCounts.OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key},{kvp.Value}"));
                File.WriteAllLines("daily_release_trends.csv", dailyCsv);

                // Save monthly CSV
                var monthlyCsv = new List<string> { "Month,Count" };
                monthlyCsv.AddRange(monthlyCounts.OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key},{kvp.Value}"));
                File.WriteAllLines("monthly_release_trends.csv", monthlyCsv);

                Console.WriteLine("\n💾 Trend files saved:");
                Console.WriteLine("  ✅ daily_release_trends.csv");
                Console.WriteLine("  ✅ monthly_release_trends.csv");

                // Additional insights
                Console.WriteLine($"\n🔍 INSIGHTS");
                var busiestDay = dailyCounts.OrderByDescending(kvp => kvp.Value).First();
                var avgDaily = dailyCounts.Values.Average();
                var modsWithMultiple = modReleases.Count(m => m.Releases.Count > 1);
                var modsWithSingle = modReleases.Count(m => m.Releases.Count == 1);

                Console.WriteLine($"  Busiest day:         {busiestDay.Key} ({busiestDay.Value} releases)");
                Console.WriteLine($"  Average daily:       {avgDaily:F1} releases/day");
                Console.WriteLine($"  Mods w/ updates:     {modsWithMultiple} ({100.0 * modsWithMultiple / modReleases.Count:F1}%)");
                Console.WriteLine($"  Single-release mods: {modsWithSingle} ({100.0 * modsWithSingle / modReleases.Count:F1}%)");
            }

            Console.WriteLine("\n" + new string('=', 60));
        }
    }

    // Data models
    public class ModListResponse
    {
        [JsonPropertyName("results")]
        public List<ModInfo> Results { get; set; } = [];
    }

    public class ModInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public string Owner { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("downloads_count")]
        public int DownloadsCount { get; set; }
    }

    public class ShortModInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public string Owner { get; set; } = string.Empty;

        [JsonPropertyName("releases")]
        public List<Release> Releases { get; set; } = [];
    }

    public class ModReleaseInfo
    {
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public string Owner { get; set; } = "";
        public int Downloads { get; set; }
        public List<Release> Releases { get; set; } = [];
    }

    public class Release
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("released_at")]
        public string ReleasedAt { get; set; } = string.Empty;
    }
}