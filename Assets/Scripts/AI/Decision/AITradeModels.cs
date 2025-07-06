using System.Collections.Generic;
using NPC;

namespace AI.Decision
{
    /// <summary>
    /// AI交易上下文
    /// </summary>
    public class AITradeContext
    {
        // NPC信息
        public string NPCType;
        public NPCType NPCTypeEnum;
        
        // AI当前状态
        public int CurrentGold;
        public float CurrentHealth;
        public float MaxHealth;
        public float CurrentHunger;
        public float MaxHunger;
        public float CurrentThirst;
        public float MaxThirst;
        
        // 背包信息
        public List<ItemInfo> InventoryItems;
        public int InventoryCapacity;
        public int UsedSlots;
        
        // 商店物品（商人）
        public List<ShopItemInfo> ShopItems;
        
        // 可用服务（医生、餐厅等）
        public List<ServiceInfo> Services;
        
        // 当前心情
        public float EmotionMood;
        public float SocialMood;
        public float MentalityMood;
    }
    
    /// <summary>
    /// 物品信息
    /// </summary>
    public class ItemInfo
    {
        public string ItemName;
        public int Quantity;
        public int BasePrice;
        public string ItemType; // Consumable, Weapon, etc.
    }
    
    /// <summary>
    /// 商店物品信息
    /// </summary>
    public class ShopItemInfo
    {
        public string ItemName;
        public int Price;
        public bool IsOnSale;
        public string Description;
        public string ItemType;
    }
    
    /// <summary>
    /// 服务信息
    /// </summary>
    public class ServiceInfo
    {
        public string Name;
        public int Price;
        public string Description;
        public string Effect; // 效果描述
    }
    
    /// <summary>
    /// AI交易决策
    /// </summary>
    public class AITradeDecision
    {
        public bool ShouldTrade;
        public TradeType TradeType;
        public string ItemOrServiceName;
        public int Quantity = 1;
        public string Reasoning;
        
        // 附加决策
        public List<string> AdditionalActions; // 额外建议
    }
    
    /// <summary>
    /// 交易类型
    /// </summary>
    public enum TradeType
    {
        None,
        Buy,     // 购买物品
        Sell,    // 出售物品
        Service  // 使用服务
    }
}