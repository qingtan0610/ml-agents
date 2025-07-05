using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Rooms.Core;
using Rooms.Data;

namespace Rooms
{
    /// <summary>
    /// 地图生成器，负责生成16x16的房间地图
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("Map Configuration")]
        [SerializeField] private int mapSize = 16; // 地图大小（16x16房间）
        [SerializeField] private int roomSize = 16; // 每个房间的大小
        [SerializeField] private int currentMapLevel = 1; // 当前地图等级
        
        // 公开当前地图等级供其他系统使用
        public int CurrentMapLevel => currentMapLevel;
        
        [Header("Room System Configuration")]
        [SerializeField] private RoomSystemConfig roomSystemConfig; // 房间系统配置
        
        [Header("Generation Settings")]
        [SerializeField] private Vector2Int spawnRoomPosition = new Vector2Int(8, 8); // 出生房间位置
        [SerializeField] private int minRoomsPerType = 1; // 每种类型房间的最小数量
        [SerializeField] private float roomDensity = 1.0f; // 房间密度（0-1），完全填满
        
        [Header("Runtime Data")]
        [SerializeField] private SimplifiedRoom[,] roomGrid; // 房间网格
        [SerializeField] private SimplifiedRoom currentRoom; // 当前房间
        [SerializeField] private List<GameObject> allPlayers = new List<GameObject>(); // 所有玩家
        
        // 事件
        public System.Action<SimplifiedRoom, SimplifiedRoom> OnRoomChanged; // 房间切换事件
        public System.Action<int> OnMapGenerated; // 地图生成完成事件
        
        private void Start()
        {
            // 导入房间配置
            LoadPoolConfigurations();
            
            GenerateMap();
        }
        
        private void LoadPoolConfigurations()
        {
            // 如果没有手动分配，尝试从Resources加载
            if (roomSystemConfig == null)
            {
                roomSystemConfig = Resources.Load<RoomSystemConfig>("RoomSystemConfig");
                if (roomSystemConfig == null)
                {
                    Debug.LogError("[MapGenerator] No RoomSystemConfig assigned and RoomSystemConfig not found in Resources!");
                }
            }
            
            Debug.Log($"[MapGenerator] Loaded RoomSystemConfig: {roomSystemConfig != null}");
        }
        
        /// <summary>
        /// 生成地图
        /// </summary>
        public void GenerateMap()
        {
            Debug.Log($"[MapGenerator] Generating map level {currentMapLevel}...");
            
            // 清理旧地图
            ClearMap();
            
            // 初始化房间网格
            roomGrid = new SimplifiedRoom[mapSize, mapSize];
            
            // 生成房间布局
            var roomLayout = GenerateRoomLayout();
            
            // 创建房间
            CreateRooms(roomLayout);
            
            // 放置玩家到出生房间
            PlacePlayersInSpawnRoom();
            
            OnMapGenerated?.Invoke(currentMapLevel);
            
            Debug.Log($"[MapGenerator] Map generation complete!");
        }
        
        /// <summary>
        /// 生成房间布局
        /// </summary>
        private RoomType[,] GenerateRoomLayout()
        {
            var layout = new RoomType[mapSize, mapSize];
            
            // 初始化为空房间
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    layout[x, y] = RoomType.Empty;
                }
            }
            
            // 1. 放置出生房间
            layout[spawnRoomPosition.x, spawnRoomPosition.y] = RoomType.Spawn;
            
            // 2. 找到传送房间的位置并生成主路径
            var teleportPos = FindEdgeFarthestFromSpawn(layout, spawnRoomPosition);
            layout[teleportPos.x, teleportPos.y] = RoomType.Teleport;
            GenerateMainPath(layout, spawnRoomPosition, teleportPos);
            
            // 3. 生成分支路径
            GenerateBranches(layout);
            
            // 4. 放置必需的房间类型
            PlaceRequiredRooms(layout);
            
            // 5. 填充所有剩余的空间
            FillRemainingSpace(layout);
            
            return layout;
        }
        
        /// <summary>
        /// 放置必需的房间
        /// </summary>
        private void PlaceRequiredRooms(RoomType[,] layout)
        {
            // 必需的房间类型（使用新的NPC房间类型）
            var requiredTypes = new List<RoomType>
            {
                RoomType.NPC,  // NPC房间（会随机生成各种NPC）
                RoomType.NPC   // 多放置几个NPC房间确保有足够的服务
            };
            
            foreach (var type in requiredTypes)
            {
                PlaceRoomType(layout, type, 1);
            }
        }
        
        /// <summary>
        /// 生成从起点到终点的主路径
        /// </summary>
        private void GenerateMainPath(RoomType[,] layout, Vector2Int start, Vector2Int end)
        {
            var current = start;
            var visited = new HashSet<Vector2Int> { start };
            
            // 使用A*或简化的路径寻找算法
            while (current != end)
            {
                // 计算下一步应该走的方向
                Vector2Int nextStep = current;
                int minDistance = int.MaxValue;
                
                // 检查四个方向
                var directions = new Vector2Int[] 
                { 
                    Vector2Int.up, Vector2Int.down, 
                    Vector2Int.left, Vector2Int.right 
                };
                
                foreach (var dir in directions)
                {
                    var neighbor = current + dir;
                    
                    // 检查边界
                    if (neighbor.x < 0 || neighbor.x >= mapSize || 
                        neighbor.y < 0 || neighbor.y >= mapSize)
                        continue;
                    
                    // 跳过已访问的
                    if (visited.Contains(neighbor))
                        continue;
                    
                    // 计算到终点的曼哈顿距离
                    int distance = Mathf.Abs(neighbor.x - end.x) + Mathf.Abs(neighbor.y - end.y);
                    
                    // 添加一些随机性，避免路径太直
                    distance += Random.Range(0, 3);
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nextStep = neighbor;
                    }
                }
                
                // 如果找到了下一步
                if (nextStep != current)
                {
                    current = nextStep;
                    visited.Add(current);
                    
                    // 放置房间（除了终点）
                    if (current != end && layout[current.x, current.y] == RoomType.Empty)
                    {
                        layout[current.x, current.y] = GetRandomRoomType();
                    }
                }
                else
                {
                    // 如果被困住了，随机选择一个方向
                    var validDirs = new List<Vector2Int>();
                    foreach (var dir in directions)
                    {
                        var neighbor = current + dir;
                        if (neighbor.x >= 0 && neighbor.x < mapSize && 
                            neighbor.y >= 0 && neighbor.y < mapSize && 
                            !visited.Contains(neighbor))
                        {
                            validDirs.Add(neighbor);
                        }
                    }
                    
                    if (validDirs.Count > 0)
                    {
                        current = validDirs[Random.Range(0, validDirs.Count)];
                        visited.Add(current);
                        if (layout[current.x, current.y] == RoomType.Empty)
                        {
                            layout[current.x, current.y] = GetRandomRoomType();
                        }
                    }
                    else
                    {
                        break; // 无法继续
                    }
                }
            }
            
            Debug.Log($"[MapGenerator] Main path created with {visited.Count} rooms");
        }
        
        /// <summary>
        /// 生成分支路径
        /// </summary>
        private void GenerateBranches(RoomType[,] layout)
        {
            // 找出所有已经放置的房间
            var existingRooms = new List<Vector2Int>();
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (layout[x, y] != RoomType.Empty)
                    {
                        existingRooms.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            // 从现有房间生成分支
            int branchCount = Random.Range(5, 10);
            for (int i = 0; i < branchCount; i++)
            {
                if (existingRooms.Count == 0) break;
                
                // 随机选择一个起点
                var branchStart = existingRooms[Random.Range(0, existingRooms.Count)];
                
                // 生成一个分支
                int branchLength = Random.Range(3, 8);
                var current = branchStart;
                
                for (int j = 0; j < branchLength; j++)
                {
                    // 随机选择一个方向
                    var directions = new Vector2Int[] 
                    { 
                        Vector2Int.up, Vector2Int.down, 
                        Vector2Int.left, Vector2Int.right 
                    };
                    
                    var validDirs = new List<Vector2Int>();
                    foreach (var dir in directions)
                    {
                        var next = current + dir;
                        if (next.x >= 0 && next.x < mapSize && 
                            next.y >= 0 && next.y < mapSize && 
                            layout[next.x, next.y] == RoomType.Empty)
                        {
                            validDirs.Add(next);
                        }
                    }
                    
                    if (validDirs.Count == 0) break;
                    
                    var chosen = validDirs[Random.Range(0, validDirs.Count)];
                    layout[chosen.x, chosen.y] = GetRandomRoomType();
                    existingRooms.Add(chosen);
                    current = chosen;
                }
            }
        }
        
        /// <summary>
        /// 填充剩余空间
        /// </summary>
        private void FillRemainingSpace(RoomType[,] layout)
        {
            int filledCount = 0;
            
            // 填充所有剩余的空格子
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (layout[x, y] == RoomType.Empty)
                    {
                        layout[x, y] = GetRandomRoomType();
                        filledCount++;
                    }
                }
            }
            
            // 统计总数
            int totalRooms = 0;
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (layout[x, y] != RoomType.Empty)
                    {
                        totalRooms++;
                    }
                }
            }
            
            Debug.Log($"[MapGenerator] Filled {filledCount} remaining spaces. Total rooms: {totalRooms}/{mapSize * mapSize}");
        }
        
        
        /// <summary>
        /// 创建房间实例
        /// </summary>
        private void CreateRooms(RoomType[,] layout)
        {
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (layout[x, y] != RoomType.Empty)
                    {
                        var gridPos = new Vector2Int(x, y);
                        var availableDoors = GetAvailableDoors(gridPos, layout);
                        
                        CreateRoom(layout[x, y], gridPos, availableDoors);
                    }
                }
            }
        }
        
        /// <summary>
        /// 创建单个房间
        /// </summary>
        private void CreateRoom(RoomType roomType, Vector2Int gridPos, bool[] availableDoors)
        {
            GameObject prefab = GetRoomPrefab(roomType);
            if (prefab == null)
            {
                Debug.LogError($"[MapGenerator] No prefab found for room type: {roomType}");
                return;
            }
            
            var roomObj = Instantiate(prefab, transform);
            var room = roomObj.GetComponent<SimplifiedRoom>();
            
            if (room == null)
            {
                room = roomObj.AddComponent<SimplifiedRoom>();
            }
            
            room.Initialize(roomType, gridPos, availableDoors, roomSystemConfig);
            
            roomGrid[gridPos.x, gridPos.y] = room;
            
            // 订阅房间事件
            room.OnRoomCleared += OnRoomCleared;
        }
        
        /// <summary>
        /// 从房间系统配置获取房间预制体
        /// </summary>
        private GameObject GetRoomPrefab(RoomType roomType)
        {
            if (roomSystemConfig == null)
            {
                Debug.LogError("[MapGenerator] RoomSystemConfig is null! Cannot get room prefab.");
                return null;
            }
            
            // 从房间系统配置获取预制体
            var prefab = roomSystemConfig.GetRoomPrefab(roomType);
            
            if (prefab == null)
            {
                Debug.LogWarning($"[MapGenerator] No room prefab found for type {roomType}!");
            }
            
            return prefab;
        }
        
        /// <summary>
        /// 获取可用的门
        /// </summary>
        private bool[] GetAvailableDoors(Vector2Int position, RoomType[,] layout)
        {
            bool[] doors = new bool[4]; // North, East, South, West
            
            // 检查每个方向
            // North
            if (position.y < mapSize - 1 && layout[position.x, position.y + 1] != RoomType.Empty)
                doors[0] = true;
            
            // East
            if (position.x < mapSize - 1 && layout[position.x + 1, position.y] != RoomType.Empty)
                doors[1] = true;
            
            // South
            if (position.y > 0 && layout[position.x, position.y - 1] != RoomType.Empty)
                doors[2] = true;
            
            // West
            if (position.x > 0 && layout[position.x - 1, position.y] != RoomType.Empty)
                doors[3] = true;
            
            return doors;
        }
        
        /// <summary>
        /// 获取未访问的邻居
        /// </summary>
        private List<Vector2Int> GetUnvisitedNeighbors(Vector2Int position, HashSet<Vector2Int> visited)
        {
            var neighbors = new List<Vector2Int>();
            var directions = new Vector2Int[]
            {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
            };
            
            foreach (var dir in directions)
            {
                var neighbor = position + dir;
                if (neighbor.x >= 0 && neighbor.x < mapSize &&
                    neighbor.y >= 0 && neighbor.y < mapSize &&
                    !visited.Contains(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }
            
            return neighbors;
        }
        
        /// <summary>
        /// 从房间系统配置获取随机房间类型
        /// </summary>
        private RoomType GetRandomRoomType()
        {
            if (roomSystemConfig == null)
            {
                Debug.LogWarning("[MapGenerator] RoomSystemConfig is null, using fallback random type");
                // 降级方案：返回常见类型
                var fallbackTypes = new RoomType[] { RoomType.Monster, RoomType.Treasure, RoomType.Fountain };
                return fallbackTypes[Random.Range(0, fallbackTypes.Length)];
            }
            
            // 从房间系统配置获取随机类型
            return roomSystemConfig.GetRandomRoomType(currentMapLevel);
        }
        
        /// <summary>
        /// 放置特定类型的房间
        /// </summary>
        private void PlaceRoomType(RoomType[,] layout, RoomType type, int count)
        {
            int placed = 0;
            int attempts = 0;
            int maxAttempts = 100;
            
            while (placed < count && attempts < maxAttempts)
            {
                int x = Random.Range(0, mapSize);
                int y = Random.Range(0, mapSize);
                
                if (layout[x, y] == RoomType.Empty)
                {
                    layout[x, y] = type;
                    placed++;
                }
                
                attempts++;
            }
        }
        
        /// <summary>
        /// 获取出生点的世界坐标
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            // 出生房间在(8,8)，每个房间16单位
            return new Vector3(spawnRoomPosition.x * roomSize, spawnRoomPosition.y * roomSize, 0);
        }
        
        /// <summary>
        /// 找到距离最远的空位置
        /// </summary>
        private Vector2Int FindFarthestEmptyPosition(RoomType[,] layout, Vector2Int from)
        {
            Vector2Int farthest = new Vector2Int(-1, -1);
            int maxDistance = 0;
            
            for (int x = 0; x < mapSize; x++)
            {
                for (int y = 0; y < mapSize; y++)
                {
                    if (layout[x, y] != RoomType.Empty) continue;
                    
                    int distance = Mathf.Abs(x - from.x) + Mathf.Abs(y - from.y);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthest = new Vector2Int(x, y);
                    }
                }
            }
            
            return farthest;
        }
        
        /// <summary>
        /// 找到边缘上距离出生点最远的位置
        /// </summary>
        private Vector2Int FindEdgeFarthestFromSpawn(RoomType[,] layout, Vector2Int from)
        {
            Vector2Int farthest = new Vector2Int(-1, -1);
            int maxDistance = 0;
            
            // 检查所有边缘位置
            for (int i = 0; i < mapSize; i++)
            {
                // 上边缘
                if (layout[i, mapSize - 1] == RoomType.Empty)
                {
                    int distance = Mathf.Abs(i - from.x) + Mathf.Abs(mapSize - 1 - from.y);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthest = new Vector2Int(i, mapSize - 1);
                    }
                }
                
                // 下边缘
                if (layout[i, 0] == RoomType.Empty)
                {
                    int distance = Mathf.Abs(i - from.x) + Mathf.Abs(0 - from.y);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthest = new Vector2Int(i, 0);
                    }
                }
                
                // 左边缘
                if (layout[0, i] == RoomType.Empty)
                {
                    int distance = Mathf.Abs(0 - from.x) + Mathf.Abs(i - from.y);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthest = new Vector2Int(0, i);
                    }
                }
                
                // 右边缘
                if (layout[mapSize - 1, i] == RoomType.Empty)
                {
                    int distance = Mathf.Abs(mapSize - 1 - from.x) + Mathf.Abs(i - from.y);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthest = new Vector2Int(mapSize - 1, i);
                    }
                }
            }
            
            // 如果边缘没有空位，返回任意最远的空位
            if (farthest.x < 0)
            {
                return FindFarthestEmptyPosition(layout, from);
            }
            
            return farthest;
        }
        
        /// <summary>
        /// 放置玩家到出生房间
        /// </summary>
        private void PlacePlayersInSpawnRoom()
        {
            var spawnRoom = roomGrid[spawnRoomPosition.x, spawnRoomPosition.y];
            if (spawnRoom == null) return;
            
            currentRoom = spawnRoom;
            
            // 查找所有玩家
            var players = GameObject.FindGameObjectsWithTag("Player");
            allPlayers.Clear();
            allPlayers.AddRange(players);
            
            // 放置玩家
            for (int i = 0; i < players.Length; i++)
            {
                var angle = (360f / players.Length) * i;
                var offset = Quaternion.Euler(0, 0, angle) * Vector3.up * 2f; // 默认生成半径2
                
                players[i].transform.position = spawnRoom.WorldPosition + offset;
            }
            
            // 通知房间（出生房间不需要锁门，使用默认方向）
            foreach (var player in players)
            {
                spawnRoom.OnPlayerEnter(player);
            }
        }
        
        /// <summary>
        /// 玩家使用门
        /// </summary>
        public void OnPlayerUseDoor(SimplifiedRoom fromRoom, DoorDirection direction, GameObject player)
        {
            var targetGridPos = fromRoom.GetAdjacentGridPosition(direction);
            
            // 检查目标位置是否有效
            if (targetGridPos.x < 0 || targetGridPos.x >= mapSize ||
                targetGridPos.y < 0 || targetGridPos.y >= mapSize)
            {
                return;
            }
            
            var targetRoom = roomGrid[targetGridPos.x, targetGridPos.y];
            if (targetRoom == null) return;
            
            // 切换房间
            SwitchRoom(fromRoom, targetRoom, player);
        }
        
        /// <summary>
        /// 切换房间
        /// </summary>
        private void SwitchRoom(SimplifiedRoom fromRoom, SimplifiedRoom toRoom, GameObject player)
        {
            // 计算进入位置和方向
            var directionFromPrevRoom = GetDirectionBetweenRooms(fromRoom, toRoom);
            var enterDirection = GetOppositeDirection(directionFromPrevRoom);
            
            Debug.Log($"[MapGenerator] Room switch: {fromRoom.GridPosition} -> {toRoom.GridPosition}");
            Debug.Log($"[MapGenerator] Direction from prev room: {directionFromPrevRoom}, Enter direction: {enterDirection}");
            
            // var offset = GetDirectionOffset(enterDirection) * 2f;
            
            // 不再传送玩家，让玩家自然走过去
            // player.transform.position = toRoom.WorldPosition + new Vector3(offset.x, offset.y, 0);
            
            // 通知房间，传递进入方向
            toRoom.OnPlayerEnter(player, enterDirection);
            
            // 更新当前房间
            currentRoom = toRoom;
            
            // 触发事件
            OnRoomChanged?.Invoke(fromRoom, toRoom);
        }
        
        /// <summary>
        /// 获取两个房间之间的方向
        /// </summary>
        private DoorDirection GetDirectionBetweenRooms(SimplifiedRoom from, SimplifiedRoom to)
        {
            var diff = to.GridPosition - from.GridPosition;
            
            if (diff.y > 0) return DoorDirection.North;
            if (diff.x > 0) return DoorDirection.East;
            if (diff.y < 0) return DoorDirection.South;
            if (diff.x < 0) return DoorDirection.West;
            
            return DoorDirection.North;
        }
        
        /// <summary>
        /// 获取相反方向
        /// </summary>
        private DoorDirection GetOppositeDirection(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.North: return DoorDirection.South;
                case DoorDirection.East: return DoorDirection.West;
                case DoorDirection.South: return DoorDirection.North;
                case DoorDirection.West: return DoorDirection.East;
                default: return DoorDirection.North;
            }
        }
        
        /// <summary>
        /// 获取方向偏移
        /// </summary>
        private Vector2 GetDirectionOffset(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.North: return Vector2.up;
                case DoorDirection.East: return Vector2.right;
                case DoorDirection.South: return Vector2.down;
                case DoorDirection.West: return Vector2.left;
                default: return Vector2.zero;
            }
        }
        
        /// <summary>
        /// 房间清理完成回调
        /// </summary>
        private void OnRoomCleared(SimplifiedRoom room)
        {
            Debug.Log($"[MapGenerator] Room at {room.GridPosition} cleared!");
            
            // 检查是否所有必要的房间都已清理
            CheckMapCompletion();
        }
        
        /// <summary>
        /// 检查地图完成度
        /// </summary>
        private void CheckMapCompletion()
        {
            // 这里可以添加地图完成的逻辑
        }
        
        /// <summary>
        /// 清理地图
        /// </summary>
        private void ClearMap()
        {
            if (roomGrid != null)
            {
                foreach (var room in roomGrid)
                {
                    if (room != null)
                    {
                        Destroy(room.gameObject);
                    }
                }
            }
            
            // 清理所有子对象
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            
            // 清理场景中所有的掉落物
            ClearAllPickups();
            
            // 清理NPC运行时数据，让新地图的NPC重新随机化
            var npcDataManager = NPC.Runtime.NPCRuntimeDataManager.Instance;
            if (npcDataManager != null)
            {
                npcDataManager.ClearCurrentMapData();
            }
            
            // 清理商人存档数据
            var merchantSaveManager = NPC.Managers.MerchantSaveManager.Instance;
            if (merchantSaveManager != null)
            {
                merchantSaveManager.ClearAllData();
            }
        }
        
        /// <summary>
        /// 清理场景中所有的掉落物
        /// </summary>
        private void ClearAllPickups()
        {
            // 查找所有UnifiedPickup组件
            var pickups = FindObjectsOfType<Loot.UnifiedPickup>();
            foreach (var pickup in pickups)
            {
                if (pickup != null)
                {
                    Destroy(pickup.gameObject);
                }
            }
            
            // 清理可能遗留的其他拾取物
            var legacyPickups = FindObjectsOfType<Loot.Pickup>();
            foreach (var pickup in legacyPickups)
            {
                if (pickup != null)
                {
                    Destroy(pickup.gameObject);
                }
            }
            
            // 清理名称包含"Pickup"的GameObject
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.Contains("Pickup") || obj.name.Contains("pickup"))
                {
                    // 确保不是预制体引用，只清理场景中的实例
                    if (obj.scene.IsValid())
                    {
                        Destroy(obj);
                    }
                }
            }
            
            Debug.Log("[MapGenerator] Cleared all pickups from scene");
        }
        
        /// <summary>
        /// 传送到下一层
        /// </summary>
        public void TeleportToNextLevel()
        {
            currentMapLevel++;
            GenerateMap();
        }
        
        /// <summary>
        /// 根据世界坐标获取房间网格坐标
        /// </summary>
        public Vector2Int GetRoomCoordinate(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x + roomSize / 2) / roomSize);
            int y = Mathf.FloorToInt((worldPosition.y + roomSize / 2) / roomSize);
            return new Vector2Int(x, y);
        }
        
        /// <summary>
        /// 获取指定网格坐标的房间
        /// </summary>
        public SimplifiedRoom GetRoomAt(int x, int y)
        {
            if (x < 0 || x >= mapSize || y < 0 || y >= mapSize)
                return null;
            
            return roomGrid[x, y];
        }
        
        private void OnDrawGizmos()
        {
            if (roomGrid == null) return;
            
            // 绘制地图网格
            Gizmos.color = Color.gray;
            for (int x = 0; x <= mapSize; x++)
            {
                Gizmos.DrawLine(
                    new Vector3(x * roomSize - roomSize/2, -roomSize/2, 0),
                    new Vector3(x * roomSize - roomSize/2, mapSize * roomSize - roomSize/2, 0)
                );
            }
            
            for (int y = 0; y <= mapSize; y++)
            {
                Gizmos.DrawLine(
                    new Vector3(-roomSize/2, y * roomSize - roomSize/2, 0),
                    new Vector3(mapSize * roomSize - roomSize/2, y * roomSize - roomSize/2, 0)
                );
            }
        }
    }
}