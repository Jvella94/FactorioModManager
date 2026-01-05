using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class ModListService(ILogService logService, string listsFilePath) : IModListService
    {
        private readonly string _listsFilePath = listsFilePath;
        private readonly ILogService _logService = logService;

        public ModListService(ILogService logService) : this(logService, Path.Combine(FolderPathHelper.GetModsDirectory(), "custom-mod-lists.json"))
        {
        }

        public List<CustomModList> LoadLists()
        {
            if (!File.Exists(_listsFilePath))
                return [];

            try
            {
                var json = File.ReadAllText(_listsFilePath);
                var collection = JsonSerializer.Deserialize<CustomModListCollection>(json);
                return collection?.Lists ?? [];
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load custom mod lists", ex);
                return [];
            }
        }

        public void SaveLists(List<CustomModList> lists)
        {
            try
            {
                var collection = new CustomModListCollection { Lists = lists };
                var json = JsonSerializer.Serialize(collection, Constants.JsonOptions.IndentedOnly);
                File.WriteAllText(_listsFilePath, json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving custom mod lists: {ex.Message}", ex);
            }
        }

        public void AddList(CustomModList list)
        {
            var lists = LoadLists();
            lists.Add(list);
            SaveLists(lists);
        }

        public void UpdateList(string oldName, CustomModList updated)
        {
            var lists = LoadLists();
            var idx = lists.FindIndex(l => l.Name == oldName);
            if (idx >= 0)
            {
                lists[idx] = updated;
                SaveLists(lists);
            }
        }

        public void DeleteList(string name)
        {
            var lists = LoadLists();
            var item = lists.FirstOrDefault(l => l.Name == name);
            if (item != null)
            {
                lists.Remove(item);
                SaveLists(lists);
            }
        }
    }
}