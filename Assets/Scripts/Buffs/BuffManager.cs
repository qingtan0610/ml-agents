using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Buffs
{
    /// <summary>
    /// 管理单个对象上的所有Buff/Debuff
    /// </summary>
    public class BuffManager : MonoBehaviour
    {
        [Header("Active Buffs")]
        [SerializeField] private List<BuffInstance> activeBuffs = new List<BuffInstance>();
        
        [Header("Settings")]
        [SerializeField] private bool allowMultipleDebuffs = true;
        [SerializeField] private int maxBuffCount = 20;
        
        // Events
        public System.Action<BuffInstance> OnBuffAdded;
        public System.Action<BuffInstance> OnBuffRemoved;
        public System.Action<BuffInstance> OnBuffStackChanged;
        
        private void Update()
        {
            UpdateBuffs(Time.deltaTime);
        }
        
        private void UpdateBuffs(float deltaTime)
        {
            // 更新所有buff
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = activeBuffs[i];
                buff.Update(deltaTime);
                
                // 移除过期的buff
                if (buff.IsExpired)
                {
                    RemoveBuff(buff);
                }
            }
        }
        
        public bool AddBuff(BuffBase buffData)
        {
            if (buffData == null) return false;
            
            // 检查是否已有相同的buff
            var existingBuff = activeBuffs.FirstOrDefault(b => b.BuffId == buffData.BuffId);
            
            if (existingBuff != null)
            {
                if (buffData.IsStackable)
                {
                    existingBuff.AddStack();
                    OnBuffStackChanged?.Invoke(existingBuff);
                    Debug.Log($"[BuffManager] Buff {buffData.BuffName} stacked to {existingBuff.CurrentStacks}");
                    return true;
                }
                else
                {
                    // 刷新持续时间
                    RemoveBuff(existingBuff);
                }
            }
            
            // 检查buff数量限制
            if (activeBuffs.Count >= maxBuffCount)
            {
                Debug.LogWarning($"[BuffManager] Max buff count reached ({maxBuffCount})");
                return false;
            }
            
            // 创建新的buff实例
            var newBuff = buffData.CreateInstance(gameObject);
            activeBuffs.Add(newBuff);
            OnBuffAdded?.Invoke(newBuff);
            
            Debug.Log($"[BuffManager] Added buff: {buffData.BuffName}");
            return true;
        }
        
        public void RemoveBuff(string buffId)
        {
            var buff = activeBuffs.FirstOrDefault(b => b.BuffId == buffId);
            if (buff != null)
            {
                RemoveBuff(buff);
            }
        }
        
        private void RemoveBuff(BuffInstance buff)
        {
            buff.Remove();
            activeBuffs.Remove(buff);
            OnBuffRemoved?.Invoke(buff);
            
            Debug.Log($"[BuffManager] Removed buff: {buff.BuffName}");
        }
        
        public void RemoveAllBuffs()
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                RemoveBuff(activeBuffs[i]);
            }
        }
        
        public void RemoveAllDebuffs()
        {
            var debuffs = activeBuffs.Where(b => b.Type == BuffType.Debuff).ToList();
            foreach (var debuff in debuffs)
            {
                RemoveBuff(debuff);
            }
        }
        
        public bool HasBuff(string buffId)
        {
            return activeBuffs.Any(b => b.BuffId == buffId);
        }
        
        public BuffInstance GetBuff(string buffId)
        {
            return activeBuffs.FirstOrDefault(b => b.BuffId == buffId);
        }
        
        public List<BuffInstance> GetAllBuffs()
        {
            return new List<BuffInstance>(activeBuffs);
        }
        
        public List<BuffInstance> GetBuffsByType(BuffType type)
        {
            return activeBuffs.Where(b => b.Type == type).ToList();
        }
        
        public int GetBuffCount(BuffType? type = null)
        {
            if (type.HasValue)
            {
                return activeBuffs.Count(b => b.Type == type.Value);
            }
            return activeBuffs.Count;
        }
        
        // 用于UI显示
        public string GetBuffsDescription()
        {
            var descriptions = new List<string>();
            
            foreach (var buff in activeBuffs)
            {
                string stackInfo = buff.CurrentStacks > 1 ? $" x{buff.CurrentStacks}" : "";
                string timeInfo = buff.Data.IsPermanent ? "" : $" ({buff.RemainingTime:F1}s)";
                descriptions.Add($"{buff.BuffName}{stackInfo}{timeInfo}");
            }
            
            return string.Join("\n", descriptions);
        }
    }
}