using UnityEngine;

namespace Rooms.Core
{
    /// <summary>
    /// 简单的门触发器，检测玩家进入
    /// </summary>
    public class DoorTrigger : MonoBehaviour
    {
        [Header("Door Settings")]
        [SerializeField] private DoorDirection direction;
        [SerializeField] private SimplifiedRoom parentRoom;
        
        private void Start()
        {
            // 如果没有手动设置，尝试获取父房间
            if (parentRoom == null)
            {
                parentRoom = GetComponentInParent<SimplifiedRoom>();
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && parentRoom != null)
            {
                // 通知地图生成器
                var mapGenerator = FindObjectOfType<MapGenerator>();
                if (mapGenerator != null)
                {
                    mapGenerator.OnPlayerUseDoor(parentRoom, direction, other.gameObject);
                }
            }
        }
    }
}