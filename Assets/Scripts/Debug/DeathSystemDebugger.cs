using UnityEngine;
using AI.Stats;
using Player;
using Rooms;
using Debug = UnityEngine.Debug;

namespace PlayerDebug
{
    /// <summary>
    /// 死亡系统调试器，用于诊断死亡和复活流程
    /// </summary>
    public class DeathSystemDebugger : MonoBehaviour
    {
        private AIStats aiStats;
        private PlayerDeathManager deathManager;
        private Transform playerTransform;
        
        private Vector3 lastPosition;
        private bool wasDeadLastFrame = false;
        
        private void Start()
        {
            // 查找玩家组件
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[DeathSystemDebugger] Player not found!");
                enabled = false;
                return;
            }
            
            playerTransform = player.transform;
            aiStats = player.GetComponent<AIStats>();
            deathManager = player.GetComponent<PlayerDeathManager>();
            
            if (aiStats == null)
            {
                Debug.LogError("[DeathSystemDebugger] AIStats not found on player!");
                enabled = false;
                return;
            }
            
            // 监听死亡事件
            aiStats.OnDeath.AddListener(OnPlayerDeath);
            aiStats.OnRespawn.AddListener(OnPlayerRespawn);
            
            lastPosition = playerTransform.position;
            
            Debug.Log("[DeathSystemDebugger] Initialized successfully");
        }
        
        private void Update()
        {
            if (aiStats == null || playerTransform == null) return;
            
            // 检测死亡状态变化
            bool isDeadNow = aiStats.IsDead;
            if (isDeadNow != wasDeadLastFrame)
            {
                Debug.Log($"[DeathSystemDebugger] Death state changed: {wasDeadLastFrame} -> {isDeadNow}");
                wasDeadLastFrame = isDeadNow;
            }
            
            // 检测位置变化
            if (Vector3.Distance(playerTransform.position, lastPosition) > 0.1f)
            {
                Debug.Log($"[DeathSystemDebugger] Position changed: {lastPosition} -> {playerTransform.position}");
                lastPosition = playerTransform.position;
            }
            
            // F10键：强制触发死亡
            if (Input.GetKeyDown(KeyCode.F10))
            {
                Debug.Log("[DeathSystemDebugger] F10 - Forcing death");
                aiStats.SetStat(StatType.Health, 0, StatChangeReason.Other);
            }
            
            // F11键：诊断信息
            if (Input.GetKeyDown(KeyCode.F11))
            {
                PrintDiagnostics();
            }
            
            // F12键：强制复活到出生点
            if (Input.GetKeyDown(KeyCode.F12))
            {
                Debug.Log("[DeathSystemDebugger] F12 - Forcing respawn");
                ForceRespawn();
            }
        }
        
        private void OnPlayerDeath(AIDeathEventArgs args)
        {
            Debug.Log($"[DeathSystemDebugger] OnDeath event received!");
            Debug.Log($"  - Cause: {args.causeOfDeath}");
            Debug.Log($"  - Position: {args.deathPosition}");
            Debug.Log($"  - Time survived: {args.timeSurvived}");
            Debug.Log($"  - Current position: {playerTransform.position}");
        }
        
        private void OnPlayerRespawn()
        {
            Debug.Log($"[DeathSystemDebugger] OnRespawn event received!");
            Debug.Log($"  - New position: {playerTransform.position}");
            Debug.Log($"  - Is dead: {aiStats.IsDead}");
            Debug.Log($"  - Health: {aiStats.GetStat(StatType.Health)}");
        }
        
        private void PrintDiagnostics()
        {
            Debug.Log("=== Death System Diagnostics ===");
            Debug.Log($"Player position: {playerTransform.position}");
            Debug.Log($"Is dead: {aiStats.IsDead}");
            Debug.Log($"Health: {aiStats.GetStat(StatType.Health)}");
            Debug.Log($"Hunger: {aiStats.GetStat(StatType.Hunger)}");
            Debug.Log($"Thirst: {aiStats.GetStat(StatType.Thirst)}");
            
            var rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Debug.Log($"Rigidbody velocity: {rb.velocity}");
                Debug.Log($"Rigidbody constraints: {rb.constraints}");
            }
            
            Debug.Log($"PlayerDeathManager enabled: {deathManager?.enabled}");
            Debug.Log($"AIStats enabled: {aiStats?.enabled}");
            
            // 检查MapGenerator
            var mapGen = FindObjectOfType<MapGenerator>();
            if (mapGen != null)
            {
                Debug.Log($"MapGenerator spawn position: {mapGen.GetSpawnPosition()}");
            }
            else
            {
                Debug.Log("MapGenerator not found!");
            }
            
            Debug.Log("================================");
        }
        
        private void ForceRespawn()
        {
            // 获取出生点
            Vector3 spawnPos = new Vector3(8 * 16, 8 * 16, 0);
            var mapGen = FindObjectOfType<MapGenerator>();
            if (mapGen != null)
            {
                spawnPos = mapGen.GetSpawnPosition();
            }
            
            Debug.Log($"[DeathSystemDebugger] Force respawning to {spawnPos}");
            
            // 强制设置位置
            playerTransform.position = spawnPos;
            
            // 如果玩家已死，执行复活
            if (aiStats.IsDead)
            {
                aiStats.Respawn(spawnPos, false);
            }
            
            // 重置刚体
            var rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            
            // 更新相机
            if (UnityEngine.Camera.main != null)
            {
                UnityEngine.Camera.main.transform.position = new Vector3(spawnPos.x, spawnPos.y, UnityEngine.Camera.main.transform.position.z);
            }
            
            Debug.Log($"[DeathSystemDebugger] Force respawn complete. New position: {playerTransform.position}");
        }
        
        private void OnDestroy()
        {
            if (aiStats != null)
            {
                aiStats.OnDeath.RemoveListener(OnPlayerDeath);
                aiStats.OnRespawn.RemoveListener(OnPlayerRespawn);
            }
        }
    }
}