using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Perception;

namespace AI.Core
{
    /// <summary>
    /// AI情境记忆系统 - 记住在特定情境下的成功和失败经验
    /// </summary>
    public class AIContextualMemory : MonoBehaviour
    {
        [Header("记忆设置")]
        [SerializeField] private int maxMemories = 1000;
        [SerializeField] private float memoryDecayRate = 0.95f; // 记忆衰减率
        [SerializeField] private float contextSimilarityThreshold = 0.8f;
        [SerializeField] private float memoryUpdateInterval = 1f;
        
        [Header("情境权重")]
        [SerializeField] private float healthWeight = 2f;
        [SerializeField] private float locationWeight = 1f;
        [SerializeField] private float enemyWeight = 1.5f;
        [SerializeField] private float itemWeight = 1f;
        [SerializeField] private float timeWeight = 0.5f;
        
        // 情境记忆存储
        private Dictionary<string, ContextMemory> memories = new Dictionary<string, ContextMemory>();
        private Queue<string> memoryOrder = new Queue<string>(); // 用于LRU
        
        // 当前情境
        private Context currentContext;
        private float lastUpdateTime;
        
        // 组件引用
        private AIStats aiStats;
        private AIPerception perception;
        private AIBrain aiBrain;
        private Inventory.Inventory inventory;
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            perception = GetComponent<AIPerception>();
            aiBrain = GetComponent<AIBrain>();
            inventory = GetComponent<Inventory.Inventory>();
        }
        
        private void Update()
        {
            if (Time.time - lastUpdateTime > memoryUpdateInterval)
            {
                UpdateCurrentContext();
                lastUpdateTime = Time.time;
            }
        }
        
        /// <summary>
        /// 记录动作结果
        /// </summary>
        public void RecordActionResult(int[] actions, float reward, bool isTerminal = false)
        {
            if (currentContext == null) return;
            
            string contextKey = currentContext.ToKey();
            string actionKey = ActionsToKey(actions);
            
            // 获取或创建记忆
            if (!memories.ContainsKey(contextKey))
            {
                memories[contextKey] = new ContextMemory();
                memoryOrder.Enqueue(contextKey);
                
                // 如果超出容量，移除最旧的记忆
                if (memories.Count > maxMemories)
                {
                    string oldestKey = memoryOrder.Dequeue();
                    memories.Remove(oldestKey);
                }
            }
            
            var memory = memories[contextKey];
            
            // 更新动作价值
            if (!memory.actionValues.ContainsKey(actionKey))
            {
                memory.actionValues[actionKey] = new ActionValue();
            }
            
            var actionValue = memory.actionValues[actionKey];
            actionValue.count++;
            actionValue.totalReward += reward;
            actionValue.averageReward = actionValue.totalReward / actionValue.count;
            
            if (isTerminal)
            {
                actionValue.terminalCount++;
            }
            
            // 记录最佳和最差动作
            UpdateBestWorstActions(memory);
            
            // 衰减旧记忆
            DecayMemories();
        }
        
        /// <summary>
        /// 获取情境建议
        /// </summary>
        public ContextSuggestion GetContextSuggestion()
        {
            var suggestion = new ContextSuggestion();
            
            if (currentContext == null)
            {
                UpdateCurrentContext();
            }
            
            // 查找相似情境
            var similarContexts = FindSimilarContexts(currentContext, 3);
            
            if (similarContexts.Count > 0)
            {
                // 分析相似情境的经验
                var bestActions = new Dictionary<string, float>();
                var worstActions = new Dictionary<string, float>();
                
                foreach (var similar in similarContexts)
                {
                    var memory = memories[similar.Key];
                    float weight = similar.Value; // 相似度作为权重
                    
                    // 收集最佳动作
                    if (!string.IsNullOrEmpty(memory.bestAction))
                    {
                        if (!bestActions.ContainsKey(memory.bestAction))
                            bestActions[memory.bestAction] = 0;
                        bestActions[memory.bestAction] += memory.bestActionValue * weight;
                    }
                    
                    // 收集最差动作
                    if (!string.IsNullOrEmpty(memory.worstAction))
                    {
                        if (!worstActions.ContainsKey(memory.worstAction))
                            worstActions[memory.worstAction] = 0;
                        worstActions[memory.worstAction] += memory.worstActionValue * weight;
                    }
                }
                
                // 生成建议
                if (bestActions.Count > 0)
                {
                    var best = bestActions.OrderByDescending(kvp => kvp.Value).First();
                    suggestion.recommendedActions = KeyToActions(best.Key);
                    suggestion.confidence = Mathf.Clamp01(best.Value / similarContexts.Count);
                }
                
                if (worstActions.Count > 0)
                {
                    var worst = worstActions.OrderBy(kvp => kvp.Value).First();
                    suggestion.avoidActions = KeyToActions(worst.Key);
                    suggestion.avoidanceStrength = Mathf.Clamp01(-worst.Value / similarContexts.Count);
                }
                
                // 如果在相似情境下总是失败，建议尝试新策略
                float averageSuccess = similarContexts.Average(s => memories[s.Key].successRate);
                if (averageSuccess < 0.3f)
                {
                    suggestion.shouldTryNewStrategy = true;
                    suggestion.explorationBonus = 0.5f * (1f - averageSuccess);
                }
            }
            else
            {
                // 没有相似经验，鼓励探索
                suggestion.shouldTryNewStrategy = true;
                suggestion.explorationBonus = 0.3f;
            }
            
            return suggestion;
        }
        
        /// <summary>
        /// 检查当前情境是否危险
        /// </summary>
        public bool IsCurrentContextDangerous()
        {
            if (currentContext == null) return false;
            
            var similar = FindSimilarContexts(currentContext, 5);
            if (similar.Count == 0) return false;
            
            // 检查相似情境的死亡率
            float deathRate = 0f;
            foreach (var s in similar)
            {
                var memory = memories[s.Key];
                deathRate += memory.deathRate * s.Value;
            }
            
            return deathRate / similar.Count > 0.5f;
        }
        
        /// <summary>
        /// 更新当前情境
        /// </summary>
        private void UpdateCurrentContext()
        {
            currentContext = new Context
            {
                // 健康状态
                healthRatio = aiStats != null ? aiStats.CurrentHealth / aiStats.Config.maxHealth : 1f,
                hungerRatio = aiStats != null ? aiStats.CurrentHunger / aiStats.Config.maxHunger : 1f,
                thirstRatio = aiStats != null ? aiStats.CurrentThirst / aiStats.Config.maxThirst : 1f,
                
                // 位置信息（网格化）
                gridX = Mathf.RoundToInt(transform.position.x / 4f),
                gridY = Mathf.RoundToInt(transform.position.y / 4f),
                
                // 环境信息
                nearbyEnemyCount = perception != null ? perception.GetNearbyEnemies().Count : 0,
                nearbyItemCount = perception != null ? perception.GetNearbyItems().Count : 0,
                nearbyNPCCount = perception != null ? perception.GetNearbyNPCs().Count : 0,
                
                // 资源信息
                hasWeapon = inventory != null && HasWeapon(),
                itemCount = inventory != null ? GetItemCount() : 0,
                
                // 时间信息
                timePhase = Mathf.FloorToInt((Time.time % 300f) / 60f) // 5分钟为一个时间段
            };
        }
        
        /// <summary>
        /// 查找相似情境
        /// </summary>
        private List<KeyValuePair<string, float>> FindSimilarContexts(Context context, int maxCount)
        {
            var similarities = new List<KeyValuePair<string, float>>();
            
            foreach (var kvp in memories)
            {
                var memContext = Context.FromKey(kvp.Key);
                float similarity = CalculateContextSimilarity(context, memContext);
                
                if (similarity > contextSimilarityThreshold)
                {
                    similarities.Add(new KeyValuePair<string, float>(kvp.Key, similarity));
                }
            }
            
            return similarities
                .OrderByDescending(s => s.Value)
                .Take(maxCount)
                .ToList();
        }
        
        /// <summary>
        /// 计算情境相似度
        /// </summary>
        private float CalculateContextSimilarity(Context c1, Context c2)
        {
            float similarity = 0f;
            float totalWeight = 0f;
            
            // 健康状态相似度
            float healthSim = 1f - Mathf.Abs(c1.healthRatio - c2.healthRatio);
            similarity += healthSim * healthWeight;
            totalWeight += healthWeight;
            
            // 位置相似度
            float posDist = Mathf.Sqrt(Mathf.Pow(c1.gridX - c2.gridX, 2) + Mathf.Pow(c1.gridY - c2.gridY, 2));
            float posSim = Mathf.Exp(-posDist / 5f); // 指数衰减
            similarity += posSim * locationWeight;
            totalWeight += locationWeight;
            
            // 敌人数量相似度
            float enemySim = 1f - Mathf.Abs(c1.nearbyEnemyCount - c2.nearbyEnemyCount) / 10f;
            similarity += enemySim * enemyWeight;
            totalWeight += enemyWeight;
            
            // 物品相似度
            float itemSim = 1f - Mathf.Abs(c1.itemCount - c2.itemCount) / 10f;
            similarity += itemSim * itemWeight;
            totalWeight += itemWeight;
            
            return totalWeight > 0 ? similarity / totalWeight : 0f;
        }
        
        /// <summary>
        /// 更新最佳/最差动作
        /// </summary>
        private void UpdateBestWorstActions(ContextMemory memory)
        {
            float bestValue = float.MinValue;
            float worstValue = float.MaxValue;
            
            foreach (var kvp in memory.actionValues)
            {
                if (kvp.Value.averageReward > bestValue)
                {
                    bestValue = kvp.Value.averageReward;
                    memory.bestAction = kvp.Key;
                    memory.bestActionValue = bestValue;
                }
                
                if (kvp.Value.averageReward < worstValue)
                {
                    worstValue = kvp.Value.averageReward;
                    memory.worstAction = kvp.Key;
                    memory.worstActionValue = worstValue;
                }
            }
            
            // 计算成功率
            int totalAttempts = memory.actionValues.Sum(kvp => kvp.Value.count);
            int successCount = memory.actionValues.Count(kvp => kvp.Value.averageReward > 0);
            memory.successRate = totalAttempts > 0 ? (float)successCount / totalAttempts : 0f;
            
            // 计算死亡率
            int deathCount = memory.actionValues.Sum(kvp => kvp.Value.terminalCount);
            memory.deathRate = totalAttempts > 0 ? (float)deathCount / totalAttempts : 0f;
        }
        
        /// <summary>
        /// 衰减记忆
        /// </summary>
        private void DecayMemories()
        {
            foreach (var memory in memories.Values)
            {
                foreach (var actionValue in memory.actionValues.Values)
                {
                    actionValue.totalReward *= memoryDecayRate;
                    actionValue.averageReward *= memoryDecayRate;
                }
            }
        }
        
        // 辅助方法
        private string ActionsToKey(int[] actions)
        {
            return string.Join(",", actions);
        }
        
        private int[] KeyToActions(string key)
        {
            if (string.IsNullOrEmpty(key)) return new int[5];
            
            try
            {
                return key.Split(',').Select(int.Parse).ToArray();
            }
            catch
            {
                return new int[5];
            }
        }
        
        private bool HasWeapon()
        {
            // 检查是否有武器
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && slot.Item is Inventory.Items.WeaponItem)
                {
                    return true;
                }
            }
            return false;
        }
        
        private int GetItemCount()
        {
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    count++;
            }
            return count;
        }
        
        /// <summary>
        /// 清空记忆（新地图时使用）
        /// </summary>
        public void ClearMemories()
        {
            memories.Clear();
            memoryOrder.Clear();
        }
        
        // 数据类
        [System.Serializable]
        private class Context
        {
            public float healthRatio;
            public float hungerRatio;
            public float thirstRatio;
            public int gridX;
            public int gridY;
            public int nearbyEnemyCount;
            public int nearbyItemCount;
            public int nearbyNPCCount;
            public bool hasWeapon;
            public int itemCount;
            public int timePhase;
            
            public string ToKey()
            {
                return $"{(int)(healthRatio*10)}_{(int)(hungerRatio*10)}_{(int)(thirstRatio*10)}" +
                       $"_{gridX}_{gridY}_{nearbyEnemyCount}_{itemCount}_{hasWeapon}";
            }
            
            public static Context FromKey(string key)
            {
                var parts = key.Split('_');
                return new Context
                {
                    healthRatio = int.Parse(parts[0]) / 10f,
                    hungerRatio = int.Parse(parts[1]) / 10f,
                    thirstRatio = int.Parse(parts[2]) / 10f,
                    gridX = int.Parse(parts[3]),
                    gridY = int.Parse(parts[4]),
                    nearbyEnemyCount = int.Parse(parts[5]),
                    itemCount = int.Parse(parts[6]),
                    hasWeapon = bool.Parse(parts[7])
                };
            }
        }
        
        private class ContextMemory
        {
            public Dictionary<string, ActionValue> actionValues = new Dictionary<string, ActionValue>();
            public string bestAction;
            public float bestActionValue;
            public string worstAction;
            public float worstActionValue;
            public float successRate;
            public float deathRate;
        }
        
        private class ActionValue
        {
            public int count;
            public float totalReward;
            public float averageReward;
            public int terminalCount;
        }
        
        public class ContextSuggestion
        {
            public int[] recommendedActions;
            public int[] avoidActions;
            public float confidence;
            public float avoidanceStrength;
            public bool shouldTryNewStrategy;
            public float explorationBonus;
        }
    }
}