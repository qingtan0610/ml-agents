using UnityEngine;
using System.Collections;
using Inventory;
using Inventory.Items;

namespace Player
{
    /// <summary>
    /// 武器视觉显示系统 - 在玩家旁边显示装备的武器图标并播放攻击动画
    /// </summary>
    public class WeaponVisualDisplay : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private float weaponDistance = 0.8f; // 武器距离玩家的距离
        [SerializeField] private Vector2 weaponOffset = Vector2.right; // 武器相对于玩家的偏移
        [SerializeField] private float weaponScale = 0.6f; // 武器图标缩放
        [SerializeField] private float targetIconSize = 32f; // 目标图标大小（像素）
        [SerializeField] private bool autoScaleIcon = true; // 是否自动缩放图标
        
        [Header("Animation Settings")]
        // 注意：动画现在会自动使用武器的实际攻击范围，下面的值仅作为备用
        [SerializeField] private float circleAttackRadius = 1.2f; // 圆形攻击动画半径（备用）
        [SerializeField] private float thrustDistance = 1f; // 刺击距离（备用）
        [SerializeField] private float rangedExtendDistance = 0.8f; // 远程武器伸展距离（备用）
        
        [Header("Trail Effect Settings")]
        [SerializeField] private bool enableTrailEffect = true; // 是否启用轨迹特效
        [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.8f); // 轨迹颜色
        [SerializeField] private float trailDuration = 0.3f; // 轨迹持续时间
        [SerializeField] private float trailStartWidth = 0.2f; // 轨迹起始宽度
        [SerializeField] private float trailEndWidth = 0.05f; // 轨迹结束宽度
        [SerializeField] private Material trailMaterial; // 轨迹材质（可选）
        
        [Header("Components")]
        private Inventory.Inventory inventory;
        private SpriteRenderer weaponRenderer;
        private GameObject weaponDisplay;
        private TrailRenderer weaponTrail;
        
        // 当前状态
        private WeaponItem currentWeapon;
        private bool isAttacking = false;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private Coroutine currentAttackCoroutine;
        
        private void Awake()
        {
            inventory = GetComponent<Inventory.Inventory>();
            CreateWeaponDisplay();
        }
        
        private void Start()
        {
            if (inventory != null)
            {
                inventory.OnWeaponChanged.AddListener(OnWeaponChanged);
                // 初始化显示当前武器
                OnWeaponChanged(inventory.EquippedWeapon);
            }
        }
        
        private void CreateWeaponDisplay()
        {
            // 创建武器显示对象
            weaponDisplay = new GameObject("WeaponDisplay");
            weaponDisplay.transform.SetParent(transform);
            
            // 添加SpriteRenderer
            weaponRenderer = weaponDisplay.AddComponent<SpriteRenderer>();
            weaponRenderer.sortingOrder = 1; // 显示在玩家前面
            
            // 添加TrailRenderer用于轨迹效果
            if (enableTrailEffect)
            {
                weaponTrail = weaponDisplay.AddComponent<TrailRenderer>();
                ConfigureTrailRenderer();
                weaponTrail.enabled = false; // 默认禁用，只在攻击时启用
            }
            
            // 设置初始位置和缩放
            UpdateWeaponPosition();
            weaponDisplay.transform.localScale = Vector3.one * 1f; // 默认缩放为1
            baseScale = weaponDisplay.transform.localScale;
            
            // 初始隐藏
            weaponDisplay.SetActive(false);
        }
        
        /// <summary>
        /// 配置轨迹渲染器
        /// </summary>
        private void ConfigureTrailRenderer()
        {
            if (weaponTrail == null) return;
            
            // 基本设置
            weaponTrail.time = trailDuration;
            weaponTrail.startWidth = trailStartWidth;
            weaponTrail.endWidth = trailEndWidth;
            
            // 颜色渐变
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(trailColor, 0.0f), 
                    new GradientColorKey(trailColor, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(trailColor.a, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) // 渐隐效果
                }
            );
            weaponTrail.colorGradient = gradient;
            
            // 材质设置
            if (trailMaterial != null)
            {
                weaponTrail.material = trailMaterial;
            }
            else
            {
                // 使用默认的Sprites-Default材质
                weaponTrail.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            // 其他设置
            weaponTrail.sortingOrder = 0; // 显示在武器后面
            weaponTrail.minVertexDistance = 0.1f;
            weaponTrail.autodestruct = false;
        }
        
        private void Update()
        {
            if (weaponDisplay != null && weaponDisplay.activeInHierarchy)
            {
                UpdateWeaponPosition();
            }
        }
        
        private void UpdateWeaponPosition()
        {
            if (weaponDisplay == null) return;
            
            // 基础位置：玩家位置 + 根据玩家朝向旋转的偏移
            Vector3 rotatedOffset = transform.rotation * (Vector3)(weaponOffset.normalized * weaponDistance);
            basePosition = transform.position + rotatedOffset;
            
            if (!isAttacking)
            {
                weaponDisplay.transform.position = basePosition;
                // 武器图标也跟随玩家朝向旋转
                weaponDisplay.transform.rotation = transform.rotation;
            }
        }
        
        private void OnWeaponChanged(WeaponItem newWeapon)
        {
            currentWeapon = newWeapon;
            
            if (newWeapon == null)
            {
                // 没有武器，隐藏显示
                if (weaponDisplay != null)
                {
                    weaponDisplay.SetActive(false);
                }
                Debug.Log("[WeaponVisual] Weapon unequipped, hiding display");
            }
            else
            {
                // 有武器，显示图标
                ShowWeapon(newWeapon);
                Debug.Log($"[WeaponVisual] Weapon equipped: {newWeapon.ItemName}");
            }
        }
        
        private void ShowWeapon(WeaponItem weapon)
        {
            if (weaponDisplay == null || weaponRenderer == null) return;
            
            // 设置武器图标
            if (weapon.Icon != null)
            {
                weaponRenderer.sprite = weapon.Icon;
                weaponDisplay.SetActive(true);
                
                // 自动缩放图标到目标大小
                if (autoScaleIcon)
                {
                    ApplyAutoScale(weapon.Icon);
                }
                else
                {
                    weaponDisplay.transform.localScale = Vector3.one * 1f; // 默认缩放为1
                    baseScale = weaponDisplay.transform.localScale;
                }
                
                Debug.Log($"[WeaponVisual] Displaying weapon icon: {weapon.ItemName} (scale: {weaponDisplay.transform.localScale})");
            }
            else
            {
                Debug.LogWarning($"[WeaponVisual] Weapon {weapon.ItemName} has no icon");
                weaponDisplay.SetActive(false);
            }
        }
        
        /// <summary>
        /// 自动缩放图标到目标大小
        /// </summary>
        private void ApplyAutoScale(Sprite sprite)
        {
            if (sprite == null) return;
            
            // 获取图标的原始尺寸（像素）
            float originalWidth = sprite.rect.width;
            float originalHeight = sprite.rect.height;
            
            // 找出最大的维度
            float maxDimension = Mathf.Max(originalWidth, originalHeight);
            
            // 计算缩放因子
            float pixelsPerUnit = sprite.pixelsPerUnit;
            
            // 计算当前sprite在世界空间中的大小
            float currentWorldSize = maxDimension / pixelsPerUnit;
            
            // 计算目标世界大小（假设玩家是32x32像素，PPU=32，所以是1个单位）
            // 如果要让16x16的图标看起来和32x32的玩家差不多大，需要缩放到相似的世界大小
            float targetWorldSize = targetIconSize / 32f; // 32是假设的基础PPU
            
            // 计算需要的缩放
            float finalScale = targetWorldSize / currentWorldSize;
            
            // 应用缩放
            weaponDisplay.transform.localScale = Vector3.one * finalScale;
            baseScale = weaponDisplay.transform.localScale;
            
            Debug.Log($"[WeaponVisual] Auto-scaled icon from {originalWidth}x{originalHeight} (PPU: {pixelsPerUnit}) to target size {targetIconSize}px");
            Debug.Log($"[WeaponVisual] Current world size: {currentWorldSize}, Target world size: {targetWorldSize}");
            Debug.Log($"[WeaponVisual] Final scale: {finalScale}, Result world size: {currentWorldSize * finalScale}");
        }
        
        /// <summary>
        /// 播放攻击动画
        /// </summary>
        public void PlayAttackAnimation()
        {
            // 只有在正在攻击时才清理，否则直接开始新攻击
            if (isAttacking && currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
                currentAttackCoroutine = null;
                isAttacking = false;
            }
            
            // 空手攻击特效
            if (currentWeapon == null)
            {
                Debug.Log("[WeaponVisual] Playing unarmed attack animation");
                currentAttackCoroutine = StartCoroutine(UnarmedAttackAnimation());
                return;
            }
            
            if (weaponDisplay == null) return;
            
            Debug.Log($"[WeaponVisual] Playing attack animation for {currentWeapon.ItemName} ({currentWeapon.WeaponType}, {currentWeapon.AttackShape})");
            
            // 启用轨迹效果
            if (enableTrailEffect && weaponTrail != null)
            {
                weaponTrail.enabled = true;
                weaponTrail.Clear(); // 清除之前的轨迹
            }
            
            // 根据武器类型和攻击形状播放不同动画
            if (currentWeapon.WeaponType == WeaponType.Ranged)
            {
                currentAttackCoroutine = StartCoroutine(RangedAttackAnimation());
            }
            else if (currentWeapon.WeaponType == WeaponType.Magic)
            {
                currentAttackCoroutine = StartCoroutine(MagicAttackAnimation());
            }
            else
            {
                // 近战武器根据攻击形状选择动画
                switch (currentWeapon.AttackShape)
                {
                    case AttackShape.Circle:
                        currentAttackCoroutine = StartCoroutine(CircleAttackAnimation());
                        break;
                    case AttackShape.Line:
                        currentAttackCoroutine = StartCoroutine(ThrustAttackAnimation());
                        break;
                    case AttackShape.Sector:
                        currentAttackCoroutine = StartCoroutine(SectorAttackAnimation());
                        break;
                    case AttackShape.Rectangle:
                        currentAttackCoroutine = StartCoroutine(RectangleAttackAnimation());
                        break;
                    default:
                        currentAttackCoroutine = StartCoroutine(DefaultAttackAnimation());
                        break;
                }
            }
        }
        
        /// <summary>
        /// 停止轨迹效果
        /// </summary>
        private IEnumerator DisableTrailAfterDelay()
        {
            if (weaponTrail == null) yield break;
            
            // 等待轨迹完全消失
            yield return new WaitForSeconds(trailDuration);
            weaponTrail.enabled = false;
        }
        
        /// <summary>
        /// 圆形攻击动画 - 以玩家为中心，相对于玩家朝向旋转一圈
        /// </summary>
        private IEnumerator CircleAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed; // 根据攻速计算动画时长
            float elapsed = 0f;
            
            // 使用实际的武器攻击范围
            float actualRadius = currentWeapon.AttackRange;
            Debug.Log($"[WeaponVisual] Circle attack - Weapon range: {currentWeapon.AttackRange}, Animation radius: {actualRadius}");
            
            while (elapsed < duration)
            {
                // 实时更新玩家位置，让动画跟随玩家移动
                Vector3 playerPos = transform.position;
                float playerAngle = transform.eulerAngles.z * Mathf.Deg2Rad;
                
                float progress = elapsed / duration;
                // 从玩家当前朝向开始，顺时针转一整圈
                float currentAngle = playerAngle + (progress * 360f * Mathf.Deg2Rad);
                
                Vector3 circlePos = playerPos + new Vector3(
                    Mathf.Cos(currentAngle) * actualRadius,
                    Mathf.Sin(currentAngle) * actualRadius,
                    0
                );
                
                weaponDisplay.transform.position = circlePos;
                // 武器图标朝向圆心（玩家）
                Vector3 directionToPlayer = (playerPos - circlePos).normalized;
                float weaponAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg - 90f;
                weaponDisplay.transform.rotation = Quaternion.AngleAxis(weaponAngle, Vector3.forward);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 动画结束，回到基础位置和朝向
            UpdateWeaponPosition();
            isAttacking = false;
            
            // 延迟关闭轨迹
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 直线刺击动画 - 沿玩家朝向向前刺出再收回
        /// </summary>
        private IEnumerator ThrustAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float halfDuration = duration * 0.5f;
            
            // 使用实际的武器攻击范围
            float actualThrustDistance = currentWeapon.AttackRange;
            Debug.Log($"[WeaponVisual] Line attack - Weapon range: {currentWeapon.AttackRange}, Thrust distance: {actualThrustDistance}");
            
            // 刺出阶段
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                // 实时更新位置，让动画跟随玩家移动
                UpdateWeaponPosition();
                Vector3 startPos = basePosition;
                Vector3 thrustDirection = transform.up;
                Vector3 thrustPos = startPos + thrustDirection * actualThrustDistance;
                
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.position = Vector3.Lerp(startPos, thrustPos, progress);
                // 保持武器朝向
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 收回阶段
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                // 实时更新位置
                UpdateWeaponPosition();
                Vector3 startPos = basePosition;
                Vector3 thrustDirection = transform.up;
                Vector3 thrustPos = startPos + thrustDirection * actualThrustDistance;
                
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.position = Vector3.Lerp(thrustPos, startPos, progress);
                // 保持武器朝向
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 动画结束，回到基础位置和朝向
            UpdateWeaponPosition();
            isAttacking = false;
            
            // 延迟关闭轨迹
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 远程武器动画 - 弓箭和枪械的后拉射击动画
        /// </summary>
        private IEnumerator RangedAttackAnimation()
        {
            isAttacking = true;
            float duration = Mathf.Max(1f / currentWeapon.AttackSpeed, 0.5f); // 最少0.5秒保证动画完整
            float moveTime = duration * 0.2f;     // 移动到位20%
            float drawTime = duration * 0.2f;     // 后拉20%
            float holdTime = duration * 0.2f;     // 保持20%
            float releaseTime = duration * 0.2f;  // 发射20%
            float returnTime = duration * 0.2f;   // 返回20%
            
            // 瞄准位置：在玩家前方较近的位置
            float aimDistance = 0.5f; // 固定的瞄准距离
            float drawBackDistance = 0.3f; // 后拉距离
            
            Debug.Log($"[WeaponVisual] Ranged attack - {currentWeapon.ItemName}");
            
            // 1. 移动到瞄准位置
            float elapsed = 0f;
            while (elapsed < moveTime)
            {
                UpdateWeaponPosition();
                Vector3 aimPos = transform.position + transform.up * aimDistance;
                float progress = elapsed / moveTime;
                weaponDisplay.transform.position = Vector3.Lerp(basePosition, aimPos, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 2. 后拉（拉弓/拉栓）
            elapsed = 0f;
            while (elapsed < drawTime)
            {
                UpdateWeaponPosition();
                Vector3 aimPos = transform.position + transform.up * aimDistance;
                Vector3 drawPos = aimPos - transform.up * drawBackDistance;
                float progress = elapsed / drawTime;
                weaponDisplay.transform.position = Vector3.Lerp(aimPos, drawPos, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 3. 保持（瞄准）
            elapsed = 0f;
            while (elapsed < holdTime)
            {
                UpdateWeaponPosition();
                Vector3 drawPos = transform.position + transform.up * (aimDistance - drawBackDistance);
                weaponDisplay.transform.position = drawPos;
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 4. 发射（快速向前）
            elapsed = 0f;
            while (elapsed < releaseTime)
            {
                UpdateWeaponPosition();
                Vector3 drawPos = transform.position + transform.up * (aimDistance - drawBackDistance);
                Vector3 shootPos = transform.position + transform.up * (aimDistance + 0.2f); // 稍微超过瞄准位置
                float progress = elapsed / releaseTime;
                weaponDisplay.transform.position = Vector3.Lerp(drawPos, shootPos, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 5. 返回原位
            elapsed = 0f;
            while (elapsed < returnTime)
            {
                UpdateWeaponPosition();
                Vector3 shootPos = transform.position + transform.up * (aimDistance + 0.2f);
                float progress = elapsed / returnTime;
                weaponDisplay.transform.position = Vector3.Lerp(shootPos, basePosition, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 动画结束
            UpdateWeaponPosition();
            isAttacking = false;
            
            // 延迟关闭轨迹
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 扇形攻击动画 - 相对于玩家朝向进行扇形挥舞
        /// </summary>
        private IEnumerator SectorAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float elapsed = 0f;
            
            // 使用实际的武器攻击范围
            float actualRadius = currentWeapon.AttackRange;
            float sectorAngle = currentWeapon.SectorAngle * Mathf.Deg2Rad;
            Debug.Log($"[WeaponVisual] Sector attack - Weapon range: {currentWeapon.AttackRange}, Sector angle: {currentWeapon.SectorAngle}°");
            
            while (elapsed < duration)
            {
                // 实时更新玩家位置和朝向
                Vector3 playerPos = transform.position;
                float playerAngle = transform.eulerAngles.z * Mathf.Deg2Rad;
                
                // 相对于玩家朝向的扇形范围
                float startAngle = playerAngle - sectorAngle * 0.5f;
                float endAngle = playerAngle + sectorAngle * 0.5f;
                
                float progress = elapsed / duration;
                float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
                
                Vector3 sectorPos = playerPos + new Vector3(
                    Mathf.Cos(currentAngle) * actualRadius,
                    Mathf.Sin(currentAngle) * actualRadius,
                    0
                );
                
                weaponDisplay.transform.position = sectorPos;
                
                // 武器朝向从玩家指向当前位置
                Vector3 direction = (sectorPos - playerPos).normalized;
                float weaponAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                weaponDisplay.transform.rotation = Quaternion.AngleAxis(weaponAngle, Vector3.forward);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 动画结束，回到基础位置和朝向
            UpdateWeaponPosition();
            isAttacking = false;
            
            // 延迟关闭轨迹
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 矩形攻击动画 - 相对于玩家朝向进行横扫
        /// </summary>
        private IEnumerator RectangleAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float elapsed = 0f;
            
            // 使用实际的矩形宽度
            float actualWidth = currentWeapon.RectangleWidth;
            float forwardDistance = currentWeapon.AttackRange * 0.5f; // 矩形在玩家前方
            Debug.Log($"[WeaponVisual] Rectangle attack - Width: {actualWidth}, Range: {currentWeapon.AttackRange}");
            
            while (elapsed < duration)
            {
                // 实时更新玩家位置和朝向
                Vector3 playerPos = transform.position;
                Vector3 playerForward = transform.up;
                Vector3 playerRight = transform.right;
                
                // 矩形攻击在玩家前方
                Vector3 rectangleCenter = playerPos + playerForward * forwardDistance;
                Vector3 sweepLeft = rectangleCenter - playerRight * (actualWidth * 0.5f);
                Vector3 sweepRight = rectangleCenter + playerRight * (actualWidth * 0.5f);
                
                float progress = elapsed / duration;
                weaponDisplay.transform.position = Vector3.Lerp(sweepLeft, sweepRight, progress);
                // 保持武器朝向
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 动画结束，回到基础位置和朝向
            UpdateWeaponPosition();
            isAttacking = false;
            
            // 延迟关闭轨迹
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 默认攻击动画 - 缩放效果，保持朝向
        /// </summary>
        private IEnumerator DefaultAttackAnimation()
        {
            isAttacking = true;
            float duration = Mathf.Max(1f / currentWeapon.AttackSpeed, 0.5f); // 最小0.5秒
            float halfDuration = duration * 0.5f;
            
            Vector3 enlargedScale = baseScale * 1.3f;
            
            // 放大阶段
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.localScale = Vector3.Lerp(baseScale, enlargedScale, progress);
                // 保持武器朝向
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 缩回阶段
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.localScale = Vector3.Lerp(enlargedScale, baseScale, progress);
                // 保持武器朝向
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            weaponDisplay.transform.localScale = baseScale;
            // 动画结束，回到基础位置和朝向
            UpdateWeaponPosition();
            isAttacking = false;
            
            // 延迟关闭轨迹
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.OnWeaponChanged.RemoveListener(OnWeaponChanged);
            }
        }
        
        // 调试绘制
        private void OnDrawGizmosSelected()
        {
            if (currentWeapon == null) return;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, weaponDistance);
            
            // 绘制实际的攻击范围
            Gizmos.color = Color.red;
            switch (currentWeapon.AttackShape)
            {
                case AttackShape.Circle:
                    Gizmos.DrawWireSphere(transform.position, currentWeapon.AttackRange);
                    break;
                    
                case AttackShape.Line:
                    Gizmos.DrawLine(transform.position, transform.position + transform.up * currentWeapon.AttackRange);
                    break;
                    
                case AttackShape.Sector:
                    // 绘制扇形
                    float halfAngle = currentWeapon.SectorAngle * 0.5f * Mathf.Deg2Rad;
                    Vector3 leftDir = Quaternion.Euler(0, 0, -currentWeapon.SectorAngle * 0.5f) * transform.up;
                    Vector3 rightDir = Quaternion.Euler(0, 0, currentWeapon.SectorAngle * 0.5f) * transform.up;
                    Gizmos.DrawLine(transform.position, transform.position + leftDir * currentWeapon.AttackRange);
                    Gizmos.DrawLine(transform.position, transform.position + rightDir * currentWeapon.AttackRange);
                    break;
                    
                case AttackShape.Rectangle:
                    // 绘制矩形
                    Vector3 center = transform.position + transform.up * (currentWeapon.AttackRange * 0.5f);
                    Vector3 halfSize = new Vector3(currentWeapon.RectangleWidth * 0.5f, currentWeapon.AttackRange * 0.5f, 0);
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, halfSize * 2f);
                    Gizmos.matrix = oldMatrix;
                    break;
            }
        }
        
        /// <summary>
        /// 魔法武器动画 - 法杖向前指向施法
        /// </summary>
        private IEnumerator MagicAttackAnimation()
        {
            isAttacking = true;
            float duration = Mathf.Max(1f / currentWeapon.AttackSpeed, 0.5f); // 最少0.5秒
            float raiseTime = duration * 0.3f;   // 举起30%
            float castTime = duration * 0.4f;    // 施法40%
            float lowerTime = duration * 0.3f;   // 放下30%
            
            float castDistance = 0.6f; // 施法时向前的距离
            
            Debug.Log($"[WeaponVisual] Magic attack - {currentWeapon.ItemName}");
            
            // 1. 举起法杖
            float elapsed = 0f;
            while (elapsed < raiseTime)
            {
                UpdateWeaponPosition();
                Vector3 castPos = transform.position + transform.up * castDistance;
                float progress = elapsed / raiseTime;
                weaponDisplay.transform.position = Vector3.Lerp(basePosition, castPos, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 2. 施法（保持）
            elapsed = 0f;
            while (elapsed < castTime)
            {
                UpdateWeaponPosition();
                Vector3 castPos = transform.position + transform.up * castDistance;
                weaponDisplay.transform.position = castPos;
                weaponDisplay.transform.rotation = transform.rotation;
                // 可以添加轻微的震动效果
                float vibration = Mathf.Sin(elapsed * 20f) * 0.02f;
                weaponDisplay.transform.position += transform.right * vibration;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 3. 放下法杖
            elapsed = 0f;
            while (elapsed < lowerTime)
            {
                UpdateWeaponPosition();
                Vector3 castPos = transform.position + transform.up * castDistance;
                float progress = elapsed / lowerTime;
                weaponDisplay.transform.position = Vector3.Lerp(castPos, basePosition, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 动画结束
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 清理所有斩击特效
        /// </summary>
        private void CleanupAllSlashEffects()
        {
            // 立即停止当前攻击协程
            if (currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
                currentAttackCoroutine = null;
            }
            
            // 查找并销毁所有斩击特效
            foreach (Transform child in transform)
            {
                if (child.name.Contains("SlashEffect") || child.name.Contains("HandKnife") || 
                    child.name.Contains("SectorBg") || child.name.Contains("ArcFlash") ||
                    child.name.Contains("ArcSlash"))
                {
                    Destroy(child.gameObject);
                }
            }
            
            isAttacking = false;
        }
        
        /// <summary>
        /// 空手攻击动画 - 以角色为圆心的弧形刀光
        /// </summary>
        private IEnumerator UnarmedAttackAnimation()
        {
            isAttacking = true;
            float duration = 0.1f; // 短而凌厉
            
            // 先强制清理所有残留特效
            foreach (Transform child in transform)
            {
                if (child.name.Contains("ArcSlash"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            
            // 创建弧形刀光 - 用LineRenderer
            GameObject arcSlash = new GameObject("ArcSlash");
            arcSlash.transform.SetParent(transform);
            arcSlash.transform.position = transform.position; // 以角色为圆心
            
            var lineRenderer = arcSlash.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.sortingOrder = 10;
            lineRenderer.useWorldSpace = true;
            
            // 设置弧形路径 - 以角色为圆心，正前方的弧形
            int pointCount = 12;
            lineRenderer.positionCount = pointCount;
            
            float radius = 1f;
            // 角色的前方是transform.up方向
            Vector3 forward = transform.up;
            float playerAngle = Mathf.Atan2(forward.y, forward.x); // 角色朝向角度
            float arcSpan = 45f * Mathf.Deg2Rad; // 弧形张角45度
            float startAngle = playerAngle - arcSpan * 0.5f; // 相对前方的左侧
            float endAngle = playerAngle + arcSpan * 0.5f; // 相对前方的右侧
            
            Vector3[] points = new Vector3[pointCount];
            Vector3 center = transform.position; // 以角色为圆心
            
            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)(pointCount - 1);
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                
                points[i] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0
                );
            }
            lineRenderer.SetPositions(points);
            
            // 设置线条属性 - 更细
            lineRenderer.startWidth = 0.02f;
            lineRenderer.endWidth = 0.02f;
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;
            
            float elapsed = 0f;
            while (elapsed < duration && arcSlash != null)
            {
                float progress = elapsed / duration;
                
                // 凌厉感：模糊到锐利的突变
                float alpha;
                if (progress < 0.3f)
                {
                    alpha = progress / 0.3f * 0.4f; // 模糊出现
                }
                else if (progress < 0.6f)
                {
                    alpha = 1f; // 突然锐利
                }
                else
                {
                    alpha = 1f - (progress - 0.6f) / 0.4f; // 快速消失
                }
                
                // 统一设置颜色
                lineRenderer.startColor = new Color(1f, 1f, 1f, alpha);
                lineRenderer.endColor = new Color(1f, 1f, 1f, alpha);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 强制清理
            if (arcSlash != null) 
            {
                DestroyImmediate(arcSlash);
            }
            
            isAttacking = false;
            currentAttackCoroutine = null;
        }
    }
}