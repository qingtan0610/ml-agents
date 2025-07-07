using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Decision;
using AI.Visual;
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
using Visuals;

namespace AI.Core
{
    /// <summary>
    /// 交互优先级
    /// </summary>
    public enum InteractionPriority
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
    
    /// <summary>
    /// AI控制器 - 负责执行AI的具体行动
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CombatSystem2D))]
    public class AIController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private bool isRunning = false;
        
        [Header("Combat")]
        [SerializeField] private float attackCooldown = 0.5f;
        [SerializeField] private float attackRange = 3.5f; // 增加攻击范围
        
        [Header("Interaction")]
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private LayerMask interactableLayer;
        
        [Header("Communication")]
        [SerializeField] private float communicationCooldown = 5f;
        [SerializeField] private float communicationRange = 20f;
        [SerializeField] private float helpCooldown = 30f;    // help请求的冷却时间
        
        // 组件引用
        private Rigidbody2D rb;
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        private CurrencyManager currencyManager;
        private AmmoManager ammoManager;
        private CombatSystem2D combatSystem;
        private AICommunicator communicator;
        private AnimationController2D animController;
        private AIWeaponVisualDisplay weaponVisual;
        private AIItemEvaluator itemEvaluator;
        private AIEconomicSystem economicSystem;
        
        // 状态
        private Vector2 moveDirection;
        private float nextAttackTime;
        private float nextCommunicationTime;
        private float nextHelpTime;               // help请求的下次可用时间
        private bool killedEnemyThisFrame = false;
        private bool collectedItemThisFrame = false;
        private bool reachedPortal = false;
        
        // 当前目标
        private GameObject currentTarget;
        private AIActionPriority currentPriority = AIActionPriority.Normal;
        private GameObject chaseTarget; // 需要追击的目标
        public GameObject ChaseTarget => chaseTarget;
        
        // DeepSeek决策
        private AIDecision currentDeepSeekDecision;
        private float lastDecisionTime = -10f;
        private bool useDeepSeekForTrade = true; // 是否使用DeepSeek进行交易决策
        
        // 面对面交流
        private float faceToFaceRange = 2f;
        private float lastFaceToFaceTime = 0f;
        private float faceToFaceCooldown = 10f;
        
        private void Awake()
        {
            // 获取组件
            rb = GetComponent<Rigidbody2D>();
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            ammoManager = GetComponent<AmmoManager>();
            combatSystem = GetComponent<CombatSystem2D>();
            animController = GetComponent<AnimationController2D>();
            weaponVisual = GetComponent<AIWeaponVisualDisplay>();
            
            // 获取或创建通信组件
            communicator = GetComponent<AICommunicator>();
            if (communicator == null)
            {
                communicator = gameObject.AddComponent<AICommunicator>();
                Debug.Log($"[AIController] {name} 创建了新的AICommunicator");
            }
            else
            {
                Debug.Log($"[AIController] {name} 使用现有的AICommunicator");
            }
            
            // 注册死亡事件监听器
            if (aiStats != null && aiStats.OnDeath != null)
            {
                aiStats.OnDeath.AddListener(OnAIDeath);
                Debug.Log($"[AIController] {name} 注册了死亡事件监听器");
            }
            
            // 如果没有AIWeaponVisualDisplay组件，自动添加
            if (weaponVisual == null)
            {
                weaponVisual = gameObject.AddComponent<AIWeaponVisualDisplay>();
                Debug.Log($"[AIController] {name} 添加了AIWeaponVisualDisplay组件");
            }
            
            // 获取或添加物品评估器
            itemEvaluator = GetComponent<AIItemEvaluator>();
            if (itemEvaluator == null)
            {
                itemEvaluator = gameObject.AddComponent<AIItemEvaluator>();
                Debug.Log($"[AIController] 为 {name} 添加了AIItemEvaluator");
            }
            
            // 获取或添加经济系统
            economicSystem = GetComponent<AIEconomicSystem>();
            if (economicSystem == null)
            {
                economicSystem = gameObject.AddComponent<AIEconomicSystem>();
                Debug.Log($"[AIController] 为 {name} 添加了AIEconomicSystem");
            }
            
            // 设置2D刚体
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }
            
            // 设置层
            if (interactableLayer == 0)
            {
                interactableLayer = LayerMask.GetMask("Interactable", "NPC", "Item");
            }
        }
        
        private void OnDestroy()
        {
            // 清理死亡事件监听器
            if (aiStats != null && aiStats.OnDeath != null)
            {
                aiStats.OnDeath.RemoveListener(OnAIDeath);
            }
        }
        
        /// <summary>
        /// 处理AI死亡事件
        /// </summary>
        private void OnAIDeath(AIDeathEventArgs args)
        {
            Debug.Log($"[AIController] {name} 收到死亡事件 - 停止所有行动");
            
            // 立即停止所有移动和行动
            StopMoving();
            currentTarget = null;
            currentDeepSeekDecision = null;
            
            // 停止攻击冷却
            nextAttackTime = 0;
            nextCommunicationTime = 0;
            nextHelpTime = 0;
            
            // 重置所有帧标记
            killedEnemyThisFrame = false;
            collectedItemThisFrame = false;
            reachedPortal = false;
            
            Debug.Log($"[AIController] {name} 已停止所有行动，等待复活");
        }
        
        private void Update()
        {
            // 死亡检查 - 如果AI已死亡，停止所有行动
            if (aiStats != null && aiStats.IsDead)
            {
                StopMoving();
                return;
            }
            
            // 重置帧标记
            killedEnemyThisFrame = false;
            collectedItemThisFrame = false;
            
            // 更新移动
            if (moveDirection != Vector2.zero && !aiStats.IsDead)
            {
                // 直接控制刚体移动
                float currentSpeed = isRunning ? runSpeed : moveSpeed;
                rb.velocity = moveDirection * currentSpeed;
                
                // 更新动画
                if (animController != null)
                {
                    // 动画控制器会自动检测移动
                }
            }
            
            // 定期检查是否需要发送交互机消息（降低频率避免spam）
            if (Time.frameCount % 180 == 0) // 每3秒检查一次
            {
                ConsiderRequestingHelp();
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
                // 继续奔跑
                aiStats.SetMovementState(true);
            }
            else
            {
                isRunning = false;
                aiStats.SetMovementState(moveDirection != Vector2.zero);
            }
        }
        
        public void StopMoving()
        {
            moveDirection = Vector2.zero;
            rb.velocity = Vector2.zero;
            aiStats.SetMovementState(false);
            
            if (animController != null)
            {
                // 动画控制器会自动检测停止
            }
        }
        
        // 设置移动方向
        public void SetMoveDirection(float x, float y)
        {
            Move(new Vector2(x, y));
        }
        
        // 设置移动目标
        public void SetMoveTarget(Vector2 targetPosition)
        {
            // 计算方向并移动
            Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
            Move(direction);
        }
        
        // 追击目标
        public void ChaseToTarget(GameObject target)
        {
            if (target == null || aiStats.IsDead) return;
            
            Vector2 direction = (target.transform.position - transform.position).normalized;
            Move(direction);
        }
        
        // 战斗控制
        public void Attack(GameObject target)
        {
            if (target == null || Time.time < nextAttackTime || aiStats.IsDead) 
            {
                Debug.Log($"[AIController] {name} 攻击失败 - target null: {target == null}, 冷却中: {Time.time < nextAttackTime}, 已死亡: {aiStats.IsDead}");
                return;
            }
            
            var enemy = target.GetComponent<Enemy2D>();
            if (enemy == null || !enemy.IsAlive) 
            {
                Debug.Log($"[AIController] {name} 攻击失败 - enemy组件: {enemy == null}, 敌人已死: {enemy?.IsAlive == false}");
                return;
            }
            
            // 检查距离
            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance > attackRange) 
            {
                Debug.Log($"[AIController] {name} 攻击失败 - 距离太远: {distance} > {attackRange}");
                // 设置追击目标，让AIBrain决定是否追击
                chaseTarget = target;
                return;
            }
            
            // 清除追击目标（已经在攻击范围内）
            if (chaseTarget == target)
            {
                chaseTarget = null;
            }
            
            Debug.Log($"[AIController] {name} 正在攻击 {target.name}, 距离: {distance}");
            
            // 面向目标
            Vector2 direction = (target.transform.position - transform.position).normalized;
            
            // 设置面向
            if (animController != null)
            {
                animController.SetFacing(direction.x > 0);
            }
            
            // 使用战斗系统执行攻击
            if (combatSystem != null)
            {
                combatSystem.PerformAttack();
                
                // 触发武器视觉动画
                if (weaponVisual != null)
                {
                    weaponVisual.PlayAttackAnimation();
                }
            }
            
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
            InteractionPriority highestPriority = InteractionPriority.Low;
            
            foreach (var collider in colliders)
            {
                float distance = Vector2.Distance(transform.position, collider.transform.position);
                InteractionPriority priority = GetInteractionPriority(collider.gameObject);
                
                // 优先级更高或同优先级但更近
                if (priority > highestPriority || (priority == highestPriority && distance < closestDistance))
                {
                    closest = collider.gameObject;
                    closestDistance = distance;
                    highestPriority = priority;
                }
            }
            
            if (closest != null)
            {
                InteractWith(closest);
            }
        }
        
        private InteractionPriority GetInteractionPriority(GameObject obj)
        {
            // 1. 检查是否是拾取物
            var pickup = obj.GetComponent<Loot.UnifiedPickup>();
            if (pickup != null && pickup.PickupItem != null)
            {
                // 评估物品价值
                if (itemEvaluator != null && itemEvaluator.ShouldPickupItem(pickup.PickupItem))
                {
                    var category = itemEvaluator.IdentifyItemCategory(pickup.PickupItem);
                    
                    // 关键物品优先级最高
                    if (category == ItemCategory.Key || pickup.PickupItem.Rarity >= ItemRarity.Epic)
                        return InteractionPriority.Critical;
                    
                    // 急需物品优先级高
                    if (category == itemEvaluator.GetMostNeededItemCategory())
                        return InteractionPriority.High;
                    
                    return InteractionPriority.Medium;
                }
                return InteractionPriority.Low;
            }
            
            // 2. 检查是否是宝箱
            var chest = obj.GetComponent<Interactables.TreasureChest>();
            if (chest != null)
            {
                bool isLocked = chest.IsLocked;
                float chestValue = itemEvaluator?.EvaluateTreasureChest(isLocked) ?? 100f;
                
                if (chestValue > 150f)
                    return InteractionPriority.High;
                else if (chestValue > 0f)
                    return InteractionPriority.Medium;
                else
                    return InteractionPriority.None; // 锁着且没钥匙
            }
            
            // 3. NPC根据需求判断
            var npc = obj.GetComponent<NPCBase>();
            if (npc != null && npc.CanInteract(gameObject))
            {
                switch (npc.NPCType)
                {
                    case NPCType.Doctor:
                        if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f)
                            return InteractionPriority.High;
                        break;
                    case NPCType.Restaurant:
                        if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f ||
                            aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
                            return InteractionPriority.High;
                        break;
                    case NPCType.Merchant:
                        if (itemEvaluator?.GetMostNeededItemCategory() != ItemCategory.Other)
                            return InteractionPriority.Medium;
                        break;
                }
                return InteractionPriority.Medium;
            }
            
            // 4. 其他交互物（泉水、传送门等）
            if (obj.GetComponent<IInteractable>() != null)
            {
                if (obj.name.Contains("Portal") || obj.name.Contains("Teleporter"))
                    return InteractionPriority.Critical;
                    
                if (obj.name.Contains("Fountain") && aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.5f)
                    return InteractionPriority.High;
                    
                return InteractionPriority.Medium;
            }
            
            return InteractionPriority.None;
        }
        
        private void InteractWith(GameObject target)
        {
            // 1. 拾取物交互（优先处理）
            var pickup = target.GetComponent<Loot.UnifiedPickup>();
            if (pickup != null && pickup.PickupItem != null)
            {
                // 尝试拾取
                if (pickup.CanPickup && pickup.RequiresInteraction)
                {
                    // 手动拾取的物品
                    if (itemEvaluator != null && itemEvaluator.ShouldPickupItem(pickup.PickupItem))
                    {
                        pickup.Pickup(gameObject);
                        collectedItemThisFrame = true;
                        Debug.Log($"[AIController] {name} 拾取了 {pickup.PickupItem.ItemName}");
                    }
                }
                return;
            }
            
            // 2. 宝箱交互
            var chest = target.GetComponent<Interactables.TreasureChest>();
            if (chest != null)
            {
                chest.Interact(gameObject);
                Debug.Log($"[AIController] {name} 开启了宝箱");
                return;
            }
            
            // 3. NPC交互
            var npc = target.GetComponent<NPCBase>();
            if (npc != null)
            {
                // 通知其他AI发现了NPC（根据NPC类型发送具体消息）
                AnnounceFindNPC(npc);
                
                npc.StartInteraction(gameObject);
                StartCoroutine(HandleNPCInteraction(npc));
                return;
            }
            
            // 4. 其他交互物
            var interactable = target.GetComponent<IInteractable>();
            if (interactable != null)
            {
                // 检查是否是泉水
                if (target.name.Contains("Fountain") || target.GetComponent<Interactables.Fountain>() != null)
                {
                    // 通知其他AI发现了水源
                    SendCommunication(CommunicationType.FoundWater);
                }
                
                interactable.Interact(gameObject);
                
                // 检查是否是传送门
                if (target.name.Contains("Portal") || target.name.Contains("Teleporter"))
                {
                    reachedPortal = true;
                    // 通知其他AI传送门位置
                    SendCommunication(CommunicationType.FoundPortal);
                    // 尝试激活传送装置
                    var teleporter = target.GetComponent<Interactables.TeleportDevice>();
                    if (teleporter != null)
                    {
                        teleporter.TryActivate(gameObject);
                    }
                }
            }
        }
        
        private IEnumerator HandleNPCInteraction(NPCBase npc)
        {
            // AI与NPC的自动交互逻辑 - 不阻塞
            yield return null; // 立即返回，不等待
            
            // 使用DeepSeek决策是否交易
            if (useDeepSeekForTrade)
            {
                RequestTradeDecision(npc);
            }
            else
            {
                // 使用默认逻辑
                bool interactionComplete = false;
                switch (npc.NPCType)
                {
                    case NPCType.Merchant:
                        interactionComplete = HandleMerchantInteraction(npc);
                        break;
                    case NPCType.Doctor:
                        interactionComplete = HandleDoctorInteraction(npc);
                        break;
                    case NPCType.Restaurant:
                        interactionComplete = HandleRestaurantInteraction(npc);
                        break;
                    case NPCType.Blacksmith:
                        interactionComplete = HandleBlacksmithInteraction(npc);
                        break;
                    case NPCType.Tailor:
                        interactionComplete = HandleTailorInteraction(npc);
                        break;
                }
                
                // 如果交互完成，结束交互
                if (interactionComplete)
                {
                    npc.EndInteraction();
                }
            }
        }
        
        private bool HandleMerchantInteraction(NPCBase merchant)
        {
            // 使用经济系统进行智能决策
            if (economicSystem == null) 
            {
                // 回退到简单逻辑
                return HandleMerchantInteractionFallback(merchant);
            }
            
            var merchantNPC = merchant as NPC.Types.MerchantNPC;
            if (merchantNPC == null) return true;
            
            // 1. 创建购物清单
            var shoppingList = economicSystem.CreateShoppingList(merchant);
            
            // 2. 检查是否需要出售物品来筹钱
            if (shoppingList.TotalCost > currencyManager.CurrentGold)
            {
                var itemsToSell = economicSystem.CreateSellingList(merchant, shoppingList.TotalCost - currencyManager.CurrentGold);
                
                // 执行出售
                foreach (var item in itemsToSell)
                {
                    merchantNPC.HandleAIRequest($"sell_{item.ItemName}", gameObject);
                    economicSystem.RecordTransaction(TransactionType.Sell, item, 1, item.BuyPrice * 0.5f, merchant);
                }
            }
            
            // 3. 执行购买
            foreach (var shoppingItem in shoppingList.Items)
            {
                string requestType = DetermineRequestType(shoppingItem.Item);
                for (int i = 0; i < shoppingItem.Quantity; i++)
                {
                    merchantNPC.HandleAIRequest(requestType, gameObject);
                }
                economicSystem.RecordTransaction(TransactionType.Buy, shoppingItem.Item, shoppingItem.Quantity, shoppingItem.PricePerUnit, merchant);
            }
            
            return shoppingList.Items.Count > 0;
        }
        
        private bool HandleMerchantInteractionFallback(NPCBase merchant)
        {
            // 原始的简单逻辑作为后备
            var merchantNPC = merchant as NPC.Types.MerchantNPC;
            if (merchantNPC == null) return true;
            
            if (currencyManager != null && currencyManager.CurrentGold > 50)
            {
                if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f)
                {
                    merchantNPC.HandleAIRequest("buy_health_potion", gameObject);
                    return true;
                }
                if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f)
                {
                    merchantNPC.HandleAIRequest("buy_food", gameObject);
                    return true;
                }
                if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
                {
                    merchantNPC.HandleAIRequest("buy_water", gameObject);
                    return true;
                }
            }
            return false;
        }
        
        private string DetermineRequestType(ItemBase item)
        {
            var category = itemEvaluator?.IdentifyItemCategory(item) ?? ItemCategory.Other;
            
            switch (category)
            {
                case ItemCategory.Health:
                    return "buy_health_potion";
                case ItemCategory.Food:
                    return "buy_food";
                case ItemCategory.Water:
                    return "buy_water";
                case ItemCategory.Weapon:
                    return "buy_weapon";
                case ItemCategory.Key:
                    return "buy_key";
                default:
                    return "buy_item";
            }
        }
        
        private bool HandleDoctorInteraction(NPCBase doctor)
        {
            var doctorNPC = doctor as NPC.Types.DoctorNPC;
            if (doctorNPC == null) return true;
            
            // 使用经济系统评估
            if (economicSystem != null)
            {
                // 评估治疗服务
                float healingPrice = 100f; // 假设基础治疗价格
                if (economicSystem.ShouldUseService(ServiceType.Healing, healingPrice))
                {
                    doctorNPC.HandleAIRequest("heal", gameObject);
                    economicSystem.RecordTransaction(TransactionType.Service, null, 1, healingPrice, doctor);
                    return true;
                }
                
                // 考虑购买药品
                var shoppingList = economicSystem.CreateShoppingList(doctor);
                foreach (var item in shoppingList.Items)
                {
                    doctorNPC.HandleAIRequest($"buy_{item.Item.ItemName}", gameObject);
                    economicSystem.RecordTransaction(TransactionType.Buy, item.Item, item.Quantity, item.PricePerUnit, doctor);
                }
                
                return shoppingList.Items.Count > 0;
            }
            
            // 后备逻辑
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f && currencyManager?.CurrentGold > 50)
            {
                doctorNPC.HandleAIRequest("heal", gameObject);
                return true;
            }
            return false;
        }
        
        private bool HandleRestaurantInteraction(NPCBase restaurant)
        {
            var restaurantNPC = restaurant as NPC.Types.RestaurantNPC;
            if (restaurantNPC == null) return true;
            
            bool actionTaken = false;
            
            // 使用经济系统评估
            if (economicSystem != null)
            {
                // 免费水服务
                if (economicSystem.ShouldUseService(ServiceType.Water, 0f))
                {
                    restaurantNPC.HandleAIRequest("water", gameObject);
                    actionTaken = true;
                }
                
                // 付费食物服务
                float foodPrice = 20f; // 假设基础食物价格
                if (economicSystem.ShouldUseService(ServiceType.Food, foodPrice))
                {
                    restaurantNPC.HandleAIRequest("food", gameObject);
                    economicSystem.RecordTransaction(TransactionType.Service, null, 1, foodPrice, restaurant);
                    actionTaken = true;
                }
            }
            else
            {
                // 后备逻辑
                if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
                {
                    restaurantNPC.HandleAIRequest("water", gameObject);
                    actionTaken = true;
                }
                else if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f && currencyManager?.CurrentGold > 10)
                {
                    restaurantNPC.HandleAIRequest("food", gameObject);
                    actionTaken = true;
                }
            }
            
            return actionTaken;
        }
        
        private bool HandleBlacksmithInteraction(NPCBase blacksmith)
        {
            var blacksmithNPC = blacksmith as NPC.Types.BlacksmithNPC;
            if (blacksmithNPC == null) return true;
            
            // 使用经济系统评估
            if (economicSystem != null)
            {
                // 武器升级决策
                float upgradePrice = 200f; // 假设基础升级价格
                if (inventory?.EquippedWeapon != null && 
                    economicSystem.ShouldUseService(ServiceType.WeaponUpgrade, upgradePrice))
                {
                    blacksmithNPC.HandleAIRequest("upgrade_weapon", gameObject);
                    economicSystem.RecordTransaction(TransactionType.Service, null, 1, upgradePrice, blacksmith);
                    return true;
                }
                
                // 考虑购买新武器
                var shoppingList = economicSystem.CreateShoppingList(blacksmith);
                foreach (var item in shoppingList.Items)
                {
                    if (item.Item is WeaponItem)
                    {
                        blacksmithNPC.HandleAIRequest($"buy_{item.Item.ItemName}", gameObject);
                        economicSystem.RecordTransaction(TransactionType.Buy, item.Item, 1, item.PricePerUnit, blacksmith);
                        return true;
                    }
                }
            }
            else
            {
                // 后备逻辑
                if (inventory?.EquippedWeapon != null && currencyManager?.CurrentGold > 200)
                {
                    blacksmithNPC.HandleAIRequest("upgrade_weapon", gameObject);
                    return true;
                }
            }
            return false;
        }
        
        private bool HandleTailorInteraction(NPCBase tailor)
        {
            var tailorNPC = tailor as NPC.Types.TailorNPC;
            if (tailorNPC == null) return true;
            
            if (inventory == null) return false;
            
            // 使用经济系统评估
            if (economicSystem != null)
            {
                float expansionPrice = 500f; // 假设扩容价格
                if (economicSystem.ShouldUseService(ServiceType.BagExpansion, expansionPrice))
                {
                    tailorNPC.HandleAIRequest("expand_bag", gameObject);
                    economicSystem.RecordTransaction(TransactionType.Service, null, 1, expansionPrice, tailor);
                    return true;
                }
            }
            else
            {
                // 后备逻辑：计算背包使用率
                int usedSlots = 0;
                for (int i = 0; i < inventory.Size; i++)
                {
                    if (!inventory.GetSlot(i).IsEmpty) usedSlots++;
                }
                
                if (usedSlots > inventory.Size * 0.8f && currencyManager?.CurrentGold > 500)
                {
                    tailorNPC.HandleAIRequest("expand_bag", gameObject);
                    return true;
                }
            }
            return false;
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
        
        // 智能武器选择
        public void SelectBestWeapon(GameObject target = null)
        {
            if (inventory == null || aiStats.IsDead) return;
            
            WeaponItem bestWeapon = null;
            float bestScore = -1f;
            
            // 检查所有背包中的武器
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot.IsEmpty) continue;
                
                var weapon = slot.Item as WeaponItem;
                if (weapon == null) continue;
                
                float score = EvaluateWeapon(weapon, target);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestWeapon = weapon;
                }
            }
            
            // 装备最佳武器
            if (bestWeapon != null && inventory.EquippedWeapon != bestWeapon)
            {
                inventory.EquipWeapon(bestWeapon);
                Debug.Log($"[AI] {name} 切换到武器: {bestWeapon.ItemName}");
            }
        }
        
        private float EvaluateWeapon(WeaponItem weapon, GameObject target)
        {
            float score = 0f;
            
            // 基础伤害分数
            score += weapon.Damage * 0.5f;
            
            // 攻击速度分数
            score += weapon.AttackSpeed * 10f;
            
            // 根据目标距离评估武器
            if (target != null)
            {
                float distance = Vector2.Distance(transform.position, target.transform.position);
                
                if (weapon.WeaponType == WeaponType.Ranged)
                {
                    // 远程武器在远距离加分
                    if (distance > 5f)
                    {
                        score += 30f;
                    }
                    // 检查弹药
                    if (ammoManager != null && weapon.RequiredAmmo != AmmoType.None)
                    {
                        int ammoCount = ammoManager.GetAmmo(weapon.RequiredAmmo);
                        if (ammoCount == 0)
                        {
                            score = 0f; // 没弹药就不用了
                        }
                        else
                        {
                            score += Mathf.Min(ammoCount, 20); // 弹药充足加分
                        }
                    }
                }
                else if (weapon.WeaponType == WeaponType.Melee)
                {
                    // 近战武器在近距离加分
                    if (distance < 3f)
                    {
                        score += 20f;
                    }
                    // 近战武器的攻击范围加分
                    score += weapon.AttackRange * 5f;
                }
                else if (weapon.WeaponType == WeaponType.Magic)
                {
                    // 魔法武器在中距离加分
                    if (distance > 3f && distance < 8f)
                    {
                        score += 25f;
                    }
                    // 范围攻击加分
                    if (weapon.AttackShape != AttackShape.Line)
                    {
                        score += 15f;
                    }
                }
            }
            
            // 特殊效果加分
            if (weapon.OnHitDebuffs != null && weapon.OnHitDebuffs.Count > 0)
            {
                score += 10f * weapon.OnHitDebuffs.Count;
            }
            
            return score;
        }
        
        // 通信控制
        public void SendCommunication(CommunicationType type)
        {
            if (aiStats.IsDead) return;
            
            // Help请求有特殊的冷却时间
            if (type == CommunicationType.Help)
            {
                if (Time.time < nextHelpTime) 
                {
                    // 减少日志spam - 每5秒最多输出一次冷却提示
                    if (Time.frameCount % 300 == 0)
                    {
                        Debug.Log($"[AIController] {name} Help冷却中，剩余: {nextHelpTime - Time.time:F0}秒");
                    }
                    return;
                }
                nextHelpTime = Time.time + helpCooldown;
                Debug.Log($"[AIController] {name} 发送Help请求（{helpCooldown}秒冷却）");
            }
            else
            {
                if (Time.time < nextCommunicationTime) return;
                nextCommunicationTime = Time.time + communicationCooldown;
            }
            
            communicator.SendMessage(type, transform.position);
        }
        
        // 面对面交流
        public void TryFaceToFaceInteraction()
        {
            if (Time.time < lastFaceToFaceTime + faceToFaceCooldown || aiStats.IsDead) return;
            
            // 寻找附近的其他AI
            var nearbyAIs = Physics2D.OverlapCircleAll(transform.position, faceToFaceRange)
                .Select(c => c.GetComponent<AIStats>())
                .Where(ai => ai != null && ai != aiStats && !ai.IsDead)
                .ToArray();
                
            if (nearbyAIs.Length > 0)
            {
                // 选择最近的AI进行交流
                var targetAI = nearbyAIs.OrderBy(ai => Vector2.Distance(transform.position, ai.transform.position)).First();
                
                // 互相面对
                Vector2 direction = (targetAI.transform.position - transform.position).normalized;
                if (animController != null)
                {
                    animController.SetFacing(direction.x > 0);
                }
                
                // 触发面对面交流
                aiStats.TriggerFaceToFaceInteraction(true); // 说话方
                targetAI.TriggerFaceToFaceInteraction(false); // 倾听方
                
                // 发送房间内声音
                SendRoomSound($"{name} 正在与 {targetAI.name} 交谈");
                
                lastFaceToFaceTime = Time.time;
                Debug.Log($"[AI] {name} 与 {targetAI.name} 进行了面对面交流");
            }
        }
        
        // 房间内声音系统
        private void SendRoomSound(string message)
        {
            // 获取当前房间
            var mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator == null) return;
            
            var currentRoom = mapGenerator.GetCurrentRoom(transform.position);
            if (currentRoom == null) return;
            
            // 通知同房间内的所有AI
            var roomBounds = currentRoom.GetComponent<Collider2D>()?.bounds ?? new Bounds(currentRoom.transform.position, Vector3.one * 16f);
            var aisInRoom = Physics2D.OverlapBoxAll(roomBounds.center, roomBounds.size, 0f)
                .Select(c => c.GetComponent<AIStats>())
                .Where(ai => ai != null && ai != aiStats)
                .ToArray();
                
            foreach (var ai in aisInRoom)
            {
                // AI听到声音，轻微改善心情
                ai.TriggerCommunicatorInteraction();
                Debug.Log($"[AI] {ai.name} 听到了: {message}");
            }
        }
        
        // 尝试切换到最佳武器
        public void TrySwitchBestWeapon()
        {
            SelectBestWeapon(currentTarget);
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
                // 尝试拾取物品
                var pickup = other.GetComponent<Loot.UnifiedPickup>();
                if (pickup != null)
                {
                    pickup.Interact(gameObject);
                    collectedItemThisFrame = true;
                }
            }
        }
        
        // 接收DeepSeek决策
        public void ApplyDeepSeekDecision(AIDecision decision)
        {
            if (decision == null) return;
            
            currentDeepSeekDecision = decision;
            lastDecisionTime = Time.time;
            currentPriority = decision.Priority;
            
            Debug.Log($"[AIController] 应用DeepSeek决策: {decision.RecommendedState} - {decision.Explanation}");
            
            // 根据决策状态执行不同行为
            ExecuteDecisionActions(decision);
        }
        
        private void ExecuteDecisionActions(AIDecision decision)
        {
            var brain = GetComponent<AIBrain>();
            if (brain == null) return;
            
            switch (decision.RecommendedState)
            {
                case AIState.Critical:
                    // 危急状态 - 紧急求救并寻找补给
                    // 智能判断是否真的需要求救
                    if (ShouldRequestHelp())
                    {
                        SendCommunication(CommunicationType.Help);
                    }
                    
                    // 使用恢复物品
                    UseHealthItems();
                    UseFoodItems();
                    UseWaterItems();
                    break;
                    
                case AIState.Fleeing:
                    // 逃跑状态 - 远离威胁
                    if (currentTarget != null && currentTarget.CompareTag("Enemy"))
                    {
                        Vector2 awayDirection = (transform.position - currentTarget.transform.position).normalized;
                        SetMoveDirection(awayDirection.x, awayDirection.y);
                        currentPriority = AIActionPriority.Survival;
                    }
                    break;
                    
                case AIState.Seeking:
                    // 寻找资源 - 主动寻找NPC和补给
                    // 优先寻找记忆中的资源点
                    var communicator = GetComponent<AICommunicator>();
                    if (communicator != null)
                    {
                        // 检查是否有AI请求帮助（优先级最高）
                        var comeHereMsg = communicator.GetLatestMessage(CommunicationType.ComeHere);
                        if (comeHereMsg != null)
                        {
                            Debug.Log($"[AIController] {name} 响应ComeHere请求，前往 {comeHereMsg.Position}");
                            SetMoveTarget(comeHereMsg.Position);
                        }
                        // 检查最近的水源消息
                        else if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.4f)
                        {
                            var waterMsg = communicator.GetLatestMessage(CommunicationType.FoundWater);
                            if (waterMsg != null)
                            {
                                Debug.Log($"[AIController] {name} 前往水源位置 {waterMsg.Position}");
                                SetMoveTarget(waterMsg.Position);
                            }
                        }
                        // 检查商人消息
                        else
                        {
                            var npcMsg = communicator.GetLatestMessage(CommunicationType.FoundNPC);
                            if (npcMsg != null)
                            {
                                Debug.Log($"[AIController] {name} 前往NPC位置 {npcMsg.Position}");
                                SetMoveTarget(npcMsg.Position);
                            }
                        }
                    }
                    break;
                    
                case AIState.Communicating:
                    // 社交状态 - 主动寻找其他AI交流
                    communicator = GetComponent<AICommunicator>();
                    if (communicator != null)
                    {
                        var nearbyAIs = communicator.GetNearbyAIsForTalk();
                        if (nearbyAIs.Count > 0)
                        {
                            // 向最近的AI移动
                            SetMoveTarget(nearbyAIs[0].transform.position);
                            // 尝试面对面交流
                            communicator.TryFaceToFaceTalk();
                        }
                    }
                    break;
                    
                case AIState.Fighting:
                    // 战斗状态 - 积极进攻
                    if (currentTarget != null && currentTarget.CompareTag("Enemy"))
                    {
                        // 选择最佳武器
                        TrySwitchBestWeapon();
                        // 保持攻击距离
                        float idealDistance = GetIdealCombatDistance();
                        float currentDistance = Vector2.Distance(transform.position, currentTarget.transform.position);
                        
                        if (currentDistance > idealDistance)
                        {
                            // 靠近目标
                            Vector2 toTarget = (currentTarget.transform.position - transform.position).normalized;
                            SetMoveDirection(toTarget.x, toTarget.y);
                        }
                        else if (currentDistance < idealDistance * 0.7f)
                        {
                            // 保持距离
                            Vector2 awayFromTarget = (transform.position - currentTarget.transform.position).normalized;
                            SetMoveDirection(awayFromTarget.x * 0.5f, awayFromTarget.y * 0.5f);
                        }
                    }
                    break;
                    
                case AIState.Resting:
                    // 休息状态 - 停止移动，恢复体力
                    SetMoveDirection(0, 0);
                    // 可以在安全的地方休息
                    break;
                    
                case AIState.Exploring:
                    // 探索状态 - 继续正常探索
                    // 由ML-Agents控制
                    break;
            }
            
            // 执行具体行动建议
            if (decision.SpecificActions != null)
            {
                foreach (var action in decision.SpecificActions)
                {
                    ExecuteSpecificAction(action);
                }
            }
        }
        
        private void ExecuteSpecificAction(string action)
        {
            // 根据具体行动建议执行
            if (action.Contains("使用") || action.Contains("恢复"))
            {
                UseHealthItems();
                UseFoodItems();
                UseWaterItems();
            }
            else if (action.Contains("求救"))
            {
                // 智能判断是否真的需要求救
                if (ShouldRequestHelp())
                {
                    SendCommunication(CommunicationType.Help);
                }
            }
            else if (action.Contains("交流"))
            {
                var communicator = GetComponent<AICommunicator>();
                if (communicator != null)
                {
                    communicator.TryFaceToFaceTalk();
                }
            }
            else if (action.Contains("商人") || action.Contains("NPC"))
            {
                // 寻找NPC
                var communicator = GetComponent<AICommunicator>();
                if (communicator != null)
                {
                    var npcMsg = communicator.GetLatestMessage(CommunicationType.FoundNPC);
                    if (npcMsg != null)
                    {
                        SetMoveTarget(npcMsg.Position);
                    }
                }
            }
        }
        
        private float GetIdealCombatDistance()
        {
            var weapon = inventory.EquippedWeapon;
            if (weapon != null)
            {
                switch (weapon.WeaponType)
                {
                    case WeaponType.Melee:
                        return weapon.AttackRange * 0.8f;
                    case WeaponType.Ranged:
                        return 6f;
                    case WeaponType.Magic:
                        return 4f;
                }
            }
            return 2f;
        }
        
        // 使用恢复物品
        private void UseHealthItems()
        {
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty && slot.Item is ConsumableItem consumable)
                {
                    if (consumable.ItemName.Contains("生命") || consumable.ItemName.Contains("Health"))
                    {
                        inventory.UseItem(i);
                        break;
                    }
                }
            }
        }
        
        private void UseFoodItems()
        {
            if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.5f)
            {
                for (int i = 0; i < inventory.Size; i++)
                {
                    var slot = inventory.GetSlot(i);
                    if (!slot.IsEmpty && slot.Item is ConsumableItem consumable)
                    {
                        if (consumable.ItemName.Contains("食") || consumable.ItemName.Contains("Food"))
                        {
                            inventory.UseItem(i);
                            break;
                        }
                    }
                }
            }
        }
        
        private void UseWaterItems()
        {
            if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.5f)
            {
                for (int i = 0; i < inventory.Size; i++)
                {
                    var slot = inventory.GetSlot(i);
                    if (!slot.IsEmpty && slot.Item is ConsumableItem consumable)
                    {
                        if (consumable.ItemName.Contains("水") || consumable.ItemName.Contains("Water"))
                        {
                            inventory.UseItem(i);
                            break;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 智能判断是否应该请求帮助
        /// </summary>
        private bool ShouldRequestHelp()
        {
            // 基础状态检查
            float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerPercent = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstPercent = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            float staminaPercent = aiStats.CurrentStamina / aiStats.Config.maxStamina;
            
            // 检查附近敌人威胁
            var perception = GetComponent<AI.Perception.AIPerception>();
            var nearbyEnemies = perception?.GetNearbyEnemies() ?? new List<Enemy.Enemy2D>();
            
            // 评估自身战斗能力
            bool hasWeapon = inventory?.EquippedWeapon != null;
            bool hasAmmo = true;
            if (hasWeapon && inventory.EquippedWeapon.RequiredAmmo != Inventory.AmmoType.None)
            {
                hasAmmo = ammoManager != null && ammoManager.GetAmmo(inventory.EquippedWeapon.RequiredAmmo) > 0;
            }
            
            // 计算真正的威胁
            int seriousThreats = 0;
            foreach (var enemy in nearbyEnemies)
            {
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < 3f && enemy.IsAlive) // 3单位内的活敌人算严重威胁
                {
                    seriousThreats++;
                }
            }
            
            // 判断求救条件（必须同时满足危急状态和真正的威胁）
            bool criticalHealth = healthPercent < 0.15f;
            bool criticalResources = hungerPercent < 0.1f || thirstPercent < 0.1f;
            bool noMeansToFight = (!hasWeapon || !hasAmmo) && seriousThreats > 0;
            bool overwhelmed = seriousThreats >= 3 && (healthPercent < 0.3f || staminaPercent < 0.2f);
            bool desperateSituation = criticalHealth && seriousThreats >= 1;
            
            bool shouldHelp = criticalResources || noMeansToFight || overwhelmed || desperateSituation;
            
            if (shouldHelp)
            {
                Debug.Log($"[AIController] {name} 请求帮助 - 生命:{healthPercent:P0}, 威胁:{seriousThreats}, 武器:{hasWeapon}, 弹药:{hasAmmo}");
            }
            
            return shouldHelp;
        }
        
        /// <summary>
        /// 通知其他AI发现了NPC
        /// </summary>
        private void AnnounceFindNPC(NPCBase npc)
        {
            if (npc == null) return;
            
            // 根据NPC类型发送具体的发现消息
            switch (npc.NPCType)
            {
                case NPCType.Merchant:
                    Debug.Log($"[AIController] {name} 发现了商人！");
                    break;
                case NPCType.Blacksmith:
                    Debug.Log($"[AIController] {name} 发现了铁匠！");
                    break;
                case NPCType.Doctor:
                    Debug.Log($"[AIController] {name} 发现了医生！");
                    break;
                case NPCType.Restaurant:
                    Debug.Log($"[AIController] {name} 发现了餐馆！");
                    break;
                case NPCType.Tailor:
                    Debug.Log($"[AIController] {name} 发现了裁缝！");
                    break;
            }
            
            // 发送通用的发现NPC消息
            SendCommunication(CommunicationType.FoundNPC);
        }
        
        /// <summary>
        /// 请求其他AI到这里来（在需要协助时使用）
        /// </summary>
        public void RequestAIComeHere()
        {
            Debug.Log($"[AIController] {name} 请求其他AI到这里来");
            SendCommunication(CommunicationType.ComeHere);
        }
        
        /// <summary>
        /// 通知其他AI我要去某个地方
        /// </summary>
        public void AnnounceGoingTo(Vector2 destination)
        {
            Debug.Log($"[AIController] {name} 通知要前往 {destination}");
            SendCommunication(CommunicationType.GoingTo);
        }
        
        /// <summary>
        /// 智能决定是否需要发送交互机消息
        /// </summary>
        private void ConsiderRequestingHelp()
        {
            // 在以下情况考虑请求其他AI来帮助：
            // 1. 发现了重要资源但自己状态不好
            // 2. 发现了传送门需要4人激活
            // 3. 面临困难需要支援
            
            if (reachedPortal)
            {
                // 发现传送门，请求其他AI过来
                RequestAIComeHere();
            }
            else if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.3f)
            {
                // 健康状态不佳，可能需要支援
                var perception = GetComponent<AI.Perception.AIPerception>();
                var nearbyEnemies = perception?.GetNearbyEnemies();
                if (nearbyEnemies != null && nearbyEnemies.Count >= 2)
                {
                    RequestAIComeHere();
                }
            }
            
            // 检查是否需要通知其他AI我要去某个地方
            ConsiderAnnounceMovement();
        }
        
        /// <summary>
        /// 考虑是否需要通知其他AI我的行动计划
        /// </summary>
        private void ConsiderAnnounceMovement()
        {
            var communicator = GetComponent<AICommunicator>();
            if (communicator == null) return;
            
            // 检查是否有重要的目标需要通知
            // 1. 前往传送门
            var portalMsg = communicator.GetLatestMessage(CommunicationType.FoundPortal);
            if (portalMsg != null && Vector2.Distance(transform.position, portalMsg.Position) > 8f)
            {
                // 距离传送门较远，通知其他AI我要去传送门
                AnnounceGoingTo(portalMsg.Position);
                return;
            }
            
            // 2. 前往重要NPC（当自己状态需要时）
            bool needsHealing = aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f;
            bool needsFood = aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f;
            bool needsWater = aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f;
            
            if (needsHealing || needsFood || needsWater)
            {
                var npcMsg = communicator.GetLatestMessage(CommunicationType.FoundNPC);
                if (npcMsg != null && Vector2.Distance(transform.position, npcMsg.Position) > 5f)
                {
                    // 距离NPC较远，通知其他AI我要去NPC那里
                    AnnounceGoingTo(npcMsg.Position);
                }
            }
        }
        
        /// <summary>
        /// 请求DeepSeek进行交易决策
        /// </summary>
        private void RequestTradeDecision(NPCBase npc)
        {
            // 构建交易上下文
            var context = new AI.Decision.AITradeContext
            {
                NPCType = npc.NPCType.ToString(),
                NPCTypeEnum = npc.NPCType,
                CurrentGold = currencyManager?.CurrentGold ?? 0,
                CurrentHealth = aiStats.CurrentHealth,
                MaxHealth = aiStats.Config.maxHealth,
                CurrentHunger = aiStats.CurrentHunger,
                MaxHunger = aiStats.Config.maxHunger,
                CurrentThirst = aiStats.CurrentThirst,
                MaxThirst = aiStats.Config.maxThirst,
                EmotionMood = aiStats.GetMood(MoodDimension.Emotion),
                SocialMood = aiStats.GetMood(MoodDimension.Social),
                MentalityMood = aiStats.GetMood(MoodDimension.Mentality),
                InventoryCapacity = inventory.Size,
                UsedSlots = 0,
                InventoryItems = new List<AI.Decision.ItemInfo>(),
                Services = new List<AI.Decision.ServiceInfo>()
            };
            
            // 填充背包物品
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    context.UsedSlots++;
                    context.InventoryItems.Add(new AI.Decision.ItemInfo
                    {
                        ItemName = slot.Item.ItemName,
                        Quantity = slot.Quantity,
                        BasePrice = slot.Item.SellPrice, // 使用卖出价格
                        ItemType = slot.Item.GetType().Name
                    });
                }
            }
            
            // 根据NPC类型填充商店物品或服务
            switch (npc.NPCType)
            {
                case NPCType.Merchant:
                    // TODO: 获取商人物品列表
                    break;
                    
                case NPCType.Doctor:
                    context.Services.Add(new AI.Decision.ServiceInfo
                    {
                        Name = "治疗",
                        Price = 50,
                        Description = "恢复生命值到满",
                        Effect = "生命值+100%"
                    });
                    break;
                    
                case NPCType.Restaurant:
                    context.Services.Add(new AI.Decision.ServiceInfo
                    {
                        Name = "免费水",
                        Price = 0,
                        Description = "免费提供饮用水",
                        Effect = "口渴度+50"
                    });
                    context.Services.Add(new AI.Decision.ServiceInfo
                    {
                        Name = "简单套餐",
                        Price = 20,
                        Description = "基础食物",
                        Effect = "饥饿度+60"
                    });
                    break;
            }
            
            // 调用DeepSeek API
            AI.Decision.DeepSeekAPIClient.Instance.RequestTradeDecision(context, (decision) =>
            {
                ExecuteTradeDecision(npc, decision);
            });
        }
        
        /// <summary>
        /// 执行交易决策
        /// </summary>
        private void ExecuteTradeDecision(NPCBase npc, AI.Decision.AITradeDecision decision)
        {
            Debug.Log($"[AIController] 执行交易决策: {decision.TradeType} - {decision.ItemOrServiceName} - {decision.Reasoning}");
            
            if (!decision.ShouldTrade)
            {
                npc.EndInteraction();
                return;
            }
            
            // 根据决策类型执行
            switch (decision.TradeType)
            {
                case AI.Decision.TradeType.Buy:
                    if (npc is NPC.Types.MerchantNPC merchant)
                    {
                        merchant.HandleAIRequest($"buy_{decision.ItemOrServiceName}", gameObject);
                    }
                    break;
                    
                case AI.Decision.TradeType.Sell:
                    if (npc is NPC.Types.MerchantNPC merchant2)
                    {
                        merchant2.HandleAIRequest($"sell_{decision.ItemOrServiceName}", gameObject);
                    }
                    break;
                    
                case AI.Decision.TradeType.Service:
                    // 根据NPC类型调用具体的HandleAIRequest
                    switch (npc.NPCType)
                    {
                        case NPCType.Doctor:
                            if (npc is NPC.Types.DoctorNPC doctor)
                                doctor.HandleAIRequest(decision.ItemOrServiceName.ToLower(), gameObject);
                            break;
                        case NPCType.Restaurant:
                            if (npc is NPC.Types.RestaurantNPC restaurant)
                                restaurant.HandleAIRequest(decision.ItemOrServiceName.ToLower(), gameObject);
                            break;
                        case NPCType.Blacksmith:
                            if (npc is NPC.Types.BlacksmithNPC blacksmith)
                                blacksmith.HandleAIRequest(decision.ItemOrServiceName.ToLower(), gameObject);
                            break;
                        case NPCType.Tailor:
                            if (npc is NPC.Types.TailorNPC tailor)
                                tailor.HandleAIRequest(decision.ItemOrServiceName.ToLower(), gameObject);
                            break;
                    }
                    break;
            }
            
            // 交易完成
            npc.EndInteraction();
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
        
        // Public getters
        public GameObject GetCurrentTarget() => currentTarget;
    }
    
    public enum AIActionPriority
    {
        Normal,
        Survival,
        Combat,
        Exploration
    }
}