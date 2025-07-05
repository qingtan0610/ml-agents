using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Enemy;
using NPC.Core;
using Inventory.Items;
using Rooms;
using Rooms.Core;
using Loot;

namespace AI.Perception
{
    /// <summary>
    /// AI感知系统 - 负责收集环境信息
    /// </summary>
    public class AIPerception : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float visionRange = 8f; // 当前房间视野范围
        [SerializeField] private bool hasEnhancedVision = false; // 是否有增强视野（药水效果）
        [SerializeField] private LayerMask visionBlockingLayers; // 阻挡视线的层
        
        [Header("Detection Settings")]
        [SerializeField] private float enemyDetectionRange = 10f;
        [SerializeField] private float npcDetectionRange = 8f;
        [SerializeField] private float itemDetectionRange = 5f;
        [SerializeField] private float updateInterval = 0.2f;
        
        [Header("Current Perception")]
        [SerializeField] private List<RoomInfo> visibleRooms = new List<RoomInfo>();
        [SerializeField] private List<Enemy2D> nearbyEnemies = new List<Enemy2D>();
        [SerializeField] private List<NPCBase> nearbyNPCs = new List<NPCBase>();
        [SerializeField] private List<GameObject> nearbyItems = new List<GameObject>();
        [SerializeField] private SimplifiedRoom currentRoom;
        
        // 事件
        public System.Action<SimplifiedRoom> OnRoomDiscovered;
        public System.Action<Enemy2D> OnEnemyDetected;
        public System.Action<NPCBase> OnNPCDetected;
        public System.Action<GameObject> OnItemDetected;
        
        private float nextUpdateTime = 0f;
        private HashSet<SimplifiedRoom> discoveredRooms = new HashSet<SimplifiedRoom>();
        private bool discoveredNewRoomThisFrame = false;
        
        // 缓存的引用
        private MapGenerator mapGenerator;
        
        private void Start()
        {
            // 初始化视线阻挡层
            if (visionBlockingLayers == 0)
            {
                visionBlockingLayers = LayerMask.GetMask("Wall");
            }
            
            // 缓存MapGenerator引用
            mapGenerator = FindObjectOfType<MapGenerator>();
        }
        
        private void Update()
        {
            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + updateInterval;
                UpdatePerception();
            }
            
            // 重置帧标记
            discoveredNewRoomThisFrame = false;
        }
        
        private void UpdatePerception()
        {
            // 更新当前房间
            UpdateCurrentRoom();
            
            // 更新可见房间
            UpdateVisibleRooms();
            
            // 检测附近的实体
            DetectNearbyEnemies();
            DetectNearbyNPCs();
            DetectNearbyItems();
        }
        
        private void UpdateCurrentRoom()
        {
            // 获取当前所在的房间
            if (mapGenerator != null)
            {
                Vector2Int roomCoord = mapGenerator.GetRoomCoordinate(transform.position);
                var room = mapGenerator.GetRoomAt(roomCoord.x, roomCoord.y);
                
                if (room != null && room != currentRoom)
                {
                    currentRoom = room;
                    
                    // 检查是否是新发现的房间
                    if (!discoveredRooms.Contains(room))
                    {
                        discoveredRooms.Add(room);
                        discoveredNewRoomThisFrame = true;
                        OnRoomDiscovered?.Invoke(room);
                    }
                }
            }
        }
        
        private void UpdateVisibleRooms()
        {
            visibleRooms.Clear();
            
            if (currentRoom == null) return;
            
            // 添加当前房间信息
            visibleRooms.Add(new RoomInfo
            {
                Room = currentRoom,
                RoomType = GetRoomType(currentRoom),
                IsExplored = currentRoom.IsExplored,
                IsCleared = currentRoom.IsCleared,
                Position = currentRoom.transform.position
            });
            
            // 如果有增强视野，添加相邻房间
            if (hasEnhancedVision && mapGenerator != null)
            {
                Vector2Int currentCoord = mapGenerator.GetRoomCoordinate(transform.position);
                
                // 检查四个方向的相邻房间
                Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var dir in directions)
                {
                    Vector2Int neighborCoord = currentCoord + dir;
                    var neighborRoom = mapGenerator.GetRoomAt(neighborCoord.x, neighborCoord.y);
                    
                    if (neighborRoom != null)
                    {
                        visibleRooms.Add(new RoomInfo
                        {
                            Room = neighborRoom,
                            RoomType = GetRoomType(neighborRoom),
                            IsExplored = neighborRoom.IsExplored,
                            IsCleared = neighborRoom.IsCleared,
                            Position = neighborRoom.transform.position
                        });
                    }
                }
            }
        }
        
        private RoomType GetRoomType(SimplifiedRoom room)
        {
            // 根据房间的特征判断房间类型
            if (room.name.Contains("Spawn")) return RoomType.Spawn;
            if (room.name.Contains("Monster")) return RoomType.Monster;
            if (room.name.Contains("Treasure")) return RoomType.Treasure;
            if (room.name.Contains("Fountain")) return RoomType.Fountain;
            if (room.name.Contains("Restaurant")) return RoomType.Restaurant;
            if (room.name.Contains("Merchant")) return RoomType.Merchant;
            if (room.name.Contains("Blacksmith")) return RoomType.Blacksmith;
            if (room.name.Contains("Doctor")) return RoomType.Doctor;
            if (room.name.Contains("Tailor")) return RoomType.Tailor;
            if (room.name.Contains("Portal")) return RoomType.Portal;
            
            return RoomType.Empty;
        }
        
        private void DetectNearbyEnemies()
        {
            nearbyEnemies.Clear();
            
            // 使用OverlapCircle检测敌人
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, enemyDetectionRange);
            
            foreach (var collider in colliders)
            {
                var enemy = collider.GetComponent<Enemy2D>();
                if (enemy != null && enemy.gameObject.activeSelf && CanSee(enemy.transform.position))
                {
                    nearbyEnemies.Add(enemy);
                    
                    // 触发事件
                    OnEnemyDetected?.Invoke(enemy);
                }
            }
            
            // 按距离排序
            nearbyEnemies = nearbyEnemies.OrderBy(e => 
                Vector2.Distance(transform.position, e.transform.position)).ToList();
        }
        
        private void DetectNearbyNPCs()
        {
            nearbyNPCs.Clear();
            
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, npcDetectionRange);
            
            foreach (var collider in colliders)
            {
                var npc = collider.GetComponent<NPCBase>();
                if (npc != null && CanSee(npc.transform.position))
                {
                    nearbyNPCs.Add(npc);
                    
                    // 触发事件
                    OnNPCDetected?.Invoke(npc);
                }
            }
        }
        
        private void DetectNearbyItems()
        {
            nearbyItems.Clear();
            
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, itemDetectionRange);
            
            foreach (var collider in colliders)
            {
                // 检查是否是可拾取物品
                if (collider.CompareTag("Pickup") || collider.GetComponent<Pickup>() != null)
                {
                    if (CanSee(collider.transform.position))
                    {
                        nearbyItems.Add(collider.gameObject);
                        
                        // 触发事件
                        OnItemDetected?.Invoke(collider.gameObject);
                    }
                }
            }
        }
        
        private bool CanSee(Vector3 targetPosition)
        {
            // 视线检测
            Vector2 direction = targetPosition - transform.position;
            float distance = direction.magnitude;
            
            // 检查是否在视野范围内
            if (distance > visionRange && !hasEnhancedVision)
                return false;
            
            // 射线检测是否有障碍物
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction.normalized, 
                distance, visionBlockingLayers);
            
            return hit.collider == null;
        }
        
        // 公共接口
        public List<RoomInfo> GetVisibleRooms() => new List<RoomInfo>(visibleRooms);
        public List<Enemy2D> GetNearbyEnemies() => new List<Enemy2D>(nearbyEnemies);
        public List<NPCBase> GetNearbyNPCs() => new List<NPCBase>(nearbyNPCs);
        public List<GameObject> GetNearbyItems() => new List<GameObject>(nearbyItems);
        public SimplifiedRoom GetCurrentRoom() => currentRoom;
        public bool DiscoveredNewRoom() => discoveredNewRoomThisFrame;
        
        public void SetEnhancedVision(bool enhanced, float duration = 0f)
        {
            hasEnhancedVision = enhanced;
            
            if (enhanced && duration > 0f)
            {
                // 定时关闭增强视野
                CancelInvoke(nameof(DisableEnhancedVision));
                Invoke(nameof(DisableEnhancedVision), duration);
            }
        }
        
        private void DisableEnhancedVision()
        {
            hasEnhancedVision = false;
        }
        
        // 获取到特定位置的路径信息
        public bool CanReachPosition(Vector2 targetPosition)
        {
            // 简单的直线检测，后续可以集成A*寻路
            RaycastHit2D hit = Physics2D.Raycast(transform.position, 
                targetPosition - (Vector2)transform.position, 
                Vector2.Distance(transform.position, targetPosition), 
                visionBlockingLayers);
            
            return hit.collider == null;
        }
        
        // Gizmos可视化
        private void OnDrawGizmosSelected()
        {
            // 绘制视野范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, visionRange);
            
            if (hasEnhancedVision)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, visionRange * 2f);
            }
            
            // 绘制检测范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, npcDetectionRange);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, itemDetectionRange);
        }
    }
    
    [System.Serializable]
    public class RoomInfo
    {
        public SimplifiedRoom Room;
        public RoomType RoomType;
        public bool IsExplored;
        public bool IsCleared;
        public Vector3 Position;
    }
    
    public enum RoomType
    {
        Empty = 0,
        Spawn = 1,
        Monster = 2,
        Treasure = 3,
        Fountain = 4,
        Restaurant = 5,
        Merchant = 6,
        Blacksmith = 7,
        Doctor = 8,
        Tailor = 9,
        Portal = 10
    }
}