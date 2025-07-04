using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Pools
{
    /// <summary>
    /// 怪物池 - 管理所有怪物预制体
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterPool", menuName = "Pools/Monster Pool")]
    public class MonsterPool : ScriptableObject
    {
        [System.Serializable]
        public class MonsterEntry
        {
            public string monsterName = "Basic Monster";
            public GameObject prefab;
            [Range(1, 10)] public int difficulty = 1;
            [Range(0f, 1f)] public float spawnWeight = 1f;
            public int minMapLevel = 1;
            public int maxMapLevel = 99;
            
            [Header("Group Settings")]
            public int minGroupSize = 1;
            public int maxGroupSize = 1;
            public bool isElite = false;
            public bool isBoss = false;
        }
        
        [SerializeField] private List<MonsterEntry> monsters = new List<MonsterEntry>();
        
        /// <summary>
        /// 根据地图等级获取可用的怪物
        /// </summary>
        public List<MonsterEntry> GetAvailableMonsters(int mapLevel, int maxDifficulty)
        {
            return monsters.Where(m => 
                m.prefab != null &&
                m.minMapLevel <= mapLevel && 
                m.maxMapLevel >= mapLevel &&
                m.difficulty <= maxDifficulty
            ).ToList();
        }
        
        /// <summary>
        /// 随机选择一个怪物类型
        /// </summary>
        public MonsterEntry GetRandomMonster(int mapLevel, int maxDifficulty)
        {
            var available = GetAvailableMonsters(mapLevel, maxDifficulty);
            if (available.Count == 0) return null;
            
            // 根据权重随机选择
            float totalWeight = available.Sum(m => m.spawnWeight);
            float random = Random.Range(0f, totalWeight);
            
            float currentWeight = 0;
            foreach (var monster in available)
            {
                currentWeight += monster.spawnWeight;
                if (random <= currentWeight)
                    return monster;
            }
            
            return available[0];
        }
        
        /// <summary>
        /// 生成一组怪物（在指定位置）
        /// </summary>
        public List<GameObject> SpawnMonsterGroup(MonsterEntry entry, Transform[] spawnPoints, Transform parent = null)
        {
            if (entry == null || entry.prefab == null || spawnPoints == null || spawnPoints.Length == 0)
                return new List<GameObject>();
            
            // 确定生成数量
            int count = Random.Range(entry.minGroupSize, entry.maxGroupSize + 1);
            count = Mathf.Min(count, spawnPoints.Length); // 不超过生成点数量
            
            var spawnedMonsters = new List<GameObject>();
            var usedPoints = new List<Transform>();
            
            for (int i = 0; i < count; i++)
            {
                // 选择未使用的生成点
                var availablePoints = spawnPoints.Where(p => p != null && !usedPoints.Contains(p)).ToList();
                if (availablePoints.Count == 0)
                    break;
                    
                var spawnPoint = availablePoints[Random.Range(0, availablePoints.Count)];
                usedPoints.Add(spawnPoint);
                
                var monster = Instantiate(entry.prefab, spawnPoint.position, Quaternion.identity, parent);
                spawnedMonsters.Add(monster);
                
                // 为精英怪物添加特殊标记
                if (entry.isElite)
                {
                    monster.name = $"{entry.monsterName} (Elite)";
                    // TODO: 增强精英怪物属性
                }
            }
            
            Debug.Log($"[MonsterPool] Spawned {spawnedMonsters.Count} {entry.monsterName}");
            return spawnedMonsters;
        }
        
        private void OnValidate()
        {
            // 验证数据
            foreach (var entry in monsters)
            {
                if (entry.minGroupSize < 1) entry.minGroupSize = 1;
                if (entry.maxGroupSize < entry.minGroupSize) 
                    entry.maxGroupSize = entry.minGroupSize;
            }
        }
        
#if UNITY_EDITOR
        [ContextMenu("Auto Collect Monster Prefabs")]
        private void AutoCollectMonsters()
        {
            monsters.Clear();
            
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs/Enemies" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null && prefab.GetComponent<Enemy.Enemy2D>() != null)
                {
                    var entry = new MonsterEntry
                    {
                        monsterName = prefab.name,
                        prefab = prefab,
                        difficulty = 1,
                        spawnWeight = 1f
                    };
                    monsters.Add(entry);
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[MonsterPool] Found {monsters.Count} monster prefabs");
        }
#endif
    }
}