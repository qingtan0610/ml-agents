using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    /// <summary>
    /// 虚弱Debuff - 用于敌人攻击施加
    /// </summary>
    [CreateAssetMenu(fileName = "WeakenDebuff", menuName = "Buffs/Enemy/Weaken Debuff")]
    public class WeakenDebuff : BuffBase
    {
        [Header("Weaken Settings")]
        [SerializeField] private float damageReduction = 25f;
        [SerializeField] private float staminaReduction = 20f;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 基础设置
            buffName = "虚弱";
            description = $"攻击力降低{damageReduction}%，体力消耗增加{staminaReduction}%";
            buffType = BuffType.Debuff;
            duration = 12f;
            isPermanent = false;
            
            // 敌人攻击触发
            applicationMode = BuffApplicationMode.OnAttack;
            applicationChance = 1f; // 由敌人的debuffChance控制，这里设为100%
            requiresCrit = false;
            
            // 不可叠加，但刷新持续时间
            isStackable = false;
            stackMode = StackMode.Refresh;
            
            // 可被净化
            cleansable = true;
            
            // 效果设置
            effects.Clear();
            
            // 降低攻击力（暂时用体力模拟）
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.Instant,
                targetStat = StatType.Stamina,
                modifierType = StatModifierType.Percentage,
                value = -damageReduction
            });
            
            // 视觉效果
            buffColor = new Color(0.8f, 0.5f, 0.5f, 0.5f); // 暗红色
        }
    }
}