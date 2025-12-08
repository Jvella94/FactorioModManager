using System.IO.Compression;
namespace FactorioModManager.Tests.TestData
{
    class TestDataGenerator
    {
        static void GenerateTestData()
        {
            var testDir = Path.Combine("TestData", "test-mods");
            Directory.CreateDirectory(testDir);

            CreateTestMod("test-mod-1.0.0", "1.0.0", "Test Author");
            CreateTestMod("test-mod-1.1.0", "1.1.0", "Test Author");

            Console.WriteLine("✅ Test data generated!");
        }

        async static void CreateTestMod(string name, string version, string author)
        {
            var zipPath = Path.Combine("TestData", "test-mods", $"{name}.zip");
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            var infoJson = $$"""
        {
            "name": "{{name}}",
            "title": "Test {{name}}",
            "version": "{{version}}",
            "author": "{{author}}",
            "description": "This is a test mod for testing.",
            "dependencies": ["base >= 1.1.0"]
        }
        """;

            var entry = archive.CreateEntry("info.json");
            using var writer = entry.Open();
            using var streamWriter = new StreamWriter(writer);
            await streamWriter.WriteAsync(infoJson);
        }
    }

}
