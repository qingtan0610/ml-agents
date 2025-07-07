using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Perception;

namespace AI.Core
{
    /// <summary>
    /// AI目标系统 - 提供层次化的目标管理
    /// </summary>
    public class AIGoalSystem : MonoBehaviour
    {
        [Header("Goal Settings")]
        [SerializeField] private float goalUpdateInterval = 2f;
        [SerializeField] private bool debugGoals = true;
        
        // 当前目标
        public AIGoal CurrentHighLevelGoal { get; private set; }
        public AIGoal CurrentMidLevelGoal { get; private set; }
        public AIGoal CurrentLowLevelGoal { get; private set; }
        
        // 目标进度
        public float HighLevelProgress { get; private set; }
        public float MidLevelProgress { get; private set; }
        public float LowLevelProgress { get; private set; }
        
        // 组件引用
        private AIStats aiStats;
        private AIPerception perception;
        private AIMemory memory;
        private Inventory.Inventory inventory;
        private AICommunicator communicator;
        
        // 上次更新时间
        private float lastGoalUpdateTime;
        
        // 个性特征（影响目标选择）
        [Header("Personality Traits")]
        [Range(0f, 1f)] public float aggression = 0.5f;     // 攻击性
        [Range(0f, 1f)] public float curiosity = 0.5f;      // 好奇心
        [Range(0f, 1f)] public float sociability = 0.5f;    // 社交性
        [Range(0f, 1f)] public float caution = 0.5f;        // 谨慎性
        [Range(0f, 1f)] public float leadership = 0.5f;     // 领导力
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            perception = GetComponent<AIPerception>();
            // AIMemory is not a MonoBehaviour, create new instance
            memory = new AIMemory();
            inventory = GetComponent<Inventory.Inventory>();
            communicator = GetComponent<AICommunicator>();
            
            // 随机化个性（如果需要）
            RandomizePersonality();
        }
        
        private void Start()
        {
            // 初始目标
            UpdateGoals();
        }
        
        private void Update()
        {
            // 定期更新目标
            if (Time.time - lastGoalUpdateTime > goalUpdateInterval)
            {
                UpdateGoals();
                lastGoalUpdateTime = Time.time;
            }
            
            // 更新目标进度
            UpdateGoalProgress();
        }
        
        /// <summary>
        /// 随机化AI个性
        /// </summary>
        private void RandomizePersonality()
        {
            // 可以基于某种分布来生成更有特色的个性
            aggression = Random.Range(0.2f, 0.8f);
            curiosity = Random.Range(0.3f, 0.9f);
            sociability = Random.Range(0.2f, 0.8f);
            caution = Random.Range(0.3f, 0.7f);
            leadership = Random.Range(0.1f, 0.9f);
        }
        
        /// <summary>
        /// 更新目标层次
        /// </summary>
        private void UpdateGoals()
        {
            // 更新高层目标
            CurrentHighLevelGoal = SelectHighLevelGoal();
            
            // 基于高层目标选择中层目标
            CurrentMidLevelGoal = SelectMidLevelGoal(CurrentHighLevelGoal);
            
            // 基于中层目标选择低层目标
            CurrentLowLevelGoal = SelectLowLevelGoal(CurrentMidLevelGoal);
            
            if (debugGoals)
            {
                Debug.Log($"[GoalSystem] {name} 目标更新: {CurrentHighLevelGoal.Type} > {CurrentMidLevelGoal.Type} > {CurrentLowLevelGoal.Type}");
            }
        }
        
        /// <summary>
        /// 选择高层战略目标
        /// </summary>
        private AIGoal SelectHighLevelGoal()
        {
            var goals = new List<(AIGoal goal, float priority)>();
            
            // 生存目标
            float survivalPriority = CalculateSurvivalPriority();
            goals.Add((new AIGoal(GoalType.Survival, "维持生命体征"), survivalPriority));
            
            // 进展目标
            float progressionPriority = CalculateProgressionPriority();
            goals.Add((new AIGoal(GoalType.Progression, "寻找并激活传送门"), progressionPriority));
            
            // 资源积累目标
            float resourcePriority = CalculateResourcePriority();
            goals.Add((new AIGoal(GoalType.ResourceAccumulation, "收集金币和物品"), resourcePriority));
            
            // 社交目标
            float socialPriority = CalculateSocialPriority();
            goals.Add((new AIGoal(GoalType.SocialBonding, "与其他AI交流"), socialPriority));
            
            // 选择优先级最高的目标
            return goals.OrderByDescending(g => g.priority).First().goal;
        }
        
        /// <summary>
        /// 选择中层战术目标
        /// </summary>
        private AIGoal SelectMidLevelGoal(AIGoal highLevelGoal)
        {
            switch (highLevelGoal.Type)
            {
                case GoalType.Survival:
                    return SelectSurvivalTactic();
                    
                case GoalType.Progression:
                    return SelectProgressionTactic();
                    
                case GoalType.ResourceAccumulation:
                    return SelectResourceTactic();
                    
                case GoalType.SocialBonding:
                    return SelectSocialTactic();
                    
                default:
                    return new AIGoal(GoalType.Exploration, "探索未知区域");
            }
        }
        
        /// <summary>
        /// 选择低层行动目标
        /// </summary>
        private AIGoal SelectLowLevelGoal(AIGoal midLevelGoal)
        {
            switch (midLevelGoal.Type)
            {
                case GoalType.Combat:
                    var enemies = perception?.GetNearbyEnemies();
                    if (enemies != null && enemies.Count > 0)
                    {
                        return new AIGoal(GoalType.Attack, $"攻击 {enemies[0].name}");
                    }
                    return new AIGoal(GoalType.MoveToTarget, "接近敌人");
                    
                case GoalType.Exploration:
                    return new AIGoal(GoalType.MoveToTarget, "前往未探索房间");
                    
                case GoalType.Trade:
                    var npcs = perception?.GetNearbyNPCs();
                    if (npcs != null && npcs.Count > 0)
                    {
                        return new AIGoal(GoalType.Interact, $"与 {npcs[0].NPCType} 交易");
                    }
                    return new AIGoal(GoalType.MoveToTarget, "寻找商人");
                    
                case GoalType.Communication:
                    return new AIGoal(GoalType.Interact, "发送通信或面对面交流");
                    
                case GoalType.ResourceManagement:
                    if (NeedToUseItem())
                    {
                        return new AIGoal(GoalType.UseItem, "使用恢复物品");
                    }
                    return new AIGoal(GoalType.PickupItem, "收集物品");
                    
                default:
                    return new AIGoal(GoalType.MoveToTarget, "随机移动");
            }
        }
        
        // 优先级计算方法
        private float CalculateSurvivalPriority()
        {
            if (aiStats == null || aiStats.Config == null) return 0.5f;
            
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            // 任何一项低于30%时，生存优先级急剧上升
            float minRatio = Mathf.Min(healthRatio, hungerRatio, thirstRatio);
            if (minRatio < 0.3f)
            {
                return 1f - minRatio; // 越低优先级越高
            }
            
            // 否则基于平均值和谨慎性
            float avgRatio = (healthRatio + hungerRatio + thirstRatio) / 3f;
            return (1f - avgRatio) * (0.5f + caution * 0.5f);
        }
        
        private float CalculateProgressionPriority()
        {
            // 如果已知传送门位置，优先级提高
            if (memory != null && memory.KnowsPortalLocation())
            {
                return 0.7f + leadership * 0.3f;
            }
            
            // 否则基于探索进度和好奇心
            float explorationProgress = memory?.GetExplorationProgress() ?? 0f;
            return (0.3f + explorationProgress * 0.4f) * curiosity;
        }
        
        private float CalculateResourcePriority()
        {
            // 基于当前资源状态和攻击性
            float goldRatio = Mathf.Min(GetComponent<Inventory.Managers.CurrencyManager>()?.CurrentGold ?? 0f, 100f) / 100f;
            float itemRatio = inventory != null ? GetTotalItemCount() / 10f : 0f;
            
            float resourceNeed = 1f - (goldRatio + itemRatio) / 2f;
            return resourceNeed * (0.5f + aggression * 0.5f);
        }
        
        private int GetTotalItemCount()
        {
            if (inventory == null) return 0;
            
            int count = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    count += slot.Quantity;
                }
            }
            return count;
        }
        
        private float CalculateSocialPriority()
        {
            // 基于孤独感和社交性
            float loneliness = -aiStats.GetMood(MoodDimension.Social) / 100f; // 转换为0-1
            return Mathf.Clamp01(loneliness * sociability);
        }
        
        // 战术选择方法
        private AIGoal SelectSurvivalTactic()
        {
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            // 优先处理最紧急的需求
            if (healthRatio < 0.3f)
            {
                return new AIGoal(GoalType.ResourceManagement, "寻找治疗");
            }
            else if (thirstRatio < 0.3f)
            {
                return new AIGoal(GoalType.Trade, "寻找水源");
            }
            else if (hungerRatio < 0.3f)
            {
                return new AIGoal(GoalType.Trade, "寻找食物");
            }
            
            return new AIGoal(GoalType.Exploration, "寻找补给点");
        }
        
        private AIGoal SelectProgressionTactic()
        {
            // 如果知道传送门位置
            if (memory != null && memory.KnowsPortalLocation())
            {
                return new AIGoal(GoalType.Exploration, "前往传送门");
            }
            
            // 否则继续探索
            return new AIGoal(GoalType.Exploration, "探索新区域");
        }
        
        private AIGoal SelectResourceTactic()
        {
            var enemies = perception?.GetNearbyEnemies();
            
            // 如果附近有敌人且状态良好，选择战斗
            if (enemies != null && enemies.Count > 0 && aiStats.CurrentHealth > aiStats.Config.maxHealth * 0.5f)
            {
                return new AIGoal(GoalType.Combat, "清理敌人获取掉落");
            }
            
            // 否则探索寻找资源
            return new AIGoal(GoalType.Exploration, "寻找宝箱和物品");
        }
        
        private AIGoal SelectSocialTactic()
        {
            var otherAIs = FindObjectsOfType<AICommunicator>()
                .Where(c => c != communicator && c.gameObject != gameObject)
                .ToList();
            
            // 如果附近有其他AI
            if (otherAIs.Any(ai => Vector2.Distance(transform.position, ai.transform.position) < 5f))
            {
                return new AIGoal(GoalType.Communication, "面对面交流");
            }
            
            // 否则使用交互机
            return new AIGoal(GoalType.Communication, "发送位置信息");
        }
        
        private bool NeedToUseItem()
        {
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            return healthRatio < 0.5f || hungerRatio < 0.5f || thirstRatio < 0.5f;
        }
        
        /// <summary>
        /// 更新目标进度
        /// </summary>
        private void UpdateGoalProgress()
        {
            // 这里可以根据具体目标计算进度
            // 暂时使用简单的时间累积
            HighLevelProgress = Mathf.Min(HighLevelProgress + Time.deltaTime * 0.01f, 1f);
            MidLevelProgress = Mathf.Min(MidLevelProgress + Time.deltaTime * 0.05f, 1f);
            LowLevelProgress = Mathf.Min(LowLevelProgress + Time.deltaTime * 0.1f, 1f);
            
            // 低层目标完成时重置
            if (LowLevelProgress >= 1f)
            {
                LowLevelProgress = 0f;
                MidLevelProgress += 0.2f;
            }
            
            // 中层目标完成时重置
            if (MidLevelProgress >= 1f)
            {
                MidLevelProgress = 0f;
                HighLevelProgress += 0.2f;
            }
        }
        
        /// <summary>
        /// 获取当前目标的编码（用于观察空间）
        /// </summary>
        public float[] GetGoalEncoding()
        {
            return new float[]
            {
                // 高层目标类型 (one-hot编码，4维)
                CurrentHighLevelGoal?.Type == GoalType.Survival ? 1f : 0f,
                CurrentHighLevelGoal?.Type == GoalType.Progression ? 1f : 0f,
                CurrentHighLevelGoal?.Type == GoalType.ResourceAccumulation ? 1f : 0f,
                CurrentHighLevelGoal?.Type == GoalType.SocialBonding ? 1f : 0f,
                
                // 中层目标类型 (5维)
                CurrentMidLevelGoal?.Type == GoalType.Combat ? 1f : 0f,
                CurrentMidLevelGoal?.Type == GoalType.Exploration ? 1f : 0f,
                CurrentMidLevelGoal?.Type == GoalType.Trade ? 1f : 0f,
                CurrentMidLevelGoal?.Type == GoalType.Communication ? 1f : 0f,
                CurrentMidLevelGoal?.Type == GoalType.ResourceManagement ? 1f : 0f,
                
                // 进度 (3维)
                HighLevelProgress,
                MidLevelProgress,
                LowLevelProgress,
                
                // 个性特征 (5维)
                aggression,
                curiosity,
                sociability,
                caution,
                leadership
            };
        }
        
        // Public getter for memory
        public AIMemory GetMemory() => memory;
    }
    
    /// <summary>
    /// AI目标
    /// </summary>
    [System.Serializable]
    public class AIGoal
    {
        public GoalType Type { get; private set; }
        public string Description { get; private set; }
        public float Priority { get; set; }
        
        public AIGoal(GoalType type, string description)
        {
            Type = type;
            Description = description;
            Priority = 0f;
        }
    }
    
    /// <summary>
    /// 目标类型
    /// </summary>
    public enum GoalType
    {
        // 高层战略目标
        Survival,               // 生存
        Progression,            // 进展（找传送门）
        ResourceAccumulation,   // 资源积累
        SocialBonding,         // 社交联系
        
        // 中层战术目标
        Combat,                // 战斗
        Exploration,           // 探索
        Trade,                 // 交易
        Communication,         // 通信
        ResourceManagement,    // 资源管理
        
        // 低层行动目标
        MoveToTarget,         // 移动到目标
        Attack,               // 攻击
        PickupItem,           // 拾取物品
        UseItem,              // 使用物品
        Interact              // 交互
    }
}