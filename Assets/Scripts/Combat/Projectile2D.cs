using UnityEngine;
using Combat.Interfaces;

namespace Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Projectile2D : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private LayerMask targetLayers = -1;
        
        private float damage;
        private GameObject shooter;
        private float effectRadius;
        private bool isPiercing;
        private int maxPierceTargets;
        private int currentPierceCount = 0;
        
        private Rigidbody2D rb;
        
        // Public properties
        public GameObject Owner => shooter;
        public float Damage => damage;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 俯视角不需要重力
            
            // 确保碰撞器是触发器
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }
        
        public void Initialize(float damage, GameObject shooter, float effectRadius = 0f, bool isPiercing = false, int maxPierceTargets = 1)
        {
            this.damage = damage;
            this.shooter = shooter;
            this.effectRadius = effectRadius;
            this.isPiercing = isPiercing;
            this.maxPierceTargets = maxPierceTargets;
            
            // 设置层级，避免与发射者碰撞
            if (shooter != null)
            {
                Physics2D.IgnoreCollision(GetComponent<Collider2D>(), shooter.GetComponent<Collider2D>(), true);
            }
            
            // 自动销毁
            Destroy(gameObject, lifetime);
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 忽略发射者
            if (other.gameObject == shooter) return;
            
            // 检查层级
            if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
            
            // 范围伤害
            if (effectRadius > 0)
            {
                ApplyAreaDamage(transform.position);
            }
            else
            {
                // 单体伤害
                ApplyDirectDamage(other.gameObject);
            }
            
            // 穿透处理
            if (isPiercing && currentPierceCount < maxPierceTargets)
            {
                currentPierceCount++;
                damage *= 0.8f; // 穿透后伤害递减
            }
            else
            {
                // 销毁弹药
                Destroy(gameObject);
            }
        }
        
        private void ApplyDirectDamage(GameObject target)
        {
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                var damageInfo = new DamageInfo(damage)
                {
                    hitPoint = transform.position,
                    hitDirection = rb.velocity.normalized
                };
                
                damageable.TakeDamage(damage, shooter, damageInfo);
            }
        }
        
        private void ApplyAreaDamage(Vector2 center)
        {
            // 获取范围内所有目标
            Collider2D[] targets = Physics2D.OverlapCircleAll(center, effectRadius, targetLayers);
            
            foreach (var target in targets)
            {
                if (target.gameObject == shooter) continue;
                
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    // 根据距离计算伤害衰减
                    float distance = Vector2.Distance(center, target.transform.position);
                    float falloff = 1f - (distance / effectRadius);
                    float areaDamage = damage * falloff;
                    
                    var damageInfo = new DamageInfo(areaDamage)
                    {
                        hitPoint = target.transform.position,
                        hitDirection = ((Vector2)target.transform.position - center).normalized
                    };
                    
                    damageable.TakeDamage(areaDamage, shooter, damageInfo);
                }
            }
            
            // 创建爆炸特效
            Debug.Log($"Area damage at {center} with radius {effectRadius}");
        }
        
        private void OnDrawGizmosSelected()
        {
            if (effectRadius > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, effectRadius);
            }
        }
    }
}