using UnityEngine;
using System.Collections.Generic;
using NPC.Data;
using Inventory.Items;

namespace NPC.Managers
{
    /// <summary>
    /// 管理武器升级数据的单例
    /// </summary>
    public class WeaponUpgradeManager : MonoBehaviour
    {
        private static WeaponUpgradeManager instance;
        public static WeaponUpgradeManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<WeaponUpgradeManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("WeaponUpgradeManager");
                        instance = go.AddComponent<WeaponUpgradeManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        private Dictionary<string, WeaponUpgradeData> upgradeDataDict = new Dictionary<string, WeaponUpgradeData>();
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        /// <summary>
        /// 获取武器的升级数据
        /// </summary>
        public WeaponUpgradeData GetUpgradeData(WeaponItem weapon)
        {
            if (weapon == null) return null;
            
            string key = weapon.ItemId;
            if (!upgradeDataDict.ContainsKey(key))
            {
                upgradeDataDict[key] = new WeaponUpgradeData(weapon);
            }
            
            return upgradeDataDict[key];
        }
        
        /// <summary>
        /// 应用升级效果
        /// </summary>
        public void ApplyUpgrade(WeaponItem weapon, WeaponUpgrade upgrade)
        {
            if (weapon == null || upgrade == null) return;
            
            var data = GetUpgradeData(weapon);
            data.upgradeLevel++;
            data.damageBonus += upgrade.damageIncrease;
            data.attackSpeedBonus += upgrade.attackSpeedIncrease;
            data.rangeBonus += upgrade.rangeIncrease;
            data.critChanceBonus += upgrade.critChanceIncrease;
        }
        
        /// <summary>
        /// 获取武器的升级等级
        /// </summary>
        public int GetWeaponUpgradeLevel(WeaponItem weapon)
        {
            if (weapon == null) return 0;
            
            var data = GetUpgradeData(weapon);
            return data.upgradeLevel;
        }
        
        /// <summary>
        /// 获取升级后的武器属性显示文本
        /// </summary>
        public string GetUpgradedStatsText(WeaponItem weapon)
        {
            var data = GetUpgradeData(weapon);
            if (data.upgradeLevel == 0) return "";
            
            string text = $"\n<color=green>强化 +{data.upgradeLevel}</color>";
            if (data.damageBonus > 0) text += $"\n伤害 +{data.damageBonus}";
            if (data.attackSpeedBonus > 0) text += $"\n攻速 +{data.attackSpeedBonus}";
            if (data.rangeBonus > 0) text += $"\n范围 +{data.rangeBonus}";
            if (data.critChanceBonus > 0) text += $"\n暴击 +{data.critChanceBonus:P0}";
            
            return text;
        }
        
        /// <summary>
        /// 清除所有升级数据
        /// </summary>
        public void ClearAllData()
        {
            upgradeDataDict.Clear();
        }
        
        /// <summary>
        /// 保存升级数据
        /// </summary>
        public Dictionary<string, WeaponUpgradeData> GetSaveData()
        {
            return new Dictionary<string, WeaponUpgradeData>(upgradeDataDict);
        }
        
        /// <summary>
        /// 加载升级数据
        /// </summary>
        public void LoadSaveData(Dictionary<string, WeaponUpgradeData> data)
        {
            if (data != null)
            {
                upgradeDataDict = new Dictionary<string, WeaponUpgradeData>(data);
            }
        }
    }
}