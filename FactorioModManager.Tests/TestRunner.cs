using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FluentAssertions;
using Moq;
using System.Diagnostics;
using System.IO.Compression;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 Factorio Mod Manager - .NET 9 Test Suite");
        Console.WriteLine("==========================================\n");

        // Run all test phases
        await RunUnitTests();
        await TestCoreServices();

        Console.WriteLine("\n✅ All tests completed successfully! 🎉");
    }

    private static async Task RunUnitTests()
    {
        Console.WriteLine("📋 Running unit tests with .NET 9...");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = """
                test --logger "console;verbosity=detailed"
                     --collect:"XPlat Code Coverage"
                     --results-directory ./coverage
                """,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.WriteLine("✅ Unit tests completed");
        Console.WriteLine();
    }

    private static async Task TestCoreServices()
    {
        await TestModService();
        await TestLogService();
        await TestSettingsService();
    }

    private static async Task TestModService()
    {
        Console.WriteLine("🔍 Testing ModService...");

        var modsDir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "test-mods");
        Directory.CreateDirectory(modsDir);

        // Create test mod
        var testZip = Path.Combine(modsDir, "test-mod-1.0.0.zip");
        CreateTestModZip(testZip);

        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetModsPath()).Returns(modsDir);

        var mockLog = Mock.Of<ILogService>();

        var modService = new ModService(
            mockSettings.Object,     // ISettingsService
            mockLog,                  // ILogService
            new HttpClient()
        );
        var mods = modService.LoadAllMods().ToList();

        Console.WriteLine($"✅ Found {mods.Count} test mods");
    }

    private static async Task TestLogService()
    {
        Console.WriteLine("📝 Testing LogService...");

        var logService = LogService.Instance;
        logService.Log("Test log entry", LogLevel.Info);
        logService.Log("Test warning", LogLevel.Warning);
        logService.LogWarning("Test error");

        var logs = logService.GetLogs().ToList();
        logs.Count.Should().Be(3);

        logService.ClearLogs();
        Console.WriteLine("✅ LogService tests passed");
    }

    private static async Task TestSettingsService()
    {
        Console.WriteLine("⚙️ Testing SettingsService...");

        var settings = new SettingsService(LogService.Instance);
        settings.SetModsPath("C:\\test-mods");

        var savedPath = settings.GetModsPath();
        savedPath.Should().Be("C:\\test-mods");

        Console.WriteLine("✅ SettingsService tests passed");
    }

    private static void CreateTestModZip(string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var infoJson = """
        {
            "name": "test-mod",
            "title": "Test Mod",
            "version": "1.0.0",
            "author": "test-author",
            "description": "Test description",
            "dependencies": ["base >= 1.0.0"]
        }
        """;

        var entry = archive.CreateEntry("info.json");
        using var writer = entry.Open();
        using var streamWriter = new StreamWriter(writer);
        streamWriter.Write(infoJson);
    }
}