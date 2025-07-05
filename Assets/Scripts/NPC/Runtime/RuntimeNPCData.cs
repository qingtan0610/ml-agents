using UnityEngine;
using System.Collections.Generic;
using NPC.Data;

namespace NPC.Runtime
{
    /// <summary>
    /// NPC运行时数据基类 - 管理每个NPC实例的独立数据
    /// </summary>
    public abstract class RuntimeNPCData
    {
        protected string npcId;
        protected float seed;
        
        public RuntimeNPCData(string id, Vector3 position)
        {
            npcId = id;
            seed = position.x * 1000f + position.y;
        }
        
        public abstract void Initialize(NPCData sourceData);
        public abstract object GetSaveData();
        public abstract void LoadSaveData(object saveData, NPCData sourceData);
    }
    
    /// <summary>
    /// NPC运行时数据管理器
    /// </summary>
    public class NPCRuntimeDataManager : MonoBehaviour
    {
        private static NPCRuntimeDataManager instance;
        public static NPCRuntimeDataManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<NPCRuntimeDataManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("NPCRuntimeDataManager");
                        instance = go.AddComponent<NPCRuntimeDataManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        private Dictionary<string, object> npcSaveData = new Dictionary<string, object>();
        
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
        /// 保存NPC数据
        /// </summary>
        public void SaveNPCData(string npcId, object data)
        {
            npcSaveData[npcId] = data;
            Debug.Log($"[NPCRuntimeDataManager] 保存NPC {npcId} 的数据");
        }
        
        /// <summary>
        /// 获取NPC存档数据
        /// </summary>
        public T GetNPCData<T>(string npcId) where T : class
        {
            if (npcSaveData.TryGetValue(npcId, out var data))
            {
                return data as T;
            }
            return null;
        }
        
        /// <summary>
        /// 清空所有数据（新游戏）
        /// </summary>
        public void ClearAllData()
        {
            npcSaveData.Clear();
            Debug.Log("[NPCRuntimeDataManager] 清空所有NPC数据");
        }
        
        /// <summary>
        /// 清空当前地图的NPC数据（地图切换时调用）
        /// </summary>
        public void ClearCurrentMapData()
        {
            // 地图切换时清空所有NPC数据，让新地图的NPC重新随机化
            npcSaveData.Clear();
            Debug.Log("[NPCRuntimeDataManager] 地图切换，清空NPC数据以重新随机化");
        }
        
        /// <summary>
        /// 获取所有存档数据
        /// </summary>
        public Dictionary<string, object> GetAllSaveData()
        {
            return new Dictionary<string, object>(npcSaveData);
        }
        
        /// <summary>
        /// 加载所有存档数据
        /// </summary>
        public void LoadAllSaveData(Dictionary<string, object> data)
        {
            npcSaveData = data ?? new Dictionary<string, object>();
            Debug.Log($"[NPCRuntimeDataManager] 加载了 {npcSaveData.Count} 个NPC的数据");
        }
    }
}