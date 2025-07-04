using UnityEngine;
using System.Text;
using AI.Stats;
using Inventory;
using Inventory.Items;
using Inventory.Managers;
using Debug = UnityEngine.Debug;

namespace PlayerDebug
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
            
            // M - 添加金币（改为M键避免与丢弃冲突）
            if (Input.GetKeyDown(KeyCode.M) && currencyManager != null)
            {
                currencyManager.AddGold(100);
            }
            
            // B - 添加弹药
            if (Input.GetKeyDown(KeyCode.B) && ammoManager != null)
            {
                ammoManager.AddAmmo(AmmoType.Bullets, 50);
                ammoManager.AddAmmo(AmmoType.Arrows, 30);
            }
            
            // G - 添加金币（开发测试）
            if (Input.GetKeyDown(KeyCode.G) && currencyManager != null)
            {
                currencyManager.AddGold(100);
                Debug.Log($"[UnifiedDebugger] Added 100 gold. Total: {currencyManager.CurrentGold}");
            }
            
            // R - 复活（如果死亡）
            if (Input.GetKeyDown(KeyCode.R) && aiStats != null)
            {
                if (aiStats.IsDead)
                {
                    Debug.Log("[UnifiedDebugger] Attempting respawn through debugger");
                    // 找到地图生成器获取出生点
                    var mapGen = FindObjectOfType<Rooms.MapGenerator>();
                    Vector3 spawnPos = mapGen != null ? mapGen.GetSpawnPosition() : new Vector3(128, 128, 0);
                    
                    // 先移动位置
                    transform.position = spawnPos;
                    // 再执行复活
                    aiStats.Respawn(spawnPos);
                    
                    // 确保相机跟随
                    if (Camera.main != null)
                    {
                        Camera.main.transform.position = new Vector3(spawnPos.x, spawnPos.y, Camera.main.transform.position.z);
                    }
                }
                else
                {
                    Debug.Log("[UnifiedDebugger] R pressed but player not dead");
                }
            }
            
            // K - 强制杀死玩家（测试死亡）
            if (Input.GetKeyDown(KeyCode.K) && aiStats != null && !aiStats.IsDead)
            {
                Debug.Log("[UnifiedDebugger] Force killing player for testing");
                Debug.Log($"[UnifiedDebugger] Before kill - Health: {aiStats.GetStat(StatType.Health)}, IsDead: {aiStats.IsDead}");
                
                aiStats.ModifyStat(StatType.Health, -1000f, StatChangeReason.Combat);
                
                // 立即检查结果
                Debug.Log($"[UnifiedDebugger] After kill - Health: {aiStats.GetStat(StatType.Health)}, IsDead: {aiStats.IsDead}");
            }
            
            // T - 测试饥饿死亡
            if (Input.GetKeyDown(KeyCode.T) && aiStats != null && !aiStats.IsDead)
            {
                Debug.Log("[UnifiedDebugger] Testing hunger death");
                Debug.Log($"[UnifiedDebugger] Before hunger kill - Hunger: {aiStats.GetStat(StatType.Hunger)}, IsDead: {aiStats.IsDead}");
                Debug.Log($"[UnifiedDebugger] Config exists: {aiStats.Config != null}");
                Debug.Log($"[UnifiedDebugger] Is stats dead flag: {aiStats.IsDead}");
                
                // 直接设置饥饿值为0而不是减少1000
                aiStats.SetStat(StatType.Hunger, 0f, StatChangeReason.Natural);
                
                // 等待一帧让CheckDeathConditions执行
                StartCoroutine(CheckHungerDeathAfterFrame());
            }
            
            // 数字键1-9 - 使用对应槽位的物品
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    UseItemInSlot(i);
                }
            }
            
            // 数字键0 - 使用第10个槽位的物品
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                UseItemInSlot(9);
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
                if (weapon != null)
                {
                    var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
                    int upgradeLevel = upgradeManager.GetWeaponUpgradeLevel(weapon);
                    
                    sb.AppendLine($"Weapon: {weapon.ItemName}{(upgradeLevel > 0 ? $" +{upgradeLevel}" : "")}");
                    sb.AppendLine($"Type: {weapon.WeaponType}, Shape: {weapon.AttackShape}");
                    
                    // 显示强化后的属性
                    float damage = upgradeManager.GetUpgradedDamage(weapon);
                    float speed = upgradeManager.GetUpgradedAttackSpeed(weapon);
                    float range = upgradeManager.GetUpgradedAttackRange(weapon);
                    float crit = upgradeManager.GetUpgradedCritChance(weapon);
                    
                    sb.AppendLine($"Damage: {damage:F1}{(damage > weapon.Damage ? $" (+{damage - weapon.Damage:F1})" : "")}");
                    sb.AppendLine($"Speed: {speed:F1}{(speed > weapon.AttackSpeed ? $" (+{speed - weapon.AttackSpeed:F1})" : "")}");
                    if (range > weapon.AttackRange) sb.AppendLine($"Range: {range:F1} (+{range - weapon.AttackRange:F1})");
                    if (crit > weapon.CriticalChance) sb.AppendLine($"Crit: {crit:P0} (+{(crit - weapon.CriticalChance):P0})");
                }
                else
                {
                    sb.AppendLine("Weapon: Unarmed");
                }
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
            sb.AppendLine("M:Gold B:Ammo G:+100Gold R:Respawn");
            sb.AppendLine("K:Kill(Test) T:Hunger Death");
            sb.AppendLine("1-0:Hotbar Mouse:Attack(with visual)");
            
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
        
        /// <summary>
        /// 使用指定槽位的物品
        /// </summary>
        private void UseItemInSlot(int slotIndex)
        {
            if (inventory == null || slotIndex < 0 || slotIndex >= inventory.Size)
                return;
                
            var slot = inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                UnityEngine.Debug.Log($"[UnifiedDebugger] Slot {slotIndex + 1} is empty");
                return;
            }
            
            var item = slot.Item;
            
            // 使用物品 - 直接调用Inventory的UseItem方法
            UnityEngine.Debug.Log($"[UnifiedDebugger] Using item in slot {slotIndex + 1}: {item.ItemName}");
            
            bool success = inventory.UseItem(slotIndex);
            
            if (!success)
            {
                UnityEngine.Debug.Log($"[UnifiedDebugger] Failed to use item: {item.ItemName}");
            }
        }
        
        private System.Collections.IEnumerator CheckHungerDeathAfterFrame()
        {
            yield return null; // 等待一帧
            Debug.Log($"[UnifiedDebugger] After frame - Hunger: {aiStats.GetStat(StatType.Hunger)} (display), {aiStats.GetRawStat(StatType.Hunger)} (raw), IsDead: {aiStats.IsDead}");
            
            yield return new WaitForSeconds(0.1f); // 再等一小段时间
            Debug.Log($"[UnifiedDebugger] After 0.1s - Hunger: {aiStats.GetStat(StatType.Hunger)} (display), {aiStats.GetRawStat(StatType.Hunger)} (raw), IsDead: {aiStats.IsDead}");
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