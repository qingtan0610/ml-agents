using UnityEngine;
using System.Collections.Generic;
using Inventory.Items;

namespace NPC.Data
{
    [CreateAssetMenu(fileName = "MerchantData", menuName = "NPC/Merchant Data")]
    public class MerchantData : NPCData
    {
        [Header("Shop Settings")]
        public ShopInventory shopInventory;
        
        [Header("Randomization")]
        public bool randomizeInventory = true; // 是否随机化商品
        public int minItemTypes = 4; // 最少商品种类
        public int maxItemTypes = 8; // 最多商品种类
        
        [Header("Pricing")]
        [Range(0.5f, 2f)]
        public float buyPriceMultiplier = 1f;  // 购买价格倍率
        [Range(0.1f, 1f)]
        public float sellPriceMultiplier = 0.5f;  // 出售价格倍率
        
        [Header("Special Conditions")]
        public bool hasDailyDeals = true;
        public int dailyDealDiscount = 20;  // 每日特惠折扣百分比
        public bool acceptsTrade = true;
        
        [Header("Merchant Dialogue")]
        [TextArea(2, 4)]
        public string shopOpenText = "看看我的商品吧！";
        [TextArea(2, 4)]
        public string purchaseSuccessText = "感谢惠顾！";
        [TextArea(2, 4)]
        public string purchaseFailText = "你的金币不够！";
        [TextArea(2, 4)]
        public string inventoryFullText = "你的背包满了！";
        
        protected override void OnValidate()
        {
            base.OnValidate();
            npcType = NPCType.Merchant;
            interactionType = NPCInteractionType.Shop;
        }
    }
    
    [System.Serializable]
    public class ShopInventory
    {
        public List<ShopItem> items = new List<ShopItem>();
        
        [System.Serializable]
        public class ShopItem
        {
            public ItemBase item;
            public int stock = -1;  // -1表示无限库存
            public int restockAmount = 5;
            public float priceOverride = -1;  // -1表示使用物品默认价格
            public bool isDailyDeal = false;
        }
        
        public ShopItem GetShopItem(string itemId)
        {
            return items.Find(x => x.item != null && x.item.ItemId == itemId);
        }
    }
}