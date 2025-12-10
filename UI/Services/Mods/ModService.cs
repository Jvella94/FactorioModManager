using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;

namespace FactorioModManager.Services.Mods
{
    public interface IModService
    {
        List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods();
        Result ToggleMod(string modName, bool enabled);
        Result RemoveMod(string modName, string filePath);
        ModInfo? ReadModInfo(string filePath);
    }

    public class ModService(IModRepository repository, ILogService logService) : IModService
    {
        private readonly IModRepository _repository = repository;
        private readonly ILogService _logService = logService;

        public List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods()
        {
            return _repository.LoadAllMods();
        }

        public Result ToggleMod(string modName, bool enabled)
        {
            try
            {
                _repository.SaveModState(modName, enabled);
                _logService.Log($"Toggled {modName}: {(enabled ? "enabled" : "disabled")}");
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error toggling mod {modName}", ex);
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        public Result RemoveMod(string modName, string filePath)
        {
            try
            {
                // Delete the mod file/directory
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logService.Log($"Deleted mod file: {filePath}");
                }
                else if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, recursive: true);
                    _logService.Log($"Deleted mod directory: {filePath}");
                }
                else
                {
                    _logService.LogWarning($"Mod file/directory not found: {filePath}");
                    return Result.Fail("Mod not found", ErrorCode.FileNotFound);
                }

                // Update mod-list.json
                try
                {
                    var states = _repository.LoadEnabledStates();
                    if (states.Remove(modName))
                    {
                        _repository.SaveEnabledStates(states);
                        _logService.Log($"Removed {modName} from mod-list.json");
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Error updating mod-list.json after removal: {ex.Message}");
                }

                _logService.Log($"Successfully removed mod: {modName}");
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing mod {modName}", ex);
                return Result.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        public ModInfo? ReadModInfo(string filePath)
        {
            return _repository.ReadModInfo(filePath);
        }
    }
}