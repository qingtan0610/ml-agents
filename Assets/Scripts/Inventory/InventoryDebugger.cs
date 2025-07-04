using UnityEngine;
using System.Text;
using Inventory.Items;
using Inventory.Managers;
using Inventory.Data;


namespace Inventory
{
    public class InventoryDebugger : MonoBehaviour
    {
        [Header("Components")]
        private Inventory inventory;
        private CurrencyManager currencyManager;
        private AmmoManager ammoManager;
        
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugUI = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F2;
        
        [Header("Test Items")]
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private ConsumableItem testFood;
        [SerializeField] private ConsumableItem testPotion;
        [SerializeField] private WeaponItem testWeapon;
        
        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            ammoManager = GetComponent<AmmoManager>();
            
            if (inventory == null)
            {
                Debug.LogError("InventoryDebugger: Inventory component not found!");
                enabled = false;
            }
        }
        
        private void Start()
        {
            // Subscribe to events
            if (inventory != null)
            {
                inventory.OnInventoryChanged.AddListener(OnInventoryChanged);
                inventory.OnWeaponChanged.AddListener(OnWeaponChanged);
                inventory.OnHotbarSelectionChanged.AddListener(OnHotbarChanged);
            }
            
            if (currencyManager != null)
            {
                currencyManager.OnGoldChanged.AddListener(OnGoldChanged);
            }
            
            if (ammoManager != null)
            {
                ammoManager.OnAmmoChanged.AddListener(OnAmmoChanged);
            }
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showDebugUI = !showDebugUI;
            }
            
            HandleTestInputs();
        }
        
        private void HandleTestInputs()
        {
            // Number keys 1-5 for hotbar are handled by Inventory
            
            // I - Add test items
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (testFood != null)
                {
                    bool added = inventory.AddItem(testFood, 3);
                    Debug.Log($"Added food: {added}");
                }
                
                if (testPotion != null)
                {
                    bool added = inventory.AddItem(testPotion, 2);
                    Debug.Log($"Added potion: {added}");
                }
                
                if (testWeapon != null)
                {
                    bool added = inventory.AddItem(testWeapon, 1);
                    Debug.Log($"Added weapon: {added}");
                }
            }
            
            // M - Add gold (改为M键避免与丢弃冲突)
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (currencyManager != null)
                {
                    currencyManager.AddGold(100);
                    Debug.Log("Added 100 gold");
                }
            }
            
            // B - Add bullets
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (ammoManager != null)
                {
                    ammoManager.AddAmmo(AmmoType.Bullets, 30);
                    Debug.Log("Added 30 bullets");
                }
            }
            
            // S - Sort inventory
            if (Input.GetKeyDown(KeyCode.S))
            {
                inventory.SortInventory();
                Debug.Log("Sorted inventory");
            }
            
            // Q - Use selected item (handled by Inventory)
            
            // E - Swap slots test
            if (Input.GetKeyDown(KeyCode.E))
            {
                inventory.SwapSlots(0, 1);
                Debug.Log("Swapped slots 0 and 1");
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            
            float boxWidth = 500f;
            float boxHeight = 600f;
            
            GUILayout.BeginArea(new Rect(Screen.width - boxWidth - 10, 10, boxWidth, boxHeight));
            GUILayout.Box(GetDebugInfo(), style, GUILayout.Width(boxWidth), GUILayout.Height(boxHeight));
            GUILayout.EndArea();
        }
        
        private string GetDebugInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Inventory Debug ===");
            sb.AppendLine();
            
            // Currency and Ammo
            if (currencyManager != null)
            {
                sb.AppendLine($"Gold: {currencyManager.CurrentGold}");
            }
            
            if (ammoManager != null)
            {
                sb.AppendLine($"Ammo - Bullets: {ammoManager.GetAmmo(AmmoType.Bullets)} | " +
                             $"Arrows: {ammoManager.GetAmmo(AmmoType.Arrows)} | " +
                             $"Mana: {ammoManager.GetAmmo(AmmoType.Mana)}");
            }
            
            sb.AppendLine();
            
            // Equipped Weapon
            sb.AppendLine($"Equipped Weapon: {(inventory.EquippedWeapon != null ? inventory.EquippedWeapon.ItemName : "None")}");
            sb.AppendLine($"Selected Hotbar: {inventory.SelectedHotbarSlot + 1}");
            sb.AppendLine();
            
            // Inventory Slots
            sb.AppendLine("--- Inventory ---");
            var slots = inventory.GetAllSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                string hotbarMark = i < inventory.HotbarSize ? $"[{i + 1}] " : "    ";
                
                if (slot.IsEmpty)
                {
                    sb.AppendLine($"{hotbarMark}Slot {i}: [Empty]");
                }
                else
                {
                    sb.AppendLine($"{hotbarMark}Slot {i}: {slot.Item.ItemName} x{slot.Quantity}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("--- Controls ---");
            sb.AppendLine("1-5: Select hotbar | C: Use item | Q: Drop item | I: Add test items");
            sb.AppendLine("M: Add gold | B: Add bullets | S: Sort | E: Swap test");
            sb.AppendLine("Shift+1-0: Drop specific slot item");
            sb.AppendLine("F2: Toggle debug");
            
            return sb.ToString();
        }
        
        // Event handlers
        private void OnInventoryChanged(int slotIndex, ItemSlot slot)
        {
            Debug.Log($"[Inventory] Slot {slotIndex} changed: {(slot.IsEmpty ? "Empty" : $"{slot.Item.ItemName} x{slot.Quantity}")}");
        }
        
        private void OnWeaponChanged(WeaponItem weapon)
        {
            Debug.Log($"[Weapon] Equipped: {(weapon != null ? weapon.ItemName : "None")}");
        }
        
        private void OnHotbarChanged(int slot)
        {
            Debug.Log($"[Hotbar] Selected slot: {slot + 1}");
        }
        
        private void OnGoldChanged(int oldValue, int newValue)
        {
            Debug.Log($"[Currency] Gold: {oldValue} → {newValue}");
        }
        
        private void OnAmmoChanged(AmmoType type, int oldValue, int newValue)
        {
            Debug.Log($"[Ammo] {type}: {oldValue} → {newValue}");
        }
    }
}