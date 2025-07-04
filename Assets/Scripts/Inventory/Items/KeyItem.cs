using UnityEngine;

namespace Inventory.Items
{
    /// <summary>
    /// 钥匙物品 - 作为普通杂项物品，不实现IUsable
    /// </summary>
    [CreateAssetMenu(fileName = "Key_", menuName = "Inventory/Items/Key")]
    public class KeyItem : ItemBase
    {
        [Header("Key Settings")]
        [SerializeField] private bool isUniversal = true;
        [SerializeField] private string specificLockId = "";
        
        public bool IsUniversal => isUniversal;
        public string SpecificLockId => specificLockId;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置物品类型为杂项
            itemType = ItemType.Misc;
            
            // 钥匙可以堆叠
            if (maxStackSize < 1)
                maxStackSize = 99;
            
            // 设置默认名称和描述
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = isUniversal ? "通用钥匙" : "特殊钥匙";
            }
            
            if (string.IsNullOrEmpty(description))
            {
                description = isUniversal ? "可以打开任何锁着的宝箱" : $"打开特定的锁 ({specificLockId})";
            }
        }
        
        public override string GetTooltipText()
        {
            var tooltip = base.GetTooltipText();
            
            if (isUniversal)
            {
                tooltip += "\n\n<color=#FFD700>通用钥匙 - 可开启所有锁</color>";
            }
            else if (!string.IsNullOrEmpty(specificLockId))
            {
                tooltip += $"\n\n<color=#C0C0C0>专用钥匙 - {specificLockId}</color>";
            }
            
            return tooltip;
        }
    }
}