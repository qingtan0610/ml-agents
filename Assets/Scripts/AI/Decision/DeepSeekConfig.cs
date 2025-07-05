using UnityEngine;

namespace AI.Decision
{
    /// <summary>
    /// DeepSeek API配置文件
    /// </summary>
    [CreateAssetMenu(fileName = "DeepSeekConfig", menuName = "AI/DeepSeek Config")]
    public class DeepSeekConfig : ScriptableObject
    {
        [Header("API设置")]
        [Tooltip("DeepSeek API密钥")]
        [SerializeField] private string apiKey = "";
        
        [Tooltip("API端点URL")]
        [SerializeField] private string apiEndpoint = "https://api.deepseek.com/v1/chat/completions";
        
        [Tooltip("模型名称")]
        [SerializeField] private string model = "deepseek-chat";
        
        [Header("请求参数")]
        [Tooltip("温度参数，控制回复的随机性(0-2)")]
        [Range(0f, 2f)]
        public float temperature = 0.7f;
        
        [Tooltip("最大回复令牌数")]
        public int maxTokens = 500;
        
        [Tooltip("Top-p采样参数")]
        [Range(0f, 1f)]
        public float topP = 0.95f;
        
        [Tooltip("频率惩罚")]
        [Range(-2f, 2f)]
        public float frequencyPenalty = 0f;
        
        [Tooltip("存在惩罚")]
        [Range(-2f, 2f)]
        public float presencePenalty = 0f;
        
        [Header("网络设置")]
        [Tooltip("请求超时时间(秒)")]
        public float requestTimeout = 30f;
        
        [Tooltip("最大重试次数")]
        public int maxRetries = 3;
        
        [Tooltip("重试延迟(秒)")]
        public float retryDelay = 1f;
        
        [Header("使用限制")]
        [Tooltip("每分钟最大请求数")]
        public int maxRequestsPerMinute = 10;
        
        [Tooltip("每天最大请求数")]
        public int maxRequestsPerDay = 1000;
        
        [Tooltip("缓存持续时间(秒)")]
        public float cacheDuration = 300f;
        
        [Header("系统提示词")]
        [TextArea(5, 10)]
        public string systemPrompt = @"你是一个Roguelike游戏中的高级AI决策助手。你需要：
1. 分析AI角色的当前状态（生命、饥饿、口渴、心情等）
2. 评估环境威胁和机会
3. 提供战术和战略建议
4. 保持角色扮演的一致性
5. 优先考虑生存，然后是探索和发展";
        
        /// <summary>
        /// 获取API密钥（优先级：ScriptableObject > PlayerPrefs > 环境变量）
        /// </summary>
        public string GetApiKey()
        {
            // 1. ScriptableObject中的密钥
            if (!string.IsNullOrEmpty(apiKey))
                return apiKey;
            
            // 2. PlayerPrefs中的密钥
            string playerPrefsKey = PlayerPrefs.GetString("DeepSeekAPIKey", "");
            if (!string.IsNullOrEmpty(playerPrefsKey))
                return playerPrefsKey;
            
            // 3. 环境变量
            string envKey = System.Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
                return envKey;
            
            return "";
        }
        
        /// <summary>
        /// 获取API端点
        /// </summary>
        public string GetApiEndpoint()
        {
            return string.IsNullOrEmpty(apiEndpoint) ? "https://api.deepseek.com/v1/chat/completions" : apiEndpoint;
        }
        
        /// <summary>
        /// 获取模型名称
        /// </summary>
        public string GetModel()
        {
            return string.IsNullOrEmpty(model) ? "deepseek-chat" : model;
        }
        
        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(GetApiKey());
        }
        
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/AI/DeepSeek Config")]
        private static void CreateDeepSeekConfig()
        {
            var asset = CreateInstance<DeepSeekConfig>();
            UnityEditor.AssetDatabase.CreateAsset(asset, "Assets/DeepSeekConfig.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorUtility.FocusProjectWindow();
            UnityEditor.Selection.activeObject = asset;
        }
#endif
    }
}