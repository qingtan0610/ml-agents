using UnityEngine;
using System.Collections;
using AI.Core;
using Enemy;

namespace Camera
{
    /// <summary>
    /// Unified camera management system
    /// Handles God View, AI Following, Enemy Control
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        public enum CameraMode
        {
            GodView,      // Free camera movement with WASD
            FollowAI,     // Following an AI
            FollowPlayer, // Following the player
            ControlEnemy  // Controlling an enemy
        }

        [Header("Camera Settings")]
        [SerializeField] private float cameraZ = -10f;
        [SerializeField] private float smoothSpeed = 0.1f;
        
        [Header("God View Settings")]
        [SerializeField] private float moveSpeed = 30f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float defaultZoom = 20f;
        
        [Header("Follow Settings")]
        [SerializeField] private float followZoom = 10f;
        [SerializeField] private float enemyControlZoom = 8f;
        
        [Header("UI References")]
        [SerializeField] private GameObject overviewPanel;
        [SerializeField] private TMPro.TextMeshProUGUI modeText;
        
        // State
        private CameraMode currentMode = CameraMode.GodView;
        private Transform followTarget;
        private UnityEngine.Camera mainCamera;
        private float currentZoom;
        
        // Double click detection
        private float lastClickTime = 0f;
        private GameObject lastClickedObject = null;
        private const float doubleClickTime = 0.3f;
        
        // Enemy control
        private Enemy2D controlledEnemy;
        
        private static CameraManager instance;
        public static CameraManager Instance => instance;
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            
            mainCamera = GetComponent<UnityEngine.Camera>();
            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
            }
            
            if (mainCamera != null)
            {
                mainCamera.orthographic = true;
                currentZoom = defaultZoom;
                mainCamera.orthographicSize = currentZoom;
            }
            
            // Set initial position
            transform.position = new Vector3(128f, 128f, cameraZ);
        }
        
        private void Start()
        {
            ShowModeInfo();
        }
        
        private void Update()
        {
            // Mode switching
            if (Input.GetKeyDown(KeyCode.G))
            {
                SwitchToGodView();
            }
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SwitchToGodView();
            }
            
            // Tab for overview
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleOverviewPanel();
            }
            
            // Handle double click
            if (Input.GetMouseButtonDown(0))
            {
                HandleDoubleClick();
            }
            
            // Mode-specific updates
            switch (currentMode)
            {
                case CameraMode.GodView:
                    HandleGodViewMovement();
                    HandleZoom();
                    break;
                    
                case CameraMode.ControlEnemy:
                    HandleEnemyControl();
                    break;
            }
            
            // Always ensure correct Z position
            EnsureCameraZ();
        }
        
        private void LateUpdate()
        {
            // Follow target smoothly
            if ((currentMode == CameraMode.FollowAI || currentMode == CameraMode.FollowPlayer) && followTarget != null)
            {
                Vector3 targetPos = followTarget.position;
                targetPos.z = cameraZ;
                transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed);
            }
        }
        
        private void HandleGodViewMovement()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            if (horizontal != 0 || vertical != 0)
            {
                Vector3 movement = new Vector3(horizontal, vertical, 0) * moveSpeed * Time.deltaTime;
                transform.position += movement;
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
            
            // Check what was clicked
            Collider2D[] colliders = Physics2D.OverlapCircleAll(mousePos2D, 0.5f);
            
            GameObject clickedObject = null;
            
            // Prioritize AI and Enemy
            foreach (var collider in colliders)
            {
                if (collider.GetComponent<AIBrain>() != null || 
                    collider.GetComponent<Enemy2D>() != null ||
                    collider.CompareTag("Player"))
                {
                    clickedObject = collider.gameObject;
                    break;
                }
            }
            
            if (clickedObject != null)
            {
                // Check double click
                if (lastClickedObject == clickedObject && Time.time - lastClickTime < doubleClickTime)
                {
                    // Double clicked!
                    OnObjectDoubleClicked(clickedObject);
                    lastClickedObject = null;
                }
                else
                {
                    lastClickedObject = clickedObject;
                    lastClickTime = Time.time;
                }
            }
        }
        
        private void OnObjectDoubleClicked(GameObject obj)
        {
            // Check if it's the player
            if (obj.CompareTag("Player"))
            {
                FollowPlayer(obj.transform);
            }
            // Check if it's an AI
            else if (obj.GetComponent<AIBrain>() != null)
            {
                FollowAI(obj.transform);
            }
            // Check if it's an enemy
            else if (obj.GetComponent<Enemy2D>() != null)
            {
                ControlEnemy(obj.GetComponent<Enemy2D>());
            }
        }
        
        public void SwitchToGodView()
        {
            currentMode = CameraMode.GodView;
            followTarget = null;
            
            // Stop controlling enemy
            if (controlledEnemy != null)
            {
                controlledEnemy.enabled = true;
                controlledEnemy = null;
            }
            
            currentZoom = defaultZoom;
            if (mainCamera != null)
                mainCamera.orthographicSize = currentZoom;
                
            ShowModeInfo();
            Debug.Log("[CameraManager] Switched to God View");
        }
        
        public void FollowPlayer(Transform player)
        {
            currentMode = CameraMode.FollowPlayer;
            followTarget = player;
            
            currentZoom = followZoom;
            if (mainCamera != null)
                mainCamera.orthographicSize = currentZoom;
                
            ShowModeInfo();
            Debug.Log($"[CameraManager] Following player");
        }
        
        public void FollowAI(Transform ai)
        {
            currentMode = CameraMode.FollowAI;
            followTarget = ai;
            
            currentZoom = followZoom;
            if (mainCamera != null)
                mainCamera.orthographicSize = currentZoom;
                
            ShowModeInfo();
            Debug.Log($"[CameraManager] Following AI: {ai.name}");
        }
        
        public void ControlEnemy(Enemy2D enemy)
        {
            // Stop previous control
            if (controlledEnemy != null)
            {
                controlledEnemy.enabled = true;
            }
            
            currentMode = CameraMode.ControlEnemy;
            followTarget = enemy.transform;
            controlledEnemy = enemy;
            
            // Disable enemy AI
            enemy.enabled = false;
            
            currentZoom = enemyControlZoom;
            if (mainCamera != null)
                mainCamera.orthographicSize = currentZoom;
                
            ShowModeInfo();
            Debug.Log($"[CameraManager] Controlling enemy: {enemy.name}");
        }
        
        private void HandleEnemyControl()
        {
            if (controlledEnemy == null) return;
            
            // Basic enemy control
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            if (horizontal != 0 || vertical != 0)
            {
                Vector2 movement = new Vector2(horizontal, vertical).normalized;
                controlledEnemy.transform.position += (Vector3)(movement * 5f * Time.deltaTime);
            }
            
            // Attack on left click
            if (Input.GetMouseButtonDown(0))
            {
                // Trigger enemy attack if it has the method
                var combatSystem = controlledEnemy.GetComponent<Combat.CombatSystem2D>();
                if (combatSystem != null)
                {
                    combatSystem.PerformAttack();
                }
            }
        }
        
        private void EnsureCameraZ()
        {
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.z - cameraZ) > 0.01f)
            {
                pos.z = cameraZ;
                transform.position = pos;
            }
        }
        
        private void ShowModeInfo()
        {
            if (modeText != null)
            {
                string info = currentMode switch
                {
                    CameraMode.GodView => "God View - WASD to move, G to toggle",
                    CameraMode.FollowPlayer => "Following Player - ESC to exit",
                    CameraMode.FollowAI => $"Following {followTarget?.name} - ESC to exit",
                    CameraMode.ControlEnemy => $"Controlling {controlledEnemy?.name} - ESC to exit",
                    _ => ""
                };
                
                modeText.text = info;
            }
        }
        
        private void ToggleOverviewPanel()
        {
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
            
            // Create overview panel
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
            titleText.text = "Game Overview";
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
            
            // Add updater
            var updater = overviewPanel.AddComponent<SimpleOverviewUpdater>();
            updater.contentText = contentText;
            
            overviewPanel.SetActive(false);
        }
        
        private void OnGUI()
        {
            // Show current mode and controls
            GUI.Label(new Rect(10, 10, 300, 20), $"Camera Mode: {currentMode}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Zoom: {currentZoom:F1}");
            
            string controls = currentMode switch
            {
                CameraMode.GodView => "WASD - Move | Scroll - Zoom | Tab - Overview | Double-click - Follow/Control",
                CameraMode.FollowPlayer => "ESC/G - God View | Tab - Overview",
                CameraMode.FollowAI => "ESC/G - God View | Tab - Overview",
                CameraMode.ControlEnemy => "WASD - Move | Click - Attack | ESC/G - God View",
                _ => ""
            };
            
            GUI.Label(new Rect(10, 50, 500, 20), controls);
        }
    }
}