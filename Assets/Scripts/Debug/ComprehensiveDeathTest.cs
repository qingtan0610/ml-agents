using UnityEngine;
using AI.Stats;
using Player;
using Rooms;
using System.Collections;
using Debug = UnityEngine.Debug;

namespace PlayerDebug
{
    /// <summary>
    /// 综合死亡系统测试
    /// </summary>
    public class ComprehensiveDeathTest : MonoBehaviour
    {
        [Header("Test Controls")]
        [SerializeField] private bool enableAutoTest = false;
        [SerializeField] private float testInterval = 5f;
        
        private AIStats aiStats;
        private PlayerDeathManager deathManager;
        private PlayerController2D playerController;
        private Rigidbody2D rb;
        
        private int testStep = 0;
        private float nextTestTime = 0;
        
        private void Start()
        {
            // 获取组件
            aiStats = GetComponent<AIStats>();
            deathManager = GetComponent<PlayerDeathManager>();
            playerController = GetComponent<PlayerController2D>();
            rb = GetComponent<Rigidbody2D>();
            
            if (aiStats == null || deathManager == null)
            {
                Debug.LogError("[ComprehensiveDeathTest] Required components not found!");
                enabled = false;
                return;
            }
            
            Debug.Log("[ComprehensiveDeathTest] Test ready. Press F1 to run full test sequence.");
        }
        
        private void Update()
        {
            // F1: 运行完整测试序列
            if (Input.GetKeyDown(KeyCode.F1))
            {
                StartCoroutine(RunFullTestSequence());
            }
            
            // F2: 快速死亡测试
            if (Input.GetKeyDown(KeyCode.F2))
            {
                QuickDeathTest();
            }
            
            // F3: 位置验证
            if (Input.GetKeyDown(KeyCode.F3))
            {
                VerifyPosition();
            }
            
            // 自动测试
            if (enableAutoTest && Time.time >= nextTestTime)
            {
                nextTestTime = Time.time + testInterval;
                RunNextTest();
            }
        }
        
        private void RunNextTest()
        {
            switch (testStep)
            {
                case 0:
                    Debug.Log("[ComprehensiveDeathTest] Step 0: Setting health to 0");
                    aiStats.SetStat(StatType.Health, 0);
                    break;
                case 1:
                    Debug.Log("[ComprehensiveDeathTest] Step 1: Checking death state");
                    LogCurrentState();
                    break;
                case 2:
                    Debug.Log("[ComprehensiveDeathTest] Step 2: Triggering respawn");
                    if (aiStats.IsDead)
                    {
                        // 触发手动复活
                        deathManager.SendMessage("Respawn");
                    }
                    break;
                case 3:
                    Debug.Log("[ComprehensiveDeathTest] Step 3: Verifying respawn");
                    VerifyPosition();
                    testStep = -1; // 重置循环
                    break;
            }
            testStep++;
        }
        
        private IEnumerator RunFullTestSequence()
        {
            Debug.Log("=== Starting Comprehensive Death Test Sequence ===");
            
            // Step 1: 记录初始状态
            Debug.Log("[Test] Step 1: Recording initial state");
            Vector3 initialPosition = transform.position;
            LogCurrentState();
            yield return new WaitForSeconds(1f);
            
            // Step 2: 触发死亡
            Debug.Log("[Test] Step 2: Triggering death");
            aiStats.SetStat(StatType.Health, 0);
            yield return new WaitForSeconds(0.5f);
            
            // Step 3: 验证死亡状态
            Debug.Log("[Test] Step 3: Verifying death state");
            if (!aiStats.IsDead)
            {
                Debug.LogError("[Test] FAILED: Player should be dead but IsDead = false");
            }
            else
            {
                Debug.Log("[Test] SUCCESS: Player is dead");
            }
            LogCurrentState();
            yield return new WaitForSeconds(1f);
            
            // Step 4: 等待自动复活或手动触发
            Debug.Log("[Test] Step 4: Waiting for respawn...");
            float waitTime = 0;
            while (aiStats.IsDead && waitTime < 5f)
            {
                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }
            
            if (aiStats.IsDead)
            {
                Debug.Log("[Test] Auto-respawn didn't trigger, forcing manual respawn");
                // 使用反射调用私有方法
                var respawnMethod = deathManager.GetType().GetMethod("Respawn", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (respawnMethod != null)
                {
                    respawnMethod.Invoke(deathManager, null);
                }
            }
            
            yield return new WaitForSeconds(1f);
            
            // Step 5: 验证复活状态
            Debug.Log("[Test] Step 5: Verifying respawn state");
            if (aiStats.IsDead)
            {
                Debug.LogError("[Test] FAILED: Player should be alive but IsDead = true");
            }
            else
            {
                Debug.Log("[Test] SUCCESS: Player is alive");
            }
            
            // Step 6: 验证位置
            Debug.Log("[Test] Step 6: Verifying position");
            Vector3 expectedSpawnPos = new Vector3(128, 128, 0); // 默认出生点
            var mapGen = FindObjectOfType<MapGenerator>();
            if (mapGen != null)
            {
                expectedSpawnPos = mapGen.GetSpawnPosition();
            }
            
            float distance = Vector3.Distance(transform.position, expectedSpawnPos);
            if (distance > 1f)
            {
                Debug.LogError($"[Test] FAILED: Player position incorrect. Expected: {expectedSpawnPos}, Actual: {transform.position}, Distance: {distance}");
            }
            else
            {
                Debug.Log($"[Test] SUCCESS: Player at correct spawn position {transform.position}");
            }
            
            LogCurrentState();
            
            Debug.Log("=== Test Sequence Complete ===");
        }
        
        private void QuickDeathTest()
        {
            Debug.Log("[ComprehensiveDeathTest] Quick death test");
            aiStats.SetStat(StatType.Health, 0);
            StartCoroutine(CheckDeathAfterDelay());
        }
        
        private IEnumerator CheckDeathAfterDelay()
        {
            yield return new WaitForSeconds(0.1f);
            LogCurrentState();
        }
        
        private void VerifyPosition()
        {
            Debug.Log("=== Position Verification ===");
            Debug.Log($"Current Position: {transform.position}");
            Debug.Log($"Is Dead: {aiStats.IsDead}");
            
            var mapGen = FindObjectOfType<MapGenerator>();
            if (mapGen != null)
            {
                Vector3 spawnPos = mapGen.GetSpawnPosition();
                Debug.Log($"Expected Spawn Position: {spawnPos}");
                Debug.Log($"Distance from spawn: {Vector3.Distance(transform.position, spawnPos)}");
            }
            
            if (rb != null)
            {
                Debug.Log($"Rigidbody velocity: {rb.velocity}");
                Debug.Log($"Rigidbody constraints: {rb.constraints}");
            }
            
            Debug.Log("===========================");
        }
        
        private void LogCurrentState()
        {
            Debug.Log("--- Current State ---");
            Debug.Log($"Position: {transform.position}");
            Debug.Log($"IsDead: {aiStats.IsDead}");
            Debug.Log($"Health: {aiStats.GetStat(StatType.Health)}");
            Debug.Log($"PlayerController enabled: {playerController?.enabled}");
            Debug.Log($"DeathManager enabled: {deathManager?.enabled}");
            if (rb != null)
            {
                Debug.Log($"RB velocity: {rb.velocity}, constraints: {rb.constraints}");
            }
            Debug.Log("-------------------");
        }
    }
}