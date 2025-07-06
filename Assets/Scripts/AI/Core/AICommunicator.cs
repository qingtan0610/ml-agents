using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace AI.Core
{
    /// <summary>
    /// AI通信系统 - 处理AI之间的通信
    /// </summary>
    public class AICommunicator : MonoBehaviour
    {
        [Header("Communication Settings")]
        [SerializeField] private float voiceRange = 16f; // 同房间内的声音范围
        [SerializeField] private float radioRange = 100f; // 交互机通信范围（全地图）
        [SerializeField] private float facialExpressionRange = 3f; // 面对面交流范围
        
        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private List<AudioClip> communicationSounds;
        
        [Header("DeepSeek Integration")]
        [SerializeField] private bool useDeepSeekForDialogue = false;
        
        // 所有AI通信器的静态列表
        private static List<AICommunicator> allCommunicators = new List<AICommunicator>();
        
        // 静态构造函数，确保场景加载时清理
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            allCommunicators = new List<AICommunicator>();
            Debug.Log("[AICommunicator] 静态列表已重置");
        }
        
        private void Start()
        {
            // 确保开始时没有旧消息
            receivedMessages.Clear();
            sentMessages.Clear();
            
            // 清理可能存在的幽灵AI（已销毁但还在列表中的）
            allCommunicators.RemoveAll(c => c == null);
            
            Debug.Log($"[AICommunicator] {name} 初始化完成，消息列表已清空，当前活跃AI数: {allCommunicators.Count}");
        }
        
        // 通信记录
        private List<CommunicationRecord> sentMessages = new List<CommunicationRecord>();
        private List<CommunicationRecord> receivedMessages = new List<CommunicationRecord>();
        
        // 事件
        public System.Action<CommunicationMessage> OnMessageReceived;
        public System.Action<AICommunicator, float> OnFaceToFaceTalk; // 发送者，心情改善值
        
        private void OnEnable()
        {
            allCommunicators.Add(this);
            // 清理旧消息，避免接收到之前的消息
            receivedMessages.Clear();
            sentMessages.Clear();
            Debug.Log($"[AICommunicator] {name} 加入，当前总数: {allCommunicators.Count}");
        }
        
        private void OnDisable()
        {
            allCommunicators.Remove(this);
            Debug.Log($"[AICommunicator] {name} 离开，剩余总数: {allCommunicators.Count}");
        }
        
        // 发送消息（交互机）
        public void SendMessage(CommunicationType type, Vector2 position)
        {
            var message = new CommunicationMessage
            {
                Sender = this,
                Type = type,
                Position = position,
                Timestamp = Time.time,
                IsRadioMessage = true
            };
            
            // 记录发送
            sentMessages.Add(new CommunicationRecord
            {
                Message = message,
                Time = Time.time
            });
            
            // 广播给所有其他AI
            int receiversCount = 0;
            foreach (var receiver in allCommunicators)
            {
                if (receiver != this && receiver != null)
                {
                    receiver.ReceiveRadioMessage(message);
                    receiversCount++;
                }
            }
            
            // 播放通信音效
            PlayCommunicationSound(type);
            
            Debug.Log($"[AICommunicator] {name} 发送消息: {type} at {position}, 接收者数量: {receiversCount}");
        }
        
        // 发出声音（同房间）
        public void MakeSound(SoundType soundType, float volume = 1f)
        {
            // 检查同房间的AI
            var nearbyAIs = allCommunicators
                .Where(c => c != this && Vector2.Distance(transform.position, c.transform.position) <= voiceRange)
                .ToList();
            
            foreach (var ai in nearbyAIs)
            {
                float distance = Vector2.Distance(transform.position, ai.transform.position);
                float hearingWeight = 1f - (distance / voiceRange); // 距离越近权重越高
                
                ai.HearSound(this, soundType, hearingWeight * volume);
            }
        }
        
        // 面对面交流
        public void TryFaceToFaceTalk()
        {
            // 查找面对面的AI
            var facingAI = FindFacingAI();
            if (facingAI != null)
            {
                // 根据TODO.txt：面对面倾述或者倾听都能大幅度降低孤独
                float moodImprovement = 30f; // 大幅降低孤独
                
                // 获取AI状态组件
                var myStats = GetComponent<AI.Stats.AIStats>();
                var otherStats = facingAI.GetComponent<AI.Stats.AIStats>();
                
                if (myStats != null)
                {
                    myStats.ModifyMood(AI.Stats.MoodDimension.Social, moodImprovement, AI.Stats.StatChangeReason.Interact);
                }
                
                if (otherStats != null)
                {
                    otherStats.ModifyMood(AI.Stats.MoodDimension.Social, moodImprovement, AI.Stats.StatChangeReason.Interact);
                }
                
                // 触发对话系统
                var dialogueSystem = GetComponent<AIDialogueSystem>();
                if (dialogueSystem != null && useDeepSeekForDialogue)
                {
                    dialogueSystem.TryDialogueWithNearbyAI();
                }
                
                OnFaceToFaceTalk?.Invoke(facingAI, moodImprovement);
                facingAI.OnFaceToFaceTalk?.Invoke(this, moodImprovement);
                
                // 播放交谈声音
                MakeSound(SoundType.Talk);
                
                Debug.Log($"[AICommunicator] {name} 与 {facingAI.name} 面对面交谈，大幅改善社交心情");
            }
        }
        
        private AICommunicator FindFacingAI()
        {
            // 检查前方的AI
            Vector2 forward = transform.up; // 2D中使用up作为前方
            
            foreach (var other in allCommunicators)
            {
                if (other == this) continue;
                
                Vector2 toOther = (Vector2)(other.transform.position - transform.position);
                float distance = toOther.magnitude;
                
                if (distance > facialExpressionRange) continue;
                
                // 检查是否面对面
                float angle = Vector2.Angle(forward, toOther);
                if (angle < 45f) // 45度内认为是面对
                {
                    // 检查对方是否也面向我们
                    Vector2 otherForward = other.transform.up;
                    Vector2 toUs = -toOther;
                    float otherAngle = Vector2.Angle(otherForward, toUs);
                    
                    if (otherAngle < 45f)
                    {
                        return other;
                    }
                }
            }
            
            return null;
        }
        
        // 接收无线电消息
        private void ReceiveRadioMessage(CommunicationMessage message)
        {
            // 验证消息有效性
            if (message == null || message.Sender == null)
            {
                Debug.LogWarning($"[AICommunicator] {name} 收到无效消息（发送者为空）");
                return;
            }
            
            // 记录接收
            receivedMessages.Add(new CommunicationRecord
            {
                Message = message,
                Time = Time.time
            });
            
            // 限制消息历史
            if (receivedMessages.Count > 20)
            {
                receivedMessages.RemoveAt(0);
            }
            
            // 触发事件
            OnMessageReceived?.Invoke(message);
            
            // 根据TODO.txt：交互机交流能够极小幅度降低孤独
            var aiStats = GetComponent<AI.Stats.AIStats>();
            if (aiStats != null)
            {
                aiStats.ModifyMood(AI.Stats.MoodDimension.Social, 2f, AI.Stats.StatChangeReason.Interact); // 极小幅度改善
            }
        }
        
        // 听到声音
        private void HearSound(AICommunicator source, SoundType soundType, float weight)
        {
            // 根据声音类型和权重处理
            switch (soundType)
            {
                case SoundType.Combat:
                    // 战斗声音可能引起警觉
                    Debug.Log($"[AICommunicator] {name} 听到 {source.name} 的战斗声 (权重: {weight:F2})");
                    break;
                    
                case SoundType.Talk:
                    // 交谈声音可能吸引注意
                    if (weight > 0.5f) // 距离较近
                    {
                        var aiStats = GetComponent<AI.Stats.AIStats>();
                        if (aiStats != null)
                        {
                            aiStats.ModifyMood(AI.Stats.MoodDimension.Social, 5f * weight, AI.Stats.StatChangeReason.Interact); // 根据距离改善心情
                        }
                    }
                    break;
                    
                case SoundType.Pain:
                    // 痛苦声音可能引起担忧或警惕
                    Debug.Log($"[AICommunicator] {name} 听到 {source.name} 的痛苦声 (权重: {weight:F2})");
                    break;
            }
        }
        
        // 播放通信音效
        private void PlayCommunicationSound(CommunicationType type)
        {
            if (audioSource != null && communicationSounds.Count > 0)
            {
                int soundIndex = Mathf.Min((int)type, communicationSounds.Count - 1);
                if (soundIndex >= 0 && communicationSounds[soundIndex] != null)
                {
                    audioSource.PlayOneShot(communicationSounds[soundIndex]);
                }
            }
        }
        
        // 查询接口
        public List<CommunicationRecord> GetRecentMessages(int count = 10)
        {
            return receivedMessages.TakeLast(count).ToList();
        }
        
        public bool HasRecentMessage(CommunicationType type, float timeWindow = 30f)
        {
            return receivedMessages.Any(r => 
                r.Message.Type == type && 
                Time.time - r.Time < timeWindow);
        }
        
        public CommunicationMessage GetLatestMessage(CommunicationType type)
        {
            // 只返回最近60秒内的消息，避免过期消息影响决策
            float messageValidTime = 60f;
            
            // 过滤掉发送者为空的消息
            var message = receivedMessages
                .Where(r => r.Message != null && r.Message.Sender != null && 
                           r.Message.Type == type && Time.time - r.Time < messageValidTime)
                .OrderByDescending(r => r.Time)
                .FirstOrDefault()?.Message;
            
            // 调试日志，追踪消息来源
            if (message != null && type == CommunicationType.Help)
            {
                Debug.Log($"[AICommunicator] {name} 获取到Help消息: 发送者={message.Sender?.name ?? "NULL"}, 时间差={Time.time - message.Timestamp:F1}秒, 发送者存在={message.Sender != null}");
            }
            
            return message;
        }
        
        // 获取附近可以交流的AI
        public List<AICommunicator> GetNearbyAIsForTalk()
        {
            return allCommunicators
                .Where(c => c != this && Vector2.Distance(transform.position, c.transform.position) <= voiceRange)
                .OrderBy(c => Vector2.Distance(transform.position, c.transform.position))
                .ToList();
        }
        
        // Gizmos
        private void OnDrawGizmosSelected()
        {
            // 声音范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, voiceRange);
            
            // 面对面交流范围
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, facialExpressionRange);
            
            // 前方方向
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.up * facialExpressionRange);
        }
    }
    
    // 通信消息
    [System.Serializable]
    public class CommunicationMessage
    {
        public AICommunicator Sender;
        public CommunicationType Type;
        public Vector2 Position;
        public float Timestamp;
        public bool IsRadioMessage; // true=交互机消息, false=声音
    }
    
    // 通信记录
    [System.Serializable]
    public class CommunicationRecord
    {
        public CommunicationMessage Message;
        public float Time;
    }
    
    // 声音类型
    public enum SoundType
    {
        Talk,      // 交谈
        Combat,    // 战斗
        Pain,      // 受伤
        Joy,       // 欢呼
        Warning    // 警告
    }
}