using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Rooms.Data
{
    /// <summary>
    /// 房间池 - 任意的房间预制体集合，无特殊含义
    /// </summary>
    [CreateAssetMenu(fileName = "RoomPool", menuName = "Rooms/Room Pool")]
    public class RoomPool : ScriptableObject
    {
        [System.Serializable]
        public class RoomEntry
        {
            [SerializeField] private GameObject roomPrefab; // 房间预制体
            [Range(0f, 1f)] public float spawnWeight = 1f; // 生成权重
            [TextArea(2, 4)] public string notes; // 设计说明
            
            // 属性访问器
            public GameObject RoomPrefab
            {
                get => roomPrefab;
                set => roomPrefab = value;
            }
            
            // 在Inspector中验证预制体
            public void Validate()
            {
                if (roomPrefab != null && roomPrefab.GetComponent<Rooms.Core.SimplifiedRoom>() == null)
                {
                    Debug.LogWarning($"[RoomPool] Prefab '{roomPrefab.name}' does not have SimplifiedRoom component!");
                }
            }
        }
        
        [SerializeField] private List<RoomEntry> roomPrefabs = new List<RoomEntry>();
        
        /// <summary>
        /// 获取所有房间条目
        /// </summary>
        public List<RoomEntry> AllRooms => roomPrefabs;
        
        /// <summary>
        /// 随机获取一个房间预制体（根据权重）
        /// </summary>
        public GameObject GetRandomRoomPrefab()
        {
            if (roomPrefabs == null || roomPrefabs.Count == 0)
                return null;
            
            // 过滤掉空条目
            var validEntries = roomPrefabs.Where(r => r.RoomPrefab != null).ToList();
            if (validEntries.Count == 0)
                return null;
            
            // 根据权重随机选择
            float totalWeight = validEntries.Sum(r => r.spawnWeight);
            float random = Random.Range(0f, totalWeight);
            
            float currentWeight = 0;
            foreach (var entry in validEntries)
            {
                currentWeight += entry.spawnWeight;
                if (random <= currentWeight)
                    return entry.RoomPrefab;
            }
            
            return validEntries[0].RoomPrefab;
        }
        
        /// <summary>
        /// 根据筛选条件获取房间预制体（简化版本）
        /// </summary>
        public GameObject GetRandomRoomPrefab(bool isEdge = false, bool isCorner = false, int doorCount = -1)
        {
            // 房间池只是简单的预制体集合，不处理复杂逻辑
            return GetRandomRoomPrefab();
        }
        
        private void OnValidate()
        {
            // 验证所有条目
            foreach (var entry in roomPrefabs)
            {
                if (entry != null)
                {
                    entry.Validate();
                }
            }
            
            // 移除空条目
            roomPrefabs.RemoveAll(r => r == null);
        }
        
#if UNITY_EDITOR
        [ContextMenu("Auto Collect Room Prefabs")]
        private void AutoCollectRoomPrefabs()
        {
            roomPrefabs.Clear();
            
            // 搜索整个项目中所有包含SimplifiedRoom组件的预制体
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameObject");
            int foundCount = 0;
            
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null && prefab.GetComponent<Rooms.Core.SimplifiedRoom>() != null)
                {
                    var entry = new RoomEntry
                    {
                        RoomPrefab = prefab,
                        spawnWeight = 1f,
                        notes = $"Auto-discovered from {path}"
                    };
                    roomPrefabs.Add(entry);
                    foundCount++;
                    Debug.Log($"[RoomPool] Found room prefab: {prefab.name} at {path}");
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[RoomPool] Auto-collection complete. Found {foundCount} room prefabs with SimplifiedRoom component.");
        }
        
        [ContextMenu("Validate All Entries")]
        private void ValidateAllEntries()
        {
            int validCount = 0;
            int invalidCount = 0;
            
            foreach (var entry in roomPrefabs)
            {
                if (entry?.RoomPrefab != null)
                {
                    if (entry.RoomPrefab.GetComponent<Rooms.Core.SimplifiedRoom>() != null)
                    {
                        validCount++;
                    }
                    else
                    {
                        invalidCount++;
                        Debug.LogWarning($"[RoomPool] Prefab '{entry.RoomPrefab.name}' is missing SimplifiedRoom component!");
                    }
                }
            }
            
            Debug.Log($"[RoomPool] Validation complete. Valid: {validCount}, Invalid: {invalidCount}");
        }
#endif
    }
}