using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Config;
using AI.Interfaces;
using Combat.Interfaces;

namespace AI.Stats
{
    public class AIStats : MonoBehaviour, IDamageable
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
        private StatType? lastDeathCause = null;
        
        // IDamageable implementation (using existing properties)
        public float MaxHealth => config?.maxHealth ?? 100f;
        float IDamageable.CurrentHealth => CurrentHealth; // 使用已有的CurrentHealth属性
        bool IDamageable.IsDead => IsDead; // 使用已有的IsDead属性
        
        public void TakeDamage(float damage, GameObject attacker, DamageInfo damageInfo = null)
        {
            if (isDead) return;
            
            // 计算实际伤害（考虑护甲等）
            float actualDamage = damage;
            
            // 应用伤害
            ModifyStat(StatType.Health, -actualDamage, StatChangeReason.Combat);
            
            // 添加战斗日志
            Debug.Log($"[AIStats] {name} 受到 {actualDamage} 点伤害来自 {attacker?.name ?? "Unknown"}");
            
            // 应用Debuff
            if (damageInfo != null && damageInfo.appliedDebuffs != null)
            {
                var buffManager = GetComponent<Buffs.BuffManager>();
                if (buffManager != null)
                {
                    foreach (var debuff in damageInfo.appliedDebuffs)
                    {
                        if (debuff != null && UnityEngine.Random.value < damageInfo.debuffChance)
                        {
                            buffManager.AddBuff(debuff);
                        }
                    }
                }
            }
            
            // 击退效果
            if (damageInfo != null && damageInfo.knockback > 0)
            {
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.AddForce(damageInfo.hitDirection * damageInfo.knockback, ForceMode2D.Impulse);
                }
            }
        }
        
        public void Heal(float amount)
        {
            if (isDead) return;
            ModifyStat(StatType.Health, amount, StatChangeReason.Item);
        }
        
        public void Die()
        {
            if (!isDead)
            {
                Die(StatType.Health);
            }
        }
        
        // Events - 确保事件被正确初始化
        public StatChangeEvent OnStatChanged = new StatChangeEvent();
        public MoodChangeEvent OnMoodChanged = new MoodChangeEvent();
        public AIDeathEvent OnDeath = new AIDeathEvent();
        public UnityEvent OnRespawn = new UnityEvent();
        
        // Properties for external access
        public bool IsDead => isDead;
        public float TimeSurvived => timeSinceSpawn;
        public AIStatsConfig Config => config;
        
        // Quick access properties for current stats
        public float CurrentHealth => GetStat(StatType.Health);
        public float CurrentHunger => GetStat(StatType.Hunger);
        public float CurrentThirst => GetStat(StatType.Thirst);
        public float CurrentStamina => GetStat(StatType.Stamina);
        
        private void Awake()
        {
            Debug.Log("[AIStats] Awake called");
            
            // 确保事件被初始化
            if (OnDeath == null)
            {
                OnDeath = new AIDeathEvent();
                Debug.Log("[AIStats] OnDeath event was null, created new instance");
            }
            else
            {
                Debug.Log("[AIStats] OnDeath event already initialized");
            }
            
            if (config == null)
            {
                Debug.LogError("AIStats: No configuration assigned! Please create and assign an AIStatsConfig.");
                enabled = false;
                return;
            }
            
            InitializeStats();
            mood = new AIMood(config);
            mood.OnMoodChanged += HandleMoodChange;
            
            Debug.Log($"[AIStats] Awake finished. OnDeath event exists: {OnDeath != null}");
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
            
            // 使用GetRawStat获取真实值（包括负值）进行死亡检测
            if (currentStats.ContainsKey(StatType.Health) && GetRawStat(StatType.Health) <= 0)
            {
                deathCause = StatType.Health;
                Debug.Log($"[AIStats] Death condition met: Health={GetRawStat(StatType.Health)} (raw), {GetStat(StatType.Health)} (display)");
            }
            else if (currentStats.ContainsKey(StatType.Hunger) && GetRawStat(StatType.Hunger) <= 0)
            {
                deathCause = StatType.Hunger;
                Debug.Log($"[AIStats] Death condition met: Hunger={GetRawStat(StatType.Hunger)} (raw), {GetStat(StatType.Hunger)} (display)");
            }
            else if (currentStats.ContainsKey(StatType.Thirst) && GetRawStat(StatType.Thirst) <= 0)
            {
                deathCause = StatType.Thirst;
                Debug.Log($"[AIStats] Death condition met: Thirst={GetRawStat(StatType.Thirst)} (raw), {GetStat(StatType.Thirst)} (display)");
            }
            
            if (deathCause.HasValue && !isDead)
            {
                Debug.Log($"[AIStats] Triggering death from {deathCause.Value}");
                Die(deathCause.Value);
            }
        }
        
        private void Die(StatType cause)
        {
            Debug.Log($"[AIStats] Die() called - Setting isDead=true, cause={cause}");
            isDead = true;
            lastDeathCause = cause;
            
            Debug.Log($"[AIStats] Invoking OnDeath event (event exists: {OnDeath != null})");
            OnDeath?.Invoke(new AIDeathEventArgs(cause, transform.position, timeSinceSpawn));
            
            Debug.Log($"[AIStats] Death complete. IsDead={isDead}");
        }
        
        public void Respawn(Vector3 spawnPosition, bool applyPenalties = true)
        {
            if (!isDead)
            {
                Debug.LogWarning("[AIStats] Respawn called but not dead!");
                return;
            }
            
            Debug.Log($"[AIStats] Respawning at {spawnPosition}");
            Debug.Log($"[AIStats] Current position before respawn: {transform.position}");
            
            // 先恢复状态值，避免在设置位置后又被检测为死亡
            float respawnPercent = config != null ? config.respawnStatPercentage : 0.5f;
            float maxHealthValue = config != null ? config.maxHealth : 100f;
            float maxHungerValue = config != null ? config.maxHunger : 100f;
            float maxThirstValue = config != null ? config.maxThirst : 100f;
            float maxStaminaValue = config != null ? config.maxStamina : 100f;
            
            currentStats[StatType.Health] = maxHealthValue * respawnPercent;
            currentStats[StatType.Hunger] = maxHungerValue * respawnPercent;
            currentStats[StatType.Thirst] = maxThirstValue * respawnPercent;
            currentStats[StatType.Stamina] = maxStaminaValue * respawnPercent;
            
            // 设置isDead为false
            isDead = false;
            
            // 设置新位置
            transform.position = spawnPosition;
            
            // 强制同步Transform
            if (GetComponent<Rigidbody2D>() != null)
            {
                Physics2D.SyncTransforms();
            }
            
            Debug.Log($"[AIStats] Position after setting: {transform.position}");
            Debug.Log($"[AIStats] Respawned with Health: {currentStats[StatType.Health]}, IsDead: {isDead}");
            
            // 根据死亡原因应用惩罚
            if (applyPenalties && lastDeathCause.HasValue)
            {
                ApplyDeathPenalty(lastDeathCause.Value);
                lastDeathCause = null;
            }
            
            timeSinceSpawn = 0f;
            OnRespawn?.Invoke();
        }
        
        private void ApplyDeathPenalty(StatType deathCause)
        {
            Debug.Log($"[AIStats] Applying death penalty for cause: {deathCause}");
            
            var inventory = GetComponent<Inventory.Inventory>();
            var currencyManager = GetComponent<Inventory.Managers.CurrencyManager>();
            var ammoManager = GetComponent<Inventory.Managers.AmmoManager>();
            
            switch (deathCause)
            {
                case StatType.Health:
                    // 生命归零：清空所有物品（背包、金币、弹药）
                    Debug.Log("[AIStats] Death by health loss - clearing all items");
                    if (inventory != null) inventory.DropAllItems();
                    if (currencyManager != null) currencyManager.SpendGold(currencyManager.CurrentGold);
                    if (ammoManager != null)
                    {
                        ammoManager.UseAmmo(Inventory.AmmoType.Bullets, ammoManager.GetAmmo(Inventory.AmmoType.Bullets));
                        ammoManager.UseAmmo(Inventory.AmmoType.Arrows, ammoManager.GetAmmo(Inventory.AmmoType.Arrows));
                        ammoManager.UseAmmo(Inventory.AmmoType.Mana, ammoManager.GetAmmo(Inventory.AmmoType.Mana));
                    }
                    break;
                    
                case StatType.Hunger:
                    // 饥饿归零：只清空金币
                    Debug.Log("[AIStats] Death by hunger - clearing gold");
                    if (currencyManager != null) currencyManager.SpendGold(currencyManager.CurrentGold);
                    break;
                    
                case StatType.Thirst:
                    // 口渴归零：只清空药水类物品
                    Debug.Log("[AIStats] Death by thirst - clearing potions");
                    if (inventory != null)
                    {
                        for (int i = 0; i < inventory.Size; i++)
                        {
                            var slot = inventory.GetSlot(i);
                            if (!slot.IsEmpty && slot.Item is Inventory.Items.ConsumableItem consumable)
                            {
                                // 通过名称判断是否是药水或饮料
                                if (slot.Item.ItemName.Contains("药水") || slot.Item.ItemName.Contains("饮料") ||
                                    slot.Item.ItemName.ToLower().Contains("potion") || slot.Item.ItemName.ToLower().Contains("drink"))
                                {
                                    inventory.RemoveItemAt(i, slot.Quantity);
                                }
                            }
                        }
                    }
                    break;
            }
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
            
            // 为了UI显示，将负值显示为0（但内部保持负值用于死亡检测）
            return Mathf.Max(0f, finalValue);
        }
        
        /// <summary>
        /// 获取原始属性值（包括负值），用于死亡检测等内部逻辑
        /// </summary>
        public float GetRawStat(StatType statType)
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
            
            // 限制在最大值范围内（允许负值）
            finalValue = ClampStatValue(statType, finalValue);
            
            return finalValue; // 返回真实值，包括负值
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
        
        public void ModifyMood(MoodDimension dimension, float amount, StatChangeReason reason = StatChangeReason.Other)
        {
            switch (dimension)
            {
                case MoodDimension.Social:
                    mood.ImproveSocial(amount);
                    break;
                // 可以扩展其他维度的修改方法
            }
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
            
            // 对于生命关键属性，允许负值以触发死亡检测
            // 但其他属性（如弹药、护甲）仍然限制在0以上
            switch (statType)
            {
                case StatType.Health:
                case StatType.Hunger:
                case StatType.Thirst:
                    return Mathf.Clamp(value, float.MinValue, max);
                default:
                    return Mathf.Clamp(value, 0f, max);
            }
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