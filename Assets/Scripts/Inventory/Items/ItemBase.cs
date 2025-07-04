using UnityEngine;
using Inventory.Interfaces;

namespace Inventory.Items
{
    public abstract class ItemBase : ScriptableObject, IStackable
    {
        [Header("Basic Info")]
        [SerializeField] protected string itemId;
        [SerializeField] protected string itemName;
        [SerializeField, TextArea(3, 5)] protected string description;
        [SerializeField] protected Sprite icon;
        [SerializeField] protected ItemType itemType;
        [SerializeField] protected ItemRarity rarity = ItemRarity.Common;
        
        [Header("Stack Settings")]
        [SerializeField] protected int maxStackSize = 1;
        
        [Header("Economy")]
        [SerializeField] protected int buyPrice = 100;
        [SerializeField] protected int sellPrice = 50;
        
        [Header("Behavior")]
        [SerializeField] protected bool isDroppable = true;
        [SerializeField] protected bool isTradeable = true;
        [SerializeField] protected bool isDestroyable = true;
        
        // Properties
        public string ItemId => itemId;
        public string ItemName => itemName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemType ItemType => itemType;
        public ItemRarity Rarity => rarity;
        public int MaxStackSize => maxStackSize;
        public bool IsStackable => maxStackSize > 1;
        public int BuyPrice => buyPrice;
        public int SellPrice => sellPrice;
        public bool IsDroppable => isDroppable;
        public bool IsTradeable => isTradeable;
        public bool IsDestroyable => isDestroyable;
        
        // Virtual methods for override
        public virtual string GetTooltipText()
        {
            return $"<b>{itemName}</b>\n<color=#888888>{GetRarityColor()}{rarity}</color>\n\n{description}\n\n价值: {sellPrice} 金币";
        }
        
        protected string GetRarityColor()
        {
            switch (rarity)
            {
                case ItemRarity.Common: return "<color=#FFFFFF>";
                case ItemRarity.Uncommon: return "<color=#1EFF00>";
                case ItemRarity.Rare: return "<color=#0070FF>";
                case ItemRarity.Epic: return "<color=#A335EE>";
                case ItemRarity.Legendary: return "<color=#FF8000>";
                default: return "<color=#FFFFFF>";
            }
        }
        
        // Editor validation
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = name.ToLower().Replace(" ", "_");
            }
            
            // Ensure sell price is not higher than buy price
            if (sellPrice > buyPrice)
            {
                sellPrice = Mathf.FloorToInt(buyPrice * 0.5f);
            }
        }
    }
}