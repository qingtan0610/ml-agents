using UnityEngine;
using System.Collections.Generic;
using System.Text;
using AI.Stats;
using AI.Perception;
using AI.Core;

namespace AI.Decision
{
    /// <summary>
    /// DeepSeek API决策系统 - 在关键时刻提供高级决策
    /// </summary>
    public class DeepSeekDecisionMaker
    {
        private const string API_ENDPOINT = "https://api.deepseek.com/v1/chat/completions";
        private string apiKey;
        
        // 决策回调
        public delegate void DecisionCallback(AIDecision decision);
        
        public DeepSeekDecisionMaker()
        {
            // 从配置或环境变量读取API密钥
            apiKey = PlayerPrefs.GetString("DeepSeekAPIKey", "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[DeepSeek] API密钥未设置，DeepSeek决策将不可用");
            }
        }
        
        /// <summary>
        /// 请求AI决策
        /// </summary>
        public void RequestDecision(AIDecisionContext context, DecisionCallback callback)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[DeepSeek] 无法请求决策：API密钥未设置");
                callback?.Invoke(GetDefaultDecision());
                return;
            }
            
            // 构建提示词
            string prompt = BuildPrompt(context);
            
            // TODO: 实际API调用实现
            // 这里需要使用UnityWebRequest或第三方HTTP库
            Debug.Log($"[DeepSeek] 请求决策，上下文：\n{prompt}");
            
            // 模拟返回
            SimulateAPIResponse(context, callback);
        }
        
        private string BuildPrompt(AIDecisionContext context)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("你是一个Roguelike游戏中的AI角色决策系统。基于以下情况，提供最佳行动建议：");
            sb.AppendLine();
            
            // 基本状态
            sb.AppendLine("【当前状态】");
            sb.AppendLine($"- 生命值: {context.Stats.CurrentHealth}/{context.Stats.Config.maxHealth}");
            sb.AppendLine($"- 饥饿度: {context.Stats.CurrentHunger}/{context.Stats.Config.maxHunger}");
            sb.AppendLine($"- 口渴度: {context.Stats.CurrentThirst}/{context.Stats.Config.maxThirst}");
            sb.AppendLine($"- 体力值: {context.Stats.CurrentStamina}/{context.Stats.Config.maxStamina}");
            sb.AppendLine();
            
            // 心情状态
            sb.AppendLine("【心情状态】");
            sb.AppendLine($"- 情绪: {context.Mood.EmotionalState:F2} (沮丧 <-> 开心)");
            sb.AppendLine($"- 社交: {context.Mood.SocialState:F2} (孤独 <-> 温暖)");
            sb.AppendLine($"- 心态: {context.Mood.MentalState:F2} (急躁 <-> 平静)");
            sb.AppendLine();
            
            // 环境信息
            sb.AppendLine("【环境感知】");
            sb.AppendLine($"- 可见房间数: {context.VisibleRooms.Count}");
            sb.AppendLine($"- 附近敌人数: {context.NearbyEnemies.Count}");
            sb.AppendLine($"- 附近NPC: {string.Join(", ", GetNPCTypes(context.NearbyNPCs))}");
            sb.AppendLine($"- 附近物品数: {context.NearbyItems.Count}");
            sb.AppendLine();
            
            // 记忆信息
            if (context.Memory != null && context.Memory.ContainsKey("ImportantLocations"))
            {
                sb.AppendLine("【重要记忆】");
                var locations = context.Memory["ImportantLocations"] as Dictionary<string, LocationMemory>;
                if (locations != null)
                {
                    foreach (var loc in locations)
                    {
                        sb.AppendLine($"- {loc.Key}: {loc.Value.Position}");
                    }
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("请根据以上信息，建议AI应该采取什么行动？优先考虑生存，然后是探索和发展。");
            sb.AppendLine("回答格式：");
            sb.AppendLine("状态：[Exploring/Fighting/Fleeing/Seeking/Interacting/Communicating/Resting/Critical]");
            sb.AppendLine("优先级：[Survival/Combat/Exploration]");
            sb.AppendLine("理由：[简短说明]");
            
            return sb.ToString();
        }
        
        private List<string> GetNPCTypes(List<NPC.Core.NPCBase> npcs)
        {
            var types = new List<string>();
            foreach (var npc in npcs)
            {
                types.Add(npc.NPCType.ToString());
            }
            return types;
        }
        
        private void SimulateAPIResponse(AIDecisionContext context, DecisionCallback callback)
        {
            // 模拟智能决策
            AIDecision decision = new AIDecision();
            
            // 基于状态分析
            float healthPercent = context.Stats.CurrentHealth / context.Stats.Config.maxHealth;
            float hungerPercent = context.Stats.CurrentHunger / context.Stats.Config.maxHunger;
            float thirstPercent = context.Stats.CurrentThirst / context.Stats.Config.maxThirst;
            
            // 危急状态判断
            if (healthPercent < 0.3f || hungerPercent < 0.2f || thirstPercent < 0.2f)
            {
                decision.RecommendedState = AIState.Critical;
                decision.Priority = AIActionPriority.Survival;
                decision.Explanation = "生命值或基础需求过低，需要立即寻找补给";
            }
            // 战斗状态判断
            else if (context.NearbyEnemies.Count > 0 && healthPercent > 0.5f)
            {
                decision.RecommendedState = AIState.Fighting;
                decision.Priority = AIActionPriority.Combat;
                decision.Explanation = "附近有敌人且状态良好，应该战斗";
            }
            // 寻找资源
            else if (hungerPercent < 0.5f || thirstPercent < 0.5f)
            {
                decision.RecommendedState = AIState.Seeking;
                decision.Priority = AIActionPriority.Survival;
                decision.Explanation = "需要补充食物或水分";
            }
            // 社交需求
            else if (context.Mood.SocialState < -0.5f)
            {
                decision.RecommendedState = AIState.Communicating;
                decision.Priority = AIActionPriority.Normal;
                decision.Explanation = "感到孤独，应该与其他AI交流";
            }
            // 探索
            else
            {
                decision.RecommendedState = AIState.Exploring;
                decision.Priority = AIActionPriority.Exploration;
                decision.Explanation = "状态良好，继续探索新区域";
            }
            
            // 具体行动建议
            decision.SpecificActions = GenerateSpecificActions(context, decision.RecommendedState);
            
            // 延迟模拟网络请求
            callback?.Invoke(decision);
        }
        
        private List<string> GenerateSpecificActions(AIDecisionContext context, AIState state)
        {
            var actions = new List<string>();
            
            switch (state)
            {
                case AIState.Critical:
                    actions.Add("立即使用背包中的恢复物品");
                    actions.Add("寻找最近的泉水或餐厅");
                    actions.Add("避免战斗，必要时逃跑");
                    actions.Add("向其他AI发送求救信号");
                    break;
                    
                case AIState.Seeking:
                    if (context.Memory != null)
                    {
                        actions.Add("前往记忆中的资源点");
                    }
                    actions.Add("探索新房间寻找补给");
                    actions.Add("与商人NPC交易");
                    break;
                    
                case AIState.Fighting:
                    actions.Add("优先攻击最近的敌人");
                    actions.Add("合理使用技能和道具");
                    actions.Add("保持移动避免被包围");
                    break;
                    
                case AIState.Communicating:
                    actions.Add("寻找其他AI进行面对面交流");
                    actions.Add("使用交互机分享有用信息");
                    actions.Add("跟随其他AI行动");
                    break;
                    
                case AIState.Exploring:
                    actions.Add("优先探索未知房间");
                    actions.Add("标记重要位置");
                    actions.Add("收集有价值的物品");
                    break;
            }
            
            return actions;
        }
        
        private AIDecision GetDefaultDecision()
        {
            return new AIDecision
            {
                RecommendedState = AIState.Exploring,
                Priority = AIActionPriority.Normal,
                Explanation = "默认决策：继续探索",
                SpecificActions = new List<string> { "探索未知区域", "收集资源" }
            };
        }
    }
    
    /// <summary>
    /// AI决策上下文
    /// </summary>
    public class AIDecisionContext
    {
        public AIStats Stats;
        public AIMood Mood;
        public Inventory.Inventory Inventory;
        public List<RoomInfo> VisibleRooms;
        public List<Enemy.Enemy2D> NearbyEnemies;
        public List<NPC.Core.NPCBase> NearbyNPCs;
        public List<GameObject> NearbyItems;
        public Dictionary<string, object> Memory;
    }
    
    /// <summary>
    /// AI决策结果
    /// </summary>
    public class AIDecision
    {
        public AIState RecommendedState;
        public AIActionPriority Priority;
        public string Explanation;
        public List<string> SpecificActions;
    }
}