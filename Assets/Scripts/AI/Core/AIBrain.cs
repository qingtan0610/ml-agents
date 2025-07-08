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
        private AIBehaviorAnalyzer behaviorAnalyzer;
        private AIContextualMemory contextualMemory;
        private AIDecision lastDecision; // 存储最后的DeepSeek决策
        
        // ML-Agents观察空间
        private const int GRID_SIZE = 16; // 房间网格大小
        private const int VISION_RANGE = 1; // 视野范围（房间数）
        
        // 动作记录
        private int[] lastActions = new int[5];
        
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
            
            // 获取或添加行为分析器
            behaviorAnalyzer = GetComponent<AIBehaviorAnalyzer>();
            if (behaviorAnalyzer == null)
            {
                behaviorAnalyzer = gameObject.AddComponent<AIBehaviorAnalyzer>();
                Debug.Log($"[AIBrain] 为 {name} 添加了AIBehaviorAnalyzer");
            }
            
            // 获取或添加情境记忆
            contextualMemory = GetComponent<AIContextualMemory>();
            if (contextualMemory == null)
            {
                contextualMemory = gameObject.AddComponent<AIContextualMemory>();
                Debug.Log($"[AIBrain] 为 {name} 添加了AIContextualMemory");
            }
            
            // 获取或添加交易管理器
            var tradeManager = GetComponent<AITradeManager>();
            if (tradeManager == null)
            {
                tradeManager = gameObject.AddComponent<AITradeManager>();
                Debug.Log($"[AIBrain] 为 {name} 添加了AITradeManager");
            }
            
            // 获取或添加背包管理器
            var inventoryManager = GetComponent<AIInventoryManager>();
            if (inventoryManager == null)
            {
                inventoryManager = gameObject.AddComponent<AIInventoryManager>();
                Debug.Log($"[AIBrain] 为 {name} 添加了AIInventoryManager");
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
        
        private void Start()
        {
            // 连接通信系统到记忆系统
            var communicator = GetComponent<AICommunicator>();
            if (communicator != null && memory != null)
            {
                communicator.OnMessageReceived += OnCommunicationReceived;
                Debug.Log($"[AIBrain] {name} 连接通信系统到记忆系统");
            }
        }
        
        private void OnDestroy()
        {
            // 清理事件订阅
            var communicator = GetComponent<AICommunicator>();
            if (communicator != null)
            {
                communicator.OnMessageReceived -= OnCommunicationReceived;
            }
        }
        
        /// <summary>
        /// 处理收到的通信消息
        /// </summary>
        private void OnCommunicationReceived(CommunicationMessage message)
        {
            if (message == null || message.Sender == null) return;
            
            // 记录到记忆系统
            memory.RecordAICommunication(message.Sender.name, message.Position, message.Type);
            
            Debug.Log($"[AIBrain] {name} 收到来自 {message.Sender.name} 的消息: {message.Type} at {message.Position}");
            
            // 特殊处理不同类型的消息
            switch (message.Type)
            {
                case CommunicationType.Help:
                    // 队友求救，记录危险位置
                    memory.RecordDangerZone(message.Position, 20f);
                    break;
                    
                case CommunicationType.ComeHere:
                    // 请求过来（可能是交易请求）
                    // 交易管理器会独立处理
                    break;
                    
                case CommunicationType.FoundPortal:
                    // 发现传送门，立即记录
                    memory.RecordPortalLocation(message.Position);
                    Debug.Log($"[AIBrain] {name} 得知传送门位置: {message.Position}");
                    break;
                    
                case CommunicationType.FoundWater:
                    // 发现水源
                    memory.RecordResourceLocation("Water", message.Position);
                    break;
                    
                case CommunicationType.FoundNPC:
                    // 发现NPC
                    memory.RecordNPCLocation("Unknown", message.Position);
                    break;
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
                // 添加默认观察值: 5(状态) + 7(背包) + 27(房间) + 15(敌人) + 12(NPC) + 20(物品) + 8(墙壁) + 8(记忆) + 12(其他AI) = 114
                for (int i = 0; i < 114; i++)
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
                if (i < nearbyEnemies.Count && nearbyEnemies[i] != null)
                {
                    var enemy = nearbyEnemies[i];
                    if (enemy != null && enemy.gameObject != null)
                    {
                        Vector2 relativePos = (Vector2)enemy.transform.position - (Vector2)transform.position;
                        sensor.AddObservation(relativePos.x / 10f);
                        sensor.AddObservation(relativePos.y / 10f);
                        sensor.AddObservation(enemy.CurrentHealth / enemy.MaxHealth);
                    }
                    else
                    {
                        // 敌人已被销毁，添加默认值
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                    }
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }
            
            // 编码NPC信息 (最多3个)
            for (int i = 0; i < 3; i++)
            {
                if (i < nearbyNPCs.Count && nearbyNPCs[i] != null)
                {
                    var npc = nearbyNPCs[i];
                    if (npc != null && npc.gameObject != null)
                    {
                        Vector2 relativePos = (Vector2)npc.transform.position - (Vector2)transform.position;
                        sensor.AddObservation(relativePos.x / 10f);
                        sensor.AddObservation(relativePos.y / 10f);
                        sensor.AddObservation((float)npc.NPCType / 10f); // NPC类型
                        sensor.AddObservation(npc.CanInteract(gameObject) ? 1f : 0f); // 是否可交互
                    }
                    else
                    {
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
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
            
            // 编码物品/交互物信息 (最多5个)
            for (int i = 0; i < 5; i++)
            {
                if (i < nearbyItems.Count && nearbyItems[i] != null)
                {
                    var item = nearbyItems[i];
                    if (item != null)
                    {
                        Vector2 relativePos = (Vector2)item.transform.position - (Vector2)transform.position;
                        sensor.AddObservation(relativePos.x / 10f);
                        sensor.AddObservation(relativePos.y / 10f);
                        
                        // 物品类型识别
                        float itemType = 0f;
                        float itemState = 0f;
                        
                        var chest = item.GetComponent<Interactables.TreasureChest>();
                        var fountain = item.GetComponent<Interactables.Fountain>();
                        var pickup = item.GetComponent<Loot.UnifiedPickup>();
                        
                        if (chest != null)
                        {
                            itemType = 1f; // 宝箱
                            itemState = chest.IsOpened ? 0f : (chest.IsLocked ? 0.5f : 1f); // 已开/锁着/可开
                        }
                        else if (fountain != null)
                        {
                            itemType = 2f; // 泉水
                            itemState = 1f; // 泉水总是可用（内部有冷却逻辑）
                        }
                        else if (pickup != null)
                        {
                            itemType = 3f; // 掉落物
                            itemState = 1f; // 总是可拾取
                        }
                        else if (item.name.Contains("Portal") || item.name.Contains("Teleport"))
                        {
                            itemType = 4f; // 传送门
                            itemState = 1f;
                        }
                        
                        sensor.AddObservation(itemType / 10f);
                        sensor.AddObservation(itemState);
                    }
                    else
                    {
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
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
            
            // 墙壁检测信息 (8方向) - 让AI感知周围的墙壁
            float[] wallDistances = new float[8];
            Vector2[] directions = new Vector2[]
            {
                Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
                new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized
            };
            
            int wallLayer = 1 << 9; // Layer 9 是 Wall
            for (int i = 0; i < 8; i++)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, directions[i], 5f, wallLayer);
                float distance = hit.collider != null ? hit.distance : 5f;
                sensor.AddObservation(distance / 5f); // 归一化到0-1
            }
            
                // 记忆信息 (8个值) - 扩展重要位置记忆
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
                for (int i = 0; i < 114; i++)
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
                
                // 保存原始动作用于记录
                lastActions[0] = moveAction;
                lastActions[1] = interactAction;
                lastActions[2] = itemAction;
                lastActions[3] = communicateAction;
                lastActions[4] = combatAction;
                
                // 获取情境建议
                if (contextualMemory != null)
                {
                    var contextSuggestion = contextualMemory.GetContextSuggestion();
                    
                    // 如果有推荐动作且置信度高
                    if (contextSuggestion.recommendedActions != null && contextSuggestion.confidence > 0.7f)
                    {
                        // 有一定概率采用推荐动作
                        if (UnityEngine.Random.value < contextSuggestion.confidence * 0.3f)
                        {
                            moveAction = contextSuggestion.recommendedActions[0];
                            interactAction = contextSuggestion.recommendedActions[1];
                            itemAction = contextSuggestion.recommendedActions[2];
                            communicateAction = contextSuggestion.recommendedActions[3];
                            combatAction = contextSuggestion.recommendedActions[4];
                            Debug.Log($"[AIBrain] {name} 采用情境记忆推荐动作，置信度: {contextSuggestion.confidence}");
                        }
                    }
                    
                    // 如果有应该避免的动作
                    if (contextSuggestion.avoidActions != null && contextSuggestion.avoidanceStrength > 0.5f)
                    {
                        // 检查当前动作是否在避免列表中
                        bool shouldAvoid = moveAction == contextSuggestion.avoidActions[0] ||
                                         interactAction == contextSuggestion.avoidActions[1] ||
                                         itemAction == contextSuggestion.avoidActions[2];
                        
                        if (shouldAvoid && UnityEngine.Random.value < contextSuggestion.avoidanceStrength)
                        {
                            // 随机改变动作
                            moveAction = UnityEngine.Random.Range(0, 5);
                            Debug.Log($"[AIBrain] {name} 避免危险动作，强度: {contextSuggestion.avoidanceStrength}");
                        }
                    }
                    
                    // 如果建议尝试新策略
                    if (contextSuggestion.shouldTryNewStrategy)
                    {
                        AddReward(contextSuggestion.explorationBonus);
                    }
                }
                
                // 记录动作用于行为分析
                if (behaviorAnalyzer != null)
                {
                    behaviorAnalyzer.RecordAction(actions.DiscreteActions.Array);
                    
                    // 获取行为建议
                    var suggestion = behaviorAnalyzer.GetActionSuggestion();
                    
                    // 如果AI卡住了，强制添加随机性 - 移除概率判断，确保立即执行
                    if (suggestion.shouldRandomMove)
                    {
                        moveAction = UnityEngine.Random.Range(1, 5); // 强制随机移动
                        interactAction = 0; // 清除其他动作，专注于移动
                        Debug.Log($"[AIBrain] {name} 检测到卡住，强制随机移动到方向 {moveAction}");
                    }
                    
                    // 如果在重复模式，强制添加动作噪声 - 移除概率判断
                    if (suggestion.shouldAddNoise)
                    {
                        // 50%概率改变移动，50%概率改变其他动作
                        if (UnityEngine.Random.value < 0.5f)
                        {
                            // 强制改变移动方向
                            moveAction = UnityEngine.Random.Range(1, 5);
                            Debug.Log($"[AIBrain] {name} 强制改变移动方向打破重复模式");
                        }
                        else
                        {
                            // 随机改变一个其他动作
                            int randomActionIndex = UnityEngine.Random.Range(1, 5); // 跳过移动
                            switch (randomActionIndex)
                            {
                                case 1: interactAction = UnityEngine.Random.Range(0, 3); break;
                                case 2: itemAction = UnityEngine.Random.Range(0, 11); break;
                                case 3: communicateAction = UnityEngine.Random.Range(0, 7); break;
                                case 4: combatAction = UnityEngine.Random.Range(0, 3); break;
                            }
                            Debug.Log($"[AIBrain] {name} 添加行为噪声打破重复模式");
                        }
                    }
                }
            
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
                    if (target != null && target.gameObject != null)
                    {
                        Debug.Log($"[AIBrain] {name} 目标敌人: {target.name} 距离: {Vector2.Distance(transform.position, target.transform.position)}");
                        
                        if (combatAction == 1)
                        {
                            // 攻击
                            Debug.Log($"[AIBrain] {name} 执行攻击");
                            controller.Attack(target.gameObject);
                        
                            // 如果目标超出攻击范围，让模型决定是否追击
                            if (controller.ChaseTarget != null)
                            {
                                // 模型需要通过移动行动来追击
                                Debug.Log($"[AIBrain] {name} 目标超出范围，需要追击决策");
                            }
                        }
                        else if (combatAction == 2)
                        {
                            // 切换到最佳武器
                            controller.SelectBestWeapon(target.gameObject);
                        }
                    }
                }
            }
            
            // 处理追击逻辑
            if (controller.ChaseTarget != null && moveAction != 0)
            {
                // AI选择移动，追击目标
                controller.ChaseToTarget(controller.ChaseTarget);
                AddReward(0.01f); // 追击奖励
            }
            else if (controller.ChaseTarget != null && moveAction == 0)
            {
                // AI选择不移动，但有追击目标
                AddReward(-0.01f); // 轻微惩罚
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
            float totalReward = 0f;
            
            // 1. 使用增强的奖励计算器
            if (rewardCalculator != null)
            {
                totalReward += rewardCalculator.CalculateTotalReward();
            }
            else
            {
                // 基础生存奖励
                totalReward += 0.001f;
            }
            
            // 2. 行为分析惩罚/奖励
            if (behaviorAnalyzer != null)
            {
                float behaviorPenalty = behaviorAnalyzer.CalculateBehaviorPenalty();
                totalReward += behaviorPenalty;
                
                // 如果AI被严重卡住，考虑重置
                if (behaviorAnalyzer.IsStuck && UnityEngine.Random.value < 0.1f)
                {
                    behaviorAnalyzer.ForceBreakLoop();
                    Debug.Log($"[AIBrain] {name} 尝试强制打破死循环");
                }
            }
            
            // 3. 探索奖励（基于熵）
            float explorationBonus = CalculateExplorationBonus();
            totalReward += explorationBonus;
            
            // 应用总奖励
            AddReward(totalReward);
            
            // 记录到情境记忆
            if (contextualMemory != null)
            {
                // 获取最近的动作（从OnActionReceived保存）
                contextualMemory.RecordActionResult(lastActions, totalReward, aiStats.IsDead);
            }
            
            // 死亡时结束episode
            if (aiStats.IsDead)
            {
                EndEpisode();
            }
        }
        
        // 计算探索奖励（鼓励尝试新行为）
        private float CalculateExplorationBonus()
        {
            // 基于动作的多样性给予小幅奖励
            // 这鼓励AI不要总是选择相同的动作
            float entropy = 0f;
            
            // 简单的熵计算（可以更复杂）
            if (UnityEngine.Random.value < 0.1f) // 10%概率给予探索奖励
            {
                entropy = 0.01f;
            }
            
            return entropy;
        }
        
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // 增强的启发式策略，确保在无DeepSeek API时也有智能交流
            var discreteActions = actionsOut.DiscreteActions;
            
            // 死亡时不行动
            if (aiStats.IsDead)
            {
                for (int i = 0; i < discreteActions.Length; i++)
                    discreteActions[i] = 0;
                return;
            }
            
            // 使用智能交流决策
            if (ShouldCommunicateHeuristic())
            {
                var commType = DetermineHeuristicCommunication();
                discreteActions[3] = (int)commType + 1; // 通信动作
                Debug.Log($"[AIBrain] Heuristic 交流决策: {commType}");
                return; // 专注于交流
            }
            
            // 生存优先逻辑
            bool needWater = aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.4f;
            bool needFood = aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f;
            bool needHealing = aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f;
            bool criticalHealth = aiStats.CurrentHealth <= aiStats.Config.maxHealth * 0.2f;
            
            // 紧急使用物品
            if (criticalHealth || needWater || needFood)
            {
                discreteActions[2] = 1; // 使用物品
                if (!criticalHealth) // 非危急状态可以移动寻找资源
                {
                    discreteActions[0] = UnityEngine.Random.Range(1, 5); // 随机移动寻找资源
                }
                return;
            }
            
            // 战斗逻辑
            var enemies = perception.GetNearbyEnemies();
            if (enemies.Count > 0 && !criticalHealth)
            {
                discreteActions[4] = 1; // 攻击
                // 简单移动逻辑：朝敌人移动
                if (enemies.Count > 0 && enemies[0] != null)
                {
                    Vector2 toEnemy = (enemies[0].transform.position - transform.position).normalized;
                    discreteActions[0] = GetMoveActionFromDirection(toEnemy);
                }
                return;
            }
            
            // 交互优先
            var npcs = perception.GetNearbyNPCs();
            var items = perception.GetNearbyItems();
            if (npcs.Count > 0 || items.Count > 0)
            {
                discreteActions[1] = 1; // 尝试交互
                return;
            }
            
            // 默认探索
            discreteActions[0] = UnityEngine.Random.Range(1, 5); // 移动
        }
        
        /// <summary>
        /// 启发式：判断是否应该交流
        /// </summary>
        private bool ShouldCommunicateHeuristic()
        {
            float socialMood = aiStats.GetMood(MoodDimension.Social);
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            int currentGold = currencyManager?.CurrentGold ?? 0;
            
            // 1. 紧急求助
            if (healthRatio < 0.3f && currentGold < 20 && socialMood < 0)
                return true;
            
            // 2. 严重孤独
            if (socialMood < -40f)
                return true;
            
            // 3. 发现重要信息需要分享
            var npcs = perception.GetNearbyNPCs();
            var enemies = perception.GetNearbyEnemies();
            if (npcs.Count > 0 || enemies.Count > 2)
                return true;
            
            // 4. 定期社交维护
            if (socialMood < 10f && UnityEngine.Random.value < 0.1f)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// 启发式：确定交流类型
        /// </summary>
        private CommunicationType DetermineHeuristicCommunication()
        {
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            int currentGold = currencyManager?.CurrentGold ?? 0;
            float socialMood = aiStats.GetMood(MoodDimension.Social);
            
            // 1. 紧急求助
            if (healthRatio < 0.3f || currentGold < 20)
                return CommunicationType.Help;
            
            // 2. 分享NPC发现
            var npcs = perception.GetNearbyNPCs();
            if (npcs.Count > 0)
                return CommunicationType.FoundNPC;
            
            // 3. 邀请协作（多敌人）
            var enemies = perception.GetNearbyEnemies();
            if (enemies.Count > 1 && healthRatio > 0.6f)
                return CommunicationType.ComeHere;
            
            // 4. 位置报告（默认）
            return CommunicationType.GoingTo;
        }
        
        /// <summary>
        /// 将方向向量转换为移动动作
        /// </summary>
        private int GetMoveActionFromDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x > 0 ? 4 : 3; // 右或左
            }
            else
            {
                return direction.y > 0 ? 1 : 2; // 上或下
            }
        }
        
        // 移除所有硬编码的Execute*Goal方法
        // 让强化学习模型自己学习如何达成目标
        
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
            if (shouldFight && nearbyEnemies.Count > 0)
            {
                // 清理已销毁的敌人
                nearbyEnemies.RemoveAll(e => e == null);
                if (nearbyEnemies.Count == 0) return;
                
                var nearestEnemy = nearbyEnemies[0];
                if (nearestEnemy != null && nearestEnemy.gameObject != null)
                {
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
            }
            // 2. 逃跑行为
            else if (shouldFlee)
            {
                var nearestEnemy = nearbyEnemies[0];
                if (nearestEnemy != null && nearestEnemy.gameObject != null)
                {
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
            
            // 收集通信信息
            var recentComms = new List<RecentCommunication>();
            var communicator = GetComponent<AICommunicator>();
            if (communicator != null)
            {
                var messages = communicator.GetRecentMessages(5);
                foreach (var msg in messages)
                {
                    if (msg?.Message != null)
                    {
                        recentComms.Add(new RecentCommunication
                        {
                            SenderName = msg.Message.Sender?.name ?? "Unknown",
                            MessageType = msg.Message.Type,
                            Position = msg.Message.Position,
                            TimeSince = Time.time - msg.Time
                        });
                    }
                }
            }
            
            return new AIDecisionContext
            {
                Stats = aiStats,
                Inventory = inventory,
                VisibleRooms = perception.GetVisibleRooms(),
                NearbyEnemies = perception.GetNearbyEnemies(),
                NearbyNPCs = perception.GetNearbyNPCs(),
                NearbyItems = perception.GetNearbyItems(),
                Memory = memory.GetAllMemories(),
                CurrentGold = currencyManager?.CurrentGold ?? 0,
                SourceGameObject = gameObject,
                RecentCommunications = recentComms
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
                // 存储决策供UI显示
                lastDecision = decision;
                
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
        
        /// <summary>
        /// 检查背包中是否有钥匙
        /// </summary>
        private bool HasKeyInInventory()
        {
            if (inventory == null) return false;
            
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && slot.Item is Inventory.Items.KeyItem)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 获取当前AI状态
        /// </summary>
        public AIState GetCurrentState()
        {
            return currentState;
        }
    }
    
    public enum AIState
    {
        Idle,          // 空闲
        Exploring,      // 探索
        Fighting,       // 战斗
        Fleeing,       // 逃跑
        Seeking,       // 寻找资源
        Trading,       // 交易
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