using UnityEngine;
using TMPro;
using System.Text;
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
                    sb.AppendLine($"[{ai.name}]");
                    sb.AppendLine($"  HP: {stats.CurrentHealth:F0}/{stats.Config?.maxHealth:F0}");
                    sb.AppendLine($"  Hunger: {stats.CurrentHunger:F0}/{stats.Config?.maxHunger:F0}");
                    sb.AppendLine($"  Thirst: {stats.CurrentThirst:F0}/{stats.Config?.maxThirst:F0}");
                    sb.AppendLine($"  State: {ai.GetCurrentState()}");
                    
                    // 获取目标信息
                    var controller = ai.GetComponent<AIController>();
                    if (controller != null)
                    {
                        var targetField = typeof(AIController).GetField("currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (targetField != null)
                        {
                            var target = targetField.GetValue(controller) as GameObject;
                            if (target != null)
                            {
                                sb.AppendLine($"  Target: {target.name}");
                            }
                        }
                    }
                    
                    // 获取DeepSeek决策
                    var lastDecisionField = typeof(AIBrain).GetField("lastDecision", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (lastDecisionField != null)
                    {
                        var decision = lastDecisionField.GetValue(ai) as AI.Decision.AIDecision;
                        if (decision != null && !string.IsNullOrEmpty(decision.Explanation))
                        {
                            sb.AppendLine($"  Plan: {decision.Explanation}");
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