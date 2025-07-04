using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PlayerDebug
{
    /// <summary>
    /// 简单的位置测试脚本，用于验证transform.position设置
    /// </summary>
    public class SimplePositionTest : MonoBehaviour
    {
        private Vector3 targetPosition = new Vector3(128, 128, 0); // 出生点位置
        
        private void Start()
        {
            Debug.Log($"[SimplePositionTest] Initial position: {transform.position}");
        }
        
        private void Update()
        {
            // 按T键测试传送
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log($"[SimplePositionTest] T key pressed - Teleporting from {transform.position} to {targetPosition}");
                
                // 直接设置位置
                transform.position = targetPosition;
                
                Debug.Log($"[SimplePositionTest] Position after setting: {transform.position}");
                
                // 检查刚体
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Debug.Log($"[SimplePositionTest] Rigidbody velocity: {rb.velocity}, constraints: {rb.constraints}");
                }
            }
            
            // 按Y键验证位置
            if (Input.GetKeyDown(KeyCode.Y))
            {
                Debug.Log($"[SimplePositionTest] Current position: {transform.position}");
                Debug.Log($"[SimplePositionTest] Distance from target: {Vector3.Distance(transform.position, targetPosition)}");
            }
        }
        
        // 物理更新后检查
        private void LateUpdate()
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                Debug.Log($"[SimplePositionTest] LateUpdate position: {transform.position}");
            }
        }
    }
}