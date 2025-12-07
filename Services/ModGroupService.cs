using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FactorioModManager.Models;

namespace FactorioModManager.Services
{
    public class ModGroupService
    {
        private readonly string _groupsFilePath;

        public ModGroupService()
        {
            var modsDir = ModPathHelper.GetModsDirectory();
            _groupsFilePath = Path.Combine(modsDir, "mod-groups.json");
        }

        public List<ModGroup> LoadGroups()
        {
            if (!File.Exists(_groupsFilePath))
            {
                return new List<ModGroup>();
            }

            var json = File.ReadAllText(_groupsFilePath);
            var collection = JsonSerializer.Deserialize<ModGroupCollection>(json);
            return collection?.Groups ?? new List<ModGroup>();
        }

        public void SaveGroups(List<ModGroup> groups)
        {
            var collection = new ModGroupCollection { Groups = groups };
            var json = JsonSerializer.Serialize(collection, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_groupsFilePath, json);
        }

        public void AddGroup(ModGroup group)
        {
            var groups = LoadGroups();
            groups.Add(group);
            SaveGroups(groups);
        }

        public void UpdateGroup(string oldName, ModGroup updatedGroup)
        {
            var groups = LoadGroups();
            var index = groups.FindIndex(g => g.Name == oldName);
            if (index >= 0)
            {
                groups[index] = updatedGroup;
                SaveGroups(groups);
            }
        }

        public void DeleteGroup(string groupName)
        {
            var groups = LoadGroups();
            groups.RemoveAll(g => g.Name == groupName);
            SaveGroups(groups);
        }
    }
}
