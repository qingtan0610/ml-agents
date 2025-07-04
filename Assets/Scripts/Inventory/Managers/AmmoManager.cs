using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace Inventory.Managers
{
    [Serializable]
    public class AmmoChangeEvent : UnityEvent<AmmoType, int, int> { } // type, oldValue, newValue
    
    public class AmmoManager : MonoBehaviour
    {
        [SerializeField] private Dictionary<AmmoType, int> ammoStorage = new Dictionary<AmmoType, int>();
        
        [Header("Debug View")]
        [SerializeField] private int bullets;
        [SerializeField] private int arrows;
        [SerializeField] private int mana;
        
        public AmmoChangeEvent OnAmmoChanged = new AmmoChangeEvent();
        
        private void Awake()
        {
            InitializeAmmo();
        }
        
        private void InitializeAmmo()
        {
            var aiStats = GetComponent<AI.Stats.AIStats>();
            if (aiStats != null && aiStats.Config != null)
            {
                var config = aiStats.Config;
                ammoStorage[AmmoType.Bullets] = Mathf.RoundToInt(config.initialBullets);
                ammoStorage[AmmoType.Arrows] = Mathf.RoundToInt(config.initialArrows);
                ammoStorage[AmmoType.Mana] = Mathf.RoundToInt(config.initialMana);
            }
            else
            {
                // Default values
                ammoStorage[AmmoType.Bullets] = 30;
                ammoStorage[AmmoType.Arrows] = 20;
                ammoStorage[AmmoType.Mana] = 50;
            }
            
            UpdateDebugView();
        }
        
        public int GetAmmo(AmmoType type)
        {
            if (type == AmmoType.None) return int.MaxValue;
            
            if (ammoStorage.ContainsKey(type))
            {
                return ammoStorage[type];
            }
            return 0;
        }
        
        public bool HasAmmo(AmmoType type, int amount = 1)
        {
            if (type == AmmoType.None) return true;
            return GetAmmo(type) >= amount;
        }
        
        public bool UseAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None) return true;
            if (!HasAmmo(type, amount)) return false;
            
            int oldValue = GetAmmo(type);
            ammoStorage[type] = Mathf.Max(0, oldValue - amount);
            int newValue = ammoStorage[type];
            
            UpdateDebugView();
            OnAmmoChanged?.Invoke(type, oldValue, newValue);
            return true;
        }
        
        public void AddAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None) return;
            
            int oldValue = GetAmmo(type);
            int maxAmmo = GetMaxAmmo(type);
            ammoStorage[type] = Mathf.Clamp(oldValue + amount, 0, maxAmmo);
            int newValue = ammoStorage[type];
            
            if (oldValue != newValue)
            {
                UpdateDebugView();
                OnAmmoChanged?.Invoke(type, oldValue, newValue);
            }
        }
        
        public void SetAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None) return;
            
            int oldValue = GetAmmo(type);
            int maxAmmo = GetMaxAmmo(type);
            ammoStorage[type] = Mathf.Clamp(amount, 0, maxAmmo);
            int newValue = ammoStorage[type];
            
            if (oldValue != newValue)
            {
                UpdateDebugView();
                OnAmmoChanged?.Invoke(type, oldValue, newValue);
            }
        }
        
        private int GetMaxAmmo(AmmoType type)
        {
            var aiStats = GetComponent<AI.Stats.AIStats>();
            if (aiStats != null && aiStats.Config != null)
            {
                var config = aiStats.Config;
                switch (type)
                {
                    case AmmoType.Bullets: return Mathf.RoundToInt(config.maxBullets);
                    case AmmoType.Arrows: return Mathf.RoundToInt(config.maxArrows);
                    case AmmoType.Mana: return Mathf.RoundToInt(config.maxMana);
                }
            }
            
            // Default max values
            switch (type)
            {
                case AmmoType.Bullets: return 999;
                case AmmoType.Arrows: return 999;
                case AmmoType.Mana: return 100;
                default: return 999;
            }
        }
        
        private void UpdateDebugView()
        {
            bullets = GetAmmo(AmmoType.Bullets);
            arrows = GetAmmo(AmmoType.Arrows);
            mana = GetAmmo(AmmoType.Mana);
        }
        
        // Mana regeneration
        private void Update()
        {
            // Mana regenerates slowly over time
            if (Time.frameCount % 60 == 0) // Every second
            {
                AddAmmo(AmmoType.Mana, 1);
            }
        }
        
        // Save/Load
        public AmmoData GetAmmoData()
        {
            return new AmmoData
            {
                bullets = GetAmmo(AmmoType.Bullets),
                arrows = GetAmmo(AmmoType.Arrows),
                mana = GetAmmo(AmmoType.Mana)
            };
        }
        
        public void LoadAmmoData(AmmoData data)
        {
            if (data != null)
            {
                SetAmmo(AmmoType.Bullets, data.bullets);
                SetAmmo(AmmoType.Arrows, data.arrows);
                SetAmmo(AmmoType.Mana, data.mana);
            }
        }
    }
    
    [Serializable]
    public class AmmoData
    {
        public int bullets;
        public int arrows;
        public int mana;
    }
}