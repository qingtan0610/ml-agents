using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AI.Stats;
using AI.Core;
using Inventory;
using Inventory.Managers;
using System.Linq;

namespace AI.Decision
{
    /// <summary>
    /// DeepSeek API客户端
    /// </summary>
    public class DeepSeekAPIClient : MonoBehaviour
    {
        private static DeepSeekAPIClient instance;
        public static DeepSeekAPIClient Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("DeepSeekAPIClient");
                    instance = go.AddComponent<DeepSeekAPIClient>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        
        private DeepSeekConfig config;
        private Dictionary<string, (string response, float timestamp)> responseCache = new Dictionary<string, (string, float)>();
        private int requestsThisMinute = 0;
        private float minuteResetTime;
        private int requestsToday = 0;
        private float dayResetTime;
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 加载配置
            LoadConfig();
            
            // 初始化计时器
            minuteResetTime = Time.time + 60f;
            dayResetTime = Time.time + 86400f;
        }
        
        private void LoadConfig()
        {
            // 尝试从Resources加载配置
            config = Resources.Load<DeepSeekConfig>("DeepSeekConfig");
            
            if (config == null)
            {
                Debug.LogWarning("[DeepSeekAPI] 未找到配置文件，尝试从项目中查找");
                
                #if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DeepSeekConfig");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = UnityEditor.AssetDatabase.LoadAssetAtPath<DeepSeekConfig>(path);
                }
                #endif
            }
            
            if (config == null)
            {
                Debug.LogError("[DeepSeekAPI] 无法加载DeepSeekConfig配置文件！");
            }
        }
        
        /// <summary>
        /// 发送决策请求
        /// </summary>
        public void RequestDecision(AIDecisionContext context, Action<AIDecision> callback)
        {
            if (!CheckRateLimit())
            {
                Debug.LogWarning("[DeepSeekAPI] 达到请求速率限制");
                callback?.Invoke(GetDefaultDecision());
                return;
            }
            
            string prompt = BuildDecisionPrompt(context);
            string cacheKey = $"decision_{prompt.GetHashCode()}";
            
            // 检查缓存
            if (TryGetCachedResponse(cacheKey, out string cachedResponse))
            {
                var decision = ParseDecisionResponse(cachedResponse);
                callback?.Invoke(decision);
                return;
            }
            
            // 构建消息
            var messages = new List<DeepSeekRequest.Message>
            {
                new DeepSeekRequest.Message("system", config.systemPrompt),
                new DeepSeekRequest.Message("user", prompt)
            };
            
            StartCoroutine(SendRequest(messages, response =>
            {
                if (response != null)
                {
                    CacheResponse(cacheKey, response);
                    var decision = ParseDecisionResponse(response);
                    callback?.Invoke(decision);
                }
                else
                {
                    callback?.Invoke(GetDefaultDecision());
                }
            }));
        }
        
        /// <summary>
        /// 发送对话请求
        /// </summary>
        public void RequestDialogue(string speaker, string context, string previousDialogue, Action<string> callback)
        {
            if (!CheckRateLimit())
            {
                callback?.Invoke("...");
                return;
            }
            
            string prompt = BuildDialoguePrompt(speaker, context, previousDialogue);
            
            var messages = new List<DeepSeekRequest.Message>
            {
                new DeepSeekRequest.Message("system", "你是一个Roguelike游戏中的AI角色，需要根据当前情况生成合适的对话。保持角色个性，对话要简短自然。"),
                new DeepSeekRequest.Message("user", prompt)
            };
            
            StartCoroutine(SendRequest(messages, response =>
            {
                if (response != null)
                {
                    var dialogueResponse = ParseDialogueResponse(response);
                    callback?.Invoke(dialogueResponse.message);
                }
                else
                {
                    callback?.Invoke("...");
                }
            }));
        }
        
        /// <summary>
        /// 请求NPC交易决策
        /// </summary>
        public void RequestTradeDecision(AITradeContext context, Action<AITradeDecision> callback)
        {
            if (!CheckRateLimit())
            {
                Debug.LogWarning("[DeepSeekAPI] 达到请求速率限制");
                callback?.Invoke(GetDefaultTradeDecision());
                return;
            }
            
            string prompt = BuildTradePrompt(context);
            string cacheKey = $"trade_{prompt.GetHashCode()}";
            
            // 检查缓存
            if (TryGetCachedResponse(cacheKey, out string cachedResponse))
            {
                var decision = ParseTradeResponse(cachedResponse);
                callback?.Invoke(decision);
                return;
            }
            
            // 构建消息
            var messages = new List<DeepSeekRequest.Message>
            {
                new DeepSeekRequest.Message("system", config.systemPrompt),
                new DeepSeekRequest.Message("user", prompt)
            };
            
            StartCoroutine(SendRequest(messages, response =>
            {
                if (response != null)
                {
                    CacheResponse(cacheKey, response);
                    var decision = ParseTradeResponse(response);
                    callback?.Invoke(decision);
                }
                else
                {
                    callback?.Invoke(GetDefaultTradeDecision());
                }
            }));
        }
        
        private IEnumerator SendRequest(List<DeepSeekRequest.Message> messages, Action<string> callback)
        {
            if (config == null || !config.IsValid())
            {
                Debug.LogError("[DeepSeekAPI] 配置无效或API密钥未设置");
                callback?.Invoke(null);
                yield break;
            }
            
            // 添加全局超时保护
            float startTime = Time.time;
            float maxWaitTime = 30f; // 最多等待30秒
            
            var request = new DeepSeekRequest
            {
                model = config.GetModel(),
                messages = messages,
                temperature = config.temperature,
                max_tokens = config.maxTokens,
                top_p = config.topP,
                frequency_penalty = config.frequencyPenalty,
                presence_penalty = config.presencePenalty,
                stream = false
            };
            
            string jsonRequest = JsonUtility.ToJson(request);
            Debug.Log($"[DeepSeekAPI] 发送请求: {jsonRequest}");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
            
            // 检查全局超时
            if (Time.time - startTime > maxWaitTime)
            {
                Debug.LogError("[DeepSeekAPI] 全局超时，取消请求");
                callback?.Invoke(null);
                yield break;
            }
            
            int retryCount = 0;
            while (retryCount < config.maxRetries)
            {
                using (UnityWebRequest webRequest = new UnityWebRequest(config.GetApiEndpoint(), "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Authorization", $"Bearer {config.GetApiKey()}");
                    webRequest.timeout = (int)config.requestTimeout;
                    
                    yield return webRequest.SendWebRequest();
                    
                    // 再次检查全局超时
                    if (Time.time - startTime > maxWaitTime)
                    {
                        Debug.LogError("[DeepSeekAPI] 请求超时，中断请求");
                        callback?.Invoke(null);
                        yield break;
                    }
                    
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            Debug.Log($"[DeepSeekAPI] 原始响应: {webRequest.downloadHandler.text}");
                            
                            var response = JsonUtility.FromJson<DeepSeekResponse>(webRequest.downloadHandler.text);
                            
                            if (response == null)
                            {
                                Debug.LogError($"[DeepSeekAPI] JSON解析失败，原始响应: {webRequest.downloadHandler.text}");
                                callback?.Invoke(null);
                                yield break;
                            }
                            
                            // 检查是否有错误响应
                            if (response.error != null && !string.IsNullOrEmpty(response.error.message))
                            {
                                Debug.LogError($"[DeepSeekAPI] API错误: {response.error.message}");
                                Debug.LogError($"[DeepSeekAPI] 错误类型: {response.error.type}, 代码: {response.error.code}");
                                callback?.Invoke(null);
                                yield break;
                            }
                            
                            // 检查是否有有效的响应
                            if (response.choices != null && response.choices.Count > 0)
                            {
                                string content = response.choices[0].message.content;
                                Debug.Log($"[DeepSeekAPI] 收到响应: {content}");
                                
                                // 更新请求计数
                                requestsThisMinute++;
                                requestsToday++;
                                
                                callback?.Invoke(content);
                                yield break;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[DeepSeekAPI] 解析响应失败: {e.Message}");
                            Debug.LogError($"原始响应: {webRequest.downloadHandler.text}");
                            callback?.Invoke(null);
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[DeepSeekAPI] 请求失败: {webRequest.error}");
                        Debug.LogError($"HTTP状态码: {webRequest.responseCode}");
                        
                        if (webRequest.responseCode == 429) // Rate limit
                        {
                            Debug.LogWarning("[DeepSeekAPI] 触发API速率限制，等待重试...");
                            yield return new WaitForSeconds(config.retryDelay * (retryCount + 1));
                        }
                        else
                        {
                            // 非速率限制的错误，直接返回错误
                            Debug.LogError($"[DeepSeekAPI] 非速率限制错误，停止重试");
                            callback?.Invoke(null);
                            yield break;
                        }
                    }
                }
                
                retryCount++;
                if (retryCount < config.maxRetries)
                {
                    // 检查是否超时
                    if (Time.time - startTime > maxWaitTime)
                    {
                        Debug.LogError("[DeepSeekAPI] 重试超时，停止请求");
                        callback?.Invoke(null);
                        yield break;
                    }
                    yield return new WaitForSeconds(config.retryDelay);
                }
            }
            
            Debug.LogError($"[DeepSeekAPI] 重试{config.maxRetries}次后仍然失败");
            callback?.Invoke(null);
        }
        
        private bool CheckRateLimit()
        {
            // 重置分钟计数器
            if (Time.time > minuteResetTime)
            {
                requestsThisMinute = 0;
                minuteResetTime = Time.time + 60f;
            }
            
            // 重置每日计数器
            if (Time.time > dayResetTime)
            {
                requestsToday = 0;
                dayResetTime = Time.time + 86400f;
            }
            
            return requestsThisMinute < config.maxRequestsPerMinute && 
                   requestsToday < config.maxRequestsPerDay;
        }
        
        private bool TryGetCachedResponse(string key, out string response)
        {
            if (responseCache.ContainsKey(key))
            {
                var cached = responseCache[key];
                if (Time.time - cached.timestamp < config.cacheDuration)
                {
                    response = cached.response;
                    Debug.Log("[DeepSeekAPI] 使用缓存响应");
                    return true;
                }
                else
                {
                    responseCache.Remove(key);
                }
            }
            
            response = null;
            return false;
        }
        
        private void CacheResponse(string key, string response)
        {
            responseCache[key] = (response, Time.time);
        }
        
        private string BuildDecisionPrompt(AIDecisionContext context)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("当前AI状态和环境：");
            
            // 清晰地表示状态
            float healthPercent = context.Stats.CurrentHealth / context.Stats.Config.maxHealth;
            float hungerPercent = context.Stats.CurrentHunger / context.Stats.Config.maxHunger;
            float thirstPercent = context.Stats.CurrentThirst / context.Stats.Config.maxThirst;
            float staminaPercent = context.Stats.CurrentStamina / context.Stats.Config.maxStamina;
            
            sb.AppendLine($"生命值: {context.Stats.CurrentHealth}/{context.Stats.Config.maxHealth} ({healthPercent:P0}) - " +
                         (healthPercent < 0.3f ? "危险！需要治疗" : healthPercent < 0.6f ? "受伤" : "健康"));
            
            sb.AppendLine($"饥饿度: {context.Stats.CurrentHunger}/{context.Stats.Config.maxHunger} ({hungerPercent:P0}) - " +
                         (hungerPercent < 0.3f ? "非常饥饿！需要立即进食" : hungerPercent < 0.6f ? "有些饿" : "吃饱了"));
            
            sb.AppendLine($"口渴度: {context.Stats.CurrentThirst}/{context.Stats.Config.maxThirst} ({thirstPercent:P0}) - " +
                         (thirstPercent < 0.3f ? "非常口渴！需要立即喝水" : thirstPercent < 0.6f ? "有些渴" : "不渴"));
            
            sb.AppendLine($"体力值: {context.Stats.CurrentStamina}/{context.Stats.Config.maxStamina} ({staminaPercent:P0}) - " +
                         (staminaPercent < 0.3f ? "精疲力竭！需要休息（停止移动）" : staminaPercent < 0.6f ? "有些累" : "精力充沛"));
            
            // 获取金币信息（通过CurrencyManager）
            var currencyManager = context.Stats.GetComponent<CurrencyManager>();
            int gold = currencyManager != null ? currencyManager.CurrentGold : 0;
            sb.AppendLine($"金币: {gold}");
            
            var emotionValue = context.Stats.GetMood(MoodDimension.Emotion);
            var socialValue = context.Stats.GetMood(MoodDimension.Social);
            var mentalValue = context.Stats.GetMood(MoodDimension.Mentality);
            sb.AppendLine($"心情: 情绪{emotionValue:F0} 社交{socialValue:F0} 心态{mentalValue:F0}");
            
            sb.AppendLine($"附近敌人: {context.NearbyEnemies.Count}个");
            if (context.NearbyNPCs.Count > 0)
            {
                sb.Append("附近NPC: ");
                foreach (var npc in context.NearbyNPCs)
                {
                    sb.Append($"{npc.NPCType} ");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"可见物品: {context.NearbyItems.Count}个");
            
            // 添加通信信息
            var communicator = context.Stats.GetComponent<AICommunicator>();
            if (communicator != null)
            {
                var helpMsg = communicator.GetLatestMessage(CommunicationType.Help);
                var comeHereMsg = communicator.GetLatestMessage(CommunicationType.ComeHere);
                var portalMsg = communicator.GetLatestMessage(CommunicationType.FoundPortal);
                var waterMsg = communicator.GetLatestMessage(CommunicationType.FoundWater);
                var npcMsg = communicator.GetLatestMessage(CommunicationType.FoundNPC);
                
                // 只有真的有消息时才显示
                bool hasAnyMessage = helpMsg != null || comeHereMsg != null || portalMsg != null || 
                                   waterMsg != null || npcMsg != null;
                
                if (hasAnyMessage)
                {
                    sb.AppendLine("\n收到的交互机消息:");
                    if (helpMsg != null) 
                    {
                        // 详细调试信息
                        Debug.Log($"[DeepSeekAPI] 检测到Help消息: 发送者={helpMsg.Sender?.name ?? "Unknown"}, " +
                                $"发送者存在={helpMsg.Sender != null}, 发送者是自己={helpMsg.Sender == context.Stats.GetComponent<AICommunicator>()}, " +
                                $"时间={helpMsg.Timestamp}, 当前时间={Time.time}, 时间差={Time.time - helpMsg.Timestamp:F1}秒");
                        
                        // 如果发送者是自己，不显示求救信息
                        if (helpMsg.Sender != communicator)
                        {
                            sb.AppendLine("- 有AI求救");
                        }
                        else
                        {
                            Debug.Log("[DeepSeekAPI] Help消息是自己发送的，忽略");
                        }
                    }
                    if (comeHereMsg != null && comeHereMsg.Sender != communicator) sb.AppendLine("- 有AI请求支援");
                    if (portalMsg != null && portalMsg.Sender != communicator) sb.AppendLine("- 发现了传送门位置");
                    if (waterMsg != null && waterMsg.Sender != communicator) sb.AppendLine("- 发现了水源位置");
                    if (npcMsg != null && npcMsg.Sender != communicator) sb.AppendLine("- 发现了NPC位置");
                }
            }
            
            sb.AppendLine("\n请分析情况并提供决策建议。回复格式：");
            sb.AppendLine("状态: [Exploring/Fighting/Fleeing/Seeking/Interacting/Communicating/Resting/Critical]");
            sb.AppendLine("优先级: [Survival/Combat/Exploration/Normal]");
            sb.AppendLine("理由: [一句话说明]");
            sb.AppendLine("行动: [行动1, 行动2, ...]");
            
            return sb.ToString();
        }
        
        private string BuildDialoguePrompt(string speaker, string context, string previousDialogue)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"角色: {speaker}");
            sb.AppendLine($"情境: {context}");
            if (!string.IsNullOrEmpty(previousDialogue))
            {
                sb.AppendLine($"之前的对话: {previousDialogue}");
            }
            sb.AppendLine("\n生成一句符合当前情境的对话，要求：");
            sb.AppendLine("1. 简短自然（10-30字）");
            sb.AppendLine("2. 符合角色性格");
            sb.AppendLine("3. 体现当前情绪");
            sb.AppendLine("\n回复格式：");
            sb.AppendLine("对话: [对话内容]");
            sb.AppendLine("情绪: [开心/平静/紧张/恐惧/愤怒]");
            sb.AppendLine("意图: [问候/求助/交易/分享信息/警告]");
            
            return sb.ToString();
        }
        
        private AIDecision ParseDecisionResponse(string response)
        {
            var decision = new AIDecision();
            
            try
            {
                // 简单的文本解析
                string[] lines = response.Split('\n');
                foreach (string line in lines)
                {
                    if (line.StartsWith("状态:") || line.StartsWith("State:"))
                    {
                        string stateStr = line.Substring(line.IndexOf(':') + 1).Trim();
                        Enum.TryParse<AIState>(stateStr, out decision.RecommendedState);
                    }
                    else if (line.StartsWith("优先级:") || line.StartsWith("Priority:"))
                    {
                        string priorityStr = line.Substring(line.IndexOf(':') + 1).Trim();
                        Enum.TryParse<AIActionPriority>(priorityStr, out decision.Priority);
                    }
                    else if (line.StartsWith("理由:") || line.StartsWith("Reasoning:"))
                    {
                        decision.Explanation = line.Substring(line.IndexOf(':') + 1).Trim();
                    }
                    else if (line.StartsWith("行动:") || line.StartsWith("Actions:"))
                    {
                        string actionsStr = line.Substring(line.IndexOf(':') + 1).Trim();
                        actionsStr = actionsStr.Trim('[', ']');
                        string[] actions = actionsStr.Split(',');
                        decision.SpecificActions = new List<string>();
                        foreach (string action in actions)
                        {
                            decision.SpecificActions.Add(action.Trim());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeekAPI] 解析决策响应失败: {e.Message}");
                return GetDefaultDecision();
            }
            
            // 验证决策
            if (decision.SpecificActions == null || decision.SpecificActions.Count == 0)
            {
                decision.SpecificActions = new List<string> { "继续当前行动" };
            }
            
            return decision;
        }
        
        private DeepSeekDialogueResponse ParseDialogueResponse(string response)
        {
            var dialogueResponse = new DeepSeekDialogueResponse();
            
            try
            {
                string[] lines = response.Split('\n');
                foreach (string line in lines)
                {
                    if (line.StartsWith("对话:") || line.StartsWith("Dialogue:"))
                    {
                        dialogueResponse.message = line.Substring(line.IndexOf(':') + 1).Trim();
                    }
                    else if (line.StartsWith("情绪:") || line.StartsWith("Emotion:"))
                    {
                        dialogueResponse.emotion = line.Substring(line.IndexOf(':') + 1).Trim();
                    }
                    else if (line.StartsWith("意图:") || line.StartsWith("Intent:"))
                    {
                        dialogueResponse.intent = line.Substring(line.IndexOf(':') + 1).Trim();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeekAPI] 解析对话响应失败: {e.Message}");
                dialogueResponse.message = "...";
                dialogueResponse.emotion = "平静";
                dialogueResponse.intent = "无";
            }
            
            if (string.IsNullOrEmpty(dialogueResponse.message))
            {
                dialogueResponse.message = "...";
            }
            
            return dialogueResponse;
        }
        
        private AIDecision GetDefaultDecision()
        {
            return new AIDecision
            {
                RecommendedState = AIState.Exploring,
                Priority = AIActionPriority.Normal,
                Explanation = "默认决策",
                SpecificActions = new List<string> { "继续探索" }
            };
        }
        
        private string BuildTradePrompt(AITradeContext context)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("【交易决策场景】");
            sb.AppendLine($"NPC类型: {context.NPCType}");
            sb.AppendLine($"AI当前金币: {context.CurrentGold}");
            
            // 当前状态
            float healthPercent = context.CurrentHealth / context.MaxHealth;
            float hungerPercent = context.CurrentHunger / context.MaxHunger;
            float thirstPercent = context.CurrentThirst / context.MaxThirst;
            
            sb.AppendLine($"\n【AI当前状态】");
            sb.AppendLine($"生命: {healthPercent:P0} - {(healthPercent < 0.3f ? "需要治疗!" : "状态良好")}");
            sb.AppendLine($"饥饿: {hungerPercent:P0} - {(hungerPercent < 0.3f ? "需要食物!" : "不饿")}");
            sb.AppendLine($"口渴: {thirstPercent:P0} - {(thirstPercent < 0.3f ? "需要水!" : "不渴")}");
            
            // 背包物品
            if (context.InventoryItems != null && context.InventoryItems.Count > 0)
            {
                sb.AppendLine($"\n【背包物品】 (容量: {context.InventoryItems.Count}/{context.InventoryCapacity})");
                foreach (var item in context.InventoryItems)
                {
                    sb.AppendLine($"- {item.ItemName} x{item.Quantity} (估值: {item.BasePrice * item.Quantity}金币)");
                }
            }
            
            // 商店物品（商人）
            if (context.NPCType == "Merchant" && context.ShopItems != null)
            {
                sb.AppendLine($"\n【商店物品】");
                foreach (var item in context.ShopItems)
                {
                    sb.AppendLine($"- {item.ItemName}: {item.Price}金币 {(item.IsOnSale ? "[特价]" : "")}");
                }
            }
            
            // 服务项目（医生、餐厅等）
            if (context.Services != null && context.Services.Count > 0)
            {
                sb.AppendLine($"\n【可用服务】");
                foreach (var service in context.Services)
                {
                    sb.AppendLine($"- {service.Name}: {service.Price}金币 - {service.Description}");
                }
            }
            
            sb.AppendLine($"\n【决策需求】");
            sb.AppendLine("请根据以上信息决定：");
            sb.AppendLine("1. 是否进行交易");
            sb.AppendLine("2. 买什么/卖什么/使用什么服务");
            sb.AppendLine("3. 数量和优先级");
            
            sb.AppendLine($"\n回复格式：");
            sb.AppendLine("决定: [交易/不交易]");
            sb.AppendLine("类型: [购买/出售/服务/无]");
            sb.AppendLine("物品: [物品名称或服务名称]");
            sb.AppendLine("数量: [数字]");
            sb.AppendLine("理由: [一句话说明]");
            
            return sb.ToString();
        }
        
        private AITradeDecision ParseTradeResponse(string response)
        {
            var decision = new AITradeDecision();
            
            try
            {
                string[] lines = response.Split('\n');
                foreach (string line in lines)
                {
                    if (line.StartsWith("决定:") || line.StartsWith("Decision:"))
                    {
                        decision.ShouldTrade = line.Contains("交易") || line.Contains("Trade") || line.Contains("Yes");
                    }
                    else if (line.StartsWith("类型:") || line.StartsWith("Type:"))
                    {
                        string typeStr = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (typeStr.Contains("购买") || typeStr.Contains("Buy"))
                            decision.TradeType = TradeType.Buy;
                        else if (typeStr.Contains("出售") || typeStr.Contains("Sell"))
                            decision.TradeType = TradeType.Sell;
                        else if (typeStr.Contains("服务") || typeStr.Contains("Service"))
                            decision.TradeType = TradeType.Service;
                    }
                    else if (line.StartsWith("物品:") || line.StartsWith("Item:"))
                    {
                        decision.ItemOrServiceName = line.Substring(line.IndexOf(':') + 1).Trim();
                    }
                    else if (line.StartsWith("数量:") || line.StartsWith("Quantity:"))
                    {
                        string quantityStr = line.Substring(line.IndexOf(':') + 1).Trim();
                        int.TryParse(quantityStr, out decision.Quantity);
                    }
                    else if (line.StartsWith("理由:") || line.StartsWith("Reason:"))
                    {
                        decision.Reasoning = line.Substring(line.IndexOf(':') + 1).Trim();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeekAPI] 解析交易响应失败: {e.Message}");
                return GetDefaultTradeDecision();
            }
            
            return decision;
        }
        
        private AITradeDecision GetDefaultTradeDecision()
        {
            return new AITradeDecision
            {
                ShouldTrade = false,
                TradeType = TradeType.None,
                Reasoning = "保存资源"
            };
        }
    }
}