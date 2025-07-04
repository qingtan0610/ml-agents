using UnityEngine;

namespace Rooms
{
    /// <summary>
    /// 简单的门阻挡器 - 直接控制已有的碰撞器
    /// </summary>
    public class SimpleDoorBlocker : MonoBehaviour
    {
        [Header("Door State")]
        [SerializeField] private bool isOpen = true;
        
        [Header("Components")]
        private BoxCollider2D doorCollider;
        private Animator animator; // 可选，如果有的话
        
        private void Awake()
        {
            // 调试层级信息
            Debug.Log($"[SimpleDoorBlocker] {gameObject.name} is on layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
            
            // 获取所有碰撞器
            var allColliders = GetComponents<BoxCollider2D>();
            Debug.Log($"[SimpleDoorBlocker] Found {allColliders.Length} BoxCollider2D components on {gameObject.name}");
            
            // 使用第一个非触发器碰撞器，如果没有则使用第一个
            foreach (var col in allColliders)
            {
                Debug.Log($"[SimpleDoorBlocker] Collider: size={col.size}, isTrigger={col.isTrigger}, enabled={col.enabled}");
                if (doorCollider == null || !col.isTrigger)
                {
                    doorCollider = col;
                }
            }
            
            if (doorCollider == null)
            {
                Debug.LogError($"[SimpleDoorBlocker] No BoxCollider2D found on {gameObject.name}!");
                return;
            }
            
            // 检查碰撞器大小，如果是0则设置默认值
            if (doorCollider.size.x <= 0 || doorCollider.size.y <= 0)
            {
                // 根据对象名称判断方向
                if (gameObject.name.ToLower().Contains("north") || gameObject.name.ToLower().Contains("south"))
                {
                    doorCollider.size = new Vector2(2f, 0.5f);
                }
                else
                {
                    doorCollider.size = new Vector2(0.5f, 2f);
                }
                Debug.LogWarning($"[SimpleDoorBlocker] Collider size was 0, set to {doorCollider.size}");
            }
            
            // 获取动画组件（如果有）
            animator = GetComponent<Animator>();
            
            // 应用初始状态
            ApplyState();
            
            Debug.Log($"[SimpleDoorBlocker] Initialized on {gameObject.name}, collider size: {doorCollider.size}, isOpen: {isOpen}");
        }
        
        /// <summary>
        /// 关闭门（阻挡）
        /// </summary>
        public void Close()
        {
            isOpen = false;
            ApplyState();
            Debug.Log($"[SimpleDoorBlocker] Door closed - {gameObject.name}");
        }
        
        /// <summary>
        /// 打开门（可通过）
        /// </summary>
        public void Open()
        {
            isOpen = true;
            ApplyState();
            Debug.Log($"[SimpleDoorBlocker] Door opened - {gameObject.name}");
        }
        
        /// <summary>
        /// 应用当前状态到组件
        /// </summary>
        private void ApplyState()
        {
            if (doorCollider != null)
            {
                // 开门 = 触发器（可通过），关门 = 实体（阻挡）
                doorCollider.isTrigger = isOpen;
                
                // 强制物理系统更新
                Physics2D.SyncTransforms();
                
                Debug.Log($"[SimpleDoorBlocker] Applied state - isTrigger: {doorCollider.isTrigger}, bounds: {doorCollider.bounds}");
            }
            
            // 如果有动画组件，也触发相应动画
            if (animator != null)
            {
                animator.SetTrigger(isOpen ? "Open" : "Close");
            }
        }
        
        // 用于调试
        private void OnDrawGizmosSelected()
        {
            if (doorCollider != null)
            {
                Gizmos.color = isOpen ? Color.green : Color.red;
                Gizmos.DrawWireCube(transform.position + (Vector3)doorCollider.offset, doorCollider.size);
            }
        }
    }
}