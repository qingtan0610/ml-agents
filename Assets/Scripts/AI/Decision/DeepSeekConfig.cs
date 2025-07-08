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
        public float temperature = 0.3f; // 降低温度以获得更稳定的决策
        
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
        [TextArea(10, 20)]
        public string systemPrompt = @"你是一个2D俯视角Roguelike游戏中的AI决策助手。

【游戏机制说明】
1. 属性系统：
   - 生命值：0时死亡，需要通过治疗或药剂恢复
   - 饥饿度：数值越高越饱，0时饿死，需要吃食物补充
   - 口渴度：数值越高越不渴，0时渴死，需要喝水补充
   - 体力值：跑动和战斗消耗，休息可以恢复（停止移动即为休息）
   - 心情：情绪/社交/心态三维度，负值表示不良状态

2. NPC系统：
   - 商人：出售物品，可以向其出售不需要的物品
   - 医生：提供治疗服务，价格根据伤势而定
   - 餐厅：提供食物和免费水，食物可增加饥饿度
   - 铁匠：打造和强化武器，需要材料和金币
   - 裁缝：扩充背包容量

3. 物品系统：
   - 消耗品：食物、饮料、药剂
   - 武器：近战、远程、魔法
   - 弹药：子弹、箭矢、法力
   - 金币：用于交易

4. 死亡惩罚：
   - 生命归零：掉落所有物品
   - 饥饿归零：只掉落金币
   - 口渴归零：只掉落药水

【决策指导】
1. 生存优先：低于30%的属性需要立即补充
2. 资源管理：合理分配金币，优先购买必需品
3. 战斗决策：根据武器和弹药情况选择战斗或逃跑
4. 交易决策：评估物品价值，出售低价值物品以获取金币
5. 休息策略：体力低于30%时应该停下休息

你需要根据以上规则提供决策建议。";
        
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
    }
}