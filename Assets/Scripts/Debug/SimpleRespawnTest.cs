using UnityEngine;
using AI.Stats;
using Debug = UnityEngine.Debug;

namespace PlayerDebug
{
    /// <summary>
    /// 简单的复活测试 - 只测试最基础的功能
    /// </summary>
    public class SimpleRespawnTest : MonoBehaviour
    {
        private AIStats aiStats;
        
        private void Start()
        {
            aiStats = GetComponent<AIStats>();
            if (aiStats == null)
            {
                Debug.LogError("[SimpleRespawnTest] No AIStats component found!");
                enabled = false;
            }
        }
        
        private void Update()
        {
            // 数字键1 - 杀死玩家
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("\n[SimpleRespawnTest] === KILLING PLAYER ===");
                Debug.Log($"Current position: {transform.position}");
                Debug.Log($"IsDead before: {aiStats.IsDead}");
                
                // 设置生命值为0
                aiStats.SetStat(StatType.Health, 0, StatChangeReason.Combat);
                
                // 立即检查状态
                Debug.Log($"IsDead after: {aiStats.IsDead}");
                Debug.Log($"Health: {aiStats.GetStat(StatType.Health)}");
            }
            
            // 数字键2 - 复活到出生点
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Debug.Log("\n[SimpleRespawnTest] === RESPAWNING ===");
                
                if (!aiStats.IsDead)
                {
                    Debug.Log("Player is not dead!");
                    return;
                }
                
                Vector3 spawnPos = new Vector3(128, 128, 0);
                Debug.Log($"Target spawn position: {spawnPos}");
                Debug.Log($"Position before respawn: {transform.position}");
                
                // 调用复活
                aiStats.Respawn(spawnPos);
                
                // 检查结果
                Debug.Log($"Position after respawn: {transform.position}");
                Debug.Log($"IsDead after respawn: {aiStats.IsDead}");
                Debug.Log($"Health after respawn: {aiStats.GetStat(StatType.Health)}");
                
                // 如果位置没变，强制设置
                if (Vector3.Distance(transform.position, spawnPos) > 0.1f)
                {
                    Debug.LogWarning("Position not changed! Forcing position...");
                    transform.position = spawnPos;
                    Debug.Log($"Position after force: {transform.position}");
                }
            }
            
            // 数字键3 - 测试直接移动
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Debug.Log("\n[SimpleRespawnTest] === TESTING DIRECT MOVEMENT ===");
                Vector3 testPos = new Vector3(100, 100, 0);
                Debug.Log($"Moving to: {testPos}");
                Debug.Log($"Position before: {transform.position}");
                
                transform.position = testPos;
                
                Debug.Log($"Position after: {transform.position}");
                
                // 检查是否有其他组件在干扰
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Debug.Log($"Rigidbody velocity: {rb.velocity}");
                    Debug.Log($"Rigidbody constraints: {rb.constraints}");
                }
            }
            
            // 数字键4 - 显示当前状态
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Debug.Log("\n[SimpleRespawnTest] === CURRENT STATUS ===");
                Debug.Log($"Position: {transform.position}");
                Debug.Log($"IsDead: {aiStats.IsDead}");
                Debug.Log($"Health: {aiStats.GetStat(StatType.Health)}");
                Debug.Log($"GameObject active: {gameObject.activeSelf}");
                Debug.Log($"Transform parent: {transform.parent?.name ?? "None"}");
                
                // 检查所有可能影响位置的组件
                var components = GetComponents<Component>();
                Debug.Log($"Total components: {components.Length}");
                foreach (var comp in components)
                {
                    if (comp is MonoBehaviour mb && mb.enabled)
                    {
                        Debug.Log($"  - {comp.GetType().Name} (enabled)");
                    }
                }
            }
        }
    }
}