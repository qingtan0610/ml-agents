using UnityEngine;
using System.Collections.Generic;
using Loot;
using Inventory;
using Inventory.Items;
using Rooms.Core;

namespace Interactables
{
    /// <summary>
    /// 宝箱交互对象
    /// </summary>
    public class TreasureChest : MonoBehaviour, IInteractable
    {
        [Header("Chest Settings")]
        [SerializeField] private bool isLocked = false;
        [SerializeField] private bool requiresUniversalKey = true; // 是否需要通用钥匙
        [SerializeField] private bool isOpened = false;
        [SerializeField] private float interactionRange = 2f;
        
        [Header("Loot Configuration")]
        [SerializeField] private LootTable lootTable; // 掉落表
        [SerializeField] private int extraGoldAmount = 0; // 额外固定金币数量（在掉落表之外）
        [SerializeField] private List<GuaranteedDrop> guaranteedItems = new List<GuaranteedDrop>(); // 保证掉落的物品
        
        [Header("Pickup Configuration")]
        [SerializeField] private GameObject unifiedPickupPrefab; // 统一拾取物预制体
        [SerializeField] private float dropSpread = 1.5f; // 掉落散布范围
        
        [Header("Visual")]
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openedVisual;
        [SerializeField] private Animator chestAnimator;
        [SerializeField] private string openAnimationTrigger = "Open";
        [SerializeField] private ParticleSystem openEffect;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip lockedSound;
        
        [Header("UI")]
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private string openPromptText = "按E打开宝箱";
        [SerializeField] private string lockedPromptText = "需要钥匙";
        [SerializeField] private string emptyPromptText = "宝箱已打开";
        
        
        [System.Serializable]
        public class GuaranteedDrop
        {
            public ItemBase item; // 直接引用物品，而不是通过ID
            public int minAmount = 1;
            public int maxAmount = 1;
            [Range(0f, 1f)] public float dropChance = 1f;
        }
        
        private void Start()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
            
            UpdateVisuals();
        }
        
        
        private void UpdateVisuals()
        {
            if (closedVisual != null)
                closedVisual.SetActive(!isOpened);
            
            if (openedVisual != null)
                openedVisual.SetActive(isOpened);
        }
        
        public void Interact(GameObject interactor)
        {
            Debug.Log($"[TreasureChest] Interact called by {interactor.name}. isOpened: {isOpened}, isLocked: {isLocked}");
            
            if (isOpened) 
            {
                Debug.Log("[TreasureChest] Chest already opened!");
                return;
            }
            
            // 检查是否上锁
            if (isLocked)
            {
                Debug.Log("[TreasureChest] Chest is locked, trying to unlock...");
                if (!TryUnlock(interactor))
                {
                    // 播放锁定音效
                    if (audioSource != null && lockedSound != null)
                    {
                        audioSource.PlayOneShot(lockedSound);
                    }
                    Debug.Log($"[TreasureChest] Chest is locked! Requires a key to open.");
                    return;
                }
            }
            
            // 打开宝箱
            Debug.Log("[TreasureChest] Opening chest...");
            OpenChest(interactor);
        }
        
        private bool TryUnlock(GameObject interactor)
        {
            var inventory = interactor.GetComponent<Inventory.Inventory>();
            if (inventory == null) return false;
            
            // 查找通用钥匙
            ItemBase foundKey = null;
            foreach (var slot in inventory.GetAllSlots())
            {
                if (!slot.IsEmpty && slot.Item is KeyItem keyItem && keyItem.IsUniversal)
                {
                    foundKey = slot.Item;
                    break;
                }
            }
            
            if (foundKey != null && inventory.GetItemCount(foundKey) > 0)
            {
                // 消耗钥匙
                inventory.RemoveItem(foundKey, 1);
                isLocked = false;
                Debug.Log($"[TreasureChest] Unlocked with universal key");
                return true;
            }
            
            return false;
        }
        
        private void OpenChest(GameObject interactor)
        {
            Debug.Log("[TreasureChest] OpenChest method called");
            isOpened = true;
            
            // 播放开启动画
            if (chestAnimator != null && !string.IsNullOrEmpty(openAnimationTrigger))
            {
                Debug.Log($"[TreasureChest] Playing animation: {openAnimationTrigger}");
                chestAnimator.SetTrigger(openAnimationTrigger);
            }
            else
            {
                Debug.Log("[TreasureChest] No animator or trigger configured");
            }
            
            // 播放开启特效
            if (openEffect != null)
            {
                openEffect.Play();
            }
            
            // 播放开启音效
            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }
            
            // 生成掉落物
            Debug.Log("[TreasureChest] Starting loot spawn coroutine...");
            StartCoroutine(SpawnLoot(interactor));
            
            // 更新视觉效果
            UpdateVisuals();
        }
        
        private System.Collections.IEnumerator SpawnLoot(GameObject interactor)
        {
            Debug.Log("[TreasureChest] SpawnLoot coroutine started");
            
            // 等待动画播放
            yield return new WaitForSeconds(0.5f);
            
            var spawnPosition = transform.position + Vector3.up * 0.5f;
            Debug.Log($"[TreasureChest] Spawn position: {spawnPosition}");
            
            // 生成保证掉落的物品
            Debug.Log($"[TreasureChest] Guaranteed items count: {guaranteedItems.Count}");
            foreach (var drop in guaranteedItems)
            {
                if (drop.item != null && Random.value <= drop.dropChance)
                {
                    int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                    Debug.Log($"[TreasureChest] Spawning guaranteed item: {drop.item.ItemName} x{amount}");
                    SpawnItemPickup(drop.item, amount, spawnPosition);
                }
            }
            
            // 使用掉落表生成物品
            if (lootTable != null)
            {
                Debug.Log("[TreasureChest] Generating loot from loot table...");
                var loot = lootTable.GenerateLoot(1f); // 可以传入幸运值
                
                // 生成物品（金币弹药通过guaranteedItems配置）
                foreach (var drop in loot.items)
                {
                    SpawnItemPickup(drop.item, drop.quantity, spawnPosition);
                }
            }
            
            Debug.Log($"[TreasureChest] Chest opened by {interactor.name}!");
        }
        
        
        private void SpawnItemPickup(ItemBase item, int amount, Vector3 position)
        {
            if (item == null || unifiedPickupPrefab == null) 
            {
                Debug.LogWarning($"[TreasureChest] Cannot spawn pickup: item={item?.ItemName ?? "null"}, prefab={unifiedPickupPrefab != null}");
                return;
            }
            
            // 获取房间边界
            SimplifiedRoom currentRoom = GetComponentInParent<SimplifiedRoom>();
            float roomHalfSize = 7f; // 房间半径，默认值
            if (currentRoom != null)
            {
                roomHalfSize = 7f; // SimplifiedRoom的标准大小是16x16，半径约7
            }
            
            // 为多个物品创建散落效果
            int stacks = 1;
            int amountPerStack = amount;
            
            // 如果是金币或弹药，分成多个堆
            if (item is SpecialConsumable && amount > 10)
            {
                stacks = Mathf.Min(3, amount / 10); // 最多3堆
                amountPerStack = amount / stacks;
            }
            
            for (int i = 0; i < stacks; i++)
            {
                // 计算当前堆的数量
                int stackAmount = (i == stacks - 1) ? 
                    amount - (amountPerStack * (stacks - 1)) : // 最后一堆包含余数
                    amountPerStack;
                
                // 计算生成位置 - 确保在房间内
                float angle = (360f / stacks) * i + Random.Range(-15f, 15f);
                float distance = Random.Range(0.1f, 0.3f); // 非常小的初始偏移，主要靠物理分离
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                );
                
                // 限制在房间边界内
                Vector3 spawnPos = position + new Vector3(offset.x, offset.y, 0);
                Vector3 roomCenter = currentRoom != null ? currentRoom.transform.position : position;
                Vector3 toSpawn = spawnPos - roomCenter;
                
                // 如果超出房间边界，将其限制在边界内
                if (toSpawn.magnitude > roomHalfSize - 1f) // 留1单位的边距
                {
                    toSpawn = toSpawn.normalized * (roomHalfSize - 1f);
                    spawnPos = roomCenter + toSpawn;
                }
                
                // 创建拾取物
                GameObject pickup = Instantiate(unifiedPickupPrefab, spawnPos, Quaternion.identity);
                var pickupComponent = pickup.GetComponent<UnifiedPickup>();
                
                if (pickupComponent != null)
                {
                    pickupComponent.Initialize(item, stackAmount);
                }
                
                // 添加碰撞检测组件，用于检测墙壁并停止移动
                var stopOnWall = pickup.AddComponent<StopOnWallCollision>();
                
                // 添加极小的弹出效果
                var rb = pickup.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // 极小的力，只是让物品稍微分开
                    Vector2 force = offset.normalized * Random.Range(0.2f, 0.4f) + Vector2.up * Random.Range(0.1f, 0.3f);
                    rb.AddForce(force, ForceMode2D.Impulse);
                    
                    // 高阻力确保快速停止
                    rb.drag = 8f;
                    rb.angularDrag = 8f;
                    
                    // 确保重力为0，避免物品掉落
                    rb.gravityScale = 0f;
                    
                    // 很小的旋转
                    rb.angularVelocity = Random.Range(-30f, 30f);
                }
            }
        }
        
        
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && interactionPrompt != null && !isOpened)
            {
                interactionPrompt.SetActive(true);
                
                // 根据状态更新提示文字
                var textComponent = interactionPrompt.GetComponentInChildren<UnityEngine.UI.Text>();
                if (textComponent != null)
                {
                    if (isLocked) textComponent.text = lockedPromptText;
                    else textComponent.text = openPromptText;
                }
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") && interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isOpened ? Color.gray : (isLocked ? Color.red : Color.yellow);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
        
        /// <summary>
        /// 重置宝箱状态（用于测试或地图重置）
        /// </summary>
        [ContextMenu("Reset Chest")]
        public void ResetChest()
        {
            isOpened = false;
            UpdateVisuals();
        }
    }
}