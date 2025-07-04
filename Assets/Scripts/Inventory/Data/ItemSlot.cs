using System;
using UnityEngine;
using Inventory.Items;

namespace Inventory.Data
{
    [Serializable]
    public class ItemSlot
    {
        [SerializeField] private ItemBase item;
        [SerializeField] private int quantity;
        [SerializeField] private int slotIndex;
        
        public ItemBase Item => item;
        public int Quantity => quantity;
        public int SlotIndex => slotIndex;
        public bool IsEmpty => item == null || quantity <= 0;
        
        public ItemSlot(int index)
        {
            slotIndex = index;
            Clear();
        }
        
        public void Set(ItemBase newItem, int newQuantity)
        {
            item = newItem;
            quantity = Mathf.Max(0, newQuantity);
            
            if (quantity == 0)
            {
                Clear();
            }
        }
        
        public void Add(int amount)
        {
            if (item == null) return;
            
            quantity = Mathf.Min(quantity + amount, item.MaxStackSize);
        }
        
        public void Remove(int amount)
        {
            if (item == null) return;
            
            quantity -= amount;
            if (quantity <= 0)
            {
                Clear();
            }
        }
        
        public void Clear()
        {
            item = null;
            quantity = 0;
        }
        
        public bool CanAddItem(ItemBase itemToAdd, int amount = 1)
        {
            if (itemToAdd == null) return false;
            
            // Empty slot can accept any item
            if (IsEmpty) return true;
            
            // Same item and has space
            if (item.ItemId == itemToAdd.ItemId && item.IsStackable)
            {
                return quantity + amount <= item.MaxStackSize;
            }
            
            return false;
        }
        
        public int GetAvailableSpace()
        {
            if (IsEmpty) return int.MaxValue;
            if (!item.IsStackable) return 0;
            return item.MaxStackSize - quantity;
        }
        
        public ItemSlot Clone()
        {
            var clone = new ItemSlot(slotIndex);
            clone.Set(item, quantity);
            return clone;
        }
    }
}