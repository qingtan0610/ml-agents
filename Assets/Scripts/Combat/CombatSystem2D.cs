using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Inventory.Items;
using Inventory;
using Combat.Interfaces;

namespace Combat
{
    public class CombatSystem2D : MonoBehaviour
    {
        [Header("Components")]
        private Inventory.Inventory inventory;
        private Inventory.Managers.AmmoManager ammoManager;
        
        [Header("Combat Settings")]
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private float attackCooldown = 0f;
        [SerializeField] private Transform firePoint;
        
        [Header("Debug")]
        [SerializeField] private bool showAttackGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
        
        private float lastAttackTime;
        
        private void Awake()
        {
            inventory = GetComponent<Inventory.Inventory>();
            ammoManager = GetComponent<Inventory.Managers.AmmoManager>();
            
            // 如果没有设置发射点，创建一个
            if (firePoint == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0, 0.5f, 0);
                firePoint = fp.transform;
            }
        }
        
        public bool CanAttack()
        {
            if (Time.time - lastAttackTime < attackCooldown) return false;
            
            var weapon = inventory?.EquippedWeapon;
            if (weapon == null) return true; // 可以徒手攻击
            
            // 检查弹药
            if (weapon.RequiredAmmo != AmmoType.None)
            {
                return ammoManager.HasAmmo(weapon.RequiredAmmo, weapon.AmmoPerShot);
            }
            
            return true;
        }
        
        public void PerformAttack()
        {
            if (!CanAttack()) return;
            
            var weapon = inventory?.EquippedWeapon;
            
            if (weapon == null)
            {
                // 徒手攻击
                PerformMeleeAttack(5f, 1.5f, AttackShape.Sector, 90f);
            }
            else if (weapon.IsRangedWeapon)
            {
                // 远程攻击
                PerformRangedAttack(weapon);
            }
            else
            {
                // 近战攻击
                PerformMeleeAttack(weapon);
            }
            
            lastAttackTime = Time.time;
            
            // 更新攻击冷却
            if (weapon != null)
            {
                var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
                float attackSpeed = upgradeManager.GetUpgradedAttackSpeed(weapon);
                attackCooldown = 1f / attackSpeed;
            }
        }
        
        private void PerformMeleeAttack(WeaponItem weapon)
        {
            // 获取强化后的属性
            var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
            float damage = upgradeManager.GetUpgradedDamage(weapon);
            float range = upgradeManager.GetUpgradedAttackRange(weapon);
            float critChance = upgradeManager.GetUpgradedCritChance(weapon);
            
            PerformMeleeAttack(
                damage,
                range,
                weapon.AttackShape,
                weapon.SectorAngle,
                weapon.RectangleWidth,
                weapon.Knockback,
                critChance
            );
        }
        
        private void PerformMeleeAttack(float damage, float range, AttackShape shape, float angle = 90f, float width = 2f, float knockback = 0f, float critChance = 0.05f)
        {
            List<GameObject> targets = GetTargetsInShape2D(transform.position, transform.up, range, shape, angle, width);
            
            foreach (var target in targets)
            {
                ApplyDamageToTarget(target, damage, knockback, critChance);
            }
            
            Debug.Log($"Melee attack hit {targets.Count} targets");
        }
        
        private void PerformRangedAttack(WeaponItem weapon)
        {
            // 消耗弹药
            if (!ammoManager.UseAmmo(weapon.RequiredAmmo, weapon.AmmoPerShot))
            {
                Debug.Log("Out of ammo!");
                return;
            }
            
            // 检查是否应该使用形状攻击
            // 魔法武器且攻击形状不是直线时，使用形状攻击而非弹药
            if (weapon.WeaponType == WeaponType.Magic && weapon.AttackShape != AttackShape.Line)
            {
                // 魔法武器使用形状攻击（类似近战但范围更远）
                PerformShapeAttack(weapon);
                return;
            }
            
            // 创建2D弹药
            if (weapon.ProjectilePrefab != null)
            {
                Vector2 spawnPos = firePoint.position;
                GameObject projectile = Instantiate(weapon.ProjectilePrefab, spawnPos, transform.rotation);
                
                // 设置弹药速度
                var rb2d = projectile.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                {
                    rb2d.velocity = transform.up * weapon.ProjectileSpeed;
                }
                
                // 设置弹药伤害信息
                var proj = projectile.GetComponent<Projectile2D>();
                if (proj != null)
                {
                    var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
                    float damage = upgradeManager.GetUpgradedDamage(weapon);
                    proj.Initialize(damage, gameObject, weapon.EffectRadius, weapon.IsPiercing, weapon.MaxPierceTargets);
                }
                
                // 自动缩放弹药大小
                var spriteRenderer = projectile.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    float maxDimension = Mathf.Max(spriteRenderer.sprite.rect.width, spriteRenderer.sprite.rect.height);
                    float pixelsPerUnit = spriteRenderer.sprite.pixelsPerUnit;
                    float currentWorldSize = maxDimension / pixelsPerUnit;
                    
                    // 让弹药大小合理（目标大小16-24像素，取决于弹药类型）
                    float targetSize = 20f; // 弹药目标大小20像素
                    float targetWorldSize = targetSize / 32f; // 假设基础PPU是32
                    float scaleFactor = targetWorldSize / currentWorldSize;
                    
                    projectile.transform.localScale = Vector3.one * scaleFactor;
                    Debug.Log($"[Combat] Auto-scaled projectile from {maxDimension}px to target {targetSize}px (scale: {scaleFactor})");
                }
            }
            else
            {
                // 即时命中扫描
                var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
                float damage = upgradeManager.GetUpgradedDamage(weapon);
                float range = upgradeManager.GetUpgradedAttackRange(weapon);
                float critChance = upgradeManager.GetUpgradedCritChance(weapon);
                
                RaycastHit2D hit = Physics2D.Raycast(transform.position, transform.up, range, targetLayers);
                if (hit.collider != null)
                {
                    ApplyDamageToTarget(hit.collider.gameObject, damage, weapon.Knockback, critChance);
                }
            }
        }
        
        private void PerformShapeAttack(WeaponItem weapon)
        {
            Debug.Log($"[Combat] Performing shape attack with {weapon.ItemName} - Shape: {weapon.AttackShape}, Range: {weapon.AttackRange}");
            
            var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
            float damage = upgradeManager.GetUpgradedDamage(weapon);
            float range = upgradeManager.GetUpgradedAttackRange(weapon);
            float critChance = upgradeManager.GetUpgradedCritChance(weapon);
            
            // 获取形状内的目标
            List<GameObject> targets = GetTargetsInShape2D(
                transform.position,
                transform.up,
                range,
                weapon.AttackShape,
                weapon.SectorAngle,
                weapon.RectangleWidth
            );
            
            Debug.Log($"[Combat] Shape attack found {targets.Count} targets");
            
            // 对每个目标造成伤害
            foreach (var target in targets)
            {
                ApplyDamageToTarget(target, damage, weapon.Knockback, critChance);
                
                // 如果武器有范围效果，在目标位置应用范围伤害
                if (weapon.EffectRadius > 0)
                {
                    ApplyAreaDamageAtPosition(target.transform.position, damage, weapon.EffectRadius, critChance);
                }
            }
            
            // 播放攻击特效
            if (weapon.HitEffectPrefab != null && targets.Count > 0)
            {
                foreach (var target in targets)
                {
                    Instantiate(weapon.HitEffectPrefab, target.transform.position, Quaternion.identity);
                }
            }
            
            Debug.Log($"Shape attack ({weapon.AttackShape}) hit {targets.Count} targets");
        }
        
        private void ApplyAreaDamageAtPosition(Vector2 position, float damage, float radius, float critChance)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(position, radius, targetLayers);
            
            foreach (var collider in colliders)
            {
                if (collider.gameObject == gameObject) continue;
                
                float distance = Vector2.Distance(position, collider.transform.position);
                float falloff = 1f - (distance / radius);
                float areaDamage = damage * falloff * 0.5f; // 范围伤害为原始伤害的50%
                
                ApplyDamageToTarget(collider.gameObject, areaDamage, 0f, critChance * 0.5f);
            }
        }
        
        private List<GameObject> GetTargetsInShape2D(Vector2 origin, Vector2 forward, float range, AttackShape shape, float angle, float width)
        {
            List<GameObject> targets = new List<GameObject>();
            Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, range, targetLayers);
            
            foreach (var collider in colliders)
            {
                if (collider.gameObject == gameObject) continue;
                
                Vector2 dirToTarget = ((Vector2)collider.transform.position - origin).normalized;
                float distToTarget = Vector2.Distance(origin, collider.transform.position);
                
                bool inRange = false;
                
                switch (shape)
                {
                    case AttackShape.Circle:
                        inRange = distToTarget <= range;
                        break;
                        
                    case AttackShape.Sector:
                        float angleToTarget = Vector2.Angle(forward, dirToTarget);
                        inRange = distToTarget <= range && angleToTarget <= angle / 2f;
                        break;
                        
                    case AttackShape.Rectangle:
                        // 转换到本地坐标
                        Vector2 localPos = transform.InverseTransformPoint(collider.transform.position);
                        inRange = localPos.y > 0 && localPos.y <= range && 
                                 Mathf.Abs(localPos.x) <= width / 2f;
                        break;
                        
                    case AttackShape.Line:
                        // 使用2D射线检测
                        RaycastHit2D hit = Physics2D.Raycast(origin, forward, range, targetLayers);
                        if (hit.collider == collider)
                        {
                            inRange = true;
                        }
                        break;
                }
                
                if (inRange)
                {
                    targets.Add(collider.gameObject);
                }
            }
            
            return targets;
        }
        
        private void ApplyDamageToTarget(GameObject target, float damage, float knockback, float critChance)
        {
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                bool isCrit = Random.value <= critChance;
                float finalDamage = isCrit ? damage * 2f : damage;
                
                var damageInfo = new DamageInfo(finalDamage)
                {
                    isCritical = isCrit,
                    knockback = knockback,
                    hitPoint = target.transform.position,
                    hitDirection = (target.transform.position - transform.position).normalized
                };
                
                // 添加武器的Debuff
                var weapon = inventory?.EquippedWeapon;
                if (weapon != null && weapon.OnHitDebuffs.Count > 0)
                {
                    foreach (var debuff in weapon.OnHitDebuffs)
                    {
                        if (debuff != null && debuff.ApplicationMode == Buffs.BuffApplicationMode.OnHit)
                        {
                            // 检查是否满足施加条件
                            bool critRequirementMet = !debuff.RequiresCrit || isCrit;
                            
                            // 使用Buff自身的触发概率，如果武器也有概率则相乘
                            float finalChance = debuff.ApplicationChance * weapon.DebuffChance;
                            
                            if (critRequirementMet && Random.value <= finalChance)
                            {
                                damageInfo.AddDebuff(debuff);
                            }
                        }
                    }
                }
                
                damageable.TakeDamage(finalDamage, gameObject, damageInfo);
                
                // 2D击退
                if (knockback > 0)
                {
                    var rb2d = target.GetComponent<Rigidbody2D>();
                    if (rb2d != null)
                    {
                        Vector2 knockbackDir = (Vector2)damageInfo.hitDirection;
                        rb2d.AddForce(knockbackDir * knockback, ForceMode2D.Impulse);
                    }
                }
                
                // 应用Debuff（如果目标有BuffManager）
                var buffManager = target.GetComponent<Buffs.BuffManager>();
                if (buffManager != null && damageInfo.appliedDebuffs != null)
                {
                    foreach (var debuff in damageInfo.appliedDebuffs)
                    {
                        buffManager.AddBuff(debuff);
                    }
                }
            }
        }
        
        // 可视化调试
        private void OnDrawGizmosSelected()
        {
            if (!showAttackGizmos) return;
            
            var weapon = inventory?.EquippedWeapon;
            if (weapon == null) return;
            
            Gizmos.color = gizmoColor;
            Vector3 origin = transform.position;
            Vector3 forward = transform.up; // 2D中使用up作为前方
            
            switch (weapon.AttackShape)
            {
                case AttackShape.Circle:
                    DrawCircleGizmo2D(origin, weapon.AttackRange);
                    break;
                    
                case AttackShape.Sector:
                    DrawSectorGizmo2D(origin, forward, weapon.AttackRange, weapon.SectorAngle);
                    break;
                    
                case AttackShape.Rectangle:
                    DrawRectangleGizmo2D(origin, forward, weapon.AttackRange, weapon.RectangleWidth);
                    break;
                    
                case AttackShape.Line:
                    Gizmos.DrawLine(origin, origin + forward * weapon.AttackRange);
                    break;
            }
        }
        
        private void DrawCircleGizmo2D(Vector3 center, float radius)
        {
            // 绘制2D圆形
            int segments = 32;
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + Vector3.right * radius;
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
        
        private void DrawSectorGizmo2D(Vector3 origin, Vector3 forward, float radius, float angle)
        {
            // 转换为2D角度
            float forwardAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float rightAngle = forwardAngle + angle / 2f;
            float leftAngle = forwardAngle - angle / 2f;
            
            Vector3 right = new Vector3(Mathf.Cos(rightAngle * Mathf.Deg2Rad), Mathf.Sin(rightAngle * Mathf.Deg2Rad), 0);
            Vector3 left = new Vector3(Mathf.Cos(leftAngle * Mathf.Deg2Rad), Mathf.Sin(leftAngle * Mathf.Deg2Rad), 0);
            
            Gizmos.DrawLine(origin, origin + right * radius);
            Gizmos.DrawLine(origin, origin + left * radius);
            
            // 绘制弧线
            int segments = Mathf.RoundToInt(angle / 5f);
            float angleStep = angle / segments;
            Vector3 prevPoint = origin + left * radius;
            
            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = leftAngle + i * angleStep;
                Vector3 dir = new Vector3(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad), 0);
                Vector3 point = origin + dir * radius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
        
        private void DrawRectangleGizmo2D(Vector3 origin, Vector3 forward, float length, float width)
        {
            // 计算矩形的四个角
            Vector3 right = new Vector3(-forward.y, forward.x, 0).normalized;
            
            Vector3 frontLeft = origin + forward * length - right * width / 2f;
            Vector3 frontRight = origin + forward * length + right * width / 2f;
            Vector3 backLeft = origin - right * width / 2f;
            Vector3 backRight = origin + right * width / 2f;
            
            Gizmos.DrawLine(backLeft, frontLeft);
            Gizmos.DrawLine(frontLeft, frontRight);
            Gizmos.DrawLine(frontRight, backRight);
            Gizmos.DrawLine(backRight, backLeft);
        }
    }
}