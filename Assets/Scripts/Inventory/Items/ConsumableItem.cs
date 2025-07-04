using UnityEngine;
using System.Collections.Generic;
using Inventory.Interfaces;
using AI.Stats;
using Buffs;

namespace Inventory.Items
{
    [CreateAssetMenu(fileName = "New Consumable", menuName = "Inventory/Items/Consumable")]
    public class ConsumableItem : ItemBase, IUsable
    {
        [Header("Consumable Settings")]
        [SerializeField] private ConsumableType consumableType;
        [SerializeField] private float useTime = 1f;
        [SerializeField] private string useAnimation = "UseItem";
        
        [Header("Stat Effects")]
        [SerializeField] private List<StatEffect> statEffects = new List<StatEffect>();
        
        [Header("Buff Effects")]
        [SerializeField] private List<BuffBase> appliedBuffs = new List<BuffBase>();
        
        [Header("Requirements")]
        [SerializeField] private float minHealthToUse = 0f;
        [SerializeField] private bool requiresNotInCombat = false;
        
        // IUsable implementation
        public float UseTime => useTime;
        public string UseAnimation => useAnimation;
        
        public bool CanUse(GameObject user)
        {
            if (user == null) return false;
            
            var aiStats = user.GetComponent<AIStats>();
            if (aiStats == null) return false;
            
            // Check health requirement
            if (aiStats.GetStat(StatType.Health) < minHealthToUse)
                return false;
            
            // Check combat requirement (需要战斗系统实现后补充)
            if (requiresNotInCombat)
            {
                // TODO: Check if in combat
            }
            
            return true;
        }
        
        public void Use(GameObject user)
        {
            if (!CanUse(user)) return;
            
            var aiStats = user.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            // Apply immediate stat effects
            foreach (var effect in statEffects)
            {
                if (effect.isPercentage)
                {
                    float maxValue = GetMaxStatValue(aiStats, effect.statType);
                    float amount = maxValue * (effect.value / 100f);
                    aiStats.ModifyStat(effect.statType, amount, StatChangeReason.Item);
                }
                else
                {
                    aiStats.ModifyStat(effect.statType, effect.value, StatChangeReason.Item);
                }
            }
            
            // Apply buff effects using BuffManager
            var buffManager = user.GetComponent<BuffManager>();
            if (buffManager != null)
            {
                foreach (var buff in appliedBuffs)
                {
                    if (buff != null)
                    {
                        buffManager.AddBuff(buff);
                    }
                }
            }
            
            // Special effects based on consumable type
            ApplySpecialEffects(user, aiStats);
            
            Debug.Log($"{user.name} used {itemName}");
        }
        
        private void ApplySpecialEffects(GameObject user, AIStats aiStats)
        {
            switch (consumableType)
            {
                case ConsumableType.Food:
                    // Food might also slightly improve mood
                    if (aiStats.GetStat(StatType.Hunger) > 80f)
                    {
                        aiStats.TriggerFaceToFaceInteraction(false); // Small mood boost
                    }
                    break;
                    
                case ConsumableType.Drink:
                    // Drinks restore thirst faster
                    break;
                    
                case ConsumableType.Potion:
                    // Potions might have particle effects
                    break;
            }
        }
        
        private float GetMaxStatValue(AIStats aiStats, StatType statType)
        {
            // This is a helper method since we can't directly access max values
            // In a real implementation, AIStats should expose max values
            switch (statType)
            {
                case StatType.Health: return 100f;
                case StatType.Hunger: return 100f;
                case StatType.Thirst: return 100f;
                case StatType.Stamina: return 100f;
                default: return 100f;
            }
        }
        
        public override string GetTooltipText()
        {
            var tooltip = base.GetTooltipText();
            
            // Add stat effects
            if (statEffects.Count > 0)
            {
                tooltip += "\n\n<b>效果:</b>";
                foreach (var effect in statEffects)
                {
                    string sign = effect.value >= 0 ? "+" : "";
                    string suffix = effect.isPercentage ? "%" : "";
                    tooltip += $"\n{sign}{effect.value}{suffix} {effect.statType}";
                }
            }
            
            // Add buff effects
            if (appliedBuffs.Count > 0)
            {
                tooltip += "\n\n<b>增益:</b>";
                foreach (var buff in appliedBuffs)
                {
                    if (buff != null)
                    {
                        tooltip += $"\n{buff.BuffName} ({buff.Duration}秒)";
                    }
                }
            }
            
            return tooltip;
        }
        
        protected override void OnValidate()
        {
            base.OnValidate();
            itemType = ItemType.Consumable;
            
            // Set default stack sizes based on consumable type
            if (maxStackSize == 1)
            {
                switch (consumableType)
                {
                    case ConsumableType.Food:
                    case ConsumableType.Drink:
                        maxStackSize = 10;
                        break;
                    case ConsumableType.Potion:
                        maxStackSize = 5;
                        break;
                    default:
                        maxStackSize = 20;
                        break;
                }
            }
        }
    }
    
    [System.Serializable]
    public class StatEffect
    {
        public StatType statType;
        public float value;
        public bool isPercentage;
    }
    
}