using UnityEngine;
using System.Collections;
using Combat.Interfaces;
using Combat;

namespace Enemy
{
    public class RangedEnemy2D : Enemy2D
    {
        [Header("Ranged Settings")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float retreatDistance = 5f;
        [SerializeField] private float projectileDamage = 8f;
        
        [Header("Projectile Variations")]
        [SerializeField] private int projectileCount = 1; // 同时发射的弹药数
        [SerializeField] private float spreadAngle = 0f; // 散射角度
        [SerializeField] private float projectileSize = 1f; // 弹药大小
        [SerializeField] private bool homingProjectile = false; // 追踪弹
        [SerializeField] private float homingStrength = 5f; // 追踪强度
        [SerializeField] private float explosionRadius = 0f; // 爆炸半径（0=无爆炸）
        [SerializeField] private bool piercing = false; // 穿透
        [SerializeField] private int bounceCount = 0; // 弹跳次数
        
        protected override void Awake()
        {
            base.Awake();
            
            // 设置远程敌人的默认值
            if (attackRange == 2f) attackRange = 8f;
            if (damage == 10f) damage = projectileDamage;
            if (maxHealth == 50f) maxHealth = 30f;
            currentHealth = maxHealth;
            
            // 远程敌人默认掉落更多箭矢和法力（通过guaranteedDrops配置）
            if (!overrideLootChances)
            {
                overrideLootChances = true;
                lootMultiplier = 1.2f; // 20%额外掉落
            }
            
            // 创建发射点
            if (firePoint == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector2(0, 0.5f);
                firePoint = fp.transform;
            }
        }
        
        protected override IEnumerator AttackBehavior()
        {
            while (currentState == EnemyState.Attack && currentTarget != null)
            {
                float distance = Vector2.Distance(transform.position, currentTarget.transform.position);
                
                // 保持距离
                if (distance < retreatDistance)
                {
                    // 后退
                    Vector2 retreatDir = ((Vector2)transform.position - (Vector2)currentTarget.transform.position).normalized;
                    rb.velocity = retreatDir * moveSpeed * 0.5f;
                }
                else if (distance > attackRange)
                {
                    // 太远了，追击
                    ChangeState(EnemyState.Chase);
                    yield break;
                }
                else
                {
                    // 合适的距离，停止移动并攻击
                    rb.velocity = Vector2.zero;
                    
                    if (Time.time - lastAttackTime >= attackCooldown)
                    {
                        PerformAttack();
                        lastAttackTime = Time.time;
                    }
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        protected override void PerformAttack()
        {
            if (currentTarget == null || projectilePrefab == null) return;
            
            Vector2 firePos = firePoint != null ? firePoint.position : transform.position;
            Vector2 baseDirection = ((Vector2)currentTarget.transform.position - firePos).normalized;
            
            // 发射多个弹药
            for (int i = 0; i < projectileCount; i++)
            {
                Vector2 direction = baseDirection;
                
                // 计算散射
                if (projectileCount > 1 && spreadAngle > 0)
                {
                    float angleOffset = 0f;
                    if (projectileCount == 1)
                    {
                        angleOffset = 0f;
                    }
                    else
                    {
                        float totalSpread = spreadAngle;
                        float angleStep = totalSpread / (projectileCount - 1);
                        angleOffset = -totalSpread / 2f + angleStep * i;
                    }
                    
                    direction = Quaternion.Euler(0, 0, angleOffset) * baseDirection;
                }
                
                CreateProjectile(firePos, direction);
            }
            
            Debug.Log($"{name} fired {projectileCount} projectile(s) at {currentTarget.name}");
        }
        
        private void CreateProjectile(Vector2 firePos, Vector2 direction)
        {
            GameObject projectile = Instantiate(projectilePrefab, firePos, Quaternion.identity);
            
            // 设置大小
            if (projectileSize != 1f)
            {
                projectile.transform.localScale *= projectileSize;
            }
            
            // 设置弹药速度和旋转
            var rb2d = projectile.GetComponent<Rigidbody2D>();
            if (rb2d != null)
            {
                rb2d.velocity = direction * projectileSpeed;
                
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            
            // 设置弹药属性
            var enemyProj = projectile.GetComponent<EnemyProjectile2D>();
            if (enemyProj != null)
            {
                enemyProj.Initialize(projectileDamage, gameObject, attackDebuffs, debuffChance);
                
                // 设置特殊属性
                // TODO: 传递追踪、爆炸、穿透等属性给弹药
            }
            else
            {
                var proj = projectile.GetComponent<Projectile2D>();
                if (proj != null)
                {
                    proj.Initialize(projectileDamage, gameObject, explosionRadius, piercing, piercing ? 999 : 1);
                }
            }
        }
    }
}