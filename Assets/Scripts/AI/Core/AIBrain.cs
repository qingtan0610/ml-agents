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
        [SerializeField] private bool useDeepSeekAPI = true;     // 默认启用DeepSeek
        [SerializeField] private float deepSeekThreshold = 0.3f; // 当AI健康度低于此值时调用DeepSeek
        [SerializeField] private float deepSeekCooldown = 10f;   // DeepSeek调用冷却时间
        [SerializeField] private float initialDecisionDelay = 5f; // 初始决策延迟 - 增加到5秒确保场景完全初始化
        private float lastDeepSeekTime = -10f;
        private bool hasInitialDecision = false;
        
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
            hasInitialDecision = false;
            lastDeepSeekTime = Time.time - deepSeekCooldown; // 允许立即使用DeepSeek
            
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
            // 检查是否已经死亡
            if (aiStats != null && aiStats.IsDead)
            {
                // AI已死亡，不执行任何动作
                return;
            }
            
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
            // 用于测试的启发式控制 - 增强版
            var discreteActions = actionsOut.DiscreteActions;
            
            // 获取周围情况
            var nearbyEnemies = perception.GetNearbyEnemies();
            var nearbyItems = perception.GetNearbyItems();
            var nearbyNPCs = perception.GetNearbyNPCs();
            
            // 优先级判断
            bool shouldFight = nearbyEnemies.Count > 0 && aiStats.CurrentHealth > aiStats.Config.maxHealth * 0.3f;
            bool shouldFlee = nearbyEnemies.Count > 0 && aiStats.CurrentHealth <= aiStats.Config.maxHealth * 0.3f;
            bool shouldPickup = nearbyItems.Count > 0 && !shouldFlee;
            bool needHealing = aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f;
            
            // 1. 战斗行为
            if (shouldFight)
            {
                var nearestEnemy = nearbyEnemies[0];
                Vector2 toEnemy = (nearestEnemy.transform.position - transform.position).normalized;
                
                // 移动向敌人
                if (Mathf.Abs(toEnemy.x) > Mathf.Abs(toEnemy.y))
                {
                    discreteActions[0] = toEnemy.x > 0 ? 4 : 3;
                }
                else
                {
                    discreteActions[0] = toEnemy.y > 0 ? 1 : 2;
                }
                
                // 攻击决策
                float distance = Vector2.Distance(transform.position, nearestEnemy.transform.position);
                if (distance <= 2f) // 攻击范围内
                {
                    discreteActions[4] = 1; // 攻击
                    Debug.Log($"[AIBrain] Heuristic: 攻击敌人 {nearestEnemy.name}");
                }
                else if (UnityEngine.Random.value < 0.1f) // 10%概率切换武器
                {
                    discreteActions[4] = 2; // 切换武器
                }
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
            // 3. 拾取物品
            else if (shouldPickup)
            {
                var nearestItem = nearbyItems[0];
                Vector2 toItem = (nearestItem.transform.position - transform.position).normalized;
                
                // 移动向物品
                if (Mathf.Abs(toItem.x) > Mathf.Abs(toItem.y))
                {
                    discreteActions[0] = toItem.x > 0 ? 4 : 3;
                }
                else
                {
                    discreteActions[0] = toItem.y > 0 ? 1 : 2;
                }
                
                // 靠近时交互
                float distance = Vector2.Distance(transform.position, nearestItem.transform.position);
                if (distance <= 2.5f)
                {
                    discreteActions[1] = 1; // 物品交互
                    Debug.Log($"[AIBrain] Heuristic: 尝试拾取物品");
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
                
                // 检查是否需要调用DeepSeek API
                if (useDeepSeekAPI && ShouldUseDeepSeek())
                {
                    RequestDeepSeekDecision();
                }
                
                // 请求ML-Agents决策
                RequestDecision();
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
            bool periodicDecision = (Time.time - lastDeepSeekTime > deepSeekCooldown * 5); // 延长定期决策间隔
            
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