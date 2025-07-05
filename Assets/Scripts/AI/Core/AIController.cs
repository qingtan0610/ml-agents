using UnityEngine;
using System.Collections;
using AI.Stats;
using Inventory;
using Inventory.Items;
using Inventory.Managers;
using NPC;
using NPC.Core;
using Enemy;
using Combat;
using Player;
using Interactables;
using Rooms;

namespace AI.Core
{
    /// <summary>
    /// AI控制器 - 负责执行AI的具体行动
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerController2D))]
    public class AIController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private bool isRunning = false;
        
        [Header("Combat")]
        [SerializeField] private float attackCooldown = 0.5f;
        [SerializeField] private float attackRange = 1.5f;
        
        [Header("Interaction")]
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private LayerMask interactableLayer;
        
        [Header("Communication")]
        [SerializeField] private float communicationCooldown = 5f;
        [SerializeField] private float communicationRange = 20f;
        
        // 组件引用
        private Rigidbody2D rb;
        private PlayerController2D playerController;
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        private CurrencyManager currencyManager;
        private AmmoManager ammoManager;
        private CombatSystem2D combatSystem;
        private AICommunicator communicator;
        
        // 状态
        private Vector2 moveDirection;
        private float nextAttackTime;
        private float nextCommunicationTime;
        private bool killedEnemyThisFrame = false;
        private bool collectedItemThisFrame = false;
        private bool reachedPortal = false;
        
        // 当前目标
        private GameObject currentTarget;
        private AIActionPriority currentPriority = AIActionPriority.Normal;
        
        private void Awake()
        {
            // 获取组件
            rb = GetComponent<Rigidbody2D>();
            playerController = GetComponent<PlayerController2D>();
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            ammoManager = GetComponent<AmmoManager>();
            combatSystem = GetComponent<CombatSystem2D>();
            
            // 创建通信组件
            communicator = gameObject.AddComponent<AICommunicator>();
            
            // 设置层
            if (interactableLayer == 0)
            {
                interactableLayer = LayerMask.GetMask("Interactable", "NPC", "Item");
            }
        }
        
        private void Update()
        {
            // 重置帧标记
            killedEnemyThisFrame = false;
            collectedItemThisFrame = false;
            
            // 更新移动
            if (moveDirection != Vector2.zero && !aiStats.IsDead)
            {
                playerController.SetMovementInput(moveDirection);
            }
        }
        
        // 移动控制
        public void Move(Vector2 direction)
        {
            if (aiStats.IsDead) return;
            
            moveDirection = direction.normalized;
            
            // 根据体力决定是否奔跑
            if (isRunning && aiStats.CurrentStamina > 10f)
            {
                playerController.StartSprint();
            }
            else
            {
                playerController.StopSprint();
                isRunning = false;
            }
        }
        
        public void StopMoving()
        {
            moveDirection = Vector2.zero;
            playerController.SetMovementInput(Vector2.zero);
        }
        
        // 战斗控制
        public void Attack(GameObject target)
        {
            if (target == null || Time.time < nextAttackTime || aiStats.IsDead) return;
            
            var enemy = target.GetComponent<Enemy2D>();
            if (enemy == null || !enemy.IsAlive) return;
            
            // 检查距离
            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance > attackRange) return;
            
            // 面向目标
            Vector2 direction = (target.transform.position - transform.position).normalized;
            playerController.SetAimDirection(direction);
            
            // 执行攻击
            playerController.PerformAttack();
            nextAttackTime = Time.time + attackCooldown;
            
            // 监听敌人死亡
            if (!enemy.IsAlive)
            {
                killedEnemyThisFrame = true;
            }
        }
        
        // 交互控制
        public void TryInteract()
        {
            if (aiStats.IsDead) return;
            
            // 寻找最近的可交互对象
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange, interactableLayer);
            
            GameObject closest = null;
            float closestDistance = float.MaxValue;
            
            foreach (var collider in colliders)
            {
                float distance = Vector2.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    // 检查是否可交互
                    var npc = collider.GetComponent<NPCBase>();
                    if (npc != null && npc.CanInteract(gameObject))
                    {
                        closest = collider.gameObject;
                        closestDistance = distance;
                        continue;
                    }
                    
                    // 检查其他可交互对象
                    if (collider.GetComponent<IInteractable>() != null)
                    {
                        closest = collider.gameObject;
                        closestDistance = distance;
                    }
                }
            }
            
            if (closest != null)
            {
                InteractWith(closest);
            }
        }
        
        private void InteractWith(GameObject target)
        {
            // NPC交互
            var npc = target.GetComponent<NPCBase>();
            if (npc != null)
            {
                npc.StartInteraction(gameObject);
                StartCoroutine(HandleNPCInteraction(npc));
                return;
            }
            
            // 其他交互
            var interactable = target.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(gameObject);
                
                // 检查是否是传送门
                if (target.name.Contains("Portal"))
                {
                    reachedPortal = true;
                }
            }
        }
        
        private IEnumerator HandleNPCInteraction(NPCBase npc)
        {
            // AI与NPC的自动交互逻辑
            yield return new WaitForSeconds(1f);
            
            // 根据NPC类型执行不同的交互
            switch (npc.NPCType)
            {
                case NPCType.Merchant:
                    HandleMerchantInteraction(npc);
                    break;
                case NPCType.Doctor:
                    HandleDoctorInteraction(npc);
                    break;
                case NPCType.Restaurant:
                    HandleRestaurantInteraction(npc);
                    break;
                case NPCType.Blacksmith:
                    HandleBlacksmithInteraction(npc);
                    break;
                case NPCType.Tailor:
                    HandleTailorInteraction(npc);
                    break;
            }
            
            yield return new WaitForSeconds(2f);
            npc.EndInteraction();
        }
        
        private void HandleMerchantInteraction(NPCBase merchant)
        {
            // AI购买决策
            if (currencyManager.CurrentGold > 100)
            {
                // 优先购买补给品
                Debug.Log($"[AI] {name} 正在商人处购买补给品");
            }
        }
        
        private void HandleDoctorInteraction(NPCBase doctor)
        {
            // AI治疗决策
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f && currencyManager.CurrentGold > 50)
            {
                Debug.Log($"[AI] {name} 正在接受治疗");
            }
        }
        
        private void HandleRestaurantInteraction(NPCBase restaurant)
        {
            // AI进食决策
            if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f || 
                aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
            {
                Debug.Log($"[AI] {name} 正在餐厅用餐");
            }
        }
        
        private void HandleBlacksmithInteraction(NPCBase blacksmith)
        {
            // AI装备升级决策
            if (inventory.EquippedWeapon != null && currencyManager.CurrentGold > 200)
            {
                Debug.Log($"[AI] {name} 正在升级武器");
            }
        }
        
        private void HandleTailorInteraction(NPCBase tailor)
        {
            // AI背包扩容决策
            int usedSlots = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty) usedSlots++;
            }
            
            if (usedSlots > inventory.Size * 0.8f && currencyManager.CurrentGold > 500)
            {
                Debug.Log($"[AI] {name} 正在扩容背包");
            }
        }
        
        // 物品使用
        public void UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= inventory.Size || aiStats.IsDead) return;
            
            var slot = inventory.GetSlot(slotIndex);
            if (slot.IsEmpty) return;
            
            var consumable = slot.Item as ConsumableItem;
            if (consumable != null)
            {
                // 使用消耗品
                inventory.UseItem(slotIndex);
                Debug.Log($"[AI] {name} 使用了 {consumable.ItemName}");
            }
            else
            {
                // 装备武器
                var weapon = slot.Item as WeaponItem;
                if (weapon != null)
                {
                    inventory.EquipWeapon(weapon);
                    Debug.Log($"[AI] {name} 装备了 {weapon.ItemName}");
                }
            }
        }
        
        // 通信控制
        public void SendCommunication(CommunicationType type)
        {
            if (Time.time < nextCommunicationTime || aiStats.IsDead) return;
            
            communicator.SendMessage(type, transform.position);
            nextCommunicationTime = Time.time + communicationCooldown;
        }
        
        // 优先级设置
        public void SetPriority(AIActionPriority priority)
        {
            currentPriority = priority;
            
            // 根据优先级调整行为
            switch (priority)
            {
                case AIActionPriority.Survival:
                    // 生存优先 - 提高移动速度，降低攻击频率
                    isRunning = true;
                    attackCooldown = 1f;
                    break;
                case AIActionPriority.Combat:
                    // 战斗优先 - 降低移动速度，提高攻击频率
                    isRunning = false;
                    attackCooldown = 0.3f;
                    break;
                case AIActionPriority.Exploration:
                    // 探索优先 - 正常速度
                    isRunning = false;
                    attackCooldown = 0.5f;
                    break;
            }
        }
        
        // 重置到出生点
        public void ResetToSpawn()
        {
            var mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null)
            {
                transform.position = mapGenerator.GetSpawnPosition();
            }
            
            StopMoving();
            currentTarget = null;
            reachedPortal = false;
        }
        
        // 状态查询
        public bool KilledEnemyThisFrame() => killedEnemyThisFrame;
        public bool CollectedItemThisFrame() => collectedItemThisFrame;
        public bool ReachedPortal() => reachedPortal;
        
        // 拾取物品事件
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Pickup"))
            {
                collectedItemThisFrame = true;
            }
        }
        
        // Gizmos
        private void OnDrawGizmosSelected()
        {
            // 攻击范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // 交互范围
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // 通信范围
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, communicationRange);
        }
    }
    
    public enum AIActionPriority
    {
        Normal,
        Survival,
        Combat,
        Exploration
    }
}