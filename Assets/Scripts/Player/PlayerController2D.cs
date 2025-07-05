using UnityEngine;
using Combat;
using Combat.Interfaces;
using AI.Stats;

namespace Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(CombatSystem2D))]
    public class PlayerController2D : MonoBehaviour, IDamageable
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float smoothTime = 0.1f;
        
        [Header("Combat")]
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        
        [Header("Camera")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float cameraZ = -10f;
        [SerializeField] private float cameraSmooth = 0.1f;
        
        // Components
        private Rigidbody2D rb;
        private CombatSystem2D combatSystem;
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        
        // Movement
        private Vector2 movement;
        private Vector2 velocity;
        private Vector2 velocitySmooth;
        private bool isSprinting;
        
        // AI Control
        private bool isAIControlled = false;
        private Vector2 aiMovementInput;
        private Vector2 aiAimDirection;
        
        // IDamageable implementation
        public float CurrentHealth => aiStats?.GetStat(StatType.Health) ?? 100f;
        public float MaxHealth => aiStats?.Config?.maxHealth ?? 100f;
        public bool IsDead => aiStats?.IsDead ?? false;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            combatSystem = GetComponent<CombatSystem2D>();
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            
            // 设置2D刚体
            rb.gravityScale = 0f; // 俯视角不需要重力
            rb.freezeRotation = true;
            
            SetupCamera();
        }
        
        private void SetupCamera()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            
            if (playerCamera != null)
            {
                // 设置正交相机
                playerCamera.orthographic = true;
                UpdateCameraPosition();
            }
        }
        
        private void Update()
        {
            if (IsDead)
            {
                StopMovement();
                return;
            }
            
            HandleInput();
            HandleRotation();
            HandleCombat();
            HandleInteraction();
        }
        
        private void StopMovement()
        {
            movement = Vector2.zero;
            velocity = Vector2.zero;
            velocitySmooth = Vector2.zero; // 重置平滑变量
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
        
        private void FixedUpdate()
        {
            if (IsDead) 
            {
                // 死亡时确保不会有任何移动
                if (rb != null && rb.velocity.magnitude > 0.01f)
                {
                    rb.velocity = Vector2.zero;
                }
                return;
            }
            
            HandleMovement();
        }
        
        private void LateUpdate()
        {
            UpdateCameraPosition();
        }
        
        private void HandleInput()
        {
            if (isAIControlled)
            {
                // 使用AI输入
                movement = aiMovementInput;
                
                // 归一化但保持死区
                if (movement.magnitude > 1f)
                {
                    movement.Normalize();
                }
                // Sprint状态由AI控制方法设置
            }
            else
            {
                // 获取输入
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                movement = new Vector2(h, v);
                
                // 归一化但保持死区
                if (movement.magnitude > 1f)
                {
                    movement.Normalize();
                }
                
                // 调试输入问题
                if (movement.magnitude > 0.1f && Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f)
                {
                    Debug.LogWarning($"Input mismatch! Movement: {movement}, Raw input: ({h}, {v})");
                }
                
                // 检查冲刺
                isSprinting = Input.GetKey(sprintKey) && aiStats != null && aiStats.GetStat(StatType.Stamina) > 10f;
            }
        }
        
        private void HandleMovement()
        {
            // 确保移动向量正确
            if (movement.magnitude < 0.01f)
            {
                movement = Vector2.zero;
                velocity = Vector2.Lerp(velocity, Vector2.zero, Time.fixedDeltaTime * 10f);
            }
            else
            {
                float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;
                
                // 体力影响移动速度：体力为0时速度减半
                if (aiStats != null)
                {
                    float stamina = aiStats.GetStat(StatType.Stamina);
                    if (stamina <= 0f)
                    {
                        currentSpeed *= 0.5f; // 体力为0时速度减半
                        Debug.Log($"[Player] Low stamina! Speed reduced to {currentSpeed}");
                    }
                }
                
                velocity = Vector2.SmoothDamp(velocity, movement * currentSpeed, ref velocitySmooth, smoothTime);
            }
            
            rb.velocity = velocity;
            
            // 更新AI状态
            if (movement.magnitude > 0.1f)
            {
                aiStats?.SetMovementState(true);
                
                // 冲刺消耗体力
                if (isSprinting && aiStats != null)
                {
                    aiStats.ModifyStat(StatType.Stamina, -10f * Time.fixedDeltaTime, StatChangeReason.Natural);
                }
            }
            else
            {
                aiStats?.SetMovementState(false);
            }
        }
        
        private void HandleRotation()
        {
            // 面向鼠标方向
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = transform.position.z;
            
            Vector2 lookDir = (mousePos - transform.position).normalized;
            if (lookDir != Vector2.zero)
            {
                float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
        
        private void HandleCombat()
        {
            if (Input.GetKeyDown(attackKey))
            {
                combatSystem.PerformAttack();
                
                // 触发武器视觉动画
                var weaponVisual = GetComponent<Player.WeaponVisualDisplay>();
                if (weaponVisual != null)
                {
                    weaponVisual.PlayAttackAnimation();
                }
            }
        }
        
        private void HandleInteraction()
        {
            if (Input.GetKeyDown(interactKey))
            {
                Debug.Log($"[Interaction] E key pressed at position {transform.position}");
                
                // 检测周围可交互对象（使用更大的范围）
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 2.5f);
                Debug.Log($"[Interaction] Found {colliders.Length} colliders in range");
                
                foreach (var collider in colliders)
                {
                    Debug.Log($"[Interaction] Checking collider: {collider.name} on GameObject: {collider.gameObject.name}");
                    
                    // 先检查NPC
                    var npc = collider.GetComponent<NPC.Core.NPCBase>();
                    if (npc != null)
                    {
                        Debug.Log($"[Interaction] Found NPC: {npc.name}, CanInteract: {npc.CanInteract(gameObject)}");
                        if (npc.CanInteract(gameObject))
                        {
                            Debug.Log($"[Interaction] Starting interaction with NPC: {npc.name}");
                            npc.StartInteraction(gameObject);
                            break;
                        }
                    }
                    
                    // 其他可交互对象
                    var interactable = collider.GetComponent<Interactables.IInteractable>();
                    if (interactable != null)
                    {
                        Debug.Log($"[Interaction] Found IInteractable on {collider.name}");
                        interactable.Interact(gameObject);
                        break;
                    }
                }
                
                if (colliders.Length == 0)
                {
                    Debug.Log("[Interaction] No colliders found in range!");
                }
            }
        }
        
        private void UpdateCameraPosition()
        {
            if (playerCamera == null) return;
            
            // 相机跟随玩家
            Vector3 targetPos = transform.position;
            targetPos.z = cameraZ;
            
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position, 
                targetPos, 
                cameraSmooth
            );
        }
        
        // IDamageable implementation
        public void TakeDamage(float damage, GameObject attacker, DamageInfo damageInfo = null)
        {
            if (IsDead) return;
            
            // 应用防御计算
            float armor = aiStats?.GetStat(StatType.Armor) ?? 0f;
            float damageReduction = armor / (armor + 100f);
            float finalDamage = damage * (1f - damageReduction);
            
            aiStats?.ModifyStat(StatType.Health, -finalDamage, StatChangeReason.Combat);
            
            // 显示伤害数字
            string attackerName = attacker != null ? attacker.name : "Unknown";
            Debug.Log($"Player took {finalDamage:F1} damage from {attackerName}");
            
            // 击退效果
            if (damageInfo != null && damageInfo.knockback > 0 && attacker != null)
            {
                Vector2 knockbackDir = ((Vector2)transform.position - (Vector2)attacker.transform.position).normalized;
                rb.AddForce(knockbackDir * damageInfo.knockback, ForceMode2D.Impulse);
            }
        }
        
        public void Heal(float amount)
        {
            aiStats?.ModifyStat(StatType.Health, amount, StatChangeReason.Item);
        }
        
        public void Die()
        {
            Debug.Log("[PlayerController2D] Die() called");
            StopMovement();
            
            // 重置所有运动相关变量
            movement = Vector2.zero;
            velocity = Vector2.zero;
            velocitySmooth = Vector2.zero;
            
            // 确保刚体完全停止
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;  // 冻结所有运动
            }
            
            // 禁用控制但不禁用整个组件，这样其他组件仍能工作
            // enabled = false; // 不要禁用，否则会阻止其他组件工作
        }
        
        public void Respawn()
        {
            Debug.Log("[PlayerController2D] Respawn() called");
            
            // 重置运动状态
            StopMovement();
            
            // 重置所有运动变量
            movement = Vector2.zero;
            velocity = Vector2.zero;
            velocitySmooth = Vector2.zero;
            
            // 解冻刚体
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;  // 只冻结旋转
            }
        }
        
        // AI Control Methods
        public void SetAIControlled(bool controlled)
        {
            isAIControlled = controlled;
        }
        
        public void SetMovementInput(Vector2 input)
        {
            if (isAIControlled)
            {
                aiMovementInput = input;
            }
        }
        
        public void SetAimDirection(Vector2 direction)
        {
            if (isAIControlled)
            {
                aiAimDirection = direction;
            }
        }
        
        public void StartSprint()
        {
            if (isAIControlled && aiStats?.GetStat(StatType.Stamina) > 0)
            {
                isSprinting = true;
            }
        }
        
        public void StopSprint()
        {
            if (isAIControlled)
            {
                isSprinting = false;
            }
        }
        
        public void PerformAttack()
        {
            if (isAIControlled && !IsDead)
            {
                combatSystem.PerformAttack();
                
                // 触发武器视觉动画
                var weaponVisual = GetComponent<Player.WeaponVisualDisplay>();
                if (weaponVisual != null)
                {
                    weaponVisual.PlayAttackAnimation();
                }
            }
        }
        
        
        // 调试绘制
        private void OnDrawGizmosSelected()
        {
            // 绘制交互范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 2.5f);
        }
        
        private void OnDrawGizmos()
        {
            // 始终显示交互范围
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 2.5f);
        }
    }
}