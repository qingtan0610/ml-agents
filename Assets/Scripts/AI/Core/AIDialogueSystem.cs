using UnityEngine;
using System.Collections.Generic;
using AI.Decision;
using AI.Stats;
using AI.Perception;

namespace AI.Core
{
    /// <summary>
    /// AI对话系统 - 管理AI之间的对话交流
    /// </summary>
    public class AIDialogueSystem : MonoBehaviour
    {
        [Header("Dialogue Settings")]
        [SerializeField] private float dialogueRange = 3f;           // 对话距离
        [SerializeField] private float dialogueCooldown = 5f;        // 对话冷却时间
        [SerializeField] private bool useDeepSeekForDialogue = true; // 使用DeepSeek生成对话
        
        [Header("Visual Settings")]
        [SerializeField] private GameObject dialogueBubblePrefab;    // 对话气泡预制体
        [SerializeField] private float bubbleDuration = 3f;          // 气泡显示时间
        
        private AIStats aiStats;
        private string aiName;
        private Dictionary<string, float> lastDialogueTimes = new Dictionary<string, float>();
        private GameObject currentBubble;
        
        // 预设对话模板（当DeepSeek不可用时使用）
        private readonly Dictionary<string, List<string>> dialogueTemplates = new Dictionary<string, List<string>>
        {
            ["greeting"] = new List<string> 
            { 
                "你好！", 
                "嗨，朋友！", 
                "很高兴见到你。",
                "你还好吗？"
            },
            ["help"] = new List<string> 
            { 
                "我需要帮助！", 
                "快来帮我！", 
                "这里有危险！",
                "我快撑不住了..."
            },
            ["share_info"] = new List<string> 
            { 
                "我发现了一些有用的东西。", 
                "前面有个商人。", 
                "那边有敌人，小心！",
                "我找到了补给品。"
            },
            ["thanks"] = new List<string> 
            { 
                "谢谢你！", 
                "太感谢了！", 
                "你真是个好人。",
                "我欠你一个人情。"
            },
            ["warning"] = new List<string> 
            { 
                "小心！", 
                "快跑！", 
                "有危险！",
                "不要过去！"
            },
            ["lonely"] = new List<string>
            {
                "好孤单啊...",
                "有人在吗？",
                "我需要个伴。",
                "一起走吧？"
            }
        };
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            aiName = gameObject.name;
        }
        
        /// <summary>
        /// 尝试与附近的AI对话
        /// </summary>
        public void TryDialogueWithNearbyAI()
        {
            // 查找附近的AI
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, dialogueRange);
            
            foreach (var collider in colliders)
            {
                if (collider.gameObject == gameObject) continue;
                
                var otherDialogue = collider.GetComponent<AIDialogueSystem>();
                if (otherDialogue != null && CanDialogueWith(otherDialogue))
                {
                    InitiateDialogue(otherDialogue);
                    break;
                }
            }
        }
        
        /// <summary>
        /// 发起对话
        /// </summary>
        private void InitiateDialogue(AIDialogueSystem other)
        {
            string context = BuildDialogueContext(other);
            string dialogueType = DetermineDialogueType();
            
            if (useDeepSeekForDialogue && DeepSeekAPIClient.Instance != null)
            {
                // 使用DeepSeek生成对话
                DeepSeekAPIClient.Instance.RequestDialogue(aiName, context, "", (response) =>
                {
                    DisplayDialogue(response);
                    other.RespondToDialogue(this, response);
                });
            }
            else
            {
                // 使用预设模板
                string dialogue = GetTemplateDialogue(dialogueType);
                DisplayDialogue(dialogue);
                other.RespondToDialogue(this, dialogue);
            }
            
            // 更新对话时间
            lastDialogueTimes[other.aiName] = Time.time;
            
            // 提升社交心情
            aiStats.ModifyMood(MoodDimension.Social, 20f, StatChangeReason.Interact);
        }
        
        /// <summary>
        /// 回应对话
        /// </summary>
        public void RespondToDialogue(AIDialogueSystem initiator, string previousDialogue)
        {
            string context = $"对方说：{previousDialogue}";
            
            if (useDeepSeekForDialogue && DeepSeekAPIClient.Instance != null)
            {
                DeepSeekAPIClient.Instance.RequestDialogue(aiName, context, previousDialogue, (response) =>
                {
                    DisplayDialogue(response);
                });
            }
            else
            {
                // 简单回应
                string response = GetResponseDialogue(previousDialogue);
                DisplayDialogue(response);
            }
            
            // 更新对话时间
            lastDialogueTimes[initiator.aiName] = Time.time;
            
            // 提升社交心情
            aiStats.ModifyMood(MoodDimension.Social, 15f, StatChangeReason.Interact);
        }
        
        /// <summary>
        /// 显示对话气泡
        /// </summary>
        private void DisplayDialogue(string text)
        {
            Debug.Log($"[对话] {aiName}: {text}");
            
            // 如果有预制体，创建对话气泡
            if (dialogueBubblePrefab != null)
            {
                if (currentBubble != null)
                {
                    Destroy(currentBubble);
                }
                
                currentBubble = Instantiate(dialogueBubblePrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
                currentBubble.transform.SetParent(transform);
                
                // 设置文本（需要对话气泡预制体有Text组件）
                var textComponent = currentBubble.GetComponentInChildren<TMPro.TextMeshPro>();
                if (textComponent != null)
                {
                    textComponent.text = text;
                }
                
                // 定时销毁
                Destroy(currentBubble, bubbleDuration);
            }
        }
        
        /// <summary>
        /// 检查是否可以与目标对话
        /// </summary>
        private bool CanDialogueWith(AIDialogueSystem other)
        {
            string key = other.aiName;
            if (lastDialogueTimes.ContainsKey(key))
            {
                return Time.time - lastDialogueTimes[key] > dialogueCooldown;
            }
            return true;
        }
        
        /// <summary>
        /// 构建对话上下文
        /// </summary>
        private string BuildDialogueContext(AIDialogueSystem other)
        {
            var context = new System.Text.StringBuilder();
            
            // 自己的状态
            float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float socialMood = aiStats.GetMood(MoodDimension.Social);
            
            context.AppendLine($"我的状态：生命{healthPercent:P0}，社交心情{socialMood:F0}");
            
            // 环境信息
            var perception = GetComponent<AIPerception>();
            if (perception != null)
            {
                int enemyCount = perception.GetNearbyEnemies().Count;
                if (enemyCount > 0)
                {
                    context.AppendLine($"附近有{enemyCount}个敌人");
                }
            }
            
            return context.ToString();
        }
        
        /// <summary>
        /// 确定对话类型
        /// </summary>
        private string DetermineDialogueType()
        {
            float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float socialMood = aiStats.GetMood(MoodDimension.Social);
            
            if (healthPercent < 0.3f)
                return "help";
            else if (socialMood < -50f)
                return "lonely";
            else if (Random.value < 0.3f)
                return "share_info";
            else
                return "greeting";
        }
        
        /// <summary>
        /// 获取模板对话
        /// </summary>
        private string GetTemplateDialogue(string type)
        {
            if (dialogueTemplates.ContainsKey(type))
            {
                var templates = dialogueTemplates[type];
                return templates[Random.Range(0, templates.Count)];
            }
            return "...";
        }
        
        /// <summary>
        /// 获取回应对话
        /// </summary>
        private string GetResponseDialogue(string previousDialogue)
        {
            // 简单的回应逻辑
            if (previousDialogue.Contains("帮") || previousDialogue.Contains("help"))
            {
                return "我来帮你！";
            }
            else if (previousDialogue.Contains("谢") || previousDialogue.Contains("thank"))
            {
                return "不客气！";
            }
            else if (previousDialogue.Contains("危险") || previousDialogue.Contains("小心"))
            {
                return "知道了，谢谢提醒！";
            }
            else
            {
                return GetTemplateDialogue("greeting");
            }
        }
        
        /// <summary>
        /// 广播消息给附近的AI
        /// </summary>
        public void BroadcastMessage(string message, float range = 5f)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, range);
            
            foreach (var collider in colliders)
            {
                if (collider.gameObject == gameObject) continue;
                
                var otherDialogue = collider.GetComponent<AIDialogueSystem>();
                if (otherDialogue != null)
                {
                    otherDialogue.ReceiveBroadcast(this, message);
                }
            }
            
            DisplayDialogue(message);
        }
        
        /// <summary>
        /// 接收广播消息
        /// </summary>
        private void ReceiveBroadcast(AIDialogueSystem sender, string message)
        {
            Debug.Log($"[广播] {sender.aiName} -> {aiName}: {message}");
            
            // 可以根据消息内容做出反应
            if (message.Contains("help") || message.Contains("帮"))
            {
                // 向发送者移动提供帮助
                var controller = GetComponent<AIController>();
                if (controller != null)
                {
                    controller.SetMoveTarget(sender.transform.position);
                }
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // 绘制对话范围
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, dialogueRange);
        }
    }
}