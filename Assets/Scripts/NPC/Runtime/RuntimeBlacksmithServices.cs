using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NPC.Data;

namespace NPC.Runtime
{
    /// <summary>
    /// 运行时铁匠服务管理 - 管理随机化的打造配方和强化选项
    /// </summary>
    public class RuntimeBlacksmithServices
    {
        private List<CraftingRecipe> availableRecipes;
        private List<WeaponUpgrade> availableUpgrades;
        private Dictionary<string, int> craftingCounts; // 记录打造次数
        
        public RuntimeBlacksmithServices()
        {
            availableRecipes = new List<CraftingRecipe>();
            availableUpgrades = new List<WeaponUpgrade>();
            craftingCounts = new Dictionary<string, int>();
        }
        
        /// <summary>
        /// 初始化随机服务
        /// </summary>
        public void InitializeRandomized(BlacksmithData blacksmithData, float seed)
        {
            if (blacksmithData == null)
            {
                Debug.LogError("[RuntimeBlacksmithServices] 无效的铁匠数据!");
                return;
            }
            
            // 初始化打造配方
            InitializeRecipes(blacksmithData, seed);
            
            // 初始化强化选项
            InitializeUpgrades(blacksmithData, seed + 1000f);
        }
        
        /// <summary>
        /// 初始化打造配方
        /// </summary>
        private void InitializeRecipes(BlacksmithData blacksmithData, float seed)
        {
            if (blacksmithData.recipes == null || blacksmithData.recipes.Count == 0)
            {
                Debug.LogWarning("[RuntimeBlacksmithServices] 没有配置打造配方");
                return;
            }
            
            // 如果不随机化，使用全部配方
            if (!blacksmithData.randomizeRecipes)
            {
                availableRecipes = new List<CraftingRecipe>(blacksmithData.recipes);
                return;
            }
            
            // 随机选择配方数量
            int recipeCount = Random.Range(blacksmithData.minRecipes, blacksmithData.maxRecipes + 1);
            recipeCount = Mathf.Min(recipeCount, blacksmithData.recipes.Count);
            
            // 使用ServiceRandomizer选择配方
            availableRecipes = ServiceRandomizer.RandomSelect(blacksmithData.recipes, recipeCount, seed);
            
            Debug.Log($"[RuntimeBlacksmithServices] 生成了 {availableRecipes.Count} 个打造配方");
        }
        
        /// <summary>
        /// 初始化强化选项
        /// </summary>
        private void InitializeUpgrades(BlacksmithData blacksmithData, float seed)
        {
            if (blacksmithData.upgradeOptions == null || blacksmithData.upgradeOptions.Count == 0)
            {
                Debug.LogWarning("[RuntimeBlacksmithServices] 没有配置强化选项");
                return;
            }
            
            // 如果不随机化，使用全部强化选项
            if (!blacksmithData.randomizeUpgrades)
            {
                availableUpgrades = new List<WeaponUpgrade>(blacksmithData.upgradeOptions);
                return;
            }
            
            // 随机选择强化类型数量
            int upgradeCount = Random.Range(blacksmithData.minUpgradeTypes, blacksmithData.maxUpgradeTypes + 1);
            upgradeCount = Mathf.Min(upgradeCount, blacksmithData.upgradeOptions.Count);
            
            // 使用ServiceRandomizer选择强化选项
            availableUpgrades = ServiceRandomizer.RandomSelect(blacksmithData.upgradeOptions, upgradeCount, seed);
            
            Debug.Log($"[RuntimeBlacksmithServices] 生成了 {availableUpgrades.Count} 种强化选项");
        }
        
        /// <summary>
        /// 获取可用的打造配方
        /// </summary>
        public List<CraftingRecipe> GetAvailableRecipes()
        {
            return new List<CraftingRecipe>(availableRecipes);
        }
        
        /// <summary>
        /// 获取可用的强化选项
        /// </summary>
        public List<WeaponUpgrade> GetAvailableUpgrades()
        {
            return new List<WeaponUpgrade>(availableUpgrades);
        }
        
        /// <summary>
        /// 获取指定配方
        /// </summary>
        public CraftingRecipe GetRecipe(string recipeName)
        {
            return availableRecipes.Find(r => r.recipeName == recipeName);
        }
        
        /// <summary>
        /// 获取指定强化选项
        /// </summary>
        public WeaponUpgrade GetUpgrade(string upgradeName)
        {
            return availableUpgrades.Find(u => u.upgradeName == upgradeName);
        }
        
        /// <summary>
        /// 记录打造次数
        /// </summary>
        public void RecordCrafting(string recipeName)
        {
            if (craftingCounts.ContainsKey(recipeName))
            {
                craftingCounts[recipeName]++;
            }
            else
            {
                craftingCounts[recipeName] = 1;
            }
        }
        
        /// <summary>
        /// 获取打造次数
        /// </summary>
        public int GetCraftingCount(string recipeName)
        {
            return craftingCounts.TryGetValue(recipeName, out int count) ? count : 0;
        }
        
        /// <summary>
        /// 获取存档数据
        /// </summary>
        public BlacksmithServicesSaveData GetSaveData()
        {
            return new BlacksmithServicesSaveData
            {
                availableRecipeNames = availableRecipes.Select(r => r.recipeName).ToList(),
                availableUpgradeNames = availableUpgrades.Select(u => u.upgradeName).ToList(),
                craftingCounts = new Dictionary<string, int>(craftingCounts)
            };
        }
        
        /// <summary>
        /// 加载存档数据
        /// </summary>
        public void LoadSaveData(BlacksmithServicesSaveData saveData, BlacksmithData blacksmithData)
        {
            if (saveData == null || blacksmithData == null) return;
            
            // 恢复可用配方
            availableRecipes.Clear();
            foreach (var recipeName in saveData.availableRecipeNames)
            {
                var recipe = blacksmithData.recipes.Find(r => r.recipeName == recipeName);
                if (recipe != null)
                {
                    availableRecipes.Add(recipe);
                }
            }
            
            // 恢复可用强化选项
            availableUpgrades.Clear();
            foreach (var upgradeName in saveData.availableUpgradeNames)
            {
                var upgrade = blacksmithData.upgradeOptions.Find(u => u.upgradeName == upgradeName);
                if (upgrade != null)
                {
                    availableUpgrades.Add(upgrade);
                }
            }
            
            // 恢复打造计数
            craftingCounts = new Dictionary<string, int>(saveData.craftingCounts);
            
            Debug.Log($"[RuntimeBlacksmithServices] 加载了 {availableRecipes.Count} 个配方和 {availableUpgrades.Count} 种强化选项");
        }
    }
    
    /// <summary>
    /// 铁匠服务存档数据
    /// </summary>
    [System.Serializable]
    public class BlacksmithServicesSaveData
    {
        public List<string> availableRecipeNames;
        public List<string> availableUpgradeNames;
        public Dictionary<string, int> craftingCounts;
    }
}