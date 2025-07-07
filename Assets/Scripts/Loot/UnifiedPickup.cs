using UnityEngine;
using Inventory;
using Inventory.Items;
using Interactables;

namespace Loot
{
    /// <summary>
    /// 统一的拾取物系统 - 所有拾取物都基于物品
    /// </summary>
    public class UnifiedPickup : MonoBehaviour, IInteractable
    {
        [Header("Pickup Settings")]
        [SerializeField] private float pickupRange = 1.5f;
        [SerializeField] private float magnetRange = 3f;
        [SerializeField] private float magnetSpeed = 5f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobSpeed = 2f;
        
        [Header("Interaction")]
        [SerializeField] private bool requiresInteraction = false; // 是否需要按E交互
        [SerializeField] private GameObject interactionPrompt; // 交互提示UI
        
        [Header("Content")]
        [SerializeField] private ItemBase item;
        [SerializeField] private int quantity = 1;
        
        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject pickupEffect;
        
        private GameObject magnetTarget;
        private Vector3 startPosition;
        private bool isPicked = false;
        
        // 公共属性
        public ItemBase PickupItem => item;
        public int Quantity => quantity;
        public bool CanPickup => !isPicked;
        public bool RequiresInteraction => requiresInteraction;
        
        private void Awake()
        {
            // 如果没有手动赋值，尝试自动获取
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                }
                
                if (spriteRenderer == null)
                {
                    Debug.LogError($"[UnifiedPickup] No SpriteRenderer found on {gameObject.name}!");
                }
            }
        }
        
        private void Start()
        {
            startPosition = transform.position;
            
            // 设置精灵
            UpdateVisual();
            
            // 设置碰撞器
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }
        
        /// <summary>
        /// 初始化拾取物
        /// </summary>
        public void Initialize(ItemBase itemToPickup, int amount = 1)
        {
            item = itemToPickup;
            quantity = amount;
            
            // 根据物品类型决定是否需要交互
            SetupInteractionMode();
            
            UpdateVisual();
        }
        
        private void SetupInteractionMode()
        {
            if (item == null) return;
            
            // 金币、弹药等特殊消耗品自动拾取
            if (item is SpecialConsumable special)
            {
                requiresInteraction = false; // 自动拾取
            }
            else
            {
                // 药水、武器、装备等需要按E交互
                requiresInteraction = true;
            }
            
            // 显示或隐藏交互提示
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false); // 初始隐藏
            }
        }
        
        private void UpdateVisual()
        {
            if (item == null)
            {
                Debug.LogWarning($"[UnifiedPickup] No item assigned to {gameObject.name}");
                return;
            }
            
            if (spriteRenderer == null)
            {
                Debug.LogError($"[UnifiedPickup] No SpriteRenderer found for {gameObject.name}");
                return;
            }
            
            // 优先使用物品自带的图标
            if (item.Icon != null)
            {
                spriteRenderer.sprite = item.Icon;
                Debug.Log($"[UnifiedPickup] Set icon for {item.ItemName} on {gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[UnifiedPickup] Item {item.ItemName} has no icon!");
                
                // 如果是特殊消耗品，根据类型加载默认图标
                if (item is SpecialConsumable special && item.Icon == null)
                {
                    string iconPath = GetDefaultIconPath(special);
                    if (!string.IsNullOrEmpty(iconPath))
                    {
                        var defaultSprite = Resources.Load<Sprite>(iconPath);
                        if (defaultSprite != null)
                        {
                            spriteRenderer.sprite = defaultSprite;
                        }
                    }
                }
                
                // 显示数量（如果大于1）
                if (quantity > 1)
                {
                    // TODO: 添加数量显示UI
                }
            }
        }
        
        private string GetDefaultIconPath(SpecialConsumable special)
        {
            // 根据物品名称或ID返回默认图标路径
            if (special.ItemName.Contains("金币") || special.ItemName.ToLower().Contains("gold"))
                return "Sprites/Icons/icon_gold";
            else if (special.ItemName.Contains("子弹") || special.ItemName.ToLower().Contains("bullet"))
                return "Sprites/Icons/icon_bullet";
            else if (special.ItemName.Contains("箭") || special.ItemName.ToLower().Contains("arrow"))
                return "Sprites/Icons/icon_arrow";
            else if (special.ItemName.Contains("法力") || special.ItemName.ToLower().Contains("mana"))
                return "Sprites/Icons/icon_mana";
            
            return null;
        }
        
        private void Update()
        {
            if (isPicked) return;
            
            // 漂浮动画
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            
            // 磁吸效果（只对自动拾取物品有效）
            if (!requiresInteraction && magnetTarget == null)
            {
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, magnetRange);
                foreach (var col in colliders)
                {
                    if (col.CompareTag("Player"))
                    {
                        magnetTarget = col.gameObject;
                        break;
                    }
                }
            }
            
            // 交互提示显示逻辑
            if (requiresInteraction)
            {
                UpdateInteractionPrompt();
            }
            
            // 向目标移动
            if (magnetTarget != null)
            {
                float distance = Vector2.Distance(transform.position, magnetTarget.transform.position);
                
                if (distance <= pickupRange)
                {
                    TryPickup(magnetTarget);
                }
                else if (distance <= magnetRange)
                {
                    Vector2 direction = (magnetTarget.transform.position - transform.position).normalized;
                    transform.position = Vector2.MoveTowards(transform.position, 
                        magnetTarget.transform.position, magnetSpeed * Time.deltaTime);
                }
                else
                {
                    magnetTarget = null;
                }
            }
        }
        
        private void UpdateInteractionPrompt()
        {
            if (interactionPrompt == null) return;
            
            // 检查附近是否有玩家
            bool playerNearby = false;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, pickupRange);
            foreach (var col in colliders)
            {
                if (col.CompareTag("Player"))
                {
                    playerNearby = true;
                    break;
                }
            }
            
            interactionPrompt.SetActive(playerNearby);
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                if (requiresInteraction)
                {
                    // 需要交互的物品，显示提示但不自动拾取
                    if (interactionPrompt != null)
                    {
                        interactionPrompt.SetActive(true);
                    }
                }
                else
                {
                    // 自动拾取的物品（金币、弹药）
                    TryPickup(other.gameObject);
                }
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") && requiresInteraction)
            {
                if (interactionPrompt != null)
                {
                    interactionPrompt.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// 公共方法：直接拾取物品
        /// </summary>
        public void Pickup(GameObject picker)
        {
            TryPickup(picker);
        }
        
        private void TryPickup(GameObject picker)
        {
            if (isPicked || item == null) 
            {
                if (isPicked) Debug.Log($"[UnifiedPickup] Already picked up: {gameObject.name}");
                if (item == null) Debug.Log($"[UnifiedPickup] No item assigned: {gameObject.name}");
                return;
            }
            
            Debug.Log($"[UnifiedPickup] Attempting to pickup {item.ItemName} x{quantity}");
            bool success = false;
            
            // 检查是否是特殊消耗品（金币、弹药等）
            if (item is SpecialConsumable special && special.ShouldAddToInventory == false)
            {
                // 特殊消耗品直接使用，不进背包
                for (int i = 0; i < quantity; i++)
                {
                    special.OnPickup(picker);
                }
                success = true;
                Debug.Log($"[UnifiedPickup] Picked up {quantity}x {item.ItemName}");
            }
            else
            {
                // 普通物品尝试加入背包
                var inventory = picker.GetComponent<Inventory.Inventory>();
                if (inventory != null)
                {
                    Debug.Log($"[UnifiedPickup] Attempting to add {quantity}x {item.ItemName} to inventory");
                    success = inventory.AddItem(item, quantity);
                    if (success)
                    {
                        Debug.Log($"[UnifiedPickup] Successfully added {quantity}x {item.ItemName} to inventory");
                    }
                    else
                    {
                        Debug.LogWarning($"[UnifiedPickup] Failed to add {quantity}x {item.ItemName} to inventory - possibly full or invalid item");
                    }
                }
                else
                {
                    Debug.LogError($"[UnifiedPickup] No inventory component found on {picker.name}");
                }
            }
            
            if (success)
            {
                isPicked = true;
                Debug.Log($"[UnifiedPickup] Pickup successful, marking as picked and destroying {gameObject.name}");
                
                // 播放拾取特效
                if (pickupEffect != null)
                {
                    Instantiate(pickupEffect, transform.position, Quaternion.identity);
                }
                
                // 立即禁用碰撞器和渲染器，防止重复拾取
                var collider = GetComponent<Collider2D>();
                if (collider != null) collider.enabled = false;
                
                var renderer = GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.enabled = false;
                
                // 销毁拾取物（延迟一帧确保所有状态更新完成）
                Destroy(gameObject, 0.1f);
            }
            else
            {
                Debug.LogWarning($"[UnifiedPickup] Pickup failed for {item?.ItemName ?? "unknown item"}");
            }
        }
        
        // IInteractable 接口实现
        public void Interact(GameObject interactor)
        {
            if (requiresInteraction)
            {
                Debug.Log($"[UnifiedPickup] Player interacted with {item?.ItemName}");
                TryPickup(interactor);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }
    }
}