using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Enemy;
using NPC.Core;
using Inventory.Items;
using Rooms;
using Rooms.Core;
using Loot;
using Combat;
using AI.Core;
using Interactables;

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
        [SerializeField] private float projectileDetectionRange = 8f; // 弹药检测范围
        [SerializeField] private float teammateDetectionRange = 15f; // 队友检测范围
        [SerializeField] private float updateInterval = 0.2f;
        
        [Header("Current Perception")]
        [SerializeField] private List<RoomInfo> visibleRooms = new List<RoomInfo>();
        [SerializeField] private List<Enemy2D> nearbyEnemies = new List<Enemy2D>();
        [SerializeField] private List<NPCBase> nearbyNPCs = new List<NPCBase>();
        [SerializeField] private List<GameObject> nearbyItems = new List<GameObject>();
        [SerializeField] private List<Combat.Projectile2D> nearbyProjectiles = new List<Combat.Projectile2D>();
        [SerializeField] private List<AIBrain> nearbyTeammates = new List<AIBrain>();
        [SerializeField] private SimplifiedRoom currentRoom;
        
        // 队友战斗状态追踪
        private Dictionary<AIBrain, float> teammateLastAttackTime = new Dictionary<AIBrain, float>();
        
        // 事件
        public System.Action<SimplifiedRoom> OnRoomDiscovered;
        public System.Action<Enemy2D> OnEnemyDetected;
        public System.Action<NPCBase> OnNPCDetected;
        public System.Action<GameObject> OnItemDetected;
        public System.Action<Combat.Projectile2D> OnProjectileDetected;
        public System.Action<AIBrain> OnTeammateDetected;
        public System.Action<AIBrain, Enemy2D> OnTeammateAttacking;
        
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
            
            // 订阅所有AI的攻击事件以追踪队友战斗状态
            var allAIs = FindObjectsOfType<AIBrain>();
            foreach (var ai in allAIs)
            {
                if (ai != this)
                {
                    var combatSystem = ai.GetComponent<CombatSystem2D>();
                    if (combatSystem != null)
                    {
                        combatSystem.OnDamageDealt += (target, damage) => OnTeammateAttack(ai, target);
                    }
                }
            }
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
            DetectNearbyProjectiles();
            DetectNearbyTeammates();
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
            
            // 使用OverlapCircle检测敌人 - 使用Layer 12 (Enemy)
            int enemyLayer = 1 << 12; // Layer 12 是 Enemy
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, enemyDetectionRange, enemyLayer);
            
            // 调试输出
            if (colliders.Length > 0)
            {
                Debug.Log($"[AIPerception] {name} 检测到 {colliders.Length} 个Enemy层的碰撞体");
            }
            
            foreach (var collider in colliders)
            {
                var enemy = collider.GetComponent<Enemy2D>();
                if (enemy != null && enemy.gameObject.activeSelf)
                {
                    // 暂时跳过CanSee检查，先确保基础检测工作
                    nearbyEnemies.Add(enemy);
                    Debug.Log($"[AIPerception] {name} 检测到敌人: {enemy.name} 距离: {Vector2.Distance(transform.position, enemy.transform.position)}");
                    
                    // 触发事件
                    OnEnemyDetected?.Invoke(enemy);
                }
                else if (enemy == null)
                {
                    Debug.LogWarning($"[AIPerception] 在Enemy层找到碰撞体 {collider.name} 但没有Enemy2D组件");
                }
            }
            
            // 手动排序避免LINQ的ToList操作 - 防止卡死
            if (nearbyEnemies.Count > 1)
            {
                nearbyEnemies.Sort((e1, e2) => 
                {
                    float dist1 = Vector2.Distance(transform.position, e1.transform.position);
                    float dist2 = Vector2.Distance(transform.position, e2.transform.position);
                    return dist1.CompareTo(dist2);
                });
            }
        }
        
        private void DetectNearbyNPCs()
        {
            nearbyNPCs.Clear();
            
            // 使用Layer 13 (NPC)
            int npcLayer = 1 << 13;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, npcDetectionRange, npcLayer);
            
            foreach (var collider in colliders)
            {
                var npc = collider.GetComponent<NPCBase>();
                if (npc != null) // 暂时跳过CanSee检查
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
            
            // 检测多个层: Item (14), Interactive (15)
            int itemLayers = (1 << 14) | (1 << 15);
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, itemDetectionRange, itemLayers);
            
            // 也检测所有层的Pickup标签物体
            Collider2D[] allColliders = Physics2D.OverlapCircleAll(transform.position, itemDetectionRange);
            
            // 合并结果
            var combinedColliders = new List<Collider2D>(colliders);
            foreach (var col in allColliders)
            {
                if (col.CompareTag("Pickup") && !combinedColliders.Contains(col))
                {
                    combinedColliders.Add(col);
                }
            }
            
            foreach (var collider in combinedColliders)
            {
                bool isInteractable = false;
                
                // 检查是否是可拾取物品
                if (collider.CompareTag("Pickup") || collider.GetComponent<Pickup>() != null ||
                    collider.GetComponent<UnifiedPickup>() != null)
                {
                    isInteractable = true;
                }
                
                // 检查是否是交互物品（宝箱、泉水、传送门等）
                if (collider.GetComponent<IInteractable>() != null)
                {
                    isInteractable = true;
                }
                
                // 检查特定的交互物品类型
                if (collider.GetComponent<Interactables.TreasureChest>() != null ||
                    collider.GetComponent<Interactables.Fountain>() != null ||
                    collider.GetComponent<Interactables.TeleportDevice>() != null ||
                    collider.CompareTag("Interactive"))
                {
                    isInteractable = true;
                }
                
                if (isInteractable) // 暂时跳过CanSee检查
                {
                    nearbyItems.Add(collider.gameObject);
                    
                    // 触发事件
                    OnItemDetected?.Invoke(collider.gameObject);
                }
            }
        }
        
        private void DetectNearbyProjectiles()
        {
            nearbyProjectiles.Clear();
            
            // 检测附近的弹药 - 使用Layer 16 (Projectile)
            int projectileLayer = 1 << 16;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, projectileDetectionRange, projectileLayer);
            
            foreach (var collider in colliders)
            {
                var projectile = collider.GetComponent<Combat.Projectile2D>();
                if (projectile != null && projectile.gameObject.activeSelf)
                {
                    // 只检测敌人发射的弹药
                    if (projectile.Owner != null && (projectile.Owner.CompareTag("Enemy") || projectile.Owner.layer == 12))
                    {
                        nearbyProjectiles.Add(projectile);
                        
                        // 触发事件
                        OnProjectileDetected?.Invoke(projectile);
                        
                        // 如果弹药正朝向AI飞来，给予警告
                        var rb = projectile.GetComponent<Rigidbody2D>();
                        if (rb != null && rb.velocity.magnitude > 0.1f)
                        {
                            Vector2 toAI = (transform.position - projectile.transform.position).normalized;
                            Vector2 projectileDir = rb.velocity.normalized;
                            float dot = Vector2.Dot(projectileDir, toAI);
                            
                            if (dot > 0.7f) // 弹药大致朝向AI
                            {
                                Debug.Log($"[AIPerception] {name} 检测到来袭弹药！距离: {Vector2.Distance(transform.position, projectile.transform.position):F1}");
                            }
                        }
                    }
                }
            }
            
            // 按距离排序
            if (nearbyProjectiles.Count > 1)
            {
                nearbyProjectiles.Sort((p1, p2) => 
                {
                    float dist1 = Vector2.Distance(transform.position, p1.transform.position);
                    float dist2 = Vector2.Distance(transform.position, p2.transform.position);
                    return dist1.CompareTo(dist2);
                });
            }
        }
        
        private void DetectNearbyTeammates()
        {
            nearbyTeammates.Clear();
            
            // 检测附近的队友AI - 使用Layer 11 (Player)
            int playerLayer = 1 << 11;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, teammateDetectionRange, playerLayer);
            
            foreach (var collider in colliders)
            {
                // 跳过自己
                if (collider.gameObject == gameObject) continue;
                
                // 检查是否是AI队友
                var otherBrain = collider.GetComponent<AIBrain>();
                if (otherBrain != null && otherBrain.gameObject.activeSelf)
                {
                    nearbyTeammates.Add(otherBrain);
                    
                    // 触发事件
                    OnTeammateDetected?.Invoke(otherBrain);
                    
                    // 检查队友是否在战斗
                    var combatSystem = otherBrain.GetComponent<Combat.CombatSystem2D>();
                    if (combatSystem != null)
                    {
                        // 检查最近的攻击时间
                        float lastAttackTime = 0f;
                        if (teammateLastAttackTime.TryGetValue(otherBrain, out lastAttackTime))
                        {
                            if (Time.time - lastAttackTime < 2f) // 2秒内有攻击行为
                            {
                                // 找到队友正在攻击的敌人
                                var teammateController = otherBrain.GetComponent<AIController>();
                                if (teammateController != null && teammateController.GetCurrentTarget() != null)
                                {
                                    var targetEnemy = teammateController.GetCurrentTarget().GetComponent<Enemy2D>();
                                    if (targetEnemy != null)
                                    {
                                        OnTeammateAttacking?.Invoke(otherBrain, targetEnemy);
                                        Debug.Log($"[AIPerception] {name} 发现队友 {otherBrain.name} 正在攻击 {targetEnemy.name}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // 按距离排序
            if (nearbyTeammates.Count > 1)
            {
                nearbyTeammates.Sort((t1, t2) => 
                {
                    float dist1 = Vector2.Distance(transform.position, t1.transform.position);
                    float dist2 = Vector2.Distance(transform.position, t2.transform.position);
                    return dist1.CompareTo(dist2);
                });
            }
        }
        
        private bool CanSee(Vector3 targetPosition)
        {
            // 防止NaN和无效位置导致卡死
            if (float.IsNaN(targetPosition.x) || float.IsNaN(targetPosition.y) || 
                float.IsInfinity(targetPosition.x) || float.IsInfinity(targetPosition.y))
            {
                return false;
            }
            
            // 视线检测
            Vector2 direction = targetPosition - transform.position;
            float distance = direction.magnitude;
            
            // 防止距离为0或无效值
            if (distance < 0.01f || float.IsNaN(distance) || float.IsInfinity(distance))
                return false;
            
            // 检查是否在视野范围内
            if (distance > visionRange && !hasEnhancedVision)
                return false;
            
            // 防止方向向量无效
            Vector2 normalizedDirection = direction.normalized;
            if (float.IsNaN(normalizedDirection.x) || float.IsNaN(normalizedDirection.y))
                return false;
            
            // 射线检测是否有障碍物 - 添加安全检查
            try
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, normalizedDirection, 
                    distance, visionBlockingLayers);
                
                return hit.collider == null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIPerception] 射线检测异常: {e.Message}");
                return false;
            }
        }
        
        // 缓存的公共接口结果 - 避免频繁的List创建
        private List<RoomInfo> cachedVisibleRooms = new List<RoomInfo>();
        private List<Enemy2D> cachedNearbyEnemies = new List<Enemy2D>();
        private List<NPCBase> cachedNearbyNPCs = new List<NPCBase>();
        private List<GameObject> cachedNearbyItems = new List<GameObject>();
        private List<Combat.Projectile2D> cachedNearbyProjectiles = new List<Combat.Projectile2D>();
        private List<AIBrain> cachedNearbyTeammates = new List<AIBrain>();
        private float lastCacheUpdateTime = 0f;
        private const float CACHE_UPDATE_INTERVAL = 0.1f; // 每0.1秒更新一次缓存
        
        // 公共接口 - 使用缓存避免频繁的List创建
        public List<RoomInfo> GetVisibleRooms() 
        {
            UpdateCacheIfNeeded();
            return cachedVisibleRooms;
        }
        
        public List<Enemy2D> GetNearbyEnemies() 
        {
            UpdateCacheIfNeeded();
            return cachedNearbyEnemies;
        }
        
        public List<NPCBase> GetNearbyNPCs() 
        {
            UpdateCacheIfNeeded();
            return cachedNearbyNPCs;
        }
        
        public List<GameObject> GetNearbyItems() 
        {
            UpdateCacheIfNeeded();
            return cachedNearbyItems;
        }
        
        public List<Projectile2D> GetNearbyProjectiles()
        {
            UpdateCacheIfNeeded();
            return cachedNearbyProjectiles;
        }
        
        public List<AIBrain> GetNearbyTeammates()
        {
            UpdateCacheIfNeeded();
            return cachedNearbyTeammates;
        }
        
        private void UpdateCacheIfNeeded()
        {
            if (Time.time - lastCacheUpdateTime > CACHE_UPDATE_INTERVAL)
            {
                // 更新缓存
                cachedVisibleRooms.Clear();
                cachedVisibleRooms.AddRange(visibleRooms);
                
                cachedNearbyEnemies.Clear();
                cachedNearbyEnemies.AddRange(nearbyEnemies);
                
                cachedNearbyNPCs.Clear();
                cachedNearbyNPCs.AddRange(nearbyNPCs);
                
                cachedNearbyItems.Clear();
                cachedNearbyItems.AddRange(nearbyItems);
                
                cachedNearbyProjectiles.Clear();
                cachedNearbyProjectiles.AddRange(nearbyProjectiles);
                
                cachedNearbyTeammates.Clear();
                cachedNearbyTeammates.AddRange(nearbyTeammates);
                
                lastCacheUpdateTime = Time.time;
            }
        }
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
            
            // 绘制弹药检测范围
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, projectileDetectionRange);
            
            // 绘制队友检测范围
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, teammateDetectionRange);
        }
        
        // 调试方法
        public void DebugPerception()
        {
            Debug.Log($"[AIPerception] {name} 感知调试:");
            Debug.Log($"  - 检测到敌人: {nearbyEnemies.Count}");
            foreach (var enemy in nearbyEnemies)
            {
                Debug.Log($"    * {enemy.name} 距离: {Vector2.Distance(transform.position, enemy.transform.position):F1}");
            }
            Debug.Log($"  - 检测到NPC: {nearbyNPCs.Count}");
            Debug.Log($"  - 检测到物品: {nearbyItems.Count}");
            Debug.Log($"  - 检测到弹药: {nearbyProjectiles.Count}");
            Debug.Log($"  - 检测到队友: {nearbyTeammates.Count}");
        }
        
        // 队友攻击回调
        private void OnTeammateAttack(AIBrain teammate, GameObject target)
        {
            if (teammate != null && target != null)
            {
                // 记录队友的攻击时间
                teammateLastAttackTime[teammate] = Time.time;
            }
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