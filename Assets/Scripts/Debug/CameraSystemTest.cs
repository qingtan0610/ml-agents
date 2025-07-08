using UnityEngine;
using Camera;

namespace Debugging
{
    /// <summary>
    /// Test script for camera system - add to any GameObject to test
    /// </summary>
    public class CameraSystemTest : MonoBehaviour
    {
        private void Start()
        {
            UnityEngine.Debug.Log("[CameraSystemTest] Starting camera system test...");
            
            // Check if camera exists
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                UnityEngine.Debug.LogError("[CameraSystemTest] No main camera found!");
                return;
            }
            
            UnityEngine.Debug.Log($"[CameraSystemTest] Main camera found at: {mainCamera.transform.position}");
            
            // Check camera controller
            var cameraController = mainCamera.GetComponent<global::Camera.SimpleCameraController>();
            if (cameraController == null)
            {
                UnityEngine.Debug.LogWarning("[CameraSystemTest] No SimpleCameraController found on main camera");
            }
            else
            {
                UnityEngine.Debug.Log("[CameraSystemTest] SimpleCameraController is active");
            }
            
            // Test input system
            StartCoroutine(TestInputSystem());
        }
        
        private System.Collections.IEnumerator TestInputSystem()
        {
            yield return new WaitForSeconds(1f);
            
            UnityEngine.Debug.Log("[CameraSystemTest] Testing input system...");
            
            for (int i = 0; i < 5; i++)
            {
                float h = Input.GetAxis("Horizontal");
                float v = Input.GetAxis("Vertical");
                
                if (h != 0 || v != 0)
                {
                    UnityEngine.Debug.Log($"[CameraSystemTest] Input detected: H={h}, V={v}");
                }
                
                yield return new WaitForSeconds(0.5f);
            }
            
            UnityEngine.Debug.Log("[CameraSystemTest] Input test complete");
        }
        
        private void Update()
        {
            // Log any key presses
            if (Input.GetKeyDown(KeyCode.F12))
            {
                UnityEngine.Debug.Log("[CameraSystemTest] === System Status ===");
                UnityEngine.Debug.Log($"Time.timeScale: {Time.timeScale}");
                UnityEngine.Debug.Log($"Application.isFocused: {Application.isFocused}");
                UnityEngine.Debug.Log($"Cursor.lockState: {Cursor.lockState}");
                
                var cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    UnityEngine.Debug.Log($"Camera position: {cam.transform.position}");
                    UnityEngine.Debug.Log($"Camera enabled: {cam.enabled}");
                }
            }
        }
    }
}