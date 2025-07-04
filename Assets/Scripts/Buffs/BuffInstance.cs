using UnityEngine;
using AI.Stats;
using System.Collections.Generic;
using Buffs.Core;

namespace Buffs
{
    /// <summary>
    /// 运行时的Buff实例
    /// </summary>
    public class BuffInstance
    {
        private BuffBase buffData;
        private GameObject target;
        private float remainingTime;
        private int currentStacks;
        private float tickTimer;
        private List<StatModifier> activeModifiers = new List<StatModifier>();
        private GameObject visualEffect;
        
        // Properties
        public string BuffId => buffData.BuffId;
        public string BuffName => buffData.BuffName;
        public BuffType Type => buffData.Type;
        public float RemainingTime => remainingTime;
        public int CurrentStacks => currentStacks;
        public bool IsExpired => !buffData.IsPermanent && remainingTime <= 0;
        public BuffBase Data => buffData;
        
        public BuffInstance(BuffBase data, GameObject target)
        {
            this.buffData = data;
            this.target = target;
            this.remainingTime = data.Duration;
            this.currentStacks = 1;
            this.tickTimer = 0f;
            
            ApplyEffects();
            CreateVisualEffect();
        }
        
        public void Update(float deltaTime)
        {
            if (buffData.IsPermanent) return;
            
            remainingTime -= deltaTime;
            
            // 处理周期性效果
            if (buffData.TickOverTime)
            {
                tickTimer += deltaTime;
                if (tickTimer >= buffData.TickInterval)
                {
                    ApplyTickEffects();
                    tickTimer = 0f;
                }
            }
        }
        
        public void AddStack()
        {
            if (!buffData.IsStackable) return;
            
            if (currentStacks < buffData.MaxStacks)
            {
                currentStacks++;
                OnStackAdded();
            }
            
            // 刷新持续时间
            remainingTime = buffData.Duration;
        }
        
        public void Remove()
        {
            RemoveEffects();
            DestroyVisualEffect();
            
            // 应用结束时效果
            ApplyExpireEffects();
        }
        
        private void ApplyEffects()
        {
            var aiStats = target.GetComponent<AIStats>();
            
            foreach (var effect in buffData.Effects)
            {
                if (effect.effectType == BuffEffectType.Instant)
                {
                    // 对于即时效果，如果是百分比生命伤害，直接应用
                    if (effect.targetStat == StatType.Health && effect.modifierType == StatModifierType.Percentage && effect.value < 0)
                    {
                        ApplyInstantDamage(effect);
                    }
                    else if (aiStats != null)
                    {
                        // 其他效果需要AIStats
                        var modifier = effect.CreateModifier(buffData.BuffId, buffData.Duration, currentStacks);
                        aiStats.AddModifier(modifier);
                        activeModifiers.Add(modifier);
                    }
                }
                else if (effect.effectType == BuffEffectType.OverTime && aiStats != null)
                {
                    var modifier = effect.CreateModifier(buffData.BuffId, buffData.Duration, currentStacks);
                    aiStats.AddModifier(modifier);
                    activeModifiers.Add(modifier);
                }
            }
        }
        
        private void RemoveEffects()
        {
            var aiStats = target.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            foreach (var modifier in activeModifiers)
            {
                aiStats.RemoveModifier(modifier.Id);
            }
            activeModifiers.Clear();
        }
        
        private void ApplyInstantDamage(BuffEffect effect)
        {
            // 优先使用IDamageable接口（适用于所有可受伤对象）
            var damageable = target.GetComponent<Combat.Interfaces.IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                float damage = 0f;
                
                if (effect.modifierType == StatModifierType.Percentage)
                {
                    // 百分比伤害
                    damage = damageable.MaxHealth * (-effect.value / 100f);
                }
                else
                {
                    // 固定伤害
                    damage = -effect.value;
                }
                
                var damageInfo = new Combat.Interfaces.DamageInfo(damage)
                {
                    damageType = Combat.Interfaces.DamageType.True // 真实伤害
                };
                
                damageable.TakeDamage(damage, target, damageInfo);
                Debug.Log($"[Buff] {buffData.BuffName} dealt {damage} instant damage to {target.name}");
            }
        }
        
        private void ApplyTickEffects()
        {
            var aiStats = target.GetComponent<AIStats>();
            
            foreach (var effect in buffData.Effects)
            {
                if (effect.effectType == BuffEffectType.OverTime)
                {
                    if (effect.targetStat == StatType.Health && effect.value < 0)
                    {
                        // 对于生命值伤害，使用IDamageable
                        var damageable = target.GetComponent<Combat.Interfaces.IDamageable>();
                        if (damageable != null && !damageable.IsDead)
                        {
                            float damage = -effect.value;
                            if (effect.modifierType == StatModifierType.Percentage)
                            {
                                damage = damageable.MaxHealth * (-effect.value / 100f);
                            }
                            
                            var damageInfo = new Combat.Interfaces.DamageInfo(damage)
                            {
                                damageType = Combat.Interfaces.DamageType.True
                            };
                            
                            damageable.TakeDamage(damage, target, damageInfo);
                        }
                    }
                    else if (aiStats != null)
                    {
                        // 其他属性修改
                        aiStats.ModifyStat(effect.targetStat, effect.value, StatChangeReason.Buff);
                    }
                }
            }
        }
        
        private void ApplyExpireEffects()
        {
            var aiStats = target.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            foreach (var effect in buffData.Effects)
            {
                if (effect.effectType == BuffEffectType.OnExpire)
                {
                    aiStats.ModifyStat(effect.targetStat, effect.value, StatChangeReason.Buff);
                }
            }
        }
        
        private void OnStackAdded()
        {
            var aiStats = target.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            foreach (var effect in buffData.Effects)
            {
                if (effect.effectType == BuffEffectType.OnStack)
                {
                    aiStats.ModifyStat(effect.targetStat, effect.value, StatChangeReason.Buff);
                }
            }
            
            // 更新现有修饰器的值
            foreach (var modifier in activeModifiers)
            {
                // TODO: 实现叠加逻辑
            }
        }
        
        private void CreateVisualEffect()
        {
            if (buffData.VisualEffectPrefab != null)
            {
                visualEffect = Object.Instantiate(buffData.VisualEffectPrefab, target.transform);
                visualEffect.transform.localPosition = Vector3.zero;
            }
        }
        
        private void DestroyVisualEffect()
        {
            if (visualEffect != null)
            {
                Object.Destroy(visualEffect);
            }
        }
        
        public float GetRemainingPercentage()
        {
            if (buffData.IsPermanent) return 1f;
            return remainingTime / buffData.Duration;
        }
    }
}