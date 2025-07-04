using UnityEngine;
using AI.Stats;
using System.Text;

namespace AI.Stats
{
    public class AIStatsDebugger : MonoBehaviour
    {
        private AIStats aiStats;
        
        [Header("Debug Controls")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private KeyCode toggleDebugKey = KeyCode.F1;
        
        [Header("Test Controls")]
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float healAmount = 20f;
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            if (aiStats == null)
            {
                Debug.LogError("AIStatsDebugger: AIStats component not found!");
                enabled = false;
                return;
            }
            
            // 订阅事件
            aiStats.OnStatChanged.AddListener(OnStatChanged);
            aiStats.OnMoodChanged.AddListener(OnMoodChanged);
            aiStats.OnDeath.AddListener(OnDeath);
            aiStats.OnRespawn.AddListener(OnRespawn);
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(toggleDebugKey))
            {
                showDebugInfo = !showDebugInfo;
            }
            
            // 测试按键
            HandleTestInputs();
        }
        
        private void HandleTestInputs()
        {
            // H - 伤害
            if (Input.GetKeyDown(KeyCode.H))
            {
                aiStats.ModifyStat(StatType.Health, -damageAmount, StatChangeReason.Combat);
                Debug.Log($"Applied {damageAmount} damage");
            }
            
            // J - 治疗
            if (Input.GetKeyDown(KeyCode.J))
            {
                aiStats.ModifyStat(StatType.Health, healAmount, StatChangeReason.Item);
                Debug.Log($"Applied {healAmount} healing");
            }
            
            // K - 补充食物
            if (Input.GetKeyDown(KeyCode.K))
            {
                aiStats.SetStat(StatType.Hunger, 100f, StatChangeReason.Interact);
                Debug.Log("Refilled hunger");
            }
            
            // L - 补充水分
            if (Input.GetKeyDown(KeyCode.L))
            {
                aiStats.SetStat(StatType.Thirst, 100f, StatChangeReason.Interact);
                Debug.Log("Refilled thirst");
            }
            
            // M - 切换移动状态
            if (Input.GetKeyDown(KeyCode.M))
            {
                bool currentMoving = aiStats.GetComponent<AIStats>().GetType()
                    .GetField("isMoving", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(aiStats) as bool? ?? false;
                aiStats.SetMovementState(!currentMoving);
                Debug.Log($"Movement state: {!currentMoving}");
            }
            
            // C - 交互机通信
            if (Input.GetKeyDown(KeyCode.C))
            {
                aiStats.TriggerCommunicatorInteraction();
                Debug.Log("Communicator interaction triggered");
            }
            
            // F - 面对面交流
            if (Input.GetKeyDown(KeyCode.F))
            {
                aiStats.TriggerFaceToFaceInteraction(true);
                Debug.Log("Face-to-face interaction triggered");
            }
            
            // R - 复活（如果死亡）
            if (Input.GetKeyDown(KeyCode.R) && aiStats.IsDead)
            {
                aiStats.Respawn(transform.position + Vector3.up * 2f);
                Debug.Log("Respawned AI");
            }
            
            // T - 添加测试Buff
            if (Input.GetKeyDown(KeyCode.T))
            {
                var buff = new StatModifier("test_buff", StatType.Health, StatModifierType.Percentage, 20f, 10f);
                aiStats.AddModifier(buff);
                Debug.Log("Added 20% health buff for 10 seconds");
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo || aiStats == null) return;
            
            // 使用GUI而不是GUILayout，避免布局错误
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.padding = new RectOffset(10, 10, 10, 10);
            
            float boxWidth = 400f;
            float boxHeight = 500f;
            
            string debugInfo = "";
            try
            {
                debugInfo = GetDebugInfo();
            }
            catch (System.Exception e)
            {
                debugInfo = $"Error getting debug info: {e.Message}";
            }
            
            GUI.Box(new Rect(10, 10, boxWidth, boxHeight), debugInfo, style);
        }
        
        private string GetDebugInfo()
        {
            if (aiStats == null) return "No AI Stats";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== AI Stats Debug Info ===");
            sb.AppendLine($"Status: {(aiStats.IsDead ? "DEAD" : "ALIVE")}");
            sb.AppendLine($"Time Survived: {aiStats.TimeSurvived:F1}s");
            sb.AppendLine();
            
            // 检查配置
            if (aiStats.Config == null)
            {
                sb.AppendLine("ERROR: No AIStatsConfig assigned!");
                sb.AppendLine("Please assign a config in the Inspector.");
                return sb.ToString();
            }
            
            // 基础属性
            sb.AppendLine("--- Basic Stats ---");
            sb.AppendLine($"Health: {aiStats.GetStat(StatType.Health):F1}/{aiStats.Config.maxHealth} ({aiStats.GetStatPercentage(StatType.Health):P0})");
            sb.AppendLine($"Hunger: {aiStats.GetStat(StatType.Hunger):F1}/{aiStats.Config.maxHunger} ({aiStats.GetStatPercentage(StatType.Hunger):P0})");
            sb.AppendLine($"Thirst: {aiStats.GetStat(StatType.Thirst):F1}/{aiStats.Config.maxThirst} ({aiStats.GetStatPercentage(StatType.Thirst):P0})");
            sb.AppendLine($"Stamina: {aiStats.GetStat(StatType.Stamina):F1}/{aiStats.Config.maxStamina} ({aiStats.GetStatPercentage(StatType.Stamina):P0})");
            sb.AppendLine($"Armor: {aiStats.GetStat(StatType.Armor):F1}/{aiStats.Config.maxArmor}");
            sb.AppendLine($"Toughness: {aiStats.GetStat(StatType.Toughness):F1}/{aiStats.Config.maxToughness}");
            sb.AppendLine();
            
            // 弹药
            sb.AppendLine("--- Ammo ---");
            sb.AppendLine($"Bullets: {aiStats.GetStat(StatType.Bullets):F0}");
            sb.AppendLine($"Arrows: {aiStats.GetStat(StatType.Arrows):F0}");
            sb.AppendLine($"Mana: {aiStats.GetStat(StatType.Mana):F1}");
            sb.AppendLine();
            
            // 心情
            sb.AppendLine("--- Mood ---");
            sb.AppendLine($"Emotion: {aiStats.GetMood(MoodDimension.Emotion):F1} ({aiStats.GetMoodDescription(MoodDimension.Emotion)})");
            sb.AppendLine($"Social: {aiStats.GetMood(MoodDimension.Social):F1} ({aiStats.GetMoodDescription(MoodDimension.Social)})");
            sb.AppendLine($"Mentality: {aiStats.GetMood(MoodDimension.Mentality):F1} ({aiStats.GetMoodDescription(MoodDimension.Mentality)})");
            sb.AppendLine();
            
            // 修改器
            var modifiers = aiStats.GetActiveModifiers();
            if (modifiers.Count > 0)
            {
                sb.AppendLine("--- Active Modifiers ---");
                foreach (var mod in modifiers)
                {
                    sb.AppendLine($"{mod.Id}: {mod.TargetStat} {mod.Value:+0;-0}% ({mod.Duration:F1}s)");
                }
                sb.AppendLine();
            }
            
            // 控制提示
            sb.AppendLine("--- Controls ---");
            sb.AppendLine("H: Damage | J: Heal | K: Food | L: Water");
            sb.AppendLine("M: Move | C: Comm | F: Talk | R: Respawn");
            sb.AppendLine("T: Test Buff | F1: Toggle Debug");
            
            return sb.ToString();
        }
        
        private void OnStatChanged(StatChangeEventArgs args)
        {
            // 只记录重要的变化（生命值、死亡等）
            if (args.statType == StatType.Health && args.reason == StatChangeReason.Combat)
            {
                Debug.Log($"[Combat] Health: {args.oldValue:F1} → {args.newValue:F1}");
            }
        }
        
        private void OnMoodChanged(MoodChangeEventArgs args)
        {
            // 禁用心情变化日志
        }
        
        private void OnDeath(AIDeathEventArgs args)
        {
            Debug.LogWarning($"[AI Died] Cause: {args.causeOfDeath}, Position: {args.deathPosition}, Survived: {args.timeSurvived:F1}s");
        }
        
        private void OnRespawn()
        {
            Debug.Log("[AI Respawned]");
        }
    }
}