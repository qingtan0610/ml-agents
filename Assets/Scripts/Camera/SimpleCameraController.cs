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
        
        [Header("UI")]
        [SerializeField] private GameObject overviewPanel;
        
        // Camera reference
        private UnityEngine.Camera mainCamera;
        private float currentZoom = 20f;
        
        // Double click detection
        private float lastClickTime = 0f;
        private GameObject lastClickedObject = null;
        private float doubleClickTime = 0.3f;
        
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
                isFollowing = false;
                followTarget = null;
                Debug.Log("[SimpleCameraController] Stopped following");
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
            
            // Check for AI or Enemy with a small radius
            Collider2D[] colliders = Physics2D.OverlapCircleAll(mousePos2D, 0.5f);
            
            GameObject clickedObject = null;
            
            // Find the most relevant object (AI or Enemy)
            foreach (var collider in colliders)
            {
                if (collider.GetComponent<AIBrain>() != null || collider.GetComponent<Enemy2D>() != null)
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
                    if (clickedObject.GetComponent<AIBrain>() != null)
                    {
                        isFollowing = true;
                        followTarget = clickedObject.transform;
                        currentZoom = 10f;
                        if (mainCamera != null)
                            mainCamera.orthographicSize = currentZoom;
                        UnityEngine.Debug.Log($"[SimpleCameraController] Following AI: {clickedObject.name}");
                    }
                    // Check if it's an enemy
                    else if (clickedObject.GetComponent<Enemy2D>() != null)
                    {
                        isFollowing = true;
                        followTarget = clickedObject.transform;
                        currentZoom = 8f;
                        if (mainCamera != null)
                            mainCamera.orthographicSize = currentZoom;
                        UnityEngine.Debug.Log($"[SimpleCameraController] Following Enemy: {clickedObject.name}");
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
    }
}