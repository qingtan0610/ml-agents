using UnityEngine;
using AI.Stats;
using Buffs.Core;

namespace Buffs.Types
{
    [CreateAssetMenu(fileName = "RegenerationBuff", menuName = "Buffs/Regeneration Buff")]
    public class RegenerationBuff : BuffBase
    {
        [Header("Regeneration Settings")]
        [SerializeField] private float healPerTick = 3f;
        [SerializeField] private float armorBonus = 10f;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置基础属性
            buffName = "生命回复";
            description = $"每秒恢复{healPerTick}点生命值，护甲+{armorBonus}";
            buffType = BuffType.Buff;
            duration = 20f;
            tickOverTime = true;
            tickInterval = 1f;
            buffColor = new Color(0f, 1f, 0f, 0.5f);
            isStackable = true;
            maxStacks = 3;
            
            // 设置效果
            effects.Clear();
            
            // 持续治疗
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.OverTime,
                targetStat = StatType.Health,
                modifierType = StatModifierType.Flat,
                value = healPerTick
            });
            
            // 护甲加成
            effects.Add(new BuffEffect
            {
                effectType = BuffEffectType.Instant,
                targetStat = StatType.Armor,
                modifierType = StatModifierType.Flat,
                value = armorBonus
            });
        }
    }
}