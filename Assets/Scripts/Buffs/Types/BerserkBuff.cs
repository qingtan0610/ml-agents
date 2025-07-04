using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    /// <summary>
    /// 狂暴Buff - 暴击时触发，增加攻击速度和暴击率
    /// </summary>
    [CreateAssetMenu(fileName = "BerserkBuff", menuName = "Buffs/Weapon/Berserk Buff")]
    public class BerserkBuff : BuffBase
    {
        [Header("Berserk Settings")]
        [SerializeField] private float attackSpeedIncrease = 50f;
        [SerializeField] private float critChanceIncrease = 20f;
        [SerializeField] private float damageIncrease = 25f;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 基础设置
            buffName = "狂暴";
            description = $"攻击速度+{attackSpeedIncrease}%，暴击率+{critChanceIncrease}%，伤害+{damageIncrease}%";
            buffType = BuffType.Buff;
            duration = 6f;
            
            // 特殊触发条件：需要暴击才能触发
            applicationMode = BuffApplicationMode.OnHit;
            applicationChance = 0.5f; // 暴击时50%概率触发
            requiresCrit = true; // 必须暴击
            
            // 可以叠加持续时间
            isStackable = false;
            stackMode = StackMode.Extend; // 延长持续时间
            
            // 不可被净化（正面效果）
            cleansable = false;
            
            // 效果设置
            effects.Clear();
            
            // 攻击速度提升（暂时用体力模拟）
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.Instant,
                targetStat = StatType.Stamina,
                modifierType = StatModifierType.Percentage,
                value = attackSpeedIncrease
            });
            
            // 视觉效果
            buffColor = new Color(1f, 0.2f, 0.2f, 0.5f); // 红色光环
        }
    }
}