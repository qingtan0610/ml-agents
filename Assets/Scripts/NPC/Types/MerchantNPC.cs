using UnityEngine;
using NPC.Core;
using NPC.Data;
using NPC.Interfaces;
using Inventory;
using Inventory.Items;
using Inventory.Managers;

namespace NPC.Types
{
    public class MerchantNPC : NPCBase, IShopkeeper
    {
        [Header("Merchant Settings")]
        [SerializeField] private GameObject shopUIPrefab;
        
        private GameObject currentShopUI;
        private MerchantData merchantData => npcData as MerchantData;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 验证数据类型
            if (npcData != null && !(npcData is MerchantData))
            {
                Debug.LogError($"MerchantNPC requires MerchantData, but got {npcData.GetType().Name}");
            }
        }
        
        protected override void OnInteractionStarted(GameObject interactor)
        {
            OpenShop(interactor);
        }
        
        protected override void OnInteractionEnded()
        {
            CloseShop();
        }
        
        // IShopkeeper 实现
        public void OpenShop(GameObject customer)
        {
            if (merchantData == null)
            {
                Debug.LogError("MerchantNPC: No merchant data assigned!");
                return;
            }
            
            ShowDialogue(merchantData.shopOpenText);
            
            // TODO: 创建商店UI
            // 这里需要实现具体的UI系统
            Debug.Log($"Opening shop for {customer.name}");
            
            // 临时实现：直接显示商品列表
            DisplayShopInventory();
        }
        
        public bool CanAfford(GameObject customer, string itemId, int quantity)
        {
            var currencyManager = customer.GetComponent<CurrencyManager>();
            if (currencyManager == null) return false;
            
            var shopItem = merchantData.shopInventory.GetShopItem(itemId);
            if (shopItem == null || shopItem.item == null) return false;
            
            float price = GetItemPrice(shopItem) * quantity;
            return currencyManager.CanAfford(Mathf.RoundToInt(price));
        }
        
        public bool PurchaseItem(GameObject customer, string itemId, int quantity)
        {
            if (!CanAfford(customer, itemId, quantity)) 
            {
                ShowDialogue(merchantData.purchaseFailText);
                return false;
            }
            
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            var shopItem = merchantData.shopInventory.GetShopItem(itemId);
            if (shopItem == null || shopItem.item == null) return false;
            
            // 检查背包空间
            if (!inventory.CanAddItem(shopItem.item, quantity))
            {
                ShowDialogue(merchantData.inventoryFullText);
                return false;
            }
            
            // 检查库存
            if (shopItem.stock != -1 && shopItem.stock < quantity)
            {
                ShowDialogue($"抱歉，我只有 {shopItem.stock} 个 {shopItem.item.ItemName}");
                return false;
            }
            
            // 执行交易
            float totalPrice = GetItemPrice(shopItem) * quantity;
            if (currencyManager.SpendGold(Mathf.RoundToInt(totalPrice)))
            {
                inventory.AddItem(shopItem.item, quantity);
                
                // 更新库存
                if (shopItem.stock != -1)
                {
                    shopItem.stock -= quantity;
                }
                
                ShowDialogue(merchantData.purchaseSuccessText);
                
                // 播放交易音效
                if (merchantData.interactionSound != null)
                {
                    AudioSource.PlayClipAtPoint(merchantData.interactionSound, transform.position);
                }
                
                return true;
            }
            
            return false;
        }
        
        public float GetPriceMultiplier()
        {
            float baseMultiplier = merchantData.buyPriceMultiplier;
            
            // 根据心情调整价格
            switch (merchantData.defaultMood)
            {
                case NPCMood.Happy:
                    baseMultiplier *= 0.9f;  // 打9折
                    break;
                case NPCMood.Grumpy:
                    baseMultiplier *= 1.1f;  // 加价10%
                    break;
                case NPCMood.Excited:
                    baseMultiplier *= 0.8f;  // 打8折
                    break;
            }
            
            return baseMultiplier;
        }
        
        // 辅助方法
        private float GetItemPrice(ShopInventory.ShopItem shopItem)
        {
            if (shopItem.priceOverride > 0)
            {
                return shopItem.priceOverride;
            }
            
            float basePrice = shopItem.item.BuyPrice;
            float multiplier = GetPriceMultiplier();
            
            // 每日特惠
            if (shopItem.isDailyDeal && merchantData.hasDailyDeals)
            {
                multiplier *= (1f - merchantData.dailyDealDiscount / 100f);
            }
            
            return basePrice * multiplier;
        }
        
        private void DisplayShopInventory()
        {
            Debug.Log($"=== {merchantData.npcName}的商店 ===");
            
            foreach (var shopItem in merchantData.shopInventory.items)
            {
                if (shopItem.item == null) continue;
                
                string stockText = shopItem.stock == -1 ? "无限" : shopItem.stock.ToString();
                float price = GetItemPrice(shopItem);
                string dealText = shopItem.isDailyDeal ? " [今日特惠!]" : "";
                
                Debug.Log($"{shopItem.item.ItemName} - {price:F0}金币 (库存: {stockText}){dealText}");
            }
        }
        
        private void CloseShop()
        {
            if (currentShopUI != null)
            {
                Destroy(currentShopUI);
                currentShopUI = null;
            }
        }
        
        // 处理玩家出售物品给商人
        public bool BuyFromPlayer(GameObject player, ItemBase item, int quantity)
        {
            if (item == null || !item.IsTradeable) return false;
            
            var inventory = player.GetComponent<Inventory.Inventory>();
            var currencyManager = player.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            // 检查玩家是否有足够的物品
            if (inventory.GetItemCount(item) < quantity) return false;
            
            // 计算收购价格
            float sellPrice = item.SellPrice * merchantData.sellPriceMultiplier * quantity;
            
            // 执行交易
            if (inventory.RemoveItem(item, quantity))
            {
                currencyManager.AddGold(Mathf.RoundToInt(sellPrice));
                ShowDialogue($"我以 {sellPrice:F0} 金币收购了你的 {item.ItemName} x{quantity}");
                return true;
            }
            
            return false;
        }
    }
}