using UnityEngine;
using System.Collections.Generic;

namespace Visuals
{
    /// <summary>
    /// 通用2D动画控制器 - 解耦动画逻辑
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AnimationController2D : MonoBehaviour
    {
        [Header("Animation Setup")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool autoDetectComponents = true;
        
        [Header("Movement Animation")]
        [SerializeField] private string moveSpeedParam = "MoveSpeed";
        [SerializeField] private string isMovingParam = "IsMoving";
        [SerializeField] private bool flipBasedOnDirection = true;
        
        [Header("Combat Animation")]
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hurtTrigger = "Hurt";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string isDeadParam = "IsDead";
        
        [Header("State Animation")]
        [SerializeField] private Dictionary<string, string> customStates = new Dictionary<string, string>();
        
        private Vector3 lastPosition;
        private bool hasAnimator;
        
        private void Awake()
        {
            if (autoDetectComponents)
            {
                if (animator == null) animator = GetComponent<Animator>();
                if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            }
            
            hasAnimator = animator != null;
            lastPosition = transform.position;
        }
        
        private void Update()
        {
            if (!hasAnimator) return;
            
            UpdateMovementAnimation();
        }
        
        /// <summary>
        /// 更新移动动画
        /// </summary>
        private void UpdateMovementAnimation()
        {
            Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
            float speed = velocity.magnitude;
            
            // 更新动画参数
            SetFloat(moveSpeedParam, speed);
            SetBool(isMovingParam, speed > 0.1f);
            
            // 翻转精灵
            if (flipBasedOnDirection && Mathf.Abs(velocity.x) > 0.1f)
            {
                SetFacing(velocity.x > 0);
            }
            
            lastPosition = transform.position;
        }
        
        /// <summary>
        /// 设置朝向
        /// </summary>
        public void SetFacing(bool faceRight)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = !faceRight;
            }
        }
        
        /// <summary>
        /// 播放攻击动画
        /// </summary>
        public void PlayAttack()
        {
            SetTrigger(attackTrigger);
        }
        
        /// <summary>
        /// 播放受伤动画
        /// </summary>
        public void PlayHurt()
        {
            SetTrigger(hurtTrigger);
        }
        
        /// <summary>
        /// 播放死亡动画
        /// </summary>
        public void PlayDeath()
        {
            SetTrigger(deathTrigger);
            SetBool(isDeadParam, true);
        }
        
        /// <summary>
        /// 播放自定义状态动画
        /// </summary>
        public void PlayCustomState(string stateName)
        {
            if (customStates.ContainsKey(stateName))
            {
                SetTrigger(customStates[stateName]);
            }
        }
        
        // 安全的Animator方法封装
        private void SetFloat(string param, float value)
        {
            if (hasAnimator && !string.IsNullOrEmpty(param))
            {
                try { animator.SetFloat(param, value); } catch { }
            }
        }
        
        private void SetBool(string param, bool value)
        {
            if (hasAnimator && !string.IsNullOrEmpty(param))
            {
                try { animator.SetBool(param, value); } catch { }
            }
        }
        
        private void SetTrigger(string param)
        {
            if (hasAnimator && !string.IsNullOrEmpty(param))
            {
                try { animator.SetTrigger(param); } catch { }
            }
        }
        
        /// <summary>
        /// 设置动画速度
        /// </summary>
        public void SetAnimationSpeed(float speed)
        {
            if (hasAnimator)
            {
                animator.speed = speed;
            }
        }
        
        /// <summary>
        /// 暂停/恢复动画
        /// </summary>
        public void SetAnimationEnabled(bool enabled)
        {
            if (hasAnimator)
            {
                animator.enabled = enabled;
            }
        }
    }
}