using UnityEngine;

namespace Interactables
{
    /// <summary>
    /// 用于检测墙壁碰撞并停止物体移动的组件
    /// </summary>
    public class StopOnWallCollision : MonoBehaviour
    {
        private Rigidbody2D rb;
        private int floorLayer;
        private bool shouldStop = false;
        private Vector3 stopPosition;
        
        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            floorLayer = LayerMask.NameToLayer("Floor");
            
            // 确保有碰撞器
            var collider = GetComponent<Collider2D>();
            if (collider == null)
            {
                Debug.LogError("[StopOnWall] No Collider2D found on pickup!");
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 如果碰到的不是地板层，就停止移动
            if (other.gameObject.layer != floorLayer)
            {
                // 检查是否是墙壁或其他障碍物
                if (other.CompareTag("Wall") || other.gameObject.layer == LayerMask.NameToLayer("Wall"))
                {
                    Debug.Log($"[StopOnWall] Hit wall! Marking for stop.");
                    shouldStop = true;
                    stopPosition = transform.position;
                    
                    // 立即尝试停止
                    if (rb != null)
                    {
                        rb.velocity = Vector2.zero;
                        rb.angularVelocity = 0f;
                    }
                }
            }
        }
        
        private void FixedUpdate()
        {
            // 如果标记为需要停止，持续强制停止
            if (shouldStop && rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                transform.position = stopPosition;
            }
        }
        
        private void Update()
        {
            // 在 Update 中也强制位置
            if (shouldStop)
            {
                transform.position = stopPosition;
            }
        }
        
        // 几秒后可以移除这个组件，因为物品应该已经停止了
        private void OnEnable()
        {
            Invoke("RemoveComponent", 2f);
        }
        
        private void RemoveComponent()
        {
            shouldStop = false;
            Destroy(this);
        }
    }
}