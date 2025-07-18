using UnityEngine;
using AI.Stats;
using AI.Perception;
using Combat;
using System.Collections.Generic;
using Rooms.Core;
using NPC.Core;
using Interactables;

namespace AI.Core
{
    /// <summary>
    /// AI奖励计算器 - 提供更精细的奖励机制
    /// </summary>
    public class AIRewardCalculator : MonoBehaviour
    {
        [Header("Combat Rewards")]
        [SerializeField] private float damageDealtReward = 0.02f;      // 每点伤害奖励 (从0.01提升)
        [SerializeField] private float killReward = 2.0f;              // 击杀奖励 (从1.0提升)
        [SerializeField] private float assistReward = 0.5f;            // 助攻奖励 (从0.3提升)
        [SerializeField] private float roomClearReward = 2.0f;         // 清理房间奖励 (从1.0提升)
        [SerializeField] private float damageTakenPenalty = -0.01f;    // 受伤惩罚 (减少惩罚，从-0.02降到-0.01)
        [SerializeField] private float combatEngagementReward = 0.3f;  // 参与战斗奖励 (从0.1大幅提升到0.3)
        
        [Header("Exploration Rewards")]
        [SerializeField] private float newRoomReward = 0.5f;           // 发现新房间
        [SerializeField] private float specialRoomReward = 1.0f;       // 发现特殊房间（商人、医生等）
        [SerializeField] private float portalDiscoveryReward = 5.0f;   // 发现传送门的巨大奖励
        [SerializeField] private float portalInteractionReward = 2.0f; // 与传送门交互奖励
        [SerializeField] private float treasureRoomReward = 2.0f;      // 发现宝箱房间
        [SerializeField] private float treasureOpenReward = 1.5f;      // 打开宝箱奖励
        [SerializeField] private float fountainRoomReward = 1.5f;      // 发现泉水房间
        [SerializeField] private float fountainUseReward = 1.0f;       // 使用泉水奖励
        [SerializeField] private float mapCoverageReward = 0.001f;     // 地图覆盖度奖励
        
        [Header("Social Rewards")]
        [SerializeField] private float communicationReward = 0.2f;     // 成功通信
        [SerializeField] private float faceToFaceReward = 0.5f;        // 面对面交流
        [SerializeField] private float helpTeammateReward = 0.5f;      // 帮助队友
        [SerializeField] private float portalCoordinationReward = 10.0f; // 协同激活传送门（最高奖励）
        [SerializeField] private float shareInfoReward = 0.4f;          // 分享重要信息（水源、商人等）
        [SerializeField] private float reduceLonelinessReward = 0.3f;  // 降低队友孤独感
        [SerializeField] private float teamProximityReward = 0.1f;     // 保持团队接近
        
        [Header("Resource Management")]
        [SerializeField] private float efficientItemUseReward = 0.3f;  // 高效使用物品
        [SerializeField] private float tradeSuccessReward = 1.0f;      // AI间交易成功 (大幅提升)
        [SerializeField] private float npcTradeReward = 0.2f;          // NPC交易成功
        [SerializeField] private float avoidWasteReward = 0.1f;        // 避免浪废
        [SerializeField] private float strategicPurchaseReward = 0.4f; // 战略性购买
        [SerializeField] private float smartTradeReward = 1.5f;        // 智能交易决策 (新增)
        [SerializeField] private float tradeInitiativeReward = 0.5f;   // 主动发起交易 (新增)
        [SerializeField] private float inventoryOptimizationReward = 0.8f; // 背包优化 (新增)
        
        [Header("Survival Rewards")]
        [SerializeField] private float survivalBonus = 0.5f;           // 生存奖励系数（大幅提升）
        [SerializeField] private float criticalRecoveryReward = 1.0f;  // 从危急状态恢复（提升）
        [SerializeField] private float deathPenalty = -10.0f;          // 死亡惩罚（加重）
        [SerializeField] private float itemUseReward = 0.5f;           // 使用物品奖励
        [SerializeField] private float drinkWaterReward = 0.8f;        // 喝水奖励
        [SerializeField] private float eatFoodReward = 0.8f;           // 吃食物奖励
        [SerializeField] private float pickupItemReward = 0.3f;        // 拾取物品奖励
        
        // 追踪数据
        private float lastHealth;
        private float totalDamageDealt;
        private float totalDamageTaken;
        private HashSet<Enemy.Enemy2D> damagedEnemies = new HashSet<Enemy.Enemy2D>();
        private HashSet<string> visitedRooms = new HashSet<string>();
        private int lastItemCount;
        private bool wasInCriticalState;
        private float lastLonelinessLevel;
        
        // 行为追踪
        private float lastThirst;
        private float lastHunger;
        private bool usedItemThisFrame = false;
        private bool drankWaterThisFrame = false;
        private bool ateFoodThisFrame = false;
        private bool pickedUpItemThisFrame = false;
        
        // 组件引用
        private AIStats aiStats;
        private CombatSystem2D combatSystem;
        private AIBrain aiBrain;
        private AICommunicator communicator;
        private Inventory.Inventory inventory;
        private AITradeManager tradeManager;
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            combatSystem = GetComponent<CombatSystem2D>();
            aiBrain = GetComponent<AIBrain>();
            communicator = GetComponent<AICommunicator>();
            inventory = GetComponent<Inventory.Inventory>();
            tradeManager = GetComponent<AITradeManager>();
            
            // 订阅战斗事件
            if (combatSystem != null)
            {
                combatSystem.OnDamageDealt += OnDamageDealt;
                combatSystem.OnKill += OnKill;
            }
            
            // 初始化
            lastHealth = aiStats?.CurrentHealth ?? 100f;
            lastThirst = aiStats?.CurrentThirst ?? 100f;
            lastHunger = aiStats?.CurrentHunger ?? 100f;
            lastItemCount = 0; // 延迟到Start中初始化
        }
        
        private void Start()
        {
            // 在Start中初始化需要其他组件的值
            if (inventory != null)
            {
                lastItemCount = GetUsedSlotCount();
            }
        }
        
        private void OnDestroy()
        {
            // 取消订阅
            if (combatSystem != null)
            {
                combatSystem.OnDamageDealt -= OnDamageDealt;
                combatSystem.OnKill -= OnKill;
            }
        }
        
        /// <summary>
        /// 计算综合奖励
        /// </summary>
        public float CalculateTotalReward()
        {
            float totalReward = 0f;
            
            // 1. 生存奖励
            totalReward += CalculateSurvivalReward();
            
            // 2. 战斗奖励
            totalReward += CalculateCombatReward();
            
            // 3. 探索奖励
            totalReward += CalculateExplorationReward();
            
            // 4. 社交奖励
            totalReward += CalculateSocialReward();
            
            // 5. 资源管理奖励
            totalReward += CalculateResourceReward();
            
            // 6. 策略奖励
            totalReward += CalculateStrategyReward();
            
            // 重置帧数据
            ResetFrameData();
            
            return totalReward;
        }
        
        private float CalculateSurvivalReward()
        {
            if (aiStats == null || aiStats.Config == null) return 0f;
            
            float reward = 0f;
            
            // 基础生存奖励
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            float staminaRatio = aiStats.CurrentStamina / aiStats.Config.maxStamina;
            
            reward += (healthRatio + hungerRatio + thirstRatio + staminaRatio) * survivalBonus;
            
            // 危急恢复奖励
            bool isInCritical = healthRatio < 0.3f || hungerRatio < 0.2f || thirstRatio < 0.2f;
            if (wasInCriticalState && !isInCritical)
            {
                reward += criticalRecoveryReward;
                Debug.Log($"[RewardCalculator] {name} 从危急状态恢复，奖励: {criticalRecoveryReward}");
            }
            wasInCriticalState = isInCritical;
            
            // 死亡惩罚
            if (aiStats.IsDead)
            {
                reward += deathPenalty;
            }
            
            // 受伤惩罚
            float damageTaken = lastHealth - aiStats.CurrentHealth;
            if (damageTaken > 0)
            {
                reward += damageTaken * damageTakenPenalty;
                totalDamageTaken += damageTaken;
            }
            
            // 检测喝水行为
            float thirstIncrease = aiStats.CurrentThirst - lastThirst;
            if (thirstIncrease > 5f) // 口渴值增加超过5
            {
                reward += drinkWaterReward;
                drankWaterThisFrame = true;
                Debug.Log($"[RewardCalculator] {name} 喝水奖励: {drinkWaterReward}");
            }
            
            // 检测进食行为
            float hungerIncrease = aiStats.CurrentHunger - lastHunger;
            if (hungerIncrease > 5f) // 饥饿值增加超过5
            {
                reward += eatFoodReward;
                ateFoodThisFrame = true;
                Debug.Log($"[RewardCalculator] {name} 进食奖励: {eatFoodReward}");
            }
            
            // 更新上一帧的值
            lastHealth = aiStats.CurrentHealth;
            lastThirst = aiStats.CurrentThirst;
            lastHunger = aiStats.CurrentHunger;
            
            return reward;
        }
        
        private float CalculateCombatReward()
        {
            float reward = 0f;
            
            // 伤害奖励（通过事件累积）
            if (totalDamageDealt > 0)
            {
                reward += totalDamageDealt * damageDealtReward;
                Debug.Log($"[RewardCalculator] {name} 造成伤害奖励: {totalDamageDealt * damageDealtReward}");
            }
            
            // 参与战斗奖励
            if (damagedEnemies.Count > 0)
            {
                reward += combatEngagementReward;
            }
            
            return reward;
        }
        
        private float CalculateExplorationReward()
        {
            float reward = 0f;
            
            // 获取当前房间信息
            var currentRoom = GetCurrentRoom();
            if (currentRoom != null)
            {
                string roomKey = GetRoomKey(currentRoom);
                if (!visitedRooms.Contains(roomKey))
                {
                    visitedRooms.Add(roomKey);
                    reward += newRoomReward;
                    
                    // 特殊房间额外奖励
                    if (IsSpecialRoom(currentRoom))
                    {
                        // 根据房间类型给予不同奖励
                        if (HasPortal(currentRoom))
                        {
                            reward += portalDiscoveryReward;
                            Debug.Log($"[RewardCalculator] {name} 发现传送门！巨大奖励: {portalDiscoveryReward}");
                        }
                        else if (HasTreasureChest(currentRoom))
                        {
                            reward += treasureRoomReward;
                            Debug.Log($"[RewardCalculator] {name} 发现宝箱房间，奖励: {treasureRoomReward}");
                        }
                        else if (HasFountain(currentRoom))
                        {
                            reward += fountainRoomReward;
                            Debug.Log($"[RewardCalculator] {name} 发现泉水房间，奖励: {fountainRoomReward}");
                        }
                        else
                        {
                            reward += specialRoomReward;
                            Debug.Log($"[RewardCalculator] {name} 发现特殊房间（NPC），奖励: {specialRoomReward}");
                        }
                    }
                }
            }
            
            // 地图覆盖度奖励
            float coveragePercent = visitedRooms.Count / 256f; // 16x16地图
            reward += coveragePercent * mapCoverageReward;
            
            return reward;
        }
        
        private float CalculateSocialReward()
        {
            float reward = 0f;
            
            if (communicator == null) return reward;
            
            // 面对面交流奖励
            if (communicator.LastFaceToFaceTime > Time.time - 1f)
            {
                reward += faceToFaceReward;
                Debug.Log($"[RewardCalculator] {name} 面对面交流奖励: {faceToFaceReward}");
            }
            
            // 成功通信奖励
            if (communicator.LastMessageTime > Time.time - 1f)
            {
                reward += communicationReward;
            }
            
            // 降低孤独感奖励
            float currentLoneliness = aiStats.GetMood(MoodDimension.Social);
            if (currentLoneliness > lastLonelinessLevel && lastLonelinessLevel < -30f)
            {
                reward += reduceLonelinessReward;
                Debug.Log($"[RewardCalculator] {name} 降低孤独感奖励: {reduceLonelinessReward}");
            }
            lastLonelinessLevel = currentLoneliness;
            
            // 智能交流时机奖励
            reward += CalculateSmartCommunicationReward();
            
            return reward;
        }
        
        /// <summary>
        /// 计算智能交流时机奖励 - 奖励在正确时机的交流
        /// </summary>
        private float CalculateSmartCommunicationReward()
        {
            if (communicator == null) return 0f;
            
            float reward = 0f;
            bool justCommunicated = communicator.LastMessageTime > Time.time - 1f;
            
            if (!justCommunicated) return 0f;
            
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float socialMood = aiStats.GetMood(MoodDimension.Social);
            var currencyManager = GetComponent<Inventory.Managers.CurrencyManager>();
            int gold = currencyManager?.CurrentGold ?? 0;
            
            // 1. 在紧急情况下求助 - 高奖励
            if (healthRatio < 0.3f && gold < 30)
            {
                reward += shareInfoReward * 2f; // 双倍奖励紧急求助
                Debug.Log($"[RewardCalculator] {name} 紧急求助交流奖励: {shareInfoReward * 2f}");
            }
            
            // 2. 发现重要资源后分享 - 高奖励
            var perception = GetComponent<AIPerception>();
            if (perception != null)
            {
                var npcs = perception.GetNearbyNPCs();
                var enemies = perception.GetNearbyEnemies();
                
                if (npcs.Count > 0) // 发现NPC后交流
                {
                    reward += shareInfoReward * 1.5f;
                    Debug.Log($"[RewardCalculator] {name} 分享NPC发现奖励: {shareInfoReward * 1.5f}");
                }
                
                if (enemies.Count > 2) // 发现大量敌人后警告队友
                {
                    reward += shareInfoReward;
                    Debug.Log($"[RewardCalculator] {name} 战斗警告交流奖励: {shareInfoReward}");
                }
            }
            
            // 3. 在孤独时主动交流 - 中等奖励
            if (socialMood < -20f)
            {
                reward += communicationReward * 1.5f;
                Debug.Log($"[RewardCalculator] {name} 缓解孤独交流奖励: {communicationReward * 1.5f}");
            }
            
            // 4. 定期维护社交关系 - 小奖励
            if (socialMood > -10f && socialMood < 30f)
            {
                reward += communicationReward * 0.5f;
            }
            
            return reward;
        }
        
        private float CalculateResourceReward()
        {
            float reward = 0f;
            
            if (inventory == null) return reward;
            
            // 物品使用效率
            int currentItemCount = GetUsedSlotCount();
            
            // 检测物品使用（背包物品减少）
            if (currentItemCount < lastItemCount)
            {
                // 判断是否高效使用
                bool efficientUse = false;
                if (aiStats.CurrentHealth > lastHealth) efficientUse = true;
                if (aiStats.CurrentThirst > lastThirst) efficientUse = true;
                if (aiStats.CurrentHunger > lastHunger) efficientUse = true;
                
                if (efficientUse)
                {
                    reward += efficientItemUseReward;
                    Debug.Log($"[RewardCalculator] {name} 高效使用物品奖励: {efficientItemUseReward}");
                }
                
                // 基础使用物品奖励
                reward += itemUseReward;
                usedItemThisFrame = true;
                Debug.Log($"[RewardCalculator] {name} 使用物品奖励: {itemUseReward}");
            }
            
            // 检测物品拾取（背包物品增加）
            if (currentItemCount > lastItemCount)
            {
                reward += pickupItemReward;
                pickedUpItemThisFrame = true;
                Debug.Log($"[RewardCalculator] {name} 拾取物品奖励: {pickupItemReward}");
            }
            
            // AI间交易奖励
            reward += CalculateTradeReward();
            
            // 背包管理奖励
            reward += CalculateInventoryManagementReward();
            
            lastItemCount = currentItemCount;
            
            return reward;
        }
        
        /// <summary>
        /// 计算交易相关奖励
        /// </summary>
        private float CalculateTradeReward()
        {
            float reward = 0f;
            
            if (tradeManager == null) return reward;
            
            // 成功的AI间交易奖励
            int tradeCount = tradeManager.GetTradeHistoryCount();
            if (tradeCount > 0)
            {
                // 假设最近有交易发生，给予奖励
                reward += tradeSuccessReward;
                Debug.Log($"[RewardCalculator] {name} AI间交易成功奖励: {tradeSuccessReward}");
            }
            
            // 主动发起交易的奖励（通过交易管理器的状态检测）
            // 这需要交易管理器提供更多状态信息
            
            return reward;
        }
        
        /// <summary>
        /// 计算背包管理奖励
        /// </summary>
        private float CalculateInventoryManagementReward()
        {
            float reward = 0f;
            
            if (inventory == null) return reward;
            
            float inventoryFullness = GetInventoryFullness();
            int currentUsedSlots = GetUsedSlotCount();
            
            // 背包接近满时主动整理的奖励
            if (inventoryFullness > 0.8f && currentUsedSlots < lastItemCount)
            {
                reward += inventoryOptimizationReward;
                Debug.Log($"[RewardCalculator] {name} 背包优化奖励: {inventoryOptimizationReward}");
            }
            
            // 保持合理背包使用率的奖励
            if (inventoryFullness > 0.3f && inventoryFullness < 0.9f)
            {
                reward += 0.1f; // 小幅奖励合理的背包管理
            }
            
            return reward;
        }
        
        /// <summary>
        /// 获取背包使用率
        /// </summary>
        private float GetInventoryFullness()
        {
            if (inventory == null) return 0f;
            
            int usedSlots = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    usedSlots++;
            }
            return (float)usedSlots / inventory.Size;
        }
        
        private float CalculateStrategyReward()
        {
            float reward = 0f;
            
            // 追击决策奖励
            var controller = GetComponent<AIController>();
            if (controller != null && controller.ChaseTarget != null)
            {
                // AI选择追击而不是无效攻击，给予奖励
                reward += 0.1f;
            }
            
            // 智能撤退奖励
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.3f)
            {
                // 低血量时远离敌人
                var perception = GetComponent<AIPerception>();
                if (perception != null)
                {
                    var enemies = perception.GetNearbyEnemies();
                    if (enemies.Count > 0)
                    {
                        float minDistance = float.MaxValue;
                        foreach (var enemy in enemies)
                        {
                            float dist = Vector2.Distance(transform.position, enemy.transform.position);
                            if (dist < minDistance) minDistance = dist;
                        }
                        
                        // 距离越远奖励越高
                        if (minDistance > 5f)
                        {
                            reward += 0.2f;
                        }
                    }
                }
            }
            
            // 资源寻找奖励
            if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.4f ||
                aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.4f)
            {
                // 低资源时接近NPC或已知资源点
                var memory = GetComponent<AIMemory>();
                var rb = GetComponent<Rigidbody2D>();
                if (memory != null && rb != null && memory.IsMovingTowardImportantLocation(transform.position, rb.velocity))
                {
                    reward += 0.15f;
                }
            }
            
            // 团队协作奖励
            var allAIs = FindObjectsOfType<AIBrain>();
            int nearbyTeammates = 0;
            foreach (var ai in allAIs)
            {
                if (ai != aiBrain && Vector2.Distance(transform.position, ai.transform.position) < 10f)
                {
                    nearbyTeammates++;
                }
            }
            
            // 根据情况判断是否应该聚集
            if (nearbyTeammates > 0)
            {
                // 检查是否在传送门附近
                var portal = GameObject.FindObjectOfType<TeleportDevice>();
                if (portal != null && Vector2.Distance(transform.position, portal.transform.position) < 15f)
                {
                    // 在传送门附近聚集是好的
                    reward += teamProximityReward * nearbyTeammates;
                }
            }
            
            return reward;
        }
        
        // 事件处理
        private void OnDamageDealt(GameObject target, float damage)
        {
            totalDamageDealt += damage;
            
            var enemy = target.GetComponent<Enemy.Enemy2D>();
            if (enemy != null)
            {
                damagedEnemies.Add(enemy);
            }
        }
        
        private void OnKill(GameObject target)
        {
            // 击杀奖励在AIBrain中处理，这里可以添加额外逻辑
            var enemy = target.GetComponent<Enemy.Enemy2D>();
            if (enemy != null && damagedEnemies.Contains(enemy))
            {
                // 这是我们参与击杀的敌人
                Debug.Log($"[RewardCalculator] {name} 击杀了敌人");
            }
        }
        
        private void ResetFrameData()
        {
            totalDamageDealt = 0f;
            damagedEnemies.Clear();
            usedItemThisFrame = false;
            drankWaterThisFrame = false;
            ateFoodThisFrame = false;
            pickedUpItemThisFrame = false;
        }
        
        // 辅助方法
        private SimplifiedRoom GetCurrentRoom()
        {
            // 通过碰撞检测获取当前房间
            var colliders = Physics2D.OverlapCircleAll(transform.position, 0.5f);
            foreach (var collider in colliders)
            {
                var room = collider.GetComponentInParent<SimplifiedRoom>();
                if (room != null) return room;
            }
            return null;
        }
        
        private string GetRoomKey(SimplifiedRoom room)
        {
            return $"{room.transform.position.x},{room.transform.position.y}";
        }
        
        private bool IsSpecialRoom(SimplifiedRoom room)
        {
            // 检查是否是特殊房间（商人、医生、铁匠等）
            return room.GetComponentInChildren<NPCBase>() != null ||
                   room.GetComponentInChildren<TeleportDevice>() != null ||
                   room.GetComponentInChildren<Interactables.TreasureChest>() != null ||
                   room.GetComponentInChildren<Interactables.Fountain>() != null;
        }
        
        private bool HasPortal(SimplifiedRoom room)
        {
            return room.GetComponentInChildren<TeleportDevice>() != null;
        }
        
        private bool HasTreasureChest(SimplifiedRoom room)
        {
            return room.GetComponentInChildren<Interactables.TreasureChest>() != null;
        }
        
        private bool HasFountain(SimplifiedRoom room)
        {
            return room.GetComponentInChildren<Interactables.Fountain>() != null;
        }
        
        private int GetUsedSlotCount()
        {
            if (inventory == null) return 0;
            
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    count++;
            }
            return count;
        }
        
        // 公共查询方法
        public bool UsedItemThisFrame() => usedItemThisFrame;
        public bool DrankWaterThisFrame() => drankWaterThisFrame;
        public bool AteFoodThisFrame() => ateFoodThisFrame;
        public bool PickedUpItemThisFrame() => pickedUpItemThisFrame;
    }
}