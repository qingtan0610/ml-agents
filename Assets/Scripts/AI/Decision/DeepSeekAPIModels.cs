using System;
using System.Collections.Generic;

namespace AI.Decision
{
    /// <summary>
    /// DeepSeek API请求模型
    /// </summary>
    [Serializable]
    public class DeepSeekRequest
    {
        public string model;
        public List<Message> messages;
        public float temperature;
        public int max_tokens;
        public float top_p;
        public float frequency_penalty;
        public float presence_penalty;
        public bool stream = false;
        
        [Serializable]
        public class Message
        {
            public string role;
            public string content;
            
            public Message(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }
    }
    
    /// <summary>
    /// DeepSeek API响应模型
    /// </summary>
    [Serializable]
    public class DeepSeekResponse
    {
        public string id;
        public string @object;
        public long created;
        public string model;
        public List<Choice> choices;
        public Usage usage;
        public Error error;
        
        [Serializable]
        public class Choice
        {
            public int index;
            public Message message;
            public string finish_reason;
            
            [Serializable]
            public class Message
            {
                public string role;
                public string content;
            }
        }
        
        [Serializable]
        public class Usage
        {
            public int prompt_tokens;
            public int completion_tokens;
            public int total_tokens;
        }
        
        [Serializable]
        public class Error
        {
            public string message;
            public string type;
            public string param;
            public string code;
        }
    }
    
    /// <summary>
    /// AI决策响应格式
    /// </summary>
    [Serializable]
    public class DeepSeekDecisionResponse
    {
        public string state;              // Exploring/Fighting/Fleeing/Seeking/Interacting/Communicating/Resting/Critical
        public string priority;           // Survival/Combat/Exploration/Normal
        public string reasoning;          // 决策理由
        public List<string> actions;      // 具体行动建议
        public Dictionary<string, float> parameters;  // 额外参数（如移动方向、攻击目标等）
    }
    
    /// <summary>
    /// AI对话响应格式
    /// </summary>
    [Serializable]
    public class DeepSeekDialogueResponse
    {
        public string message;            // 对话内容
        public string emotion;            // 情绪状态
        public string intent;             // 意图（greeting/help/trade/information/warning）
        public List<string> topics;       // 话题标签
    }
}