using UnityEngine;
using System.Linq;
using AI.Core;
using Enemy;

namespace Camera
{
    /// <summary>
    /// Simple camera controller that works like PlayerController2D
    /// </summary>
    public class SimpleCameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private float cameraZ = -10f;
        [SerializeField] private float moveSpeed = 30f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 50f;
        
        [Header("View Modes")]
        [SerializeField] private bool isFollowing = false;
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool isControllingEnemy = false;
        
        [Header("UI")]
        [SerializeField] private GameObject overviewPanel;
        [SerializeField] private GameObject aiStatusPanel;
        [SerializeField] private GameObject enemyHealthBar;
        
        [Header("Detection")]
        [SerializeField] private LayerMask detectableLayers = -1; // 默认检测所有层
        [SerializeField] private float clickDetectionRadius = 2.5f; // 点击检测半径
        
        // Camera reference
        private UnityEngine.Camera mainCamera;
        private float currentZoom = 20f;
        
        // Double click detection
        private float lastClickTime = 0f;
        private GameObject lastClickedObject = null;
        private float doubleClickTime = 0.5f; // 增加到0.5秒，更容易双击
        
        // Debug visualization
        private Vector2 lastClickWorldPos;
        private float debugClickVisualTime = 0f;
        
        // Current follow targets
        private AIBrain currentAI;
        private Enemy2D currentEnemy;
        
        // UI references
        private TMPro.TextMeshProUGUI aiStatusText;
        private UnityEngine.UI.Slider enemyHealthSlider;
        
        private void Awake()
        {
            mainCamera = GetComponent<UnityEngine.Camera>();
            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
            }
            
            if (mainCamera != null)
            {
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = currentZoom;
            }
            
        }
        
        private void Update()
        {
            try
            {
                // ESC to stop following
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    StopFollowing();
                }
                
                // Handle double click (only in non-control mode)
                if (Input.GetMouseButtonDown(0) && !isControllingEnemy)
                {
                    HandleDoubleClick();
                }
                
                // Tab key for overview panel
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    ToggleOverviewPanel();
                }
                
                if (!isFollowing)
                {
                    // Free camera movement
                    HandleMovement();
                    HandleZoom();
                }
                else if (isControllingEnemy && currentEnemy != null)
                {
                    // Control enemy movement
                    HandleEnemyControl();
                }
                
                // Zoom always works
                HandleZoom();
                
                // Update UI
                UpdateUI();
            }
            catch (System.Exception e)
            {
                // 不使用LogError避免触发Unity的Error Pause
                UnityEngine.Debug.LogWarning($"[SimpleCameraController] Update error: {e.Message}");
            }
        }
        
        private void LateUpdate()
        {
            if (isFollowing && followTarget != null)
            {
                // Follow target like PlayerController2D does
                Vector3 targetPos = followTarget.position;
                targetPos.z = cameraZ;
                
                transform.position = Vector3.Lerp(transform.position, targetPos, 0.1f);
            }
        }
        
        private void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            if (horizontal != 0 || vertical != 0)
            {
                Vector3 movement = new Vector3(horizontal, vertical, 0) * moveSpeed * Time.deltaTime;
                transform.position += movement;
                
                // Keep Z position fixed
                Vector3 pos = transform.position;
                pos.z = cameraZ;
                transform.position = pos;
            }
            
            // Debug - check if input is working
            if (Input.GetKeyDown(KeyCode.F10))
            {
                Debug.Log($"[SimpleCameraController] Input H:{horizontal} V:{vertical}, Pos:{transform.position}, Component enabled:{enabled}");
            }
        }
        
        private void HandleEnemyControl()
        {
            if (currentEnemy == null) return;
            
            // Get movement input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            if (horizontal != 0 || vertical != 0)
            {
                Vector2 movement = new Vector2(horizontal, vertical).normalized;
                
                // Get enemy's movement speed using reflection
                var moveSpeedField = typeof(Enemy2D).GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                float moveSpeed = moveSpeedField != null ? (float)moveSpeedField.GetValue(currentEnemy) : 3.5f;
                
                // Move the enemy
                currentEnemy.transform.position += (Vector3)(movement * moveSpeed * Time.deltaTime);
                
                // Rotate to face movement direction
                if (movement.magnitude > 0.1f)
                {
                    float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg - 90f;
                    currentEnemy.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
            
            // Attack on left click
            if (Input.GetMouseButtonDown(0))
            {
                // Make enemy face mouse position before attacking
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                Vector2 direction = (mouseWorldPos - currentEnemy.transform.position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                currentEnemy.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                
                // Check if it's a ranged enemy
                var rangedEnemy = currentEnemy as RangedEnemy2D;
                if (rangedEnemy != null)
                {
                    // Try to call RangedAttack method
                    var rangedAttackMethod = typeof(RangedEnemy2D).GetMethod("RangedAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (rangedAttackMethod != null)
                    {
                        rangedAttackMethod.Invoke(rangedEnemy, null);
                    }
                }
                else
                {
                    // Regular enemy - try combat system first
                    var combatSystem = currentEnemy.GetComponent<Combat.CombatSystem2D>();
                    if (combatSystem != null)
                    {
                        combatSystem.PerformAttack();
                    }
                    else
                    {
                        // Try to call enemy's attack method using reflection
                        var attackMethod = typeof(Enemy2D).GetMethod("Attack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (attackMethod != null)
                        {
                            attackMethod.Invoke(currentEnemy, null);
                        }
                    }
                }
            }
        }
        
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                currentZoom -= scroll * zoomSpeed;
                currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
                
                if (mainCamera != null)
                {
                    mainCamera.orthographicSize = currentZoom;
                }
            }
        }
        
        private void HandleDoubleClick()
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);
            
            // Store for debug visualization
            lastClickWorldPos = mousePos2D;
            debugClickVisualTime = Time.time + 1f; // Show for 1 second
            
            // Check for AI or Enemy with configurable radius
            float searchRadius = clickDetectionRadius;
            
            // 如果没有设置检测层，默认检测所有层
            if (detectableLayers == -1)
            {
                detectableLayers = ~0; // All layers
            }
            
            Collider2D[] colliders = Physics2D.OverlapCircleAll(mousePos2D, searchRadius, detectableLayers);
            
            GameObject clickedObject = null;
            
            // Find the most relevant object (AI or Enemy)
            foreach (var collider in colliders)
            {
                // Check for AI
                var aiBrain = collider.GetComponent<AIBrain>();
                if (aiBrain != null)
                {
                    clickedObject = collider.gameObject;
                    break;
                }
                
                // Check for Enemy (includes both Enemy2D and RangedEnemy2D)
                var enemy = collider.GetComponent<Enemy2D>();
                if (enemy != null)
                {
                    clickedObject = collider.gameObject;
                    break;
                }
            }
            
            if (clickedObject != null)
            {
                // Check double click timing
                if (lastClickedObject == clickedObject && Time.time - lastClickTime < doubleClickTime)
                {
                    // Double clicked!
                    // Check if it's an AI
                    var ai = clickedObject.GetComponent<AIBrain>();
                    if (ai != null)
                    {
                        isFollowing = true;
                        followTarget = clickedObject.transform;
                        currentAI = ai;
                        currentEnemy = null;
                        currentZoom = 10f;
                        if (mainCamera != null)
                            mainCamera.orthographicSize = currentZoom;
                        ShowAIStatusPanel(true);
                        ShowEnemyHealthBar(false);
                    }
                    // Check if it's an enemy
                    var enemy = clickedObject.GetComponent<Enemy2D>();
                    if (enemy != null)
                    {
                        isFollowing = true;
                        followTarget = clickedObject.transform;
                        currentEnemy = enemy;
                        currentAI = null;
                        currentZoom = 8f;
                        if (mainCamera != null)
                            mainCamera.orthographicSize = currentZoom;
                        ShowAIStatusPanel(false);
                        ShowEnemyHealthBar(true);
                        
                        // If holding Ctrl, enable control mode
                        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                        {
                            isControllingEnemy = true;
                            // Don't disable the entire enemy component, just stop its AI behavior
                            // enemy.enabled = false; // This might cause issues
                            var enemyAI = enemy.GetComponent<AI.Core.AIBrain>();
                            if (enemyAI != null) enemyAI.enabled = false;
                        }
                    }
                    
                    lastClickedObject = null; // Reset
                }
                else
                {
                    lastClickedObject = clickedObject;
                    lastClickTime = Time.time;
                }
            }
        }
        
        private void ToggleOverviewPanel()
        {
            // Create panel if not exists
            if (overviewPanel == null)
            {
                CreateOverviewPanel();
            }
            
            if (overviewPanel != null)
            {
                bool newState = !overviewPanel.activeSelf;
                overviewPanel.SetActive(newState);
                UnityEngine.Debug.Log($"[SimpleCameraController] Overview panel toggled to: {newState}");
            }
        }
        
        private void CreateOverviewPanel()
        {
            // Find or create canvas
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null)
            {
                canvas = new GameObject("GameCanvas");
                var canvasComp = canvas.AddComponent<Canvas>();
                canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasComp.sortingOrder = 0; // 基础层级
                var scaler = canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Create simple overview panel
            overviewPanel = new GameObject("OverviewPanel");
            overviewPanel.transform.SetParent(canvas.transform);
            
            var rect = overviewPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.1f);  // 从屏幕20%开始
            rect.anchorMax = new Vector2(0.8f, 0.9f);  // 到屏幕80%结束
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // Background with subtle styling
            var bg = overviewPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f); // 深蓝灰色背景，更美观
            
            // Title
            var title = new GameObject("Title");
            title.transform.SetParent(overviewPanel.transform);
            var titleText = title.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "AI总览";
            titleText.fontSize = 16; // 减小标题字体
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            
            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(overviewPanel.transform);
            var contentText = content.AddComponent<TMPro.TextMeshProUGUI>();
            contentText.fontSize = 11; // 减小内容字体，为4个AI留空间
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.1f, 0.1f);
            contentRect.anchorMax = new Vector2(0.9f, 0.85f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            // Add update component
            var updater = overviewPanel.AddComponent<SimpleOverviewUpdater>();
            updater.contentText = contentText;
            
            // Start hidden
            overviewPanel.SetActive(false);
        }
        
        private void StopFollowing()
        {
            isFollowing = false;
            followTarget = null;
            currentAI = null;
            
            // If controlling enemy, re-enable its AI
            if (isControllingEnemy && currentEnemy != null)
            {
                // Re-enable AI component if it exists
                var enemyAI = currentEnemy.GetComponent<AI.Core.AIBrain>();
                if (enemyAI != null) enemyAI.enabled = true;
                isControllingEnemy = false;
            }
            
            currentEnemy = null;
            ShowAIStatusPanel(false);
            ShowEnemyHealthBar(false);
        }
        
        private void ShowAIStatusPanel(bool show)
        {
            if (aiStatusPanel == null && show)
            {
                CreateAIStatusPanel();
            }
            
            if (aiStatusPanel != null)
            {
                aiStatusPanel.SetActive(show);
            }
        }
        
        private void ShowEnemyHealthBar(bool show)
        {
            if (enemyHealthBar == null && show)
            {
                CreateEnemyHealthBar();
            }
            
            if (enemyHealthBar != null)
            {
                enemyHealthBar.SetActive(show);
            }
        }
        
        private void CreateAIStatusPanel()
        {
            // Find or create canvas
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null)
            {
                canvas = new GameObject("GameCanvas");
                var canvasComp = canvas.AddComponent<Canvas>();
                canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Create AI status panel
            aiStatusPanel = new GameObject("AIStatusPanel");
            aiStatusPanel.transform.SetParent(canvas.transform);
            
            var rect = aiStatusPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);      // 左上角anchor点
            rect.anchorMax = new Vector2(0f, 1f);      // 同样是左上角anchor点
            rect.anchoredPosition = new Vector2(0, 0);  // 相对于anchor的位置
            rect.sizeDelta = new Vector2(300, 200);     // 面板的实际大小
            rect.pivot = new Vector2(0, 1);             // 设置pivot为左上角
            
            // Background with gradient-like styling
            var bg = aiStatusPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.05f, 0.1f, 0.2f, 0.8f); // 深蓝色背景
            
            // Status text
            var statusTextGO = new GameObject("StatusText");
            statusTextGO.transform.SetParent(aiStatusPanel.transform);
            aiStatusText = statusTextGO.AddComponent<TMPro.TextMeshProUGUI>();
            aiStatusText.fontSize = 10; // 更小的字体适合紧凑布局
            aiStatusText.color = Color.white;
            
            // 配置字体
            
            var textRect = statusTextGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 5);  // 减小内边距
            textRect.offsetMax = new Vector2(-5, -5);
        }
        
        private void CreateEnemyHealthBar()
        {
            // Find or create canvas
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null)
            {
                canvas = new GameObject("GameCanvas");
                var canvasComp = canvas.AddComponent<Canvas>();
                canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Create enemy health bar
            enemyHealthBar = new GameObject("EnemyHealthBar");
            enemyHealthBar.transform.SetParent(canvas.transform);
            
            var rect = enemyHealthBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.95f);  // 更宽一些
            rect.anchorMax = new Vector2(0.75f, 0.97f);  // 更细一些
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // Background with subtle dark theme
            var bg = enemyHealthBar.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // 深灰色背景，更低调
            
            // 简单的血条，不用复杂的Slider
            var healthBarGO = new GameObject("HealthBarDisplay");
            healthBarGO.transform.SetParent(enemyHealthBar.transform);
            
            var healthBarRect = healthBarGO.GetComponent<RectTransform>();
            if (healthBarRect == null) healthBarRect = healthBarGO.AddComponent<RectTransform>();
            healthBarRect.anchorMin = new Vector2(0.1f, 0.3f);
            healthBarRect.anchorMax = new Vector2(0.9f, 0.7f);
            healthBarRect.offsetMin = Vector2.zero;
            healthBarRect.offsetMax = Vector2.zero;
            
            // 血条背景
            var bgImage = healthBarGO.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            
            // 血条填充
            var fillGO = new GameObject("HealthFill");
            fillGO.transform.SetParent(healthBarGO.transform);
            var fillRect = fillGO.GetComponent<RectTransform>();
            if (fillRect == null) fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f); // 初始满血
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            var fillImage = fillGO.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // 绿色
            
            // 创建一个简单的slider来控制fillRect的宽度
            enemyHealthSlider = enemyHealthBar.AddComponent<UnityEngine.UI.Slider>();
            enemyHealthSlider.fillRect = fillRect;
            enemyHealthSlider.interactable = false;
            enemyHealthSlider.minValue = 0f;
            enemyHealthSlider.maxValue = 1f;
            enemyHealthSlider.value = 1f;
            
            Debug.Log($"[SimpleCameraController] 创建简化血条，fillImage颜色: {fillImage.color}");
        }
        
        private void UpdateUI()
        {
            try
            {
                // Update AI status panel
                if (currentAI != null && aiStatusText != null)
                {
                var aiStats = currentAI.GetComponent<AI.Stats.AIStats>();
                var aiController = currentAI.GetComponent<AI.Core.AIController>();
                
                if (aiStats != null)
                {
                    string status = $"<b>{currentAI.name}</b>\n";
                    
                    // Basic stats - 紧凑显示
                    status += $"<color=#90EE90>生命:{aiStats.GetStat(AI.Stats.StatType.Health):F0}/{aiStats.Config?.maxHealth:F0}</color> ";
                    status += $"<color=#FFA500>饥饿:{aiStats.GetStat(AI.Stats.StatType.Hunger):F0}</color>\n";
                    status += $"<color=#87CEEB>口渴:{aiStats.GetStat(AI.Stats.StatType.Thirst):F0}</color> ";
                    status += $"<color=#FFFF00>体力:{aiStats.GetStat(AI.Stats.StatType.Stamina):F0}</color>\n";
                    
                    // Mood - 一行显示
                    status += $"情绪:{aiStats.GetMood(AI.Stats.MoodDimension.Emotion):F1} ";
                    status += $"社交:{aiStats.GetMood(AI.Stats.MoodDimension.Social):F1} ";
                    status += $"心态:{aiStats.GetMood(AI.Stats.MoodDimension.Mentality):F1}\n";
                    
                    // Current state and target - 一行显示
                    var stateField = typeof(AIBrain).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stateField != null)
                    {
                        var state = stateField.GetValue(currentAI);
                        status += $"<color=#FF69B4>状态:{state}</color>";
                    }
                    
                    // Target - 同一行
                    var targetField = typeof(AIController).GetField("currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (aiController != null && targetField != null)
                    {
                        var target = targetField.GetValue(aiController) as GameObject;
                        if (target != null)
                        {
                            status += $" <color=#FFFF00>目标:{target.name}</color>";
                        }
                    }
                    status += "\n";
                    
                    status += "<b>DeepSeek决策</b>\n";
                    
                    // Get DeepSeek decision info - 紧凑显示
                    var lastDecisionField = typeof(AIBrain).GetField("lastDecision", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (lastDecisionField != null)
                    {
                        var lastDecision = lastDecisionField.GetValue(currentAI) as AI.Decision.AIDecision;
                        if (lastDecision != null && !string.IsNullOrEmpty(lastDecision.Explanation))
                        {
                            // 截取前40个字符
                            string shortPlan = lastDecision.Explanation.Length > 40 ? 
                                              lastDecision.Explanation.Substring(0, 40) + "..." : 
                                              lastDecision.Explanation;
                            status += $"<color=#98FB98>规划:{shortPlan}</color>\n";
                        }
                        else
                        {
                            status += "<color=#808080>规划:暂无</color>\n";
                        }
                    }
                    else
                    {
                        status += "<color=#808080>规划:暂无</color>\n";
                    }
                    
                    // Get recent communications - 只显示最新一条
                    var memoryField = typeof(AIBrain).GetField("memory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (memoryField != null)
                    {
                        var memory = memoryField.GetValue(currentAI) as AI.Core.AIMemory;
                        if (memory != null)
                        {
                            var recentEvents = memory.GetRecentEvents(1);
                            var commEvents = recentEvents.Where(e => e.EventType == AI.Core.EventType.Communication).ToList();
                            
                            if (commEvents.Count > 0)
                            {
                                string dialogue = commEvents[0].Description.Length > 35 ?
                                                commEvents[0].Description.Substring(0, 35) + "..." :
                                                commEvents[0].Description;
                                status += $"<color=#DDA0DD>对话:{dialogue}</color>\n";
                            }
                            else
                            {
                                status += "<color=#808080>对话:暂无</color>\n";
                            }
                        }
                    }
                    
                    aiStatusText.text = status;
                }
            }
            
            // Update enemy health bar
            if (currentEnemy != null && enemyHealthSlider != null)
            {
                // Use reflection to get health values since they are protected/private
                var healthField = typeof(Enemy2D).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var maxHealthField = typeof(Enemy2D).GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
                if (healthField != null && maxHealthField != null)
                {
                    float currentHealth = (float)healthField.GetValue(currentEnemy);
                    float maxHealth = (float)maxHealthField.GetValue(currentEnemy);
                    float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0;
                    enemyHealthSlider.value = healthPercent;
                    
                    // Show control mode indicator with smaller text
                    if (isControllingEnemy && enemyHealthBar != null)
                    {
                        var title = enemyHealthBar.transform.Find("Title");
                        if (title == null)
                        {
                            var titleGO = new GameObject("Title");
                            titleGO.transform.SetParent(enemyHealthBar.transform);
                            var titleText = titleGO.AddComponent<TMPro.TextMeshProUGUI>();
                            
                            // Show enemy type with smaller font
                            string enemyType = currentEnemy is RangedEnemy2D ? "远程敌人" : "近战敌人";
                            titleText.text = $"{enemyType} - 控制模式 - WASD移动，左键攻击";
                            
                            titleText.fontSize = 10; // 减小字体大小
                            titleText.alignment = TMPro.TextAlignmentOptions.Center;
                            titleText.color = Color.white;
                            var titleRect = titleGO.GetComponent<RectTransform>();
                            titleRect.anchorMin = new Vector2(0, -0.5f);
                            titleRect.anchorMax = new Vector2(1, 0);
                            titleRect.offsetMin = Vector2.zero;
                            titleRect.offsetMax = Vector2.zero;
                        }
                    }
                
                    // 直接通过血条GameObject查找fillImage
                    var healthFill = enemyHealthBar.transform.Find("HealthBarDisplay/HealthFill");
                    if (healthFill != null)
                    {
                        var fillImage = healthFill.GetComponent<UnityEngine.UI.Image>();
                        if (fillImage != null)
                        {
                            Color newColor;
                            if (healthPercent > 0.6f)
                            {
                                // 健康：鲜绿色
                                newColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                            }
                            else if (healthPercent > 0.3f)
                            {
                                // 受伤：橙黄色
                                newColor = new Color(1f, 0.6f, 0.1f, 1f);
                            }
                            else
                            {
                                // 危险：鲜红色
                                newColor = new Color(0.9f, 0.1f, 0.1f, 1f);
                            }
                            
                            fillImage.color = newColor;
                            
                            // 调试信息
                            if (Time.frameCount % 60 == 0) // 每秒输出一次
                            {
                                Debug.Log($"[SimpleCameraController] 血条更新: 敌人{currentEnemy.name}, 当前血量{currentHealth:F0}/{maxHealth:F0} ({healthPercent:P0}), 颜色{newColor}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[SimpleCameraController] HealthFill的Image组件为null!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[SimpleCameraController] 找不到HealthFill对象!");
                    }
                }
            }
            }
            catch (System.Exception e)
            {
                // 不使用LogError避免触发Unity的Error Pause
                UnityEngine.Debug.LogWarning($"[SimpleCameraController] UpdateUI error: {e.Message}");
            }
        }
        
        // Debug visualization
        private void OnDrawGizmos()
        {
            // Show click detection area
            if (Time.time < debugClickVisualTime)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(lastClickWorldPos, 1.5f);
                
                // Draw cross at click position
                Gizmos.color = Color.red;
                float crossSize = 0.5f;
                Gizmos.DrawLine(lastClickWorldPos + Vector2.left * crossSize, lastClickWorldPos + Vector2.right * crossSize);
                Gizmos.DrawLine(lastClickWorldPos + Vector2.up * crossSize, lastClickWorldPos + Vector2.down * crossSize);
            }
            
            // Show follow target
            if (isFollowing && followTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(followTarget.position, 0.5f);
                Gizmos.DrawLine(transform.position, followTarget.position);
            }
        }
    }
}