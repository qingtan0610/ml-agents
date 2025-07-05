using UnityEngine;
using System.Collections.Generic;

namespace Interactables
{
    /// <summary>
    /// 传送装置 - 需要4个AI同时激活
    /// </summary>
    public class TeleportDevice : MonoBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private float activationRange = 3f;
        [SerializeField] private int requiredPlayers = 4;
        [SerializeField] private float activationWindow = 5f; // 激活时间窗口
        
        [Header("State")]
        [SerializeField] private HashSet<GameObject> activatingPlayers = new HashSet<GameObject>();
        [SerializeField] private float firstActivationTime;
        [SerializeField] private bool isActivated = false;
        
        public bool CanInteract(GameObject interactor)
        {
            return !isActivated && Vector2.Distance(transform.position, interactor.transform.position) <= activationRange;
        }
        
        public void Interact(GameObject interactor)
        {
            TryActivate(interactor);
        }
        
        public void TryActivate(GameObject interactor)
        {
            if (isActivated) return;
            
            // 添加激活者
            if (!activatingPlayers.Contains(interactor))
            {
                activatingPlayers.Add(interactor);
                
                // 记录第一次激活时间
                if (activatingPlayers.Count == 1)
                {
                    firstActivationTime = Time.time;
                }
                
                Debug.Log($"[TeleportDevice] {interactor.name} 激活传送装置 ({activatingPlayers.Count}/{requiredPlayers})");
            }
            
            // 检查是否满足激活条件
            if (activatingPlayers.Count >= requiredPlayers)
            {
                ActivateTeleporter();
            }
        }
        
        private void Update()
        {
            // 超时清理
            if (activatingPlayers.Count > 0 && Time.time > firstActivationTime + activationWindow)
            {
                Debug.Log("[TeleportDevice] 激活超时，重置");
                activatingPlayers.Clear();
            }
            
            // 清理死亡或离开的AI
            activatingPlayers.RemoveWhere(player => 
                player == null || 
                Vector2.Distance(transform.position, player.transform.position) > activationRange * 1.5f ||
                (player.GetComponent<AI.Stats.AIStats>()?.IsDead ?? false)
            );
        }
        
        private void ActivateTeleporter()
        {
            isActivated = true;
            Debug.Log("[TeleportDevice] 传送装置激活！准备传送到下一层");
            
            // 触发地图切换
            var mapGenerator = FindObjectOfType<Rooms.MapGenerator>();
            if (mapGenerator != null)
            {
                // 传送所有激活的AI到下一层
                foreach (var player in activatingPlayers)
                {
                    if (player != null)
                    {
                        Debug.Log($"[TeleportDevice] 传送 {player.name} 到下一层");
                    }
                }
                
                // 生成新地图
                mapGenerator.GenerateNewMap();
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActivated ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationRange);
        }
    }
}