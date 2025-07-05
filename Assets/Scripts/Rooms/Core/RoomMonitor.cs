using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Rooms.Core
{
    /// <summary>
    /// 独立的房间监控器，负责可靠地检测玩家进入和锁门
    /// </summary>
    public class RoomMonitor : MonoBehaviour
    {
        [Header("Room Reference")]
        private SimplifiedRoom room;
        
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 6f; // 比房间稍小的检测范围
        [SerializeField] private float checkInterval = 0.1f; // 检测间隔
        
        [Header("State")]
        [SerializeField] private bool isMonitoring = false;
        [SerializeField] private bool hasTriggeredLock = false;
        [SerializeField] private List<GameObject> playersInRoom = new List<GameObject>();
        [SerializeField] private DoorDirection lastEntryDirection = DoorDirection.North;
        
        // 房间进入事件
        public System.Action<GameObject, DoorDirection> OnPlayerConfirmedEntry;
        
        private void Awake()
        {
            room = GetComponent<SimplifiedRoom>();
            if (room == null)
            {
                Debug.LogError("[RoomMonitor] No SimplifiedRoom component found!");
                enabled = false;
            }
        }
        
        private void Start()
        {
            // 只有怪物房间需要监控
            if (room != null && room.Type == RoomType.Monster)
            {
                StartMonitoring();
            }
        }
        
        /// <summary>
        /// 开始监控房间
        /// </summary>
        public void StartMonitoring()
        {
            if (!isMonitoring)
            {
                isMonitoring = true;
                StartCoroutine(MonitorRoom());
                Debug.Log($"[RoomMonitor] Started monitoring room at {room.GridPosition}");
            }
        }
        
        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            isMonitoring = false;
            StopAllCoroutines();
            Debug.Log($"[RoomMonitor] Stopped monitoring room at {room.GridPosition}");
        }
        
        /// <summary>
        /// 记录玩家进入方向（由DoorTrigger调用）
        /// </summary>
        public void RecordEntryDirection(DoorDirection direction)
        {
            if (!hasTriggeredLock)
            {
                lastEntryDirection = direction;
                Debug.Log($"[RoomMonitor] Recorded entry direction: {direction}");
            }
        }
        
        /// <summary>
        /// 房间监控协程
        /// </summary>
        private IEnumerator MonitorRoom()
        {
            while (isMonitoring)
            {
                // 检测房间内的所有玩家
                DetectPlayersInRoom();
                
                // 如果有玩家在房间内且还没触发锁门
                if (playersInRoom.Count > 0 && !hasTriggeredLock && !room.IsCleared)
                {
                    TriggerRoomLock();
                }
                
                yield return new WaitForSeconds(checkInterval);
            }
        }
        
        /// <summary>
        /// 检测房间内的玩家
        /// </summary>
        private void DetectPlayersInRoom()
        {
            playersInRoom.Clear();
            
            // 获取所有玩家
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            
            foreach (var player in players)
            {
                if (player != null)
                {
                    float distance = Vector2.Distance(player.transform.position, transform.position);
                    if (distance <= detectionRadius)
                    {
                        playersInRoom.Add(player);
                    }
                }
            }
        }
        
        /// <summary>
        /// 触发房间锁门
        /// </summary>
        private void TriggerRoomLock()
        {
            if (hasTriggeredLock) return;
            
            hasTriggeredLock = true;
            Debug.Log($"[RoomMonitor] Triggering room lock for {room.Type} room at {room.GridPosition}");
            
            // 通知房间锁门
            if (playersInRoom.Count > 0)
            {
                OnPlayerConfirmedEntry?.Invoke(playersInRoom[0], lastEntryDirection);
            }
        }
        
        /// <summary>
        /// 重置监控器（房间清理后调用）
        /// </summary>
        public void ResetMonitor()
        {
            hasTriggeredLock = false;
            playersInRoom.Clear();
            Debug.Log($"[RoomMonitor] Monitor reset for room at {room.GridPosition}");
        }
        
        /// <summary>
        /// 绘制检测范围（调试用）
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isMonitoring ? Color.red : Color.gray;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            // 显示房间内的玩家
            Gizmos.color = Color.green;
            foreach (var player in playersInRoom)
            {
                if (player != null)
                {
                    Gizmos.DrawLine(transform.position, player.transform.position);
                }
            }
        }
    }
}