using UnityEngine;

namespace Debugging
{
    /// <summary>
    /// 诊断Unity暂停问题的调试器
    /// </summary>
    public class UnityPauseDebugger : MonoBehaviour
    {
        private float lastTimeScale = 1f;
        private bool lastPauseState = false;
        private float lastRealTime = 0f;
        private int frameCount = 0;
        private float fpsTimer = 0f;
        private float currentFPS = 0f;
        
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private KeyCode debugKey = KeyCode.F11;
        
        private void Start()
        {
            UnityEngine.Debug.Log("[UnityPauseDebugger] Started monitoring for pause issues");
            lastRealTime = Time.realtimeSinceStartup;
        }
        
        private void Update()
        {
            // 检测时间缩放变化
            if (Mathf.Abs(Time.timeScale - lastTimeScale) > 0.001f)
            {
                UnityEngine.Debug.LogWarning($"[UnityPauseDebugger] Time.timeScale changed: {lastTimeScale} -> {Time.timeScale}");
                lastTimeScale = Time.timeScale;
            }
            
            // 检测暂停状态变化
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused != lastPauseState)
            {
                UnityEngine.Debug.LogWarning($"[UnityPauseDebugger] Editor pause state changed: {lastPauseState} -> {UnityEditor.EditorApplication.isPaused}");
                lastPauseState = UnityEditor.EditorApplication.isPaused;
            }
#endif
            
            // 计算FPS
            frameCount++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 1f)
            {
                currentFPS = frameCount / fpsTimer;
                frameCount = 0;
                fpsTimer = 0f;
                
                // 如果FPS突然很低，可能有问题
                if (currentFPS < 5f && Time.timeScale > 0.1f)
                {
                    UnityEngine.Debug.LogError($"[UnityPauseDebugger] Very low FPS detected: {currentFPS:F1}");
                }
            }
            
            // 检测时间流逝
            float currentRealTime = Time.realtimeSinceStartup;
            float deltaReal = currentRealTime - lastRealTime;
            lastRealTime = currentRealTime;
            
            // 如果真实时间增量异常大，可能是暂停了
            if (deltaReal > 0.5f) // 超过500ms
            {
                UnityEngine.Debug.LogWarning($"[UnityPauseDebugger] Large time gap detected: {deltaReal:F3}s");
            }
            
            // 手动调试信息
            if (Input.GetKeyDown(debugKey))
            {
                PrintDebugInfo();
            }
        }
        
        private void PrintDebugInfo()
        {
            UnityEngine.Debug.Log("=== Unity Pause Debug Info ===");
            UnityEngine.Debug.Log($"Time.timeScale: {Time.timeScale}");
            UnityEngine.Debug.Log($"Time.time: {Time.time}");
            UnityEngine.Debug.Log($"Time.unscaledTime: {Time.unscaledTime}");
            UnityEngine.Debug.Log($"Time.realtimeSinceStartup: {Time.realtimeSinceStartup}");
            UnityEngine.Debug.Log($"Application.targetFrameRate: {Application.targetFrameRate}");
            UnityEngine.Debug.Log($"QualitySettings.vSyncCount: {QualitySettings.vSyncCount}");
            UnityEngine.Debug.Log($"Current FPS: {currentFPS:F1}");
            
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"EditorApplication.isPaused: {UnityEditor.EditorApplication.isPaused}");
            UnityEngine.Debug.Log($"EditorApplication.isPlaying: {UnityEditor.EditorApplication.isPlaying}");
#endif
            
            // 检查活跃的相机
            var cameras = GameObject.FindObjectsOfType<UnityEngine.Camera>();
            UnityEngine.Debug.Log($"Active cameras: {cameras.Length}");
            foreach (var cam in cameras)
            {
                UnityEngine.Debug.Log($"  - {cam.name}: enabled={cam.enabled}, depth={cam.depth}");
            }
            
            // 检查SimpleCameraController
            var cameraController = GameObject.FindObjectOfType<global::Camera.SimpleCameraController>();
            if (cameraController != null)
            {
                UnityEngine.Debug.Log($"SimpleCameraController found on: {cameraController.gameObject.name}");
            }
            
            // 检查UI Canvas
            var canvases = GameObject.FindObjectsOfType<Canvas>();
            UnityEngine.Debug.Log($"Active canvases: {canvases.Length}");
            
            UnityEngine.Debug.Log("==============================");
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            // 在屏幕左上角显示基本信息
            GUI.color = Color.green;
            GUI.Label(new Rect(10, 10, 300, 20), $"FPS: {currentFPS:F1} | TimeScale: {Time.timeScale:F2}");
            
            if (Time.timeScale < 0.1f)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 30, 300, 20), "WARNING: Time.timeScale is very low!");
            }
            
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 50, 300, 20), $"Press {debugKey} for detailed debug info");
        }
    }
}