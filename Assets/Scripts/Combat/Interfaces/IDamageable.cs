using UnityEngine;
using System.Collections.Generic;
using Buffs;

namespace Combat.Interfaces
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }
        
        void TakeDamage(float damage, GameObject attacker, DamageInfo damageInfo = null);
        void Heal(float amount);
        void Die();
    }
    
    // 伤害信息
    public class DamageInfo
    {
        public float baseDamage;
        public bool isCritical;
        public float knockback;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public DamageType damageType;
        public bool isTrap; // 是否来自陷阱
        
        // Buff/Debuff相关
        public List<BuffBase> appliedDebuffs;
        public float debuffChance = 1f;
        
        public DamageInfo(float damage)
        {
            baseDamage = damage;
            damageType = DamageType.Physical;
            appliedDebuffs = new List<BuffBase>();
        }
        
        public void AddDebuff(BuffBase debuff)
        {
            if (debuff != null && appliedDebuffs != null)
            {
                appliedDebuffs.Add(debuff);
            }
        }
    }
    
    public enum DamageType
    {
        Physical,
        Magic,
        True  // 真实伤害，无视防御
    }
}