using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using Moq;

namespace FMM.Tests.ServicesTests
{
    public class ModListServiceTests
    {
        [Fact]
        public void SaveAndLoadLists_Roundtrip()
        {
            var temp = Path.Combine(Path.GetTempPath(), $"modlists_test_{Path.GetRandomFileName()}");
            Directory.CreateDirectory(Path.GetDirectoryName(temp) ?? Path.GetTempPath());
            var file = Path.Combine(Path.GetTempPath(), $"modlists_{Path.GetRandomFileName()}.json");

            var mockLog = new Mock<ILogService>();
            var svc = new ModListService(mockLog.Object, file);

            var lists = new List<CustomModList>
            {
                new CustomModList { Name = "list1", Description = "desc", Entries = [new ModListEntry { Name = "a", Enabled = true }] },
                new CustomModList { Name = "list2", Description = "desc2", Entries = [new ModListEntry { Name = "b", Enabled = false }] }
            };

            svc.SaveLists(lists);
            var loaded = svc.LoadLists();

            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, l => l.Name == "list1" && l.Entries.Any(e => e.Name == "a"));
            File.Delete(file);
        }

        [Fact]
        public void AddUpdateDelete_Workflow()
        {
            var file = Path.Combine(Path.GetTempPath(), $"modlists_{Path.GetRandomFileName()}.json");
            var mockLog = new Mock<ILogService>();
            var svc = new ModListService(mockLog.Object, file);

            var list = new CustomModList { Name = "test", Description = "d", Entries = [] };
            svc.AddList(list);

            var loaded = svc.LoadLists();
            Assert.Single(loaded);

            svc.UpdateList("test", new CustomModList { Name = "test2", Description = "d2", Entries = [] });
            loaded = svc.LoadLists();
            Assert.Single(loaded);
            Assert.Equal("test2", loaded[0].Name);

            svc.DeleteList("test2");
            loaded = svc.LoadLists();
            Assert.Empty(loaded);

            File.Delete(file);
        }
    }
}