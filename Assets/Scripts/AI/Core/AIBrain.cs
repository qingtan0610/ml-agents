using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Perception;
using AI.Decision;
using Inventory;
using Inventory.Items;
using Inventory.Managers;

namespace AI.Core
{
    /// <summary>
    /// AI智能体的大脑 - 负责感知、决策和行动
    /// </summary>
    public class AIBrain : Agent
    {
        [Header("Core Components")]
        [SerializeField] private AIStats aiStats;
        [SerializeField] private Inventory.Inventory inventory;
        [SerializeField] private AIPerception perception;
        [SerializeField] private AIController controller;
        private Inventory.Managers.CurrencyManager currencyManager;
        private Inventory.Managers.AmmoManager ammoManager;
        
        [Header("Decision Settings")]
        [SerializeField] private float decisionInterval = 0.5f;
        [SerializeField] private bool useDeepSeekAPI = true;     // 重新启用DeepSeek（已修复感知系统卡死问题）
        [SerializeField] private float deepSeekThreshold = 0.3f; // 当AI健康度低于此值时调用DeepSeek
        [SerializeField] private float deepSeekCooldown = 20f;   // DeepSeek调用冷却时间 - 增加到20秒
        [SerializeField] private float initialDecisionDelay = 5f; // 初始决策延迟 - 增加到5秒确保场景完全初始化
        private float lastDeepSeekTime = -10f;
        private bool hasInitialDecision = false;
        
        [Header("Debug Settings")]
        [SerializeField] private bool debugPerception = true;
        [SerializeField] private float debugInterval = 3f;
        private float nextDebugTime = 0f;
        
        [Header("Current State")]
        [SerializeField] private AIState currentState = AIState.Exploring;
        [SerializeField] private Vector2 currentTarget;
        [SerializeField] private GameObject currentInteractable;
        
        // 决策系统
        private float nextDecisionTime = 0f;
        private AIMemory memory;
        private DeepSeekDecisionMaker deepSeekDecisionMaker;
        private AIRewardCalculator rewardCalculator;
        private AIGoalSystem goalSystem;
        
        // ML-Agents观察空间
        private const int GRID_SIZE = 16; // 房间网格大小
        private const int VISION_RANGE = 1; // 视野范围（房间数）
        
        protected override void Awake()
        {
            base.Awake();
            
            // 获取组件
            if (aiStats == null) aiStats = GetComponent<AIStats>();
            if (inventory == null) inventory = GetComponent<Inventory.Inventory>();
            if (perception == null) perception = GetComponent<AIPerception>();
            if (controller == null) controller = GetComponent<AIController>();
            if (currencyManager == null) currencyManager = GetComponent<Inventory.Managers.CurrencyManager>();
            if (ammoManager == null) ammoManager = GetComponent<Inventory.Managers.AmmoManager>();
            
            // 获取或添加奖励计算器
            rewardCalculator = GetComponent<AIRewardCalculator>();
            if (rewardCalculator == null)
            {
                rewardCalculator = gameObject.AddComponent<AIRewardCalculator>();
                Debug.Log($"[AIBrain] 为 {name} 添加了AIRewardCalculator");
            }
            
            // 获取或添加目标系统
            goalSystem = GetComponent<AIGoalSystem>();
            if (goalSystem == null)
            {
                goalSystem = gameObject.AddComponent<AIGoalSystem>();
                Debug.Log($"[AIBrain] 为 {name} 添加了AIGoalSystem");
            }
            
            // 初始化系统
            memory = new AIMemory();
            if (useDeepSeekAPI)
            {
                deepSeekDecisionMaker = new DeepSeekDecisionMaker();
            }
        }
        
        public override void OnEpisodeBegin()
        {
            // 重置AI状态
            currentState = AIState.Exploring;
            currentTarget = Vector2.zero;
            currentInteractable = null;
            memory.Clear();
            hasInitialDecision = false;
            lastDeepSeekTime = Time.time - deepSeekCooldown; // 允许立即使用DeepSeek
            
            // 重置位置到出生点
            controller.ResetToSpawn();
        }
        
        public override void CollectObservations(VectorSensor sensor)
        {
            // 防止组件为空导致卡死
            if (aiStats == null || aiStats.Config == null || inventory == null || perception == null)
            {
                // 添加默认观察值: 11(状态) + 7(背包) + 27(房间9*3) + 15(敌人5*3) + 8(记忆) + 12(其他AI) + 17(目标) = 97
                for (int i = 0; i < 97; i++)
                {
                    sensor.AddObservation(0f);
                }
                return;
            }
            
            try
            {
                // 1. AI自身状态 (11个值) - 扩展状态信息
                sensor.AddObservation(aiStats.CurrentHealth / aiStats.Config.maxHealth);
                sensor.AddObservation(aiStats.CurrentHunger / aiStats.Config.maxHunger);
                sensor.AddObservation(aiStats.CurrentThirst / aiStats.Config.maxThirst);
                sensor.AddObservation(aiStats.CurrentStamina / aiStats.Config.maxStamina);
                sensor.AddObservation(aiStats.GetStat(StatType.Armor) / aiStats.Config.maxArmor);
                sensor.AddObservation(aiStats.GetStat(StatType.Toughness) / aiStats.Config.maxToughness);
                sensor.AddObservation(aiStats.GetMood(MoodDimension.Emotion) / 100f);
                sensor.AddObservation(aiStats.GetMood(MoodDimension.Social) / 100f);
                sensor.AddObservation(aiStats.GetMood(MoodDimension.Mentality) / 100f);
                sensor.AddObservation(aiStats.IsDead ? 1f : 0f);
                sensor.AddObservation(0f); // 死亡次数(未实现)
            
            // 2. 背包和资源状态 (7个值) - 扩展资源信息
            int itemCount = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    itemCount++;
            }
            sensor.AddObservation(itemCount / (float)inventory.Size); // 背包占用率
            sensor.AddObservation(inventory.EquippedWeapon != null ? 1f : 0f); // 是否装备武器
            sensor.AddObservation((currencyManager?.CurrentGold ?? 0) / 1000f); // 金币数量归一化
            sensor.AddObservation((ammoManager?.GetAmmo(AmmoType.Bullets) ?? 0) / 100f); // 子弹
            sensor.AddObservation((ammoManager?.GetAmmo(AmmoType.Arrows) ?? 0) / 100f); // 箭矢
            sensor.AddObservation((ammoManager?.GetAmmo(AmmoType.Mana) ?? 0) / 100f); // 法力
            sensor.AddObservation(HasKeyInInventory() ? 1f : 0f); // 是否有钥匙
            
            // 3. 感知信息 (可变大小，使用固定大小编码)
            var visibleRooms = perception.GetVisibleRooms();
            var nearbyEnemies = perception.GetNearbyEnemies();
            var nearbyNPCs = perception.GetNearbyNPCs();
            var nearbyItems = perception.GetNearbyItems();
            
            // 编码房间信息 (最多9个房间：当前+周围8个)
            for (int i = 0; i < 9; i++)
            {
                if (i < visibleRooms.Count)
                {
                    var room = visibleRooms[i];
                    sensor.AddObservation((float)room.RoomType / 10f); // 房间类型归一化
                    sensor.AddObservation(room.IsExplored ? 1f : 0f);
                    sensor.AddObservation(room.IsCleared ? 1f : 0f);
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }
            
            // 编码敌人信息 (最多5个)
            for (int i = 0; i < 5; i++)
            {
                if (i < nearbyEnemies.Count)
                {
                    var enemy = nearbyEnemies[i];
                    Vector2 relativePos = (Vector2)enemy.transform.position - (Vector2)transform.position;
                    sensor.AddObservation(relativePos.x / 10f);
                    sensor.AddObservation(relativePos.y / 10f);
                    sensor.AddObservation(enemy.CurrentHealth / enemy.MaxHealth);
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }
            
                // 4. 记忆信息 (8个值) - 扩展重要位置记忆
                var importantLocations = memory.GetImportantLocations();
                sensor.AddObservation(importantLocations.ContainsKey("Portal") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("Fountain") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("Merchant") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("Doctor") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("Restaurant") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("Blacksmith") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("Tailor") ? 1f : 0f);
                sensor.AddObservation(importantLocations.ContainsKey("TreasureRoom") ? 1f : 0f);
                
                // 5. 其他AI信息 (12个值) - 3个AI x 4值（位置、生命、是否求救）
                var otherAIs = GameObject.FindGameObjectsWithTag("Player");
                int aiIndex = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (aiIndex < otherAIs.Length)
                    {
                        var otherAI = otherAIs[aiIndex];
                        if (otherAI != gameObject)
                        {
                            Vector2 relativePos = (Vector2)otherAI.transform.position - (Vector2)transform.position;
                            sensor.AddObservation(relativePos.x / 50f); // 更大的归一化范围
                            sensor.AddObservation(relativePos.y / 50f);
                            var otherStats = otherAI.GetComponent<AIStats>();
                            sensor.AddObservation(otherStats?.CurrentHealth / otherStats?.Config.maxHealth ?? 0f);
                            sensor.AddObservation(otherStats?.CurrentHealth < otherStats?.Config.maxHealth * 0.3f ? 1f : 0f); // 是否需要帮助
                            aiIndex++;
                        }
                    }
                    else
                    {
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                    }
                }
                
                // 6. 目标信息 (17个值)
                if (goalSystem != null)
                {
                    float[] goalEncoding = goalSystem.GetGoalEncoding();
                    foreach (float value in goalEncoding)
                    {
                        sensor.AddObservation(value);
                    }
                }
                else
                {
                    // 如果目标系统不存在，添加默认值
                    for (int i = 0; i < 17; i++)
                    {
                        sensor.AddObservation(0f);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIBrain] CollectObservations异常: {e.Message}");
                // 在异常情况下添加默认观察值
                for (int i = 0; i < 97; i++)
                {
                    sensor.AddObservation(0f);
                }
            }
        }
        
        public override void OnActionReceived(ActionBuffers actions)
        {
            // 检查是否已经死亡或组件缺失
            if (aiStats == null || aiStats.IsDead || controller == null || perception == null)
            {
                // AI已死亡或组件缺失，不执行任何动作
                return;
            }
            
            // 调试感知系统
            if (debugPerception && Time.time >= nextDebugTime)
            {
                nextDebugTime = Time.time + debugInterval;
                perception?.DebugPerception();
            }
            
            try
            {
                // 离散动作空间
                int moveAction = actions.DiscreteActions[0]; // 0-4: 不动、上下左右
                int interactAction = actions.DiscreteActions[1]; // 0-2: 不交互、物品交互、面对面交流
                int itemAction = actions.DiscreteActions[2]; // 0-10: 不使用、使用物品槽1-10
                int communicateAction = actions.DiscreteActions[3]; // 0-6: 不通信、发送各种消息
                int combatAction = actions.DiscreteActions[4]; // 0-2: 不攻击、攻击、切换武器
            
            // 执行移动
            Vector2 moveDirection = Vector2.zero;
            switch (moveAction)
            {
                case 1: moveDirection = Vector2.up; break;
                case 2: moveDirection = Vector2.down; break;
                case 3: moveDirection = Vector2.left; break;
                case 4: moveDirection = Vector2.right; break;
            }
            controller.Move(moveDirection);
            
            // 执行交互
            if (interactAction == 1)
            {
                controller.TryInteract();
            }
            else if (interactAction == 2)
            {
                controller.TryFaceToFaceInteraction();
            }
            
            // 使用物品
            if (itemAction > 0 && itemAction <= inventory.Size)
            {
                controller.UseItem(itemAction - 1);
            }
            
            // 通信
            if (communicateAction > 0)
            {
                controller.SendCommunication((CommunicationType)(communicateAction - 1));
            }
            
            // 战斗行动
            if (combatAction > 0)
            {
                var nearbyEnemies = perception.GetNearbyEnemies();
                Debug.Log($"[AIBrain] {name} 战斗行动 {combatAction}, 检测到敌人数: {nearbyEnemies.Count}");
                
                if (nearbyEnemies.Count > 0)
                {
                    var target = nearbyEnemies[0]; // 选择最近的敌人
                    Debug.Log($"[AIBrain] {name} 目标敌人: {target.name} 距离: {Vector2.Distance(transform.position, target.transform.position)}");
                    
                    if (combatAction == 1)
                    {
                        // 攻击
                        Debug.Log($"[AIBrain] {name} 执行攻击");
                        controller.Attack(target.gameObject);
                    }
                    else if (combatAction == 2)
                    {
                        // 切换到最佳武器
                        controller.SelectBestWeapon(target.gameObject);
                    }
                }
            }
            
                // 计算奖励
                CalculateRewards();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIBrain] OnActionReceived异常: {e.Message}");
                // 在异常情况下不执行任何动作
            }
        }
        
        private void CalculateRewards()
        {
            // 使用新的奖励计算器
            if (rewardCalculator != null)
            {
                float totalReward = rewardCalculator.CalculateTotalReward();
                AddReward(totalReward);
                
                // 死亡时结束episode
                if (aiStats.IsDead)
                {
                    EndEpisode();
                }
                
                return;
            }
            
            // 备用：如果奖励计算器不存在，使用原始逻辑
            float reward = 0f;
            
            // 生存奖励
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            reward += (healthRatio + hungerRatio + thirstRatio) * 0.1f;
            
            // 探索奖励
            if (perception.DiscoveredNewRoom())
            {
                reward += 0.5f;
            }
            
            // 战斗奖励
            if (controller.KilledEnemyThisFrame())
            {
                reward += 1f;
            }
            
            // 物品收集奖励
            if (controller.CollectedItemThisFrame())
            {
                reward += 0.3f;
            }
            
            // 到达传送门奖励
            if (controller.ReachedPortal())
            {
                reward += 5f;
            }
            
            // 死亡惩罚
            if (aiStats.IsDead)
            {
                reward -= 5f;
                EndEpisode();
            }
            
            // 心情奖励
            float moodBonus = (aiStats.GetMood(MoodDimension.Emotion) + 
                              aiStats.GetMood(MoodDimension.Social) + 
                              aiStats.GetMood(MoodDimension.Mentality)) / 300f; // 归一化到-1到1
            reward += moodBonus * 0.05f;
            
            AddReward(reward);
        }
        
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // 基于目标系统的启发式控制
            var discreteActions = actionsOut.DiscreteActions;
            
            // 初始化所有动作为0
            for (int i = 0; i < discreteActions.Length; i++)
            {
                discreteActions[i] = 0;
            }
            
            // 如果目标系统不可用，使用原始逻辑
            if (goalSystem == null || goalSystem.CurrentLowLevelGoal == null)
            {
                HeuristicFallback(actionsOut);
                return;
            }
            
            // 获取当前环境信息
            var nearbyEnemies = perception.GetNearbyEnemies();
            var nearbyItems = perception.GetNearbyItems();
            var nearbyNPCs = perception.GetNearbyNPCs();
            
            // 基于当前低层目标执行动作
            switch (goalSystem.CurrentLowLevelGoal.Type)
            {
                case GoalType.Attack:
                    ExecuteAttackGoal(discreteActions, nearbyEnemies);
                    break;
                    
                case GoalType.MoveToTarget:
                    ExecuteMoveToTargetGoal(discreteActions);
                    break;
                    
                case GoalType.PickupItem:
                    ExecutePickupGoal(discreteActions, nearbyItems);
                    break;
                    
                case GoalType.UseItem:
                    ExecuteUseItemGoal(discreteActions);
                    break;
                    
                case GoalType.Interact:
                    ExecuteInteractGoal(discreteActions, nearbyNPCs);
                    break;
                    
                default:
                    // 探索行为
                    ExecuteExploreGoal(discreteActions);
                    break;
            }
        }
        
        private void ExecuteAttackGoal(ActionSegment<int> actions, List<Enemy.Enemy2D> enemies)
        {
            if (enemies == null || enemies.Count == 0) return;
            
            var target = enemies[0];
            Vector2 toTarget = (target.transform.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, target.transform.position);
            
            // 移动向目标
            MoveTowards(actions, toTarget);
            
            // 如果在攻击范围内
            if (distance <= 2.5f)
            {
                actions[4] = 1; // 攻击
                
                // 基于AI的攻击性特征，可能切换武器
                if (goalSystem.aggression > 0.7f && UnityEngine.Random.value < 0.15f)
                {
                    actions[4] = 2; // 切换武器
                }
            }
        }
        
        private void ExecuteMoveToTargetGoal(ActionSegment<int> actions)
        {
            // 基于中层目标确定目标位置
            Vector2? targetPos = null;
            
            switch (goalSystem.CurrentMidLevelGoal?.Type)
            {
                case GoalType.Combat:
                    var enemies = perception.GetNearbyEnemies();
                    if (enemies.Count > 0)
                    {
                        targetPos = enemies[0].transform.position;
                    }
                    break;
                    
                case GoalType.Trade:
                    var npcs = perception.GetNearbyNPCs();
                    if (npcs.Count > 0)
                    {
                        targetPos = npcs[0].transform.position;
                    }
                    else if (memory.KnowsPortalLocation())
                    {
                        targetPos = memory.GetPathToImportantLocation("Merchant");
                    }
                    break;
                    
                case GoalType.Exploration:
                    if (memory.KnowsPortalLocation() && goalSystem.CurrentHighLevelGoal?.Type == GoalType.Progression)
                    {
                        targetPos = memory.GetPathToImportantLocation("Portal");
                    }
                    else
                    {
                        // 探索未知区域
                        targetPos = currentTarget != Vector2.zero ? currentTarget : GetRandomExplorationTarget();
                    }
                    break;
            }
            
            if (targetPos.HasValue)
            {
                Vector2 direction = (targetPos.Value - (Vector2)transform.position).normalized;
                MoveTowards(actions, direction);
                currentTarget = targetPos.Value;
            }
        }
        
        private void ExecutePickupGoal(ActionSegment<int> actions, List<GameObject> items)
        {
            if (items == null || items.Count == 0) return;
            
            var target = items[0];
            Vector2 toTarget = (target.transform.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, target.transform.position);
            
            // 移动向物品
            MoveTowards(actions, toTarget);
            
            // 如果足够近，交互
            if (distance <= 2.5f)
            {
                actions[1] = 1; // 物品交互
            }
        }
        
        private void ExecuteUseItemGoal(ActionSegment<int> actions)
        {
            // 基于需求使用物品
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            // 简单策略：使用第一个可用物品槽
            for (int i = 0; i < inventory.Size && i < 10; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                {
                    var item = inventory.GetSlot(i).Item;
                    // 检查物品类型是否符合当前需求
                    if ((healthRatio < 0.5f && item.ItemName.Contains("Health")) ||
                        (hungerRatio < 0.5f && item.ItemName.Contains("Food")) ||
                        (thirstRatio < 0.5f && item.ItemName.Contains("Water")))
                    {
                        actions[2] = i + 1; // 使用物品槽
                        break;
                    }
                }
            }
        }
        
        private void ExecuteInteractGoal(ActionSegment<int> actions, List<NPC.Core.NPCBase> npcs)
        {
            // 检查是否需要面对面交流
            if (goalSystem.CurrentMidLevelGoal?.Type == GoalType.Communication)
            {
                // 寻找其他AI进行面对面交流
                var otherAIs = FindObjectsOfType<AICommunicator>()
                    .Where(c => c.gameObject != gameObject)
                    .OrderBy(c => Vector2.Distance(transform.position, c.transform.position))
                    .ToList();
                
                if (otherAIs.Count > 0 && Vector2.Distance(transform.position, otherAIs[0].transform.position) < 3f)
                {
                    actions[1] = 2; // 面对面交流
                    
                    // 面向对方
                    Vector2 toOther = (otherAIs[0].transform.position - transform.position).normalized;
                    MoveTowards(actions, toOther, false); // 不移动，只转向
                }
                else if (goalSystem.sociability > 0.5f && UnityEngine.Random.value < 0.05f)
                {
                    // 发送通信
                    actions[3] = UnityEngine.Random.Range(1, 7); // 随机消息类型
                }
            }
            else if (npcs != null && npcs.Count > 0)
            {
                // 与NPC交互
                var target = npcs[0];
                Vector2 toTarget = (target.transform.position - transform.position).normalized;
                float distance = Vector2.Distance(transform.position, target.transform.position);
                
                // 移动向NPC
                MoveTowards(actions, toTarget);
                
                // 如果足够近，交互
                if (distance <= 2.5f)
                {
                    actions[1] = 1; // 物品交互
                }
            }
        }
        
        private void ExecuteExploreGoal(ActionSegment<int> actions)
        {
            // 选择探索目标
            if (currentTarget == Vector2.zero || Vector2.Distance(transform.position, currentTarget) < 0.5f)
            {
                currentTarget = GetRandomExplorationTarget();
            }
            
            Vector2 direction = (currentTarget - (Vector2)transform.position).normalized;
            MoveTowards(actions, direction);
        }
        
        private void MoveTowards(ActionSegment<int> actions, Vector2 direction, bool actuallyMove = true)
        {
            if (!actuallyMove) return;
            
            // 转换方向为离散动作
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                actions[0] = direction.x > 0 ? 4 : 3; // 右或左
            }
            else
            {
                actions[0] = direction.y > 0 ? 1 : 2; // 上或下
            }
        }
        
        private Vector2 GetRandomExplorationTarget()
        {
            // 基于好奇心特征，选择探索范围
            float explorationRange = 8f * (0.5f + goalSystem.curiosity * 0.5f);
            return new Vector2(
                UnityEngine.Random.Range(-explorationRange, explorationRange),
                UnityEngine.Random.Range(-explorationRange, explorationRange)
            );
        }
        
        private bool HasKeyInInventory()
        {
            if (inventory == null) return false;
            
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && slot.Item.ItemName.Contains("Key"))
                {
                    return true;
                }
            }
            return false;
        }
        
        private void HeuristicFallback(in ActionBuffers actionsOut)
        {
            // 原始的简单启发式逻辑
            var discreteActions = actionsOut.DiscreteActions;
            var nearbyEnemies = perception.GetNearbyEnemies();
            var nearbyItems = perception.GetNearbyItems();
            var nearbyNPCs = perception.GetNearbyNPCs();
            
            // 优先级判断 - 增加生存需求检查
            bool needWater = aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.4f; // 口渴40%以下
            bool needFood = aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f; // 饥饿30%以下
            bool needHealing = aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f;
            bool criticalHealth = aiStats.CurrentHealth <= aiStats.Config.maxHealth * 0.2f;
            
            bool shouldFight = nearbyEnemies.Count > 0 && !criticalHealth && !needWater && !needFood; // 只有在状态良好时才战斗
            bool shouldFlee = nearbyEnemies.Count > 0 && (criticalHealth || needWater || needFood); // 状态不好时逃跑
            bool shouldPickup = nearbyItems.Count > 0 && !shouldFlee;
            
            // 0. 生存需求 - 最高优先级
            if (needWater || needFood || needHealing)
            {
                // 寻找泉水
                if (needWater)
                {
                    foreach (var item in nearbyItems)
                    {
                        if (item.name.Contains("Fountain") || item.GetComponent<Interactables.Fountain>() != null)
                        {
                            Vector2 toFountain = (item.transform.position - transform.position).normalized;
                            float distance = Vector2.Distance(transform.position, item.transform.position);
                            
                            Debug.Log($"[AIBrain] Heuristic: 口渴寻找泉水 {item.name}, 距离: {distance:F2}");
                            
                            // 移动向泉水
                            if (distance > 2f)
                            {
                                if (Mathf.Abs(toFountain.x) > Mathf.Abs(toFountain.y))
                                    discreteActions[0] = toFountain.x > 0 ? 4 : 3;
                                else
                                    discreteActions[0] = toFountain.y > 0 ? 1 : 2;
                            }
                            else
                            {
                                discreteActions[1] = 1; // 交互
                                Debug.Log($"[AIBrain] Heuristic: 使用泉水 {item.name}");
                            }
                            return;
                        }
                    }
                }
                
                // 寻找宝箱（可能有恢复物品）
                foreach (var item in nearbyItems)
                {
                    var chest = item.GetComponent<Interactables.TreasureChest>();
                    if (chest != null && !chest.IsOpened)
                    {
                        Vector2 toChest = (item.transform.position - transform.position).normalized;
                        float distance = Vector2.Distance(transform.position, item.transform.position);
                        
                        Debug.Log($"[AIBrain] Heuristic: 生存需求寻找宝箱, 距离: {distance:F2}");
                        
                        // 移动向宝箱
                        if (distance > 2f)
                        {
                            if (Mathf.Abs(toChest.x) > Mathf.Abs(toChest.y))
                                discreteActions[0] = toChest.x > 0 ? 4 : 3;
                            else
                                discreteActions[0] = toChest.y > 0 ? 1 : 2;
                        }
                        else
                        {
                            discreteActions[1] = 1; // 交互
                            Debug.Log($"[AIBrain] Heuristic: 开启宝箱寻找物资");
                        }
                        return;
                    }
                }
            }
            
            // 1. 战斗行为 - 次高优先级
            if (shouldFight)
            {
                var nearestEnemy = nearbyEnemies[0];
                Vector2 toEnemy = (nearestEnemy.transform.position - transform.position).normalized;
                float distance = Vector2.Distance(transform.position, nearestEnemy.transform.position);
                
                Debug.Log($"[AIBrain] Heuristic: 发现敌人 {nearestEnemy.name}, 距离: {distance:F2}, 生命值: {aiStats.CurrentHealth}/{aiStats.Config.maxHealth}");
                
                // 移动向敌人 - 更积极地接近
                if (distance > 1.5f) // 距离大于1.5时主动接近
                {
                    if (Mathf.Abs(toEnemy.x) > Mathf.Abs(toEnemy.y))
                    {
                        discreteActions[0] = toEnemy.x > 0 ? 4 : 3;
                    }
                    else
                    {
                        discreteActions[0] = toEnemy.y > 0 ? 1 : 2;
                    }
                }
                
                // 攻击决策 - 提高攻击范围和积极性
                if (distance <= 3.5f) // 从2.5增加到3.5，更大攻击范围
                {
                    discreteActions[4] = 1; // 攻击
                    Debug.Log($"[AIBrain] Heuristic: 执行攻击 {nearestEnemy.name}, 距离: {distance:F2}");
                }
                else if (distance <= 5.0f && UnityEngine.Random.value < 0.25f) // 中距离时有25%概率切换武器
                {
                    discreteActions[4] = 2; // 切换武器
                    Debug.Log($"[AIBrain] Heuristic: 切换武器准备攻击 {nearestEnemy.name}");
                }
                
                // 战斗时减少其他行为，专注战斗
                return; // 直接返回，不执行其他行为
            }
            // 2. 逃跑行为
            else if (shouldFlee)
            {
                var nearestEnemy = nearbyEnemies[0];
                Vector2 awayFromEnemy = (transform.position - nearestEnemy.transform.position).normalized;
                
                // 远离敌人
                if (Mathf.Abs(awayFromEnemy.x) > Mathf.Abs(awayFromEnemy.y))
                {
                    discreteActions[0] = awayFromEnemy.x > 0 ? 4 : 3;
                }
                else
                {
                    discreteActions[0] = awayFromEnemy.y > 0 ? 1 : 2;
                }
                
                // 使用恢复物品
                if (needHealing)
                {
                    discreteActions[2] = 1; // 使用第一个物品槽
                }
            }
            // 3. 拾取物品 - 智能优先级拾取
            else if (shouldPickup)
            {
                // 按优先级排序物品：宝箱 > 钥匙 > 武器 > 恢复物品 > 其他
                GameObject priorityItem = null;
                float priorityScore = 0f;
                
                foreach (var item in nearbyItems)
                {
                    float score = 0f;
                    
                    // 宝箱优先级最高
                    var chest = item.GetComponent<Interactables.TreasureChest>();
                    if (chest != null && !chest.IsOpened)
                    {
                        score = 100f;
                        if (chest.IsLocked) score = 95f; // 锁着的宝箱稍微低一点
                    }
                    
                    // 拾取物品
                    var pickup = item.GetComponent<Loot.UnifiedPickup>();
                    if (pickup != null && pickup.PickupItem != null)
                    {
                        var itemData = pickup.PickupItem;
                        
                        // 钥匙高优先级
                        if (itemData.ItemName.Contains("Key") || itemData.ItemName.Contains("钥匙"))
                            score = 90f;
                        // 武器
                        else if (itemData is WeaponItem)
                            score = 70f;
                        // 恢复物品根据需求
                        else if (itemData.ItemName.Contains("Health") || itemData.ItemName.Contains("Food") || itemData.ItemName.Contains("Water"))
                        {
                            if (needHealing || needFood || needWater) score = 80f;
                            else score = 50f;
                        }
                        // 金币和弹药
                        else if (itemData.ItemName.Contains("Gold") || itemData.ItemName.Contains("Ammo"))
                            score = 60f;
                        else
                            score = 30f;
                    }
                    
                    if (score > priorityScore)
                    {
                        priorityScore = score;
                        priorityItem = item;
                    }
                }
                
                if (priorityItem != null)
                {
                    Vector2 toItem = (priorityItem.transform.position - transform.position).normalized;
                    float distance = Vector2.Distance(transform.position, priorityItem.transform.position);
                    
                    Debug.Log($"[AIBrain] Heuristic: 优先拾取 {priorityItem.name}, 距离: {distance:F2}, 优先级: {priorityScore}");
                    
                    // 移动向物品
                    if (distance > 2f)
                    {
                        if (Mathf.Abs(toItem.x) > Mathf.Abs(toItem.y))
                            discreteActions[0] = toItem.x > 0 ? 4 : 3;
                        else
                            discreteActions[0] = toItem.y > 0 ? 1 : 2;
                    }
                    else
                    {
                        discreteActions[1] = 1; // 物品交互
                        Debug.Log($"[AIBrain] Heuristic: 拾取/开启 {priorityItem.name}");
                    }
                }
            }
            // 4. 与NPC交互
            else if (nearbyNPCs.Count > 0 && (needHealing || aiStats.CurrentHunger < 50f || aiStats.CurrentThirst < 50f))
            {
                var nearestNPC = nearbyNPCs[0];
                Vector2 toNPC = (nearestNPC.transform.position - transform.position).normalized;
                
                // 移动向NPC
                if (Mathf.Abs(toNPC.x) > Mathf.Abs(toNPC.y))
                {
                    discreteActions[0] = toNPC.x > 0 ? 4 : 3;
                }
                else
                {
                    discreteActions[0] = toNPC.y > 0 ? 1 : 2;
                }
                
                // 靠近时交互
                float distance = Vector2.Distance(transform.position, nearestNPC.transform.position);
                if (distance <= 2.5f)
                {
                    discreteActions[1] = 1; // 物品交互
                    Debug.Log($"[AIBrain] Heuristic: 与NPC交互 {nearestNPC.NPCType}");
                }
            }
            // 5. 随机探索
            else
            {
                if (currentTarget == Vector2.zero || Vector2.Distance(transform.position, currentTarget) < 0.5f)
                {
                    // 选择新目标
                    currentTarget = new Vector2(
                        UnityEngine.Random.Range(-8f, 8f),
                        UnityEngine.Random.Range(-8f, 8f)
                    );
                }
                
                // 向目标移动
                Vector2 direction = (currentTarget - (Vector2)transform.position).normalized;
                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                {
                    discreteActions[0] = direction.x > 0 ? 4 : 3;
                }
                else
                {
                    discreteActions[0] = direction.y > 0 ? 1 : 2;
                }
            }
            
            // 通信行为
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.2f && UnityEngine.Random.value < 0.01f)
            {
                discreteActions[3] = 1; // 发送求救信号
            }
        }
        
        private void Update()
        {
            // 检查是否已经死亡
            if (aiStats != null && aiStats.IsDead)
            {
                // AI已死亡，停止所有决策
                return;
            }
            
            // 定期决策
            if (Time.time >= nextDecisionTime)
            {
                nextDecisionTime = Time.time + decisionInterval;
                
                // 调试：显示感知状态
                if (perception != null)
                {
                    var enemies = perception.GetNearbyEnemies();
                    var items = perception.GetNearbyItems();
                    var npcs = perception.GetNearbyNPCs();
                    
                    if (enemies.Count > 0 || items.Count > 0 || npcs.Count > 0)
                    {
                        Debug.Log($"[{name}] 感知状态 - 敌人:{enemies.Count}, 物品:{items.Count}, NPC:{npcs.Count}, 生命值:{aiStats.CurrentHealth:F1}/{aiStats.Config.maxHealth}");
                    }
                }
                
                // 检查是否需要调用DeepSeek API
                if (useDeepSeekAPI && ShouldUseDeepSeek())
                {
                    RequestDeepSeekDecision();
                }
                
                // 注意：不要手动调用RequestDecision()！
                // DecisionRequester组件会自动处理ML-Agents的决策请求
                // 手动调用会导致冲突和卡死
            }
            
            // 自动检查状态是否需要调整（避免卡在Critical状态）
            if (currentState == AIState.Critical)
            {
                float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
                float hungerPercent = aiStats.CurrentHunger / aiStats.Config.maxHunger;
                float thirstPercent = aiStats.CurrentThirst / aiStats.Config.maxThirst;
                
                // 如果状态已经恢复，退出Critical状态
                if (healthPercent > 0.5f && hungerPercent > 0.4f && thirstPercent > 0.4f)
                {
                    Debug.Log($"[AIBrain] 状态已恢复，退出Critical状态");
                    currentState = AIState.Exploring;
                }
            }
            
            // 更新记忆
            memory.Update(perception);
        }
        
        private bool ShouldUseDeepSeek()
        {
            // 如果AI已经死亡，不请求DeepSeek决策
            if (aiStats != null && aiStats.IsDead)
            {
                return false;
            }
            
            // 初始决策优先 - 游戏开始后进行一次决策
            if (!hasInitialDecision && Time.time > initialDecisionDelay)
            {
                // 检查AI在场景中的数量
                var allAIs = FindObjectsOfType<AIStats>();
                Debug.Log($"[AIBrain] 触发初始DeepSeek决策 - 场景中AI数量: {allAIs.Length}");
                hasInitialDecision = true;
                lastDeepSeekTime = Time.time;
                return true;
            }
            
            // 检查冷却时间（初始决策后才检查）
            if (Time.time - lastDeepSeekTime < deepSeekCooldown)
            {
                return false;
            }
            
            // 关键时刻使用DeepSeek API
            float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerPercent = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstPercent = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            // 事件触发的决策
            var nearbyEnemies = perception.GetNearbyEnemies();
            var nearbyNPCs = perception.GetNearbyNPCs();
            
            // 综合评估是否需要DeepSeek决策
            bool isInDanger = IsInRealDanger(healthPercent, hungerPercent, thirstPercent, nearbyEnemies);
            bool needsSocialHelp = (nearbyNPCs.Count > 0 && aiStats.GetMood(MoodDimension.Social) < -30f);
            bool severelyDepressed = (aiStats.GetMood(MoodDimension.Emotion) < -70f);
            bool periodicDecision = (Time.time - lastDeepSeekTime > deepSeekCooldown * 10); // 延长定期决策间隔到200秒
            
            // 如果状态很好且没有特殊情况，不需要DeepSeek
            bool isInGoodCondition = healthPercent > 0.8f && hungerPercent > 0.8f && thirstPercent > 0.8f && 
                                   nearbyEnemies.Count == 0 && aiStats.GetMood(MoodDimension.Emotion) > -30f;
            
            if (isInGoodCondition && !needsSocialHelp && !periodicDecision)
            {
                return false; // 状态良好时不需要频繁决策
            }
            
            bool shouldUse = 
                // 生存危机
                healthPercent < deepSeekThreshold || 
                hungerPercent < deepSeekThreshold ||
                thirstPercent < deepSeekThreshold ||
                // currentState == AIState.Critical || // 移除这个检查，避免循环触发
                // 真正的危险情况
                isInDanger ||
                // 社交机会
                needsSocialHelp ||
                // 心情非常糟糕
                severelyDepressed ||
                // 定期决策（降低频率）
                periodicDecision;
            
            if (shouldUse)
            {
                Debug.Log($"[AIBrain] 触发DeepSeek决策 - 健康:{healthPercent:P0}, 饥饿:{hungerPercent:P0}, 口渴:{thirstPercent:P0}, 敌人:{nearbyEnemies.Count}, NPC:{nearbyNPCs.Count}");
                lastDeepSeekTime = Time.time;
            }
            
            return shouldUse;
        }
        
        /// <summary>
        /// 判断AI是否处于真正的危险中（综合考虑多个因素）
        /// </summary>
        private bool IsInRealDanger(float healthPercent, float hungerPercent, float thirstPercent, List<Enemy.Enemy2D> nearbyEnemies)
        {
            if (nearbyEnemies.Count == 0) return false;
            
            // 评估AI的战斗能力
            bool hasWeapon = inventory.EquippedWeapon != null;
            bool hasAmmo = true; // 默认近战武器不需要弹药
            if (hasWeapon && inventory.EquippedWeapon.RequiredAmmo != AmmoType.None)
            {
                var ammoManager = GetComponent<Inventory.Managers.AmmoManager>();
                hasAmmo = ammoManager != null && ammoManager.GetAmmo(inventory.EquippedWeapon.RequiredAmmo) > 0;
            }
            
            // 评估AI的状态
            bool lowHealth = healthPercent < 0.4f;
            bool lowStamina = aiStats.CurrentStamina < aiStats.Config.maxStamina * 0.3f; // 体力不足难以逃跑
            bool criticalState = healthPercent < 0.2f || hungerPercent < 0.15f || thirstPercent < 0.15f;
            
            // 评估敌人威胁
            int closeEnemies = 0; // 近距离敌人（真正的威胁）
            foreach (var enemy in nearbyEnemies)
            {
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < 5f) // 5单位内算近距离威胁
                {
                    closeEnemies++;
                }
            }
            
            // 综合判断真正的危险情况
            bool shouldSeekHelp = false;
            
            // 1. 无武器且被围攻
            if (!hasWeapon && closeEnemies >= 2)
            {
                shouldSeekHelp = true;
            }
            // 2. 有武器但没弹药，且敌人很多
            else if (hasWeapon && !hasAmmo && closeEnemies >= 3)
            {
                shouldSeekHelp = true;
            }
            // 3. 生命很低且被多个敌人围攻
            else if (lowHealth && closeEnemies >= 2)
            {
                shouldSeekHelp = true;
            }
            // 4. 体力不足无法逃跑且面临多个敌人
            else if (lowStamina && closeEnemies >= 3)
            {
                shouldSeekHelp = true;
            }
            // 5. 生存状态危急且有敌人威胁
            else if (criticalState && closeEnemies >= 1)
            {
                shouldSeekHelp = true;
            }
            
            if (shouldSeekHelp)
            {
                Debug.Log($"[AIBrain] 检测到真正危险 - 武器:{hasWeapon}, 弹药:{hasAmmo}, 近敌:{closeEnemies}, 生命:{healthPercent:P0}, 体力:{lowStamina}");
            }
            
            return shouldSeekHelp;
        }
        
        private void RequestDeepSeekDecision()
        {
            // 在请求前再次检查是否死亡
            if (aiStats != null && aiStats.IsDead)
            {
                Debug.Log("[AIBrain] 取消DeepSeek请求 - AI已死亡");
                return;
            }
            
            if (deepSeekDecisionMaker != null)
            {
                var context = GatherDecisionContext();
                deepSeekDecisionMaker.RequestDecision(context, OnDeepSeekDecisionReceived);
            }
        }
        
        private AIDecisionContext GatherDecisionContext()
        {
            // 检查必要组件是否存在
            if (aiStats == null || aiStats.IsDead)
            {
                Debug.LogWarning("[AIBrain] 无法收集决策上下文 - AI已死亡或组件缺失");
                return null;
            }
            
            return new AIDecisionContext
            {
                Stats = aiStats,
                Inventory = inventory,
                VisibleRooms = perception.GetVisibleRooms(),
                NearbyEnemies = perception.GetNearbyEnemies(),
                NearbyNPCs = perception.GetNearbyNPCs(),
                NearbyItems = perception.GetNearbyItems(),
                Memory = memory.GetAllMemories()
            };
        }
        
        private void OnDeepSeekDecisionReceived(AIDecision decision)
        {
            // 在收到响应时检查是否已经死亡
            if (aiStats != null && aiStats.IsDead)
            {
                Debug.Log("[AIBrain] 忽略DeepSeek响应 - AI已死亡");
                return;
            }
            
            // 应用DeepSeek的决策建议
            if (decision != null)
            {
                // 只在状态真正改变时更新
                if (currentState != decision.RecommendedState)
                {
                    Debug.Log($"[AIBrain] 状态改变: {currentState} -> {decision.RecommendedState}");
                    currentState = decision.RecommendedState;
                }
                
                controller.SetPriority(decision.Priority);
                
                // 让AIController执行具体的决策行动
                controller.ApplyDeepSeekDecision(decision);
                
                Debug.Log($"[AIBrain] DeepSeek建议: {decision.Explanation}");
                
                // 记录决策到内存
                if (memory != null)
                {
                    memory.RecordEvent("DeepSeekDecision", decision.RecommendedState.ToString());
                }
            }
        }
    }
    
    public enum AIState
    {
        Exploring,      // 探索
        Fighting,       // 战斗
        Fleeing,       // 逃跑
        Seeking,       // 寻找资源
        Interacting,   // 交互
        Communicating, // 通信
        Resting,       // 休息
        Critical       // 危急状态
    }
    
    public enum CommunicationType
    {
        Help,          // 求救
        ComeHere,      // 到我这来
        GoingTo,       // 我将要到
        FoundWater,    // 找到水源
        FoundPortal,   // 找到传送门
        FoundNPC       // 找到NPC
    }
}