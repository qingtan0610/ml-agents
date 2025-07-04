using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    [CreateAssetMenu(fileName = "SpeedBuff", menuName = "Buffs/Speed Buff")]
    public class SpeedBuff : BuffBase
    {
        [Header("Speed Settings")]
        [SerializeField] private float speedIncrease = 50f; // 50%速度提升
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置基础属性
            buffName = "速度提升";
            description = $"移动速度提升{speedIncrease}%";
            buffType = BuffType.Buff;
            duration = 10f;
            
            // 清空并重新设置效果
            effects.Clear();
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.OverTime,
                targetStat = StatType.Stamina, // 这里应该是移动速度，但当前没有这个属性
                modifierType = StatModifierType.Percentage,
                value = speedIncrease
            });
        }
    }
}