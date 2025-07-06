using UnityEngine;
using UnityEngine.UI;
using AI.Stats;

namespace AI.Visual
{
    /// <summary>
    /// AI生命条显示系统 - 在AI头顶显示生命条
    /// </summary>
    public class AIHealthBarDisplay : MonoBehaviour
    {
        [Header("Health Bar Settings")]
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 1.5f, 0);
        [SerializeField] private Vector2 healthBarSize = new Vector2(1.2f, 0.15f);
        [SerializeField] private Color healthColor = Color.green;
        [SerializeField] private Color damageColor = Color.red;
        [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private float hideDelay = 3f; // 满血后多久隐藏
        
        [Header("Components")]
        private AIStats aiStats;
        private Canvas healthCanvas;
        private Image healthFill;
        private Image backgroundImage;
        private GameObject healthBarObject;
        
        // 状态
        private float lastDamageTime;
        private float currentDisplayedHealth;
        private bool isVisible = false;
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            if (aiStats == null)
            {
                Debug.LogError($"[AIHealthBar] {name} 没有找到AIStats组件");
                enabled = false;
                return;
            }
            
            CreateHealthBar();
        }
        
        private void Start()
        {
            if (aiStats != null)
            {
                // 监听统计数据变化
                aiStats.OnStatChanged.AddListener(OnStatChanged);
                // 初始化显示值
                currentDisplayedHealth = aiStats.CurrentHealth;
                UpdateHealthBarDisplay();
            }
        }
        
        private void CreateHealthBar()
        {
            // 创建Canvas
            healthBarObject = new GameObject("AIHealthBar");
            healthBarObject.transform.SetParent(transform);
            healthBarObject.transform.localPosition = healthBarOffset;
            
            healthCanvas = healthBarObject.AddComponent<Canvas>();
            healthCanvas.renderMode = RenderMode.WorldSpace;
            healthCanvas.sortingOrder = 100; // 确保在最前面
            
            var canvasScaler = healthBarObject.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 100;
            
            // 设置Canvas大小
            var rectTransform = healthBarObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = healthBarSize * 100; // 转换为像素
            
            // 创建背景
            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(healthBarObject.transform);
            backgroundObject.transform.localPosition = Vector3.zero;
            backgroundObject.transform.localRotation = Quaternion.identity;
            backgroundObject.transform.localScale = Vector3.one;
            
            backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = backgroundColor;
            backgroundImage.rectTransform.anchorMin = Vector2.zero;
            backgroundImage.rectTransform.anchorMax = Vector2.one;
            backgroundImage.rectTransform.offsetMin = Vector2.zero;
            backgroundImage.rectTransform.offsetMax = Vector2.zero;
            
            // 创建生命条填充
            var healthFillObject = new GameObject("HealthFill");
            healthFillObject.transform.SetParent(backgroundObject.transform);
            healthFillObject.transform.localPosition = Vector3.zero;
            healthFillObject.transform.localRotation = Quaternion.identity;
            healthFillObject.transform.localScale = Vector3.one;
            
            healthFill = healthFillObject.AddComponent<Image>();
            healthFill.color = healthColor;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.rectTransform.anchorMin = Vector2.zero;
            healthFill.rectTransform.anchorMax = Vector2.one;
            healthFill.rectTransform.offsetMin = Vector2.zero;
            healthFill.rectTransform.offsetMax = Vector2.zero;
            
            // 初始隐藏
            healthBarObject.SetActive(false);
        }
        
        private void Update()
        {
            if (!showHealthBar || aiStats == null || aiStats.IsDead)
            {
                if (isVisible)
                {
                    HideHealthBar();
                }
                return;
            }
            
            // 让生命条始终面向相机
            if (Camera.main != null && healthBarObject != null)
            {
                healthBarObject.transform.LookAt(Camera.main.transform);
                healthBarObject.transform.Rotate(0, 180, 0); // 翻转以正确面向相机
            }
            
            // 检查是否需要隐藏生命条
            if (hideWhenFull && aiStats.CurrentHealth >= aiStats.Config.maxHealth)
            {
                if (Time.time - lastDamageTime > hideDelay && isVisible)
                {
                    HideHealthBar();
                }
            }
        }
        
        private void OnStatChanged(StatChangeEventArgs args)
        {
            if (args.statType == StatType.Health)
            {
                // 生命值变化
                if (args.newValue < args.oldValue)
                {
                    // 受到伤害
                    lastDamageTime = Time.time;
                    ShowHealthBar();
                }
                
                UpdateHealthBarDisplay();
            }
        }
        
        private void UpdateHealthBarDisplay()
        {
            if (healthFill == null || aiStats == null) return;
            
            float healthPercent = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            healthFill.fillAmount = healthPercent;
            
            // 根据生命值百分比改变颜色
            if (healthPercent > 0.6f)
            {
                healthFill.color = healthColor; // 绿色
            }
            else if (healthPercent > 0.3f)
            {
                healthFill.color = Color.yellow; // 黄色
            }
            else
            {
                healthFill.color = damageColor; // 红色
            }
            
            currentDisplayedHealth = aiStats.CurrentHealth;
        }
        
        private void ShowHealthBar()
        {
            if (!showHealthBar || healthBarObject == null) return;
            
            healthBarObject.SetActive(true);
            isVisible = true;
        }
        
        private void HideHealthBar()
        {
            if (healthBarObject == null) return;
            
            healthBarObject.SetActive(false);
            isVisible = false;
        }
        
        /// <summary>
        /// 设置是否显示生命条
        /// </summary>
        public void SetHealthBarVisible(bool visible)
        {
            showHealthBar = visible;
            if (!visible)
            {
                HideHealthBar();
            }
            else if (aiStats != null && aiStats.CurrentHealth < aiStats.Config.maxHealth)
            {
                ShowHealthBar();
            }
        }
        
        /// <summary>
        /// 强制显示生命条（用于调试）
        /// </summary>
        public void ForceShowHealthBar()
        {
            lastDamageTime = Time.time;
            ShowHealthBar();
        }
        
        private void OnDestroy()
        {
            if (aiStats != null)
            {
                aiStats.OnStatChanged.RemoveListener(OnStatChanged);
            }
        }
        
        // 调试绘制
        private void OnDrawGizmosSelected()
        {
            // 绘制生命条位置
            Gizmos.color = Color.red;
            Vector3 healthBarPos = transform.position + healthBarOffset;
            Gizmos.DrawWireCube(healthBarPos, new Vector3(healthBarSize.x, healthBarSize.y, 0.1f));
        }
    }
}