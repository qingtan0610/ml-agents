using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;
using AI.Stats;
using AI.Perception;
using AI.Decision;
using Inventory;
using Inventory.Items;

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
        
        [Header("Decision Settings")]
        [SerializeField] private float decisionInterval = 0.5f;
        [SerializeField] private bool useDeepSeekAPI = false;
        [SerializeField] private float deepSeekThreshold = 0.3f; // 当AI健康度低于此值时调用DeepSeek
        [SerializeField] private float deepSeekCooldown = 10f;   // DeepSeek调用冷却时间
        private float lastDeepSeekTime = -10f;
        
        [Header("Current State")]
        [SerializeField] private AIState currentState = AIState.Exploring;
        [SerializeField] private Vector2 currentTarget;
        [SerializeField] private GameObject currentInteractable;
        
        // 决策系统
        private float nextDecisionTime = 0f;
        private AIMemory memory;
        private DeepSeekDecisionMaker deepSeekDecisionMaker;
        
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
            
            // 重置位置到出生点
            controller.ResetToSpawn();
        }
        
        public override void CollectObservations(VectorSensor sensor)
        {
            // 1. AI自身状态 (7个值)
            sensor.AddObservation(aiStats.CurrentHealth / aiStats.Config.maxHealth);
            sensor.AddObservation(aiStats.CurrentHunger / aiStats.Config.maxHunger);
            sensor.AddObservation(aiStats.CurrentThirst / aiStats.Config.maxThirst);
            sensor.AddObservation(aiStats.CurrentStamina / aiStats.Config.maxStamina);
            sensor.AddObservation(aiStats.GetMood(MoodDimension.Emotion) / 100f);
            sensor.AddObservation(aiStats.GetMood(MoodDimension.Social) / 100f);
            sensor.AddObservation(aiStats.GetMood(MoodDimension.Mentality) / 100f);
            
            // 2. 背包状态 (3个值)
            int itemCount = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    itemCount++;
            }
            sensor.AddObservation(itemCount / (float)inventory.Size); // 背包占用率
            sensor.AddObservation(inventory.EquippedWeapon != null ? 1f : 0f); // 是否装备武器
            sensor.AddObservation(GetComponent<Inventory.Managers.CurrencyManager>()?.CurrentGold ?? 0 / 1000f); // 金币数量归一化
            
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
            
            // 4. 记忆信息
            var importantLocations = memory.GetImportantLocations();
            sensor.AddObservation(importantLocations.ContainsKey("Portal") ? 1f : 0f);
            sensor.AddObservation(importantLocations.ContainsKey("Fountain") ? 1f : 0f);
            sensor.AddObservation(importantLocations.ContainsKey("Merchant") ? 1f : 0f);
        }
        
        public override void OnActionReceived(ActionBuffers actions)
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
                if (nearbyEnemies.Count > 0)
                {
                    var target = nearbyEnemies[0]; // 选择最近的敌人
                    
                    if (combatAction == 1)
                    {
                        // 攻击
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
        
        private void CalculateRewards()
        {
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
            // 用于测试的启发式控制
            var discreteActions = actionsOut.DiscreteActions;
            
            // 简单的探索策略
            if (currentTarget == Vector2.zero || Vector2.Distance(transform.position, currentTarget) < 0.5f)
            {
                // 选择新目标
                currentTarget = new Vector2(
                    Random.Range(-8f, 8f),
                    Random.Range(-8f, 8f)
                );
            }
            
            // 向目标移动
            Vector2 direction = (currentTarget - (Vector2)transform.position).normalized;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                discreteActions[0] = direction.x > 0 ? 4 : 3; // 右或左
            }
            else
            {
                discreteActions[0] = direction.y > 0 ? 1 : 2; // 上或下
            }
            
            // 低生命值时使用物品
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.3f)
            {
                discreteActions[2] = 1; // 尝试使用第一个物品槽
            }
        }
        
        private void Update()
        {
            // 定期决策
            if (Time.time >= nextDecisionTime)
            {
                nextDecisionTime = Time.time + decisionInterval;
                
                // 检查是否需要调用DeepSeek API
                if (useDeepSeekAPI && ShouldUseDeepSeek())
                {
                    RequestDeepSeekDecision();
                }
                
                // 请求ML-Agents决策
                RequestDecision();
            }
            
            // 更新记忆
            memory.Update(perception);
        }
        
        private bool ShouldUseDeepSeek()
        {
            // 检查冷却时间
            if (Time.time - lastDeepSeekTime < deepSeekCooldown)
                return false;
            
            // 在关键时刻使用DeepSeek API
            float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float resourcePercent = (aiStats.CurrentHunger + aiStats.CurrentThirst) / 
                                  (aiStats.Config.maxHunger + aiStats.Config.maxThirst);
            
            bool shouldUse = healthPercent < deepSeekThreshold || 
                            resourcePercent < deepSeekThreshold ||
                            currentState == AIState.Critical ||
                            perception.GetNearbyEnemies().Count >= 3;  // 被多个敌人包围
            
            if (shouldUse)
            {
                lastDeepSeekTime = Time.time;
            }
            
            return shouldUse;
        }
        
        private void RequestDeepSeekDecision()
        {
            if (deepSeekDecisionMaker != null)
            {
                var context = GatherDecisionContext();
                deepSeekDecisionMaker.RequestDecision(context, OnDeepSeekDecisionReceived);
            }
        }
        
        private AIDecisionContext GatherDecisionContext()
        {
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
            // 应用DeepSeek的决策建议
            if (decision != null)
            {
                currentState = decision.RecommendedState;
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