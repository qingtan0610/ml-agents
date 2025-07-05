using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NPC.Types;
using NPC.Runtime;

namespace NPC.Managers
{
    /// <summary>
    /// 商人存档管理器 - 管理所有商人的库存存档
    /// </summary>
    public class MerchantSaveManager : MonoBehaviour
    {
        private static MerchantSaveManager instance;
        public static MerchantSaveManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<MerchantSaveManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MerchantSaveManager");
                        instance = go.AddComponent<MerchantSaveManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        // 存储所有商人的库存数据
        private Dictionary<string, ShopInventorySaveData> merchantSaveData = new Dictionary<string, ShopInventorySaveData>();
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// 注册商人（商人初始化时调用）
        /// </summary>
        public void RegisterMerchant(string merchantId, RuntimeShopInventory inventory)
        {
            // 如果有存档数据，加载它
            if (merchantSaveData.TryGetValue(merchantId, out var saveData))
            {
                inventory.LoadSaveData(saveData);
                Debug.Log($"[MerchantSaveManager] 加载商人 {merchantId} 的库存数据");
            }
        }
        
        /// <summary>
        /// 保存商人库存（商人销毁或游戏保存时调用）
        /// </summary>
        public void SaveMerchantInventory(string merchantId, RuntimeShopInventory inventory)
        {
            if (inventory != null)
            {
                merchantSaveData[merchantId] = inventory.GetSaveData();
                Debug.Log($"[MerchantSaveManager] 保存商人 {merchantId} 的库存数据");
            }
        }
        
        /// <summary>
        /// 获取所有商人存档数据
        /// </summary>
        public MerchantsSaveData GetSaveData()
        {
            return new MerchantsSaveData
            {
                merchants = merchantSaveData.ToList()
            };
        }
        
        /// <summary>
        /// 加载所有商人存档数据
        /// </summary>
        public void LoadSaveData(MerchantsSaveData data)
        {
            merchantSaveData.Clear();
            
            if (data != null && data.merchants != null)
            {
                foreach (var kvp in data.merchants)
                {
                    merchantSaveData[kvp.Key] = kvp.Value;
                }
            }
            
            Debug.Log($"[MerchantSaveManager] 加载了 {merchantSaveData.Count} 个商人的存档数据");
        }
        
        /// <summary>
        /// 清空所有存档（新游戏时调用）
        /// </summary>
        public void ClearAllData()
        {
            merchantSaveData.Clear();
            Debug.Log("[MerchantSaveManager] 清空所有商人存档数据");
        }
        
        /// <summary>
        /// 强制所有商人补货
        /// </summary>
        public void RestockAllMerchants()
        {
            // 找到场景中所有商人
            var merchants = FindObjectsOfType<MerchantNPC>();
            foreach (var merchant in merchants)
            {
                merchant.ForceRestock();
            }
            
            Debug.Log($"[MerchantSaveManager] 强制 {merchants.Length} 个商人补货");
        }
    }
    
    /// <summary>
    /// 所有商人的存档数据
    /// </summary>
    [System.Serializable]
    public class MerchantsSaveData
    {
        public List<KeyValuePair<string, ShopInventorySaveData>> merchants;
    }
}