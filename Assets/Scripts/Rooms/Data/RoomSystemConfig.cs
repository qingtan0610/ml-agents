using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Rooms.Core;

namespace Rooms.Data
{
    /// <summary>
    /// 房间系统配置 - 一个文件配置所有房间类型
    /// </summary>
    [CreateAssetMenu(fileName = "RoomSystemConfig", menuName = "Rooms/Room System Config")]
    public class RoomSystemConfig : ScriptableObject
    {
        [Header("出生房间配置")]
        [SerializeField] private RoomBinding spawnRoom = new RoomBinding { 
            displayName = "出生房间", 
            minimapColor = Color.green,
            minPerMap = 1,
            maxPerMap = 1
        };
        
        [Header("怪物房间配置")]
        [SerializeField] private MonsterRoomBinding monsterRoom = new MonsterRoomBinding { 
            displayName = "怪物房间", 
            minimapColor = Color.red,
            minPerMap = 10,
            maxPerMap = 20,
            lockDoorsOnEntry = true,
            requiresClearance = true,
            spawnWeight = 3f // 更高的权重，更常见
        };
        
        [Header("宝箱房间配置")]
        [SerializeField] private SpecialRoomBinding treasureRoom = new SpecialRoomBinding { 
            displayName = "宝箱房间", 
            minimapColor = Color.yellow,
            minPerMap = 3,
            maxPerMap = 6
        };
        
        [Header("泉水房间配置")]
        [SerializeField] private SpecialRoomBinding fountainRoom = new SpecialRoomBinding { 
            displayName = "泉水房间", 
            minimapColor = Color.cyan,
            minPerMap = 2,
            maxPerMap = 4
        };
        
        [Header("传送房间配置")]
        [SerializeField] private SpecialRoomBinding teleportRoom = new SpecialRoomBinding { 
            displayName = "传送房间", 
            minimapColor = Color.magenta,
            minPerMap = 1,
            maxPerMap = 1
        };
        
        [Header("NPC房间配置")]
        [SerializeField] private NPCRoomBinding npcRoom = new NPCRoomBinding { 
            displayName = "NPC房间", 
            minimapColor = Color.blue,
            minPerMap = 3,
            maxPerMap = 8,
            spawnWeight = 2f // 较高权重
        };
        
        [Header("Boss房间配置")]
        [SerializeField] private BossRoomBinding bossRoom = new BossRoomBinding { 
            displayName = "Boss房间", 
            minimapColor = new Color(0.5f, 0f, 0f),
            minPerMap = 0,
            maxPerMap = 1,
            minMapLevel = 5
        };
        
        // 基础房间绑定
        [System.Serializable]
        public class RoomBinding
        {
            [Header("基础配置")]
            public string displayName = "未命名房间";
            public Color minimapColor = Color.gray;
            public RoomPool roomPool; // 这个类型使用的房间池
            
            [Header("生成规则")]
            public int minPerMap = 0;
            public int maxPerMap = 99;
            [Range(0f, 1f)] public float spawnWeight = 1f;
            public int minMapLevel = 1;
        }
        
        // 怪物房间绑定
        [System.Serializable]
        public class MonsterRoomBinding : RoomBinding
        {
            [Header("怪物配置")]
            public Pools.MonsterPool monsterPool;
            public int minMonsterCount = 3;
            public int maxMonsterCount = 6;
            public int maxMonsterDifficulty = 5;
            public bool lockDoorsOnEntry = true;
            public bool requiresClearance = true;
        }
        
        // 特殊物体房间绑定
        [System.Serializable]
        public class SpecialRoomBinding : RoomBinding
        {
            [Header("特殊物体")]
            public GameObject specialPrefab;
            public bool spawnInCenter = true;
        }
        
        // NPC房间绑定
        [System.Serializable]
        public class NPCRoomBinding : RoomBinding
        {
            [Header("NPC配置")]
            public NPC.Core.NPCPool npcPool;
            public bool allowRandomNPC = true; // 允许从池中随机选择任意NPC
        }
        
        // Boss房间绑定
        [System.Serializable]
        public class BossRoomBinding : MonsterRoomBinding
        {
            [Header("Boss特殊配置")]
            public bool isMiniBoss = false;
            public int bossHealthMultiplier = 5;
        }
        
        /// <summary>
        /// 根据房间类型获取配置
        /// </summary>
        public RoomBinding GetBinding(RoomType roomType)
        {
            switch (roomType)
            {
                case RoomType.Spawn: return spawnRoom;
                case RoomType.Monster: return monsterRoom;
                case RoomType.Treasure: return treasureRoom;
                case RoomType.Fountain: return fountainRoom;
                case RoomType.Teleport: return teleportRoom;
                case RoomType.NPC: return npcRoom;
                case RoomType.Boss: return bossRoom;
                
                // 兼容旧类型
                case RoomType.Restaurant:
                case RoomType.Merchant:
                case RoomType.Blacksmith:
                case RoomType.Doctor:
                case RoomType.Tailor:
                    Debug.LogWarning($"[RoomSystemConfig] Obsolete room type {roomType}, using NPC room instead");
                    return npcRoom;
                    
                default: return null;
            }
        }
        
        /// <summary>
        /// 获取指定类型的房间预制体
        /// </summary>
        public GameObject GetRoomPrefab(RoomType roomType)
        {
            var binding = GetBinding(roomType);
            if (binding?.roomPool == null)
            {
                Debug.LogError($"[RoomSystemConfig] No room pool bound for type {roomType}!");
                return null;
            }
            
            return binding.roomPool.GetRandomRoomPrefab();
        }
        
        /// <summary>
        /// 获取适合当前地图等级的房间类型
        /// </summary>
        public List<(RoomType type, RoomBinding binding)> GetAvailableTypes(int mapLevel)
        {
            var result = new List<(RoomType, RoomBinding)>();
            
            // 检查每种房间类型
            if (spawnRoom.roomPool != null && spawnRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.Spawn, spawnRoom));
                
            if (monsterRoom.roomPool != null && monsterRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.Monster, monsterRoom));
                
            if (treasureRoom.roomPool != null && treasureRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.Treasure, treasureRoom));
                
            if (fountainRoom.roomPool != null && fountainRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.Fountain, fountainRoom));
                
            if (teleportRoom.roomPool != null && teleportRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.Teleport, teleportRoom));
                
            if (npcRoom.roomPool != null && npcRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.NPC, npcRoom));
                
            if (bossRoom.roomPool != null && bossRoom.minMapLevel <= mapLevel)
                result.Add((RoomType.Boss, bossRoom));
                
            return result;
        }
        
        /// <summary>
        /// 根据权重随机选择房间类型
        /// </summary>
        public RoomType GetRandomRoomType(int mapLevel)
        {
            var available = GetAvailableTypes(mapLevel);
            if (available.Count == 0)
                return RoomType.Empty;
            
            // 排除特殊房间类型（这些应该被特别放置）
            available = available.Where(x => 
                x.type != RoomType.Teleport && // 传送房间只能有一个
                x.type != RoomType.Spawn &&    // 出生房间只能有一个
                x.binding.spawnWeight > 0      // 排除权重为0的房间
            ).ToList();
            
            if (available.Count == 0)
                return RoomType.Monster; // 默认返回怪物房间
            
            float totalWeight = available.Sum(x => x.binding.spawnWeight);
            float random = Random.Range(0f, totalWeight);
            
            float currentWeight = 0;
            foreach (var (type, binding) in available)
            {
                currentWeight += binding.spawnWeight;
                if (random <= currentWeight)
                    return type;
            }
            
            return available[0].type;
        }
        
        /// <summary>
        /// 获取指定类型的怪物池
        /// </summary>
        public Pools.MonsterPool GetMonsterPool(RoomType roomType)
        {
            switch (roomType)
            {
                case RoomType.Monster:
                    return monsterRoom.monsterPool;
                case RoomType.Boss:
                    return bossRoom.monsterPool;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// 获取NPC池
        /// </summary>
        public NPC.Core.NPCPool GetNPCPool(RoomType roomType)
        {
            switch (roomType)
            {
                case RoomType.NPC:
                    return npcRoom.npcPool;
                    
                // 兼容旧类型
                case RoomType.Restaurant:
                case RoomType.Merchant:
                case RoomType.Blacksmith:
                case RoomType.Doctor:
                case RoomType.Tailor:
                    return npcRoom.npcPool;
                    
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// 获取指定类型的特殊预制体
        /// </summary>
        public GameObject GetSpecialPrefab(RoomType roomType)
        {
            switch (roomType)
            {
                case RoomType.Treasure:
                    return treasureRoom.specialPrefab;
                case RoomType.Fountain:
                    return fountainRoom.specialPrefab;
                case RoomType.Teleport:
                    return teleportRoom.specialPrefab;
                default:
                    return null;
            }
        }
    }
}