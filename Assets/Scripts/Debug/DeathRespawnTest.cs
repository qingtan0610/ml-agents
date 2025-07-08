using UnityEngine;
using AI.Stats;
using Player;
using Rooms;
using Debug = UnityEngine.Debug;

namespace PlayerDebug
{
    /// <summary>
    /// 死亡复活系统测试工具
    /// </summary>
    public class DeathRespawnTest : MonoBehaviour
    {
        private AIStats aiStats;
        private PlayerController2D playerController;
        private PlayerDeathManager deathManager;
        private Rigidbody2D rb;
        
        private void Start()
        {
            aiStats = GetComponent<AIStats>();
            playerController = GetComponent<PlayerController2D>();
            deathManager = GetComponent<PlayerDeathManager>();
            rb = GetComponent<Rigidbody2D>();
            
            Debug.Log($"[DeathRespawnTest] Initialized. Components found:");
            Debug.Log($"  - AIStats: {aiStats != null}");
            Debug.Log($"  - PlayerController2D: {playerController != null}");
            Debug.Log($"  - PlayerDeathManager: {deathManager != null}");
            Debug.Log($"  - Rigidbody2D: {rb != null}");
        }
        
        private void Update()
        {
            // F5 - 测试死亡
            if (Input.GetKeyDown(KeyCode.F5))
            {
                TestDeath();
            }
            
            // F6 - 显示状态
            if (Input.GetKeyDown(KeyCode.F6))
            {
                ShowStatus();
            }
            
            // F7 - 强制复活
            if (Input.GetKeyDown(KeyCode.F7))
            {
                ForceRespawn();
            }
        }
        
        private void TestDeath()
        {
            Debug.Log("=== [DeathRespawnTest] TESTING DEATH ===");
            
            if (aiStats == null)
            {
                Debug.LogError("[DeathRespawnTest] AIStats is null!");
                return;
            }
            
            if (aiStats.IsDead)
            {
                Debug.Log("[DeathRespawnTest] Player is already dead");
                return;
            }
            
            // 记录死亡前的位置
            Vector3 deathPosition = transform.position;
            Debug.Log($"[DeathRespawnTest] Position before death: {deathPosition}");
            
            // 强制设置生命值为0
            aiStats.ModifyStat(StatType.Health, -1000f, StatChangeReason.Combat);
            
            // 等待一帧后检查状态
            StartCoroutine(CheckDeathStatus(deathPosition));
        }
        
        private System.Collections.IEnumerator CheckDeathStatus(Vector3 deathPosition)
        {
            yield return new WaitForEndOfFrame();
            
            Debug.Log($"[DeathRespawnTest] After death:");
            Debug.Log($"  - IsDead: {aiStats.IsDead}");
            Debug.Log($"  - Health: {aiStats.GetStat(StatType.Health)}");
            Debug.Log($"  - Position: {transform.position}");
            Debug.Log($"  - Velocity: {rb?.velocity}");
            Debug.Log($"  - Constraints: {rb?.constraints}");
            
            if (playerController != null)
            {
                Debug.Log($"  - PlayerController enabled: {playerController.enabled}");
            }
        }
        
        private void ShowStatus()
        {
            Debug.Log("=== [DeathRespawnTest] CURRENT STATUS ===");
            
            if (aiStats != null)
            {
                Debug.Log($"  - IsDead: {aiStats.IsDead}");
                Debug.Log($"  - Health: {aiStats.GetStat(StatType.Health)}");
                Debug.Log($"  - Hunger: {aiStats.GetStat(StatType.Hunger)}");
                Debug.Log($"  - Thirst: {aiStats.GetStat(StatType.Thirst)}");
            }
            
            Debug.Log($"  - Position: {transform.position}");
            
            if (rb != null)
            {
                Debug.Log($"  - Velocity: {rb.velocity}");
                Debug.Log($"  - Angular Velocity: {rb.angularVelocity}");
                Debug.Log($"  - Constraints: {rb.constraints}");
            }
            
            if (playerController != null)
            {
                Debug.Log($"  - PlayerController enabled: {playerController.enabled}");
            }
            
            // 检查地图生成器
            var mapGen = FindObjectOfType<MapGenerator>();
            if (mapGen != null)
            {
                Vector3 spawnPos = mapGen.GetSpawnPosition();
                Debug.Log($"  - Expected spawn position: {spawnPos}");
                Debug.Log($"  - Distance from spawn: {Vector3.Distance(transform.position, spawnPos)}");
            }
        }
        
        private void ForceRespawn()
        {
            Debug.Log("=== [DeathRespawnTest] FORCING RESPAWN ===");
            
            if (!aiStats.IsDead)
            {
                Debug.Log("[DeathRespawnTest] Player is not dead, killing first...");
                TestDeath();
                // 等待一帧后再复活
                StartCoroutine(DelayedRespawn());
                return;
            }
            
            ExecuteRespawn();
        }
        
        private System.Collections.IEnumerator DelayedRespawn()
        {
            yield return new WaitForSeconds(0.1f);
            ExecuteRespawn();
        }
        
        private void ExecuteRespawn()
        {
            // 获取出生点位置
            var mapGen = FindObjectOfType<MapGenerator>();
            Vector3 spawnPos = mapGen != null ? mapGen.GetSpawnPosition() : new Vector3(128, 128, 0);
            
            Debug.Log($"[DeathRespawnTest] Spawn position: {spawnPos}");
            
            // 方法1: 直接设置位置并调用AIStats.Respawn
            Debug.Log("[DeathRespawnTest] Method 1: Direct position + AIStats.Respawn");
            
            // 先解冻刚体
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            
            // 设置位置
            transform.position = spawnPos;
            
            // 调用复活
            aiStats.Respawn(spawnPos, false);
            
            // 重新启用PlayerController
            if (playerController != null)
            {
                playerController.enabled = true;
                playerController.Respawn();
            }
            
            // 移动相机
            if (UnityEngine.Camera.main != null)
            {
                UnityEngine.Camera.main.transform.position = new Vector3(spawnPos.x, spawnPos.y, UnityEngine.Camera.main.transform.position.z);
            }
            
            // 验证复活结果
            StartCoroutine(VerifyRespawn(spawnPos));
        }
        
        private System.Collections.IEnumerator VerifyRespawn(Vector3 expectedPos)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            
            Debug.Log("=== [DeathRespawnTest] RESPAWN VERIFICATION ===");
            Debug.Log($"  - Expected position: {expectedPos}");
            Debug.Log($"  - Actual position: {transform.position}");
            Debug.Log($"  - Position correct: {Vector3.Distance(transform.position, expectedPos) < 0.1f}");
            Debug.Log($"  - IsDead: {aiStats.IsDead}");
            Debug.Log($"  - Health: {aiStats.GetStat(StatType.Health)}");
            
            if (Vector3.Distance(transform.position, expectedPos) > 0.1f)
            {
                Debug.LogError("[DeathRespawnTest] RESPAWN FAILED - Position not set correctly!");
                Debug.Log("[DeathRespawnTest] Attempting forced position correction...");
                
                // 强制再次设置位置
                transform.position = expectedPos;
                if (rb != null)
                {
                    rb.MovePosition(expectedPos);
                    Physics2D.SyncTransforms();
                }
                
                yield return new WaitForEndOfFrame();
                Debug.Log($"[DeathRespawnTest] After forced correction: {transform.position}");
            }
            else
            {
                Debug.Log("[DeathRespawnTest] RESPAWN SUCCESS!");
            }
        }
    }
}