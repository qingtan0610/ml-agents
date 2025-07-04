using UnityEngine;
using Combat.Interfaces;

namespace Mechanisms
{
    /// <summary>
    /// 简单的陷阱，间隔循环触发
    /// </summary>
    public class SimpleTrap : MonoBehaviour
    {
        [Header("Trap Settings")]
        [SerializeField] private float damage = 20f;
        [SerializeField] private float interval = 3f; // 触发间隔
        [SerializeField] private float activeTime = 1f; // 激活持续时间
        [SerializeField] private float startDelay = 0f; // 初始延迟
        
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string activeTrigger = "Activate";
        
        [Header("Damage Area")]
        [SerializeField] private float damageRadius = 1f;
        [SerializeField] private LayerMask targetLayers = -1;
        
        private float timer = 0f;
        private bool isActive = false;
        
        private void Start()
        {
            timer = -startDelay;
            
            if (animator == null)
                animator = GetComponent<Animator>();
        }
        
        private void Update()
        {
            timer += Time.deltaTime;
            
            if (!isActive && timer >= interval)
            {
                Activate();
                timer = 0f;
            }
            else if (isActive && timer >= activeTime)
            {
                Deactivate();
            }
        }
        
        private void Activate()
        {
            isActive = true;
            
            // 播放激活动画
            if (animator != null && !string.IsNullOrEmpty(activeTrigger))
            {
                animator.SetTrigger(activeTrigger);
            }
            
            // 造成伤害
            DealDamage();
        }
        
        private void Deactivate()
        {
            isActive = false;
        }
        
        private void DealDamage()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, damageRadius, targetLayers);
            
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    var damageInfo = new DamageInfo(damage)
                    {
                        hitPoint = hit.transform.position,
                        isTrap = true
                    };
                    
                    damageable.TakeDamage(damage, gameObject, damageInfo);
                }
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActive ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }
    }
}