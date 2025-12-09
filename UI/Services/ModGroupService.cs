using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class ModGroupService : IModGroupService
    {
        private readonly string _groupsFilePath;
        private readonly ILogService _logService;

        public ModGroupService(ILogService logService)
        {
            var modsDir = ModPathHelper.GetModsDirectory();
            _groupsFilePath = Path.Combine(modsDir, "mod-groups.json");
            _logService = logService;
        }

        public List<ModGroup> LoadGroups()
        {
            if (!File.Exists(_groupsFilePath))
            {
                return [];
            }

            try
            {
                var json = File.ReadAllText(_groupsFilePath);
                var collection = JsonSerializer.Deserialize<ModGroupCollection>(json);
                return collection?.Groups ?? [];
            }
            catch
            {
                return [];
            }
        }

        public void SaveGroups(List<ModGroup> groups)
        {
            try
            {
                var collection = new ModGroupCollection { Groups = groups };
                var json = JsonSerializer.Serialize(collection, Constants.JsonHelper.IndentedOnly);
                File.WriteAllText(_groupsFilePath, json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving groups: {ex.Message}", ex);
            }
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
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            if (group != null)
            {
                groups.Remove(group);
                SaveGroups(groups);
            }
        }
    }
}