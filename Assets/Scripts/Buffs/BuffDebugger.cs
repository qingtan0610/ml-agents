using UnityEngine;
using System.Collections.Generic;

namespace Buffs
{
    /// <summary>
    /// 用于测试Buff系统的调试组件
    /// </summary>
    [RequireComponent(typeof(BuffManager))]
    public class BuffDebugger : MonoBehaviour
    {
        [Header("Test Buffs")]
        [SerializeField] private List<BuffBase> testBuffs = new List<BuffBase>();
        
        [Header("Debug Settings")]
        [SerializeField] private KeyCode addBuffKey = KeyCode.B;
        [SerializeField] private KeyCode removeAllBuffsKey = KeyCode.N;
        [SerializeField] private KeyCode removeAllDebuffsKey = KeyCode.M;
        
        private BuffManager buffManager;
        private int currentBuffIndex = 0;
        
        private void Start()
        {
            buffManager = GetComponent<BuffManager>();
            if (buffManager == null)
            {
                Debug.LogError("[BuffDebugger] BuffManager not found!");
                enabled = false;
            }
        }
        
        private void Update()
        {
            // 添加测试Buff
            if (Input.GetKeyDown(addBuffKey))
            {
                if (testBuffs.Count > 0)
                {
                    var buff = testBuffs[currentBuffIndex];
                    buffManager.AddBuff(buff);
                    
                    currentBuffIndex = (currentBuffIndex + 1) % testBuffs.Count;
                }
            }
            
            // 移除所有Buff
            if (Input.GetKeyDown(removeAllBuffsKey))
            {
                buffManager.RemoveAllBuffs();
                Debug.Log("[BuffDebugger] Removed all buffs");
            }
            
            // 移除所有Debuff
            if (Input.GetKeyDown(removeAllDebuffsKey))
            {
                buffManager.RemoveAllDebuffs();
                Debug.Log("[BuffDebugger] Removed all debuffs");
            }
            
            // 数字键添加特定Buff
            for (int i = 0; i < Mathf.Min(testBuffs.Count, 10); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    if (i < testBuffs.Count && testBuffs[i] != null)
                    {
                        buffManager.AddBuff(testBuffs[i]);
                    }
                }
            }
        }
        
        private void OnGUI()
        {
            // 显示当前Buff列表
            GUILayout.BeginArea(new Rect(10, 200, 300, 400));
            GUILayout.Label("=== Active Buffs ===");
            
            var buffs = buffManager.GetAllBuffs();
            foreach (var buff in buffs)
            {
                string info = $"{buff.BuffName}";
                if (buff.CurrentStacks > 1)
                {
                    info += $" x{buff.CurrentStacks}";
                }
                if (!buff.Data.IsPermanent)
                {
                    info += $" ({buff.RemainingTime:F1}s)";
                }
                
                GUILayout.Label(info);
            }
            
            GUILayout.Space(10);
            GUILayout.Label($"Total Buffs: {buffManager.GetBuffCount(BuffType.Buff)}");
            GUILayout.Label($"Total Debuffs: {buffManager.GetBuffCount(BuffType.Debuff)}");
            
            GUILayout.Space(10);
            GUILayout.Label("--- Controls ---");
            GUILayout.Label($"[{addBuffKey}] Add Random Buff");
            GUILayout.Label($"[{removeAllBuffsKey}] Remove All Buffs");
            GUILayout.Label($"[{removeAllDebuffsKey}] Remove All Debuffs");
            GUILayout.Label("[1-9] Add Specific Buff");
            
            GUILayout.EndArea();
        }
    }
}