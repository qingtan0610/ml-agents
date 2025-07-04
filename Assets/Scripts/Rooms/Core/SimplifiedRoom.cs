using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Rooms.Core
{
    /// <summary>
    /// 极简化的房间类，预制体包含所有内容
    /// </summary>
    public class SimplifiedRoom : MonoBehaviour
    {
        [Header("Room Info")]
        [SerializeField] private RoomType roomType = RoomType.Empty;
        [SerializeField] private Vector2Int gridPosition;
        
        [Header("Runtime State")]
        [SerializeField] private bool isExplored = false;
        [SerializeField] private bool isCleared = false;
        [SerializeField] private List<GameObject> activeEnemies = new List<GameObject>();
        private Rooms.Data.RoomSystemConfig roomSystemConfig;
        
        [Header("Door References")]
        [SerializeField] private Animator northDoor;
        [SerializeField] private Animator southDoor;
        [SerializeField] private Animator eastDoor;
        [SerializeField] private Animator westDoor;
        
        [Header("Wall Fill References")]
        [SerializeField] private GameObject northWallFill; // 北边填充墙
        [SerializeField] private GameObject southWallFill; // 南边填充墙
        [SerializeField] private GameObject eastWallFill;  // 东边填充墙
        [SerializeField] private GameObject westWallFill;  // 西边填充墙
        
        [Header("Spawn Points")]
        [SerializeField] private Transform npcSpawnPoint;
        [SerializeField] private Transform[] monsterSpawnPoints;
        [SerializeField] private Transform centerPoint;
        
        [Header("Prefab References")]
        [SerializeField] private GameObject treasureChestPrefab;
        [SerializeField] private GameObject fountainPrefab;
        [SerializeField] private GameObject enemyPrefab;
        
        // Properties
        public RoomType Type => roomType;
        public Vector2Int GridPosition => gridPosition;
        public bool IsExplored => isExplored;
        public bool IsCleared => isCleared;
        public Vector3 WorldPosition => transform.position;
        
        // Events
        public System.Action<SimplifiedRoom> OnRoomCleared;
        
        /// <summary>
        /// 初始化房间
        /// </summary>
        public void Initialize(RoomType type, Vector2Int gridPos, bool[] availableDoors, Rooms.Data.RoomSystemConfig systemConfig = null)
        {
            roomType = type;
            gridPosition = gridPos;
            gameObject.name = $"Room_{gridPos.x}_{gridPos.y}_{type}";
            
            // 设置世界位置（16x16单位）
            transform.position = new Vector3(gridPos.x * 16, gridPos.y * 16, 0);
            
            // 设置门的状态
            SetupDoors(availableDoors);
            
            // 保存系统配置
            roomSystemConfig = systemConfig;
            
            // 生成房间内容
            SpawnRoomContents();
            
            // 监听全局敌人死亡事件
            Enemy.Enemy2D.OnEnemyDied += HandleGlobalEnemyDeath;
            
            // 怪物房间不再在初始化时锁门，而是等玩家进入后才锁门
            // if (roomType == RoomType.Monster && !isCleared)
            // {
            //     LockAllDoors();
            // }
        }
        
        private void OnDestroy()
        {
            // 取消监听全局敌人死亡事件
            Enemy.Enemy2D.OnEnemyDied -= HandleGlobalEnemyDeath;
        }
        
        /// <summary>
        /// 处理全局敌人死亡事件
        /// </summary>
        private void HandleGlobalEnemyDeath(GameObject deadEnemy, Vector3 deathPosition)
        {
            // 检查这个敌人是否属于当前房间
            if (activeEnemies.Contains(deadEnemy))
            {
                Debug.Log($"[Room {gridPosition}] Enemy {deadEnemy.name} died at {deathPosition}, removing from active list");
                activeEnemies.Remove(deadEnemy);
                CheckRoomCleared();
            }
        }
        
        /// <summary>
        /// 设置门和墙体
        /// </summary>
        private void SetupDoors(bool[] availableDoors)
        {
            // North = 0, East = 1, South = 2, West = 3
            // 北门
            if (northDoor != null)
                northDoor.gameObject.SetActive(availableDoors[0]);
            if (northWallFill != null)
                northWallFill.SetActive(!availableDoors[0]); // 没有门时显示墙
                
            // 东门
            if (eastDoor != null)
                eastDoor.gameObject.SetActive(availableDoors[1]);
            if (eastWallFill != null)
                eastWallFill.SetActive(!availableDoors[1]);
                
            // 南门
            if (southDoor != null)
                southDoor.gameObject.SetActive(availableDoors[2]);
            if (southWallFill != null)
                southWallFill.SetActive(!availableDoors[2]);
                
            // 西门
            if (westDoor != null)
                westDoor.gameObject.SetActive(availableDoors[3]);
            if (westWallFill != null)
                westWallFill.SetActive(!availableDoors[3]);
        }
        
        /// <summary>
        /// 生成房间内容
        /// </summary>
        private void SpawnRoomContents()
        {
            switch (roomType)
            {
                case RoomType.Monster:
                    SpawnMonsters();
                    break;
                    
                case RoomType.Treasure:
                    SpawnTreasure();
                    break;
                    
                case RoomType.Fountain:
                    SpawnFountain();
                    break;
                    
                case RoomType.NPC:
                    SpawnNPC();
                    break;
                    
                // 兼容旧类型
                case RoomType.Restaurant:
                case RoomType.Merchant:
                case RoomType.Blacksmith:
                case RoomType.Doctor:
                case RoomType.Tailor:
                    Debug.LogWarning($"[SimplifiedRoom] Obsolete room type {roomType}, spawning NPC anyway");
                    SpawnNPC();
                    break;
                    
                case RoomType.Teleport:
                    SpawnTeleporter();
                    break;
            }
        }
        
        private void SpawnMonsters()
        {
            Debug.Log($"[SimplifiedRoom] Attempting to spawn monsters in {roomType} room at {gridPosition}");
            
            // 使用系统配置获取怪物池
            Pools.MonsterPool monsterPool = null;
            if (roomSystemConfig != null)
            {
                monsterPool = roomSystemConfig.GetMonsterPool(roomType);
            }
            
            if (monsterPool == null)
            {
                // 如果没有配置，尝试从Resources加载
                monsterPool = Resources.Load<Pools.MonsterPool>("MainMonsterPool");
            }
            
            if (monsterPool != null)
            {
                // 从系统配置获取怪物数量和难度
                int minCount = 3;
                int maxCount = 6;
                int maxDifficulty = 5;
                
                if (roomSystemConfig != null)
                {
                    var binding = roomSystemConfig.GetBinding(roomType);
                    if (binding is Rooms.Data.RoomSystemConfig.MonsterRoomBinding monsterBinding)
                    {
                        minCount = monsterBinding.minMonsterCount;
                        maxCount = monsterBinding.maxMonsterCount;
                        maxDifficulty = monsterBinding.maxMonsterDifficulty;
                    }
                }
                
                // 生成随机位置
                int spawnCount = Random.Range(minCount, maxCount + 1);
                Transform[] randomSpawnPoints = GenerateRandomSpawnPoints(spawnCount);
                
                // 为每个生成点随机选择不同的怪物
                Debug.Log($"[SimplifiedRoom] Spawning {spawnCount} monsters with mixed types");
                
                foreach (var spawnPoint in randomSpawnPoints)
                {
                    // 每个位置都随机选择一种怪物
                    var monsterEntry = monsterPool.GetRandomMonster(1, maxDifficulty);
                    if (monsterEntry != null)
                    {
                        // 生成单个怪物（使用只有一个位置的数组）
                        var singleSpawnPoint = new Transform[] { spawnPoint };
                        var monsters = monsterPool.SpawnMonsterGroup(monsterEntry, singleSpawnPoint, transform);
                        
                        if (monsters != null && monsters.Count > 0)
                        {
                            foreach (var monster in monsters)
                            {
                                var enemy = monster.GetComponent<Enemy.Enemy2D>();
                                if (enemy != null)
                                {
                                    // 在怪物死亡时通知房间
                                    StartCoroutine(MonitorEnemyDeath(monster));
                                }
                                activeEnemies.Add(monster);
                            }
                        }
                    }
                }
                
                Debug.Log($"[SimplifiedRoom] Spawned {activeEnemies.Count} monsters total");
            }
            else if (enemyPrefab != null)
            {
                // 降级方案：使用默认敌人预制体
                int count = Random.Range(3, 6);
                Transform[] randomSpawnPoints = GenerateRandomSpawnPoints(count);
                foreach (var point in randomSpawnPoints)
                {
                    var enemy = Instantiate(enemyPrefab, point.position, Quaternion.identity, transform);
                    
                    // 为怪物设置死亡回调
                    var enemyComponent = enemy.GetComponent<Enemy.Enemy2D>();
                    if (enemyComponent != null)
                    {
                        StartCoroutine(MonitorEnemyDeath(enemy));
                    }
                    
                    activeEnemies.Add(enemy);
                }
            }
        }
        
        /// <summary>
        /// 生成随机生成点
        /// </summary>
        private Transform[] GenerateRandomSpawnPoints(int count)
        {
            var spawnPoints = new Transform[count];
            float roomRadius = 6f; // 房间半径，避免生成在墙边
            float minDistance = 1.5f; // 怪物之间的最小距离
            
            List<Vector3> usedPositions = new List<Vector3>();
            
            for (int i = 0; i < count; i++)
            {
                var tempGO = new GameObject($"TempSpawnPoint_{i}");
                tempGO.transform.parent = transform;
                
                // 尝试找到合适的位置
                int attempts = 0;
                bool validPosition = false;
                Vector3 localPos = Vector3.zero;
                
                while (!validPosition && attempts < 20)
                {
                    // 在房间内随机一个位置
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float radius = Random.Range(1f, roomRadius);
                    localPos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                    
                    // 检查是否远离其他怪物
                    validPosition = true;
                    foreach (var usedPos in usedPositions)
                    {
                        if (Vector3.Distance(localPos, usedPos) < minDistance)
                        {
                            validPosition = false;
                            break;
                        }
                    }
                    
                    attempts++;
                }
                
                tempGO.transform.localPosition = localPos;
                spawnPoints[i] = tempGO.transform;
                usedPositions.Add(localPos);
            }
            
            // 在生成完成后清理临时对象
            StartCoroutine(CleanupTempSpawnPoints(spawnPoints));
            
            return spawnPoints;
        }
        
        private System.Collections.IEnumerator CleanupTempSpawnPoints(Transform[] points)
        {
            yield return new WaitForSeconds(1f);
            foreach (var point in points)
            {
                if (point != null && point.gameObject.name.StartsWith("TempSpawnPoint_"))
                {
                    Destroy(point.gameObject);
                }
            }
        }
        
        private void SpawnTreasure()
        {
            // 使用系统配置获取宝箱预制体
            GameObject prefabToUse = null;
            if (roomSystemConfig != null)
            {
                prefabToUse = roomSystemConfig.GetSpecialPrefab(roomType);
            }
            
            // 如果没有配置，使用默认预制体
            if (prefabToUse == null)
            {
                prefabToUse = treasureChestPrefab;
            }
            if (prefabToUse == null)
            {
                prefabToUse = Resources.Load<GameObject>("Prefabs/Interactables/TreasureChest");
            }
            
            if (prefabToUse != null && centerPoint != null)
            {
                Instantiate(prefabToUse, centerPoint.position, Quaternion.identity, transform);
                Debug.Log($"[SimplifiedRoom] Spawned treasure chest at {centerPoint.position}");
            }
            else
            {
                Debug.LogWarning($"[SimplifiedRoom] Failed to spawn treasure chest. Prefab: {prefabToUse != null}, Center: {centerPoint != null}");
            }
        }
        
        private void SpawnFountain()
        {
            // 使用系统配置获取泉水预制体
            GameObject prefabToUse = null;
            if (roomSystemConfig != null)
            {
                prefabToUse = roomSystemConfig.GetSpecialPrefab(roomType);
            }
            
            // 如果没有配置，使用默认预制体
            if (prefabToUse == null)
            {
                prefabToUse = fountainPrefab;
            }
            if (prefabToUse == null)
            {
                prefabToUse = Resources.Load<GameObject>("Prefabs/Interactables/Fountain");
            }
            
            if (prefabToUse != null && centerPoint != null)
            {
                Instantiate(prefabToUse, centerPoint.position, Quaternion.identity, transform);
                Debug.Log($"[SimplifiedRoom] Spawned fountain at {centerPoint.position}");
            }
            else
            {
                Debug.LogWarning($"[SimplifiedRoom] Failed to spawn fountain. Prefab: {prefabToUse != null}, Center: {centerPoint != null}");
            }
        }
        
        private void SpawnNPC()
        {
            if (npcSpawnPoint == null) return;
            
            // 使用系统配置获取NPC池
            NPC.Core.NPCPool npcPool = null;
            
            if (roomSystemConfig != null)
            {
                npcPool = roomSystemConfig.GetNPCPool(roomType);
            }
            
            if (npcPool == null)
            {
                // 如果没有配置，尝试从Resources加载
                npcPool = Resources.Load<NPC.Core.NPCPool>("MainNPCPool");
            }
            
            if (npcPool == null)
            {
                Debug.LogWarning("[SimplifiedRoom] No NPC pool found!");
                return;
            }
            
            // 从NPC池中随机选择任意NPC
            var allNPCTypes = System.Enum.GetValues(typeof(NPC.NPCType)).Cast<NPC.NPCType>().ToList();
            
            // 随机打乱并尝试找到可用的NPC
            for (int i = allNPCTypes.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = allNPCTypes[i];
                allNPCTypes[i] = allNPCTypes[j];
                allNPCTypes[j] = temp;
            }
            
            // 尝试生成任意可用的NPC
            foreach (var npcType in allNPCTypes)
            {
                var entry = npcPool.GetNPCForRoom(npcType, 1);
                if (entry != null)
                {
                    var npc = npcPool.SpawnNPC(entry, npcSpawnPoint.position);
                    if (npc != null)
                    {
                        Debug.Log($"[SimplifiedRoom] Spawned {npcType} NPC in room at {gridPosition}");
                        return;
                    }
                }
            }
            
            Debug.LogWarning($"[SimplifiedRoom] Failed to spawn any NPC in room at {gridPosition}");
        }
        
        private void SpawnTeleporter()
        {
            // 使用系统配置获取传送器预制体
            GameObject prefabToUse = null;
            if (roomSystemConfig != null)
            {
                prefabToUse = roomSystemConfig.GetSpecialPrefab(roomType);
            }
            
            // 如果没有配置，从Resources加载
            if (prefabToUse == null)
            {
                prefabToUse = Resources.Load<GameObject>("Prefabs/Interactables/Teleporter");
            }
            
            if (prefabToUse != null && centerPoint != null)
            {
                var teleporter = Instantiate(prefabToUse, centerPoint.position, Quaternion.identity, transform);
                Debug.Log($"[SimplifiedRoom] Spawned teleporter at {centerPoint.position}");
                
                // TeleportDevice会自动找到MapGenerator，不需要手动设置
            }
            else
            {
                Debug.LogWarning($"[SimplifiedRoom] Failed to spawn teleporter. Prefab: {prefabToUse != null}, Center: {centerPoint != null}");
            }
        }
        
        /// <summary>
        /// 锁定所有门
        /// </summary>
        private void LockAllDoors()
        {
            SetDoorState("Close");
        }
        
        /// <summary>
        /// 打开所有门
        /// </summary>
        private void OpenAllDoors()
        {
            SetDoorState("Open");
        }
        
        /// <summary>
        /// 设置门动画状态
        /// </summary>
        private void SetDoorState(string triggerName)
        {
            if (northDoor != null && northDoor.gameObject.activeSelf)
            {
                var doorBlocker = northDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    if (triggerName == "Open") doorBlocker.Open();
                    else if (triggerName == "Close") doorBlocker.Close();
                }
                // northDoor.SetTrigger(triggerName); // 暂时没有Animator
            }
            if (eastDoor != null && eastDoor.gameObject.activeSelf)
            {
                var doorBlocker = eastDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    if (triggerName == "Open") doorBlocker.Open();
                    else if (triggerName == "Close") doorBlocker.Close();
                }
                // eastDoor.SetTrigger(triggerName); // 暂时没有Animator
            }
            if (southDoor != null && southDoor.gameObject.activeSelf)
            {
                var doorBlocker = southDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    if (triggerName == "Open") doorBlocker.Open();
                    else if (triggerName == "Close") doorBlocker.Close();
                }
                // southDoor.SetTrigger(triggerName); // 暂时没有Animator
            }
            if (westDoor != null && westDoor.gameObject.activeSelf)
            {
                var doorBlocker = westDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    if (triggerName == "Open") doorBlocker.Open();
                    else if (triggerName == "Close") doorBlocker.Close();
                }
                // westDoor.SetTrigger(triggerName); // 暂时没有Animator
            }
        }
        
        /// <summary>
        /// 锁定除了指定方向外的所有门
        /// </summary>
        private void LockDoorsExcept(DoorDirection exceptDirection)
        {
            Debug.Log($"[Room] Locking all doors except {exceptDirection}");
            
            // 锁定所有门，除了指定的方向
            if (exceptDirection != DoorDirection.North && northDoor != null && northDoor.gameObject.activeSelf)
            {
                var doorBlocker = northDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    doorBlocker.Close();
                }
                else
                {
                    Debug.LogWarning("[Room] North door has no SimpleDoorBlocker! Add SimpleDoorBlocker component to the door prefab.");
                }
                // northDoor.SetTrigger("Close"); // 暂时没有Animator
            }
            if (exceptDirection != DoorDirection.East && eastDoor != null && eastDoor.gameObject.activeSelf)
            {
                var doorBlocker = eastDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    doorBlocker.Close();
                }
                else
                {
                    Debug.LogWarning("[Room] East door has no SimpleDoorBlocker! Add SimpleDoorBlocker component to the door prefab.");
                }
                // eastDoor.SetTrigger("Close"); // 暂时没有Animator
            }
            if (exceptDirection != DoorDirection.South && southDoor != null && southDoor.gameObject.activeSelf)
            {
                var doorBlocker = southDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    doorBlocker.Close();
                }
                else
                {
                    Debug.LogWarning("[Room] South door has no SimpleDoorBlocker! Add SimpleDoorBlocker component to the door prefab.");
                }
                // southDoor.SetTrigger("Close"); // 暂时没有Animator
            }
            if (exceptDirection != DoorDirection.West && westDoor != null && westDoor.gameObject.activeSelf)
            {
                var doorBlocker = westDoor.GetComponent<SimpleDoorBlocker>();
                if (doorBlocker != null)
                {
                    doorBlocker.Close();
                }
                else
                {
                    Debug.LogWarning("[Room] West door has no SimpleDoorBlocker! Add SimpleDoorBlocker component to the door prefab.");
                }
                // westDoor.SetTrigger("Close"); // 暂时没有Animator
            }
        }
        
        /// <summary>
        /// 玩家进入房间
        /// </summary>
        public void OnPlayerEnter(GameObject player)
        {
            OnPlayerEnter(player, DoorDirection.North); // 默认方向，保持向后兼容
        }
        
        /// <summary>
        /// 玩家从特定方向进入房间
        /// </summary>
        public void OnPlayerEnter(GameObject player, DoorDirection entryDirection)
        {
            // 检查玩家是否真的在这个房间内 (房间大小16x16，所以半径约8)
            Vector3 playerPos = player.transform.position;
            Vector3 roomCenter = transform.position;
            float roomHalfSize = 8f; // 房间半边长
            
            // 检查玩家是否在房间边界内
            bool playerInRoom = Mathf.Abs(playerPos.x - roomCenter.x) <= roomHalfSize && 
                               Mathf.Abs(playerPos.y - roomCenter.y) <= roomHalfSize;
            
            if (!playerInRoom)
            {
                Debug.Log($"[Room] Player at {playerPos} is outside room {gridPosition} (center: {roomCenter})");
                return; // 玩家还没真正进入房间，不触发房间逻辑
            }
            
            if (!isExplored)
            {
                isExplored = true;
            }
            
            Debug.Log($"[Room] Player entered {roomType} room at {gridPosition} from {entryDirection}");
            
            // 如果是怪物房间且还没清理，锁定除了进入方向外的所有门
            if (roomType == RoomType.Monster && !isCleared)
            {
                Debug.Log($"[Room] Monster room check - Cleared: {isCleared}, Enemy count: {activeEnemies.Count}");
                
                // 如果还没有生成怪物，先生成
                if (activeEnemies.Count == 0 && !isCleared)
                {
                    Debug.Log($"[Room] No enemies yet, spawning monsters first");
                    SpawnMonsters();
                }
                
                // 如果有怪物，锁定门
                if (activeEnemies.Count > 0)
                {
                    Debug.Log($"[Room] Locking doors, enemy count: {activeEnemies.Count}");
                    LockDoorsExcept(entryDirection);
                }
            }
        }
        
        /// <summary>
        /// 检查房间是否清理完毕
        /// </summary>
        public void CheckRoomCleared()
        {
            activeEnemies.RemoveAll(e => e == null);
            
            if (roomType == RoomType.Monster && activeEnemies.Count == 0 && !isCleared)
            {
                isCleared = true;
                OpenAllDoors();
                OnRoomCleared?.Invoke(this);
                Debug.Log($"[Room] Monster room at {gridPosition} cleared!");
            }
        }
        
        /// <summary>
        /// 敌人死亡回调
        /// </summary>
        public void OnEnemyDeath(GameObject enemy)
        {
            activeEnemies.Remove(enemy);
            CheckRoomCleared();
        }
        
        /// <summary>
        /// 监控敌人死亡
        /// </summary>
        private IEnumerator MonitorEnemyDeath(GameObject enemy)
        {
            while (enemy != null)
            {
                yield return null;
            }
            
            // 敌人被销毁了，说明死亡了
            OnEnemyDeath(enemy);
        }
        
        /// <summary>
        /// 获取相邻房间的网格位置
        /// </summary>
        public Vector2Int GetAdjacentGridPosition(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.North: return gridPosition + Vector2Int.up;
                case DoorDirection.East: return gridPosition + Vector2Int.right;
                case DoorDirection.South: return gridPosition + Vector2Int.down;
                case DoorDirection.West: return gridPosition + Vector2Int.left;
                default: return gridPosition;
            }
        }
        
        /// <summary>
        /// 获取门引用（用于房间池自动检测门配置）
        /// </summary>
        public Animator GetNorthDoor() => northDoor;
        public Animator GetEastDoor() => eastDoor;
        public Animator GetSouthDoor() => southDoor;
        public Animator GetWestDoor() => westDoor;
    }
}