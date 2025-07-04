using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    [CreateAssetMenu(fileName = "PoisonDebuff", menuName = "Buffs/Poison Debuff")]
    public class PoisonDebuff : BuffBase
    {
        [Header("Poison Settings")]
        [SerializeField] private float damagePerTick = 5f;
        [SerializeField] private float slowEffect = 20f; // 减速20%
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置基础属性
            buffName = "中毒";
            description = $"每秒受到{damagePerTick}点伤害，移动速度降低{slowEffect}%";
            buffType = BuffType.Debuff;
            duration = 15f;
            tickOverTime = true;
            tickInterval = 1f;
            buffColor = Color.green;
            
            // 设置效果
            effects.Clear();
            
            // 持续伤害
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.OverTime,
                targetStat = StatType.Health,
                modifierType = StatModifierType.Flat,
                value = -damagePerTick
            });
            
            // 减速效果
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.OverTime,
                targetStat = StatType.Stamina, // 临时用体力代表速度
                modifierType = StatModifierType.Percentage,
                value = -slowEffect
            });
        }
    }
}