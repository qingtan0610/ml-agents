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
        /// 选择高层战略目标 - 优化为生存经营导向
        /// </summary>
        private AIGoal SelectHighLevelGoal()
        {
            var goals = new List<(AIGoal goal, float priority)>();
            
            // 1. 紧急生存目标 - 最高优先级
            float survivalPriority = CalculateSurvivalPriority();
            goals.Add((new AIGoal(GoalType.Survival, "维持生命体征"), survivalPriority));
            
            // 2. 生存经营目标 - 稳定收入和资源管理
            float economicPriority = CalculateEconomicPriority();
            goals.Add((new AIGoal(GoalType.ResourceAccumulation, "经营资源和金币"), economicPriority));
            
            // 3. 团队合作目标 - 4人协作是游戏核心
            float cooperationPriority = CalculateCooperationPriority();
            goals.Add((new AIGoal(GoalType.SocialBonding, "团队协作寻找传送门"), cooperationPriority));
            
            // 4. 地图进展目标 - 最终目标但不是最紧急的
            float progressionPriority = CalculateProgressionPriority();
            goals.Add((new AIGoal(GoalType.Progression, "完成地图探索"), progressionPriority));
            
            // 选择优先级最高的目标
            var selectedGoal = goals.OrderByDescending(g => g.priority).First().goal;
            
            if (debugGoals)
            {
                Debug.Log($"[GoalSystem] {name} 目标优先级: 生存{survivalPriority:F2}, 经营{economicPriority:F2}, 合作{cooperationPriority:F2}, 进展{progressionPriority:F2} -> 选择: {selectedGoal.Description}");
            }
            
            return selectedGoal;
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
        
        /// <summary>
        /// 计算经营优先级 - 生存经营游戏导向
        /// </summary>
        private float CalculateEconomicPriority()
        {
            float priority = 0f;
            
            // 金币经营需求
            int currentGold = GetComponent<Inventory.Managers.CurrencyManager>()?.CurrentGold ?? 0;
            if (currentGold < 50)  // 基本生存金币
                priority += 0.7f;
            else if (currentGold < 200)  // 舒适生活金币
                priority += 0.4f;
            else if (currentGold < 500)  // 富裕状态金币
                priority += 0.2f;
            
            // 背包管理需求
            float inventoryFullness = GetInventoryFullness();
            if (inventoryFullness > 0.8f)  // 背包快满了，需要处理
                priority += 0.3f;
            else if (inventoryFullness < 0.3f)  // 背包很空，需要收集
                priority += 0.2f;
            
            // 基于生存状态调整经营优先级
            float survivalRatio = GetAverageSurvivalRatio();
            if (survivalRatio > 0.7f)  // 生存状态良好时，专注经营
                priority += 0.3f;
            
            return Mathf.Clamp01(priority);
        }
        
        /// <summary>
        /// 计算团队合作优先级
        /// </summary>
        private float CalculateCooperationPriority()
        {
            float priority = 0f;
            
            // 基于孤独感
            float loneliness = Mathf.Clamp01(-aiStats.GetMood(MoodDimension.Social) / 100f);
            priority += loneliness * 0.4f;
            
            // 如果知道传送门位置，合作优先级提高
            if (memory != null && memory.KnowsPortalLocation())
            {
                priority += 0.6f;
                
                // 检查其他AI是否也在传送门附近
                var otherAIs = FindObjectsOfType<AIBrain>();
                int nearPortalCount = 0;
                foreach (var ai in otherAIs)
                {
                    if (ai != GetComponent<AIBrain>())
                    {
                        // 简化检查：如果其他AI也有类似目标，认为在协作
                        var goalSystem = ai.GetComponent<AIGoalSystem>();
                        if (goalSystem != null && goalSystem.CurrentHighLevelGoal?.Type == GoalType.Progression)
                            nearPortalCount++;
                    }
                }
                
                if (nearPortalCount >= 2)  // 至少2个AI在协作
                    priority += 0.3f;
            }
            
            // 基于领导力和社交性
            priority *= (leadership + sociability) / 2f;
            
            return Mathf.Clamp01(priority);
        }
        
        private float CalculateResourcePriority()
        {
            // 保留原有方法，但重命名为经营优先级
            return CalculateEconomicPriority();
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
        
        /// <summary>
        /// 获取平均生存状态
        /// </summary>
        private float GetAverageSurvivalRatio()
        {
            if (aiStats == null || aiStats.Config == null) return 0.5f;
            
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            return (healthRatio + hungerRatio + thirstRatio) / 3f;
        }
        
        private float CalculateSocialPriority()
        {
            // 基于孤独感和社交性
            float loneliness = -aiStats.GetMood(MoodDimension.Social) / 100f; // 转换为0-1
            return Mathf.Clamp01(loneliness * sociability);
        }
        
        // 战术选择方法 - 生存经营导向
        private AIGoal SelectSurvivalTactic()
        {
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            int currentGold = GetComponent<Inventory.Managers.CurrencyManager>()?.CurrentGold ?? 0;
            
            // 1. 生命危险 - 最高优先级
            if (healthRatio < 0.2f)
            {
                if (currentGold >= 30) // 有钱买治疗
                    return new AIGoal(GoalType.Trade, "找医生治疗");
                else
                    return new AIGoal(GoalType.ResourceManagement, "使用治疗物品");
            }
            
            // 2. 严重脱水 - 次高优先级
            if (thirstRatio < 0.15f)
            {
                // 优先找免费的泉水
                return new AIGoal(GoalType.Exploration, "寻找泉水");
            }
            
            // 3. 严重饥饿
            if (hungerRatio < 0.15f)
            {
                if (currentGold >= 20) // 有钱买食物
                    return new AIGoal(GoalType.Trade, "找餐厅吃饭");
                else
                    return new AIGoal(GoalType.ResourceManagement, "使用食物物品");
            }
            
            // 4. 预防性生存管理
            if (healthRatio < 0.5f || hungerRatio < 0.4f || thirstRatio < 0.4f)
            {
                if (currentGold < 50) // 钱不够，先赚钱
                    return new AIGoal(GoalType.Combat, "打怪赚钱");
                else
                    return new AIGoal(GoalType.Trade, "预防性补给");
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
            int currentGold = GetComponent<Inventory.Managers.CurrencyManager>()?.CurrentGold ?? 0;
            float inventoryFullness = GetInventoryFullness();
            var enemies = perception?.GetNearbyEnemies();
            var nearbyAIs = FindObjectsOfType<AITradeManager>()
                .Where(ai => ai != GetComponent<AITradeManager>() && 
                       Vector2.Distance(transform.position, ai.transform.position) < 5f)
                .ToList();
            
            // 1. 背包管理优先
            if (inventoryFullness > 0.8f)
            {
                // 优先考虑AI间交易清理背包
                if (nearbyAIs.Count > 0 && HasTradableItems())
                    return new AIGoal(GoalType.Trade, "与AI交易清理背包");
                else if (currentGold > 100) // 有钱扩容
                    return new AIGoal(GoalType.Trade, "找裁缝扩容背包");
                else
                    return new AIGoal(GoalType.ResourceManagement, "整理背包卖物品");
            }
            
            // 2. AI间资源互助
            if (NeedUrgentSupplies() && nearbyAIs.Count > 0)
            {
                return new AIGoal(GoalType.Trade, "与AI交易紧急补给");
            }
            
            // 3. 主动赚钱策略
            if (currentGold < 200) // 目标金币未达成
            {
                if (enemies != null && enemies.Count > 0 && 
                    aiStats.CurrentHealth > aiStats.Config.maxHealth * 0.6f)
                {
                    return new AIGoal(GoalType.Combat, "打怪赚金币");
                }
                else
                {
                    return new AIGoal(GoalType.Exploration, "寻找怪物房间");
                }
            }
            
            // 4. 投资和提升
            if (currentGold >= 200)
            {
                // 先考虑AI间武器交易
                if (nearbyAIs.Count > 0 && NeedWeaponUpgrade())
                    return new AIGoal(GoalType.Trade, "与AI交易更好的武器");
                else
                    return new AIGoal(GoalType.Trade, "投资装备提升效率");
            }
            
            // 5. 被动收集
            return new AIGoal(GoalType.Exploration, "寻找宝箱和物品");
        }
        
        /// <summary>
        /// 检查是否有可交易的物品
        /// </summary>
        private bool HasTradableItems()
        {
            if (inventory == null) return false;
            
            // 检查是否有多余的消耗品或非关键装备
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    // 如果有多个同类物品，可以交易
                    if (slot.Quantity > 1) return true;
                    
                    // 如果背包很满，任何物品都可以考虑交易
                    if (GetInventoryFullness() > 0.8f) return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 检查是否需要紧急补给
        /// </summary>
        private bool NeedUrgentSupplies()
        {
            if (aiStats == null || aiStats.Config == null) return false;
            
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            // 任何属性低于30%就是紧急状态
            return healthRatio < 0.3f || hungerRatio < 0.3f || thirstRatio < 0.3f;
        }
        
        /// <summary>
        /// 检查是否需要武器升级
        /// </summary>
        private bool NeedWeaponUpgrade()
        {
            var currentWeapon = inventory?.EquippedWeapon;
            if (currentWeapon == null) return true;
            
            // 简单检查：如果武器伤害低于20，需要升级
            return currentWeapon.Damage < 20f;
        }
        
        private AIGoal SelectSocialTactic()
        {
            var otherAIs = FindObjectsOfType<AICommunicator>()
                .Where(c => c != communicator && c.gameObject != gameObject)
                .ToList();
            
            float socialMood = aiStats.GetMood(MoodDimension.Social);
            int currentGold = GetComponent<Inventory.Managers.CurrencyManager>()?.CurrentGold ?? 0;
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            
            // 1. 紧急求助
            if ((healthRatio < 0.3f || currentGold < 20) && socialMood < 0)
            {
                return new AIGoal(GoalType.Communication, "发送求救信号");
            }
            
            // 2. 分享重要发现
            var nearbyNPCs = perception?.GetNearbyNPCs();
            if (nearbyNPCs != null && nearbyNPCs.Count > 0)
            {
                return new AIGoal(GoalType.Communication, "分享NPC位置信息");
            }
            
            // 3. 面对面交流缓解孤独
            var nearbyAI = otherAIs.FirstOrDefault(ai => 
                Vector2.Distance(transform.position, ai.transform.position) < 5f);
            if (nearbyAI != null && socialMood < -20f)
            {
                return new AIGoal(GoalType.Communication, "面对面交流缓解孤独");
            }
            
            // 4. 战斗协作
            var enemies = perception?.GetNearbyEnemies();
            if (enemies != null && enemies.Count > 1 && healthRatio > 0.6f)
            {
                return new AIGoal(GoalType.Communication, "邀请队友协作战斗");
            }
            
            // 5. 传送门协作
            if (memory != null && memory.KnowsPortalLocation())
            {
                return new AIGoal(GoalType.Communication, "协调传送门激活");
            }
            
            // 6. 定期维护社交关系
            return new AIGoal(GoalType.Communication, "维护队友关系");
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