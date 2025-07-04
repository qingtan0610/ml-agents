using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Config;
using AI.Interfaces;

namespace AI.Stats
{
    public class AIStats : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AIStatsConfig config;
        
        [Header("Current Stats (Debug View)")]
        [SerializeField] private Dictionary<StatType, float> currentStats = new Dictionary<StatType, float>();
        [SerializeField] private AIMood mood;
        
        [Header("Modifiers")]
        private Dictionary<string, StatModifier> activeModifiers = new Dictionary<string, StatModifier>();
        
        [Header("State")]
        [SerializeField] private bool isDead = false;
        [SerializeField] private bool isMoving = false;
        [SerializeField] private float timeSinceSpawn = 0f;
        
        // Events
        public StatChangeEvent OnStatChanged = new StatChangeEvent();
        public MoodChangeEvent OnMoodChanged = new MoodChangeEvent();
        public AIDeathEvent OnDeath = new AIDeathEvent();
        public UnityEvent OnRespawn = new UnityEvent();
        
        // Properties for external access
        public bool IsDead => isDead;
        public float TimeSurvived => timeSinceSpawn;
        public AIStatsConfig Config => config;
        
        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("AIStats: No configuration assigned! Please create and assign an AIStatsConfig.");
                enabled = false;
                return;
            }
            
            InitializeStats();
            mood = new AIMood(config);
            mood.OnMoodChanged += HandleMoodChange;
        }
        
        private void InitializeStats()
        {
            // 初始化基础属性
            currentStats[StatType.Health] = config.initialHealth;
            currentStats[StatType.Hunger] = config.initialHunger;
            currentStats[StatType.Thirst] = config.initialThirst;
            currentStats[StatType.Stamina] = config.initialStamina;
            currentStats[StatType.Armor] = config.initialArmor;
            currentStats[StatType.Toughness] = config.initialToughness;
            
            // 初始化弹药
            currentStats[StatType.Bullets] = config.initialBullets;
            currentStats[StatType.Arrows] = config.initialArrows;
            currentStats[StatType.Mana] = config.initialMana;
        }
        
        private void Update()
        {
            if (isDead || config == null) return;
            
            float deltaTime = Time.deltaTime;
            timeSinceSpawn += deltaTime;
            
            // 更新自然消耗
            UpdateNaturalDepletion(deltaTime);
            
            // 更新修改器
            UpdateModifiers(deltaTime);
            
            // 更新心情
            if (mood != null && currentStats != null)
            {
                mood.UpdateMood(currentStats, deltaTime);
            }
            
            // 检查死亡状态
            CheckDeathConditions();
        }
        
        private void UpdateNaturalDepletion(float deltaTime)
        {
            if (config == null) return;
            
            // 饥饿值消耗
            ModifyStat(StatType.Hunger, -config.hungerDepletionRate * deltaTime, StatChangeReason.Natural);
            
            // 口渴值消耗（受心情影响）
            float thirstMultiplier = mood != null && mood.Mentality < -50 ? 1.5f : 1f; // 急躁时消耗更快
            ModifyStat(StatType.Thirst, -config.thirstDepletionRate * thirstMultiplier * deltaTime, StatChangeReason.Natural);
            
            // 体力值更新
            if (isMoving)
            {
                ModifyStat(StatType.Stamina, -config.staminaDepletionRate * deltaTime, StatChangeReason.Natural);
            }
            else
            {
                ModifyStat(StatType.Stamina, config.staminaRecoveryRate * deltaTime, StatChangeReason.Natural);
            }
        }
        
        private void UpdateModifiers(float deltaTime)
        {
            var expiredModifiers = new List<string>();
            
            foreach (var kvp in activeModifiers)
            {
                kvp.Value.Update(deltaTime);
                if (kvp.Value.IsExpired)
                {
                    expiredModifiers.Add(kvp.Key);
                }
            }
            
            // 移除过期的修改器
            foreach (var id in expiredModifiers)
            {
                RemoveModifier(id);
            }
        }
        
        private void CheckDeathConditions()
        {
            if (currentStats == null || currentStats.Count == 0) return;
            
            StatType? deathCause = null;
            
            if (currentStats.ContainsKey(StatType.Health) && currentStats[StatType.Health] <= 0)
                deathCause = StatType.Health;
            else if (currentStats.ContainsKey(StatType.Hunger) && currentStats[StatType.Hunger] <= 0)
                deathCause = StatType.Hunger;
            else if (currentStats.ContainsKey(StatType.Thirst) && currentStats[StatType.Thirst] <= 0)
                deathCause = StatType.Thirst;
            
            if (deathCause.HasValue && !isDead)
            {
                Die(deathCause.Value);
            }
        }
        
        private void Die(StatType cause)
        {
            isDead = true;
            OnDeath?.Invoke(new AIDeathEventArgs(cause, transform.position, timeSinceSpawn));
        }
        
        public void Respawn(Vector3 spawnPosition, bool applyPenalties = true)
        {
            if (!isDead) return;
            
            transform.position = spawnPosition;
            isDead = false;
            
            // 根据死亡原因应用惩罚
            if (applyPenalties)
            {
                // 这里预留给背包系统处理物品清空逻辑
            }
            
            // 恢复部分状态
            float respawnPercent = config.respawnStatPercentage;
            currentStats[StatType.Health] = config.maxHealth * respawnPercent;
            currentStats[StatType.Hunger] = config.maxHunger * respawnPercent;
            currentStats[StatType.Thirst] = config.maxThirst * respawnPercent;
            currentStats[StatType.Stamina] = config.maxStamina * respawnPercent;
            
            timeSinceSpawn = 0f;
            OnRespawn?.Invoke();
        }
        
        #region Stat Access Methods
        
        public float GetStat(StatType statType)
        {
            if (!currentStats.ContainsKey(statType)) return 0f;
            
            float baseValue = currentStats[statType];
            float finalValue = baseValue;
            
            // 应用修改器
            var modifiers = activeModifiers.Values.Where(m => m.TargetStat == statType);
            foreach (var modifier in modifiers)
            {
                if (modifier.ModifierType == StatModifierType.Flat)
                {
                    finalValue += modifier.Value;
                }
                else // Percentage
                {
                    finalValue *= (1 + modifier.Value / 100f);
                }
            }
            
            // 限制在最大值范围内
            finalValue = ClampStatValue(statType, finalValue);
            return finalValue;
        }
        
        public float GetStatPercentage(StatType statType)
        {
            float current = GetStat(statType);
            float max = GetMaxStatValue(statType);
            return max > 0 ? current / max : 0f;
        }
        
        public void SetStat(StatType statType, float value, StatChangeReason reason = StatChangeReason.Other)
        {
            if (!currentStats.ContainsKey(statType)) return;
            
            float oldValue = currentStats[statType];
            float newValue = ClampStatValue(statType, value);
            
            if (Mathf.Abs(oldValue - newValue) > 0.01f)
            {
                currentStats[statType] = newValue;
                OnStatChanged?.Invoke(new StatChangeEventArgs(statType, oldValue, newValue, reason));
            }
        }
        
        public void ModifyStat(StatType statType, float amount, StatChangeReason reason = StatChangeReason.Other)
        {
            if (!currentStats.ContainsKey(statType)) return;
            
            float currentValue = currentStats[statType];
            SetStat(statType, currentValue + amount, reason);
        }
        
        #endregion
        
        #region Modifier Methods
        
        public void AddModifier(StatModifier modifier)
        {
            if (activeModifiers.ContainsKey(modifier.Id))
            {
                RemoveModifier(modifier.Id);
            }
            
            activeModifiers[modifier.Id] = modifier;
        }
        
        public void RemoveModifier(string modifierId)
        {
            if (activeModifiers.ContainsKey(modifierId))
            {
                activeModifiers.Remove(modifierId);
            }
        }
        
        public void RemoveAllModifiers()
        {
            activeModifiers.Clear();
        }
        
        public List<StatModifier> GetActiveModifiers()
        {
            return activeModifiers.Values.ToList();
        }
        
        #endregion
        
        #region Movement State
        
        public void SetMovementState(bool moving)
        {
            isMoving = moving;
        }
        
        #endregion
        
        #region Mood Access
        
        public float GetMood(MoodDimension dimension)
        {
            switch (dimension)
            {
                case MoodDimension.Emotion:
                    return mood.Emotion;
                case MoodDimension.Social:
                    return mood.Social;
                case MoodDimension.Mentality:
                    return mood.Mentality;
                default:
                    return 0f;
            }
        }
        
        public string GetMoodDescription(MoodDimension dimension)
        {
            return mood.GetMoodDescription(dimension);
        }
        
        public void TriggerCommunicatorInteraction()
        {
            mood.OnCommunicatorInteraction();
        }
        
        public void TriggerFaceToFaceInteraction(bool isTalking)
        {
            mood.OnFaceToFaceInteraction(isTalking);
        }
        
        #endregion
        
        #region Helper Methods
        
        private float GetMaxStatValue(StatType statType)
        {
            switch (statType)
            {
                case StatType.Health: return config.maxHealth;
                case StatType.Hunger: return config.maxHunger;
                case StatType.Thirst: return config.maxThirst;
                case StatType.Stamina: return config.maxStamina;
                case StatType.Armor: return config.maxArmor;
                case StatType.Toughness: return config.maxToughness;
                case StatType.Bullets: return config.maxBullets;
                case StatType.Arrows: return config.maxArrows;
                case StatType.Mana: return config.maxMana;
                default: return 100f;
            }
        }
        
        private float ClampStatValue(StatType statType, float value)
        {
            float max = GetMaxStatValue(statType);
            return Mathf.Clamp(value, 0f, max);
        }
        
        private void HandleMoodChange(MoodChangeEventArgs args)
        {
            OnMoodChanged?.Invoke(args);
        }
        
        #endregion
        
        #region Save/Load
        
        public AIStatsData GetStatsData()
        {
            var data = new AIStatsData();
            data.stats = new Dictionary<StatType, float>(currentStats);
            data.moodData = mood.GetMoodData();
            data.isDead = isDead;
            data.timeSurvived = timeSinceSpawn;
            return data;
        }
        
        public void LoadStatsData(AIStatsData data)
        {
            if (data == null) return;
            
            currentStats = new Dictionary<StatType, float>(data.stats);
            mood.LoadMoodData(data.moodData);
            isDead = data.isDead;
            timeSinceSpawn = data.timeSurvived;
        }
        
        #endregion
    }
    
    [Serializable]
    public class AIStatsData
    {
        public Dictionary<StatType, float> stats;
        public MoodData moodData;
        public bool isDead;
        public float timeSurvived;
    }
}