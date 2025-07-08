using UnityEngine;

/// <summary>
/// 简化的游戏初始化器 - 只负责设置相机
/// </summary>
public class GameInitializer : MonoBehaviour
{
    [Header("初始化设置")]
    [SerializeField] private bool setupCamera = true;
    [SerializeField] private Vector3 initialCameraPosition = new Vector3(128f, 128f, -10f);
    [SerializeField] private float initialCameraSize = 20f;
    
    private void Start()
    {
        if (setupCamera)
        {
            SetupCamera();
        }
    }
    
    private void SetupCamera()
    {
        UnityEngine.Debug.Log("[GameInitializer] Setting up camera...");
        
        // 查找主相机
        var mainCamera = UnityEngine.Camera.main;
        if (mainCamera == null)
        {
            UnityEngine.Debug.LogError("[GameInitializer] No main camera found!");
            return;
        }
        
        // 确保相机有SimpleCameraController
        var cameraController = mainCamera.GetComponent<Camera.SimpleCameraController>();
        if (cameraController == null)
        {
            cameraController = mainCamera.gameObject.AddComponent<Camera.SimpleCameraController>();
            UnityEngine.Debug.Log("[GameInitializer] Added SimpleCameraController to main camera");
        }
        
        // 设置初始位置
        mainCamera.transform.position = initialCameraPosition;
        mainCamera.orthographicSize = initialCameraSize;
        
        // 确保是正交相机
        mainCamera.orthographic = true;
        
        UnityEngine.Debug.Log("[GameInitializer] Camera setup complete");
    }
}