using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NPC.Data;

namespace NPC.Runtime
{
    /// <summary>
    /// 运行时裁缝服务管理 - 管理随机化的背包升级选项
    /// </summary>
    public class RuntimeTailorServices
    {
        private List<BagUpgrade> availableUpgrades;
        private Dictionary<string, int> upgradeUseCounts; // 记录升级使用次数
        
        public RuntimeTailorServices()
        {
            availableUpgrades = new List<BagUpgrade>();
            upgradeUseCounts = new Dictionary<string, int>();
        }
        
        /// <summary>
        /// 初始化随机升级选项
        /// </summary>
        public void InitializeRandomized(TailorData tailorData, float seed)
        {
            if (tailorData == null || tailorData.bagUpgrades == null || tailorData.bagUpgrades.Count == 0)
            {
                Debug.LogError("[RuntimeTailorServices] 无效的裁缝数据!");
                return;
            }
            
            // 如果不随机化，使用全部升级选项
            if (!tailorData.randomizeUpgrades)
            {
                availableUpgrades = new List<BagUpgrade>(tailorData.bagUpgrades);
                return;
            }
            
            // 随机选择升级选项数量
            int upgradeCount = Random.Range(tailorData.minUpgradeOptions, tailorData.maxUpgradeOptions + 1);
            upgradeCount = Mathf.Min(upgradeCount, tailorData.bagUpgrades.Count);
            
            // 使用ServiceRandomizer选择升级选项
            availableUpgrades = ServiceRandomizer.RandomSelect(tailorData.bagUpgrades, upgradeCount, seed);
            
            // 按所需格子数排序，从小到大
            availableUpgrades = availableUpgrades.OrderBy(u => u.requiredCurrentSlots).ToList();
            
            Debug.Log($"[RuntimeTailorServices] 生成了 {availableUpgrades.Count} 个背包升级选项");
        }
        
        /// <summary>
        /// 获取可用的升级选项
        /// </summary>
        public List<BagUpgrade> GetAvailableUpgrades()
        {
            return new List<BagUpgrade>(availableUpgrades);
        }
        
        /// <summary>
        /// 获取适合当前背包大小的升级选项
        /// </summary>
        public List<BagUpgrade> GetSuitableUpgrades(int currentSlots)
        {
            return availableUpgrades.Where(u => 
                u.requiredCurrentSlots <= currentSlots && 
                u.maxSlots > currentSlots
            ).ToList();
        }
        
        /// <summary>
        /// 获取指定升级选项
        /// </summary>
        public BagUpgrade GetUpgrade(string upgradeName)
        {
            return availableUpgrades.Find(u => u.upgradeName == upgradeName);
        }
        
        /// <summary>
        /// 获取下一个可用的升级
        /// </summary>
        public BagUpgrade GetNextUpgrade(int currentSlots)
        {
            // 找到第一个适合当前格子数的升级
            return availableUpgrades.FirstOrDefault(u => 
                u.requiredCurrentSlots <= currentSlots && 
                u.maxSlots > currentSlots
            );
        }
        
        /// <summary>
        /// 记录升级使用
        /// </summary>
        public void RecordUpgradeUse(string upgradeName)
        {
            if (upgradeUseCounts.ContainsKey(upgradeName))
            {
                upgradeUseCounts[upgradeName]++;
            }
            else
            {
                upgradeUseCounts[upgradeName] = 1;
            }
        }
        
        /// <summary>
        /// 获取升级使用次数
        /// </summary>
        public int GetUpgradeUseCount(string upgradeName)
        {
            return upgradeUseCounts.TryGetValue(upgradeName, out int count) ? count : 0;
        }
        
        /// <summary>
        /// 计算升级价格（可能根据使用次数调整）
        /// </summary>
        public int CalculateUpgradePrice(BagUpgrade upgrade, float priceMultiplier)
        {
            float basePrice = upgrade.upgradeFee;
            
            // 每次使用后价格增加20%
            int useCount = GetUpgradeUseCount(upgrade.upgradeName);
            if (useCount > 0)
            {
                basePrice *= Mathf.Pow(1.2f, useCount);
            }
            
            // 应用价格倍率
            basePrice *= priceMultiplier;
            
            return Mathf.RoundToInt(basePrice);
        }
        
        /// <summary>
        /// 获取存档数据
        /// </summary>
        public TailorServicesSaveData GetSaveData()
        {
            return new TailorServicesSaveData
            {
                availableUpgradeNames = availableUpgrades.Select(u => u.upgradeName).ToList(),
                upgradeUseCounts = new Dictionary<string, int>(upgradeUseCounts)
            };
        }
        
        /// <summary>
        /// 加载存档数据
        /// </summary>
        public void LoadSaveData(TailorServicesSaveData saveData, TailorData tailorData)
        {
            if (saveData == null || tailorData == null) return;
            
            // 恢复可用升级选项
            availableUpgrades.Clear();
            foreach (var upgradeName in saveData.availableUpgradeNames)
            {
                var upgrade = tailorData.bagUpgrades.Find(u => u.upgradeName == upgradeName);
                if (upgrade != null)
                {
                    availableUpgrades.Add(upgrade);
                }
            }
            
            // 按所需格子数重新排序
            availableUpgrades = availableUpgrades.OrderBy(u => u.requiredCurrentSlots).ToList();
            
            // 恢复使用计数
            upgradeUseCounts = new Dictionary<string, int>(saveData.upgradeUseCounts);
            
            Debug.Log($"[RuntimeTailorServices] 加载了 {availableUpgrades.Count} 个背包升级选项");
        }
    }
    
    /// <summary>
    /// 裁缝服务存档数据
    /// </summary>
    [System.Serializable]
    public class TailorServicesSaveData
    {
        public List<string> availableUpgradeNames;
        public Dictionary<string, int> upgradeUseCounts;
    }
}