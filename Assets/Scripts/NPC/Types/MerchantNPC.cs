using UnityEngine;
using NPC.Core;
using NPC.Data;
using NPC.Interfaces;
using NPC.Runtime;
using NPC.Managers;
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
        private RuntimeShopInventory runtimeInventory;
        private string merchantId;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 验证数据类型
            if (npcData != null && !(npcData is MerchantData))
            {
                Debug.LogError($"MerchantNPC requires MerchantData, but got {npcData.GetType().Name}");
            }
            
            // 初始化运行时库存
            runtimeInventory = new RuntimeShopInventory();
        }
        
        protected override void Start()
        {
            base.Start();
            
            // 生成唯一的商人ID - 基于地图等级和实例索引
            int mapLevel = GetCurrentMapLevel();
            int instanceIndex = GetInstanceIndex();
            merchantId = $"{merchantData?.npcId ?? "merchant"}_map{mapLevel}_inst{instanceIndex}";
            
            // 从配置初始化库存
            if (merchantData != null && merchantData.shopInventory != null)
            {
                if (merchantData.randomizeInventory)
                {
                    // 随机选择商品 - 使用地图等级和实例索引作为种子
                    float seed = mapLevel * 10000f + instanceIndex;
                    int itemCount = Random.Range(merchantData.minItemTypes, merchantData.maxItemTypes + 1);
                    runtimeInventory.InitializeRandomized(merchantData.shopInventory, itemCount, seed);
                    
                    Debug.Log($"[MerchantNPC] {merchantData.npcName} 随机选择了 {itemCount} 种商品");
                }
                else
                {
                    // 使用全部商品
                    runtimeInventory.Initialize(merchantData.shopInventory);
                }
                
                // 注册到存档管理器
                MerchantSaveManager.Instance.RegisterMerchant(merchantId, runtimeInventory);
            }
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            // 保存库存数据
            if (runtimeInventory != null)
            {
                MerchantSaveManager.Instance.SaveMerchantInventory(merchantId, runtimeInventory);
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
            
            var runtimeItem = runtimeInventory.GetItem(itemId);
            if (runtimeItem == null || runtimeItem.item == null) return false;
            
            float price = GetItemPrice(runtimeItem) * quantity;
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
            
            var runtimeItem = runtimeInventory.GetItem(itemId);
            if (runtimeItem == null || runtimeItem.item == null) return false;
            
            // 检查背包空间
            if (!inventory.CanAddItem(runtimeItem.item, quantity))
            {
                ShowDialogue(merchantData.inventoryFullText);
                return false;
            }
            
            // 检查库存
            if (runtimeItem.currentStock < quantity)
            {
                ShowDialogue($"抱歉，我只有 {runtimeItem.currentStock} 个 {runtimeItem.item.ItemName}");
                return false;
            }
            
            // 执行交易
            float totalPrice = GetItemPrice(runtimeItem) * quantity;
            if (currencyManager.SpendGold(Mathf.RoundToInt(totalPrice)))
            {
                inventory.AddItem(runtimeItem.item, quantity);
                
                // 更新运行时库存
                runtimeItem.Purchase(quantity);
                
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
        private float GetItemPrice(RuntimeShopInventory.RuntimeShopItem runtimeItem)
        {
            if (runtimeItem.priceOverride > 0)
            {
                return runtimeItem.priceOverride;
            }
            
            float basePrice = runtimeItem.item.BuyPrice;
            float multiplier = GetPriceMultiplier();
            
            // 每日特惠
            if (runtimeItem.isDailyDeal && merchantData.hasDailyDeals)
            {
                multiplier *= (1f - merchantData.dailyDealDiscount / 100f);
            }
            
            return basePrice * multiplier;
        }
        
        private void DisplayShopInventory()
        {
            Debug.Log($"=== {merchantData.npcName}的商店 ===");
            
            // 检查补货
            runtimeInventory.CheckRestock();
            
            var runtimeItems = runtimeInventory.GetAllItems();
            foreach (var runtimeItem in runtimeItems)
            {
                if (runtimeItem.item == null) continue;
                
                string stockText = runtimeItem.maxStock == -1 ? "无限" : runtimeItem.currentStock.ToString();
                float price = GetItemPrice(runtimeItem);
                string dealText = runtimeItem.isDailyDeal ? " [今日特惠!]" : "";
                
                Debug.Log($"{runtimeItem.item.ItemName} - {price:F0}金币 (库存: {stockText}){dealText}");
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
        
        /// <summary>
        /// 强制补货（由管理器调用）
        /// </summary>
        public void ForceRestock()
        {
            if (runtimeInventory != null)
            {
                runtimeInventory.Restock();
                Debug.Log($"[MerchantNPC] {merchantData?.npcName ?? name} 已补货");
            }
        }
        
        /// <summary>
        /// 获取商人ID
        /// </summary>
        public string GetMerchantId() => merchantId;
    }
}