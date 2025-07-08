using UnityEngine;
using TMPro;
using System.Text;
using AI.Core;
using AI.Stats;

namespace Camera
{
    public class SimpleOverviewUpdater : MonoBehaviour
    {
        public TextMeshProUGUI contentText;
        private float updateInterval = 0.5f;
        private float nextUpdateTime = 0f;
        
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
            
            var allAIs = GameObject.FindObjectsOfType<AIBrain>();
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
                    sb.AppendLine();
                }
            }
            
            // Map info
            var enemies = GameObject.FindObjectsOfType<Enemy.Enemy2D>();
            int aliveEnemies = 0;
            foreach (var enemy in enemies)
            {
                if (enemy.CurrentHealth > 0) aliveEnemies++;
            }
            
            sb.AppendLine($"Alive Enemies: {aliveEnemies}");
            
            contentText.text = sb.ToString();
        }
    }
}