using UnityEngine;
using System;
using AI.Stats;

namespace AI.Config
{
    [CreateAssetMenu(fileName = "AIStatsConfig", menuName = "AI/Stats Config")]
    public class AIStatsConfig : ScriptableObject
    {
        [Header("Initial Values")]
        public float initialHealth = 100f;
        public float initialHunger = 100f;
        public float initialThirst = 100f;
        public float initialStamina = 100f;
        public float initialArmor = 0f;
        public float initialToughness = 50f;
        
        [Header("Ammo Initial Values")]
        public float initialBullets = 30f;
        public float initialArrows = 20f;
        public float initialMana = 50f;
        
        [Header("Max Values")]
        public float maxHealth = 100f;
        public float maxHunger = 100f;
        public float maxThirst = 100f;
        public float maxStamina = 100f;
        public float maxArmor = 100f;
        public float maxToughness = 100f;
        public float maxBullets = 999f;
        public float maxArrows = 999f;
        public float maxMana = 100f;
        
        [Header("Depletion Rates (per second)")]
        public float hungerDepletionRate = 0.5f;  // 每秒减少0.5
        public float thirstDepletionRate = 0.8f;  // 每秒减少0.8
        public float staminaRecoveryRate = 2f;    // 每秒恢复2（休息时）
        public float staminaDepletionRate = 5f;   // 每秒消耗5（行动时）
        
        [Header("Critical Thresholds")]
        public float criticalHealthThreshold = 30f;
        public float criticalHungerThreshold = 30f;
        public float criticalThirstThreshold = 30f;
        
        [Header("Mood Impact Factors")]
        [Range(0, 1)] public float healthMoodImpact = 0.3f;
        [Range(0, 1)] public float hungerMoodImpact = 0.2f;
        [Range(0, 1)] public float thirstMoodImpact = 0.2f;
        [Range(0, 1)] public float staminaMoodImpact = 0.1f;
        [Range(0, 1)] public float socialDecayRate = 0.01f;  // 每秒社交值衰减
        
        [Header("Death Penalties")]
        public bool clearMoneyOnHealthDeath = true;
        public bool clearMoneyOnHungerDeath = true;
        public bool clearPotionsOnThirstDeath = true;
        public bool clearEquipmentOnHealthDeath = true;
        
        [Header("Respawn Settings")]
        [Range(0, 1)] public float respawnStatPercentage = 0.5f;  // 复活时恢复50%状态
    }
}