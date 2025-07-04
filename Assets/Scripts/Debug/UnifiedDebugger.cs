using UnityEngine;
using System.Text;
using AI.Stats;
using Inventory;
using Inventory.Items;
using Inventory.Managers;

namespace Debugging
{
    /// <summary>
    /// 统一的调试系统，合并AI属性和背包调试功能
    /// </summary>
    public class UnifiedDebugger : MonoBehaviour
    {
        [Header("Components")]
        private AIStats aiStats;
        private Inventory.Inventory inventory;
        private CurrencyManager currencyManager;
        private AmmoManager ammoManager;
        
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugUI = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        
        [Header("Test Items")]
        [SerializeField] private ConsumableItem testFood;
        [SerializeField] private ConsumableItem testPotion;
        [SerializeField] private WeaponItem testWeapon;
        
        private bool initialized = false;
        private Texture2D backgroundTexture;
        
        private void Start()
        {
            // 获取组件
            aiStats = GetComponent<AIStats>();
            inventory = GetComponent<Inventory.Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            ammoManager = GetComponent<AmmoManager>();
            
            // 创建背景纹理
            backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 0.9f));
            backgroundTexture.Apply();
            
            initialized = true;
            
            UnityEngine.Debug.Log("[UnifiedDebugger] Initialized. Press F1 to toggle debug UI.");
        }
        
        private void Update()
        {
            if (!initialized) return;
            
            // F1 - 切换调试界面
            if (Input.GetKeyDown(toggleKey))
            {
                showDebugUI = !showDebugUI;
            }
            
            // 只在调试界面开启时处理测试输入
            if (showDebugUI)
            {
                HandleTestInputs();
            }
        }
        
        private void HandleTestInputs()
        {
            // === AI属性测试 ===
            // H - 伤害
            if (Input.GetKeyDown(KeyCode.H) && aiStats != null)
            {
                aiStats.ModifyStat(StatType.Health, -20f, StatChangeReason.Combat);
            }
            
            // J - 治疗
            if (Input.GetKeyDown(KeyCode.J) && aiStats != null)
            {
                aiStats.ModifyStat(StatType.Health, 30f, StatChangeReason.Item);
            }
            
            // === 背包测试 ===
            // I - 添加测试物品
            if (Input.GetKeyDown(KeyCode.I) && inventory != null)
            {
                if (testFood != null) inventory.AddItem(testFood, 3);
                if (testPotion != null) inventory.AddItem(testPotion, 2);
                if (testWeapon != null) inventory.AddItem(testWeapon, 1);
            }
            
            // G - 添加金币
            if (Input.GetKeyDown(KeyCode.G) && currencyManager != null)
            {
                currencyManager.AddGold(100);
            }
            
            // B - 添加弹药
            if (Input.GetKeyDown(KeyCode.B) && ammoManager != null)
            {
                ammoManager.AddAmmo(AmmoType.Bullets, 50);
                ammoManager.AddAmmo(AmmoType.Arrows, 30);
            }
            
            // R - 复活（如果死亡）
            if (Input.GetKeyDown(KeyCode.R) && aiStats != null && aiStats.IsDead)
            {
                aiStats.Respawn(transform.position);
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugUI || !initialized) return;
            
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;
            style.normal.background = backgroundTexture;
            style.fontSize = 12;
            style.padding = new RectOffset(10, 10, 10, 10);
            style.wordWrap = true;
            
            float boxWidth = 400f;
            float boxHeight = 500f;
            
            string debugInfo = GetDebugInfo();
            GUI.Box(new Rect(10, 10, boxWidth, boxHeight), debugInfo, style);
        }
        
        private string GetDebugInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Unified Debug System (F1 Toggle) ===");
            sb.AppendLine();
            
            // AI Stats
            if (aiStats != null && aiStats.Config != null)
            {
                sb.AppendLine("--- AI Stats ---");
                sb.AppendLine($"Status: {(aiStats.IsDead ? "DEAD" : "ALIVE")}");
                sb.AppendLine($"Health: {aiStats.GetStat(StatType.Health):F0}/{aiStats.Config.maxHealth}");
                sb.AppendLine($"Hunger: {aiStats.GetStat(StatType.Hunger):F0}/{aiStats.Config.maxHunger}");
                sb.AppendLine($"Thirst: {aiStats.GetStat(StatType.Thirst):F0}/{aiStats.Config.maxThirst}");
                sb.AppendLine($"Stamina: {aiStats.GetStat(StatType.Stamina):F0}/{aiStats.Config.maxStamina}");
                sb.AppendLine();
            }
            
            // Inventory
            if (inventory != null)
            {
                sb.AppendLine("--- Inventory ---");
                sb.AppendLine($"Used/Total: {GetUsedSlots()}/{inventory.Size}");
                var weapon = inventory.EquippedWeapon;
                sb.AppendLine($"Weapon: {(weapon != null ? weapon.ItemName : "Unarmed")}");
                sb.AppendLine();
                
                // Show items in inventory
                sb.AppendLine("Items:");
                for (int i = 0; i < Mathf.Min(inventory.Size, 10); i++) // Show first 10 slots
                {
                    var slot = inventory.GetSlot(i);
                    if (slot != null && !slot.IsEmpty)
                    {
                        sb.AppendLine($"  [{i}] {slot.Item.ItemName} x{slot.Quantity}");
                    }
                }
                sb.AppendLine();
            }
            
            // Currency and Ammo
            if (currencyManager != null)
            {
                sb.AppendLine($"Gold: {currencyManager.CurrentGold}");
            }
            
            if (ammoManager != null)
            {
                sb.AppendLine($"Bullets: {ammoManager.GetAmmo(AmmoType.Bullets)}");
                sb.AppendLine($"Arrows: {ammoManager.GetAmmo(AmmoType.Arrows)}");
                sb.AppendLine($"Mana: {ammoManager.GetAmmo(AmmoType.Mana)}");
            }
            
            sb.AppendLine();
            sb.AppendLine("--- Hotkeys ---");
            sb.AppendLine("H:Damage J:Heal I:Items");
            sb.AppendLine("G:Gold B:Ammo R:Respawn");
            sb.AppendLine("1-5:Hotbar TAB:Inventory");
            
            return sb.ToString();
        }
        
        private int GetUsedSlots()
        {
            if (inventory == null) return 0;
            
            int used = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    used++;
                }
            }
            return used;
        }
        
        private void OnDestroy()
        {
            if (backgroundTexture != null)
            {
                Destroy(backgroundTexture);
            }
        }
    }
}