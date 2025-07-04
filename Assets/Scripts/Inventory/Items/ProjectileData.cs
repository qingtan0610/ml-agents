using UnityEngine;
using System;

namespace Inventory.Items
{
    [Serializable]
    public class ProjectileData
    {
        [Header("Basic Settings")]
        public float speed = 10f;           // 飞行速度
        public float lifetime = 5f;         // 最大生存时间
        public float gravity = 0f;          // 重力影响（0为直线飞行）
        
        [Header("Damage Settings")]
        public float damageMultiplier = 1f; // 伤害倍率
        public float effectRadius = 0f;     // 爆炸半径（0为单体）
        public bool isPiercing = false;     // 是否穿透
        public int maxPierceTargets = 1;    // 最大穿透数
        
        [Header("Visual")]
        public GameObject trailEffect;      // 飞行轨迹特效
        public GameObject impactEffect;     // 命中特效
        public GameObject areaEffect;       // 范围特效
        
        [Header("Audio")]
        public AudioClip launchSound;       // 发射音效
        public AudioClip flySound;          // 飞行音效
        public AudioClip impactSound;       // 命中音效
        
        public ProjectileData Clone()
        {
            return MemberwiseClone() as ProjectileData;
        }
    }
    
    // 弹药组件（附加在弹药预制体上）
    public class Projectile : MonoBehaviour
    {
        private ProjectileData data;
        private float damage;
        private GameObject shooter;
        private Vector3 velocity;
        private float aliveTime = 0f;
        private int pierceCount = 0;
        
        public void Initialize(ProjectileData projectileData, float weaponDamage, GameObject shooter, Vector3 direction)
        {
            this.data = projectileData.Clone();
            this.damage = weaponDamage * data.damageMultiplier;
            this.shooter = shooter;
            this.velocity = direction.normalized * data.speed;
            
            // 播放发射音效
            if (data.launchSound != null)
            {
                AudioSource.PlayClipAtPoint(data.launchSound, transform.position);
            }
            
            // 创建轨迹特效
            if (data.trailEffect != null)
            {
                Instantiate(data.trailEffect, transform);
            }
        }
        
        private void Update()
        {
            // 更新位置
            velocity.y -= data.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            
            // 更新朝向
            if (velocity != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }
            
            // 检查生存时间
            aliveTime += Time.deltaTime;
            if (aliveTime >= data.lifetime)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // 忽略发射者
            if (other.gameObject == shooter || other.transform.IsChildOf(shooter.transform))
                return;
            
            // 检查是否是有效目标（敌人、障碍物等）
            bool isValidTarget = CheckValidTarget(other);
            if (!isValidTarget) return;
            
            // 处理命中
            if (data.effectRadius > 0)
            {
                // 范围伤害
                ApplyAreaDamage(transform.position);
            }
            else
            {
                // 单体伤害
                ApplyDirectDamage(other.gameObject);
            }
            
            // 播放命中特效和音效
            if (data.impactEffect != null)
            {
                Instantiate(data.impactEffect, transform.position, transform.rotation);
            }
            
            if (data.impactSound != null)
            {
                AudioSource.PlayClipAtPoint(data.impactSound, transform.position);
            }
            
            // 处理穿透
            if (data.isPiercing && pierceCount < data.maxPierceTargets)
            {
                pierceCount++;
                // 继续飞行，可能减少伤害
                damage *= 0.8f;
            }
            else
            {
                // 销毁弹药
                Destroy(gameObject);
            }
        }
        
        private bool CheckValidTarget(Collider other)
        {
            // 这里应该根据游戏逻辑判断是否是有效目标
            // 比如检查tag、layer或组件
            return other.CompareTag("Enemy") || other.CompareTag("Destructible");
        }
        
        private void ApplyDirectDamage(GameObject target)
        {
            // 这里应该调用目标的受伤接口
            // 临时示例：
            Debug.Log($"Projectile hit {target.name} for {damage} damage");
            
            // 将来实现：
            // var damageable = target.GetComponent<IDamageable>();
            // if (damageable != null)
            // {
            //     damageable.TakeDamage(damage, shooter);
            // }
        }
        
        private void ApplyAreaDamage(Vector3 center)
        {
            // 获取范围内所有目标
            Collider[] targets = Physics.OverlapSphere(center, data.effectRadius);
            
            // 显示范围特效
            if (data.areaEffect != null)
            {
                var effect = Instantiate(data.areaEffect, center, Quaternion.identity);
                effect.transform.localScale = Vector3.one * data.effectRadius * 2;
            }
            
            foreach (var target in targets)
            {
                if (target.gameObject == shooter || target.transform.IsChildOf(shooter.transform))
                    continue;
                
                if (CheckValidTarget(target))
                {
                    // 根据距离计算伤害衰减
                    float distance = Vector3.Distance(center, target.transform.position);
                    float falloff = 1f - (distance / data.effectRadius);
                    float areaDamage = damage * falloff;
                    
                    ApplyDirectDamage(target.gameObject);
                }
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (data != null && data.effectRadius > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, data.effectRadius);
            }
        }
    }
}