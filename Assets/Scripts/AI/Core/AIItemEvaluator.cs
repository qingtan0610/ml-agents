using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Inventory.Items;
using Inventory;
using AI.Stats;

namespace AI.Core
{
    /// <summary>
    /// AI物品评估系统 - 帮助AI做出智能的物品相关决策
    /// </summary>
    public class AIItemEvaluator : MonoBehaviour
    {
        [Header("Value Weights")]
        [SerializeField] private float healthValueWeight = 2f;      // 生命恢复价值权重
        [SerializeField] private float hungerValueWeight = 1.5f;    // 饥饿恢复价值权重
        [SerializeField] private float thirstValueWeight = 1.8f;    // 口渴恢复价值权重
        [SerializeField] private float weaponValueWeight = 3f;      // 武器价值权重
        [SerializeField] private float rarityMultiplier = 1.5f;     // 稀有度倍率
        
        [Header("Need Thresholds")]
        [SerializeField] private float criticalNeedThreshold = 0.3f;  // 紧急需求阈值
        [SerializeField] private float moderateNeedThreshold = 0.5f;  // 中等需求阈值
        [SerializeField] private float lowNeedThreshold = 0.7f;       // 低需求阈值
        
        // 组件引用
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        private AIGoalSystem goalSystem;
        private Inventory.Managers.CurrencyManager currencyManager;
        private Inventory.Managers.AmmoManager ammoManager;
        
        // 物品类型识别关键词
        private readonly Dictionary<string, ItemCategory> itemKeywords = new Dictionary<string, ItemCategory>
        {
            // 恢复类
            { "health", ItemCategory.Health },
            { "heal", ItemCategory.Health },
            { "potion", ItemCategory.Health },
            { "bandage", ItemCategory.Health },
            { "medicine", ItemCategory.Health },
            
            // 食物类
            { "food", ItemCategory.Food },
            { "bread", ItemCategory.Food },
            { "meat", ItemCategory.Food },
            { "fruit", ItemCategory.Food },
            { "meal", ItemCategory.Food },
            
            // 水类
            { "water", ItemCategory.Water },
            { "drink", ItemCategory.Water },
            { "juice", ItemCategory.Water },
            { "bottle", ItemCategory.Water },
            
            // 武器类
            { "sword", ItemCategory.Weapon },
            { "bow", ItemCategory.Weapon },
            { "staff", ItemCategory.Weapon },
            { "dagger", ItemCategory.Weapon },
            { "axe", ItemCategory.Weapon },
            
            // 特殊类
            { "key", ItemCategory.Key },
            { "universal", ItemCategory.Key },
            
            // Buff类
            { "buff", ItemCategory.Buff },
            { "boost", ItemCategory.Buff },
            { "enhance", ItemCategory.Buff },
            { "immunity", ItemCategory.Buff }
        };
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            goalSystem = GetComponent<AIGoalSystem>();
            currencyManager = GetComponent<Inventory.Managers.CurrencyManager>();
            ammoManager = GetComponent<Inventory.Managers.AmmoManager>();
        }
        
        /// <summary>
        /// 评估物品的总体价值
        /// </summary>
        public float EvaluateItemValue(ItemBase item, bool considerCurrentNeeds = true)
        {
            if (item == null) return 0f;
            
            float baseValue = 0f;
            
            // 1. 基础价值（价格）
            baseValue = item.BuyPrice;
            
            // 2. 类型价值
            var category = IdentifyItemCategory(item);
            float typeValue = GetCategoryValue(category);
            
            // 3. 稀有度加成
            if (item.Rarity == Inventory.ItemRarity.Rare)
                typeValue *= rarityMultiplier;
            else if (item.Rarity == Inventory.ItemRarity.Epic)
                typeValue *= rarityMultiplier * 1.5f;
            else if (item.Rarity == Inventory.ItemRarity.Legendary)
                typeValue *= rarityMultiplier * 2f;
            
            // 4. 当前需求调整
            float needMultiplier = 1f;
            if (considerCurrentNeeds)
            {
                needMultiplier = CalculateNeedMultiplier(item, category);
            }
            
            // 5. 堆叠价值
            float stackValue = 1f;
            if (item.IsStackable && item is ConsumableItem consumable)
            {
                stackValue = 1f + (consumable.MaxStackSize - 1) * 0.1f; // 堆叠能力增加价值
            }
            
            return (baseValue + typeValue) * needMultiplier * stackValue;
        }
        
        /// <summary>
        /// 评估是否应该拾取物品
        /// </summary>
        public bool ShouldPickupItem(ItemBase item)
        {
            // 特殊物品总是拾取
            if (IsSpecialItem(item)) return true;
            
            // 检查背包空间
            if (!inventory.CanAddItem(item))
            {
                // 如果背包满了，评估是否值得替换
                return IsWorthReplacing(item);
            }
            
            // 评估物品价值
            float itemValue = EvaluateItemValue(item);
            
            // 基于目标系统的决策
            if (goalSystem != null)
            {
                // 生存目标优先拾取恢复物品
                if (goalSystem.CurrentHighLevelGoal?.Type == GoalType.Survival)
                {
                    var category = IdentifyItemCategory(item);
                    if (category == ItemCategory.Health || category == ItemCategory.Food || category == ItemCategory.Water)
                    {
                        return true;
                    }
                }
                
                // 资源积累目标优先拾取高价值物品
                if (goalSystem.CurrentHighLevelGoal?.Type == GoalType.ResourceAccumulation)
                {
                    return itemValue > 50; // 价值阈值
                }
            }
            
            // 默认：价值大于20的物品都拾取
            return itemValue > 20;
        }
        
        /// <summary>
        /// 评估是否应该购买物品
        /// </summary>
        public bool ShouldBuyItem(ItemBase item, float price)
        {
            // 检查金币
            if (currencyManager.CurrentGold < price) return false;
            
            // 评估物品价值vs价格
            float itemValue = EvaluateItemValue(item);
            float priceRatio = price / itemValue;
            
            // 如果价格远高于价值，不买
            if (priceRatio > 1.5f) return false;
            
            // 基于需求的购买决策
            var category = IdentifyItemCategory(item);
            float needLevel = GetNeedLevel(category);
            
            // 紧急需求时，即使贵一点也买
            if (needLevel < criticalNeedThreshold && priceRatio < 2f)
                return true;
            
            // 中等需求，价格合理就买
            if (needLevel < moderateNeedThreshold && priceRatio < 1.2f)
                return true;
            
            // 低需求，只买便宜的
            if (needLevel < lowNeedThreshold && priceRatio < 0.8f)
                return true;
            
            // 特殊物品的购买决策
            if (category == ItemCategory.Key && !HasUniversalKey())
                return true; // 总是买钥匙
            
            if (category == ItemCategory.Weapon)
            {
                return ShouldBuyWeapon(item as WeaponItem, price);
            }
            
            return false;
        }
        
        /// <summary>
        /// 评估应该出售哪些物品
        /// </summary>
        public List<ItemBase> GetItemsToSell(float targetGold)
        {
            var itemsToSell = new List<ItemBase>();
            float currentValue = 0f;
            
            // 获取所有物品并按价值密度排序（价值/需求）
            var allItems = new List<(ItemBase item, float score)>();
            
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    var item = slot.Item;
                    var category = IdentifyItemCategory(item);
                    
                    // 不卖关键物品
                    if (IsEssentialItem(item, category)) continue;
                    
                    float value = EvaluateItemValue(item, false); // 不考虑当前需求的纯价值
                    float need = GetNeedLevel(category);
                    float score = value / (need + 0.1f); // 避免除零
                    
                    allItems.Add((item, score));
                }
            }
            
            // 按得分排序，优先卖低需求高价值的物品
            allItems = allItems.OrderByDescending(x => x.score).ToList();
            
            foreach (var (item, score) in allItems)
            {
                if (currentValue >= targetGold) break;
                
                itemsToSell.Add(item);
                currentValue += item.BuyPrice * 0.5f; // 假设卖价是原价的50%
            }
            
            return itemsToSell;
        }
        
        /// <summary>
        /// 获取当前最需要的物品类型
        /// </summary>
        public ItemCategory GetMostNeededItemCategory()
        {
            var needs = new Dictionary<ItemCategory, float>
            {
                { ItemCategory.Health, 1f - (aiStats.CurrentHealth / aiStats.Config.maxHealth) },
                { ItemCategory.Food, 1f - (aiStats.CurrentHunger / aiStats.Config.maxHunger) },
                { ItemCategory.Water, 1f - (aiStats.CurrentThirst / aiStats.Config.maxThirst) }
            };
            
            // 如果没有武器，武器需求很高
            if (inventory.EquippedWeapon == null)
            {
                needs[ItemCategory.Weapon] = 0.8f;
            }
            
            // 如果没有钥匙，钥匙需求中等
            if (!HasUniversalKey())
            {
                needs[ItemCategory.Key] = 0.5f;
            }
            
            return needs.OrderByDescending(kvp => kvp.Value).First().Key;
        }
        
        /// <summary>
        /// 评估宝箱的价值
        /// </summary>
        public float EvaluateTreasureChest(bool isLocked, float estimatedLootValue = 100f)
        {
            if (isLocked && !HasUniversalKey())
            {
                // 锁着的箱子，没钥匙就没价值
                return 0f;
            }
            
            // 基于当前需求和预期掉落价值
            float needMultiplier = 1f;
            
            // 如果很缺物资，宝箱价值更高
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f ||
                aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f ||
                aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
            {
                needMultiplier = 2f;
            }
            
            return estimatedLootValue * needMultiplier;
        }
        
        // 辅助方法
        
        public ItemCategory IdentifyItemCategory(ItemBase item)
        {
            if (item == null) return ItemCategory.Other;
            
            // 检查具体类型
            if (item is WeaponItem) return ItemCategory.Weapon;
            
            // 基于名称关键词识别
            string itemNameLower = item.ItemName.ToLower();
            foreach (var kvp in itemKeywords)
            {
                if (itemNameLower.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            
            // 基于描述识别
            if (!string.IsNullOrEmpty(item.Description))
            {
                string descLower = item.Description.ToLower();
                foreach (var kvp in itemKeywords)
                {
                    if (descLower.Contains(kvp.Key))
                    {
                        return kvp.Value;
                    }
                }
            }
            
            return ItemCategory.Other;
        }
        
        private float GetCategoryValue(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Health:
                    return 50f * healthValueWeight;
                case ItemCategory.Food:
                    return 30f * hungerValueWeight;
                case ItemCategory.Water:
                    return 25f * thirstValueWeight;
                case ItemCategory.Weapon:
                    return 100f * weaponValueWeight;
                case ItemCategory.Key:
                    return 200f; // 钥匙很有价值
                case ItemCategory.Buff:
                    return 80f;
                default:
                    return 20f;
            }
        }
        
        private float CalculateNeedMultiplier(ItemBase item, ItemCategory category)
        {
            float need = GetNeedLevel(category);
            
            // 需求越高，倍率越大
            if (need < criticalNeedThreshold)
                return 3f;
            else if (need < moderateNeedThreshold)
                return 2f;
            else if (need < lowNeedThreshold)
                return 1.5f;
            else
                return 1f;
        }
        
        private float GetNeedLevel(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Health:
                    return aiStats.CurrentHealth / aiStats.Config.maxHealth;
                case ItemCategory.Food:
                    return aiStats.CurrentHunger / aiStats.Config.maxHunger;
                case ItemCategory.Water:
                    return aiStats.CurrentThirst / aiStats.Config.maxThirst;
                case ItemCategory.Weapon:
                    return inventory.EquippedWeapon != null ? 1f : 0.2f;
                case ItemCategory.Key:
                    return HasUniversalKey() ? 1f : 0.5f;
                default:
                    return 0.7f; // 中等需求
            }
        }
        
        private bool IsSpecialItem(ItemBase item)
        {
            var category = IdentifyItemCategory(item);
            return category == ItemCategory.Key || 
                   item.Rarity >= Inventory.ItemRarity.Epic ||
                   item.BuyPrice > 500;
        }
        
        private bool IsEssentialItem(ItemBase item, ItemCategory category)
        {
            // 当前装备的武器
            if (item == inventory.EquippedWeapon) return true;
            
            // 唯一的钥匙
            if (category == ItemCategory.Key && GetKeyCount() <= 1) return true;
            
            // 紧急需求的最后物品
            if ((category == ItemCategory.Health && GetHealthItemCount() <= 1 && aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f) ||
                (category == ItemCategory.Food && GetFoodItemCount() <= 1 && aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f) ||
                (category == ItemCategory.Water && GetWaterItemCount() <= 1 && aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f))
            {
                return true;
            }
            
            return false;
        }
        
        private bool IsWorthReplacing(ItemBase newItem)
        {
            float newItemValue = EvaluateItemValue(newItem);
            
            // 找到价值最低的物品
            ItemBase lowestValueItem = null;
            float lowestValue = float.MaxValue;
            
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    var item = slot.Item;
                    var category = IdentifyItemCategory(item);
                    
                    // 不替换关键物品
                    if (IsEssentialItem(item, category)) continue;
                    
                    float value = EvaluateItemValue(item);
                    if (value < lowestValue)
                    {
                        lowestValue = value;
                        lowestValueItem = item;
                    }
                }
            }
            
            // 如果新物品价值高于最低价值物品的1.5倍，值得替换
            return lowestValueItem != null && newItemValue > lowestValue * 1.5f;
        }
        
        private bool ShouldBuyWeapon(WeaponItem weapon, float price)
        {
            if (weapon == null) return false;
            
            // 如果没有武器，优先购买
            if (inventory.EquippedWeapon == null)
            {
                return price < currencyManager.CurrentGold * 0.7f; // 不花光所有钱
            }
            
            // 比较新武器和当前武器
            var currentWeapon = inventory.EquippedWeapon;
            float currentScore = EvaluateWeaponScore(currentWeapon);
            float newScore = EvaluateWeaponScore(weapon);
            
            // 如果新武器明显更好且价格合理
            return newScore > currentScore * 1.3f && price < currencyManager.CurrentGold * 0.5f;
        }
        
        private float EvaluateWeaponScore(WeaponItem weapon)
        {
            if (weapon == null) return 0f;
            
            float score = weapon.Damage * 0.5f;
            score += weapon.AttackSpeed * 10f;
            score += weapon.AttackRange * 2f;
            
            if (weapon.WeaponType == WeaponType.Ranged)
                score *= 1.2f; // 远程武器加成
            
            return score;
        }
        
        private bool HasUniversalKey()
        {
            return GetKeyCount() > 0;
        }
        
        private int GetKeyCount()
        {
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && IdentifyItemCategory(slot.Item) == ItemCategory.Key)
                {
                    count += slot.Quantity;
                }
            }
            return count;
        }
        
        private int GetHealthItemCount()
        {
            return GetItemCountByCategory(ItemCategory.Health);
        }
        
        private int GetFoodItemCount()
        {
            return GetItemCountByCategory(ItemCategory.Food);
        }
        
        private int GetWaterItemCount()
        {
            return GetItemCountByCategory(ItemCategory.Water);
        }
        
        private int GetItemCountByCategory(ItemCategory category)
        {
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && IdentifyItemCategory(slot.Item) == category)
                {
                    count += slot.Quantity;
                }
            }
            return count;
        }
        
        private int GetTotalItemCount()
        {
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    count += slot.Quantity;
                }
            }
            return count;
        }
    }
    
    /// <summary>
    /// 物品类别
    /// </summary>
    public enum ItemCategory
    {
        Health,     // 生命恢复
        Food,       // 食物
        Water,      // 水
        Weapon,     // 武器
        Armor,      // 护甲（预留）
        Key,        // 钥匙
        Buff,       // 增益物品
        Other       // 其他
    }
}