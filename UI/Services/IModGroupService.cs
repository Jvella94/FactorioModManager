using FactorioModManager.Models;
using System.Collections.Generic;

namespace FactorioModManager.Services
{
    public interface IModGroupService
    {
        List<ModGroup> LoadGroups();

        void SaveGroups(List<ModGroup> groups);

        void AddGroup(ModGroup group);

        void UpdateGroup(string oldName, ModGroup updatedGroup);

        void DeleteGroup(string groupName);
    }
}