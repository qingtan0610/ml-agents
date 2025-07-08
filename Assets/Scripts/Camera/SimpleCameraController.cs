using UnityEngine;
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
        
        // Camera reference
        private UnityEngine.Camera mainCamera;
        private float currentZoom = 20f;
        
        // Double click detection
        private float lastClickTime = 0f;
        private GameObject lastClickedObject = null;
        private float doubleClickTime = 0.3f;
        
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
            // ESC to stop following
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopFollowing();
            }
            
            // Handle double click
            if (Input.GetMouseButtonDown(0))
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
            
            // Check for AI or Enemy with a larger radius
            float searchRadius = 1.5f; // Increased from 0.5f
            
            // 如果没有设置检测层，默认检测Player和Enemy层
            if (detectableLayers == -1)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                detectableLayers = (1 << playerLayer) | (1 << enemyLayer);
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
                            enemy.enabled = false; // Disable enemy AI
                            UnityEngine.Debug.Log($"[SimpleCameraController] Now controlling enemy: {enemy.name}");
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
                overviewPanel.SetActive(!overviewPanel.activeSelf);
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
                canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Create simple overview panel
            overviewPanel = new GameObject("OverviewPanel");
            overviewPanel.transform.SetParent(canvas.transform);
            
            var rect = overviewPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(50, 50);
            rect.offsetMax = new Vector2(-50, -50);
            
            // Background
            var bg = overviewPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.9f);
            
            // Title
            var title = new GameObject("Title");
            title.transform.SetParent(overviewPanel.transform);
            var titleText = title.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "AI Overview";
            titleText.fontSize = 24;
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
            contentText.fontSize = 16;
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
                currentEnemy.enabled = true;
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
            rect.anchorMin = new Vector2(0, 0.7f);
            rect.anchorMax = new Vector2(0.3f, 1);
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-10, -10);
            
            // Background
            var bg = aiStatusPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.8f);
            
            // Status text
            var statusTextGO = new GameObject("StatusText");
            statusTextGO.transform.SetParent(aiStatusPanel.transform);
            aiStatusText = statusTextGO.AddComponent<TMPro.TextMeshProUGUI>();
            aiStatusText.fontSize = 14;
            aiStatusText.color = Color.white;
            var textRect = statusTextGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
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
            rect.anchorMin = new Vector2(0.3f, 0.9f);
            rect.anchorMax = new Vector2(0.7f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // Background
            var bg = enemyHealthBar.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.2f, 0, 0, 0.8f);
            
            // Health bar
            var healthBarGO = new GameObject("HealthBar");
            healthBarGO.transform.SetParent(enemyHealthBar.transform);
            
            var healthRect = healthBarGO.AddComponent<RectTransform>();
            healthRect.anchorMin = Vector2.zero;
            healthRect.anchorMax = Vector2.one;
            healthRect.offsetMin = new Vector2(2, 2);
            healthRect.offsetMax = new Vector2(-2, -2);
            
            enemyHealthSlider = healthBarGO.AddComponent<UnityEngine.UI.Slider>();
            enemyHealthSlider.fillRect = healthRect;
            
            // Fill image
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(healthBarGO.transform);
            var fillImage = fillGO.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = Color.red;
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            enemyHealthSlider.fillRect = fillRect;
        }
        
        private void UpdateUI()
        {
            // Update AI status panel
            if (currentAI != null && aiStatusText != null)
            {
                var aiStats = currentAI.GetComponent<AI.Stats.AIStats>();
                var aiController = currentAI.GetComponent<AI.Core.AIController>();
                
                if (aiStats != null)
                {
                    string status = $"<b>{currentAI.name}</b>\n\n";
                    
                    // Basic stats
                    status += $"生命: {aiStats.GetStat(AI.Stats.StatType.Health):F0}/{aiStats.Config?.maxHealth:F0}\n";
                    status += $"饥饿: {aiStats.GetStat(AI.Stats.StatType.Hunger):F0}/{aiStats.Config?.maxHunger:F0}\n";
                    status += $"口渴: {aiStats.GetStat(AI.Stats.StatType.Thirst):F0}/{aiStats.Config?.maxThirst:F0}\n";
                    status += $"体力: {aiStats.GetStat(AI.Stats.StatType.Stamina):F0}/{aiStats.Config?.maxStamina:F0}\n\n";
                    
                    // Mood - using GetMood with MoodDimension
                    status += $"情绪: {aiStats.GetMood(AI.Stats.MoodDimension.Emotion):F1}\n";
                    status += $"社交: {aiStats.GetMood(AI.Stats.MoodDimension.Social):F1}\n";
                    status += $"心态: {aiStats.GetMood(AI.Stats.MoodDimension.Mentality):F1}\n\n";
                    
                    // Current state - using reflection to access private field
                    var stateField = typeof(AIBrain).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stateField != null)
                    {
                        var state = stateField.GetValue(currentAI);
                        status += $"状态: {state}\n";
                    }
                    
                    // Target - using reflection to access private field
                    var targetField = typeof(AIController).GetField("currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (aiController != null && targetField != null)
                    {
                        var target = targetField.GetValue(aiController) as GameObject;
                        if (target != null)
                        {
                            status += $"目标: {target.name}\n";
                        }
                    }
                    
                    aiStatusText.text = status;
                }
            }
            
            // Update enemy health bar
            if (currentEnemy != null && enemyHealthSlider != null)
            {
                // Use reflection to get health values since they are protected
                var healthField = typeof(Enemy2D).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var maxHealthField = typeof(Enemy2D).GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (healthField != null && maxHealthField != null)
                {
                    float currentHealth = (float)healthField.GetValue(currentEnemy);
                    float maxHealth = (float)maxHealthField.GetValue(currentEnemy);
                    float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0;
                    enemyHealthSlider.value = healthPercent;
                    
                    // Show control mode indicator
                    if (isControllingEnemy && enemyHealthBar != null)
                    {
                        var title = enemyHealthBar.transform.Find("Title");
                        if (title == null)
                        {
                            var titleGO = new GameObject("Title");
                            titleGO.transform.SetParent(enemyHealthBar.transform);
                            var titleText = titleGO.AddComponent<TMPro.TextMeshProUGUI>();
                            
                            // Show enemy type
                            string enemyType = currentEnemy is RangedEnemy2D ? "远程敌人" : "近战敌人";
                            titleText.text = $"{enemyType} - 控制模式 - WASD移动，左键攻击";
                            
                            titleText.fontSize = 12;
                            titleText.alignment = TMPro.TextAlignmentOptions.Center;
                            titleText.color = Color.white;
                            var titleRect = titleGO.GetComponent<RectTransform>();
                            titleRect.anchorMin = new Vector2(0, -0.5f);
                            titleRect.anchorMax = new Vector2(1, 0);
                            titleRect.offsetMin = Vector2.zero;
                            titleRect.offsetMax = Vector2.zero;
                        }
                    }
                
                    // Change color based on health
                var fillImage = enemyHealthSlider.fillRect.GetComponent<UnityEngine.UI.Image>();
                if (fillImage != null)
                {
                    if (healthPercent > 0.6f)
                        fillImage.color = Color.green;
                    else if (healthPercent > 0.3f)
                        fillImage.color = Color.yellow;
                    else
                        fillImage.color = Color.red;
                    }
                }
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