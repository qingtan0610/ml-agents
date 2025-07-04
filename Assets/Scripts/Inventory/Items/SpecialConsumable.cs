using UnityEngine;
using Inventory.Managers;

namespace Inventory.Items
{
    /// <summary>
    /// 特殊消耗品（金币、弹药等）- 拾取时自动使用，不占背包空间
    /// </summary>
    [CreateAssetMenu(fileName = "Special_", menuName = "Inventory/Items/Special Consumable")]
    public class SpecialConsumable : ConsumableItem
    {
        [Header("Special Consumable Settings")]
        [SerializeField] private SpecialConsumableType specialType = SpecialConsumableType.Gold;
        [SerializeField] private int value = 1;
        [SerializeField] private AmmoType ammoType = AmmoType.None; // 仅当specialType为Ammo时使用
        
        /// <summary>
        /// 特殊消耗品不占用背包空间
        /// </summary>
        public bool ShouldAddToInventory => false;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置特殊消耗品的默认值
            maxStackSize = 999; // 可以大量堆叠
            
            // 根据类型设置名称
            if (string.IsNullOrEmpty(itemName))
            {
                switch (specialType)
                {
                    case SpecialConsumableType.Gold:
                        itemName = "金币";
                        description = "通用货币";
                        break;
                    case SpecialConsumableType.Ammo:
                        itemName = GetAmmoName();
                        description = GetAmmoDescription();
                        break;
                }
            }
        }
        
        private string GetAmmoName()
        {
            switch (ammoType)
            {
                case AmmoType.Bullets: return "子弹";
                case AmmoType.Arrows: return "箭矢";
                case AmmoType.Mana: return "法力药水";
                default: return "弹药";
            }
        }
        
        private string GetAmmoDescription()
        {
            switch (ammoType)
            {
                case AmmoType.Bullets: return "枪械使用的子弹";
                case AmmoType.Arrows: return "弓箭使用的箭矢";
                case AmmoType.Mana: return "施法使用的法力";
                default: return "武器弹药";
            }
        }
        
        /// <summary>
        /// 拾取时自动使用
        /// </summary>
        public void OnPickup(GameObject picker)
        {
            switch (specialType)
            {
                case SpecialConsumableType.Gold:
                    var currencyManager = picker.GetComponent<CurrencyManager>();
                    if (currencyManager != null)
                    {
                        currencyManager.AddGold(value);
                        Debug.Log($"[SpecialConsumable] Added {value} gold");
                    }
                    break;
                    
                case SpecialConsumableType.Ammo:
                    var ammoManager = picker.GetComponent<AmmoManager>();
                    if (ammoManager != null && ammoType != AmmoType.None)
                    {
                        ammoManager.AddAmmo(ammoType, value);
                        Debug.Log($"[SpecialConsumable] Added {value} {ammoType}");
                    }
                    break;
            }
        }
    }
    
    public enum SpecialConsumableType
    {
        Gold,   // 金币
        Ammo    // 弹药
    }
}