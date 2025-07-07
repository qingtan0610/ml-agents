using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI.Perception;
using NPC;

namespace AI.Core
{
    /// <summary>
    /// AI记忆系统 - 存储重要位置和事件
    /// </summary>
    public class AIMemory
    {
        // 重要位置记忆
        private Dictionary<string, LocationMemory> importantLocations = new Dictionary<string, LocationMemory>();
        
        // 事件记忆
        private List<EventMemory> recentEvents = new List<EventMemory>();
        private const int MAX_EVENTS = 50;
        
        // 其他AI的位置记忆
        private Dictionary<string, Vector2> otherAIPositions = new Dictionary<string, Vector2>();
        
        // 危险区域记忆
        private List<DangerZone> dangerZones = new List<DangerZone>();
        
        // 资源点记忆
        private Dictionary<ResourceType, List<Vector2>> resourceLocations = new Dictionary<ResourceType, List<Vector2>>();
        
        public AIMemory()
        {
            // 初始化资源位置字典
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                resourceLocations[type] = new List<Vector2>();
            }
        }
        
        // 更新记忆
        // 清理记忆的间隔时间
        private float lastCleanupTime = 0f;
        private const float CLEANUP_INTERVAL = 5f; // 每5秒清理一次而不是每帧
        
        public void Update(AIPerception perception)
        {
            if (perception == null) return;
            
            // 更新房间信息
            var visibleRooms = perception.GetVisibleRooms();
            foreach (var roomInfo in visibleRooms)
            {
                UpdateRoomMemory(roomInfo);
            }
            
            // 更新NPC位置
            var nearbyNPCs = perception.GetNearbyNPCs();
            foreach (var npc in nearbyNPCs)
            {
                UpdateNPCMemory(npc);
            }
            
            // 定期清理过期记忆（避免每帧执行LINQ操作）
            if (Time.time - lastCleanupTime > CLEANUP_INTERVAL)
            {
                lastCleanupTime = Time.time;
                CleanupOldMemories();
            }
        }
        
        private void UpdateRoomMemory(RoomInfo roomInfo)
        {
            string key = GetRoomKey(roomInfo.RoomType);
            if (string.IsNullOrEmpty(key)) return;
            
            // 更新或添加位置记忆
            if (!importantLocations.ContainsKey(key))
            {
                importantLocations[key] = new LocationMemory
                {
                    LocationName = key,
                    Position = roomInfo.Position,
                    RoomType = roomInfo.RoomType,
                    LastVisitTime = Time.time,
                    Importance = GetRoomImportance(roomInfo.RoomType)
                };
                
                // 记录发现事件
                AddEvent(new EventMemory
                {
                    EventType = EventType.Discovery,
                    Description = $"发现了{key}",
                    Position = roomInfo.Position,
                    Time = Time.time,
                    Importance = 3
                });
            }
            else
            {
                // 更新访问时间
                importantLocations[key].LastVisitTime = Time.time;
            }
        }
        
        private void UpdateNPCMemory(NPC.Core.NPCBase npc)
        {
            string npcKey = GetNPCKey(npc.NPCType);
            if (string.IsNullOrEmpty(npcKey)) return;
            
            // 更新NPC位置
            if (!importantLocations.ContainsKey(npcKey))
            {
                importantLocations[npcKey] = new LocationMemory
                {
                    LocationName = npcKey,
                    Position = npc.transform.position,
                    RoomType = GetRoomTypeFromNPC(npc.NPCType),
                    LastVisitTime = Time.time,
                    Importance = 4,
                    AdditionalInfo = npc.Data?.npcName ?? npc.NPCType.ToString()
                };
            }
        }
        
        // 记录事件
        public void AddEvent(EventMemory eventMemory)
        {
            recentEvents.Add(eventMemory);
            
            // 限制事件数量
            if (recentEvents.Count > MAX_EVENTS)
            {
                recentEvents.RemoveAt(0);
            }
            
            // 根据事件类型更新其他记忆
            ProcessEvent(eventMemory);
        }
        
        // 记录事件（简化版本）
        public void RecordEvent(string eventType, string description)
        {
            AddEvent(new EventMemory
            {
                EventType = EventType.Other,
                Description = $"{eventType}: {description}",
                Position = Vector2.zero,
                Time = Time.time,
                Importance = 2
            });
        }
        
        private void ProcessEvent(EventMemory eventMemory)
        {
            switch (eventMemory.EventType)
            {
                case EventType.Combat:
                    // 标记危险区域
                    AddDangerZone(eventMemory.Position, 5f, eventMemory.Time + 300f); // 5分钟记忆
                    break;
                    
                case EventType.Death:
                    // 重要事件，增加危险等级
                    AddDangerZone(eventMemory.Position, 10f, eventMemory.Time + 600f); // 10分钟记忆
                    break;
                    
                case EventType.ResourceFound:
                    // 记录资源位置
                    var resourceType = ParseResourceType(eventMemory.Description);
                    if (resourceType != ResourceType.Unknown)
                    {
                        AddResourceLocation(resourceType, eventMemory.Position);
                    }
                    break;
            }
        }
        
        // 记录其他AI的通信
        public void RecordAICommunication(string aiName, Vector2 position, CommunicationType type)
        {
            // 更新AI位置
            otherAIPositions[aiName] = position;
            
            // 根据通信类型处理
            switch (type)
            {
                case CommunicationType.Help:
                    AddDangerZone(position, 8f, Time.time + 180f);
                    AddEvent(new EventMemory
                    {
                        EventType = EventType.Communication,
                        Description = $"{aiName}在求救",
                        Position = position,
                        Time = Time.time,
                        Importance = 5
                    });
                    break;
                    
                case CommunicationType.FoundWater:
                    AddResourceLocation(ResourceType.Water, position);
                    break;
                    
                case CommunicationType.FoundPortal:
                    importantLocations["Portal"] = new LocationMemory
                    {
                        LocationName = "Portal",
                        Position = position,
                        RoomType = RoomType.Portal,
                        LastVisitTime = Time.time,
                        Importance = 10
                    };
                    break;
            }
        }
        
        // 添加危险区域
        private void AddDangerZone(Vector2 position, float radius, float expiryTime)
        {
            // 检查是否已存在相近的危险区域
            var existingZone = dangerZones.FirstOrDefault(z => 
                Vector2.Distance(z.Center, position) < radius);
            
            if (existingZone != null)
            {
                // 更新现有区域
                existingZone.DangerLevel++;
                existingZone.ExpiryTime = Mathf.Max(existingZone.ExpiryTime, expiryTime);
            }
            else
            {
                // 添加新区域
                dangerZones.Add(new DangerZone
                {
                    Center = position,
                    Radius = radius,
                    DangerLevel = 1,
                    ExpiryTime = expiryTime
                });
            }
        }
        
        // 添加资源位置
        private void AddResourceLocation(ResourceType type, Vector2 position)
        {
            if (!resourceLocations[type].Any(p => Vector2.Distance(p, position) < 2f))
            {
                resourceLocations[type].Add(position);
                
                // 限制记忆数量
                if (resourceLocations[type].Count > 10)
                {
                    resourceLocations[type].RemoveAt(0);
                }
            }
        }
        
        // 清理过期记忆
        private void CleanupOldMemories()
        {
            // 清理过期的危险区域 - 使用RemoveAll比较安全
            dangerZones.RemoveAll(z => z.ExpiryTime < Time.time);
            
            // 清理太久没访问的位置 - 避免LINQ操作，使用传统循环
            var keysToRemove = new List<string>();
            foreach (var kvp in importantLocations)
            {
                if (kvp.Value.Importance < 5 && Time.time - kvp.Value.LastVisitTime > 600f)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            // 删除标记的键
            foreach (var key in keysToRemove)
            {
                importantLocations.Remove(key);
            }
            
            // 防止内存无限增长
            if (keysToRemove.Count > 0)
            {
                Debug.Log($"[AIMemory] 清理了 {keysToRemove.Count} 个过期位置记忆");
            }
        }
        
        // 查询接口
        public Dictionary<string, LocationMemory> GetImportantLocations() => new Dictionary<string, LocationMemory>(importantLocations);
        public List<EventMemory> GetRecentEvents(int count = 10) => recentEvents.TakeLast(count).ToList();
        public List<DangerZone> GetDangerZones() 
        {
            var activeDangerZones = new List<DangerZone>();
            foreach (var zone in dangerZones)
            {
                if (zone.ExpiryTime > Time.time)
                {
                    activeDangerZones.Add(zone);
                }
            }
            return activeDangerZones;
        }
        public List<Vector2> GetResourceLocations(ResourceType type) => new List<Vector2>(resourceLocations[type]);
        public Dictionary<string, object> GetAllMemories()
        {
            return new Dictionary<string, object>
            {
                ["ImportantLocations"] = importantLocations,
                ["RecentEvents"] = GetRecentEvents(20),
                ["DangerZones"] = GetDangerZones(),
                ["ResourceLocations"] = resourceLocations,
                ["OtherAIPositions"] = otherAIPositions
            };
        }
        
        // 查询最近的资源位置
        public Vector2? GetNearestResource(ResourceType type, Vector2 currentPosition)
        {
            var locations = resourceLocations[type];
            if (locations.Count == 0) return null;
            
            return locations.OrderBy(pos => Vector2.Distance(pos, currentPosition)).FirstOrDefault();
        }
        
        // 查询是否在危险区域
        public bool IsInDangerZone(Vector2 position)
        {
            return dangerZones.Any(z => 
                z.ExpiryTime > Time.time && 
                Vector2.Distance(z.Center, position) <= z.Radius);
        }
        
        // 获取到重要位置的路径建议
        public Vector2? GetPathToImportantLocation(string locationKey)
        {
            if (importantLocations.TryGetValue(locationKey, out var location))
            {
                return location.Position;
            }
            return null;
        }
        
        // 清空记忆（新地图时使用）
        public void Clear()
        {
            importantLocations.Clear();
            recentEvents.Clear();
            dangerZones.Clear();
            otherAIPositions.Clear();
            
            foreach (var list in resourceLocations.Values)
            {
                list.Clear();
            }
        }
        
        // 新增的辅助方法
        public bool KnowsPortalLocation()
        {
            return importantLocations.ContainsKey("Portal");
        }
        
        public float GetExplorationProgress()
        {
            // 基于访问的重要位置数量估算探索进度
            int knownLocations = importantLocations.Count;
            int knownResources = resourceLocations.Values.Sum(list => list.Count);
            
            // 假设完全探索需要知道20个位置
            float progress = (knownLocations + knownResources * 0.5f) / 20f;
            return Mathf.Clamp01(progress);
        }
        
        // 辅助方法
        private string GetRoomKey(RoomType type)
        {
            switch (type)
            {
                case RoomType.Portal: return "Portal";
                case RoomType.Fountain: return "Fountain";
                case RoomType.Restaurant: return "Restaurant";
                case RoomType.Merchant: return "Merchant";
                case RoomType.Blacksmith: return "Blacksmith";
                case RoomType.Doctor: return "Doctor";
                case RoomType.Tailor: return "Tailor";
                default: return null;
            }
        }
        
        private string GetNPCKey(NPCType type)
        {
            return type.ToString();
        }
        
        private RoomType GetRoomTypeFromNPC(NPCType npcType)
        {
            switch (npcType)
            {
                case NPCType.Merchant: return RoomType.Merchant;
                case NPCType.Blacksmith: return RoomType.Blacksmith;
                case NPCType.Doctor: return RoomType.Doctor;
                case NPCType.Tailor: return RoomType.Tailor;
                case NPCType.Restaurant: return RoomType.Restaurant;
                default: return RoomType.Empty;
            }
        }
        
        private int GetRoomImportance(RoomType type)
        {
            switch (type)
            {
                case RoomType.Portal: return 10;
                case RoomType.Fountain: return 7;
                case RoomType.Restaurant: return 6;
                case RoomType.Doctor: return 6;
                case RoomType.Merchant: return 5;
                case RoomType.Blacksmith: return 4;
                case RoomType.Tailor: return 4;
                case RoomType.Treasure: return 3;
                default: return 1;
            }
        }
        
        private ResourceType ParseResourceType(string description)
        {
            if (description.Contains("水") || description.Contains("Water")) return ResourceType.Water;
            if (description.Contains("食") || description.Contains("Food")) return ResourceType.Food;
            if (description.Contains("药") || description.Contains("Medicine")) return ResourceType.Medicine;
            if (description.Contains("武器") || description.Contains("Weapon")) return ResourceType.Weapon;
            if (description.Contains("弹药") || description.Contains("Ammo")) return ResourceType.Ammo;
            return ResourceType.Unknown;
        }
    }
    
    // 记忆数据结构
    [System.Serializable]
    public class LocationMemory
    {
        public string LocationName;
        public Vector2 Position;
        public RoomType RoomType;
        public float LastVisitTime;
        public int Importance; // 1-10
        public string AdditionalInfo;
    }
    
    [System.Serializable]
    public class EventMemory
    {
        public EventType EventType;
        public string Description;
        public Vector2 Position;
        public float Time;
        public int Importance; // 1-5
    }
    
    [System.Serializable]
    public class DangerZone
    {
        public Vector2 Center;
        public float Radius;
        public int DangerLevel;
        public float ExpiryTime;
    }
    
    public enum EventType
    {
        Discovery,
        Combat,
        Death,
        ResourceFound,
        Communication,
        Trade,
        Upgrade,
        Other
    }
    
    public enum ResourceType
    {
        Unknown,
        Water,
        Food,
        Medicine,
        Weapon,
        Ammo,
        Gold
    }
}