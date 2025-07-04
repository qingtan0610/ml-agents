using UnityEngine;
using AI.Stats;
using Inventory;
using Inventory.Managers;
using Rooms;

namespace Player
{
    /// <summary>
    /// 管理玩家死亡和复活逻辑
    /// </summary>
    public class PlayerDeathManager : MonoBehaviour
    {
        [Header("Components")]
        private PlayerController2D playerController;
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        private CurrencyManager currencyManager;
        private AmmoManager ammoManager;
        
        [Header("Respawn Settings")]
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private bool autoRespawn = true; // 自动复活
        
        private float deathTime;
        private StatType deathCause;
        private bool isProcessingDeath = false;
        private bool pendingRespawn = false;
        private Vector3 pendingRespawnPosition;
        
        private void Awake()
        {
            Debug.Log("[PlayerDeathManager] Awake called");
            
            // 获取组件
            playerController = GetComponent<PlayerController2D>();
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            ammoManager = GetComponent<AmmoManager>();
            
            Debug.Log($"[PlayerDeathManager] Components found - AIStats: {aiStats != null}, PlayerController: {playerController != null}");
            
            // 验证必需组件
            if (aiStats == null)
            {
                Debug.LogError("[PlayerDeathManager] AIStats component not found!");
                enabled = false;
            }
        }
        
        private void Start()
        {
            Debug.Log($"[PlayerDeathManager] Initialized. AutoRespawn: {autoRespawn}, RespawnDelay: {respawnDelay}s");
            Debug.Log($"[PlayerDeathManager] Components - AIStats: {aiStats != null}, PlayerController: {playerController != null}");
            
            // 在Start中再次尝试注册事件监听器，以确保AIStats已经初始化
            if (aiStats != null)
            {
                Debug.Log("[PlayerDeathManager] Registering death event listener in Start...");
                aiStats.OnDeath.RemoveListener(HandleDeath); // 先移除避免重复
                aiStats.OnDeath.AddListener(HandleDeath);
                Debug.Log("[PlayerDeathManager] Death event listener registered in Start");
            }
        }
        
        private void OnEnable()
        {
            if (aiStats != null)
            {
                Debug.Log($"[PlayerDeathManager] OnEnable - aiStats exists, adding listener...");
                Debug.Log($"[PlayerDeathManager] OnDeath event listener count before: {aiStats.OnDeath.GetPersistentEventCount()}");
                
                aiStats.OnDeath.AddListener(HandleDeath);
                
                Debug.Log($"[PlayerDeathManager] OnDeath event listener count after: {aiStats.OnDeath.GetPersistentEventCount()}");
                Debug.Log("[PlayerDeathManager] OnDeath event listener added successfully");
                
                // 测试监听器
                StartCoroutine(TestEventListener());
            }
            else
            {
                Debug.LogError("[PlayerDeathManager] AIStats is null in OnEnable!");
            }
        }
        
        private System.Collections.IEnumerator TestEventListener()
        {
            yield return new WaitForSeconds(0.1f);
            Debug.Log($"[PlayerDeathManager] Testing event listener - AIStats exists: {aiStats != null}, IsDead: {aiStats?.IsDead}");
        }
        
        private void OnDisable()
        {
            if (aiStats != null)
            {
                aiStats.OnDeath.RemoveListener(HandleDeath);
            }
        }
        
        private void Update()
        {
            // 每60帧输出一次状态（约每1秒）
            if (Time.frameCount % 60 == 0 && aiStats != null)
            {
                Debug.Log($"[PlayerDeathManager] Status check - IsDead:{aiStats.IsDead}, Health:{aiStats.GetStat(StatType.Health):F1}, " +
                         $"DeathTime:{deathTime:F1}, IsProcessing:{isProcessingDeath}, AutoRespawn:{autoRespawn}");
            }
            
            // 检查玩家死亡状态
            if (aiStats != null && aiStats.IsDead)
            {
                // 如果死亡了但还没开始处理，可能是事件没触发
                if (!isProcessingDeath && deathTime == 0)
                {
                    Debug.LogWarning("[PlayerDeathManager] Player is dead but death event was not received! Forcing death handling...");
                    // 手动触发死亡处理
                    var lastDeathCause = StatType.Health; // 默认死因
                    if (aiStats.GetStat(StatType.Hunger) <= 0) lastDeathCause = StatType.Hunger;
                    else if (aiStats.GetStat(StatType.Thirst) <= 0) lastDeathCause = StatType.Thirst;
                    
                    HandleDeath(new AIDeathEventArgs(lastDeathCause, transform.position, 0));
                }
                
                // 自动复活检查 - 简化逻辑
                if (autoRespawn && deathTime > 0) // 只要有死亡时间就检查
                {
                    float timeSinceDeath = Time.time - deathTime;
                    Debug.Log($"[PlayerDeathManager] Checking auto-respawn: timeSinceDeath={timeSinceDeath:F1}, respawnDelay={respawnDelay}, autoRespawn={autoRespawn}");
                    
                    if (timeSinceDeath >= respawnDelay)
                    {
                        Debug.Log($"[PlayerDeathManager] Auto-respawn triggered! Time since death: {timeSinceDeath:F1}s");
                        Respawn();
                    }
                    else
                    {
                        Debug.Log($"[PlayerDeathManager] Waiting for auto-respawn in {(respawnDelay - timeSinceDeath):F1}s");
                    }
                }
            }
            else if (deathTime > 0)
            {
                // 如果不再死亡但deathTime还有值，重置它
                Debug.Log("[PlayerDeathManager] Player no longer dead, resetting death time");
                deathTime = 0;
                isProcessingDeath = false;
            }
            
            // 手动复活（按R键）
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (aiStats != null && aiStats.IsDead)
                {
                    Debug.Log("[PlayerDeathManager] Manual respawn triggered (R key)");
                    Respawn();
                }
                else if (aiStats != null)
                {
                    Debug.Log($"[PlayerDeathManager] R pressed but player not dead. IsDead: {aiStats.IsDead}");
                    Debug.Log($"[PlayerDeathManager] Current Health: {aiStats.GetStat(StatType.Health)}");
                }
            }
            
            // 处理延迟的复活位置设置
            if (pendingRespawn)
            {
                ApplyPendingRespawn();
            }
            
            // 调试信息
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (aiStats != null)
                {
                    Debug.Log($"[PlayerDeathManager] Debug Info:");
                    Debug.Log($"  - IsDead: {aiStats.IsDead}");
                    Debug.Log($"  - Health: {aiStats.GetStat(StatType.Health)}");
                    Debug.Log($"  - Hunger: {aiStats.GetStat(StatType.Hunger)}");
                    Debug.Log($"  - Thirst: {aiStats.GetStat(StatType.Thirst)}");
                    Debug.Log($"  - IsProcessingDeath: {isProcessingDeath}");
                    Debug.Log($"  - DeathTime: {deathTime}");
                }
            }
        }
        
        /// <summary>
        /// 处理死亡事件
        /// </summary>
        private void HandleDeath(AIDeathEventArgs args)
        {
            Debug.Log($"[PlayerDeathManager] ========== HANDLEDEATH CALLED ==========");
            Debug.Log($"[PlayerDeathManager] Death cause: {args.causeOfDeath}, Position: {args.deathPosition}, Time survived: {args.timeSurvived}");
            
            if (isProcessingDeath)
            {
                Debug.LogWarning("[PlayerDeathManager] Already processing death, ignoring duplicate death event");
                return;
            }
            
            isProcessingDeath = true;
            deathTime = Time.time;
            deathCause = args.causeOfDeath;
            
            Debug.Log($"[PlayerDeathManager] HandleDeath called - Player died from {deathCause} at time {deathTime}");
            Debug.Log($"[PlayerDeathManager] Auto respawn enabled: {autoRespawn}, respawn delay: {respawnDelay}");
            Debug.Log($"[PlayerDeathManager] isProcessingDeath set to: {isProcessingDeath}, deathTime set to: {deathTime}");
            
            // 调用PlayerController的死亡方法
            if (playerController != null)
            {
                playerController.Die();
            }
            else
            {
                Debug.LogWarning("[PlayerDeathManager] PlayerController is null!");
            }
            
            // 根据死亡原因清空物品
            ApplyDeathPenalties(deathCause);
            
            Debug.Log($"[PlayerDeathManager] ========== HANDLEDEATH FINISHED ==========");
        }
        
        /// <summary>
        /// 根据死亡原因应用惩罚
        /// </summary>
        private void ApplyDeathPenalties(StatType cause)
        {
            switch (cause)
            {
                case StatType.Health:
                    // 生命归零：清空所有物品
                    Debug.Log("[PlayerDeathManager] Health death - clearing all items");
                    ClearInventory();
                    ClearCurrency();
                    ClearAmmo();
                    break;
                    
                case StatType.Hunger:
                    // 饥饿归零：清空金币
                    Debug.Log("[PlayerDeathManager] Hunger death - clearing gold");
                    ClearCurrency();
                    break;
                    
                case StatType.Thirst:
                    // 口渴归零：清空药水
                    Debug.Log("[PlayerDeathManager] Thirst death - clearing potions");
                    ClearPotions();
                    break;
            }
        }
        
        /// <summary>
        /// 清空整个背包
        /// </summary>
        private void ClearInventory()
        {
            if (inventory == null) return;
            
            for (int i = 0; i < inventory.Size; i++)
            {
                inventory.RemoveItemAt(i, int.MaxValue);
            }
        }
        
        /// <summary>
        /// 清空金币
        /// </summary>
        private void ClearCurrency()
        {
            if (currencyManager != null)
            {
                int currentGold = currencyManager.CurrentGold;
                currencyManager.SpendGold(currentGold);
            }
        }
        
        /// <summary>
        /// 清空弹药
        /// </summary>
        private void ClearAmmo()
        {
            if (ammoManager == null) return;
            
            // 清空所有类型的弹药
            ammoManager.UseAmmo(AmmoType.Bullets, ammoManager.GetAmmo(AmmoType.Bullets));
            ammoManager.UseAmmo(AmmoType.Arrows, ammoManager.GetAmmo(AmmoType.Arrows));
            ammoManager.UseAmmo(AmmoType.Mana, ammoManager.GetAmmo(AmmoType.Mana));
        }
        
        /// <summary>
        /// 清空药水类物品
        /// </summary>
        private void ClearPotions()
        {
            if (inventory == null) return;
            
            // 由于口渴死亡时应该清空药水，我们通过物品名称判断
            // 这是一个简化的实现，实际游戏中可能需要更精确的判断
            for (int i = inventory.Size - 1; i >= 0; i--)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    // 检查是否是消耗品类型
                    if (slot.Item.ItemType == ItemType.Consumable)
                    {
                        // 可以通过物品名称包含"Potion"或"药水"来判断
                        if (slot.Item.ItemName.Contains("Potion") || 
                            slot.Item.ItemName.Contains("药水") ||
                            slot.Item.Description.Contains("恢复"))
                        {
                            inventory.RemoveItemAt(i, slot.Quantity);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 复活玩家
        /// </summary>
        private void Respawn()
        {
            if (!aiStats.IsDead)
            {
                Debug.LogWarning("[PlayerDeathManager] Trying to respawn but player is not dead!");
                return;
            }
            
            Debug.Log("[PlayerDeathManager] Starting respawn process...");
            
            // 找到出生点
            Vector3 spawnPosition = FindSpawnPoint();
            Debug.Log($"[PlayerDeathManager] Spawn position found: {spawnPosition}");
            
            // 先解冻刚体，确保可以移动
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            
            // 执行复活（AIStats.Respawn会设置位置和isDead）
            aiStats.Respawn(spawnPosition, false); // false因为我们已经处理了惩罚
            
            // 验证位置是否正确设置
            if (Vector3.Distance(transform.position, spawnPosition) > 0.1f)
            {
                Debug.LogWarning($"[PlayerDeathManager] Position mismatch after respawn! Expected: {spawnPosition}, Actual: {transform.position}");
                // 强制设置位置
                transform.position = spawnPosition;
            }
            
            // 验证复活是否成功
            if (!aiStats.IsDead)
            {
                // 只有在确认复活成功后才重置状态
                isProcessingDeath = false;
                deathTime = 0; // 重置死亡时间
                Debug.Log($"[PlayerDeathManager] Respawn successful! Death processing reset.");
            }
            else
            {
                Debug.LogError($"[PlayerDeathManager] Respawn failed! Player still dead. Health: {aiStats.GetStat(StatType.Health)}");
            }
            
            // 通知PlayerController复活
            if (playerController != null)
            {
                playerController.Respawn();
                playerController.enabled = true;
            }
            
            // 确保相机跟随
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, Camera.main.transform.position.z);
            }
            
            // 最后再次验证
            StartCoroutine(VerifyRespawnPosition(spawnPosition));
            
            Debug.Log($"[PlayerDeathManager] Player respawned at {spawnPosition}, isDead: {aiStats.IsDead}");
            Debug.Log($"[PlayerDeathManager] Current position: {transform.position}");
        }
        
        /// <summary>
        /// 验证复活位置是否正确
        /// </summary>
        private System.Collections.IEnumerator VerifyRespawnPosition(Vector3 expectedPosition)
        {
            yield return null; // 等待一帧
            
            if (Vector3.Distance(transform.position, expectedPosition) > 0.1f)
            {
                Debug.LogError($"[PlayerDeathManager] Position verification failed! Expected: {expectedPosition}, Actual: {transform.position}");
                Debug.LogError("[PlayerDeathManager] Something is overriding the player position after respawn!");
                
                // 标记需要在下一帧应用位置
                pendingRespawn = true;
                pendingRespawnPosition = expectedPosition;
            }
            else
            {
                Debug.Log($"[PlayerDeathManager] Position verification passed. Player is at {transform.position}");
            }
        }
        
        /// <summary>
        /// 在Update中应用待处理的复活位置
        /// </summary>
        private void ApplyPendingRespawn()
        {
            transform.position = pendingRespawnPosition;
            
            // 验证是否成功
            if (Vector3.Distance(transform.position, pendingRespawnPosition) < 0.1f)
            {
                Debug.Log($"[PlayerDeathManager] Pending respawn applied successfully. Position: {transform.position}");
                pendingRespawn = false;
            }
            else
            {
                Debug.LogWarning($"[PlayerDeathManager] Failed to apply pending respawn position. Retrying...");
            }
        }
        
        /// <summary>
        /// 找到出生点位置
        /// </summary>
        private Vector3 FindSpawnPoint()
        {
            // 查找地图生成器
            var mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null)
            {
                // 返回出生房间的中心位置
                return mapGenerator.GetSpawnPosition();
            }
            
            // 如果找不到地图生成器，返回默认位置
            Debug.LogWarning("[PlayerDeathManager] MapGenerator not found, using default spawn position");
            return new Vector3(8 * 16, 8 * 16, 0); // 默认出生房间在(8,8)，每个房间16单位
        }
    }
}