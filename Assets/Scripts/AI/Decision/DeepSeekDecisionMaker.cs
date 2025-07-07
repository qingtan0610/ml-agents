using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AI.Stats;
using AI.Perception;
using AI.Core;
using Rooms;

namespace AI.Decision
{
    /// <summary>
    /// DeepSeek API决策系统 - 在关键时刻提供高级决策
    /// </summary>
    public class DeepSeekDecisionMaker
    {
        private bool useRealAPI = true;  // 是否使用真实API
        
        /// <summary>
        /// 请求AI决策
        /// </summary>
        public void RequestDecision(AIDecisionContext context, System.Action<AIDecision> callback)
        {
            // 检查上下文是否有效
            if (context == null || context.Stats == null || context.Stats.IsDead)
            {
                Debug.LogWarning("[DeepSeekDecisionMaker] 决策上下文无效或AI已死亡");
                callback?.Invoke(null);
                return;
            }
            
            if (useRealAPI && DeepSeekAPIClient.Instance != null)
            {
                // 使用真实API
                DeepSeekAPIClient.Instance.RequestDecision(context, callback);
            }
            else
            {
                // 使用模拟决策
                SimulateAPIResponse(context, callback);
            }
        }
        
        /// <summary>
        /// 设置是否使用真实API
        /// </summary>
        public void SetUseRealAPI(bool useReal)
        {
            useRealAPI = useReal;
            Debug.Log($"[DeepSeek] 决策模式: {(useReal ? "真实API" : "模拟决策")}");
        }
        
        private string BuildPrompt(AIDecisionContext context)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("你是一个2D俯视角生存经营Roguelike游戏中的AI智能顾问。这是一个以生存经营为核心的合作游戏：");
            sb.AppendLine("🎮 游戏理念：生存第一，经营第二，打怪是为了赚钱，最终4人合作激活传送门");
            sb.AppendLine();
            
            // 生存状态
            float healthPercent = context.Stats.CurrentHealth / context.Stats.Config.maxHealth * 100;
            float hungerPercent = context.Stats.CurrentHunger / context.Stats.Config.maxHunger * 100;
            float thirstPercent = context.Stats.CurrentThirst / context.Stats.Config.maxThirst * 100;
            
            sb.AppendLine("📊【生存状态】");
            sb.AppendLine($"- 生命值: {context.Stats.CurrentHealth:F0}/{context.Stats.Config.maxHealth} ({healthPercent:F0}%)");
            sb.AppendLine($"- 饥饿度: {context.Stats.CurrentHunger:F0}/{context.Stats.Config.maxHunger} ({hungerPercent:F0}%)");
            sb.AppendLine($"- 口渴度: {context.Stats.CurrentThirst:F0}/{context.Stats.Config.maxThirst} ({thirstPercent:F0}%)");
            sb.AppendLine($"- 体力值: {context.Stats.CurrentStamina:F0}/{context.Stats.Config.maxStamina}");
            
            // 生存警报
            if (healthPercent < 30 || hungerPercent < 20 || thirstPercent < 20)
                sb.AppendLine("⚠️ 生存警报：某项生存指标严重不足！");
            sb.AppendLine();
            
            // 经营状态
            sb.AppendLine("💰【经营状态】");
            sb.AppendLine($"- 金币: {context.CurrentGold} (目标: 50基本生存, 200舒适, 500富裕)");
            
            if (context.Inventory != null)
            {
                int usedSlots = 0;
                for (int i = 0; i < context.Inventory.Size; i++)
                    if (!context.Inventory.GetSlot(i).IsEmpty) usedSlots++;
                float inventoryPercent = (float)usedSlots / context.Inventory.Size * 100;
                sb.AppendLine($"- 背包: {usedSlots}/{context.Inventory.Size} ({inventoryPercent:F0}%)");
                if (inventoryPercent > 80) sb.AppendLine("⚠️ 背包接近满载，考虑整理或扩容");
            }
            sb.AppendLine();
            
            // 心情影响效率
            sb.AppendLine("😊【心理状态】(影响工作效率)");
            var emotionValue = context.Stats.GetMood(MoodDimension.Emotion);
            var socialValue = context.Stats.GetMood(MoodDimension.Social);
            var mentalValue = context.Stats.GetMood(MoodDimension.Mentality);
            sb.AppendLine($"- 情绪: {emotionValue:F0}/100 (影响战斗和工作)");
            sb.AppendLine($"- 社交: {socialValue:F0}/100 (孤独降低效率)");
            sb.AppendLine($"- 心态: {mentalValue:F0}/100 (影响决策质量)");
            if (socialValue < -20) sb.AppendLine("💔 孤独感严重，建议与队友交流");
            sb.AppendLine();
            
            // 商业机会
            sb.AppendLine("🏪【周围机会】");
            if (context.NearbyEnemies.Count > 0)
                sb.AppendLine($"- 敌人: {context.NearbyEnemies.Count}个 (打怪赚钱机会)");
            if (context.NearbyNPCs.Count > 0)
                sb.AppendLine($"- 商家服务: {string.Join(", ", GetNPCTypes(context.NearbyNPCs))}");
            if (context.NearbyItems.Count > 0)
                sb.AppendLine($"- 可拾取物品: {context.NearbyItems.Count}个 (潜在收入)");
            sb.AppendLine($"- 可见房间: {context.VisibleRooms.Count}个");
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
            
            sb.AppendLine("🧠【决策指南】请基于生存经营理念给出最佳建议：");
            sb.AppendLine("1. 生存危机时：立即解决生存问题（找医生/泉水/餐厅）");
            sb.AppendLine("2. 生存稳定时：专注经营赚钱（打怪/收集/交易）");
            sb.AppendLine("3. 有闲钱时：投资效率提升（武器/背包扩容）");
            sb.AppendLine("4. 定期社交：缓解孤独，团队协作");
            sb.AppendLine("5. 最终目标：4人协作激活传送门");
            sb.AppendLine();
            sb.AppendLine("📝【回答格式】");
            sb.AppendLine("状态：[Critical/Economic/Exploring/Fighting/Seeking/Interacting/Communicating]");
            sb.AppendLine("优先级：[Survival/Economic/Cooperation/Development]");
            sb.AppendLine("理由：[基于生存经营的决策逻辑，简明扼要]");
            
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
        
        private void SimulateAPIResponse(AIDecisionContext context, System.Action<AIDecision> callback)
        {
            // 生存经营导向的智能决策
            AIDecision decision = new AIDecision();
            
            // 状态分析
            float healthPercent = context.Stats.CurrentHealth / context.Stats.Config.maxHealth;
            float hungerPercent = context.Stats.CurrentHunger / context.Stats.Config.maxHunger;
            float thirstPercent = context.Stats.CurrentThirst / context.Stats.Config.maxThirst;
            
            // 使用context中的金币信息
            int gold = context.CurrentGold;
            
            // 1. 生存危机 - 最高优先级
            if (healthPercent < 0.2f || hungerPercent < 0.15f || thirstPercent < 0.15f)
            {
                decision.RecommendedState = AIState.Critical;
                decision.Priority = AIActionPriority.Survival;
                
                if (healthPercent < 0.2f && gold >= 30)
                    decision.Explanation = "生命危险！有钱找医生治疗";
                else if (thirstPercent < 0.15f)
                    decision.Explanation = "严重脱水！寻找泉水或餐厅";
                else if (hungerPercent < 0.15f && gold >= 20)
                    decision.Explanation = "严重饥饿！去餐厅买食物";
                else
                    decision.Explanation = "生存危机！使用背包物品或求助队友";
            }
            // 2. 经营机会 - 状态良好时专注赚钱
            else if (healthPercent > 0.6f && hungerPercent > 0.4f && thirstPercent > 0.4f)
            {
                if (context.NearbyEnemies.Count > 0 && gold < 200)
                {
                    decision.RecommendedState = AIState.Fighting;
                    decision.Priority = AIActionPriority.Combat;
                    decision.Explanation = "状态良好，打怪赚钱是主要收入来源";
                }
                else if (gold >= 200 && context.NearbyNPCs.Count > 0)
                {
                    decision.RecommendedState = AIState.Interacting;
                    decision.Priority = AIActionPriority.Normal;
                    decision.Explanation = "有闲钱投资装备或背包扩容提升效率";
                }
                else
                {
                    decision.RecommendedState = AIState.Exploring;
                    decision.Priority = AIActionPriority.Normal;
                    decision.Explanation = "寻找赚钱机会：怪物房间或宝箱";
                }
            }
            // 3. 预防性维护
            else if (hungerPercent < 0.5f || thirstPercent < 0.5f)
            {
                if (gold >= 30)
                {
                    decision.RecommendedState = AIState.Seeking;
                    decision.Priority = AIActionPriority.Survival;
                    decision.Explanation = "预防性补给，维持良好状态保证工作效率";
                }
                else
                {
                    decision.RecommendedState = AIState.Fighting;
                    decision.Priority = AIActionPriority.Normal;
                    decision.Explanation = "钱不够，先赚钱再补给";
                }
            }
            // 4. 智能交流决策 - 重要改进
            else if (ShouldCommunicate(context))
            {
                var commType = DetermineOptimalCommunication(context);
                decision.RecommendedState = AIState.Communicating;
                decision.Priority = AIActionPriority.Normal;
                decision.Explanation = commType.explanation;
                decision.SpecificActions = new List<string> { commType.action };
            }
            // 5. 经营和探索
            else if (gold < 100) // 还需要赚钱
            {
                decision.RecommendedState = AIState.Exploring;
                decision.Priority = AIActionPriority.Normal;
                decision.Explanation = "寻找赚钱机会：怪物房间或宝箱";
            }
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
        
        /// <summary>
        /// 判断是否应该进行交流
        /// </summary>
        private bool ShouldCommunicate(AIDecisionContext context)
        {
            float socialMood = context.Stats.GetMood(AI.Stats.MoodDimension.Social);
            
            // 1. 孤独感严重时
            if (socialMood < -30f) return true;
            
            // 2. 发现重要信息需要分享
            if (HasImportantInfoToShare(context)) return true;
            
            // 3. 需要求助时
            if (NeedHelp(context)) return true;
            
            // 4. 定期社交维护（防止孤独累积）
            if (socialMood < 20f && UnityEngine.Random.value < 0.3f) return true;
            
            // 5. 合作时机（传送门相关）
            if (ShouldCooperate(context)) return true;
            
            return false;
        }
        
        /// <summary>
        /// 确定最优交流类型
        /// </summary>
        private (string action, string explanation) DetermineOptimalCommunication(AIDecisionContext context)
        {
            float healthPercent = context.Stats.CurrentHealth / context.Stats.Config.maxHealth;
            float socialMood = context.Stats.GetMood(AI.Stats.MoodDimension.Social);
            int gold = context.CurrentGold;
            
            // 0. 首先检查收到的消息并做出回应
            if (context.RecentCommunications != null && context.RecentCommunications.Count > 0)
            {
                // 收到求救信号
                var helpMsg = context.RecentCommunications.FirstOrDefault(c => 
                    c.MessageType == CommunicationType.Help && c.TimeSince < 30f);
                if (helpMsg != null)
                {
                    // 如果自己状态良好，应该去帮助
                    if (healthPercent > 0.5f && gold > 30)
                    {
                        // 记忆中添加目标位置
                        if (context.Memory != null)
                        {
                            context.Memory["HelpTarget"] = helpMsg.Position;
                        }
                        return ($"前往{helpMsg.SenderName}位置{helpMsg.Position}提供帮助", 
                                $"收到{helpMsg.SenderName}求救，立即前往援助");
                    }
                }
                
                // 收到传送门发现
                var portalMsg = context.RecentCommunications.FirstOrDefault(c => 
                    c.MessageType == CommunicationType.FoundPortal && c.TimeSince < 60f);
                if (portalMsg != null)
                {
                    // 这是极其重要的信息，立即响应
                    return ($"感谢{portalMsg.SenderName}分享传送门位置，立即前往{portalMsg.Position}", 
                            "收到传送门位置，这是最重要的信息");
                }
                
                // 收到水源位置且口渴
                var waterMsg = context.RecentCommunications.FirstOrDefault(c => 
                    c.MessageType == CommunicationType.FoundWater && c.TimeSince < 60f);
                if (waterMsg != null && context.Stats.CurrentThirst < context.Stats.Config.maxThirst * 0.5f)
                {
                    return ($"前往{waterMsg.SenderName}发现的水源{waterMsg.Position}", 
                            "口渴，感谢队友分享的水源位置");
                }
                
                // 收到NPC位置
                var npcMsg = context.RecentCommunications.FirstOrDefault(c => 
                    c.MessageType == CommunicationType.FoundNPC && c.TimeSince < 60f);
                if (npcMsg != null && (gold < 50 || healthPercent < 0.5f))
                {
                    return ($"前往{npcMsg.SenderName}发现的NPC位置{npcMsg.Position}", 
                            "需要补给或交易，前往NPC位置");
                }
            }
            
            // 1. 紧急求助
            if (healthPercent < 0.3f || gold < 20)
            {
                return ("发送Help求救信号", "生存困难，需要队友帮助");
            }
            
            // 2. 分享重要发现
            if (HasImportantInfoToShare(context))
            {
                // 发现传送门 - 最高优先级
                if (context.Memory != null && context.Memory.ContainsKey("PortalLocation"))
                {
                    return ("发送FoundPortal传送门位置", "发现传送门！立即告知所有队友");
                }
                
                // 发现水源
                var fountain = context.NearbyItems?.FirstOrDefault(i => i.name.Contains("Fountain"));
                if (fountain != null)
                {
                    return ("发送FoundWater水源位置", "发现泉水，分享给需要的队友");
                }
                
                // 发现NPC
                if (context.NearbyNPCs.Count > 0)
                {
                    var npcType = GetNPCTypes(context.NearbyNPCs)[0];
                    return ($"发送FoundNPC找到{npcType}的信息", $"发现{npcType}，通知队友");
                }
            }
            
            // 3. 邀请合作
            if (context.NearbyEnemies.Count > 1 && healthPercent > 0.6f)
            {
                return ("发送ComeHere邀请队友", "敌人较多，邀请队友协作战斗");
            }
            
            // 4. 位置协调
            if (socialMood < -20f && context.NearbyEnemies.Count == 0)
            {
                return ("寻找队友面对面交流", "感到孤独，需要面对面交流缓解");
            }
            
            // 5. 默认：位置分享
            return ("发送位置信息", "定期与队友保持联系");
        }
        
        /// <summary>
        /// 检查是否有重要信息需要分享
        /// </summary>
        private bool HasImportantInfoToShare(AIDecisionContext context)
        {
            // 发现了NPC
            if (context.NearbyNPCs.Count > 0) return true;
            
            // 发现了大量敌人
            if (context.NearbyEnemies.Count > 2) return true;
            
            // 发现了宝箱房间等（可以通过房间类型判断）
            // TODO: 添加房间类型检测
            
            return false;
        }
        
        /// <summary>
        /// 检查是否需要帮助
        /// </summary>
        private bool NeedHelp(AIDecisionContext context)
        {
            float healthPercent = context.Stats.CurrentHealth / context.Stats.Config.maxHealth;
            float hungerPercent = context.Stats.CurrentHunger / context.Stats.Config.maxHunger;
            float thirstPercent = context.Stats.CurrentThirst / context.Stats.Config.maxThirst;
            
            // 生存状态危险且没钱
            if ((healthPercent < 0.4f || hungerPercent < 0.3f || thirstPercent < 0.3f) && 
                context.CurrentGold < 30)
            {
                return true;
            }
            
            // 被多个敌人包围
            if (context.NearbyEnemies.Count > 2 && healthPercent < 0.6f)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查是否应该合作
        /// </summary>
        private bool ShouldCooperate(AIDecisionContext context)
        {
            // 如果知道传送门位置，应该协调队友
            if (context.Memory != null && context.Memory.ContainsKey("PortalLocation"))
            {
                return true;
            }
            
            // 如果发现了高价值区域
            if (context.NearbyNPCs.Count > 1) // 多个NPC的区域
            {
                return true;
            }
            
            return false;
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
        public Inventory.Inventory Inventory;
        public List<RoomInfo> VisibleRooms;
        public List<Enemy.Enemy2D> NearbyEnemies;
        public List<NPC.Core.NPCBase> NearbyNPCs;
        public List<GameObject> NearbyItems;
        public Dictionary<string, object> Memory;
        
        // 新增：生存经营相关信息
        public int CurrentGold;
        public GameObject SourceGameObject; // 用于获取其他组件
        
        // 新增：通信信息
        public List<RecentCommunication> RecentCommunications;
    }
    
    /// <summary>
    /// 最近的通信记录
    /// </summary>
    public class RecentCommunication
    {
        public string SenderName;
        public CommunicationType MessageType;
        public Vector2 Position;
        public float TimeSince;
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