using UnityEngine;
using UnityEngine.Events;
using System;

namespace Inventory.Managers
{
    [Serializable]
    public class CurrencyChangeEvent : UnityEvent<int, int> { } // oldValue, newValue
    
    public class CurrencyManager : MonoBehaviour
    {
        [SerializeField] private int currentGold = 0;
        [SerializeField] private int maxGold = 999999;
        
        public int CurrentGold => currentGold;
        public CurrencyChangeEvent OnGoldChanged = new CurrencyChangeEvent();
        
        private void Start()
        {
            // Initialize with some starting gold
            AddGold(100);
        }
        
        public bool CanAfford(int amount)
        {
            return currentGold >= amount;
        }
        
        public bool SpendGold(int amount)
        {
            if (!CanAfford(amount)) return false;
            
            int oldValue = currentGold;
            currentGold -= amount;
            OnGoldChanged?.Invoke(oldValue, currentGold);
            return true;
        }
        
        public void AddGold(int amount)
        {
            int oldValue = currentGold;
            currentGold = Mathf.Clamp(currentGold + amount, 0, maxGold);
            
            if (oldValue != currentGold)
            {
                OnGoldChanged?.Invoke(oldValue, currentGold);
            }
        }
        
        public void SetGold(int amount)
        {
            int oldValue = currentGold;
            currentGold = Mathf.Clamp(amount, 0, maxGold);
            
            if (oldValue != currentGold)
            {
                OnGoldChanged?.Invoke(oldValue, currentGold);
            }
        }
        
        // Clear gold on death (based on death type)
        public void ClearGoldOnDeath(AI.Stats.StatType deathCause)
        {
            var config = GetComponent<AI.Stats.AIStats>()?.Config;
            if (config == null) return;
            
            bool shouldClear = false;
            
            switch (deathCause)
            {
                case AI.Stats.StatType.Health:
                    shouldClear = config.clearMoneyOnHealthDeath;
                    break;
                case AI.Stats.StatType.Hunger:
                    shouldClear = config.clearMoneyOnHungerDeath;
                    break;
            }
            
            if (shouldClear)
            {
                SetGold(0);
                Debug.Log($"Gold cleared due to death by {deathCause}");
            }
        }
        
        // Save/Load
        public CurrencyData GetCurrencyData()
        {
            return new CurrencyData { gold = currentGold };
        }
        
        public void LoadCurrencyData(CurrencyData data)
        {
            if (data != null)
            {
                SetGold(data.gold);
            }
        }
    }
    
    [Serializable]
    public class CurrencyData
    {
        public int gold;
    }
}