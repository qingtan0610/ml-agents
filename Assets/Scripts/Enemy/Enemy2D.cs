using UnityEngine;
using Combat.Interfaces;
using System.Collections;
using System.Collections.Generic;
using Buffs;
using Loot;
using Inventory.Items;
using Interactables;

namespace Enemy
{
    [System.Serializable]
    public class GuaranteedDrop
    {
        public ItemBase item;
        public int minAmount = 1;
        public int maxAmount = 1;
        [Range(0f, 1f)] public float dropChance = 1f;
    }
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Flee,
        Dead
    }
    
    public enum AttackPattern
    {
        Circle,      // 圆形范围攻击
        Line,        // 直线穿刺（长枪）
        Cone,        // 扇形攻击（挥砍）
        Rectangle,   // 矩形攻击（横扫）
        Cross,       // 十字攻击
        Ring         // 环形攻击（冲击波）
    }
    
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy2D : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] protected float maxHealth = 50f;
        [SerializeField] protected float currentHealth;
        [SerializeField] protected float damage = 10f;
        [SerializeField] protected float attackRange = 2f;
        [SerializeField] protected float attackCooldown = 1.5f;
        [SerializeField] protected float sightRange = 10f;
        [SerializeField] protected float moveSpeed = 3.5f;
        
        [Header("Combat")]
        [SerializeField] protected LayerMask targetLayer;
        
        [Header("Attack Variations")]
        [SerializeField] protected AttackPattern attackPattern = AttackPattern.Circle;
        [SerializeField] protected float attackWidth = 1f; // 矩形宽度或扇形角度
        [SerializeField] protected float attackLength = 2f; // 矩形长度（向前）
        [SerializeField] protected float attackAngle = 90f; // 扇形角度
        [SerializeField] protected float knockbackForce = 5f;
        [SerializeField] protected bool canAttackWhileMoving = false;
        [SerializeField] protected float chargeTime = 0f; // 攻击前蓄力时间
        [SerializeField] protected GameObject attackEffectPrefab; // 攻击特效
        [SerializeField] protected bool showAttackIndicator = true; // 显示攻击预警
        
        [Header("Loot Settings")]
        [SerializeField] protected Loot.LootTable lootTable;
        [SerializeField] protected GameObject unifiedPickupPrefab; // 统一拾取物预制体
        [SerializeField] protected float dropSpread = 1f;
        [SerializeField] protected List<GuaranteedDrop> guaranteedDrops = new List<GuaranteedDrop>(); // 保证掉落
        
        [Header("Loot Overrides")]
        [SerializeField] protected bool overrideLootChances = false;
        [SerializeField] protected float lootMultiplier = 1f; // 掉落倍率
        
        [Header("Debuff Settings")]
        [SerializeField] protected List<BuffBase> attackDebuffs = new List<BuffBase>();
        [SerializeField] protected float debuffChance = 0.3f;
        
        [Header("Behavior")]
        [SerializeField] protected EnemyState currentState = EnemyState.Idle;
        [SerializeField] protected float patrolRadius = 5f;
        [SerializeField] protected float idleTime = 2f;
        
        // Components
        protected Rigidbody2D rb;
        protected GameObject currentTarget;
        protected float lastAttackTime;
        protected Vector2 spawnPosition;
        protected Vector2 patrolTarget;
        
        // State
        protected bool isDead = false;
        protected Coroutine currentBehavior;
        
        // IDamageable implementation
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;
        
        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 俯视角不需要重力
            rb.freezeRotation = true;
            
            currentHealth = maxHealth;
            spawnPosition = transform.position;
        }
        
        protected virtual void Start()
        {
            // 验证Debuff配置
            if (attackDebuffs.Count == 0)
            {
                Debug.LogWarning($"[Enemy] {name} has no attack debuffs configured!");
            }
            else
            {
                Debug.Log($"[Enemy] {name} configured with {attackDebuffs.Count} debuffs:");
                foreach (var debuff in attackDebuffs)
                {
                    if (debuff != null)
                    {
                        float finalChance = debuff.ApplicationChance * debuffChance;
                        Debug.Log($"  - {debuff.BuffName} (Final chance: {finalChance * 100:F1}%)");
                    }
                }
            }
            
            StartBehavior();
        }
        
        protected virtual void Update()
        {
            if (isDead) return;
            
            UpdateState();
            UpdateRotation();
        }
        
        protected virtual void UpdateState()
        {
            // 检查目标
            if (currentTarget != null)
            {
                var targetDamageable = currentTarget.GetComponent<IDamageable>();
                if (targetDamageable != null && targetDamageable.IsDead)
                {
                    currentTarget = null;
                    ChangeState(EnemyState.Idle);
                    return;
                }
            }
            
            switch (currentState)
            {
                case EnemyState.Idle:
                case EnemyState.Patrol:
                    SearchForTarget();
                    break;
                    
                case EnemyState.Chase:
                    if (currentTarget != null)
                    {
                        float distance = Vector2.Distance(transform.position, currentTarget.transform.position);
                        if (distance <= attackRange)
                        {
                            ChangeState(EnemyState.Attack);
                        }
                        else if (distance > sightRange * 1.5f)
                        {
                            currentTarget = null;
                            ChangeState(EnemyState.Idle);
                        }
                    }
                    break;
                    
                case EnemyState.Attack:
                    if (currentTarget != null)
                    {
                        float distance = Vector2.Distance(transform.position, currentTarget.transform.position);
                        if (distance > attackRange * 1.2f)
                        {
                            ChangeState(EnemyState.Chase);
                        }
                    }
                    break;
            }
        }
        
        protected virtual void SearchForTarget()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, sightRange, targetLayer);
            
            foreach (var collider in colliders)
            {
                var damageable = collider.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    // 视线检测
                    Vector2 direction = (collider.transform.position - transform.position).normalized;
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, sightRange, ~(1 << gameObject.layer));
                    
                    if (hit.collider == collider)
                    {
                        currentTarget = collider.gameObject;
                        ChangeState(EnemyState.Chase);
                        break;
                    }
                }
            }
        }
        
        protected virtual void UpdateRotation()
        {
            // 面向移动方向或目标
            Vector2 lookDir = Vector2.zero;
            
            if (currentTarget != null)
            {
                lookDir = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
            }
            else if (rb.velocity.magnitude > 0.1f)
            {
                lookDir = rb.velocity.normalized;
            }
            
            if (lookDir != Vector2.zero)
            {
                float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
        
        protected virtual void ChangeState(EnemyState newState)
        {
            if (currentState == newState) return;
            
            if (currentBehavior != null)
            {
                StopCoroutine(currentBehavior);
            }
            
            currentState = newState;
            
            switch (newState)
            {
                case EnemyState.Idle:
                    currentBehavior = StartCoroutine(IdleBehavior());
                    break;
                case EnemyState.Patrol:
                    currentBehavior = StartCoroutine(PatrolBehavior());
                    break;
                case EnemyState.Chase:
                    currentBehavior = StartCoroutine(ChaseBehavior());
                    break;
                case EnemyState.Attack:
                    currentBehavior = StartCoroutine(AttackBehavior());
                    break;
            }
        }
        
        protected virtual IEnumerator IdleBehavior()
        {
            rb.velocity = Vector2.zero;
            yield return new WaitForSeconds(idleTime);
            
            if (currentState == EnemyState.Idle)
            {
                ChangeState(EnemyState.Patrol);
            }
        }
        
        protected virtual IEnumerator PatrolBehavior()
        {
            while (currentState == EnemyState.Patrol)
            {
                // 选择随机巡逻点
                Vector2 randomDirection = Random.insideUnitCircle * patrolRadius;
                patrolTarget = spawnPosition + randomDirection;
                
                // 移动到巡逻点
                while (Vector2.Distance(transform.position, patrolTarget) > 0.5f)
                {
                    Vector2 direction = (patrolTarget - (Vector2)transform.position).normalized;
                    rb.velocity = direction * moveSpeed * 0.5f; // 巡逻速度较慢
                    
                    yield return null;
                    
                    if (currentState != EnemyState.Patrol) yield break;
                }
                
                rb.velocity = Vector2.zero;
                yield return new WaitForSeconds(Random.Range(1f, 3f));
            }
        }
        
        protected virtual IEnumerator ChaseBehavior()
        {
            while (currentState == EnemyState.Chase && currentTarget != null)
            {
                Vector2 direction = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
                rb.velocity = direction * moveSpeed;
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        protected virtual IEnumerator AttackBehavior()
        {
            rb.velocity = Vector2.zero;
            
            while (currentState == EnemyState.Attack && currentTarget != null)
            {
                if (Time.time - lastAttackTime >= attackCooldown)
                {
                    PerformAttack();
                    lastAttackTime = Time.time;
                }
                
                yield return null;
            }
        }
        
        protected virtual void PerformAttack()
        {
            if (currentTarget == null) return;
            
            // 显示攻击预警
            if (showAttackIndicator && chargeTime > 0)
            {
                StartCoroutine(ShowAttackIndicator());
            }
            
            // 蓄力时间
            if (chargeTime > 0)
            {
                // TODO: 播放蓄力动画
                return; // 实际攻击会在ShowAttackIndicator协程中执行
            }
            
            ExecuteAttack();
        }
        
        protected IEnumerator ShowAttackIndicator()
        {
            // TODO: 显示攻击范围预警特效
            yield return new WaitForSeconds(chargeTime);
            ExecuteAttack();
        }
        
        protected virtual void ExecuteAttack()
        {
            var targets = GetTargetsInAttackArea();
            
            foreach (var target in targets)
            {
                ApplyDamageToTarget(target);
            }
            
            // 播放攻击特效
            if (attackEffectPrefab != null)
            {
                var effect = Instantiate(attackEffectPrefab, transform.position, transform.rotation);
                Destroy(effect, 2f);
            }
        }
        
        protected List<GameObject> GetTargetsInAttackArea()
        {
            var targets = new List<GameObject>();
            Collider2D[] hits = null;
            
            switch (attackPattern)
            {
                case AttackPattern.Circle:
                    hits = Physics2D.OverlapCircleAll(transform.position, attackRange, targetLayer);
                    break;
                    
                case AttackPattern.Line:
                    // 直线攻击
                    var direction = currentTarget != null ? 
                        (currentTarget.transform.position - transform.position).normalized : 
                        transform.up;
                    hits = Physics2D.OverlapBoxAll(
                        transform.position + direction * (attackLength / 2f),
                        new Vector2(attackWidth, attackLength),
                        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f,
                        targetLayer
                    );
                    break;
                    
                case AttackPattern.Cone:
                    // 扇形攻击 - 使用圆形检测然后过滤角度
                    hits = Physics2D.OverlapCircleAll(transform.position, attackRange, targetLayer);
                    var forward = currentTarget != null ? 
                        (currentTarget.transform.position - transform.position).normalized : 
                        transform.up;
                    
                    // 过滤角度
                    var validHits = new List<Collider2D>();
                    foreach (var hit in hits)
                    {
                        var toTarget = (hit.transform.position - transform.position).normalized;
                        var angle = Vector2.Angle(forward, toTarget);
                        if (angle <= attackAngle / 2f)
                        {
                            validHits.Add(hit);
                        }
                    }
                    hits = validHits.ToArray();
                    break;
                    
                case AttackPattern.Rectangle:
                    // 矩形横扫
                    hits = Physics2D.OverlapBoxAll(
                        transform.position,
                        new Vector2(attackWidth, attackLength),
                        transform.eulerAngles.z,
                        targetLayer
                    );
                    break;
                    
                case AttackPattern.Ring:
                    // 环形攻击
                    var allHits = Physics2D.OverlapCircleAll(transform.position, attackRange, targetLayer);
                    var innerHits = Physics2D.OverlapCircleAll(transform.position, attackRange * 0.5f, targetLayer);
                    var ringHits = new List<Collider2D>(allHits);
                    foreach (var inner in innerHits)
                    {
                        ringHits.Remove(inner);
                    }
                    hits = ringHits.ToArray();
                    break;
            }
            
            if (hits != null)
            {
                foreach (var hit in hits)
                {
                    if (hit.gameObject != gameObject)
                    {
                        targets.Add(hit.gameObject);
                    }
                }
            }
            
            return targets;
        }
        
        protected void ApplyDamageToTarget(GameObject target)
        {
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                var damageInfo = new DamageInfo(damage)
                {
                    hitPoint = target.transform.position,
                    hitDirection = (target.transform.position - transform.position).normalized,
                    knockback = knockbackForce
                };
                
                // 添加Debuff
                if (attackDebuffs.Count > 0 && Random.value <= debuffChance)
                {
                    foreach (var debuff in attackDebuffs)
                    {
                        if (debuff != null)
                        {
                            damageInfo.AddDebuff(debuff);
                        }
                    }
                }
                
                damageable.TakeDamage(damage, gameObject, damageInfo);
                
                // 应用Debuff
                var buffManager = target.GetComponent<BuffManager>();
                if (buffManager != null && damageInfo.appliedDebuffs != null)
                {
                    foreach (var debuff in damageInfo.appliedDebuffs)
                    {
                        buffManager.AddBuff(debuff);
                    }
                }
                
                // 击退
                if (knockbackForce > 0)
                {
                    var targetRb = target.GetComponent<Rigidbody2D>();
                    if (targetRb != null)
                    {
                        targetRb.AddForce(damageInfo.hitDirection * knockbackForce, ForceMode2D.Impulse);
                    }
                }
            }
        }
        
        protected virtual void StartBehavior()
        {
            ChangeState(EnemyState.Idle);
        }
        
        // IDamageable implementation
        public virtual void TakeDamage(float damage, GameObject attacker, DamageInfo damageInfo = null)
        {
            if (isDead) return;
            
            currentHealth -= damage;
            Debug.Log($"{name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
            
            // 受击反应
            if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
            {
                currentTarget = attacker;
                ChangeState(EnemyState.Chase);
            }
            
            // 击退效果
            if (damageInfo != null && damageInfo.knockback > 0)
            {
                Vector2 knockbackDir = (Vector2)damageInfo.hitDirection;
                rb.AddForce(knockbackDir * damageInfo.knockback, ForceMode2D.Impulse);
            }
            
            if (currentHealth <= 0)
            {
                Die();
            }
        }
        
        public virtual void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }
        
        public virtual void Die()
        {
            if (isDead) return;
            
            isDead = true;
            
            if (currentBehavior != null)
            {
                StopCoroutine(currentBehavior);
            }
            
            rb.velocity = Vector2.zero;
            
            // 掉落物品
            DropLoot();
            
            Debug.Log($"{name} died!");
            
            // 播放死亡效果后销毁
            Destroy(gameObject, 1f);
        }
        
        protected virtual void DropLoot()
        {
            // 如果没有指定预制体，尝试从Resources加载
            if (unifiedPickupPrefab == null)
            {
                unifiedPickupPrefab = Resources.Load<GameObject>("Prefabs/Pickups/UnifiedPickup");
                if (unifiedPickupPrefab == null)
                {
                    Debug.LogWarning($"[Enemy] Could not load unified pickup prefab");
                    return;
                }
            }
            
            // 生成保证掉落的物品
            foreach (var drop in guaranteedDrops)
            {
                if (drop.item != null && Random.value <= drop.dropChance)
                {
                    int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                    SpawnItemPickup(drop.item, amount, transform.position);
                }
            }
            
            // 如果有掉落表，生成随机掉落
            if (lootTable != null)
            {
                // 生成掉落
                Loot.LootResult loot = lootTable.GenerateLoot(lootMultiplier - 1f);
                
                // 只掉落物品，金币弹药通过SpecialConsumable物品处理
                foreach (var drop in loot.items)
                {
                    SpawnItemPickup(drop.item, drop.quantity, transform.position);
                }
                
                Debug.Log($"[Enemy] {name} dropped {loot.items.Count} items");
            }
        }
        
        protected void SpawnItemPickup(Inventory.Items.ItemBase item, int amount, Vector3 position)
        {
            if (item == null || unifiedPickupPrefab == null) return;
            
            // 为多个物品创建散落效果
            int stacks = 1;
            int amountPerStack = amount;
            
            // 如果是金币或弹药，分成多个堆
            if (item is SpecialConsumable && amount > 10)
            {
                stacks = Mathf.Min(3, amount / 10); // 减少到最多3堆
                amountPerStack = amount / stacks;
            }
            
            for (int i = 0; i < stacks; i++)
            {
                // 计算当前堆的数量
                int stackAmount = (i == stacks - 1) ? 
                    amount - (amountPerStack * (stacks - 1)) : // 最后一堆包含余数
                    amountPerStack;
                
                // 随机位置 - 减小初始散落范围
                float angle = (360f / stacks) * i + Random.Range(-15f, 15f);
                float distance = Random.Range(0.2f, 0.5f); // 小一点的初始距离
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                );
                Vector3 spawnPos = position + new Vector3(offset.x, offset.y, 0);
                
                // 创建拾取物
                GameObject pickup = Instantiate(unifiedPickupPrefab, spawnPos, Quaternion.identity);
                var pickupComponent = pickup.GetComponent<Loot.UnifiedPickup>();
                
                if (pickupComponent != null)
                {
                    pickupComponent.Initialize(item, stackAmount);
                }
                
                // 添加碰撞停止组件
                var stopOnWall = pickup.AddComponent<StopOnWallCollision>();
                
                // 添加适中的弹出力
                var rb = pickup.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // 增加弹出力，让掉落效果更明显
                    Vector2 force = offset.normalized * Random.Range(1.5f, 2.5f) + Vector2.up * Random.Range(1f, 2f);
                    rb.AddForce(force, ForceMode2D.Impulse);
                    
                    // 适当的阻力
                    rb.drag = 5f;
                    rb.angularDrag = 5f;
                    rb.gravityScale = 0f;
                    
                    // 添加一些旋转
                    rb.angularVelocity = Random.Range(-90f, 90f);
                }
            }
        }
        
        // 注意：金币、弹药等现在通过guaranteedDrops中的SpecialConsumable物品掉落
        // 不再需要在LootTable中单独配置金币弹药数值
        
        // 调试绘制
        protected virtual void OnDrawGizmosSelected()
        {
            // 视野范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            
            // 攻击范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // 巡逻范围
            Vector3 patrol = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(patrol, patrolRadius);
            
            // 巡逻目标
            if (Application.isPlaying && currentState == EnemyState.Patrol)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, patrolTarget);
                Gizmos.DrawWireSphere(patrolTarget, 0.3f);
            }
        }
    }
}