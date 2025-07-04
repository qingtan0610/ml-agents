using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NPC.Data;

namespace NPC.Core
{
    [CreateAssetMenu(fileName = "NPCPool", menuName = "NPC/NPC Pool")]
    public class NPCPool : ScriptableObject
    {
        [Header("NPC Database")]
        [SerializeField] private List<NPCPoolEntry> npcEntries = new List<NPCPoolEntry>();
        
        [Header("Pool Settings")]
        [SerializeField] private bool allowDuplicates = false;
        [SerializeField] private int maxNPCsPerType = 3;
        
        // 运行时追踪
        private Dictionary<NPCType, List<NPCData>> npcByType;
        private List<string> spawnedNPCIds = new List<string>();
        
        [System.Serializable]
        public class NPCPoolEntry
        {
            public NPCData npcData;
            public GameObject npcPrefab;
            [Range(0f, 1f)]
            public float spawnWeight = 1f;  // 生成权重
            public int minMapLevel = 1;     // 最小地图层级
            public int maxMapLevel = 99;    // 最大地图层级
            public bool isUnique = false;   // 是否唯一（整个游戏只出现一次）
        }
        
        private void OnEnable()
        {
            BuildTypeDictionary();
            spawnedNPCIds.Clear();
        }
        
        private void BuildTypeDictionary()
        {
            npcByType = new Dictionary<NPCType, List<NPCData>>();
            
            foreach (var entry in npcEntries)
            {
                if (entry.npcData != null)
                {
                    var type = entry.npcData.npcType;
                    if (!npcByType.ContainsKey(type))
                    {
                        npcByType[type] = new List<NPCData>();
                    }
                    npcByType[type].Add(entry.npcData);
                }
            }
        }
        
        /// <summary>
        /// 根据类型和地图等级获取合适的NPC
        /// </summary>
        public NPCPoolEntry GetNPCForRoom(NPCType type, int mapLevel)
        {
            if (npcByType == null)
            {
                BuildTypeDictionary();
            }
            
            // 筛选符合条件的NPC
            var validEntries = npcEntries.Where(entry =>
                entry.npcData != null &&
                entry.npcData.npcType == type &&
                entry.minMapLevel <= mapLevel &&
                entry.maxMapLevel >= mapLevel &&
                (!entry.isUnique || !spawnedNPCIds.Contains(entry.npcData.npcId))
            ).ToList();
            
            if (validEntries.Count == 0)
            {
                Debug.LogWarning($"No valid NPC found for type {type} at level {mapLevel}");
                return null;
            }
            
            // 根据权重随机选择
            return GetWeightedRandom(validEntries);
        }
        
        /// <summary>
        /// 获取特定ID的NPC
        /// </summary>
        public NPCPoolEntry GetNPCById(string npcId)
        {
            return npcEntries.FirstOrDefault(entry => 
                entry.npcData != null && entry.npcData.npcId == npcId);
        }
        
        /// <summary>
        /// 生成NPC实例
        /// </summary>
        public GameObject SpawnNPC(NPCPoolEntry entry, Vector3 position, Transform parent = null)
        {
            if (entry == null || entry.npcPrefab == null)
            {
                Debug.LogError("Cannot spawn NPC: entry or prefab is null");
                return null;
            }
            
            GameObject npc = Instantiate(entry.npcPrefab, position, Quaternion.identity, parent);
            
            // 确保NPC有正确的组件
            var npcComponent = npc.GetComponent<NPCBase>();
            if (npcComponent != null)
            {
                // 如果需要，可以在这里进行额外的初始化
            }
            
            // 记录唯一NPC
            if (entry.isUnique)
            {
                spawnedNPCIds.Add(entry.npcData.npcId);
            }
            
            return npc;
        }
        
        /// <summary>
        /// 根据权重随机选择
        /// </summary>
        private NPCPoolEntry GetWeightedRandom(List<NPCPoolEntry> entries)
        {
            float totalWeight = entries.Sum(e => e.spawnWeight);
            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            
            foreach (var entry in entries)
            {
                currentWeight += entry.spawnWeight;
                if (randomValue <= currentWeight)
                {
                    return entry;
                }
            }
            
            // 保险起见，返回最后一个
            return entries.LastOrDefault();
        }
        
        /// <summary>
        /// 获取所有可用的NPC类型
        /// </summary>
        public List<NPCType> GetAvailableTypes(int mapLevel)
        {
            return npcEntries
                .Where(entry => 
                    entry.npcData != null &&
                    entry.minMapLevel <= mapLevel &&
                    entry.maxMapLevel >= mapLevel)
                .Select(entry => entry.npcData.npcType)
                .Distinct()
                .ToList();
        }
        
        /// <summary>
        /// 重置池状态（用于新游戏）
        /// </summary>
        public void ResetPool()
        {
            spawnedNPCIds.Clear();
        }
        
        // 编辑器辅助
        [ContextMenu("Validate Entries")]
        private void ValidateEntries()
        {
            foreach (var entry in npcEntries)
            {
                if (entry.npcData == null)
                {
                    Debug.LogWarning("Found null NPC data in pool");
                    continue;
                }
                
                if (entry.npcPrefab == null)
                {
                    Debug.LogWarning($"NPC {entry.npcData.npcName} has no prefab assigned");
                }
                else
                {
                    // 检查预制体是否有NPC组件
                    var npcComponent = entry.npcPrefab.GetComponent<NPCBase>();
                    if (npcComponent == null)
                    {
                        Debug.LogError($"NPC prefab {entry.npcPrefab.name} missing NPCBase component");
                    }
                }
            }
            
            Debug.Log($"Validated {npcEntries.Count} NPC entries");
        }
        
        [ContextMenu("Find All NPCs in Project")]
        private void FindAllNPCs()
        {
#if UNITY_EDITOR
            npcEntries.Clear();
            
            // 查找所有NPC数据
            string[] dataGuids = UnityEditor.AssetDatabase.FindAssets("t:NPCData");
            Dictionary<NPCType, List<NPCData>> npcDataByType = new Dictionary<NPCType, List<NPCData>>();
            
            foreach (string guid in dataGuids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                NPCData npcData = UnityEditor.AssetDatabase.LoadAssetAtPath<NPCData>(path);
                if (npcData != null)
                {
                    if (!npcDataByType.ContainsKey(npcData.npcType))
                    {
                        npcDataByType[npcData.npcType] = new List<NPCData>();
                    }
                    npcDataByType[npcData.npcType].Add(npcData);
                }
            }
            
            // 查找所有NPC预制体
            string[] prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab NPC_");
            Dictionary<string, GameObject> prefabsByName = new Dictionary<string, GameObject>();
            
            foreach (string guid in prefabGuids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<NPCBase>() != null)
                {
                    prefabsByName[prefab.name] = prefab;
                }
            }
            
            // 匹配数据和预制体
            foreach (var kvp in npcDataByType)
            {
                foreach (var npcData in kvp.Value)
                {
                    NPCPoolEntry entry = new NPCPoolEntry();
                    entry.npcData = npcData;
                    
                    // 尝试根据类型匹配预制体
                    string expectedPrefabName = $"NPC_{npcData.npcType}";
                    if (prefabsByName.TryGetValue(expectedPrefabName, out GameObject prefab))
                    {
                        entry.npcPrefab = prefab;
                    }
                    
                    // 设置默认权重和等级范围
                    entry.spawnWeight = 1f;
                    entry.minMapLevel = 1;
                    entry.maxMapLevel = 99;
                    entry.isUnique = false;
                    
                    npcEntries.Add(entry);
                }
            }
            
            // 按类型排序
            npcEntries = npcEntries.OrderBy(e => e.npcData.npcType)
                                 .ThenBy(e => e.npcData.npcName)
                                 .ToList();
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Found {npcEntries.Count} NPCs in project");
            
            // 提示缺少预制体的NPC
            foreach (var entry in npcEntries)
            {
                if (entry.npcPrefab == null)
                {
                    Debug.LogWarning($"NPC {entry.npcData.npcName} ({entry.npcData.npcType}) missing prefab. Expected: NPC_{entry.npcData.npcType}");
                }
            }
#endif
        }
    }
}