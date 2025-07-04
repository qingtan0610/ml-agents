using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Interactables;

namespace Rooms
{
    /// <summary>
    /// 传送装置，需要所有4个AI交互才能激活
    /// </summary>
    public class TeleportDevice : MonoBehaviour, Interactables.IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private int requiredPlayers = 4; // 需要的玩家数量
        [SerializeField] private float interactionRange = 3f; // 交互范围
        [SerializeField] private float activationDelay = 3f; // 激活延迟
        [SerializeField] private float teleportDelay = 2f; // 传送延迟
        
        [Header("Visual Settings")]
        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private Color readyColor = Color.cyan;
        [SerializeField] private Color activatingColor = Color.yellow;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseIntensity = 0.3f;
        
        [Header("Runtime State")]
        private HashSet<GameObject> interactingPlayers = new HashSet<GameObject>();
        [SerializeField] private bool isActivating = false;
        [SerializeField] private bool isActivated = false;
        [SerializeField] private int currentPlayerCount = 0; // 用于调试显示
        
        // Components
        private Renderer deviceRenderer;
        private MapGenerator mapGenerator;
        
        // Visual elements
        private GameObject[] playerIndicators;
        private TextMesh statusText;
        
        private void Awake()
        {
            // 获取或创建渲染器
            deviceRenderer = GetComponent<Renderer>();
            if (deviceRenderer == null)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = new Vector3(3f, 0.5f, 3f);
                deviceRenderer = visual.GetComponent<Renderer>();
            }
            
            // 创建交互触发器
            CreateInteractionTrigger();
            
            // 创建玩家指示器
            CreatePlayerIndicators();
            
            // 创建状态文本
            CreateStatusText();
            
            // 获取地图生成器
            mapGenerator = FindObjectOfType<MapGenerator>();
        }
        
        private void Start()
        {
            UpdateVisuals();
        }
        
        /// <summary>
        /// 创建交互触发器
        /// </summary>
        private void CreateInteractionTrigger()
        {
            // 直接在主对象上添加触发器，这样IInteractable才能被检测到
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = interactionRange;
            collider.isTrigger = true;
        }
        
        /// <summary>
        /// 创建玩家指示器
        /// </summary>
        private void CreatePlayerIndicators()
        {
            playerIndicators = new GameObject[requiredPlayers];
            
            for (int i = 0; i < requiredPlayers; i++)
            {
                var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.transform.SetParent(transform);
                indicator.transform.localScale = Vector3.one * 0.5f;
                
                // 环形排列
                float angle = (360f / requiredPlayers) * i * Mathf.Deg2Rad;
                float radius = 2f;
                indicator.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.5f,
                    Mathf.Sin(angle) * radius
                );
                
                // 移除碰撞器
                Destroy(indicator.GetComponent<Collider>());
                
                playerIndicators[i] = indicator;
            }
        }
        
        /// <summary>
        /// 创建状态文本
        /// </summary>
        private void CreateStatusText()
        {
            var textObj = new GameObject("StatusText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0, 2f, 0);
            
            statusText = textObj.AddComponent<TextMesh>();
            statusText.text = $"Need {requiredPlayers} players";  // 使用英文避免编码问题
            statusText.fontSize = 24;
            statusText.alignment = TextAlignment.Center;
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.characterSize = 0.1f; // 设置合适的字符大小
        }
        
        private void Update()
        {
            // 脉动效果
            if (deviceRenderer != null)
            {
                float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity + 1f;
                
                Color currentColor = inactiveColor;
                if (isActivated)
                    currentColor = activeColor;
                else if (isActivating)
                    currentColor = activatingColor;
                else if (interactingPlayers.Count == requiredPlayers)
                    currentColor = readyColor;
                
                deviceRenderer.material.color = currentColor * pulse;
            }
            
            // 更新状态文本旋转（面向相机）
            if (statusText != null && Camera.main != null)
            {
                statusText.transform.rotation = Quaternion.LookRotation(
                    statusText.transform.position - Camera.main.transform.position
                );
            }
        }
        
        /// <summary>
        /// 实现IInteractable接口
        /// </summary>
        public void Interact(GameObject interactor)
        {
            OnPlayerInteract(interactor);
        }
        
        /// <summary>
        /// 玩家交互
        /// </summary>
        public void OnPlayerInteract(GameObject player)
        {
            if (isActivated) return;
            
            Debug.Log($"[TeleportDevice] OnPlayerInteract called by {player.name}");
            
            if (interactingPlayers.Contains(player))
            {
                // 取消交互
                interactingPlayers.Remove(player);
                currentPlayerCount = interactingPlayers.Count;
                Debug.Log($"[TeleportDevice] Player stopped interacting. Current: {currentPlayerCount}/{requiredPlayers}");
            }
            else
            {
                // 开始交互
                interactingPlayers.Add(player);
                currentPlayerCount = interactingPlayers.Count;
                Debug.Log($"[TeleportDevice] Player started interacting. Current: {currentPlayerCount}/{requiredPlayers}");
            }
            
            UpdateVisuals();
            
            // 检查是否满足激活条件
            if (interactingPlayers.Count >= requiredPlayers && !isActivating)
            {
                Debug.Log($"[TeleportDevice] Activation condition met! Starting activation...");
                StartCoroutine(ActivateDevice());
            }
        }
        
        /// <summary>
        /// 激活装置
        /// </summary>
        private IEnumerator ActivateDevice()
        {
            isActivating = true;
            Debug.Log("[TeleportDevice] Activation started!");
            
            // 更新状态文本
            float timer = activationDelay;
            while (timer > 0)
            {
                if (statusText != null)
                    statusText.text = $"Activating... {timer:F1}s";
                timer -= Time.deltaTime;
                
                // 检查是否所有玩家仍在交互
                if (interactingPlayers.Count < requiredPlayers)
                {
                    Debug.Log("[TeleportDevice] Activation cancelled - not enough players!");
                    isActivating = false;
                    UpdateVisuals();
                    yield break;
                }
                
                yield return null;
            }
            
            // 激活成功
            isActivated = true;
            isActivating = false;
            if (statusText != null)
                statusText.text = "Teleport Active!";
            
            Debug.Log("[TeleportDevice] Device activated! Starting teleport sequence...");
            
            // 传送延迟
            yield return new WaitForSeconds(teleportDelay);
            
            // 执行传送
            TeleportPlayers();
        }
        
        /// <summary>
        /// 传送玩家
        /// </summary>
        private void TeleportPlayers()
        {
            if (mapGenerator == null)
            {
                Debug.LogError("[TeleportDevice] MapGenerator not found!");
                return;
            }
            
            Debug.Log("[TeleportDevice] Teleporting players to next level!");
            
            // 传送到下一层
            mapGenerator.TeleportToNextLevel();
        }
        
        /// <summary>
        /// 更新视觉效果
        /// </summary>
        private void UpdateVisuals()
        {
            // 更新玩家指示器
            for (int i = 0; i < playerIndicators.Length; i++)
            {
                if (i < interactingPlayers.Count)
                {
                    playerIndicators[i].GetComponent<Renderer>().material.color = Color.green;
                }
                else
                {
                    playerIndicators[i].GetComponent<Renderer>().material.color = Color.red;
                }
            }
            
            // 更新状态文本
            if (!isActivating && !isActivated && statusText != null)
            {
                statusText.text = $"Need {requiredPlayers - interactingPlayers.Count} more players";
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                // 显示交互提示
                ShowInteractionHint(other.gameObject, true);
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                // 隐藏交互提示
                ShowInteractionHint(other.gameObject, false);
                
                // 如果玩家离开，取消其交互状态
                if (interactingPlayers.Contains(other.gameObject))
                {
                    interactingPlayers.Remove(other.gameObject);
                    UpdateVisuals();
                }
            }
        }
        
        /// <summary>
        /// 显示/隐藏交互提示
        /// </summary>
        private void ShowInteractionHint(GameObject player, bool show)
        {
            // 这里可以显示 [E] 交互提示
            // 暂时使用Debug.Log
            if (show)
            {
                Debug.Log($"[TeleportDevice] Press E to interact with teleport device");
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // 交互范围
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // 传送装置范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(3f, 1f, 3f));
        }
    }
}