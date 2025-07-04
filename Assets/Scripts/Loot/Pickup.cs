using UnityEngine;
using Inventory;
using Inventory.Items;
using Inventory.Managers;
using AI.Stats;

namespace Loot
{
    /// <summary>
    /// 可拾取的物体
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [SerializeField] private float pickupRange = 1.5f;
        [SerializeField] private float magnetRange = 3f;
        [SerializeField] private float magnetSpeed = 5f;
        [SerializeField] private bool autoPickup = true;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobSpeed = 2f;
        
        [Header("Pickup Content")]
        [SerializeField] private PickupType pickupType;
        [SerializeField] private int amount = 1;
        [SerializeField] private ItemBase item;
        
        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject pickupEffect;
        
        private GameObject magnetTarget;
        private Vector3 startPosition;
        private bool isPicked = false;
        
        private void Start()
        {
            startPosition = transform.position;
            
            // 自动设置精灵
            if (spriteRenderer != null && item != null && item.Icon != null)
            {
                spriteRenderer.sprite = item.Icon;
            }
            
            // 设置碰撞器
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// 初始化拾取物
        /// </summary>
        public void Initialize(PickupType type, int value)
        {
            pickupType = type;
            amount = value;
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// 初始化物品拾取物
        /// </summary>
        public void InitializeItem(ItemBase itemToPickup, int quantity = 1)
        {
            pickupType = PickupType.Item;
            item = itemToPickup;
            amount = quantity;
            
            UpdateVisuals();
        }
        
        /// <summary>
        /// 更新视觉效果
        /// </summary>
        private void UpdateVisuals()
        {
            if (spriteRenderer == null) return;
            
            // 根据类型设置精灵
            if (pickupType == PickupType.Item && item != null && item.Icon != null)
            {
                spriteRenderer.sprite = item.Icon;
            }
            else
            {
                // 为其他类型设置默认精灵
                // 这里可以根据pickupType加载不同的精灵
            }
        }
        
        private void Update()
        {
            if (isPicked) return;
            
            // 漂浮动画
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            
            // 磁吸效果
            if (autoPickup && magnetTarget == null)
            {
                // 查找最近的玩家
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
                    // 磁吸移动
                    Vector2 direction = (magnetTarget.transform.position - transform.position).normalized;
                    transform.position = Vector2.MoveTowards(transform.position, magnetTarget.transform.position, magnetSpeed * Time.deltaTime);
                }
                else
                {
                    magnetTarget = null;
                }
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!autoPickup && other.CompareTag("Player"))
            {
                TryPickup(other.gameObject);
            }
        }
        
        public void Initialize(PickupType type, int amount, ItemBase item = null)
        {
            this.pickupType = type;
            this.amount = amount;
            this.item = item;
            
            // 更新视觉
            if (spriteRenderer != null && item != null && item.Icon != null)
            {
                spriteRenderer.sprite = item.Icon;
            }
        }
        
        private void TryPickup(GameObject picker)
        {
            if (isPicked) return;
            
            bool success = false;
            
            switch (pickupType)
            {
                case PickupType.Gold:
                    var currency = picker.GetComponent<CurrencyManager>();
                    if (currency != null)
                    {
                        currency.AddGold(amount);
                        success = true;
                        Debug.Log($"Picked up {amount} gold");
                    }
                    break;
                    
                case PickupType.Bullets:
                    var ammo = picker.GetComponent<AmmoManager>();
                    if (ammo != null)
                    {
                        ammo.AddAmmo(AmmoType.Bullets, amount);
                        success = true;
                        Debug.Log($"Picked up {amount} bullets");
                    }
                    break;
                    
                case PickupType.Arrows:
                    ammo = picker.GetComponent<AmmoManager>();
                    if (ammo != null)
                    {
                        ammo.AddAmmo(AmmoType.Arrows, amount);
                        success = true;
                        Debug.Log($"Picked up {amount} arrows");
                    }
                    break;
                    
                case PickupType.Mana:
                    ammo = picker.GetComponent<AmmoManager>();
                    if (ammo != null)
                    {
                        ammo.AddAmmo(AmmoType.Mana, amount);
                        success = true;
                        Debug.Log($"Picked up {amount} mana");
                    }
                    break;
                    
                case PickupType.Item:
                    if (item != null)
                    {
                        var inventory = picker.GetComponent<Inventory.Inventory>();
                        if (inventory != null)
                        {
                            success = inventory.AddItem(item, amount);
                            if (success)
                            {
                                Debug.Log($"Picked up {amount}x {item.ItemName}");
                            }
                            else
                            {
                                Debug.Log("Inventory full!");
                            }
                        }
                    }
                    break;
                    
                case PickupType.Health:
                    var stats = picker.GetComponent<AIStats>();
                    if (stats != null)
                    {
                        stats.ModifyStat(StatType.Health, amount, StatChangeReason.Item);
                        success = true;
                        Debug.Log($"Picked up health potion ({amount} HP)");
                    }
                    break;
            }
            
            if (success)
            {
                isPicked = true;
                
                // 播放拾取特效
                if (pickupEffect != null)
                {
                    Instantiate(pickupEffect, transform.position, Quaternion.identity);
                }
                
                // 销毁拾取物
                Destroy(gameObject);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // 拾取范围
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
            
            // 磁吸范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }
    }
    
    public enum PickupType
    {
        Gold,
        Bullets,
        Arrows,
        Mana,
        Item,
        Health
    }
}