using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NPC.Data;

namespace NPC.Runtime
{
    /// <summary>
    /// 运行时餐厅服务管理 - 管理随机化的菜单
    /// </summary>
    public class RuntimeRestaurantServices
    {
        private List<FoodMenuItem> availableMenu;
        private Dictionary<string, int> dailySpecials; // 每日特价
        
        public RuntimeRestaurantServices()
        {
            availableMenu = new List<FoodMenuItem>();
            dailySpecials = new Dictionary<string, int>();
        }
        
        /// <summary>
        /// 初始化随机菜单
        /// </summary>
        public void InitializeRandomized(RestaurantData restaurantData, float seed)
        {
            if (restaurantData == null || restaurantData.menu == null || restaurantData.menu.Count == 0)
            {
                Debug.LogError("[RuntimeRestaurantServices] 无效的餐厅数据!");
                return;
            }
            
            // 如果不随机化，使用全部菜单
            if (!restaurantData.randomizeMenu)
            {
                availableMenu = new List<FoodMenuItem>(restaurantData.menu);
                GenerateDailySpecials(seed);
                return;
            }
            
            // 随机选择菜品数量
            int menuCount = Random.Range(restaurantData.minDishes, restaurantData.maxDishes + 1);
            menuCount = Mathf.Min(menuCount, restaurantData.menu.Count);
            
            // 使用ServiceRandomizer选择菜品
            var selectedItems = ServiceRandomizer.RandomSelect(restaurantData.menu, menuCount, seed);
            availableMenu = new List<FoodMenuItem>(selectedItems);
            
            // 生成每日特价
            GenerateDailySpecials(seed);
            
            Debug.Log($"[RuntimeRestaurantServices] 生成了 {availableMenu.Count} 道菜品的菜单");
        }
        
        /// <summary>
        /// 生成每日特价
        /// </summary>
        private void GenerateDailySpecials(float seed)
        {
            if (availableMenu.Count == 0) return;
            
            // 随机选择1-2个菜品作为特价
            int specialCount = Mathf.Min(Random.Range(1, 3), availableMenu.Count);
            var specials = ServiceRandomizer.RandomSelect(availableMenu, specialCount, seed + 100f);
            
            foreach (var item in specials)
            {
                // 特价为原价的60%-80%
                float discount = Random.Range(0.6f, 0.8f);
                int specialPrice = Mathf.RoundToInt(item.price * discount);
                dailySpecials[item.itemName] = specialPrice;
                
                Debug.Log($"[RuntimeRestaurantServices] 每日特价: {item.itemName} - {specialPrice}金币 (原价{item.price})");
            }
        }
        
        /// <summary>
        /// 获取可用的菜单
        /// </summary>
        public List<FoodMenuItem> GetAvailableMenu()
        {
            return new List<FoodMenuItem>(availableMenu);
        }
        
        /// <summary>
        /// 获取指定菜品
        /// </summary>
        public FoodMenuItem GetMenuItem(string itemName)
        {
            return availableMenu.Find(item => item.itemName == itemName);
        }
        
        /// <summary>
        /// 获取菜品价格（考虑特价）
        /// </summary>
        public int GetItemPrice(string itemName)
        {
            var item = GetMenuItem(itemName);
            if (item == null) return 0;
            
            // 检查是否有特价
            if (dailySpecials.TryGetValue(itemName, out int specialPrice))
            {
                return specialPrice;
            }
            
            return item.price;
        }
        
        /// <summary>
        /// 是否为特价菜品
        /// </summary>
        public bool IsSpecialOffer(string itemName)
        {
            return dailySpecials.ContainsKey(itemName);
        }
        
        /// <summary>
        /// 获取存档数据
        /// </summary>
        public RestaurantServicesSaveData GetSaveData()
        {
            return new RestaurantServicesSaveData
            {
                availableMenuNames = availableMenu.Select(item => item.itemName).ToList(),
                dailySpecials = new Dictionary<string, int>(dailySpecials)
            };
        }
        
        /// <summary>
        /// 加载存档数据
        /// </summary>
        public void LoadSaveData(RestaurantServicesSaveData saveData, RestaurantData restaurantData)
        {
            if (saveData == null || restaurantData == null) return;
            
            // 恢复可用菜单
            availableMenu.Clear();
            foreach (var itemName in saveData.availableMenuNames)
            {
                var item = restaurantData.menu.Find(m => m.itemName == itemName);
                if (item != null)
                {
                    availableMenu.Add(item);
                }
            }
            
            // 恢复特价
            dailySpecials = new Dictionary<string, int>(saveData.dailySpecials);
            
            Debug.Log($"[RuntimeRestaurantServices] 加载了 {availableMenu.Count} 道菜品的菜单");
        }
    }
    
    /// <summary>
    /// 餐厅服务存档数据
    /// </summary>
    [System.Serializable]
    public class RestaurantServicesSaveData
    {
        public List<string> availableMenuNames;
        public Dictionary<string, int> dailySpecials;
    }
}