using UnityEngine;
using Inventory.Interfaces;
using System.Collections.Generic;
using Buffs;

namespace Inventory.Items
{
    [CreateAssetMenu(fileName = "New Weapon", menuName = "Inventory/Items/Weapon")]
    public class WeaponItem : ItemBase, IEquipable
    {
        [Header("Weapon Settings")]
        [SerializeField] private WeaponType weaponType;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float attackSpeed = 1f;
        
        [Header("Range Settings")]
        [SerializeField] private float attackRange = 1f;  // 武器的攻击距离（近战）或发射距离（远程）
        [SerializeField] private float projectileSpeed = 10f;  // 弹药飞行速度（仅远程武器）
        [SerializeField] private AttackShape attackShape = AttackShape.Circle;  // 攻击形状
        [SerializeField] private float effectRadius = 0f;  // 作用半径，0为单体攻击，>0为范围攻击
        [SerializeField] private float sectorAngle = 90f;  // 扇形角度（仅扇形攻击）
        [SerializeField] private float rectangleWidth = 2f;  // 矩形宽度（仅矩形攻击）
        
        [Header("Ammo Settings")]
        [SerializeField] private AmmoType requiredAmmo = AmmoType.None;
        [SerializeField] private int ammoPerShot = 1;
        
        [Header("Combat Settings")]
        [SerializeField] private string attackAnimation = "Attack";
        [SerializeField] private float knockback = 0f;
        [SerializeField] private float criticalChance = 0.05f;
        [SerializeField] private float criticalMultiplier = 2f;
        [SerializeField] private bool isPiercing = false;  // 是否穿透（远程武器）
        [SerializeField] private int maxPierceTargets = 1;  // 最大穿透目标数
        
        [Header("Special Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip hitSound;
        
        [Header("Debuff Effects")]
        [SerializeField] private List<BuffBase> onHitDebuffs = new List<BuffBase>();
        [SerializeField] private float debuffChance = 1f;
        [SerializeField] private bool debuffRequiresCrit = false;
        
        [Header("Visual")]
        [SerializeField] private GameObject weaponModelPrefab;
        [SerializeField] private GameObject projectilePrefab;  // 弹药预制体（远程武器）
        [SerializeField] private Vector3 holdOffset;
        [SerializeField] private Vector3 holdRotation;
        
        // Properties
        public WeaponType WeaponType => weaponType;
        public float Damage => damage;
        public float AttackSpeed => attackSpeed;
        public float AttackRange => attackRange;
        public float ProjectileSpeed => projectileSpeed;
        public AttackShape AttackShape => attackShape;
        public float EffectRadius => effectRadius;
        public float SectorAngle => sectorAngle;
        public float RectangleWidth => rectangleWidth;
        public AmmoType RequiredAmmo => requiredAmmo;
        public int AmmoPerShot => ammoPerShot;
        public string AttackAnimation => attackAnimation;
        public float Knockback => knockback;
        public float CriticalChance => criticalChance;
        public float CriticalMultiplier => criticalMultiplier;
        public bool IsPiercing => isPiercing;
        public int MaxPierceTargets => maxPierceTargets;
        public GameObject WeaponModelPrefab => weaponModelPrefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public List<BuffBase> OnHitDebuffs => onHitDebuffs;
        public float DebuffChance => debuffChance;
        public bool DebuffRequiresCrit => debuffRequiresCrit;
        
        // Calculated properties
        public bool IsAreaOfEffect => effectRadius > 0f;
        public bool IsRangedWeapon => weaponType == WeaponType.Ranged || weaponType == WeaponType.Magic;
        
        // IEquipable implementation
        public EquipmentSlot Slot => EquipmentSlot.MainHand;
        
        public bool CanEquip(GameObject user)
        {
            if (user == null) return false;
            
            // Check if user has required stats or level
            // For now, always return true
            return true;
        }
        
        public void OnEquip(GameObject user)
        {
            if (!CanEquip(user)) return;
            
            // Create weapon visual if exists
            if (weaponModelPrefab != null)
            {
                var weaponVisual = Instantiate(weaponModelPrefab, user.transform);
                weaponVisual.transform.localPosition = holdOffset;
                weaponVisual.transform.localEulerAngles = holdRotation;
                weaponVisual.name = $"Weapon_{itemName}";
            }
            
            Debug.Log($"{user.name} equipped {itemName}");
        }
        
        public void OnUnequip(GameObject user)
        {
            if (user == null) return;
            
            // Remove weapon visual by name instead of tag
            Transform weaponTransform = user.transform.Find($"Weapon_{itemName}");
            if (weaponTransform != null)
            {
                Destroy(weaponTransform.gameObject);
            }
            
            Debug.Log($"{user.name} unequipped {itemName}");
        }
        
        public float CalculateDamage(bool isCritical = false)
        {
            float finalDamage = damage;
            if (isCritical)
            {
                finalDamage *= criticalMultiplier;
            }
            return finalDamage;
        }
        
        public bool RollCritical()
        {
            return Random.value <= criticalChance;
        }
        
        public bool HasAmmo(int currentAmmo)
        {
            if (requiredAmmo == AmmoType.None) return true;
            return currentAmmo >= ammoPerShot;
        }
        
        public override string GetTooltipText()
        {
            var tooltip = base.GetTooltipText();
            
            tooltip += $"\n\n<b>武器属性:</b>";
            tooltip += $"\n伤害: {damage}";
            tooltip += $"\n攻击速度: {attackSpeed}/秒";
            
            // 根据武器类型显示不同的范围信息
            if (IsRangedWeapon)
            {
                tooltip += $"\n射程: {attackRange}米";
                tooltip += $"\n弹速: {projectileSpeed}米/秒";
            }
            else
            {
                tooltip += $"\n攻击距离: {attackRange}米";
            }
            
            if (IsAreaOfEffect)
            {
                tooltip += $"\n作用范围: {effectRadius}米半径";
            }
            
            tooltip += $"\n暴击率: {criticalChance * 100}%";
            tooltip += $"\n暴击伤害: {criticalMultiplier * 100}%";
            
            if (knockback > 0)
            {
                tooltip += $"\n击退力: {knockback}";
            }
            
            if (isPiercing)
            {
                tooltip += $"\n穿透目标: {maxPierceTargets}个";
            }
            
            if (requiredAmmo != AmmoType.None)
            {
                tooltip += $"\n\n<b>弹药需求:</b>";
                tooltip += $"\n类型: {requiredAmmo}";
                tooltip += $"\n每次消耗: {ammoPerShot}";
            }
            
            return tooltip;
        }
        
        protected override void OnValidate()
        {
            base.OnValidate();
            itemType = ItemType.Weapon;
            maxStackSize = 1; // Weapons don't stack
            
            // Set ammo type based on weapon type
            if (weaponType == WeaponType.Ranged && requiredAmmo == AmmoType.None)
            {
                requiredAmmo = AmmoType.Bullets;
            }
            else if (weaponType == WeaponType.Magic && requiredAmmo == AmmoType.None)
            {
                requiredAmmo = AmmoType.Mana;
            }
        }
    }
}