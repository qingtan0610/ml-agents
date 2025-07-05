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
        
        private IEnumerator SendRequest(List<DeepSeekRequest.Message> messages, Action<string> callback)
        {
            if (config == null || !config.IsValid())
            {
                Debug.LogError("[DeepSeekAPI] 配置无效或API密钥未设置");
                callback?.Invoke(null);
                yield break;
            }
            
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
                            
                            if (response.error != null)
                            {
                                Debug.LogError($"[DeepSeekAPI] API错误: {response.error.message}");
                                Debug.LogError($"[DeepSeekAPI] 错误类型: {response.error.type}, 代码: {response.error.code}");
                                callback?.Invoke(null);
                                yield break;
                            }
                            
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
                    }
                }
                
                retryCount++;
                if (retryCount < config.maxRetries)
                {
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
            sb.AppendLine($"生命值: {context.Stats.CurrentHealth}/{context.Stats.Config.maxHealth}");
            sb.AppendLine($"饥饿度: {context.Stats.CurrentHunger}/{context.Stats.Config.maxHunger}");
            sb.AppendLine($"口渴度: {context.Stats.CurrentThirst}/{context.Stats.Config.maxThirst}");
            sb.AppendLine($"体力值: {context.Stats.CurrentStamina}/{context.Stats.Config.maxStamina}");
            
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
                
                sb.AppendLine("\n收到的交互机消息:");
                if (helpMsg != null) sb.AppendLine("- 有AI求救");
                if (comeHereMsg != null) sb.AppendLine("- 有AI请求支援");
                if (portalMsg != null) sb.AppendLine("- 发现了传送门位置");
                if (waterMsg != null) sb.AppendLine("- 发现了水源位置");
                if (npcMsg != null) sb.AppendLine("- 发现了NPC位置");
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
    }
}