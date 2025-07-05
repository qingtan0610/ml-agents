using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NPC;
using NPC.Core;
using NPC.Types;
using NPC.Data;
using Inventory;
using Inventory.Managers;
using Inventory.Items;

namespace PlayerDebug
{
    /// <summary>
    /// NPC交互调试面板 - 显示NPC交互和交易信息，支持所有NPC类型
    /// </summary>
    public class NPCInteractionDebugger : MonoBehaviour
    {
        [Header("Components")]
        private Inventory.Inventory playerInventory;
        private CurrencyManager currencyManager;
        private AmmoManager ammoManager;
        
        [Header("Debug Settings")]
        [SerializeField] private bool alwaysShow = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F4;
        [SerializeField] private float panelWidth = 450f;
        [SerializeField] private float updateInterval = 0.5f;
        
        [Header("Interaction Tracking")]
        [SerializeField] private NPCBase currentNPC;
        [SerializeField] private float lastInteractionTime;
        
        [Header("Quick Test Settings")]
        [SerializeField] private int testItemIndex = 0; // 测试购买的商品索引
        [SerializeField] private int testServiceIndex = 0; // 测试服务的索引
        [SerializeField] private bool showDetailedInfo = true; // 显示详细信息
        
        // 状态机制
        private enum InteractionMode
        {
            None,           // 默认状态
            MainMenu,       // 主菜单（1对话 2购买 3卖出 4服务）
            BuyMenu,        // 购买菜单
            SellMenu,       // 卖出菜单
            ServiceMenu,    // 服务菜单
            ServiceSelect   // 具体服务选择
        }
        
        private InteractionMode currentMode = InteractionMode.None;
        private string currentServiceType = "";
        private List<string> currentMenuItems = new List<string>(); // 当前菜单项
        
        private bool showDebugPanel = false;
        private Texture2D backgroundTexture;
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle buttonStyle;
        private float nextUpdateTime = 0f;
        
        // 缓存的NPC信息
        private string cachedNPCInfo = "";
        private string cachedShopInfo = "";
        
        
        private void Start()
        {
            // 获取玩家组件
            playerInventory = GetComponent<Inventory.Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            ammoManager = GetComponent<AmmoManager>();
            
            // 调试组件状态
            Debug.Log($"[NPCInteractionDebugger] Components status:");
            Debug.Log($"  - Inventory: {(playerInventory != null ? "Found" : "NULL")}");
            Debug.Log($"  - CurrencyManager: {(currencyManager != null ? "Found" : "NULL")}");
            Debug.Log($"  - AmmoManager: {(ammoManager != null ? "Found" : "NULL")}");
            
            Debug.Log("[NPCInteractionDebugger] Initialized. Press F4 to toggle debug panel.");
        }
        
        private void InitializeStyles()
        {
            // 创建背景纹理
            if (backgroundTexture == null)
            {
                backgroundTexture = new Texture2D(1, 1);
                backgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                backgroundTexture.Apply();
            }
            
            // 尝试加载中文字体
            Font chineseFont = null;
            
            // 方法1: 尝试从Resources加载字体（如果有的话）
            try
            {
                chineseFont = Resources.Load<Font>("Fonts/SimHei");
                if (chineseFont != null)
                {
                    Debug.Log("[NPCDebugger] Loaded Chinese font from Resources");
                }
            }
            catch
            {
                // 忽略错误，继续尝试其他方法
            }
            
            // 方法2: 如果Resources中没有字体，尝试使用系统字体
            if (chineseFont == null)
            {
                string[] chineseFontNames = {
                    "Microsoft YaHei",  // Windows
                    "PingFang SC",      // macOS
                    "SimHei",           // Windows
                    "SimSun",           // Windows
                    "Noto Sans CJK SC", // Linux
                    "WenQuanYi Micro Hei" // Linux
                };
                
                foreach (string fontName in chineseFontNames)
                {
                    try
                    {
                        chineseFont = Font.CreateDynamicFontFromOSFont(fontName, 16);
                        if (chineseFont != null)
                        {
                            Debug.Log($"[NPCDebugger] Successfully loaded OS font: {fontName}");
                            break;
                        }
                    }
                    catch
                    {
                        // 继续尝试下一个字体
                    }
                }
            }
            
            panelStyle = new GUIStyle();
            panelStyle.normal.background = backgroundTexture;
            panelStyle.padding = new RectOffset(10, 10, 10, 10);
            
            // 创建GUI样式，如果有中文字体就使用，否则使用默认字体
            headerStyle = new GUIStyle(GUI.skin.label);
            if (chineseFont != null) headerStyle.font = chineseFont;
            headerStyle.fontSize = 16;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.cyan;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            
            labelStyle = new GUIStyle(GUI.skin.label);
            if (chineseFont != null) labelStyle.font = chineseFont;
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;
            labelStyle.wordWrap = true;
            
            valueStyle = new GUIStyle(GUI.skin.label);
            if (chineseFont != null) valueStyle.font = chineseFont;
            valueStyle.fontSize = 12;
            valueStyle.normal.textColor = Color.yellow;
            valueStyle.wordWrap = true;
            
            buttonStyle = new GUIStyle(GUI.skin.button);
            if (chineseFont != null) buttonStyle.font = chineseFont;
            buttonStyle.fontSize = 11;
            
            if (chineseFont == null)
            {
                Debug.LogWarning("[NPCDebugger] No Chinese font found. Chinese characters may appear as squares.");
            }
        }
        
        private void Update()
        {
            // 切换面板显示
            if (Input.GetKeyDown(toggleKey))
            {
                showDebugPanel = !showDebugPanel;
            }
            
            // 检测当前交互的NPC
            DetectCurrentNPC();
            
            // 定期更新缓存信息
            if (Time.time >= nextUpdateTime && currentNPC != null)
            {
                UpdateCachedInfo();
                nextUpdateTime = Time.time + updateInterval;
            }
            
            // 测试快捷键（即使面板隐藏也要响应）
            if (currentNPC != null)
            {
                HandleTestInputs();
            }
        }
        
        private void HandleTestInputs()
        {
            if (currentNPC == null) return;
            
            
            // ESC键退出当前菜单
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (currentMode != InteractionMode.None)
                {
                    currentMode = InteractionMode.None;
                    Debug.Log("[NPCDebugger] Exited menu");
                    return;
                }
            }
            
            // 根据当前模式处理输入
            switch (currentMode)
            {
                case InteractionMode.None:
                    HandleNormalMode();
                    break;
                case InteractionMode.MainMenu:
                    HandleMainMenu();
                    break;
                case InteractionMode.BuyMenu:
                    HandleBuyMenu();
                    break;
                case InteractionMode.SellMenu:
                    HandleSellMenu();
                    break;
                case InteractionMode.ServiceMenu:
                    HandleServiceMenu();
                    break;
                case InteractionMode.ServiceSelect:
                    HandleServiceSelection();
                    break;
            }
            
            // L键切换详细信息显示
            if (Input.GetKeyDown(KeyCode.L))
            {
                showDetailedInfo = !showDetailedInfo;
                UpdateCachedInfo();
                Debug.Log($"[NPCDebugger] Detailed info: {(showDetailedInfo ? "ON" : "OFF")}");
            }
        }
        
        private void DetectCurrentNPC()
        {
            // 检测范围内的NPC（移除IsInteracting限制）
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 3f);
            NPCBase closestNPC = null;
            float closestDistance = float.MaxValue;
            
            foreach (var collider in colliders)
            {
                var npc = collider.GetComponent<NPCBase>();
                if (npc != null) // 移除IsInteracting检查
                {
                    float distance = Vector2.Distance(transform.position, npc.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestNPC = npc;
                    }
                }
            }
            
            // 更新当前NPC
            if (closestNPC != currentNPC)
            {
                currentNPC = closestNPC;
                if (currentNPC != null)
                {
                    lastInteractionTime = Time.time;
                    testItemIndex = 0; // 重置测试索引
                    testServiceIndex = 0;
                    UpdateCachedInfo();
                    
                    // 自动显示面板
                    if (!alwaysShow && !showDebugPanel)
                    {
                        showDebugPanel = true;
                    }
                    
                    Debug.Log($"[NPCInteractionDebugger] 检测到NPC: {currentNPC.name}，显示调试面板");
                }
                else if (!alwaysShow)
                {
                    // 离开NPC时自动隐藏
                    showDebugPanel = false;
                    Debug.Log("[NPCInteractionDebugger] 离开NPC，隐藏调试面板");
                }
            }
        }
        
        private void UpdateCachedInfo()
        {
            if (currentNPC == null) return;
            
            var npcData = currentNPC.Data;
            if (npcData == null) 
            {
                Debug.LogError($"[NPCDebugger] NPC {currentNPC.name} has null data!");
                cachedNPCInfo = $"ERROR: {currentNPC.name} has no data";
                cachedShopInfo = "No NPC data available";
                return;
            }
            
            Debug.Log($"[NPCDebugger] Updating info for {npcData.npcName} ({npcData.npcType}) - Mode: {currentMode}");
            
            // 构建NPC基础信息
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== {npcData.npcName} ===");
            sb.AppendLine($"Type: {npcData.npcType}");
            cachedNPCInfo = sb.ToString();
            
            // 根据当前模式显示不同内容
            switch (currentMode)
            {
                case InteractionMode.None:
                    // 在默认状态下显示NPC的商店和服务信息
                    cachedShopInfo = GetNPCOverviewInfo();
                    break;
                    
                case InteractionMode.MainMenu:
                    cachedShopInfo = GetMainMenuDisplay();
                    break;
                    
                case InteractionMode.BuyMenu:
                    cachedShopInfo = GetBuyMenuDisplay();
                    break;
                    
                case InteractionMode.SellMenu:
                    cachedShopInfo = GetSellMenuDisplay();
                    break;
                    
                case InteractionMode.ServiceMenu:
                    cachedShopInfo = GetServiceMenuDisplay();
                    break;
                    
                case InteractionMode.ServiceSelect:
                    cachedShopInfo = GetServiceSelectDisplay();
                    break;
                    
                default:
                    cachedShopInfo = "";
                    break;
            }
        }
        
        private void UpdateMerchantInfo(MerchantNPC merchant)
        {
            var merchantData = merchant.Data as MerchantData;
            if (merchantData == null) return;
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== SHOP INVENTORY ===");
            
            float priceMultiplier = merchant.GetPriceMultiplier();
            sb.AppendLine($"Price Multiplier: {priceMultiplier:F2}x");
            sb.AppendLine($"Use B+Number to buy items");
            sb.AppendLine();
            
            for (int i = 0; i < merchantData.shopInventory.items.Count && i < 10; i++)
            {
                var shopItem = merchantData.shopInventory.items[i];
                if (shopItem.item == null) continue;
                
                float basePrice = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                float finalPrice = basePrice * priceMultiplier;
                
                if (shopItem.isDailyDeal && merchantData.hasDailyDeals)
                {
                    finalPrice *= (1f - merchantData.dailyDealDiscount / 100f);
                }
                
                string stockText = shopItem.stock == -1 ? "∞" : shopItem.stock.ToString();
                string dealText = shopItem.isDailyDeal ? " [DEAL!]" : "";
                int keyNumber = (i + 1) % 10;
                
                sb.AppendLine($"[B+{keyNumber}] {shopItem.item.ItemName}{dealText}");
                sb.AppendLine($"  Price: {finalPrice:F0} gold | Stock: {stockText}");
                
                if (showDetailedInfo)
                {
                    // 显示物品属性
                    if (shopItem.item is ConsumableItem consumable)
                    {
                        // 使用GetTooltipText()方法获取效果信息
                        string tooltip = consumable.GetTooltipText();
                        if (tooltip.Contains("效果:"))
                        {
                            var lines = tooltip.Split('\n');
                            foreach (var line in lines)
                            {
                                if (line.Contains("效果:") || (line.StartsWith("+") || line.StartsWith("-")))
                                {
                                    sb.AppendLine($"    {line.Trim()}");
                                }
                            }
                        }
                    }
                    else if (shopItem.item is WeaponItem weapon)
                    {
                        sb.AppendLine($"    Damage: {weapon.Damage} | Attack Speed: {weapon.AttackSpeed}");
                        sb.AppendLine($"    Range: {weapon.AttackRange} | Type: {weapon.WeaponType}");
                    }
                }
                
                sb.AppendLine();
            }
            
            cachedShopInfo = sb.ToString();
        }
        
        private string GetNPCSpecificInfo(NPCBase npc)
        {
            Debug.Log($"[NPCDebugger] GetNPCSpecificInfo called for {npc.GetType().Name}");
            
            if (npc is RestaurantNPC restaurant)
            {
                Debug.Log("[NPCDebugger] Detected RestaurantNPC");
                return GetRestaurantInfo(restaurant);
            }
            else if (npc is DoctorNPC doctor)
            {
                Debug.Log("[NPCDebugger] Detected DoctorNPC");
                return GetDoctorInfo(doctor);
            }
            else if (npc is BlacksmithNPC blacksmith)
            {
                Debug.Log("[NPCDebugger] Detected BlacksmithNPC");
                return GetBlacksmithInfo(blacksmith);
            }
            else if (npc is TailorNPC tailor)
            {
                Debug.Log("[NPCDebugger] Detected TailorNPC");
                return GetTailorInfo(tailor);
            }
            
            Debug.LogWarning($"[NPCDebugger] Unknown NPC type: {npc.GetType().Name}");
            return $"Unknown NPC type: {npc.GetType().Name}";
        }
        
        private string GetRestaurantInfo(RestaurantNPC restaurant)
        {
            var restaurantData = restaurant.Data as RestaurantData;
            if (restaurantData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== RESTAURANT MENU ===");
            
            // 免费水
            if (restaurantData.provideFreeWater)
            {
                sb.AppendLine($"[0] Free Water");
                sb.AppendLine($"  Thirst+{restaurantData.waterRestoreAmount}");
            }
            
            // 菜单项目
            for (int i = 0; i < restaurantData.menu.Count && i < 9; i++)
            {
                var item = restaurantData.menu[i];
                string special = item.isSpecialDish ? " [SPECIAL]" : "";
                
                sb.AppendLine($"[{i + 1}] {item.itemName}{special}");
                sb.AppendLine($"  ${item.price}");
                
                if (showDetailedInfo)
                {
                    List<string> effects = new List<string>();
                    if (item.hungerRestore > 0) effects.Add($"H+{item.hungerRestore}");
                    if (item.thirstRestore > 0) effects.Add($"T+{item.thirstRestore}");
                    if (item.healthRestore > 0) effects.Add($"HP+{item.healthRestore}");
                    if (item.staminaRestore > 0) effects.Add($"SP+{item.staminaRestore}");
                    if (item.hasBuffEffect && item.buffData != null)
                        effects.Add($"{item.buffData.buffName}({item.buffData.duration}s)");
                    
                    if (effects.Count > 0)
                        sb.AppendLine($"  {string.Join(" ", effects)}");
                }
            }
            
            return sb.ToString();
        }
        
        private string GetDoctorInfo(DoctorNPC doctor)
        {
            var doctorData = doctor.Data as DoctorData;
            if (doctorData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== MEDICAL SERVICES ===");
            
            // 显示治疗服务
            for (int i = 0; i < doctorData.services.Count && i < 9; i++)
            {
                var service = doctorData.services[i];
                int cost = doctor.GetServiceCost(service.serviceName);
                bool canAfford = doctor.CanProvideService(gameObject, service.serviceName);
                string status = canAfford ? "" : " [NO GOLD]";
                
                sb.AppendLine($"[{i + 1}] {service.serviceName}");
                sb.AppendLine($"  ${cost}{status}");
                
                if (showDetailedInfo)
                {
                    if (service.fullHeal) 
                        sb.AppendLine("  Full heal");
                    else if (service.healthRestore > 0) 
                        sb.AppendLine($"  HP+{service.healthRestore}");
                    if (service.providesImmunity)
                        sb.AppendLine($"  Immune:{service.immunityType}({service.immunityDuration}s)");
                }
            }
            
            // 显示药品商店
            if (doctorData.medicineShop != null && doctorData.medicineShop.items.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== MEDICINE SHOP (B+Num) ===");
                int idx = 0;
                foreach (var item in doctorData.medicineShop.items)
                {
                    if (item.item == null || idx >= 10) continue;
                    float price = item.priceOverride > 0 ? item.priceOverride : item.item.BuyPrice;
                    price *= doctorData.medicinePriceMultiplier;
                    string stock = item.stock == -1 ? "inf" : item.stock.ToString();
                    sb.AppendLine($"[B+{(idx+1)%10}]{item.item.ItemName} ${price:F0} x{stock}");
                    idx++;
                }
            }
            
            return sb.ToString();
        }
        
        private string GetBlacksmithInfo(BlacksmithNPC blacksmith)
        {
            var blacksmithData = blacksmith.Data as BlacksmithData;
            if (blacksmithData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            
            // 显示打造配方
            sb.AppendLine("=== WEAPON CRAFTING ===");
            for (int i = 0; i < blacksmithData.recipes.Count && i < 9; i++)
            {
                var recipe = blacksmithData.recipes[i];
                bool canCraft = blacksmith.CanCraft(gameObject, recipe.recipeName);
                string status = canCraft ? "" : " [NEED MATERIALS]";
                
                sb.AppendLine($"[{i + 1}] {recipe.recipeName}");
                sb.AppendLine($"  ${recipe.craftingFee}{status}");
                
                if (showDetailedInfo)
                {
                    foreach (var mat in recipe.requiredMaterials)
                    {
                        int has = playerInventory?.GetItemCount(mat.material) ?? 0;
                        string hasText = has >= mat.amount ? "✓" : "✗";
                        sb.AppendLine($"  {hasText} {mat.material.ItemName} x{mat.amount} (has:{has})");
                    }
                }
            }
            
            // 显示强化服务
            sb.AppendLine();
            sb.AppendLine("=== WEAPON UPGRADE ===");
            sb.AppendLine("Press Alt+3 to upgrade equipped weapon");
            sb.AppendLine($"Max level: +{blacksmithData.maxUpgradeLevel}");
            
            var weapon = GetFirstWeaponInInventory();
            if (weapon != null)
            {
                var upgradeManager = NPC.Managers.WeaponUpgradeManager.Instance;
                int currentLevel = upgradeManager.GetWeaponUpgradeLevel(weapon);
                sb.AppendLine($"Weapon: {weapon.ItemName} +{currentLevel}");
                
                // 显示当前等级的强化选项
                var upgrade = blacksmithData.upgradeOptions.Find(u => currentLevel >= u.minUpgradeLevel && currentLevel <= u.maxUpgradeLevel);
                if (upgrade != null && currentLevel < blacksmithData.maxUpgradeLevel)
                {
                    sb.AppendLine($"Next: +{currentLevel + 1}");
                    sb.AppendLine($"Success: {upgrade.baseSuccessRate * 100:F0}%");
                    sb.AppendLine($"Cost: ${upgrade.baseUpgradeFee}");
                }
                else if (currentLevel >= blacksmithData.maxUpgradeLevel)
                {
                    sb.AppendLine("MAX LEVEL REACHED!");
                }
            }
            else
            {
                sb.AppendLine("No weapon in bag");
            }
            
            return sb.ToString();
        }
        
        private string GetTailorInfo(TailorNPC tailor)
        {
            var tailorData = tailor.Data as TailorData;
            if (tailorData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TAILOR SERVICE (Alt+2) ===");
            
            // 获取当前背包容量
            int currentSlots = playerInventory != null ? playerInventory.GetMaxSlots() : 10;
            sb.AppendLine($"Current bag: {currentSlots} slots");
            sb.AppendLine();
            
            // 显示可用升级
            bool hasAvailableUpgrade = false;
            for (int i = 0; i < tailorData.bagUpgrades.Count; i++)
            {
                var upgrade = tailorData.bagUpgrades[i];
                if (currentSlots >= upgrade.requiredCurrentSlots && currentSlots < upgrade.maxSlots)
                {
                    hasAvailableUpgrade = true;
                    int cost = (int)(upgrade.upgradeFee * tailorData.upgradePriceMultiplier);
                    bool canAfford = tailor.CanProvideService(gameObject, upgrade.upgradeName);
                    string status = canAfford ? "" : " [X]";
                    sb.AppendLine($"[Alt+2,{i}] {upgrade.upgradeName}");
                    sb.AppendLine($"  Upgrade to: {upgrade.maxSlots} (+{upgrade.slotsToAdd})");
                    sb.AppendLine($"  Cost: ${cost}{status}");
                    
                    if (showDetailedInfo && upgrade.requiredMaterials.Count > 0)
                    {
                        sb.AppendLine("  Materials:");
                        foreach (var mat in upgrade.requiredMaterials)
                        {
                            int has = playerInventory?.GetItemCount(mat.material) ?? 0;
                            string hasText = has >= mat.amount ? "+" : "-";
                            sb.AppendLine($"    {hasText}{mat.material.ItemName}x{mat.amount}(has:{has})");
                        }
                    }
                }
            }
            
            if (!hasAvailableUpgrade)
            {
                sb.AppendLine("[Max capacity reached]");
            }
            
            // 其他服务
            if (tailorData.canDyeClothes)
            {
                sb.AppendLine();
                sb.AppendLine("* Dyeing (Coming soon)");
            }
            if (tailorData.canRepairArmor)
                sb.AppendLine("* Repair (Coming soon)");
            
            return sb.ToString();
        }
        
        // 购买和卖出方法
        private void BuyShopItem(int itemIndex)
        {
            if (currentNPC == null) return;
            
            if (currentNPC is MerchantNPC merchant)
            {
                BuyFromMerchant(merchant, itemIndex);
            }
            else if (currentNPC is DoctorNPC doctor)
            {
                BuyFromDoctor(doctor, itemIndex);
            }
            else
            {
                Debug.Log($"[NPCDebugger] {currentNPC.Data.npcName} doesn't sell items");
            }
        }
        
        private void SellInventoryItem(int slotIndex)
        {
            if (playerInventory == null)
            {
                Debug.Log("[NPCDebugger] Player inventory is null");
                return;
            }
            
            if (slotIndex >= playerInventory.Size)
            {
                Debug.Log($"[NPCDebugger] Invalid slot index {slotIndex + 1}");
                return;
            }
            
            var slot = playerInventory.GetSlot(slotIndex);
            if (slot == null)
            {
                Debug.Log($"[NPCDebugger] Inventory slot {slotIndex + 1} is null");
                return;
            }
            
            if (slot.IsEmpty)
            {
                Debug.Log($"[NPCDebugger] Inventory slot {slotIndex + 1} is empty");
                return;
            }
            
            if (slot.Item == null)
            {
                Debug.Log($"[NPCDebugger] Item in slot {slotIndex + 1} is null");
                return;
            }
            
            // Calculate sell price (usually 50% of buy price)
            var sellPrice = slot.Item.SellPrice;
            var totalPrice = sellPrice * slot.Quantity;
            
            Debug.Log($"[NPCDebugger] Selling {slot.Item.ItemName} x{slot.Quantity} for {totalPrice} gold");
            
            // Add gold to player
            if (currencyManager != null)
            {
                currencyManager.AddGold(totalPrice);
            }
            else
            {
                Debug.Log("[NPCDebugger] CurrencyManager is null, cannot add gold");
            }
            
            // Remove item from inventory
            try
            {
                playerInventory.RemoveItem(slot.Item, slot.Quantity);
                Debug.Log($"[NPCDebugger] Successfully sold {slot.Item.ItemName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NPCDebugger] Failed to remove item from inventory: {e.Message}");
            }
        }
        
        private void BuyFromMerchant(MerchantNPC merchant, int itemIndex)
        {
            var merchantData = merchant.Data as MerchantData;
            if (merchantData?.shopInventory?.items == null || itemIndex >= merchantData.shopInventory.items.Count)
            {
                Debug.Log($"[NPCDebugger] Invalid item index {itemIndex + 1}");
                return;
            }
            
            var shopItem = merchantData.shopInventory.items[itemIndex];
            if (shopItem.item == null)
            {
                Debug.Log($"[NPCDebugger] No item at index {itemIndex + 1}");
                return;
            }
            
            if (merchant.PurchaseItem(gameObject, shopItem.item.ItemId, 1))
            {
                float price = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                price *= merchant.GetPriceMultiplier();
                Debug.Log($"[NPCDebugger] Successfully bought {shopItem.item.ItemName} for {price:F0} gold");
            }
            else
            {
                Debug.Log($"[NPCDebugger] Failed to buy {shopItem.item.ItemName} (insufficient gold or stock)");
            }
        }
        
        private void BuyFromDoctor(DoctorNPC doctor, int itemIndex)
        {
            var doctorData = doctor.Data as DoctorData;
            if (doctorData?.medicineShop?.items == null || itemIndex >= doctorData.medicineShop.items.Count)
            {
                Debug.Log($"[NPCDebugger] Invalid medicine index {itemIndex + 1}");
                return;
            }
            
            var shopItem = doctorData.medicineShop.items[itemIndex];
            if (shopItem.item == null)
            {
                Debug.Log($"[NPCDebugger] No medicine at index {itemIndex + 1}");
                return;
            }
            
            if (doctor.PurchaseItem(gameObject, shopItem.item.ItemId, 1))
            {
                float price = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                price *= doctorData.medicinePriceMultiplier;
                Debug.Log($"[NPCDebugger] Successfully bought {shopItem.item.ItemName} for {price:F0} gold");
            }
            else
            {
                Debug.Log($"[NPCDebugger] Failed to buy {shopItem.item.ItemName} (insufficient gold or stock)");
            }
        }
        
        // 新的菜单处理方法
        private void HandleNormalMode()
        {
            // 按Space或Enter进入主菜单
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                currentMode = InteractionMode.MainMenu;
            }
        }
        
        private void HandleMainMenu()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                // 对话
                StartDialogue();
                currentMode = InteractionMode.None;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                // 购买
                if (CanBuy())
                {
                    currentMode = InteractionMode.BuyMenu;
                    }
                else
                {
                    Debug.Log("[NPCDebugger] This NPC doesn't sell items");
                }
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                // 卖出
                currentMode = InteractionMode.SellMenu;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                // 服务
                if (HasServices())
                {
                    currentMode = InteractionMode.ServiceMenu;
                    }
                else
                {
                    Debug.Log("[NPCDebugger] This NPC doesn't provide services");
                }
            }
            else if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                // 退出
                currentMode = InteractionMode.None;
                Debug.Log("[NPCDebugger] Exited interaction menu");
            }
        }
        
        private void HandleBuyMenu()
        {
            // 数字键购买物品
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    BuyShopItem(i - 1);
                    break;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                // 0键返回主菜单
                currentMode = InteractionMode.MainMenu;
            }
        }
        
        private void HandleSellMenu()
        {
            // 数字键卖出物品
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    SellInventoryItem(i - 1);
                    break;
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                // 0键返回主菜单
                currentMode = InteractionMode.MainMenu;
            }
        }
        
        private void HandleServiceMenu()
        {
            // 根据NPC类型显示不同的服务
            if (currentNPC is RestaurantNPC)
            {
                currentMode = InteractionMode.ServiceSelect;
                currentServiceType = "restaurant";
            }
            else if (currentNPC is DoctorNPC)
            {
                currentMode = InteractionMode.ServiceSelect;
                currentServiceType = "doctor";
            }
            else if (currentNPC is BlacksmithNPC)
            {
                // 铁匠有两种服务
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    currentMode = InteractionMode.ServiceSelect;
                    currentServiceType = "blacksmith_craft";
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    // 直接强化
                    TestBlacksmithUpgrade(currentNPC as BlacksmithNPC);
                    currentMode = InteractionMode.MainMenu;
                    }
                else if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    currentMode = InteractionMode.MainMenu;
                    }
            }
            else if (currentNPC is TailorNPC)
            {
                // 直接执行扩容
                TestTailorService(currentNPC as TailorNPC);
                currentMode = InteractionMode.MainMenu;
            }
        }
        
        // 服务选择处理
        private void HandleServiceSelection()
        {
            // 在服务选择模式下，直接使用数字键
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    bool handled = false;
                    
                    switch (currentServiceType)
                    {
                        case "restaurant":
                            HandleRestaurantSelection(i);
                            handled = true;
                            break;
                        case "doctor":
                            HandleDoctorSelection(i);
                            handled = true;
                            break;
                        case "blacksmith_craft":
                            HandleBlacksmithCraftSelection(i);
                            handled = true;
                            break;
                    }
                    
                    if (handled)
                    {
                        // 执行后返回主菜单
                        currentMode = InteractionMode.MainMenu;
                                }
                    break;
                }
            }
        }
        
        private void HandleRestaurantSelection(int index)
        {
            var restaurant = currentNPC as RestaurantNPC;
            var restaurantData = restaurant?.Data as RestaurantData;
            if (restaurant == null || restaurantData == null) return;
            
            if (index == 0 && restaurantData.provideFreeWater)
            {
                restaurant.ProvideService(gameObject, "free_water");
                Debug.Log("[NPCDebugger] 获得免费水");
            }
            else if (index > 0 && index <= restaurantData.menu.Count)
            {
                var menuItem = restaurantData.menu[index - 1];
                if (restaurant.CanProvideService(gameObject, menuItem.itemName))
                {
                    restaurant.ProvideService(gameObject, menuItem.itemName);
                    Debug.Log($"[NPCDebugger] 点餐: {menuItem.itemName} - 花费 {menuItem.price} 金币");
                }
                else
                {
                    Debug.Log($"[NPCDebugger] 无法购买: {menuItem.itemName} (金币不足)");
                }
            }
        }
        
        private void HandleDoctorSelection(int index)
        {
            var doctor = currentNPC as DoctorNPC;
            var doctorData = doctor?.Data as DoctorData;
            if (doctor == null || doctorData == null) return;
            
            if (index > 0 && index <= doctorData.services.Count)
            {
                var service = doctorData.services[index - 1];
                if (doctor.CanProvideService(gameObject, service.serviceName))
                {
                    doctor.ProvideService(gameObject, service.serviceName);
                    int cost = doctor.GetServiceCost(service.serviceName);
                    Debug.Log($"[NPCDebugger] 使用医疗服务: {service.serviceName} - 花费 {cost} 金币");
                }
                else
                {
                    Debug.Log($"[NPCDebugger] 无法使用服务: {service.serviceName} (金币不足或条件不满足)");
                }
            }
        }
        
        private void HandleBlacksmithCraftSelection(int index)
        {
            var blacksmith = currentNPC as BlacksmithNPC;
            var blacksmithData = blacksmith?.Data as BlacksmithData;
            if (blacksmith == null || blacksmithData == null) return;
            
            if (index > 0 && index <= blacksmithData.recipes.Count)
            {
                var recipe = blacksmithData.recipes[index - 1];
                if (blacksmith.CanCraft(gameObject, recipe.recipeName))
                {
                    blacksmith.CraftItem(gameObject, recipe.recipeName);
                    Debug.Log($"[NPCDebugger] 开始打造: {recipe.recipeName} - 花费 {recipe.craftingFee} 金币");
                }
                else
                {
                    Debug.Log($"[NPCDebugger] 无法打造: {recipe.recipeName} (材料不足)");
                    foreach (var mat in recipe.requiredMaterials)
                    {
                        int has = playerInventory?.GetItemCount(mat.material) ?? 0;
                        if (has < mat.amount)
                        {
                            Debug.Log($"  缺少: {mat.material.ItemName} ({has}/{mat.amount})");
                        }
                    }
                }
            }
        }
        
        // 菜单显示方法
        private void ShowMainMenu()
        {
            if (currentNPC?.Data == null) return;
            
            Debug.Log($"\n=== {currentNPC.Data.npcName} ===");
            Debug.Log("[1] Talk");
            
            if (CanBuy())
                Debug.Log("[2] Buy");
                
            Debug.Log("[3] Sell");
            
            if (HasServices())
                Debug.Log("[4] Services");
                
            Debug.Log("[0] Exit");
            Debug.Log("Press number to select:");
        }
        
        private void ShowBuyMenu()
        {
            Debug.Log("\n=== BUY MENU ===");
            
            if (currentNPC is MerchantNPC merchant)
            {
                var merchantData = merchant.Data as MerchantData;
                if (merchantData?.shopInventory != null)
                {
                    for (int i = 0; i < merchantData.shopInventory.items.Count && i < 9; i++)
                    {
                        var item = merchantData.shopInventory.items[i];
                        if (item.item != null)
                        {
                            float price = item.priceOverride > 0 ? item.priceOverride : item.item.BuyPrice;
                            price *= merchant.GetPriceMultiplier();
                            Debug.Log($"[{i+1}] {item.item.ItemName} - ${price:F0}");
                        }
                    }
                }
            }
            else if (currentNPC is DoctorNPC doctor)
            {
                var doctorData = doctor.Data as DoctorData;
                if (doctorData?.medicineShop != null)
                {
                    int idx = 0;
                    foreach (var item in doctorData.medicineShop.items)
                    {
                        if (item.item != null && idx < 9)
                        {
                            float price = item.priceOverride > 0 ? item.priceOverride : item.item.BuyPrice;
                            price *= doctorData.medicinePriceMultiplier;
                            Debug.Log($"[{idx+1}] {item.item.ItemName} - ${price:F0}");
                            idx++;
                        }
                    }
                }
            }
            
            Debug.Log("[0] Back");
        }
        
        private void ShowSellMenu()
        {
            Debug.Log("\n=== SELL MENU ===");
            
            if (playerInventory != null)
            {
                for (int i = 0; i < playerInventory.Size && i < 9; i++)
                {
                    var slot = playerInventory.GetSlot(i);
                    if (!slot.IsEmpty)
                    {
                        var sellPrice = slot.Item.SellPrice * slot.Quantity;
                        Debug.Log($"[{i+1}] {slot.Item.ItemName} x{slot.Quantity} - ${sellPrice}");
                    }
                    else
                    {
                        Debug.Log($"[{i+1}] Empty");
                    }
                }
            }
            
            Debug.Log("[0] Back");
        }
        
        private void ShowServiceMenu()
        {
            Debug.Log("\n=== SERVICES ===");
            
            if (currentNPC is BlacksmithNPC)
            {
                Debug.Log("[1] Weapon Crafting");
                Debug.Log("[2] Weapon Upgrade");
            }
            else if (currentNPC is TailorNPC)
            {
                Debug.Log("[1] Bag Expansion");
            }
            
            Debug.Log("[0] Back");
        }
        
        private void ShowRestaurantServices()
        {
            var restaurant = currentNPC as RestaurantNPC;
            var restaurantData = restaurant?.Data as RestaurantData;
            if (restaurantData == null) return;
            
            Debug.Log("\n=== RESTAURANT MENU ===");
            
            if (restaurantData.provideFreeWater)
            {
                Debug.Log("[0] Free Water");
            }
            
            for (int i = 0; i < restaurantData.menu.Count && i < 9; i++)
            {
                var item = restaurantData.menu[i];
                Debug.Log($"[{i + 1}] {item.itemName} - ${item.price}");
            }
            
            Debug.Log("Press number to order:");
        }
        
        private void ShowDoctorServices()
        {
            var doctor = currentNPC as DoctorNPC;
            var doctorData = doctor?.Data as DoctorData;
            if (doctorData == null) return;
            
            Debug.Log("\n=== MEDICAL SERVICES ===");
            
            for (int i = 0; i < doctorData.services.Count && i < 9; i++)
            {
                var service = doctorData.services[i];
                int cost = doctor.GetServiceCost(service.serviceName);
                Debug.Log($"[{i + 1}] {service.serviceName} - ${cost}");
            }
            
            Debug.Log("Press number to select:");
        }
        
        private void ShowBlacksmithCrafting()
        {
            var blacksmith = currentNPC as BlacksmithNPC;
            var blacksmithData = blacksmith?.Data as BlacksmithData;
            if (blacksmithData == null) return;
            
            Debug.Log("\n=== WEAPON CRAFTING ===");
            
            for (int i = 0; i < blacksmithData.recipes.Count && i < 9; i++)
            {
                var recipe = blacksmithData.recipes[i];
                Debug.Log($"[{i + 1}] {recipe.recipeName} - ${recipe.craftingFee}");
            }
            
            Debug.Log("Press number to craft:");
        }
        
        // 辅助方法
        private bool CanBuy()
        {
            if (currentNPC is MerchantNPC merchant)
            {
                var merchantData = merchant.Data as MerchantData;
                return merchantData?.shopInventory?.items?.Count > 0;
            }
            else if (currentNPC is DoctorNPC doctor)
            {
                var doctorData = doctor.Data as DoctorData;
                return doctorData?.medicineShop?.items?.Count > 0;
            }
            return false;
        }
        
        private bool HasServices()
        {
            return currentNPC is RestaurantNPC || currentNPC is DoctorNPC || 
                   currentNPC is BlacksmithNPC || currentNPC is TailorNPC;
        }
        
        private void StartDialogue()
        {
            if (currentNPC?.Data == null) return;
            
            Debug.Log($"[NPCDebugger] Starting dialogue with {currentNPC.Data.npcName}");
            Debug.Log($"Dialogue: {currentNPC.Data.greetingText}");
            
            // 模拟开始对话
            currentNPC.StartInteraction(gameObject);
        }
        
        // 显示内容获取方法
        private string GetMainMenuDisplay()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== MAIN MENU ===");
            sb.AppendLine("[1] Talk");
            
            if (CanBuy())
                sb.AppendLine("[2] Buy");
                
            sb.AppendLine("[3] Sell");
            
            if (HasServices())
                sb.AppendLine("[4] Services");
                
            sb.AppendLine("[0] Exit");
            return sb.ToString();
        }
        
        private string GetBuyMenuDisplay()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== BUY MENU ===");
            
            if (currentNPC is MerchantNPC merchant)
            {
                var merchantData = merchant.Data as MerchantData;
                if (merchantData?.shopInventory != null)
                {
                    for (int i = 0; i < merchantData.shopInventory.items.Count && i < 9; i++)
                    {
                        var item = merchantData.shopInventory.items[i];
                        if (item.item != null)
                        {
                            float price = item.priceOverride > 0 ? item.priceOverride : item.item.BuyPrice;
                            price *= merchant.GetPriceMultiplier();
                            sb.AppendLine($"[{i+1}] {item.item.ItemName}");
                            sb.AppendLine($"  ${price:F0}");
                        }
                    }
                }
            }
            else if (currentNPC is DoctorNPC doctor)
            {
                var doctorData = doctor.Data as DoctorData;
                if (doctorData?.medicineShop != null)
                {
                    int idx = 0;
                    foreach (var item in doctorData.medicineShop.items)
                    {
                        if (item.item != null && idx < 9)
                        {
                            float price = item.priceOverride > 0 ? item.priceOverride : item.item.BuyPrice;
                            price *= doctorData.medicinePriceMultiplier;
                            sb.AppendLine($"[{idx+1}] {item.item.ItemName}");
                            sb.AppendLine($"  ${price:F0}");
                            idx++;
                        }
                    }
                }
            }
            
            sb.AppendLine("[0] Back");
            return sb.ToString();
        }
        
        private string GetSellMenuDisplay()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== SELL MENU ===");
            
            if (playerInventory != null)
            {
                for (int i = 0; i < playerInventory.Size && i < 9; i++)
                {
                    var slot = playerInventory.GetSlot(i);
                    if (!slot.IsEmpty)
                    {
                        var sellPrice = slot.Item.SellPrice * slot.Quantity;
                        sb.AppendLine($"[{i+1}] {slot.Item.ItemName} x{slot.Quantity}");
                        sb.AppendLine($"  Sell for ${sellPrice}");
                    }
                }
            }
            
            sb.AppendLine("[0] Back");
            return sb.ToString();
        }
        
        private string GetServiceMenuDisplay()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== SERVICES ===");
            
            if (currentNPC is BlacksmithNPC)
            {
                sb.AppendLine("[1] Weapon Crafting");
                sb.AppendLine("[2] Weapon Upgrade");
            }
            else if (currentNPC is TailorNPC)
            {
                sb.AppendLine("[1] Bag Expansion");
            }
            else if (currentNPC is RestaurantNPC || currentNPC is DoctorNPC)
            {
                sb.AppendLine("Service details will show next");
            }
            
            sb.AppendLine("[0] Back");
            return sb.ToString();
        }
        
        private string GetServiceSelectDisplay()
        {
            StringBuilder sb = new StringBuilder();
            
            if (currentServiceType == "restaurant")
            {
                return GetRestaurantInfo(currentNPC as RestaurantNPC);
            }
            else if (currentServiceType == "doctor")
            {
                return GetDoctorInfo(currentNPC as DoctorNPC);
            }
            else if (currentServiceType == "blacksmith_craft")
            {
                var blacksmithData = (currentNPC as BlacksmithNPC)?.Data as BlacksmithData;
                if (blacksmithData != null)
                {
                    sb.AppendLine("=== WEAPON CRAFTING ===");
                    for (int i = 0; i < blacksmithData.recipes.Count && i < 9; i++)
                    {
                        var recipe = blacksmithData.recipes[i];
                        sb.AppendLine($"[{i + 1}] {recipe.recipeName}");
                        sb.AppendLine($"  ${recipe.craftingFee}");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        private void TestBuyFunction()
        {
            if (currentNPC == null) return;
            
            if (currentNPC is MerchantNPC merchant)
            {
                TestMerchantPurchase(merchant);
            }
            else if (currentNPC is DoctorNPC doctor && (doctor.Data as DoctorData)?.medicineShop != null)
            {
                TestDoctorPurchase(doctor);
            }
            else
            {
                Debug.Log("[NPCDebugger] This NPC doesn't support buying items");
            }
        }
        
        private void TestSellFunction()
        {
            Debug.Log("[NPCDebugger] Sell function - Currently showing inventory for selling");
            
            // 显示背包物品供卖出
            if (playerInventory != null)
            {
                for (int i = 0; i < playerInventory.Size; i++)
                {
                    var slot = playerInventory.GetSlot(i);
                    if (!slot.IsEmpty)
                    {
                        var sellPrice = slot.Item.SellPrice * slot.Quantity;
                        Debug.Log($"[{i}] {slot.Item.ItemName} x{slot.Quantity} - Sell for {sellPrice} gold");
                    }
                }
            }
        }
        
        
        private void TestMerchantPurchase(MerchantNPC merchant)
        {
            var merchantData = merchant.Data as MerchantData;
            if (merchantData == null || merchantData.shopInventory.items.Count == 0) return;
            
            // 确保索引有效
            int index = Mathf.Clamp(testItemIndex, 0, merchantData.shopInventory.items.Count - 1);
            var shopItem = merchantData.shopInventory.items[index];
            
            if (shopItem.item != null && (shopItem.stock == -1 || shopItem.stock > 0))
            {
                string itemId = shopItem.item.ItemId;
                
                if (merchant.PurchaseItem(gameObject, itemId, 1))
                {
                    float price = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                    price *= merchant.GetPriceMultiplier();
                    
                    Debug.Log($"[NPCDebugger] 成功购买 {shopItem.item.ItemName}");
                }
                else
                {
                    Debug.Log($"[NPCDebugger] 购买 {shopItem.item.ItemName} 失败");
                }
            }
        }
        
        private void TestDoctorService(DoctorNPC doctor)
        {
            var doctorData = doctor.Data as DoctorData;
            if (doctorData == null || doctorData.services.Count == 0) return;
            
            // Alt+2后，使用数字键选择具体服务
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    int serviceIndex = i - 1;
                    if (serviceIndex < doctorData.services.Count)
                    {
                        var service = doctorData.services[serviceIndex];
                        if (doctor.CanProvideService(gameObject, service.serviceName))
                        {
                            doctor.ProvideService(gameObject, service.serviceName);
                            int cost = doctor.GetServiceCost(service.serviceName);
                            Debug.Log($"[NPCDebugger] 使用医疗服务: {service.serviceName} - 花费 {cost} 金币");
                        }
                        else
                        {
                            Debug.Log($"[NPCDebugger] 无法使用服务: {service.serviceName} (金币不足或条件不满足)");
                        }
                        return;
                    }
                }
            }
            
            // 如果没有按数字键，显示提示
            Debug.Log("[NPCDebugger] 医疗服务 - 按数字键选择:");
            for (int i = 0; i < doctorData.services.Count && i < 9; i++)
            {
                var service = doctorData.services[i];
                int cost = doctor.GetServiceCost(service.serviceName);
                Debug.Log($"  [{i + 1}] {service.serviceName} - {cost}金币");
            }
        }
        
        private void TestBlacksmithCrafting(BlacksmithNPC blacksmith)
        {
            var blacksmithData = blacksmith.Data as BlacksmithData;
            if (blacksmithData == null || blacksmithData.recipes.Count == 0) return;
            
            // Alt+2后，使用数字键选择具体配方
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    int recipeIndex = i - 1;
                    if (recipeIndex < blacksmithData.recipes.Count)
                    {
                        var recipe = blacksmithData.recipes[recipeIndex];
                        if (blacksmith.CanCraft(gameObject, recipe.recipeName))
                        {
                            blacksmith.CraftItem(gameObject, recipe.recipeName);
                            Debug.Log($"[NPCDebugger] 开始打造: {recipe.recipeName} - 花费 {recipe.craftingFee} 金币");
                        }
                        else
                        {
                            Debug.Log($"[NPCDebugger] 无法打造: {recipe.recipeName} (材料不足)");
                            // 显示缺少的材料
                            foreach (var mat in recipe.requiredMaterials)
                            {
                                int has = playerInventory?.GetItemCount(mat.material) ?? 0;
                                if (has < mat.amount)
                                {
                                    Debug.Log($"  缺少: {mat.material.ItemName} ({has}/{mat.amount})");
                                }
                            }
                        }
                        return;
                    }
                }
            }
            
            // 如果没有按数字键，显示提示
            Debug.Log("[NPCDebugger] 铁匠打造 - 按数字键选择配方:");
            for (int i = 0; i < blacksmithData.recipes.Count && i < 9; i++)
            {
                var recipe = blacksmithData.recipes[i];
                Debug.Log($"  [{i + 1}] {recipe.recipeName} - {recipe.craftingFee}金币");
            }
        }
        
        private void TestBlacksmithUpgrade(BlacksmithNPC blacksmith)
        {
            // 获取背包中的第一把武器
            var weapon = GetFirstWeaponInInventory();
            if (weapon != null)
            {
                if (blacksmith.UpgradeItem(gameObject, weapon.ItemId))
                {
                    Debug.Log($"[NPCDebugger] Successfully upgraded weapon: {weapon.ItemName}");
                }
                else
                {
                    Debug.Log($"[NPCDebugger] Failed to upgrade weapon: {weapon.ItemName}");
                }
            }
            else
            {
                Debug.Log("[NPCDebugger] No weapons in inventory to upgrade");
            }
        }
        
        private void TestDoctorPurchase(DoctorNPC doctor)
        {
            var doctorData = doctor.Data as DoctorData;
            if (doctorData?.medicineShop?.items == null || doctorData.medicineShop.items.Count == 0) return;
            
            int index = Mathf.Clamp(testItemIndex, 0, doctorData.medicineShop.items.Count - 1);
            var shopItem = doctorData.medicineShop.items[index];
            
            if (shopItem.item != null && doctor.PurchaseItem(gameObject, shopItem.item.ItemId, 1))
            {
                Debug.Log($"[NPCDebugger] Purchased medicine: {shopItem.item.ItemName}");
            }
            else
            {
                Debug.Log($"[NPCDebugger] Failed to purchase medicine");
            }
        }
        
        private void TestTailorService(TailorNPC tailor)
        {
            var tailorData = tailor.Data as TailorData;
            if (tailorData == null || tailorData.bagUpgrades.Count == 0) return;
            
            // 找到当前可用的升级
            var upgrade = tailorData.bagUpgrades.Find(u => tailor.CanProvideService(gameObject, u.upgradeName));
            if (upgrade != null)
            {
                tailor.ProvideService(gameObject, upgrade.upgradeName);
                int cost = tailor.GetServiceCost(upgrade.upgradeName);
                Debug.Log($"[NPCDebugger] 开始背包升级: {upgrade.upgradeName}");
            }
            else
            {
                Debug.Log("[NPCDebugger] 没有可用的背包升级");
            }
        }
        
        private void TestRestaurantService(RestaurantNPC restaurant)
        {
            var restaurantData = restaurant.Data as RestaurantData;
            if (restaurantData == null) return;
            
            // Alt+2后，使用数字键选择具体服务
            // 0 = 免费水（如果有）
            // 1-9 = 菜单项
            
            // 如果按了数字键，执行对应服务
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    if (i == 0 && restaurantData.provideFreeWater)
                    {
                        // 免费水
                        restaurant.ProvideService(gameObject, "free_water");
                        Debug.Log("[NPCDebugger] 获得免费水");
                        return;
                    }
                    else if (i > 0 && i <= restaurantData.menu.Count)
                    {
                        // 菜单项（i-1因为菜单从0开始）
                        var menuItem = restaurantData.menu[i - 1];
                        if (restaurant.CanProvideService(gameObject, menuItem.itemName))
                        {
                            restaurant.ProvideService(gameObject, menuItem.itemName);
                            Debug.Log($"[NPCDebugger] 点餐: {menuItem.itemName} - 花费 {menuItem.price} 金币");
                        }
                        else
                        {
                            Debug.Log($"[NPCDebugger] 无法购买: {menuItem.itemName} (金币不足)");
                        }
                        return;
                    }
                }
            }
            
            // 如果没有按数字键，显示提示
            Debug.Log("[NPCDebugger] 餐厅服务 - 按数字键选择:");
            if (restaurantData.provideFreeWater)
            {
                Debug.Log("  [0] 免费水");
            }
            for (int i = 0; i < restaurantData.menu.Count && i < 9; i++)
            {
                var item = restaurantData.menu[i];
                Debug.Log($"  [{i + 1}] {item.itemName} - {item.price}金币");
            }
        }
        
        private WeaponItem GetFirstWeaponInInventory()
        {
            if (playerInventory == null) return null;
            
            for (int i = 0; i < playerInventory.Size; i++)
            {
                var slot = playerInventory.GetSlot(i);
                if (!slot.IsEmpty && slot.Item is WeaponItem weapon)
                {
                    return weapon;
                }
            }
            return null;
        }
        
        private void OnGUI()
        {
            if (!showDebugPanel && !alwaysShow) return;
            
            // 初始化样式（只在OnGUI中调用GUI相关方法）
            if (panelStyle == null)
            {
                InitializeStyles();
            }
            
            // 面板位置（右侧）
            float panelHeight = Screen.height - 40;
            Rect panelRect = new Rect(Screen.width - panelWidth - 20, 20, panelWidth, panelHeight);
            
            GUI.Box(panelRect, "", panelStyle);
            
            GUILayout.BeginArea(panelRect);
            GUILayout.Space(5);
            
            // 标题
            GUILayout.Label("NPC DEBUGGER", headerStyle);
            GUILayout.Space(3);
            
            // 玩家信息
            DrawPlayerInfo();
            GUILayout.Space(3);
            
            // NPC信息
            if (currentNPC != null)
            {
                DrawNPCInfo();
            }
            else
            {
                GUILayout.Label("No NPC in range", labelStyle);
            }
            
            GUILayout.Space(3);
            
            // 功能操作说明
            DrawFunctionGuide();
            
            // 控制提示
            GUILayout.FlexibleSpace();
            DrawControlHints();
            
            GUILayout.EndArea();
        }
        
        private void DrawPlayerInfo()
        {
            GUILayout.Label("=== PLAYER ===", headerStyle);
            
            // 合并单行信息
            string playerStatus = "";
            if (currencyManager != null)
            {
                playerStatus += $"Gold:{currencyManager.CurrentGold} ";
            }
            if (playerInventory != null)
            {
                int usedSlots = 0;
                for (int i = 0; i < playerInventory.Size; i++)
                {
                    if (!playerInventory.GetSlot(i).IsEmpty)
                        usedSlots++;
                }
                playerStatus += $"Bag:{usedSlots}/{playerInventory.Size} ";
            }
            if (ammoManager != null)
            {
                playerStatus += $"B:{ammoManager.GetAmmo(AmmoType.Bullets)} A:{ammoManager.GetAmmo(AmmoType.Arrows)} M:{ammoManager.GetAmmo(AmmoType.Mana)}";
            }
            GUILayout.Label(playerStatus, labelStyle);
            
            if (playerInventory != null)
            {
                GUILayout.Label("=== INVENTORY (P+Num) ===", headerStyle);
                // 每行显示2个物品，减少行数
                for (int i = 0; i < playerInventory.Size && i < 10; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    
                    // 第一个物品
                    var slot1 = playerInventory.GetSlot(i);
                    int keyNumber1 = (i + 1) % 10;
                    string item1Text = "";
                    if (slot1 != null && !slot1.IsEmpty && slot1.Item != null)
                    {
                        var sellPrice = slot1.Item.SellPrice * slot1.Quantity;
                        item1Text = $"[P+{keyNumber1}]{slot1.Item.ItemName}x{slot1.Quantity}-{sellPrice}g";
                    }
                    else
                    {
                        item1Text = $"[P+{keyNumber1}]Empty";
                    }
                    GUILayout.Label(item1Text, labelStyle, GUILayout.Width(panelWidth/2 - 20));
                    
                    // 第二个物品（如果存在）
                    if (i + 1 < playerInventory.Size && i + 1 < 10)
                    {
                        var slot2 = playerInventory.GetSlot(i + 1);
                        int keyNumber2 = (i + 2) % 10;
                        string item2Text = "";
                        if (slot2 != null && !slot2.IsEmpty && slot2.Item != null)
                        {
                            var sellPrice = slot2.Item.SellPrice * slot2.Quantity;
                            item2Text = $"[P+{keyNumber2}]{slot2.Item.ItemName}x{slot2.Quantity}-{sellPrice}g";
                        }
                        else
                        {
                            item2Text = $"[P+{keyNumber2}]Empty";
                        }
                        GUILayout.Label(item2Text, labelStyle);
                    }
                    
                    GUILayout.EndHorizontal();
                }
            }
        }
        
        private void DrawNPCInfo()
        {
            GUILayout.Label("=== NPC INFO ===", headerStyle);
            GUILayout.Label(cachedNPCInfo, labelStyle);
            
            if (!string.IsNullOrEmpty(cachedShopInfo))
            {
                // 使用滚动视图显示详细信息
                GUILayout.Label(cachedShopInfo, labelStyle);
            }
        }
        
        private void DrawFunctionGuide()
        {
            GUILayout.Label("=== CONTROLS ===", headerStyle);
            
            if (currentNPC == null) return;
            
            switch (currentMode)
            {
                case InteractionMode.None:
                    GUILayout.Label("SPACE/ENTER: Start interaction", labelStyle);
                    break;
                    
                case InteractionMode.MainMenu:
                    GUILayout.Label("Number keys: Select option", labelStyle);
                    GUILayout.Label("ESC: Exit", labelStyle);
                    break;
                    
                case InteractionMode.BuyMenu:
                case InteractionMode.SellMenu:
                case InteractionMode.ServiceSelect:
                    GUILayout.Label("Number keys: Select item", labelStyle);
                    GUILayout.Label("0: Back | ESC: Exit", labelStyle);
                    break;
                    
                case InteractionMode.ServiceMenu:
                    GUILayout.Label("Number keys: Select service", labelStyle);
                    GUILayout.Label("0: Back | ESC: Exit", labelStyle);
                    break;
                    
                default:
                    GUILayout.Label("ESC: Exit menu", labelStyle);
                    break;
            }
        }
        
        private void DrawControlHints()
        {
            GUILayout.Label("=== INFO ===", headerStyle);
            GUILayout.Label("F4: Toggle panel", labelStyle);
            GUILayout.Label("L: Toggle details", labelStyle);
        }
        
        private string GetNPCOverviewInfo()
        {
            if (currentNPC == null) return "Press SPACE or ENTER to interact";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Press SPACE or ENTER to interact");
            sb.AppendLine();
            
            Debug.Log($"[NPCDebugger] GetNPCOverviewInfo - currentNPC type: {currentNPC.GetType().Name}");
            if (currentNPC.Data != null)
            {
                Debug.Log($"[NPCDebugger] NPC Data type: {currentNPC.Data.npcType}");
            }
            
            // 显示NPC具体信息 - 根据NPC数据类型而不是组件类型判断
            if (currentNPC.Data != null && currentNPC.Data.npcType == NPCType.Merchant)
            {
                Debug.Log($"[NPCDebugger] Processing as Merchant based on Data.npcType");
                var merchant = currentNPC as MerchantNPC;
                if (merchant != null)
                {
                    Debug.Log($"[NPCDebugger] Found MerchantNPC, checking data...");
                    var merchantData = merchant.Data as MerchantData;
                    Debug.Log($"[NPCDebugger] MerchantData: {merchantData != null}, Shop: {merchantData?.shopInventory != null}, Items: {merchantData?.shopInventory?.items?.Count ?? 0}");
                    
                    if (merchantData?.shopInventory?.items != null && merchantData.shopInventory.items.Count > 0)
                    {
                        sb.AppendLine("=== SHOP INVENTORY ===");
                        float priceMultiplier = merchant.GetPriceMultiplier();
                        sb.AppendLine($"Price Multiplier: {priceMultiplier:F2}x");
                    
                    for (int i = 0; i < merchantData.shopInventory.items.Count && i < 5; i++)
                    {
                        var shopItem = merchantData.shopInventory.items[i];
                        if (shopItem.item != null)
                        {
                            float basePrice = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                            float finalPrice = basePrice * priceMultiplier;
                            string stockText = shopItem.stock == -1 ? "∞" : shopItem.stock.ToString();
                            sb.AppendLine($"• {shopItem.item.ItemName} - ${finalPrice:F0} (Stock: {stockText})");
                        }
                    }
                    if (merchantData.shopInventory.items.Count > 5)
                    {
                        sb.AppendLine($"... and {merchantData.shopInventory.items.Count - 5} more items");
                    }
                    }
                    else
                    {
                        sb.AppendLine("=== SHOP ===");
                        sb.AppendLine("No items in stock");
                    }
                }
            }
            else if (currentNPC.Data != null && currentNPC.Data.npcType == NPCType.Doctor)
            {
                Debug.Log($"[NPCDebugger] Processing as Doctor based on Data.npcType");
                var doctor = currentNPC as DoctorNPC;
                if (doctor != null)
                {
                    Debug.Log($"[NPCDebugger] Found DoctorNPC, checking data...");
                    var doctorData = doctor.Data as DoctorData;
                Debug.Log($"[NPCDebugger] DoctorData: {doctorData != null}, Services: {doctorData?.services?.Count ?? 0}, MedicineShop: {doctorData?.medicineShop?.items?.Count ?? 0}");
                
                if (doctorData != null)
                {
                    // 显示医疗服务
                    if (doctorData.services != null && doctorData.services.Count > 0)
                    {
                        sb.AppendLine("=== MEDICAL SERVICES ===");
                        for (int i = 0; i < doctorData.services.Count && i < 3; i++)
                        {
                            var service = doctorData.services[i];
                            int cost = doctor.GetServiceCost(service.serviceName);
                            sb.AppendLine($"• {service.serviceName} - ${cost}");
                        }
                        if (doctorData.services.Count > 3)
                        {
                            sb.AppendLine($"... and {doctorData.services.Count - 3} more services");
                        }
                    }
                    
                    // 显示药品商店
                    if (doctorData.medicineShop?.items != null && doctorData.medicineShop.items.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("=== MEDICINE SHOP ===");
                        for (int i = 0; i < doctorData.medicineShop.items.Count && i < 3; i++)
                        {
                            var shopItem = doctorData.medicineShop.items[i];
                            if (shopItem.item != null)
                            {
                                float price = shopItem.priceOverride > 0 ? shopItem.priceOverride : shopItem.item.BuyPrice;
                                price *= doctorData.medicinePriceMultiplier;
                                sb.AppendLine($"• {shopItem.item.ItemName} - ${price:F0}");
                            }
                        }
                        if (doctorData.medicineShop.items.Count > 3)
                        {
                            sb.AppendLine($"... and {doctorData.medicineShop.items.Count - 3} more medicines");
                        }
                    }
                    else
                    {
                        sb.AppendLine();
                        sb.AppendLine("=== MEDICINE SHOP ===");
                        sb.AppendLine("No medicines in stock");
                    }
                }
                }
            }
            else if (currentNPC.Data != null && currentNPC.Data.npcType == NPCType.Restaurant)
            {
                Debug.Log($"[NPCDebugger] Processing as Restaurant based on Data.npcType");
                var restaurant = currentNPC as RestaurantNPC;
                if (restaurant != null)
                {
                    var restaurantData = restaurant.Data as RestaurantData;
                    if (restaurantData != null)
                    {
                    sb.AppendLine("=== RESTAURANT MENU ===");
                    if (restaurantData.provideFreeWater)
                    {
                        sb.AppendLine("• Free Water (Free)");
                    }
                    
                    if (restaurantData.menu != null && restaurantData.menu.Count > 0)
                    {
                        for (int i = 0; i < restaurantData.menu.Count && i < 4; i++)
                        {
                            var item = restaurantData.menu[i];
                            sb.AppendLine($"• {item.itemName} - ${item.price}");
                        }
                        if (restaurantData.menu.Count > 4)
                        {
                            sb.AppendLine($"... and {restaurantData.menu.Count - 4} more dishes");
                        }
                    }
                    }
                }
            }
            else if (currentNPC.Data != null && currentNPC.Data.npcType == NPCType.Blacksmith)
            {
                Debug.Log($"[NPCDebugger] Processing as Blacksmith based on Data.npcType");
                var blacksmith = currentNPC as BlacksmithNPC;
                if (blacksmith != null)
                {
                    var blacksmithData = blacksmith.Data as BlacksmithData;
                    if (blacksmithData != null)
                    {
                    sb.AppendLine("=== BLACKSMITH SERVICES ===");
                    sb.AppendLine("• Weapon Crafting");
                    sb.AppendLine("• Weapon Upgrade");
                    
                    if (blacksmithData.recipes != null && blacksmithData.recipes.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Available Recipes:");
                        for (int i = 0; i < blacksmithData.recipes.Count && i < 3; i++)
                        {
                            var recipe = blacksmithData.recipes[i];
                            sb.AppendLine($"• {recipe.recipeName} - ${recipe.craftingFee}");
                        }
                        if (blacksmithData.recipes.Count > 3)
                        {
                            sb.AppendLine($"... and {blacksmithData.recipes.Count - 3} more recipes");
                        }
                    }
                    }
                }
            }
            else if (currentNPC.Data != null && currentNPC.Data.npcType == NPCType.Tailor)
            {
                Debug.Log($"[NPCDebugger] Processing as Tailor based on Data.npcType");
                var tailor = currentNPC as TailorNPC;
                if (tailor != null)
                {
                    var tailorData = tailor.Data as TailorData;
                    if (tailorData != null)
                    {
                    sb.AppendLine("=== TAILOR SERVICES ===");
                    sb.AppendLine("• Bag Expansion");
                    
                    if (tailorData.bagUpgrades != null && tailorData.bagUpgrades.Count > 0)
                    {
                        int currentSlots = playerInventory != null ? playerInventory.GetMaxSlots() : 10;
                        var availableUpgrades = tailorData.bagUpgrades.FindAll(u => 
                            currentSlots >= u.requiredCurrentSlots && currentSlots < u.maxSlots);
                        
                        if (availableUpgrades.Count > 0)
                        {
                            sb.AppendLine("Available Upgrades:");
                            foreach (var upgrade in availableUpgrades.Take(2))
                            {
                                int cost = (int)(upgrade.upgradeFee * tailorData.upgradePriceMultiplier);
                                sb.AppendLine($"• {upgrade.upgradeName} - ${cost} (+{upgrade.slotsToAdd} slots)");
                            }
                        }
                        else
                        {
                            sb.AppendLine("Max capacity reached or no upgrades available");
                        }
                    }
                    }
                }
            }
            else
            {
                // 未识别的NPC类型
                Debug.Log($"[NPCDebugger] Unrecognized NPC type: {currentNPC.GetType().Name}");
                sb.AppendLine($"=== UNKNOWN NPC TYPE ===");
                sb.AppendLine($"Type: {currentNPC.GetType().Name}");
                if (currentNPC.Data != null)
                {
                    sb.AppendLine($"Data: {currentNPC.Data.npcName} ({currentNPC.Data.npcType})");
                }
                else
                {
                    sb.AppendLine("No NPC data found!");
                }
            }
            
            return sb.ToString();
        }
        
    }
}