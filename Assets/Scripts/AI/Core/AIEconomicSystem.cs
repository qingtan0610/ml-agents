using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using Inventory.Items;
using NPC.Core;
using NPC;

namespace AI.Core
{
    /// <summary>
    /// AI经济决策系统 - 管理买卖交易和资源分配
    /// </summary>
    public class AIEconomicSystem : MonoBehaviour
    {
        [Header("Economic Settings")]
        [SerializeField] private float emergencyReserve = 50f;      // 紧急储备金
        [SerializeField] private float wealthGoal = 500f;           // 财富目标
        [SerializeField] private float priceMemoryDuration = 300f;  // 价格记忆时长（秒）
        
        [Header("Trading Preferences")]
        [SerializeField] private float maxBuyPriceRatio = 1.5f;     // 最高购买价格比率
        [SerializeField] private float minSellPriceRatio = 0.4f;    // 最低出售价格比率
        [SerializeField] private float bulkBuyThreshold = 3;        // 批量购买阈值
        [SerializeField] private float bulkDiscountExpectation = 0.8f; // 批量折扣期望
        
        // 组件引用
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        private AIGoalSystem goalSystem;
        private AIItemEvaluator itemEvaluator;
        private Inventory.Managers.CurrencyManager currencyManager;
        
        // 价格记忆
        private Dictionary<string, PriceMemory> priceHistory = new Dictionary<string, PriceMemory>();
        
        // 交易历史
        private List<TransactionRecord> transactionHistory = new List<TransactionRecord>();
        private const int MAX_HISTORY = 50;
        
        // 市场分析
        private float averageHealthPotionPrice = 50f;
        private float averageFoodPrice = 20f;
        private float averageWaterPrice = 10f;
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            goalSystem = GetComponent<AIGoalSystem>();
            itemEvaluator = GetComponent<AIItemEvaluator>();
            currencyManager = GetComponent<Inventory.Managers.CurrencyManager>();
            
            if (itemEvaluator == null)
            {
                itemEvaluator = gameObject.AddComponent<AIItemEvaluator>();
            }
        }
        
        /// <summary>
        /// 创建购物清单
        /// </summary>
        public ShoppingList CreateShoppingList(NPCBase merchant)
        {
            var shoppingList = new ShoppingList();
            float availableGold = currencyManager.CurrentGold - emergencyReserve;
            
            if (availableGold <= 0) return shoppingList;
            
            // 1. 分析当前需求
            var needs = AnalyzeCurrentNeeds();
            
            // 2. 获取商人库存（这里假设商人有获取库存的方法）
            var merchantInventory = GetMerchantInventory(merchant);
            
            // 3. 根据需求优先级创建购物清单
            foreach (var need in needs.OrderByDescending(n => n.priority))
            {
                var matchingItems = FindMatchingItems(merchantInventory, need.category);
                
                foreach (var item in matchingItems)
                {
                    float price = GetItemPrice(item, merchant);
                    
                    // 检查价格是否合理
                    if (IsPriceReasonable(item, price, need.priority))
                    {
                        int quantity = DetermineQuantityToBuy(item, price, availableGold, need);
                        
                        if (quantity > 0)
                        {
                            shoppingList.AddItem(item, quantity, price);
                            availableGold -= price * quantity;
                            
                            if (availableGold <= 0) break;
                        }
                    }
                }
                
                if (availableGold <= 0) break;
            }
            
            return shoppingList;
        }
        
        /// <summary>
        /// 创建出售清单
        /// </summary>
        public List<ItemBase> CreateSellingList(NPCBase merchant, float targetGold = 0)
        {
            var sellingList = new List<ItemBase>();
            
            // 如果不需要钱且不是资源积累目标，不卖东西
            if (targetGold <= 0 && currencyManager.CurrentGold > emergencyReserve * 2)
            {
                if (goalSystem?.CurrentHighLevelGoal?.Type != GoalType.ResourceAccumulation)
                {
                    return sellingList;
                }
            }
            
            // 使用物品评估器获取可出售物品
            var itemsToConsider = itemEvaluator.GetItemsToSell(targetGold > 0 ? targetGold : 100f);
            
            foreach (var item in itemsToConsider)
            {
                float sellPrice = GetSellPrice(item, merchant);
                
                // 检查卖价是否可接受
                if (IsSellPriceAcceptable(item, sellPrice))
                {
                    sellingList.Add(item);
                    
                    if (targetGold > 0)
                    {
                        targetGold -= sellPrice;
                        if (targetGold <= 0) break;
                    }
                }
            }
            
            return sellingList;
        }
        
        /// <summary>
        /// 评估是否应该使用服务（医生、餐厅等）
        /// </summary>
        public bool ShouldUseService(ServiceType service, float price)
        {
            switch (service)
            {
                case ServiceType.Healing:
                    float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
                    float healthNeed = 1f - healthRatio;
                    return healthNeed > 0.3f && price < currencyManager.CurrentGold * 0.5f;
                    
                case ServiceType.Food:
                    float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
                    float hungerNeed = 1f - hungerRatio;
                    return hungerNeed > 0.4f && price < currencyManager.CurrentGold * 0.3f;
                    
                case ServiceType.Water:
                    // 水通常免费，总是接受
                    return true;
                    
                case ServiceType.WeaponUpgrade:
                    // 如果有好武器且资金充足
                    return inventory.EquippedWeapon != null && 
                           currencyManager.CurrentGold > emergencyReserve * 4 &&
                           price < currencyManager.CurrentGold * 0.3f;
                    
                case ServiceType.BagExpansion:
                    // 如果背包经常满且有钱
                    float usageRatio = GetInventoryUsageRatio();
                    return usageRatio > 0.8f && 
                           currencyManager.CurrentGold > emergencyReserve * 6 &&
                           price < currencyManager.CurrentGold * 0.4f;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 记录交易
        /// </summary>
        public void RecordTransaction(TransactionType type, ItemBase item, int quantity, float pricePerUnit, NPCBase npc)
        {
            var record = new TransactionRecord
            {
                Type = type,
                Item = item,
                Quantity = quantity,
                PricePerUnit = pricePerUnit,
                TotalPrice = pricePerUnit * quantity,
                NPCType = npc.NPCType,
                Timestamp = Time.time
            };
            
            transactionHistory.Add(record);
            
            // 限制历史记录数量
            if (transactionHistory.Count > MAX_HISTORY)
            {
                transactionHistory.RemoveAt(0);
            }
            
            // 更新价格记忆
            UpdatePriceMemory(item, pricePerUnit, npc.NPCType);
            
            // 更新市场均价
            UpdateMarketAverages(item, pricePerUnit);
        }
        
        /// <summary>
        /// 获取最佳交易NPC
        /// </summary>
        public NPCType GetBestNPCForItem(ItemBase item, bool isBuying)
        {
            var itemName = item.ItemName.ToLower();
            var category = itemEvaluator.IdentifyItemCategory(item);
            
            // 基于物品类型推荐NPC
            if (category == ItemCategory.Health || itemName.Contains("potion") || itemName.Contains("medicine"))
            {
                // 比较医生和商人的价格
                float doctorPrice = GetRememberedPrice(item, NPCType.Doctor);
                float merchantPrice = GetRememberedPrice(item, NPCType.Merchant);
                
                if (doctorPrice > 0 && merchantPrice > 0)
                {
                    return isBuying ? 
                        (doctorPrice < merchantPrice ? NPCType.Doctor : NPCType.Merchant) :
                        (doctorPrice > merchantPrice ? NPCType.Doctor : NPCType.Merchant);
                }
                
                return NPCType.Doctor; // 默认医生
            }
            else if (category == ItemCategory.Food)
            {
                return NPCType.Restaurant;
            }
            else if (category == ItemCategory.Weapon)
            {
                return NPCType.Blacksmith;
            }
            
            return NPCType.Merchant; // 默认商人
        }
        
        // 辅助方法
        
        private List<Need> AnalyzeCurrentNeeds()
        {
            var needs = new List<Need>();
            
            // 生命需求
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            if (healthRatio < 0.7f)
            {
                needs.Add(new Need
                {
                    category = ItemCategory.Health,
                    priority = Mathf.Lerp(1f, 0.3f, healthRatio),
                    desiredQuantity = Mathf.CeilToInt((1f - healthRatio) * 3)
                });
            }
            
            // 饥饿需求
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            if (hungerRatio < 0.7f)
            {
                needs.Add(new Need
                {
                    category = ItemCategory.Food,
                    priority = Mathf.Lerp(0.9f, 0.2f, hungerRatio),
                    desiredQuantity = Mathf.CeilToInt((1f - hungerRatio) * 2)
                });
            }
            
            // 口渴需求
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            if (thirstRatio < 0.7f)
            {
                needs.Add(new Need
                {
                    category = ItemCategory.Water,
                    priority = Mathf.Lerp(0.95f, 0.25f, thirstRatio),
                    desiredQuantity = Mathf.CeilToInt((1f - thirstRatio) * 2)
                });
            }
            
            // 武器需求
            if (inventory.EquippedWeapon == null)
            {
                needs.Add(new Need
                {
                    category = ItemCategory.Weapon,
                    priority = 0.6f,
                    desiredQuantity = 1
                });
            }
            
            // 钥匙需求
            if (!HasUniversalKey())
            {
                needs.Add(new Need
                {
                    category = ItemCategory.Key,
                    priority = 0.5f,
                    desiredQuantity = 1
                });
            }
            
            return needs;
        }
        
        private bool IsPriceReasonable(ItemBase item, float price, float needPriority)
        {
            // 获取记忆中的价格
            float rememberedPrice = GetRememberedPrice(item);
            float marketAverage = GetMarketAverage(item);
            
            // 基准价格（优先使用市场均价，其次记忆价格，最后物品基础价格）
            float basePrice = marketAverage > 0 ? marketAverage : 
                             (rememberedPrice > 0 ? rememberedPrice : item.BuyPrice);
            
            // 根据需求调整可接受价格
            float acceptableRatio = maxBuyPriceRatio;
            if (needPriority > 0.8f) // 紧急需求
                acceptableRatio *= 1.5f;
            else if (needPriority < 0.3f) // 低需求
                acceptableRatio *= 0.7f;
            
            return price <= basePrice * acceptableRatio;
        }
        
        private bool IsSellPriceAcceptable(ItemBase item, float sellPrice)
        {
            float rememberedPrice = GetRememberedPrice(item);
            float basePrice = rememberedPrice > 0 ? rememberedPrice : item.BuyPrice;
            
            return sellPrice >= basePrice * minSellPriceRatio;
        }
        
        private int DetermineQuantityToBuy(ItemBase item, float price, float availableGold, Need need)
        {
            // 基础数量
            int desiredQuantity = need.desiredQuantity;
            
            // 检查是否可堆叠
            if (item.IsStackable && desiredQuantity >= bulkBuyThreshold)
            {
                // 期望批量折扣
                float bulkPrice = price * bulkDiscountExpectation;
                if (bulkPrice * desiredQuantity <= availableGold)
                {
                    return desiredQuantity;
                }
            }
            
            // 根据可用金币调整数量
            int affordableQuantity = Mathf.FloorToInt(availableGold / price);
            return Mathf.Min(desiredQuantity, affordableQuantity);
        }
        
        private void UpdatePriceMemory(ItemBase item, float price, NPCType npcType)
        {
            string key = $"{item.ItemName}_{npcType}";
            
            if (!priceHistory.ContainsKey(key))
            {
                priceHistory[key] = new PriceMemory();
            }
            
            var memory = priceHistory[key];
            memory.prices.Add(price);
            memory.timestamps.Add(Time.time);
            
            // 清理过期记录
            for (int i = memory.timestamps.Count - 1; i >= 0; i--)
            {
                if (Time.time - memory.timestamps[i] > priceMemoryDuration)
                {
                    memory.prices.RemoveAt(i);
                    memory.timestamps.RemoveAt(i);
                }
            }
        }
        
        private float GetRememberedPrice(ItemBase item, NPCType? specificNPC = null)
        {
            var relevantPrices = new List<float>();
            
            foreach (var kvp in priceHistory)
            {
                if (kvp.Key.StartsWith(item.ItemName))
                {
                    if (specificNPC == null || kvp.Key.EndsWith(specificNPC.ToString()))
                    {
                        relevantPrices.AddRange(kvp.Value.prices);
                    }
                }
            }
            
            return relevantPrices.Count > 0 ? relevantPrices.Average() : 0f;
        }
        
        private void UpdateMarketAverages(ItemBase item, float price)
        {
            var category = itemEvaluator.IdentifyItemCategory(item);
            
            switch (category)
            {
                case ItemCategory.Health:
                    averageHealthPotionPrice = Mathf.Lerp(averageHealthPotionPrice, price, 0.1f);
                    break;
                case ItemCategory.Food:
                    averageFoodPrice = Mathf.Lerp(averageFoodPrice, price, 0.1f);
                    break;
                case ItemCategory.Water:
                    averageWaterPrice = Mathf.Lerp(averageWaterPrice, price, 0.1f);
                    break;
            }
        }
        
        private float GetMarketAverage(ItemBase item)
        {
            var category = itemEvaluator.IdentifyItemCategory(item);
            
            switch (category)
            {
                case ItemCategory.Health:
                    return averageHealthPotionPrice;
                case ItemCategory.Food:
                    return averageFoodPrice;
                case ItemCategory.Water:
                    return averageWaterPrice;
                default:
                    return 0f;
            }
        }
        
        private List<ItemBase> GetMerchantInventory(NPCBase merchant)
        {
            // 这里需要根据实际的商人系统实现
            // 暂时返回空列表
            return new List<ItemBase>();
        }
        
        private List<ItemBase> FindMatchingItems(List<ItemBase> inventory, ItemCategory category)
        {
            return inventory.Where(item => itemEvaluator.IdentifyItemCategory(item) == category).ToList();
        }
        
        private float GetItemPrice(ItemBase item, NPCBase merchant)
        {
            // 基础价格 * NPC的价格倍率
            return item.BuyPrice * 1.2f; // 暂时使用固定倍率
        }
        
        private float GetSellPrice(ItemBase item, NPCBase merchant)
        {
            // 卖价通常是买价的50%
            return item.BuyPrice * 0.5f;
        }
        
        private float GetInventoryUsageRatio()
        {
            int usedSlots = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    usedSlots++;
            }
            return (float)usedSlots / inventory.Size;
        }
        
        private bool HasUniversalKey()
        {
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && slot.Item.ItemName.ToLower().Contains("key"))
                {
                    return true;
                }
            }
            return false;
        }
        
        // 内部类
        
        private class PriceMemory
        {
            public List<float> prices = new List<float>();
            public List<float> timestamps = new List<float>();
        }
        
        private class Need
        {
            public ItemCategory category;
            public float priority;
            public int desiredQuantity;
        }
    }
    
    /// <summary>
    /// 购物清单
    /// </summary>
    public class ShoppingList
    {
        public List<ShoppingItem> Items { get; private set; } = new List<ShoppingItem>();
        public float TotalCost => Items.Sum(i => i.TotalPrice);
        
        public void AddItem(ItemBase item, int quantity, float pricePerUnit)
        {
            Items.Add(new ShoppingItem
            {
                Item = item,
                Quantity = quantity,
                PricePerUnit = pricePerUnit,
                TotalPrice = pricePerUnit * quantity
            });
        }
    }
    
    /// <summary>
    /// 购物项
    /// </summary>
    public class ShoppingItem
    {
        public ItemBase Item { get; set; }
        public int Quantity { get; set; }
        public float PricePerUnit { get; set; }
        public float TotalPrice { get; set; }
    }
    
    /// <summary>
    /// 交易记录
    /// </summary>
    public class TransactionRecord
    {
        public TransactionType Type { get; set; }
        public ItemBase Item { get; set; }
        public int Quantity { get; set; }
        public float PricePerUnit { get; set; }
        public float TotalPrice { get; set; }
        public NPCType NPCType { get; set; }
        public float Timestamp { get; set; }
    }
    
    /// <summary>
    /// 交易类型
    /// </summary>
    public enum TransactionType
    {
        Buy,
        Sell,
        Service
    }
    
    /// <summary>
    /// 服务类型
    /// </summary>
    public enum ServiceType
    {
        Healing,
        Food,
        Water,
        WeaponUpgrade,
        BagExpansion
    }
}