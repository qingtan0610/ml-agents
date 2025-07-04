using UnityEngine;
using Combat.Interfaces;
using System.Collections.Generic;
using Buffs;

namespace Enemy
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile2D : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float damage = 5f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private GameObject hitEffectPrefab;
        
        private GameObject shooter;
        private List<BuffBase> debuffs;
        private float debuffChance;
        
        private void Awake()
        {
            // 确保是触发器
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // 设置刚体
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
            }
        }
        
        public void Initialize(float damage, GameObject shooter, List<BuffBase> debuffs = null, float debuffChance = 1f)
        {
            this.damage = damage;
            this.shooter = shooter;
            this.debuffs = debuffs;
            this.debuffChance = debuffChance;
            
            // 忽略与发射者的碰撞
            if (shooter != null)
            {
                var shooterCollider = shooter.GetComponent<Collider2D>();
                if (shooterCollider != null)
                {
                    Physics2D.IgnoreCollision(GetComponent<Collider2D>(), shooterCollider, true);
                }
            }
            
            // 自动销毁
            Destroy(gameObject, lifetime);
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 忽略发射者
            if (other.gameObject == shooter) return;
            
            // 检查标签
            if (other.CompareTag("Player"))
            {
                // 对玩家造成伤害
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    var damageInfo = new DamageInfo(damage)
                    {
                        hitPoint = other.transform.position,
                        hitDirection = GetComponent<Rigidbody2D>()?.velocity.normalized ?? Vector2.up
                    };
                    
                    // 添加Debuff
                    if (debuffs != null && debuffs.Count > 0 && Random.value <= debuffChance)
                    {
                        Debug.Log($"[EnemyProjectile] Adding {debuffs.Count} debuffs to projectile damage");
                        foreach (var debuff in debuffs)
                        {
                            if (debuff != null)
                            {
                                damageInfo.AddDebuff(debuff);
                            }
                        }
                    }
                    
                    damageable.TakeDamage(damage, shooter, damageInfo);
                    
                    // 应用Debuff
                    var buffManager = other.GetComponent<BuffManager>();
                    if (buffManager != null && damageInfo.appliedDebuffs != null && damageInfo.appliedDebuffs.Count > 0)
                    {
                        Debug.Log($"[EnemyProjectile] Applying {damageInfo.appliedDebuffs.Count} debuffs to target");
                        foreach (var debuff in damageInfo.appliedDebuffs)
                        {
                            buffManager.AddBuff(debuff);
                        }
                    }
                    
                    // 播放击中特效
                    if (hitEffectPrefab != null)
                    {
                        Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                    }
                    
                    Destroy(gameObject);
                }
            }
            else if (other.CompareTag("Wall") || (other.gameObject.layer == LayerMask.NameToLayer("Wall")))
            {
                // 撞墙销毁
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                }
                
                Destroy(gameObject);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // 显示伤害范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}