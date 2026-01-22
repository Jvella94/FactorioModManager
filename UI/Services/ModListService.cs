using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class ModListService : IModListService
    {
        private readonly ILogService _logService;

        // New: per-list folder and index file
        private readonly string _listsFolder;

        private readonly string _indexFilePath;

        public ModListService(ILogService logService) : this(logService, Path.Combine(FolderPathHelper.GetModsDirectory(), "custom-mod-lists"))
        {
        }

        // Primary constructor for folder-based storage
        public ModListService(ILogService logService, string listsFolder)
        {
            _logService = logService;
            _listsFolder = listsFolder;
            _indexFilePath = Path.Combine(_listsFolder, "index.json");

            try
            {
                if (!Directory.Exists(_listsFolder))
                    Directory.CreateDirectory(_listsFolder);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to create lists folder: {ex.Message}", ex);
            }
        }

        public List<CustomModList> LoadLists()
        {
            var lists = new List<CustomModList>();

            if (!File.Exists(_indexFilePath))
                return lists;

            try
            {
                var json = File.ReadAllText(_indexFilePath);
                var index = JsonSerializer.Deserialize<CustomModListIndex>(json);
                if (index?.Entries == null) return lists;

                foreach (var meta in index.Entries)
                {
                    try
                    {
                        var file = Path.Combine(_listsFolder, meta.FileName);
                        if (!File.Exists(file)) continue;

                        var listJson = File.ReadAllText(file);
                        var list = JsonSerializer.Deserialize<CustomModList>(listJson);
                        if (list != null)
                        {
                            // Ensure CreatedAt preserved from metadata if missing
                            if (list.CreatedAt == default) list.CreatedAt = meta.CreatedAt;
                            lists.Add(list);
                        }
                    }
                    catch (Exception exInner)
                    {
                        _logService.LogError($"Failed to load list file {meta.FileName}: {exInner.Message}", exInner);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load custom mod lists index", ex);
            }

            return lists;
        }

        public void SaveLists(List<CustomModList> lists)
        {
            try
            {
                // Write each list to its own file and build index
                var index = new CustomModListIndex { Entries = [] };

                foreach (var list in lists)
                {
                    // If CreatedAt not set, assign
                    if (list.CreatedAt == default) list.CreatedAt = DateTime.UtcNow;

                    var fileName = MakeSafeFileName(list.Name);
                    // Append timestamp to avoid collisions
                    fileName = fileName + "_" + list.CreatedAt.ToString("yyyyMMddHHmmss") + ".json";
                    var filePath = Path.Combine(_listsFolder, fileName);

                    var json = JsonSerializer.Serialize(list, Constants.JsonOptions.IndentedOnly);
                    File.WriteAllText(filePath, json);

                    index.Entries.Add(new CustomModListMeta
                    {
                        Name = list.Name,
                        Description = list.Description,
                        FileName = fileName,
                        CreatedAt = list.CreatedAt
                    });
                }

                var indexJson = JsonSerializer.Serialize(index, Constants.JsonOptions.IndentedOnly);
                File.WriteAllText(_indexFilePath, indexJson);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving custom mod lists: {ex.Message}", ex);
            }
        }

        public void AddList(CustomModList list)
        {
            try
            {
                // ensure CreatedAt set
                if (list.CreatedAt == default) list.CreatedAt = DateTime.UtcNow;

                var baseName = MakeSafeFileName(list.Name);
                var fileName = baseName + "_" + list.CreatedAt.ToString("yyyyMMddHHmmss") + ".json";
                var filePath = Path.Combine(_listsFolder, fileName);

                // Avoid collisions by appending a counter if needed
                var counter = 1;
                while (File.Exists(filePath))
                {
                    fileName = baseName + "_" + list.CreatedAt.ToString("yyyyMMddHHmmss") + "_" + counter + ".json";
                    filePath = Path.Combine(_listsFolder, fileName);
                    counter++;
                }

                var json = JsonSerializer.Serialize(list, Constants.JsonOptions.IndentedOnly);
                File.WriteAllText(filePath, json);

                // Update index
                var index = LoadIndex();
                index.Entries.Add(new CustomModListMeta { Name = list.Name, Description = list.Description, FileName = fileName, CreatedAt = list.CreatedAt });
                SaveIndex(index);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding custom mod list: {ex.Message}", ex);
            }
        }

        public void UpdateList(string oldName, CustomModList updated)
        {
            try
            {
                var index = LoadIndex();
                var meta = index.Entries.FirstOrDefault(e => e.Name == oldName);
                if (meta == null) return;

                var oldFilePath = Path.Combine(_listsFolder, meta.FileName);

                // If name changed, create new filename with new timestamp and remove old file
                if (!string.Equals(oldName, updated.Name, StringComparison.Ordinal))
                {
                    // assign new created timestamp
                    updated.CreatedAt = DateTime.UtcNow;

                    var baseName = MakeSafeFileName(updated.Name);
                    var newFileName = baseName + "_" + updated.CreatedAt.ToString("yyyyMMddHHmmss") + ".json";
                    var newFilePath = Path.Combine(_listsFolder, newFileName);

                    // Avoid collisions
                    var counter = 1;
                    while (File.Exists(newFilePath))
                    {
                        newFileName = baseName + "_" + updated.CreatedAt.ToString("yyyyMMddHHmmss") + "_" + counter + ".json";
                        newFilePath = Path.Combine(_listsFolder, newFileName);
                        counter++;
                    }

                    var json = JsonSerializer.Serialize(updated, Constants.JsonOptions.IndentedOnly);
                    File.WriteAllText(newFilePath, json);

                    // delete old file if exists
                    try { if (File.Exists(oldFilePath)) File.Delete(oldFilePath); } catch { }

                    // update index meta
                    meta.Name = updated.Name;
                    meta.Description = updated.Description;
                    meta.FileName = newFileName;
                    meta.CreatedAt = updated.CreatedAt;
                    SaveIndex(index);
                }
                else
                {
                    // same name: preserve createdAt and filename
                    updated.CreatedAt = meta.CreatedAt;
                    var json = JsonSerializer.Serialize(updated, Constants.JsonOptions.IndentedOnly);
                    File.WriteAllText(oldFilePath, json);

                    // update description if changed
                    meta.Description = updated.Description;
                    SaveIndex(index);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error updating custom mod list: {ex.Message}", ex);
            }
        }

        public void DeleteList(string name)
        {
            try
            {
                var index = LoadIndex();
                var meta = index.Entries.FirstOrDefault(e => e.Name == name);
                if (meta == null) return;

                var filePath = Path.Combine(_listsFolder, meta.FileName);
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }

                index.Entries.Remove(meta);
                SaveIndex(index);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting custom mod list: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export a named list to an arbitrary file path. Overwrites destination if overwrite=true.
        /// </summary>
        public bool ExportList(string name, string destinationPath, bool overwrite = false)
        {
            try
            {
                var index = LoadIndex();
                var meta = index.Entries.FirstOrDefault(e => e.Name == name);
                if (meta == null) return false;

                var filePath = Path.Combine(_listsFolder, meta.FileName);
                if (!File.Exists(filePath)) return false;

                if (File.Exists(destinationPath) && !overwrite) return false;

                File.Copy(filePath, destinationPath, overwrite);
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error exporting list {name}: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Import a list file (JSON). The imported list will be added to storage and indexed. Returns the imported list or null on failure.
        /// </summary>
        public CustomModList? ImportListFromFile(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return null;

                var json = File.ReadAllText(sourcePath);
                var list = JsonSerializer.Deserialize<CustomModList>(json);
                if (list == null) return null;

                // Set CreatedAt if missing
                if (list.CreatedAt == default) list.CreatedAt = DateTime.UtcNow;

                // If a list with same name exists, append numeric suffix to name to avoid collision
                var index = LoadIndex();
                var baseName = list.Name;
                var candidateName = baseName;
                var counter = 1;
                while (index.Entries.Any(e => string.Equals(e.Name, candidateName, StringComparison.OrdinalIgnoreCase)))
                {
                    candidateName = baseName + " (" + counter + ")";
                    counter++;
                }

                list.Name = candidateName;

                AddList(list);
                return list;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error importing list from {sourcePath}: {ex.Message}", ex);
                return null;
            }
        }

        public string? GetListFilePath(string name)
        {
            try
            {
                var index = LoadIndex();
                var meta = index.Entries.FirstOrDefault(e => e.Name == name);
                if (meta == null) return null;
                var file = Path.Combine(_listsFolder, meta.FileName);
                return File.Exists(file) ? file : null;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to get list file path for {name}: {ex.Message}", ex);
                return null;
            }
        }

        private CustomModListIndex LoadIndex()
        {
            try
            {
                if (!File.Exists(_indexFilePath))
                    return new CustomModListIndex { Entries = [] };

                var json = File.ReadAllText(_indexFilePath);
                var index = JsonSerializer.Deserialize<CustomModListIndex>(json);
                return index ?? new CustomModListIndex { Entries = [] };
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to load lists index: {ex.Message}", ex);
                return new CustomModListIndex { Entries = [] };
            }
        }

        private void SaveIndex(CustomModListIndex index)
        {
            try
            {
                var json = JsonSerializer.Serialize(index, Constants.JsonOptions.IndentedOnly);
                File.WriteAllText(_indexFilePath, json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to save lists index: {ex.Message}", ex);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            if (string.IsNullOrEmpty(name)) name = "list";
            if (name.Length > 80) name = name[..80];
            return name;
        }

        private class CustomModListIndex
        {
            public List<CustomModListMeta> Entries { get; set; } = [];
        }

        private class CustomModListMeta
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string FileName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }
    }
}