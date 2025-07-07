using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Perception;

namespace AI.Core
{
    /// <summary>
    /// AI行为分析器 - 检测死循环、重复行为和无效策略
    /// </summary>
    public class AIBehaviorAnalyzer : MonoBehaviour
    {
        [Header("死循环检测")]
        [SerializeField] private float positionCheckInterval = 0.3f; // 更频繁检查
        [SerializeField] private float stuckThreshold = 0.5f; // 降低阈值，更敏感
        [SerializeField] private int stuckCheckCount = 2; // 减少检测次数，更快响应
        
        [Header("行为模式检测")]
        [SerializeField] private int actionHistorySize = 100;
        [SerializeField] private int patternMinLength = 3;
        [SerializeField] private int patternMaxLength = 10;
        [SerializeField] private float repetitionPenalty = -0.1f;
        
        [Header("无效行为检测")]
        [SerializeField] private float ineffectiveActionWindow = 5f; // 5秒内的行为窗口
        [SerializeField] private float progressThreshold = 0.1f; // 进度阈值
        
        // 位置历史
        private Queue<Vector2> positionHistory = new Queue<Vector2>();
        private float lastPositionCheckTime;
        private int consecutiveStuckCount = 0;
        
        // 动作历史
        private Queue<ActionRecord> actionHistory = new Queue<ActionRecord>();
        private Dictionary<string, int> patternFrequency = new Dictionary<string, int>();
        
        // 状态跟踪
        private float lastHealth;
        private float lastHunger;
        private float lastThirst;
        private int lastItemCount;
        private int lastEnemyCount;
        private Vector2 lastProgressPosition;
        private float lastProgressTime;
        
        // 无效行为记录
        private Dictionary<string, FailureRecord> failureRecords = new Dictionary<string, FailureRecord>();
        
        // 组件引用
        private AIStats aiStats;
        private AIBrain aiBrain;
        private AIPerception perception;
        private Inventory.Inventory inventory;
        
        // 分析结果
        public bool IsStuck { get; private set; }
        public bool IsRepeatingPattern { get; private set; }
        public float CurrentEffectiveness { get; private set; }
        public string CurrentPattern { get; private set; }
        
        // 强制移动计数器
        private int forceMovementCounter = 0;
        private float lastForceMovementTime = 0f;
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            aiBrain = GetComponent<AIBrain>();
            perception = GetComponent<AIPerception>();
            inventory = GetComponent<Inventory.Inventory>();
            
            // 初始化状态
            if (aiStats != null)
            {
                lastHealth = aiStats.CurrentHealth;
                lastHunger = aiStats.CurrentHunger;
                lastThirst = aiStats.CurrentThirst;
            }
            
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
        }
        
        private void Update()
        {
            // 定期检测位置
            if (Time.time - lastPositionCheckTime > positionCheckInterval)
            {
                CheckMovementProgress();
                lastPositionCheckTime = Time.time;
            }
            
            // 检查是否需要连续强制移动
            CheckContinuousForceMovement();
            
            // 分析行为有效性
            AnalyzeBehaviorEffectiveness();
        }
        
        /// <summary>
        /// 记录AI的动作
        /// </summary>
        public void RecordAction(int[] actions, string context = "")
        {
            var record = new ActionRecord
            {
                actions = (int[])actions.Clone(),
                timestamp = Time.time,
                position = transform.position,
                health = aiStats?.CurrentHealth ?? 100f,
                context = context
            };
            
            actionHistory.Enqueue(record);
            if (actionHistory.Count > actionHistorySize)
            {
                actionHistory.Dequeue();
            }
            
            // 检测重复模式
            DetectPatterns();
        }
        
        /// <summary>
        /// 计算行为奖励调整 - 只惩罚真正有害的行为
        /// </summary>
        public float CalculateBehaviorPenalty()
        {
            float penalty = 0f;
            
            // 1. 死循环惩罚 - 这是真正的问题
            if (IsStuck)
            {
                penalty += -1.0f; // 增强惩罚力度
                Debug.Log($"[BehaviorAnalyzer] {name} 检测到死循环！惩罚: -1.0");
            }
            
            // 2. 智能重复模式分析 - 只惩罚无效的重复
            if (IsRepeatingPattern && !string.IsNullOrEmpty(CurrentPattern))
            {
                // 检查重复模式是否有效
                if (IsPatternCounterproductive(CurrentPattern))
                {
                    int frequency = patternFrequency.GetValueOrDefault(CurrentPattern, 0);
                    penalty += repetitionPenalty * Mathf.Log(frequency + 1);
                    Debug.Log($"[BehaviorAnalyzer] {name} 无效重复模式 '{CurrentPattern}' 频率:{frequency}, 惩罚: {penalty}");
                }
                else
                {
                    // 有效的重复模式不惩罚，甚至可能奖励
                    // 不输出日志，避免刷屏
                }
            }
            
            // 3. 真正无效的行为惩罚 - 提高阈值
            if (CurrentEffectiveness < 0.1f) // 从0.3降低到0.1，只惩罚极度无效的行为
            {
                penalty += -0.1f * (1f - CurrentEffectiveness);
                Debug.Log($"[BehaviorAnalyzer] {name} 行为极度无效: {CurrentEffectiveness:F2}, 惩罚: {penalty}");
            }
            
            // 4. 探索奖励（如果在有效移动）
            if (!IsStuck && GetMovementVariance() > 2f)
            {
                penalty += 0.05f; // 小幅奖励有效探索
            }
            
            return penalty;
        }
        
        /// <summary>
        /// 检测移动进度
        /// </summary>
        private void CheckMovementProgress()
        {
            Vector2 currentPos = transform.position;
            positionHistory.Enqueue(currentPos);
            
            if (positionHistory.Count > stuckCheckCount)
            {
                positionHistory.Dequeue();
                
                // 计算移动范围
                float maxDistance = 0f;
                var positions = positionHistory.ToArray();
                for (int i = 0; i < positions.Length - 1; i++)
                {
                    for (int j = i + 1; j < positions.Length; j++)
                    {
                        float dist = Vector2.Distance(positions[i], positions[j]);
                        maxDistance = Mathf.Max(maxDistance, dist);
                    }
                }
                
                // 判断是否卡住
                if (maxDistance < stuckThreshold)
                {
                    consecutiveStuckCount++;
                    if (consecutiveStuckCount >= stuckCheckCount)
                    {
                        IsStuck = true;
                        OnDetectedStuck();
                    }
                }
                else
                {
                    consecutiveStuckCount = 0;
                    IsStuck = false;
                }
            }
        }
        
        /// <summary>
        /// 检测动作模式
        /// </summary>
        private void DetectPatterns()
        {
            if (actionHistory.Count < patternMinLength * 2) return;
            
            var actions = actionHistory.ToArray();
            IsRepeatingPattern = false;
            
            // 检测不同长度的模式
            for (int length = patternMinLength; length <= patternMaxLength && length * 2 <= actions.Length; length++)
            {
                // 获取最近的动作序列
                var recentPattern = GetActionPattern(actions, actions.Length - length, length);
                
                // 在历史中查找相同模式
                int matchCount = 0;
                for (int i = 0; i <= actions.Length - length; i++)
                {
                    if (GetActionPattern(actions, i, length) == recentPattern)
                    {
                        matchCount++;
                    }
                }
                
                // 如果模式出现频率高，记录它 - 降低阈值，更敏感
                if (matchCount >= 2) // 从3降低到2，更快检测重复
                {
                    IsRepeatingPattern = true;
                    CurrentPattern = recentPattern;
                    patternFrequency[recentPattern] = matchCount;
                    
                    // 只保留最常见的模式
                    if (patternFrequency.Count > 20)
                    {
                        var leastFrequent = patternFrequency.OrderBy(kvp => kvp.Value).First().Key;
                        patternFrequency.Remove(leastFrequent);
                    }
                }
            }
        }
        
        /// <summary>
        /// 分析行为有效性
        /// </summary>
        private void AnalyzeBehaviorEffectiveness()
        {
            float effectiveness = 0f;
            float weights = 0f;
            
            // 1. 健康进度（避免受伤）
            if (aiStats != null)
            {
                float healthProgress = (aiStats.CurrentHealth - lastHealth) / aiStats.Config.maxHealth;
                effectiveness += healthProgress * 2f; // 健康很重要
                weights += 2f;
                lastHealth = aiStats.CurrentHealth;
            }
            
            // 2. 资源获取进度
            if (inventory != null)
            {
                int currentItems = GetItemCount();
                float itemProgress = (currentItems - lastItemCount) * 0.1f;
                effectiveness += itemProgress;
                weights += 1f;
                lastItemCount = currentItems;
            }
            
            // 3. 战斗进度
            if (perception != null)
            {
                int currentEnemies = perception.GetNearbyEnemies().Count;
                float combatProgress = (lastEnemyCount - currentEnemies) * 0.2f;
                effectiveness += combatProgress;
                weights += 1f;
                lastEnemyCount = currentEnemies;
            }
            
            // 4. 探索进度
            float explorationProgress = Vector2.Distance(transform.position, lastProgressPosition) / ineffectiveActionWindow;
            effectiveness += explorationProgress * 0.1f;
            weights += 0.5f;
            
            if (Time.time - lastProgressTime > ineffectiveActionWindow)
            {
                lastProgressPosition = transform.position;
                lastProgressTime = Time.time;
            }
            
            // 计算总体效率
            CurrentEffectiveness = weights > 0 ? Mathf.Clamp01((effectiveness + 1f) / weights) : 0.5f;
        }
        
        /// <summary>
        /// 获取动作模式字符串
        /// </summary>
        private string GetActionPattern(ActionRecord[] actions, int start, int length)
        {
            var pattern = "";
            for (int i = start; i < start + length && i < actions.Length; i++)
            {
                // 将动作数组转换为字符串模式
                pattern += string.Join(",", actions[i].actions) + ";";
            }
            return pattern;
        }
        
        /// <summary>
        /// 当检测到卡住时
        /// </summary>
        private void OnDetectedStuck()
        {
            Debug.LogWarning($"[BehaviorAnalyzer] {name} 被检测到卡住！位置: {transform.position}");
            
            // 记录失败情境
            var context = GetCurrentContext();
            if (!failureRecords.ContainsKey(context))
            {
                failureRecords[context] = new FailureRecord();
            }
            failureRecords[context].count++;
            failureRecords[context].lastTime = Time.time;
            
            // 给AI一个强烈的负面信号
            if (aiBrain != null)
            {
                aiBrain.AddReward(-2f); // 增强惩罚
                
                // 强制打破循环
                ForceBreakLoop();
                
                // 立即强制移动
                ForceRandomMovement();
            }
        }
        
        /// <summary>
        /// 获取当前情境
        /// </summary>
        private string GetCurrentContext()
        {
            var context = $"Pos:{(int)transform.position.x},{(int)transform.position.y}";
            
            if (perception != null)
            {
                context += $"_E:{perception.GetNearbyEnemies().Count}";
                context += $"_I:{perception.GetNearbyItems().Count}";
                context += $"_N:{perception.GetNearbyNPCs().Count}";
            }
            
            return context;
        }
        
        /// <summary>
        /// 检查当前情境是否曾经失败过
        /// </summary>
        public bool HasFailedInContext(string context)
        {
            return failureRecords.ContainsKey(context) && failureRecords[context].count > 2;
        }
        
        /// <summary>
        /// 获取失败记录
        /// </summary>
        public int GetFailureCount(string context)
        {
            return failureRecords.ContainsKey(context) ? failureRecords[context].count : 0;
        }
        
        /// <summary>
        /// 强制打破循环
        /// </summary>
        public void ForceBreakLoop()
        {
            Debug.Log($"[BehaviorAnalyzer] {name} 强制打破循环！");
            
            // 清空位置历史
            positionHistory.Clear();
            consecutiveStuckCount = 0;
            IsStuck = false;
            
            // 清空重复模式记录
            patternFrequency.Clear();
            IsRepeatingPattern = false;
            CurrentPattern = "";
            
            // 添加随机扰动
            if (aiBrain != null)
            {
                // 给AI一个探索奖励，鼓励它尝试新行为
                aiBrain.AddReward(0.2f);
            }
        }
        
        /// <summary>
        /// 立即强制随机移动 - 直接控制AI移动
        /// </summary>
        public void ForceRandomMovement()
        {
            if (aiBrain != null)
            {
                // 获取AI控制器
                var controller = aiBrain.GetComponent<AIController>();
                if (controller != null)
                {
                    // 随机选择一个方向强制移动
                    Vector2[] directions = {
                        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                        new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
                        new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized
                    };
                    
                    Vector2 randomDirection = directions[UnityEngine.Random.Range(0, directions.Length)];
                    controller.Move(randomDirection);
                    
                    // 记录强制移动
                    forceMovementCounter++;
                    lastForceMovementTime = Time.time;
                    
                    Debug.Log($"[BehaviorAnalyzer] {name} 立即强制移动到方向 {randomDirection} (第{forceMovementCounter}次)");
                }
            }
        }
        
        /// <summary>
        /// 检查是否需要连续强制移动
        /// </summary>
        private void CheckContinuousForceMovement()
        {
            // 如果最近强制移动过且仍然卡住，继续强制移动
            if (IsStuck && Time.time - lastForceMovementTime < 2f && forceMovementCounter < 5)
            {
                ForceRandomMovement();
            }
            
            // 重置计数器
            if (Time.time - lastForceMovementTime > 3f)
            {
                forceMovementCounter = 0;
            }
        }
        
        /// <summary>
        /// 获取移动方差（衡量探索程度）
        /// </summary>
        private float GetMovementVariance()
        {
            if (positionHistory.Count < 2) return 0f;
            
            var positions = positionHistory.ToArray();
            Vector2 mean = Vector2.zero;
            foreach (var pos in positions)
            {
                mean += pos;
            }
            mean /= positions.Length;
            
            float variance = 0f;
            foreach (var pos in positions)
            {
                variance += (pos - mean).sqrMagnitude;
            }
            
            return variance / positions.Length;
        }
        
        private int GetItemCount()
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
        
        /// <summary>
        /// 判断重复模式是否反生产力
        /// </summary>
        private bool IsPatternCounterproductive(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            
            // 分析模式内容
            var actions = pattern.Split(';');
            if (actions.Length == 0) return false;
            
            // 检查是否是无效的重复模式
            bool hasMovementAction = false;
            bool hasActualMovement = false;
            bool isStuckPattern = true;
            
            foreach (var actionStr in actions)
            {
                if (string.IsNullOrEmpty(actionStr)) continue;
                
                var actionValues = actionStr.Split(',');
                if (actionValues.Length >= 5)
                {
                    // 动作格式: [移动,交互,物品,通信,战斗]
                    int moveAction = int.TryParse(actionValues[0], out int m) ? m : 0;
                    int combatAction = int.TryParse(actionValues[4], out int c) ? c : 0;
                    
                    // 记录是否有移动指令
                    if (moveAction != 0)
                    {
                        hasMovementAction = true;
                    }
                    
                    // 如果有移动或战斗行为，说明不是完全卡死
                    if (moveAction != 0 || combatAction != 0)
                    {
                        isStuckPattern = false;
                    }
                }
            }
            
            // 检查实际位移效果
            if (positionHistory.Count >= 4)
            {
                var positions = positionHistory.ToArray();
                float totalMovement = 0f;
                float maxDistance = 0f;
                
                // 计算总位移和最大位移
                for (int i = 1; i < positions.Length; i++)
                {
                    float distance = Vector2.Distance(positions[i-1], positions[i]);
                    totalMovement += distance;
                    
                    // 计算与起点的最大距离
                    float distanceFromStart = Vector2.Distance(positions[0], positions[i]);
                    maxDistance = Mathf.Max(maxDistance, distanceFromStart);
                }
                
                // 判断标准：
                // 1. 完全没有移动指令的重复模式 = 反生产力
                if (isStuckPattern && !hasMovementAction)
                {
                    return true;
                }
                
                // 2. 有移动指令但实际位移极小 = 对着墙移动，反生产力
                if (hasMovementAction && totalMovement < 0.5f && maxDistance < 0.8f) // 放宽阈值，避免误判
                {
                    int frequency = patternFrequency.GetValueOrDefault(pattern, 0);
                    if (frequency >= 2) // 重复2次以上就惩罚
                    {
                        Debug.Log($"[BehaviorAnalyzer] {name} 检测到对墙移动: 移动指令存在但位移很小 (总位移:{totalMovement:F2}, 最大距离:{maxDistance:F2})");
                        
                        // 立即强制移动打破卡墙
                        ForceRandomMovement();
                        return true;
                    }
                }
                
                // 3. 在小范围内来回移动 = 可能是无效循环
                if (hasMovementAction && totalMovement > 1.0f && maxDistance < 1.0f)
                {
                    int frequency = patternFrequency.GetValueOrDefault(pattern, 0);
                    if (frequency > 5) // 重复5次以上的小范围移动
                    {
                        Debug.Log($"[BehaviorAnalyzer] {name} 检测到小范围循环移动: 总位移:{totalMovement:F2}, 范围:{maxDistance:F2}");
                        return true;
                    }
                }
                
                // 4. 有效移动模式：有位移且不局限在小范围
                hasActualMovement = totalMovement > 0.5f && maxDistance > 1.0f;
            }
            
            // 如果有战斗行为且敌人在附近，认为是有效模式
            if (perception != null)
            {
                var nearbyEnemies = perception.GetNearbyEnemies();
                if (nearbyEnemies.Count > 0)
                {
                    bool hasCombatAction = false;
                    foreach (var actionStr in actions)
                    {
                        if (string.IsNullOrEmpty(actionStr)) continue;
                        var actionValues = actionStr.Split(',');
                        if (actionValues.Length >= 5)
                        {
                            int combatAction = int.TryParse(actionValues[4], out int c) ? c : 0;
                            if (combatAction != 0)
                            {
                                hasCombatAction = true;
                                break;
                            }
                        }
                    }
                    
                    if (hasCombatAction)
                    {
                        // 战斗模式重复认为有效，不输出日志
                        return false; // 战斗重复模式是有效的
                    }
                }
            }
            
            // 其他情况：有实际移动的模式认为是有效的
            return !hasActualMovement;
        }
        
        /// <summary>
        /// 获取行为建议
        /// </summary>
        public ActionSuggestion GetActionSuggestion()
        {
            var suggestion = new ActionSuggestion();
            
            // 如果卡住了，建议随机移动
            if (IsStuck)
            {
                suggestion.shouldRandomMove = true;
                suggestion.randomMoveStrength = 1f;
            }
            
            // 如果在重复模式，建议添加噪声
            if (IsRepeatingPattern)
            {
                suggestion.shouldAddNoise = true;
                suggestion.noiseLevel = 0.3f;
            }
            
            // 如果效率低，建议改变策略
            if (CurrentEffectiveness < 0.3f)
            {
                suggestion.shouldChangeStrategy = true;
                suggestion.strategyChangeUrgency = 1f - CurrentEffectiveness;
            }
            
            return suggestion;
        }
        
        // 内部类
        private class ActionRecord
        {
            public int[] actions;
            public float timestamp;
            public Vector2 position;
            public float health;
            public string context;
        }
        
        private class FailureRecord
        {
            public int count;
            public float lastTime;
        }
        
        public class ActionSuggestion
        {
            public bool shouldRandomMove;
            public float randomMoveStrength;
            public bool shouldAddNoise;
            public float noiseLevel;
            public bool shouldChangeStrategy;
            public float strategyChangeUrgency;
        }
    }
}