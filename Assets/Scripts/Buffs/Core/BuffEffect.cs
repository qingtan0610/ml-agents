using UnityEngine;
using AI.Stats;

namespace Buffs.Core
{
    /// <summary>
    /// Buff效果的核心定义，供所有系统使用
    /// </summary>
    [System.Serializable]
    public class BuffEffect
    {
        [Header("Effect Settings")]
        public BuffEffectType effectType = BuffEffectType.Instant;
        public StatType targetStat = StatType.Health;
        public StatModifierType modifierType = StatModifierType.Flat;
        public float value = 0f;
        
        [Header("Advanced")]
        public AnimationCurve valueCurve; // 用于随时间变化的效果
        public bool scaleWithStacks = true; // 是否随层数增加
        public float stackMultiplier = 1f; // 每层的倍率
        
        /// <summary>
        /// 创建属性修饰器
        /// </summary>
        public StatModifier CreateModifier(string buffId, float duration = -1, int stacks = 1)
        {
            float finalValue = value;
            if (scaleWithStacks && stacks > 1)
            {
                finalValue = value + (value * stackMultiplier * (stacks - 1));
            }
            
            return new StatModifier(
                $"{buffId}_{targetStat}_{System.Guid.NewGuid()}",
                targetStat,
                modifierType,
                finalValue,
                duration
            );
        }
        
        /// <summary>
        /// 获取指定时间点的效果值
        /// </summary>
        public float GetValueAtTime(float normalizedTime)
        {
            if (valueCurve != null && valueCurve.length > 0)
            {
                return value * valueCurve.Evaluate(normalizedTime);
            }
            return value;
        }
    }
    
    /// <summary>
    /// Buff应用结果
    /// </summary>
    public class BuffApplicationResult
    {
        public bool Success { get; set; }
        public string BuffId { get; set; }
        public string Reason { get; set; }
        
        public BuffApplicationResult(bool success, string buffId, string reason = "")
        {
            Success = success;
            BuffId = buffId;
            Reason = reason;
        }
    }
}