using UnityEngine;
using NPC.Core;
using NPC.Types;
using NPC.Data;
using Inventory;
using Inventory.Items;
using Inventory.Managers;
using AI.Stats;
using System.Linq;

namespace NPC.AI
{
    /// <summary>
    /// 处理AI与NPC的自动交互
    /// </summary>
    public static class NPCAIInteractionHandler
    {
        /// <summary>
        /// 处理AI与商人的交互
        /// </summary>
        public static bool HandleMerchantAIInteraction(MerchantNPC merchant, GameObject ai, string request)
        {
            var inventory = ai.GetComponent<Inventory.Inventory>();
            var currencyManager = ai.GetComponent<CurrencyManager>();
            var aiStats = ai.GetComponent<AIStats>();
            
            if (inventory == null || currencyManager == null || aiStats == null) return false;
            
            var runtimeInventory = merchant.GetRuntimeInventory();
            if (runtimeInventory == null) return false;
            
            switch (request)
            {
                case "buy_health_potion":
                    return BuyHealthPotion(merchant, ai, inventory, currencyManager);
                    
                case "buy_food":
                    return BuyFood(merchant, ai, inventory, currencyManager);
                    
                case "buy_water":
                    return BuyWater(merchant, ai, inventory, currencyManager);
                    
                default:
                    Debug.LogWarning($"[NPCAIInteraction] Unknown request: {request}");
                    return false;
            }
        }
        
        /// <summary>
        /// 处理AI与医生的交互
        /// </summary>
        public static bool HandleDoctorAIInteraction(DoctorNPC doctor, GameObject ai, string request)
        {
            var currencyManager = ai.GetComponent<CurrencyManager>();
            var aiStats = ai.GetComponent<AIStats>();
            
            if (currencyManager == null || aiStats == null) return false;
            
            switch (request)
            {
                case "heal":
                    // 检查金币
                    int healCost = 50; // 基础治疗费用
                    if (currencyManager.CurrentGold >= healCost)
                    {
                        currencyManager.SpendGold(healCost);
                        
                        // 恢复生命值
                        float healAmount = aiStats.Config.maxHealth * 0.5f; // 恢复50%生命
                        aiStats.ModifyStat(StatType.Health, healAmount, StatChangeReason.Interact);
                        
                        Debug.Log($"[NPCAIInteraction] {ai.name} 在医生处治疗，花费 {healCost} 金币");
                        return true;
                    }
                    break;
            }
            
            return false;
        }
        
        /// <summary>
        /// 处理AI与餐厅的交互
        /// </summary>
        public static bool HandleRestaurantAIInteraction(RestaurantNPC restaurant, GameObject ai, string request)
        {
            var currencyManager = ai.GetComponent<CurrencyManager>();
            var aiStats = ai.GetComponent<AIStats>();
            
            if (aiStats == null) return false;
            
            switch (request)
            {
                case "water":
                    // 免费喝水
                    aiStats.ModifyStat(StatType.Thirst, 50f, StatChangeReason.Interact);
                    Debug.Log($"[NPCAIInteraction] {ai.name} 在餐厅喝了免费水");
                    return true;
                    
                case "food":
                    if (currencyManager != null && currencyManager.CurrentGold >= 20)
                    {
                        currencyManager.SpendGold(20);
                        aiStats.ModifyStat(StatType.Hunger, 50f, StatChangeReason.Interact);
                        Debug.Log($"[NPCAIInteraction] {ai.name} 在餐厅吃饭，花费 20 金币");
                        return true;
                    }
                    break;
            }
            
            return false;
        }
        
        /// <summary>
        /// 处理AI与铁匠的交互
        /// </summary>
        public static bool HandleBlacksmithAIInteraction(BlacksmithNPC blacksmith, GameObject ai, string request)
        {
            var inventory = ai.GetComponent<Inventory.Inventory>();
            var currencyManager = ai.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            switch (request)
            {
                case "upgrade_weapon":
                    var weapon = inventory.EquippedWeapon;
                    if (weapon != null)
                    {
                        // 获取升级管理器
                        var upgradeManager = Managers.WeaponUpgradeManager.Instance;
                        if (upgradeManager != null)
                        {
                            int currentLevel = upgradeManager.GetWeaponUpgradeLevel(weapon);
                            if (currentLevel < 10) // 假设最高10级
                            {
                                int upgradeCost = 100 * (currentLevel + 1);
                                if (currencyManager.CurrentGold >= upgradeCost)
                                {
                                    currencyManager.SpendGold(upgradeCost);
                                    // 简单的升级处理 - 实际需要通过铁匠的升级系统
                                    var upgradeData = new WeaponUpgrade
                                    {
                                        damageIncrease = 5,
                                        attackSpeedIncrease = 0.1f,
                                        rangeIncrease = 0.1f,
                                        critChanceIncrease = 0.02f
                                    };
                                    upgradeManager.ApplyUpgrade(weapon, upgradeData);
                                    Debug.Log($"[NPCAIInteraction] {ai.name} 升级了武器 {weapon.ItemName}，花费 {upgradeCost} 金币");
                                    return true;
                                }
                            }
                        }
                    }
                    break;
            }
            
            return false;
        }
        
        /// <summary>
        /// 处理AI与裁缝的交互
        /// </summary>
        public static bool HandleTailorAIInteraction(TailorNPC tailor, GameObject ai, string request)
        {
            var inventory = ai.GetComponent<Inventory.Inventory>();
            var currencyManager = ai.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            switch (request)
            {
                case "expand_bag":
                    int expansionCost = 500;
                    if (currencyManager.CurrentGold >= expansionCost)
                    {
                        currencyManager.SpendGold(expansionCost);
                        inventory.ExpandCapacity(5); // 扩容5格
                        Debug.Log($"[NPCAIInteraction] {ai.name} 扩容了背包，花费 {expansionCost} 金币");
                        return true;
                    }
                    break;
            }
            
            return false;
        }
        
        // 辅助方法
        private static bool BuyHealthPotion(MerchantNPC merchant, GameObject ai, Inventory.Inventory inventory, CurrencyManager currencyManager)
        {
            var runtimeInventory = merchant.GetRuntimeInventory();
            var healthPotions = runtimeInventory.GetAllItems()
                .Where(item => item.item != null && 
                       item.item.ItemName.Contains("生命") || item.item.ItemName.ToLower().Contains("health"))
                .ToList();
                
            if (healthPotions.Count > 0)
            {
                var potion = healthPotions[0];
                float price = merchant.GetItemPrice(potion);
                
                if (currencyManager.CurrentGold >= price && potion.currentStock > 0)
                {
                    if (merchant.PurchaseItem(ai, potion.item.ItemId, 1))
                    {
                        Debug.Log($"[NPCAIInteraction] {ai.name} 购买了生命药水");
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private static bool BuyFood(MerchantNPC merchant, GameObject ai, Inventory.Inventory inventory, CurrencyManager currencyManager)
        {
            var runtimeInventory = merchant.GetRuntimeInventory();
            var foods = runtimeInventory.GetAllItems()
                .Where(item => item.item != null && 
                       item.item is ConsumableItem consumable &&
                       (item.item.ItemName.Contains("食") || item.item.ItemName.Contains("面包") || 
                        item.item.ItemName.ToLower().Contains("food") || item.item.ItemName.ToLower().Contains("bread")))
                .ToList();
                
            if (foods.Count > 0)
            {
                var food = foods[0];
                float price = merchant.GetItemPrice(food);
                
                if (currencyManager.CurrentGold >= price && food.currentStock > 0)
                {
                    if (merchant.PurchaseItem(ai, food.item.ItemId, 1))
                    {
                        Debug.Log($"[NPCAIInteraction] {ai.name} 购买了食物");
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private static bool BuyWater(MerchantNPC merchant, GameObject ai, Inventory.Inventory inventory, CurrencyManager currencyManager)
        {
            var runtimeInventory = merchant.GetRuntimeInventory();
            var waters = runtimeInventory.GetAllItems()
                .Where(item => item.item != null && 
                       item.item.ItemName.Contains("水") || item.item.ItemName.ToLower().Contains("water"))
                .ToList();
                
            if (waters.Count > 0)
            {
                var water = waters[0];
                float price = merchant.GetItemPrice(water);
                
                if (currencyManager.CurrentGold >= price && water.currentStock > 0)
                {
                    if (merchant.PurchaseItem(ai, water.item.ItemId, 1))
                    {
                        Debug.Log($"[NPCAIInteraction] {ai.name} 购买了水");
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}