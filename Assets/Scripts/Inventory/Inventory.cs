using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Items;
using Inventory.Data;

namespace Inventory
{
    [Serializable]
    public class InventoryChangeEvent : UnityEvent<int, ItemSlot> { } // slotIndex, slot
    
    public class Inventory : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int inventorySize = 10;
        [SerializeField] private int hotbarSize = 5;
        
        [Header("Drop Settings")]
        [SerializeField] private GameObject unifiedPickupPrefab; // 拖入UnifiedPickup预制体
        
        [Header("Inventory Data")]
        [SerializeField] private List<ItemSlot> slots = new List<ItemSlot>();
        [SerializeField] private WeaponItem equippedWeapon;
        [SerializeField] private int selectedHotbarSlot = 0;
        
        // Events
        public InventoryChangeEvent OnInventoryChanged = new InventoryChangeEvent();
        public UnityEvent<WeaponItem> OnWeaponChanged = new UnityEvent<WeaponItem>();
        public UnityEvent<int> OnHotbarSelectionChanged = new UnityEvent<int>();
        
        // Properties
        public int Size => inventorySize;
        public int HotbarSize => hotbarSize;
        public WeaponItem EquippedWeapon => equippedWeapon;
        public int SelectedHotbarSlot => selectedHotbarSlot;
        
        private void Awake()
        {
            InitializeInventory();
        }
        
        private void Start()
        {
            // 监听复活事件，复活时取消装备
            var aiStats = GetComponent<AI.Stats.AIStats>();
            if (aiStats != null)
            {
                aiStats.OnRespawn.AddListener(OnPlayerRespawn);
            }
        }
        
        private void OnPlayerRespawn()
        {
            Debug.Log("[Inventory] Player respawned, unequipping weapon");
            UnequipWeapon();
        }
        
        private void InitializeInventory()
        {
            slots.Clear();
            for (int i = 0; i < inventorySize; i++)
            {
                slots.Add(new ItemSlot(i));
            }
        }
        
        #region Add Items
        
        public bool AddItem(ItemBase item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            
            int remaining = quantity;
            
            // First, try to stack with existing items
            if (item.IsStackable)
            {
                foreach (var slot in slots)
                {
                    if (slot.CanAddItem(item, remaining))
                    {
                        int spaceAvailable = slot.GetAvailableSpace();
                        int toAdd = Mathf.Min(remaining, spaceAvailable);
                        
                        if (slot.IsEmpty)
                        {
                            slot.Set(item, toAdd);
                        }
                        else
                        {
                            slot.Add(toAdd);
                        }
                        
                        remaining -= toAdd;
                        OnInventoryChanged?.Invoke(slot.SlotIndex, slot);
                        
                        if (remaining <= 0) return true;
                    }
                }
            }
            
            // Then, try to find empty slots
            foreach (var slot in slots)
            {
                if (slot.IsEmpty)
                {
                    int toAdd = Mathf.Min(remaining, item.MaxStackSize);
                    slot.Set(item, toAdd);
                    remaining -= toAdd;
                    OnInventoryChanged?.Invoke(slot.SlotIndex, slot);
                    
                    if (remaining <= 0) return true;
                }
            }
            
            // Return false if we couldn't add all items
            return remaining <= 0;
        }
        
        public bool CanAddItem(ItemBase item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            
            int remaining = quantity;
            
            // Check existing stacks
            if (item.IsStackable)
            {
                foreach (var slot in slots)
                {
                    if (slot.CanAddItem(item, remaining))
                    {
                        int spaceAvailable = slot.GetAvailableSpace();
                        remaining -= Mathf.Min(remaining, spaceAvailable);
                        
                        if (remaining <= 0) return true;
                    }
                }
            }
            
            // Check empty slots
            int emptySlots = slots.Count(s => s.IsEmpty);
            int slotsNeeded = Mathf.CeilToInt((float)remaining / item.MaxStackSize);
            
            return slotsNeeded <= emptySlots;
        }
        
        #endregion
        
        #region Remove Items
        
        public bool RemoveItem(ItemBase item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            
            // 检查要移除的物品是否是当前装备的武器
            if (item is WeaponItem weapon && equippedWeapon != null && 
                equippedWeapon.ItemId == weapon.ItemId)
            {
                Debug.Log($"[Inventory] Removing equipped weapon {weapon.ItemName}, unequipping first");
                UnequipWeapon();
            }
            
            int remaining = quantity;
            
            // Remove from slots containing this item
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId)
                {
                    int toRemove = Mathf.Min(remaining, slot.Quantity);
                    slot.Remove(toRemove);
                    remaining -= toRemove;
                    OnInventoryChanged?.Invoke(slot.SlotIndex, slot);
                    
                    if (remaining <= 0) return true;
                }
            }
            
            return remaining <= 0;
        }
        
        public void RemoveItemAt(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;
            
            var slot = slots[slotIndex];
            if (!slot.IsEmpty)
            {
                // 检查要移除的物品是否是当前装备的武器
                if (slot.Item is WeaponItem weapon && equippedWeapon != null && 
                    equippedWeapon.ItemId == weapon.ItemId)
                {
                    Debug.Log($"[Inventory] Removing equipped weapon {weapon.ItemName} from slot {slotIndex}, unequipping first");
                    UnequipWeapon();
                }
                
                slot.Remove(quantity);
                OnInventoryChanged?.Invoke(slotIndex, slot);
            }
        }
        
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;
            
            slots[slotIndex].Clear();
            OnInventoryChanged?.Invoke(slotIndex, slots[slotIndex]);
        }
        
        #endregion
        
        #region Use Items
        
        public bool UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;
            
            var slot = slots[slotIndex];
            if (slot.IsEmpty) return false;
            
            var item = slot.Item;
            
            // Handle different item types
            if (item is ConsumableItem consumable)
            {
                if (consumable.CanUse(gameObject))
                {
                    consumable.Use(gameObject);
                    RemoveItemAt(slotIndex, 1);
                    return true;
                }
            }
            else if (item is WeaponItem weapon)
            {
                EquipWeapon(weapon);
                return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Weapon Management
        
        public void EquipWeapon(WeaponItem weapon)
        {
            if (weapon == null) return;
            
            // Unequip current weapon
            if (equippedWeapon != null)
            {
                equippedWeapon.OnUnequip(gameObject);
            }
            
            // Equip new weapon
            equippedWeapon = weapon;
            weapon.OnEquip(gameObject);
            OnWeaponChanged?.Invoke(weapon);
        }
        
        public void UnequipWeapon()
        {
            if (equippedWeapon != null)
            {
                equippedWeapon.OnUnequip(gameObject);
                equippedWeapon = null;
                OnWeaponChanged?.Invoke(null);
            }
        }
        
        #endregion
        
        #region Hotbar
        
        public void SelectHotbarSlot(int slot)
        {
            if (slot < 0 || slot >= hotbarSize) return;
            
            selectedHotbarSlot = slot;
            OnHotbarSelectionChanged?.Invoke(slot);
            
            // Auto-equip if it's a weapon
            var itemSlot = GetSlot(slot);
            if (!itemSlot.IsEmpty && itemSlot.Item is WeaponItem weapon)
            {
                EquipWeapon(weapon);
            }
        }
        
        public void UseSelectedHotbarItem()
        {
            UseItem(selectedHotbarSlot);
        }
        
        private void Update()
        {
            // 检查是否按住Shift键
            bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            
            // Hotbar key bindings (1-9)
            for (int i = 0; i < 9; i++) // 改为固定9个键，不受hotbarSize限制
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    if (isShiftHeld)
                    {
                        // Shift+数字键 = 丢弃物品（可以操作所有槽位，不限于hotbar）
                        if (i < slots.Count) // 确保槽位存在
                        {
                            Debug.Log($"[Inventory] Shift+{i+1} pressed, dropping item from slot {i}");
                            DropItem(i, 1);
                        }
                        else
                        {
                            Debug.Log($"[Inventory] Shift+{i+1} pressed, but slot {i} doesn't exist (inventory size: {slots.Count})");
                        }
                    }
                    else
                    {
                        // 普通选择仍然受hotbarSize限制
                        if (i < hotbarSize)
                        {
                            SelectHotbarSlot(i);
                        }
                    }
                }
            }
            
            // Key 0 for slot 10 (index 9)
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                if (isShiftHeld)
                {
                    // Shift+0 = 丢弃第10格物品（可以操作所有槽位，不限于hotbar）
                    if (9 < slots.Count) // 确保第10个槽位存在
                    {
                        Debug.Log("[Inventory] Shift+0 pressed, dropping item from slot 9");
                        DropItem(9, 1);
                    }
                    else
                    {
                        Debug.Log($"[Inventory] Shift+0 pressed, but slot 9 doesn't exist (inventory size: {slots.Count})");
                    }
                }
                else
                {
                    // 普通选择仍然受hotbarSize限制
                    if (hotbarSize >= 10)
                    {
                        SelectHotbarSlot(9);
                    }
                }
            }
            
            // Use selected item with C key
            if (Input.GetKeyDown(KeyCode.C))
            {
                UseSelectedHotbarItem();
            }
            
            // Drop selected item with Q key
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Debug.Log($"[Inventory] Q pressed, dropping item from selected slot {selectedHotbarSlot}");
                DropItem(selectedHotbarSlot, 1);
            }
        }
        
        #endregion
        
        #region Drop Items
        
        public bool DropItem(int slotIndex, int quantity = 1)
        {
            Debug.Log($"[Inventory] DropItem called: slot={slotIndex}, quantity={quantity}");
            
            if (slotIndex < 0 || slotIndex >= slots.Count) 
            {
                Debug.LogWarning($"[Inventory] Invalid slot index {slotIndex} (valid range: 0-{slots.Count-1})");
                return false;
            }
            
            var slot = slots[slotIndex];
            if (slot.IsEmpty) 
            {
                Debug.LogWarning($"[Inventory] Cannot drop from empty slot {slotIndex}");
                return false;
            }
            
            var item = slot.Item;
            int actualQuantity = Mathf.Min(quantity, slot.Quantity);
            Debug.Log($"[Inventory] Dropping {actualQuantity}x {item.ItemName} from slot {slotIndex}");
            
            // 如果是装备中的武器，先卸下
            if (item is WeaponItem weapon && equippedWeapon != null && 
                weapon.ItemId == equippedWeapon.ItemId)
            {
                Debug.Log($"[Inventory] Unequipping weapon {weapon.ItemName} before dropping");
                UnequipWeapon();
            }
            
            // 创建掉落物
            if (CreateDroppedItem(item, actualQuantity))
            {
                // 从背包移除
                RemoveItemAt(slotIndex, actualQuantity);
                Debug.Log($"[Inventory] Successfully dropped {actualQuantity}x {item.ItemName}");
                return true;
            }
            else
            {
                Debug.LogError($"[Inventory] Failed to create dropped item for {item.ItemName}");
            }
            
            return false;
        }
        
        public void DropAllItems()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty)
                {
                    DropItem(i, slot.Quantity);
                }
            }
        }
        
        private bool CreateDroppedItem(ItemBase item, int quantity)
        {
            if (item == null)
            {
                Debug.LogError("[Inventory] Cannot drop null item");
                return false;
            }
            
            if (quantity <= 0)
            {
                Debug.LogError($"[Inventory] Cannot drop {item.ItemName} with quantity {quantity}");
                return false;
            }
            
            Debug.Log($"[Inventory] Creating dropped item: {quantity}x {item.ItemName}");
            
            // 优先使用Inspector中指定的预制体
            GameObject pickupPrefab = unifiedPickupPrefab;
            
            // 如果没有指定，尝试从Resources加载
            if (pickupPrefab == null)
            {
                Debug.Log("[Inventory] No prefab assigned, trying to load from Resources");
                pickupPrefab = Resources.Load<GameObject>("Prefabs/Pickups/UnifiedPickup");
                if (pickupPrefab == null)
                {
                    pickupPrefab = Resources.Load<GameObject>("Pickups/UnifiedPickup");
                    if (pickupPrefab == null)
                    {
                        pickupPrefab = Resources.Load<GameObject>("UnifiedPickup");
                    }
                }
                
                if (pickupPrefab == null)
                {
                    Debug.LogError("[Inventory] UnifiedPickup prefab not found! Please assign it in the Inspector or place it in Resources folder");
                    return false;
                }
                else
                {
                    Debug.Log($"[Inventory] Loaded pickup prefab from Resources: {pickupPrefab.name}");
                }
            }
            
            // 计算掉落位置（在玩家附近随机位置，避免重叠）
            Vector2 dropDirection = UnityEngine.Random.insideUnitCircle.normalized;
            if (dropDirection == Vector2.zero) dropDirection = Vector2.right;
            
            float dropDistance = UnityEngine.Random.Range(0.5f, 1f);
            Vector3 dropPosition = transform.position + (Vector3)(dropDirection * dropDistance);
            
            Debug.Log($"[Inventory] Drop position calculated: {dropPosition}");
            
            // 创建拾取物
            GameObject droppedItem = null;
            try
            {
                droppedItem = Instantiate(pickupPrefab, dropPosition, Quaternion.identity);
                Debug.Log($"[Inventory] Instantiated pickup object: {droppedItem.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Inventory] Failed to instantiate pickup prefab: {e.Message}");
                return false;
            }
            
            var pickup = droppedItem.GetComponent<Loot.UnifiedPickup>();
            
            if (pickup != null)
            {
                Debug.Log($"[Inventory] Found UnifiedPickup component, initializing with {quantity}x {item.ItemName}");
                pickup.Initialize(item, quantity);
                
                Debug.Log($"[Inventory] Successfully dropped {quantity}x {item.ItemName} at {dropPosition}");
                return true;
            }
            else
            {
                Debug.LogError($"[Inventory] UnifiedPickup component not found on instantiated object {droppedItem.name}");
                Destroy(droppedItem);
                return false;
            }
        }
        
        #endregion
        
        #region Utility
        
        public ItemSlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return null;
            return slots[index];
        }
        
        public List<ItemSlot> GetAllSlots()
        {
            return new List<ItemSlot>(slots);
        }
        
        public int GetItemCount(ItemBase item)
        {
            if (item == null) return 0;
            
            return slots.Where(s => !s.IsEmpty && s.Item.ItemId == item.ItemId)
                       .Sum(s => s.Quantity);
        }
        
        public int GetMaxSlots()
        {
            return inventorySize;
        }
        
        public bool ExpandCapacity(int additionalSlots)
        {
            if (additionalSlots <= 0) return false;
            
            // 增加背包容量
            inventorySize += additionalSlots;
            
            // 添加新的槽位
            for (int i = 0; i < additionalSlots; i++)
            {
                slots.Add(new ItemSlot(slots.Count));
            }
            
            Debug.Log($"Inventory expanded by {additionalSlots} slots. New capacity: {inventorySize}");
            
            // 触发背包变化事件
            OnInventoryChanged?.Invoke(-1, null); // -1 表示容量变化
            
            return true;
        }
        
        public ItemBase FindItemById(string itemId)
        {
            var slot = slots.FirstOrDefault(s => !s.IsEmpty && s.Item.ItemId == itemId);
            return slot?.Item;
        }
        
        public T FindItemOfType<T>() where T : ItemBase
        {
            var slot = slots.FirstOrDefault(s => !s.IsEmpty && s.Item is T);
            return slot?.Item as T;
        }
        
        public bool SwapSlots(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= slots.Count) return false;
            if (toIndex < 0 || toIndex >= slots.Count) return false;
            if (fromIndex == toIndex) return false;
            
            var temp = slots[fromIndex].Clone();
            slots[fromIndex].Set(slots[toIndex].Item, slots[toIndex].Quantity);
            slots[toIndex].Set(temp.Item, temp.Quantity);
            
            OnInventoryChanged?.Invoke(fromIndex, slots[fromIndex]);
            OnInventoryChanged?.Invoke(toIndex, slots[toIndex]);
            
            return true;
        }
        
        public void SortInventory()
        {
            // Group items by type and rarity
            var sortedItems = new List<(ItemBase item, int quantity)>();
            
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    sortedItems.Add((slot.Item, slot.Quantity));
                }
            }
            
            sortedItems = sortedItems
                .OrderBy(i => i.item.ItemType)
                .ThenByDescending(i => i.item.Rarity)
                .ThenBy(i => i.item.ItemName)
                .ToList();
            
            // Clear all slots
            foreach (var slot in slots)
            {
                slot.Clear();
            }
            
            // Re-add sorted items
            foreach (var (item, quantity) in sortedItems)
            {
                AddItem(item, quantity);
            }
        }
        
        #endregion
        
        #region Death Handling
        
        public void HandleDeath(AI.Stats.StatType deathCause)
        {
            var config = GetComponent<AI.Stats.AIStats>()?.Config;
            if (config == null) return;
            
            // Clear items based on death type
            switch (deathCause)
            {
                case AI.Stats.StatType.Health:
                    if (config.clearEquipmentOnHealthDeath)
                    {
                        ClearAllItems();
                    }
                    break;
                    
                case AI.Stats.StatType.Thirst:
                    if (config.clearPotionsOnThirstDeath)
                    {
                        ClearItemsByType(ConsumableType.Potion);
                    }
                    break;
            }
        }
        
        private void ClearAllItems()
        {
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    slot.Clear();
                    OnInventoryChanged?.Invoke(slot.SlotIndex, slot);
                }
            }
            UnequipWeapon();
        }
        
        private void ClearItemsByType(ConsumableType type)
        {
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Item is ConsumableItem consumable)
                {
                    // Check if this is a potion (would need to add consumableType property to ConsumableItem)
                    slot.Clear();
                    OnInventoryChanged?.Invoke(slot.SlotIndex, slot);
                }
            }
        }
        
        #endregion
        
        #region Save/Load
        
        public InventoryData GetInventoryData()
        {
            var data = new InventoryData();
            data.slots = new List<SlotData>();
            
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    data.slots.Add(new SlotData
                    {
                        slotIndex = slot.SlotIndex,
                        itemId = slot.Item.ItemId,
                        quantity = slot.Quantity
                    });
                }
            }
            
            if (equippedWeapon != null)
            {
                data.equippedWeaponId = equippedWeapon.ItemId;
            }
            
            data.selectedHotbarSlot = selectedHotbarSlot;
            
            return data;
        }
        
        public void LoadInventoryData(InventoryData data, ItemDatabase itemDatabase)
        {
            if (data == null || itemDatabase == null) return;
            
            // Clear inventory first
            InitializeInventory();
            
            // Load items
            foreach (var slotData in data.slots)
            {
                var item = itemDatabase.GetItem(slotData.itemId);
                if (item != null && slotData.slotIndex < slots.Count)
                {
                    slots[slotData.slotIndex].Set(item, slotData.quantity);
                    OnInventoryChanged?.Invoke(slotData.slotIndex, slots[slotData.slotIndex]);
                }
            }
            
            // Load equipped weapon
            if (!string.IsNullOrEmpty(data.equippedWeaponId))
            {
                var weapon = itemDatabase.GetItem(data.equippedWeaponId) as WeaponItem;
                if (weapon != null)
                {
                    EquipWeapon(weapon);
                }
            }
            
            // Load hotbar selection
            SelectHotbarSlot(data.selectedHotbarSlot);
        }
        
        #endregion
    }
    
    [Serializable]
    public class InventoryData
    {
        public List<SlotData> slots;
        public string equippedWeaponId;
        public int selectedHotbarSlot;
    }
    
    [Serializable]
    public class SlotData
    {
        public int slotIndex;
        public string itemId;
        public int quantity;
    }
}