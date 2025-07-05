using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Decision;
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
        [SerializeField] private float attackRange = 1.5f;
        
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
        
        // DeepSeek决策
        private AIDecision currentDeepSeekDecision;
        private float lastDecisionTime = -10f;
        
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
            
            // 创建通信组件
            communicator = gameObject.AddComponent<AICommunicator>();
            
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
        
        private void Update()
        {
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
            
            // 设置面向
            if (animController != null)
            {
                animController.SetFacing(direction.x > 0);
            }
            
            // 使用战斗系统执行攻击
            if (combatSystem != null)
            {
                combatSystem.PerformAttack();
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
                // 通知其他AI发现了NPC（根据NPC类型发送具体消息）
                AnnounceFindNPC(npc);
                
                npc.StartInteraction(gameObject);
                StartCoroutine(HandleNPCInteraction(npc));
                return;
            }
            
            // 其他交互
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
            
            // 根据NPC类型执行不同的交互
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
        
        private bool HandleMerchantInteraction(NPCBase merchant)
        {
            // AI购买决策
            var merchantNPC = merchant as NPC.Types.MerchantNPC;
            if (merchantNPC == null) return true;
            
            // 检查是否需要购买补给品
            if (currencyManager != null && currencyManager.CurrentGold > 50)
            {
                // 需要补充生命
                if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f)
                {
                    merchantNPC.HandleAIRequest("buy_health_potion", gameObject);
                    return true;
                }
                // 需要补充食物
                if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f)
                {
                    merchantNPC.HandleAIRequest("buy_food", gameObject);
                    return true;
                }
                // 需要补充水
                if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
                {
                    merchantNPC.HandleAIRequest("buy_water", gameObject);
                    return true;
                }
            }
            return false;
        }
        
        private bool HandleDoctorInteraction(NPCBase doctor)
        {
            // AI治疗决策
            var doctorNPC = doctor as NPC.Types.DoctorNPC;
            if (doctorNPC == null) return true;
            
            if (aiStats.CurrentHealth < aiStats.Config.maxHealth * 0.5f && currencyManager != null && currencyManager.CurrentGold > 50)
            {
                doctorNPC.HandleAIRequest("heal", gameObject);
                return true;
            }
            return false;
        }
        
        private bool HandleRestaurantInteraction(NPCBase restaurant)
        {
            // AI进食决策
            var restaurantNPC = restaurant as NPC.Types.RestaurantNPC;
            if (restaurantNPC == null) return true;
            
            // 口渴严重，喝免费水
            if (aiStats.CurrentThirst < aiStats.Config.maxThirst * 0.3f)
            {
                restaurantNPC.HandleAIRequest("water", gameObject);
                return true;
            }
            // 饥饿且有钱
            else if (aiStats.CurrentHunger < aiStats.Config.maxHunger * 0.3f && currencyManager != null && currencyManager.CurrentGold > 10)
            {
                restaurantNPC.HandleAIRequest("food", gameObject);
                return true;
            }
            return false;
        }
        
        private bool HandleBlacksmithInteraction(NPCBase blacksmith)
        {
            // AI装备升级决策
            var blacksmithNPC = blacksmith as NPC.Types.BlacksmithNPC;
            if (blacksmithNPC == null) return true;
            
            if (inventory != null && inventory.EquippedWeapon != null && currencyManager != null && currencyManager.CurrentGold > 200)
            {
                blacksmithNPC.HandleAIRequest("upgrade_weapon", gameObject);
                return true;
            }
            return false;
        }
        
        private bool HandleTailorInteraction(NPCBase tailor)
        {
            // AI背包扩容决策
            var tailorNPC = tailor as NPC.Types.TailorNPC;
            if (tailorNPC == null) return true;
            
            if (inventory == null || currencyManager == null) return false;
            
            int usedSlots = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty) usedSlots++;
            }
            
            if (usedSlots > inventory.Size * 0.8f && currencyManager.CurrentGold > 500)
            {
                tailorNPC.HandleAIRequest("expand_bag", gameObject);
                return true;
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