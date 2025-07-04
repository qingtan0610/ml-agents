using System;
using UnityEngine;
using Inventory.Items;

namespace NPC.Data
{
    /// <summary>
    /// 用于存储武器升级信息的运行时数据
    /// </summary>
    [Serializable]
    public class WeaponUpgradeData
    {
        public string weaponId;
        public string baseName;
        public int upgradeLevel;
        public float damageBonus;
        public float attackSpeedBonus;
        public float rangeBonus;
        public float critChanceBonus;
        
        public WeaponUpgradeData(WeaponItem weapon)
        {
            weaponId = weapon.ItemId;
            baseName = weapon.ItemName;
            upgradeLevel = 0;
            damageBonus = 0;
            attackSpeedBonus = 0;
            rangeBonus = 0;
            critChanceBonus = 0;
        }
        
        /// <summary>
        /// 获取升级后的武器名称
        /// </summary>
        public string GetUpgradedName()
        {
            if (upgradeLevel > 0)
            {
                return $"{baseName} +{upgradeLevel}";
            }
            return baseName;
        }
        
        /// <summary>
        /// 获取升级后的伤害值
        /// </summary>
        public float GetUpgradedDamage(float baseDamage)
        {
            return baseDamage + damageBonus;
        }
        
        /// <summary>
        /// 获取升级后的攻速
        /// </summary>
        public float GetUpgradedAttackSpeed(float baseSpeed)
        {
            return baseSpeed + attackSpeedBonus;
        }
        
        /// <summary>
        /// 获取升级后的攻击范围
        /// </summary>
        public float GetUpgradedRange(float baseRange)
        {
            return baseRange + rangeBonus;
        }
        
        /// <summary>
        /// 获取升级后的暴击率
        /// </summary>
        public float GetUpgradedCritChance(float baseCrit)
        {
            return baseCrit + critChanceBonus;
        }
    }
}