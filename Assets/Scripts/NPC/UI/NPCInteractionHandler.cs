using UnityEngine;
using NPC.Core;
using NPC.Types;
using PlayerDebug;

namespace NPC.UI
{
    /// <summary>
    /// 处理NPC交互的临时辅助类，用于支持调试面板
    /// </summary>
    public class NPCInteractionHandler : MonoBehaviour
    {
        private static NPCInteractionHandler instance;
        
        public static NPCInteractionHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("NPCInteractionHandler");
                    instance = go.AddComponent<NPCInteractionHandler>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        
        /// <summary>
        /// 尝试购买商品（用于调试）
        /// </summary>
        public static bool TryPurchase(MerchantNPC merchant, GameObject buyer, int itemIndex)
        {
            if (merchant == null || buyer == null) return false;
            
            var merchantData = merchant.Data as NPC.Data.MerchantData;
            if (merchantData == null || itemIndex < 0 || itemIndex >= merchantData.shopInventory.items.Count) 
                return false;
            
            var shopItem = merchantData.shopInventory.items[itemIndex];
            if (shopItem.item == null) return false;
            
            // 调用商人的购买方法
            bool success = merchant.PurchaseItem(buyer, shopItem.item.ItemId, 1);
            
            // 如果购买成功，输出调试信息
            if (success)
            {
                float price = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                price *= merchant.GetPriceMultiplier();
                
                Debug.Log($"[NPCInteractionHandler] Successfully purchased {shopItem.item.ItemName} for {price:F0} gold");
            }
            
            return success;
        }
        
        /// <summary>
        /// 获取商品信息（用于显示）
        /// </summary>
        public static string GetShopItemInfo(MerchantNPC merchant, int itemIndex)
        {
            if (merchant == null) return "";
            
            var merchantData = merchant.Data as NPC.Data.MerchantData;
            if (merchantData == null || itemIndex < 0 || itemIndex >= merchantData.shopInventory.items.Count) 
                return "";
            
            var shopItem = merchantData.shopInventory.items[itemIndex];
            if (shopItem.item == null) return "";
            
            float price = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
            price *= merchant.GetPriceMultiplier();
            
            string stockText = shopItem.stock == -1 ? "无限" : shopItem.stock.ToString();
            string dealText = shopItem.isDailyDeal ? " [特惠!]" : "";
            
            return $"{shopItem.item.ItemName} - {price:F0}金币 (库存: {stockText}){dealText}";
        }
    }
}