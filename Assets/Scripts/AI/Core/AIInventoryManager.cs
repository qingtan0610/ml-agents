using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Inventory;
using Inventory.Items;
using AI.Stats;

namespace AI.Core
{
    /// <summary>
    /// AI智能背包管理器 - 提供智能的背包整理和物品管理
    /// </summary>
    public class AIInventoryManager : MonoBehaviour
    {
        [Header("Management Settings")]
        [SerializeField] private float managementInterval = 10f; // 背包管理间隔
        [SerializeField] private float criticalFullnessThreshold = 0.85f; // 关键满度阈值
        [SerializeField] private float comfortableFullnessThreshold = 0.7f; // 舒适满度阈值
        [SerializeField] private bool autoSort = true; // 自动排序
        [SerializeField] private bool debugManagement = true;
        
        [Header("Priority Settings")]
        [SerializeField] private int minHealingItems = 2; // 最少治疗物品
        [SerializeField] private int minFoodItems = 2; // 最少食物
        [SerializeField] private int minDrinkItems = 1; // 最少饮料
        [SerializeField] private int maxWeapons = 2; // 最多武器数量
        
        // 组件引用
        private Inventory.Inventory inventory;
        private AIStats aiStats;
        private AITradeManager tradeManager;
        
        // 管理状态
        private float lastManagementTime;
        private Dictionary<string, int> itemPriorities = new Dictionary<string, int>();
        
        private void Awake()
        {
            inventory = GetComponent<Inventory.Inventory>();
            aiStats = GetComponent<AIStats>();
            tradeManager = GetComponent<AITradeManager>();
            
            InitializeItemPriorities();
        }
        
        private void Update()
        {
            // 定期进行背包管理
            if (Time.time - lastManagementTime > managementInterval)
            {
                PerformInventoryManagement();
                lastManagementTime = Time.time;
            }
            
            // 紧急情况下立即管理
            if (GetInventoryFullness() > criticalFullnessThreshold)
            {
                PerformEmergencyManagement();
            }
        }
        
        /// <summary>
        /// 初始化物品优先级
        /// </summary>
        private void InitializeItemPriorities()
        {
            // 基础优先级设置（数字越高优先级越高）
            itemPriorities["healing"] = 10;
            itemPriorities["food"] = 9;
            itemPriorities["drink"] = 8;
            itemPriorities["weapon"] = 7;
            itemPriorities["ammo"] = 6;
            itemPriorities["key"] = 5;
            itemPriorities["material"] = 3;
            itemPriorities["misc"] = 1;
        }
        
        /// <summary>
        /// 执行背包管理
        /// </summary>
        public void PerformInventoryManagement()
        {
            if (inventory == null) return;
            
            if (debugManagement)
            {
                Debug.Log($"[InventoryManager] {name} 开始背包管理 - 使用率: {GetInventoryFullness():F2}");
            }
            
            // 1. 分析当前背包状态
            var analysis = AnalyzeInventory();
            
            // 2. 根据分析结果执行管理策略
            ExecuteManagementStrategy(analysis);
            
            // 3. 自动排序（如果启用）
            if (autoSort)
            {
                SortInventoryByPriority();
            }
        }
        
        /// <summary>
        /// 紧急背包管理
        /// </summary>
        private void PerformEmergencyManagement()
        {
            if (debugManagement)
            {
                Debug.Log($"[InventoryManager] {name} 执行紧急背包管理");
            }
            
            // 立即丢弃最低优先级的物品
            DropLowPriorityItems(1);
        }
        
        /// <summary>
        /// 分析背包状态
        /// </summary>
        private InventoryAnalysis AnalyzeInventory()
        {
            var analysis = new InventoryAnalysis();
            
            // 统计各类物品数量
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    var item = slot.Item;
                    int quantity = slot.Quantity;
                    
                    if (item is ConsumableItem consumable)
                    {
                        if (IsHealingItem(consumable))
                        {
                            analysis.healingItems += quantity;
                            analysis.itemsByCategory["healing"].Add(new ItemAnalysis(item, quantity, i, GetItemPriority(item)));
                        }
                        else if (IsFoodItem(consumable))
                        {
                            analysis.foodItems += quantity;
                            analysis.itemsByCategory["food"].Add(new ItemAnalysis(item, quantity, i, GetItemPriority(item)));
                        }
                        else if (IsDrinkItem(consumable))
                        {
                            analysis.drinkItems += quantity;
                            analysis.itemsByCategory["drink"].Add(new ItemAnalysis(item, quantity, i, GetItemPriority(item)));
                        }
                        else
                        {
                            analysis.itemsByCategory["misc"].Add(new ItemAnalysis(item, quantity, i, GetItemPriority(item)));
                        }
                    }
                    else if (item is WeaponItem weapon)
                    {
                        analysis.weapons += quantity;
                        analysis.itemsByCategory["weapon"].Add(new ItemAnalysis(item, quantity, i, GetItemPriority(item)));
                    }
                    else
                    {
                        analysis.itemsByCategory["misc"].Add(new ItemAnalysis(item, quantity, i, GetItemPriority(item)));
                    }
                    
                    analysis.totalItems += quantity;
                    analysis.usedSlots++;
                }
            }
            
            // 计算缺少的关键物品
            analysis.missingHealingItems = Mathf.Max(0, minHealingItems - analysis.healingItems);
            analysis.missingFoodItems = Mathf.Max(0, minFoodItems - analysis.foodItems);
            analysis.missingDrinkItems = Mathf.Max(0, minDrinkItems - analysis.drinkItems);
            
            // 计算多余的物品
            analysis.excessWeapons = Mathf.Max(0, analysis.weapons - maxWeapons);
            
            return analysis;
        }
        
        /// <summary>
        /// 执行管理策略
        /// </summary>
        private void ExecuteManagementStrategy(InventoryAnalysis analysis)
        {
            float fullness = GetInventoryFullness();
            
            if (fullness > criticalFullnessThreshold)
            {
                // 危险：立即清理空间
                ExecuteCriticalStrategy(analysis);
            }
            else if (fullness > comfortableFullnessThreshold)
            {
                // 警告：优化背包
                ExecuteOptimizationStrategy(analysis);
            }
            else
            {
                // 正常：维护策略
                ExecuteMaintenanceStrategy(analysis);
            }
        }
        
        /// <summary>
        /// 危急策略 - 立即释放空间
        /// </summary>
        private void ExecuteCriticalStrategy(InventoryAnalysis analysis)
        {
            if (debugManagement)
            {
                Debug.Log($"[InventoryManager] {name} 执行危急策略");
            }
            
            // 1. 丢弃多余的武器（保留最好的）
            if (analysis.excessWeapons > 0)
            {
                DropExcessWeapons(analysis);
            }
            
            // 2. 减少数量过多的消耗品
            ReduceExcessConsumables(analysis);
            
            // 3. 如果还不够，考虑交易或出售
            if (GetInventoryFullness() > criticalFullnessThreshold && tradeManager != null)
            {
                // 尝试与附近AI交易
                // 这将通过AITradeManager自动处理
            }
        }
        
        /// <summary>
        /// 优化策略 - 整理背包
        /// </summary>
        private void ExecuteOptimizationStrategy(InventoryAnalysis analysis)
        {
            if (debugManagement)
            {
                Debug.Log($"[InventoryManager] {name} 执行优化策略");
            }
            
            // 1. 合并相同物品
            ConsolidateItems();
            
            // 2. 丢弃质量较低的重复物品
            RemoveDuplicateItems(analysis);
            
            // 3. 调整物品数量到合理范围
            BalanceItemQuantities(analysis);
        }
        
        /// <summary>
        /// 维护策略 - 保持最佳状态
        /// </summary>
        private void ExecuteMaintenanceStrategy(InventoryAnalysis analysis)
        {
            if (debugManagement)
            {
                Debug.Log($"[InventoryManager] {name} 执行维护策略");
            }
            
            // 1. 确保有足够的关键物品
            EnsureMinimumItems(analysis);
            
            // 2. 定期整理
            if (autoSort)
            {
                SortInventoryByPriority();
            }
        }
        
        /// <summary>
        /// 丢弃多余的武器
        /// </summary>
        private void DropExcessWeapons(InventoryAnalysis analysis)
        {
            var weapons = analysis.itemsByCategory["weapon"]
                .OrderBy(w => GetWeaponQuality(w.item as WeaponItem))
                .Take(analysis.excessWeapons);
                
            foreach (var weapon in weapons)
            {
                inventory.DropItem(weapon.slotIndex, weapon.quantity);
                if (debugManagement)
                {
                    Debug.Log($"[InventoryManager] {name} 丢弃多余武器: {weapon.item.ItemName}");
                }
            }
        }
        
        /// <summary>
        /// 减少过量消耗品
        /// </summary>
        private void ReduceExcessConsumables(InventoryAnalysis analysis)
        {
            // 如果某类消耗品数量超过需要的3倍，减少到2倍
            if (analysis.healingItems > minHealingItems * 3)
            {
                int toReduce = analysis.healingItems - minHealingItems * 2;
                ReduceItemsByCategory("healing", analysis, toReduce);
            }
            
            if (analysis.foodItems > minFoodItems * 3)
            {
                int toReduce = analysis.foodItems - minFoodItems * 2;
                ReduceItemsByCategory("food", analysis, toReduce);
            }
        }
        
        /// <summary>
        /// 减少特定类别的物品
        /// </summary>
        private void ReduceItemsByCategory(string category, InventoryAnalysis analysis, int amount)
        {
            var items = analysis.itemsByCategory[category]
                .OrderBy(item => item.priority)
                .ToList();
                
            int reduced = 0;
            foreach (var item in items)
            {
                if (reduced >= amount) break;
                
                int toRemove = Mathf.Min(item.quantity, amount - reduced);
                inventory.DropItem(item.slotIndex, toRemove);
                reduced += toRemove;
                
                if (debugManagement)
                {
                    Debug.Log($"[InventoryManager] {name} 减少{category}: {item.item.ItemName} x{toRemove}");
                }
            }
        }
        
        /// <summary>
        /// 合并相同物品
        /// </summary>
        private void ConsolidateItems()
        {
            // Unity的背包系统通常会自动合并，这里是额外的优化
            // 可以实现更复杂的合并逻辑
        }
        
        /// <summary>
        /// 移除重复物品
        /// </summary>
        private void RemoveDuplicateItems(InventoryAnalysis analysis)
        {
            // 对于武器，只保留最好的
            var weaponGroups = analysis.itemsByCategory["weapon"]
                .GroupBy(w => w.item.ItemId)
                .Where(g => g.Count() > 1);
                
            foreach (var group in weaponGroups)
            {
                var bestWeapon = group.OrderByDescending(w => GetWeaponQuality(w.item as WeaponItem)).First();
                var duplicates = group.Where(w => w != bestWeapon);
                
                foreach (var duplicate in duplicates)
                {
                    inventory.DropItem(duplicate.slotIndex, duplicate.quantity);
                    if (debugManagement)
                    {
                        Debug.Log($"[InventoryManager] {name} 移除重复武器: {duplicate.item.ItemName}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 平衡物品数量
        /// </summary>
        private void BalanceItemQuantities(InventoryAnalysis analysis)
        {
            // 确保各类物品数量在合理范围内
            // 这里可以根据AI的当前状态动态调整
        }
        
        /// <summary>
        /// 确保最少物品数量
        /// </summary>
        private void EnsureMinimumItems(InventoryAnalysis analysis)
        {
            // 如果缺少关键物品，标记需要获取
            // 这将影响AI的目标选择
            if (analysis.missingHealingItems > 0 || 
                analysis.missingFoodItems > 0 || 
                analysis.missingDrinkItems > 0)
            {
                if (debugManagement)
                {
                    Debug.Log($"[InventoryManager] {name} 缺少关键物品 - 治疗:{analysis.missingHealingItems} 食物:{analysis.missingFoodItems} 饮料:{analysis.missingDrinkItems}");
                }
            }
        }
        
        /// <summary>
        /// 丢弃低优先级物品
        /// </summary>
        private void DropLowPriorityItems(int count)
        {
            var allItems = new List<ItemAnalysis>();
            
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    allItems.Add(new ItemAnalysis(slot.Item, slot.Quantity, i, GetItemPriority(slot.Item)));
                }
            }
            
            var lowestPriorityItems = allItems
                .OrderBy(item => item.priority)
                .Take(count);
                
            foreach (var item in lowestPriorityItems)
            {
                inventory.DropItem(item.slotIndex, item.quantity);
                if (debugManagement)
                {
                    Debug.Log($"[InventoryManager] {name} 丢弃低优先级物品: {item.item.ItemName}");
                }
            }
        }
        
        /// <summary>
        /// 按优先级排序背包
        /// </summary>
        public void SortInventoryByPriority()
        {
            if (inventory == null) return;
            
            // 获取所有物品并按优先级排序
            var items = new List<(ItemBase item, int quantity, int priority)>();
            
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    items.Add((slot.Item, slot.Quantity, GetItemPriority(slot.Item)));
                }
            }
            
            // 清空背包
            for (int i = 0; i < inventory.Size; i++)
            {
                inventory.ClearSlot(i);
            }
            
            // 按优先级重新添加物品
            var sortedItems = items.OrderByDescending(item => item.priority);
            foreach (var (item, quantity, priority) in sortedItems)
            {
                inventory.AddItem(item, quantity);
            }
            
            if (debugManagement)
            {
                Debug.Log($"[InventoryManager] {name} 完成背包排序");
            }
        }
        
        // 辅助方法
        private float GetInventoryFullness()
        {
            if (inventory == null) return 0f;
            
            int usedSlots = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    usedSlots++;
            }
            return (float)usedSlots / inventory.Size;
        }
        
        private int GetItemPriority(ItemBase item)
        {
            if (item is ConsumableItem consumable)
            {
                if (IsHealingItem(consumable)) return itemPriorities["healing"];
                if (IsFoodItem(consumable)) return itemPriorities["food"];
                if (IsDrinkItem(consumable)) return itemPriorities["drink"];
                return itemPriorities["misc"];
            }
            else if (item is WeaponItem)
            {
                return itemPriorities["weapon"];
            }
            
            return itemPriorities["misc"];
        }
        
        private float GetWeaponQuality(WeaponItem weapon)
        {
            if (weapon == null) return 0f;
            return weapon.Damage; // 简单的质量评估
        }
        
        private bool IsHealingItem(ConsumableItem item)
        {
            return item.name.ToLower().Contains("heal") || 
                   item.name.ToLower().Contains("potion") ||
                   item.name.ToLower().Contains("medicine");
        }
        
        private bool IsFoodItem(ConsumableItem item)
        {
            return item.name.ToLower().Contains("food") || 
                   item.name.ToLower().Contains("bread") ||
                   item.name.ToLower().Contains("meat");
        }
        
        private bool IsDrinkItem(ConsumableItem item)
        {
            return item.name.ToLower().Contains("water") || 
                   item.name.ToLower().Contains("drink") ||
                   item.name.ToLower().Contains("juice");
        }
        
        // 公共查询方法
        public InventoryAnalysis GetCurrentAnalysis() => AnalyzeInventory();
        public bool NeedsManagement() => GetInventoryFullness() > comfortableFullnessThreshold;
        public bool IsCritical() => GetInventoryFullness() > criticalFullnessThreshold;
    }
    
    // 数据结构
    [System.Serializable]
    public class InventoryAnalysis
    {
        public int totalItems = 0;
        public int usedSlots = 0;
        public int healingItems = 0;
        public int foodItems = 0;
        public int drinkItems = 0;
        public int weapons = 0;
        
        public int missingHealingItems = 0;
        public int missingFoodItems = 0;
        public int missingDrinkItems = 0;
        public int excessWeapons = 0;
        
        public Dictionary<string, List<ItemAnalysis>> itemsByCategory = new Dictionary<string, List<ItemAnalysis>>
        {
            {"healing", new List<ItemAnalysis>()},
            {"food", new List<ItemAnalysis>()},
            {"drink", new List<ItemAnalysis>()},
            {"weapon", new List<ItemAnalysis>()},
            {"misc", new List<ItemAnalysis>()}
        };
    }
    
    [System.Serializable]
    public class ItemAnalysis
    {
        public ItemBase item;
        public int quantity;
        public int slotIndex;
        public int priority;
        
        public ItemAnalysis(ItemBase item, int quantity, int slotIndex, int priority)
        {
            this.item = item;
            this.quantity = quantity;
            this.slotIndex = slotIndex;
            this.priority = priority;
        }
    }
}