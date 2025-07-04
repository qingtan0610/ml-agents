using UnityEngine;
using Interactables;

namespace Debugging
{
    /// <summary>
    /// 交互系统调试工具
    /// </summary>
    public class InteractionDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private KeyCode debugKey = KeyCode.F1;
        [SerializeField] private float detectionRadius = 3f;
        [SerializeField] private bool showGizmos = true;
        
        private void Update()
        {
            if (Input.GetKeyDown(debugKey))
            {
                PerformDebugCheck();
            }
        }
        
        private void PerformDebugCheck()
        {
            UnityEngine.Debug.Log("========== INTERACTION DEBUG START ==========");
            UnityEngine.Debug.Log($"Player Position: {transform.position}");
            
            // 检查所有碰撞体
            Collider2D[] allColliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
            UnityEngine.Debug.Log($"Total Colliders in range: {allColliders.Length}");
            
            int interactableCount = 0;
            int treasureChestCount = 0;
            
            foreach (var collider in allColliders)
            {
                UnityEngine.Debug.Log($"  - GameObject: {collider.gameObject.name}");
                UnityEngine.Debug.Log($"    - Layer: {collider.gameObject.layer} ({LayerMask.LayerToName(collider.gameObject.layer)})");
                UnityEngine.Debug.Log($"    - Tag: {collider.gameObject.tag}");
                UnityEngine.Debug.Log($"    - Active: {collider.gameObject.activeInHierarchy}");
                UnityEngine.Debug.Log($"    - Collider Enabled: {collider.enabled}");
                UnityEngine.Debug.Log($"    - Collider Type: {collider.GetType().Name}");
                UnityEngine.Debug.Log($"    - IsTrigger: {collider.isTrigger}");
                
                // 检查组件
                var components = collider.GetComponents<Component>();
                UnityEngine.Debug.Log($"    - Total Components: {components.Length}");
                
                foreach (var comp in components)
                {
                    UnityEngine.Debug.Log($"      * {comp.GetType().Name}");
                    
                    if (comp is IInteractable)
                    {
                        interactableCount++;
                        UnityEngine.Debug.Log($"        [IInteractable FOUND!]");
                    }
                    
                    if (comp is TreasureChest)
                    {
                        treasureChestCount++;
                        var chest = comp as TreasureChest;
                        UnityEngine.Debug.Log($"        [TreasureChest FOUND!]");
                        
                        // 使用反射获取私有字段信息
                        var type = chest.GetType();
                        var isOpenedField = type.GetField("isOpened", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var isLockedField = type.GetField("isLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (isOpenedField != null)
                            UnityEngine.Debug.Log($"          - isOpened: {isOpenedField.GetValue(chest)}");
                        if (isLockedField != null)
                            UnityEngine.Debug.Log($"          - isLocked: {isLockedField.GetValue(chest)}");
                    }
                }
                
                UnityEngine.Debug.Log("");
            }
            
            UnityEngine.Debug.Log($"Summary:");
            UnityEngine.Debug.Log($"  - Total IInteractables: {interactableCount}");
            UnityEngine.Debug.Log($"  - Total TreasureChests: {treasureChestCount}");
            
            // 测试直接调用
            if (treasureChestCount > 0)
            {
                UnityEngine.Debug.Log("Attempting direct interaction test...");
                foreach (var collider in allColliders)
                {
                    var chest = collider.GetComponent<TreasureChest>();
                    if (chest != null)
                    {
                        UnityEngine.Debug.Log($"Calling Interact() on {collider.gameObject.name}...");
                        chest.Interact(gameObject);
                        break;
                    }
                }
            }
            
            UnityEngine.Debug.Log("========== INTERACTION DEBUG END ==========");
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            // 画出检测到的对象
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
            foreach (var collider in colliders)
            {
                if (collider.GetComponent<IInteractable>() != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, collider.transform.position);
                    Gizmos.DrawWireCube(collider.transform.position, Vector3.one * 0.5f);
                }
            }
        }
    }
}