using UnityEngine;
using AI.Stats;
using System.Collections.Generic;
using Buffs.Core;

namespace Buffs
{
    /// <summary>
    /// Buff效果应用的时机
    /// </summary>
    public enum BuffApplicationMode
    {
        OnHit,          // 命中时施加（武器）
        OnUse,          // 使用时施加（消耗品）
        OnDamaged,      // 受伤时施加（反伤类）
        OnAttack,       // 攻击时施加（敌人）
        Aura,           // 光环效果
        Triggered       // 条件触发
    }
    [CreateAssetMenu(fileName = "NewBuff", menuName = "Buffs/Base Buff")]
    public class BuffBase : ScriptableObject
    {
        [Header("Basic Info")]
        [SerializeField] protected string buffId;
        [SerializeField] protected string buffName;
        [SerializeField] protected string description;
        [SerializeField] protected Sprite icon;
        [SerializeField] protected BuffType buffType = BuffType.Buff;
        [SerializeField] protected BuffApplicationMode applicationMode = BuffApplicationMode.OnUse;
        
        [Header("Duration")]
        [SerializeField] protected float duration = 10f;
        [SerializeField] protected bool isPermanent = false;
        [SerializeField] protected bool isStackable = false;
        [SerializeField] protected int maxStacks = 1;
        [SerializeField] protected StackMode stackMode = StackMode.Replace;
        
        [Header("Effects")]
        [SerializeField] protected List<BuffEffect> effects = new List<BuffEffect>();
        
        [Header("Visual")]
        [SerializeField] protected GameObject visualEffectPrefab;
        [SerializeField] protected Color buffColor = Color.white;
        
        [Header("Triggers")]
        [SerializeField] protected bool tickOverTime = false;
        [SerializeField] protected float tickInterval = 1f;
        
        [Header("Application Conditions")]
        [SerializeField] protected float applicationChance = 1f; // 施加概率
        [SerializeField] protected bool requiresCrit = false; // 需要暴击才能施加
        [SerializeField] protected bool cleansable = true; // 可被净化
        
        // Properties
        public string BuffId => buffId;
        public string BuffName => buffName;
        public string Description => description;
        public Sprite Icon => icon;
        public BuffType Type => buffType;
        public float Duration => duration;
        public bool IsPermanent => isPermanent;
        public bool IsStackable => isStackable;
        public int MaxStacks => maxStacks;
        public bool TickOverTime => tickOverTime;
        public float TickInterval => tickInterval;
        public List<BuffEffect> Effects => effects;
        public GameObject VisualEffectPrefab => visualEffectPrefab;
        public Color BuffColor => buffColor;
        public BuffApplicationMode ApplicationMode => applicationMode;
        public float ApplicationChance => applicationChance;
        public bool RequiresCrit => requiresCrit;
        public bool Cleansable => cleansable;
        public StackMode StackMode => stackMode;
        
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(buffId))
            {
                buffId = name.ToLower().Replace(" ", "_");
            }
        }
        
        public virtual BuffInstance CreateInstance(GameObject target)
        {
            return new BuffInstance(this, target);
        }
    }
    
    // BuffEffect类已移到Buffs.Core命名空间
    
    public enum BuffType
    {
        Buff,    // 正面效果
        Debuff,  // 负面效果
        Neutral  // 中性效果
    }
    
    public enum BuffEffectType
    {
        Instant,        // 立即生效
        OverTime,       // 持续效果
        OnExpire,       // 结束时生效
        OnStack         // 叠加时生效
    }
    
    public enum StackMode
    {
        Replace,        // 替换现有
        Stack,          // 增加层数
        Refresh,        // 刷新时间
        Extend,         // 延长时间
        Independent     // 独立存在
    }
}