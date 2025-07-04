using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    /// <summary>
    /// 冰霜减速Debuff - 用于冰系武器
    /// </summary>
    [CreateAssetMenu(fileName = "FrostSlowDebuff", menuName = "Buffs/Weapon/Frost Slow Debuff")]
    public class FrostSlowDebuff : BuffBase
    {
        [Header("Frost Settings")]
        [SerializeField] private float slowPercentage = 30f;
        [SerializeField] private float armorReduction = 5f;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 基础设置
            buffName = "冰霜减速";
            description = $"移动速度降低{slowPercentage}%，护甲减少{armorReduction}点";
            buffType = BuffType.Debuff;
            duration = 5f;
            
            // 武器触发设置
            applicationMode = BuffApplicationMode.OnHit;
            applicationChance = 0.4f; // 40%概率
            requiresCrit = false;
            
            // 叠加设置
            isStackable = true;
            maxStacks = 3;
            stackMode = StackMode.Stack; // 叠加层数，增强效果
            
            // 效果设置
            effects.Clear();
            
            // 减速效果
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.Instant,
                targetStat = StatType.Stamina, // 临时用体力代表速度
                modifierType = StatModifierType.Percentage,
                value = -slowPercentage,
                scaleWithStacks = true,
                stackMultiplier = 0.5f // 每层额外50%减速
            });
            
            // 护甲减少
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.Instant,
                targetStat = StatType.Armor,
                modifierType = StatModifierType.Flat,
                value = -armorReduction,
                scaleWithStacks = true,
                stackMultiplier = 1f // 每层减少相同护甲
            });
            
            // 视觉效果
            buffColor = new Color(0.5f, 0.8f, 1f, 0.5f); // 冰蓝色
        }
    }
}