using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NPC.Data;
using Inventory.Items;

namespace NPC.Runtime
{
    /// <summary>
    /// 商人运行时库存 - 每个商人实例独立的库存数据
    /// </summary>
    [System.Serializable]
    public class RuntimeShopInventory
    {
        [System.Serializable]
        public class RuntimeShopItem
        {
            public ItemBase item;
            public int currentStock;
            public int maxStock;
            public int restockAmount;
            public float priceOverride;
            public bool isDailyDeal;
            
            public RuntimeShopItem(ShopInventory.ShopItem source)
            {
                item = source.item;
                currentStock = source.stock == -1 ? 999 : source.stock;
                maxStock = source.stock;
                restockAmount = source.restockAmount;
                priceOverride = source.priceOverride;
                isDailyDeal = source.isDailyDeal;
            }
            
            public void Restock()
            {
                if (maxStock == -1)
                {
                    currentStock = 999; // 无限库存
                }
                else
                {
                    currentStock = Mathf.Min(currentStock + restockAmount, maxStock);
                }
            }
            
            public bool Purchase(int amount = 1)
            {
                if (maxStock == -1 || currentStock >= amount)
                {
                    if (maxStock != -1)
                    {
                        currentStock -= amount;
                    }
                    return true;
                }
                return false;
            }
        }
        
        private Dictionary<string, RuntimeShopItem> items = new Dictionary<string, RuntimeShopItem>();
        private float lastRestockTime;
        private const float RESTOCK_INTERVAL = 86400f; // 24小时（游戏时间）
        
        /// <summary>
        /// 从配置初始化运行时库存
        /// </summary>
        public void Initialize(ShopInventory sourceInventory)
        {
            items.Clear();
            
            if (sourceInventory == null || sourceInventory.items == null) return;
            
            foreach (var sourceItem in sourceInventory.items)
            {
                if (sourceItem.item != null)
                {
                    var runtimeItem = new RuntimeShopItem(sourceItem);
                    items[sourceItem.item.ItemId] = runtimeItem;
                }
            }
            
            lastRestockTime = Time.time;
        }
        
        /// <summary>
        /// 从配置初始化部分运行时库存（随机选择）
        /// </summary>
        public void InitializeRandomized(ShopInventory sourceInventory, int itemCount, float seed)
        {
            items.Clear();
            
            if (sourceInventory == null || sourceInventory.items == null) return;
            
            // 筛选有效商品
            var validItems = sourceInventory.items.Where(item => item.item != null).ToList();
            
            // 随机选择指定数量的商品
            var selectedItems = ServiceRandomizer.RandomSelect(validItems, itemCount, seed);
            
            foreach (var sourceItem in selectedItems)
            {
                var runtimeItem = new RuntimeShopItem(sourceItem);
                items[sourceItem.item.ItemId] = runtimeItem;
            }
            
            lastRestockTime = Time.time;
            
            // 随机选择每日特惠
            UpdateDailyDeals();
        }
        
        /// <summary>
        /// 获取商品
        /// </summary>
        public RuntimeShopItem GetItem(string itemId)
        {
            return items.TryGetValue(itemId, out var item) ? item : null;
        }
        
        /// <summary>
        /// 获取所有商品
        /// </summary>
        public List<RuntimeShopItem> GetAllItems()
        {
            return new List<RuntimeShopItem>(items.Values);
        }
        
        /// <summary>
        /// 购买商品
        /// </summary>
        public bool PurchaseItem(string itemId, int amount = 1)
        {
            var item = GetItem(itemId);
            if (item != null)
            {
                return item.Purchase(amount);
            }
            return false;
        }
        
        /// <summary>
        /// 检查并执行补货
        /// </summary>
        public void CheckRestock()
        {
            if (Time.time - lastRestockTime >= RESTOCK_INTERVAL)
            {
                Restock();
            }
        }
        
        /// <summary>
        /// 强制补货
        /// </summary>
        public void Restock()
        {
            foreach (var item in items.Values)
            {
                item.Restock();
            }
            lastRestockTime = Time.time;
            
            // 随机选择每日特惠
            UpdateDailyDeals();
        }
        
        /// <summary>
        /// 更新每日特惠
        /// </summary>
        private void UpdateDailyDeals()
        {
            // 先清除所有特惠标记
            foreach (var item in items.Values)
            {
                item.isDailyDeal = false;
            }
            
            // 随机选择1-3个物品作为特惠
            var itemList = new List<RuntimeShopItem>(items.Values);
            int dealCount = Random.Range(1, Mathf.Min(4, itemList.Count + 1));
            
            for (int i = 0; i < dealCount; i++)
            {
                if (itemList.Count > 0)
                {
                    int index = Random.Range(0, itemList.Count);
                    itemList[index].isDailyDeal = true;
                    itemList.RemoveAt(index);
                }
            }
        }
        
        /// <summary>
        /// 保存库存数据
        /// </summary>
        public ShopInventorySaveData GetSaveData()
        {
            var saveData = new ShopInventorySaveData
            {
                lastRestockTime = lastRestockTime,
                items = new List<ShopItemSaveData>()
            };
            
            foreach (var kvp in items)
            {
                saveData.items.Add(new ShopItemSaveData
                {
                    itemId = kvp.Key,
                    currentStock = kvp.Value.currentStock,
                    isDailyDeal = kvp.Value.isDailyDeal
                });
            }
            
            return saveData;
        }
        
        /// <summary>
        /// 加载库存数据
        /// </summary>
        public void LoadSaveData(ShopInventorySaveData saveData)
        {
            if (saveData == null) return;
            
            lastRestockTime = saveData.lastRestockTime;
            
            foreach (var itemData in saveData.items)
            {
                if (items.TryGetValue(itemData.itemId, out var item))
                {
                    item.currentStock = itemData.currentStock;
                    item.isDailyDeal = itemData.isDailyDeal;
                }
            }
        }
    }
    
    /// <summary>
    /// 商店库存存档数据
    /// </summary>
    [System.Serializable]
    public class ShopInventorySaveData
    {
        public float lastRestockTime;
        public List<ShopItemSaveData> items;
    }
    
    /// <summary>
    /// 商品存档数据
    /// </summary>
    [System.Serializable]
    public class ShopItemSaveData
    {
        public string itemId;
        public int currentStock;
        public bool isDailyDeal;
    }
}