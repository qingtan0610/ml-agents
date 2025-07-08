using UnityEngine;
using TMPro;
using System.Text;
using System.Linq;
using AI.Core;
using AI.Stats;
using AI.Decision;

namespace Camera
{
    public class SimpleOverviewUpdater : MonoBehaviour
    {
        public TextMeshProUGUI contentText;
        private float updateInterval = 1f; // Increased from 0.5f to reduce load
        private float nextUpdateTime = 0f;
        
        // Cache references
        private AIBrain[] cachedAIs;
        private float cacheRefreshTime = 0f;
        private float cacheRefreshInterval = 5f; // Refresh cache every 5 seconds
        
        void Update()
        {
            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + updateInterval;
                UpdateContent();
            }
        }
        
        void UpdateContent()
        {
            if (contentText == null) return;
            
            // Refresh cache periodically
            if (Time.time >= cacheRefreshTime || cachedAIs == null)
            {
                cachedAIs = GameObject.FindObjectsOfType<AIBrain>();
                cacheRefreshTime = Time.time + cacheRefreshInterval;
            }
            
            var allAIs = cachedAIs;
            var sb = new StringBuilder();
            
            sb.AppendLine($"Total AIs: {allAIs.Length}\n");
            
            foreach (var ai in allAIs)
            {
                var stats = ai.GetComponent<AIStats>();
                if (stats != null)
                {
                    sb.AppendLine($"<b>[{ai.name}]</b>");
                    sb.AppendLine($"<color=#90EE90>生命:{stats.CurrentHealth:F0}/{stats.Config?.maxHealth:F0}</color> <color=#FFA500>饥饿:{stats.CurrentHunger:F0}</color>");
                    sb.AppendLine($"<color=#87CEEB>口渴:{stats.CurrentThirst:F0}</color> <color=#FF69B4>状态:{ai.GetCurrentState()}</color>");
                    
                    // 获取目标信息 - 紧凑显示
                    var controller = ai.GetComponent<AIController>();
                    if (controller != null)
                    {
                        var targetField = typeof(AIController).GetField("currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (targetField != null)
                        {
                            var target = targetField.GetValue(controller) as GameObject;
                            if (target != null)
                            {
                                sb.AppendLine($"<color=#FFFF00>目标:{target.name}</color>");
                            }
                        }
                    }
                    
                    // 获取DeepSeek决策 - 简化显示
                    var lastDecisionField = typeof(AIBrain).GetField("lastDecision", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (lastDecisionField != null)
                    {
                        var decision = lastDecisionField.GetValue(ai) as AI.Decision.AIDecision;
                        if (decision != null && !string.IsNullOrEmpty(decision.Explanation))
                        {
                            // 截取前30个字符避免太长
                            string shortPlan = decision.Explanation.Length > 30 ? 
                                              decision.Explanation.Substring(0, 30) + "..." : 
                                              decision.Explanation;
                            sb.AppendLine($"<color=#98FB98>计划:{shortPlan}</color>");
                        }
                    }
                    
                    // 获取最近对话
                    var memoryField = typeof(AIBrain).GetField("memory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (memoryField != null)
                    {
                        var memory = memoryField.GetValue(ai) as AI.Core.AIMemory;
                        if (memory != null)
                        {
                            var recentEvents = memory.GetRecentEvents(1);
                            var commEvents = recentEvents.Where(e => e.EventType == AI.Core.EventType.Communication).ToList();
                            if (commEvents.Count > 0)
                            {
                                string dialogue = commEvents[0].Description.Length > 25 ?
                                                commEvents[0].Description.Substring(0, 25) + "..." :
                                                commEvents[0].Description;
                                sb.AppendLine($"<color=#DDA0DD>对话:{dialogue}</color>");
                            }
                        }
                    }
                    
                    sb.AppendLine();
                }
            }
            
            // Map info - avoid FindObjectsOfType in Update
            sb.AppendLine("[Press Tab to refresh enemy count]");
            
            contentText.text = sb.ToString();
        }
    }
}