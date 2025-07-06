using UnityEngine;
using System.Collections;
using Inventory;
using Inventory.Items;

namespace AI.Visual
{
    /// <summary>
    /// AI武器视觉显示系统 - 基于玩家WeaponVisualDisplay的AI版本
    /// </summary>
    public class AIWeaponVisualDisplay : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private float weaponDistance = 0.8f; // 武器距离AI的距离
        [SerializeField] private Vector2 weaponOffset = Vector2.right; // 武器相对于AI的偏移
        [SerializeField] private float weaponScale = 0.6f; // 武器图标缩放
        [SerializeField] private float targetIconSize = 32f; // 目标图标大小（像素）
        [SerializeField] private bool autoScaleIcon = true; // 是否自动缩放图标
        
        [Header("Animation Settings")]
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
            weaponDisplay = new GameObject("AIWeaponDisplay");
            weaponDisplay.transform.SetParent(transform);
            
            // 添加SpriteRenderer
            weaponRenderer = weaponDisplay.AddComponent<SpriteRenderer>();
            weaponRenderer.sortingOrder = 1; // 显示在AI前面
            
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
            
            // 基础位置：AI位置 + 根据AI朝向旋转的偏移
            Vector3 rotatedOffset = transform.rotation * (Vector3)(weaponOffset.normalized * weaponDistance);
            basePosition = transform.position + rotatedOffset;
            
            if (!isAttacking)
            {
                weaponDisplay.transform.position = basePosition;
                // 武器图标也跟随AI朝向旋转
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
                Debug.Log($"[AIWeaponVisual] {name} weapon unequipped, hiding display");
            }
            else
            {
                // 有武器，显示图标
                ShowWeapon(newWeapon);
                Debug.Log($"[AIWeaponVisual] {name} weapon equipped: {newWeapon.ItemName}");
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
                    weaponDisplay.transform.localScale = Vector3.one * weaponScale;
                    baseScale = weaponDisplay.transform.localScale;
                }
                
                Debug.Log($"[AIWeaponVisual] {name} displaying weapon icon: {weapon.ItemName} (scale: {weaponDisplay.transform.localScale})");
            }
            else
            {
                Debug.LogWarning($"[AIWeaponVisual] {name} weapon {weapon.ItemName} has no icon");
                weaponDisplay.SetActive(false);
            }
        }
        
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
            
            // 计算目标世界大小
            float targetWorldSize = targetIconSize / 32f * weaponScale; // 应用武器缩放
            
            // 计算需要的缩放
            float finalScale = targetWorldSize / currentWorldSize;
            
            // 应用缩放
            weaponDisplay.transform.localScale = Vector3.one * finalScale;
            baseScale = weaponDisplay.transform.localScale;
        }
        
        /// <summary>
        /// 播放攻击动画 - 从AIController调用
        /// </summary>
        public void PlayAttackAnimation()
        {
            // 停止当前动画
            if (isAttacking && currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
                currentAttackCoroutine = null;
                isAttacking = false;
            }
            
            // 空手攻击特效
            if (currentWeapon == null)
            {
                Debug.Log($"[AIWeaponVisual] {name} playing unarmed attack animation");
                currentAttackCoroutine = StartCoroutine(UnarmedAttackAnimation());
                return;
            }
            
            if (weaponDisplay == null) return;
            
            Debug.Log($"[AIWeaponVisual] {name} playing attack animation for {currentWeapon.ItemName} ({currentWeapon.WeaponType}, {currentWeapon.AttackShape})");
            
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
        /// 圆形攻击动画
        /// </summary>
        private IEnumerator CircleAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float elapsed = 0f;
            
            float actualRadius = currentWeapon.AttackRange;
            
            while (elapsed < duration)
            {
                Vector3 aiPos = transform.position;
                float aiAngle = transform.eulerAngles.z * Mathf.Deg2Rad;
                
                float progress = elapsed / duration;
                float currentAngle = aiAngle + (progress * 360f * Mathf.Deg2Rad);
                
                Vector3 circlePos = aiPos + new Vector3(
                    Mathf.Cos(currentAngle) * actualRadius,
                    Mathf.Sin(currentAngle) * actualRadius,
                    0
                );
                
                weaponDisplay.transform.position = circlePos;
                Vector3 directionToAI = (aiPos - circlePos).normalized;
                float weaponAngle = Mathf.Atan2(directionToAI.y, directionToAI.x) * Mathf.Rad2Deg - 90f;
                weaponDisplay.transform.rotation = Quaternion.AngleAxis(weaponAngle, Vector3.forward);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 直线刺击动画
        /// </summary>
        private IEnumerator ThrustAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float halfDuration = duration * 0.5f;
            
            float actualThrustDistance = currentWeapon.AttackRange;
            
            // 刺出阶段
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                UpdateWeaponPosition();
                Vector3 startPos = basePosition;
                Vector3 thrustDirection = transform.up;
                Vector3 thrustPos = startPos + thrustDirection * actualThrustDistance;
                
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.position = Vector3.Lerp(startPos, thrustPos, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 收回阶段
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                UpdateWeaponPosition();
                Vector3 startPos = basePosition;
                Vector3 thrustDirection = transform.up;
                Vector3 thrustPos = startPos + thrustDirection * actualThrustDistance;
                
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.position = Vector3.Lerp(thrustPos, startPos, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 远程武器动画
        /// </summary>
        private IEnumerator RangedAttackAnimation()
        {
            isAttacking = true;
            float duration = Mathf.Max(1f / currentWeapon.AttackSpeed, 0.5f);
            float moveTime = duration * 0.2f;
            float drawTime = duration * 0.2f;
            float holdTime = duration * 0.2f;
            float releaseTime = duration * 0.2f;
            float returnTime = duration * 0.2f;
            
            float aimDistance = 0.5f;
            float drawBackDistance = 0.3f;
            
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
            
            // 2. 后拉
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
            
            // 3. 保持
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
            
            // 4. 发射
            elapsed = 0f;
            while (elapsed < releaseTime)
            {
                UpdateWeaponPosition();
                Vector3 drawPos = transform.position + transform.up * (aimDistance - drawBackDistance);
                Vector3 shootPos = transform.position + transform.up * (aimDistance + 0.2f);
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
            
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 扇形攻击动画
        /// </summary>
        private IEnumerator SectorAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float elapsed = 0f;
            
            float actualRadius = currentWeapon.AttackRange;
            float sectorAngle = currentWeapon.SectorAngle * Mathf.Deg2Rad;
            
            while (elapsed < duration)
            {
                Vector3 aiPos = transform.position;
                float aiAngle = transform.eulerAngles.z * Mathf.Deg2Rad;
                
                float startAngle = aiAngle - sectorAngle * 0.5f;
                float endAngle = aiAngle + sectorAngle * 0.5f;
                
                float progress = elapsed / duration;
                float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);
                
                Vector3 sectorPos = aiPos + new Vector3(
                    Mathf.Cos(currentAngle) * actualRadius,
                    Mathf.Sin(currentAngle) * actualRadius,
                    0
                );
                
                weaponDisplay.transform.position = sectorPos;
                
                Vector3 direction = (sectorPos - aiPos).normalized;
                float weaponAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                weaponDisplay.transform.rotation = Quaternion.AngleAxis(weaponAngle, Vector3.forward);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 矩形攻击动画
        /// </summary>
        private IEnumerator RectangleAttackAnimation()
        {
            isAttacking = true;
            float duration = 1f / currentWeapon.AttackSpeed;
            float elapsed = 0f;
            
            float actualWidth = currentWeapon.RectangleWidth;
            float forwardDistance = currentWeapon.AttackRange * 0.5f;
            
            while (elapsed < duration)
            {
                Vector3 aiPos = transform.position;
                Vector3 aiForward = transform.up;
                Vector3 aiRight = transform.right;
                
                Vector3 rectangleCenter = aiPos + aiForward * forwardDistance;
                Vector3 sweepLeft = rectangleCenter - aiRight * (actualWidth * 0.5f);
                Vector3 sweepRight = rectangleCenter + aiRight * (actualWidth * 0.5f);
                
                float progress = elapsed / duration;
                weaponDisplay.transform.position = Vector3.Lerp(sweepLeft, sweepRight, progress);
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 默认攻击动画
        /// </summary>
        private IEnumerator DefaultAttackAnimation()
        {
            isAttacking = true;
            float duration = Mathf.Max(1f / currentWeapon.AttackSpeed, 0.5f);
            float halfDuration = duration * 0.5f;
            
            Vector3 enlargedScale = baseScale * 1.3f;
            
            // 放大阶段
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                float progress = elapsed / halfDuration;
                weaponDisplay.transform.localScale = Vector3.Lerp(baseScale, enlargedScale, progress);
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
                weaponDisplay.transform.rotation = transform.rotation;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            weaponDisplay.transform.localScale = baseScale;
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 魔法武器动画
        /// </summary>
        private IEnumerator MagicAttackAnimation()
        {
            isAttacking = true;
            float duration = Mathf.Max(1f / currentWeapon.AttackSpeed, 0.5f);
            float raiseTime = duration * 0.3f;
            float castTime = duration * 0.4f;
            float lowerTime = duration * 0.3f;
            
            float castDistance = 0.6f;
            
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
            
            // 2. 施法
            elapsed = 0f;
            while (elapsed < castTime)
            {
                UpdateWeaponPosition();
                Vector3 castPos = transform.position + transform.up * castDistance;
                weaponDisplay.transform.position = castPos;
                weaponDisplay.transform.rotation = transform.rotation;
                // 轻微震动效果
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
            
            UpdateWeaponPosition();
            isAttacking = false;
            
            if (enableTrailEffect && weaponTrail != null)
            {
                StartCoroutine(DisableTrailAfterDelay());
            }
            currentAttackCoroutine = null;
        }
        
        /// <summary>
        /// 空手攻击动画
        /// </summary>
        private IEnumerator UnarmedAttackAnimation()
        {
            isAttacking = true;
            float duration = 0.1f;
            
            // 清理残留特效
            foreach (Transform child in transform)
            {
                if (child.name.Contains("ArcSlash"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            
            // 创建弧形刀光
            GameObject arcSlash = new GameObject("ArcSlash");
            arcSlash.transform.SetParent(transform);
            arcSlash.transform.position = transform.position;
            
            var lineRenderer = arcSlash.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.sortingOrder = 10;
            lineRenderer.useWorldSpace = true;
            
            // 设置弧形路径
            int pointCount = 12;
            lineRenderer.positionCount = pointCount;
            
            float radius = 1f;
            Vector3 forward = transform.up;
            float aiAngle = Mathf.Atan2(forward.y, forward.x);
            float arcSpan = 45f * Mathf.Deg2Rad;
            float startAngle = aiAngle - arcSpan * 0.5f;
            float endAngle = aiAngle + arcSpan * 0.5f;
            
            Vector3[] points = new Vector3[pointCount];
            Vector3 center = transform.position;
            
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
            
            // 设置线条属性
            lineRenderer.startWidth = 0.02f;
            lineRenderer.endWidth = 0.02f;
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;
            
            float elapsed = 0f;
            while (elapsed < duration && arcSlash != null)
            {
                float progress = elapsed / duration;
                
                // 凌厉感效果
                float alpha;
                if (progress < 0.3f)
                {
                    alpha = progress / 0.3f * 0.4f;
                }
                else if (progress < 0.6f)
                {
                    alpha = 1f;
                }
                else
                {
                    alpha = 1f - (progress - 0.6f) / 0.4f;
                }
                
                lineRenderer.startColor = new Color(1f, 1f, 1f, alpha);
                lineRenderer.endColor = new Color(1f, 1f, 1f, alpha);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 清理
            if (arcSlash != null) 
            {
                DestroyImmediate(arcSlash);
            }
            
            isAttacking = false;
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
                    float halfAngle = currentWeapon.SectorAngle * 0.5f * Mathf.Deg2Rad;
                    Vector3 leftDir = Quaternion.Euler(0, 0, -currentWeapon.SectorAngle * 0.5f) * transform.up;
                    Vector3 rightDir = Quaternion.Euler(0, 0, currentWeapon.SectorAngle * 0.5f) * transform.up;
                    Gizmos.DrawLine(transform.position, transform.position + leftDir * currentWeapon.AttackRange);
                    Gizmos.DrawLine(transform.position, transform.position + rightDir * currentWeapon.AttackRange);
                    break;
                    
                case AttackShape.Rectangle:
                    Vector3 center = transform.position + transform.up * (currentWeapon.AttackRange * 0.5f);
                    Vector3 halfSize = new Vector3(currentWeapon.RectangleWidth * 0.5f, currentWeapon.AttackRange * 0.5f, 0);
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, halfSize * 2f);
                    Gizmos.matrix = oldMatrix;
                    break;
            }
        }
    }
}