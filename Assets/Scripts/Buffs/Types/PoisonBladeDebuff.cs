using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    /// <summary>
    /// 毒刃Debuff - 专门用于武器的中毒效果
    /// </summary>
    [CreateAssetMenu(fileName = "PoisonBladeDebuff", menuName = "Buffs/Weapon/Poison Blade Debuff")]
    public class PoisonBladeDebuff : BuffBase
    {
        [Header("Poison Blade Settings")]
        [SerializeField] private float poisonDamagePerTick = 3f;
        [SerializeField] private float slowPercentage = 15f;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 基础设置
            buffName = "毒刃";
            description = $"每秒受到{poisonDamagePerTick}点毒素伤害，移动速度降低{slowPercentage}%";
            buffType = BuffType.Debuff;
            duration = 8f;
            
            // 武器相关设置
            applicationMode = BuffApplicationMode.OnHit;
            applicationChance = 0.25f; // 25%概率
            requiresCrit = false; // 不需要暴击也能触发
            
            // 叠加设置
            isStackable = true;
            maxStacks = 5;
            stackMode = StackMode.Refresh; // 刷新持续时间
            
            // 周期性伤害
            tickOverTime = true;
            tickInterval = 1f;
            
            // 效果设置
            effects.Clear();
            
            // 持续毒素伤害
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.OverTime,
                targetStat = StatType.Health,
                modifierType = StatModifierType.Flat,
                value = -poisonDamagePerTick,
                scaleWithStacks = true,
                stackMultiplier = 0.5f // 每层额外50%伤害
            });
            
            // 减速效果
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.Instant,
                targetStat = StatType.Stamina, // 临时用体力代表速度
                modifierType = StatModifierType.Percentage,
                value = -slowPercentage,
                scaleWithStacks = false // 减速不叠加
            });
            
            // 视觉效果
            buffColor = new Color(0.2f, 0.8f, 0.2f, 0.5f); // 绿色
        }
    }
}