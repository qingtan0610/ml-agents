using UnityEngine;
using System.Collections.Generic;
using System.Text;
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
            
            // 测试快捷键
            if (showDebugPanel && currentNPC != null)
            {
                HandleTestInputs();
            }
        }
        
        private void HandleTestInputs()
        {
            if (currentNPC == null) return;
            
            bool isPPressed = Input.GetKey(KeyCode.P);
            bool isBPressed = Input.GetKey(KeyCode.B);
            bool isAltPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            
            // P+数字键：卖出背包物品 (避免与移动键S冲突)
            if (isPPressed)
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        SellInventoryItem(i - 1);
                        break;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Alpha0)) // P+0 for slot 10
                {
                    SellInventoryItem(9);
                }
            }
            // B+数字键：购买商品
            else if (isBPressed)
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        BuyShopItem(i - 1);
                        break;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Alpha0)) // B+0 for item 10
                {
                    BuyShopItem(9);
                }
            }
            // Alt+数字键执行功能
            else if (isAltPressed)
            {
                // Alt+1: 对话
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    StartDialogue();
                }
                // Alt+2: 专属功能1 (铁匠打造、医生治疗、餐厅用餐、裁缝扩容等)
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    TestSpecialFunction1();
                }
                // Alt+3: 专属功能2 (铁匠强化等)
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    TestSpecialFunction2();
                }
            }
            // 普通数字键切换项目索引
            else
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        testItemIndex = i - 1;
                        UpdateCachedInfo();
                        Debug.Log($"[NPCDebugger] Selected item index: {testItemIndex}");
                        break;
                    }
                }
                if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    testItemIndex = 9;
                    UpdateCachedInfo();
                    Debug.Log($"[NPCDebugger] Selected item index: {testItemIndex}");
                }
            }
            
            // L - 切换详细信息显示
            if (Input.GetKeyDown(KeyCode.L))
            {
                showDetailedInfo = !showDetailedInfo;
                UpdateCachedInfo();
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
            if (npcData == null) return;
            
            // 构建NPC基础信息
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Name: {npcData.npcName}");
            sb.AppendLine($"Type: {npcData.npcType}");
            sb.AppendLine($"Mood: {npcData.defaultMood}");
            sb.AppendLine($"Range: {npcData.interactionRange:F1}m");
            sb.AppendLine($"Interaction Time: {(Time.time - lastInteractionTime):F1}s");
            sb.AppendLine();
            sb.AppendLine("=== DIALOGUE ===");
            sb.AppendLine($"Greeting: \"{npcData.greetingText}\"");
            sb.AppendLine($"Farewell: \"{npcData.farewellText}\"");
            cachedNPCInfo = sb.ToString();
            
            // 构建专门信息
            if (currentNPC is MerchantNPC merchant)
            {
                UpdateMerchantInfo(merchant);
            }
            else
            {
                cachedShopInfo = GetNPCSpecificInfo(currentNPC);
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
            if (npc is RestaurantNPC restaurant)
            {
                return GetRestaurantInfo(restaurant);
            }
            else if (npc is DoctorNPC doctor)
            {
                return GetDoctorInfo(doctor);
            }
            else if (npc is BlacksmithNPC blacksmith)
            {
                return GetBlacksmithInfo(blacksmith);
            }
            else if (npc is TailorNPC tailor)
            {
                return GetTailorInfo(tailor);
            }
            
            return "未知NPC类型";
        }
        
        private string GetRestaurantInfo(RestaurantNPC restaurant)
        {
            var restaurantData = restaurant.Data as RestaurantData;
            if (restaurantData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== 餐厅菜单 ===");
            
            // 免费水
            if (restaurantData.provideFreeWater)
            {
                string currentText = (testServiceIndex == 0) ? " ←" : "";
                sb.AppendLine($"[免费] 清水{currentText}");
                sb.AppendLine($"  恢复口渴: {restaurantData.waterRestoreAmount}");
                sb.AppendLine();
            }
            
            // 菜单项目
            for (int i = 0; i < restaurantData.menu.Count; i++)
            {
                var item = restaurantData.menu[i];
                string special = item.isSpecialDish ? " [特色]" : "";
                string currentText = (i == testItemIndex && testServiceIndex == 1) ? " ←" : "";
                
                sb.AppendLine($"[{i}] {item.itemName}{special}{currentText}");
                sb.AppendLine($"  价格: {item.price}金币");
                
                if (showDetailedInfo)
                {
                    List<string> effects = new List<string>();
                    if (item.hungerRestore > 0) effects.Add($"饥饿+{item.hungerRestore}");
                    if (item.thirstRestore > 0) effects.Add($"口渴+{item.thirstRestore}");
                    if (item.healthRestore > 0) effects.Add($"生命+{item.healthRestore}");
                    if (item.staminaRestore > 0) effects.Add($"体力+{item.staminaRestore}");
                    if (item.hasBuffEffect && item.buffData != null)
                        effects.Add($"{item.buffData.buffName}({item.buffData.duration}秒)");
                    
                    if (effects.Count > 0)
                        sb.AppendLine($"  效果: {string.Join(", ", effects)}");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        private string GetDoctorInfo(DoctorNPC doctor)
        {
            var doctorData = doctor.Data as DoctorData;
            if (doctorData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== 医疗服务 ===");
            
            // 显示治疗服务
            for (int i = 0; i < doctorData.services.Count; i++)
            {
                var service = doctorData.services[i];
                int cost = doctor.GetServiceCost(service.serviceName);
                bool canAfford = doctor.CanProvideService(gameObject, service.serviceName);
                string status = canAfford ? "" : " [不可用]";
                string currentText = (i == testServiceIndex) ? " ←" : "";
                
                sb.AppendLine($"[{i}] {service.serviceName}{currentText}");
                sb.AppendLine($"  费用: {cost}金币{status}");
                
                if (showDetailedInfo)
                {
                    sb.AppendLine($"  {service.description}");
                    if (service.fullHeal) 
                        sb.AppendLine("  完全治疗");
                    else if (service.healthRestore > 0) 
                        sb.AppendLine($"  恢复生命: {service.healthRestore}");
                    if (service.providesImmunity)
                        sb.AppendLine($"  免疫: {service.immunityType} ({service.immunityDuration}秒)");
                }
                sb.AppendLine();
            }
            
            // 显示药品商店
            if (doctorData.medicineShop != null && doctorData.medicineShop.items.Count > 0)
            {
                sb.AppendLine("=== 药品商店 ===");
                sb.AppendLine("(按P键后输入'shop'访问)");
                if (showDetailedInfo)
                {
                    foreach (var item in doctorData.medicineShop.items)
                    {
                        if (item.item == null) continue;
                        float price = item.priceOverride > 0 ? item.priceOverride : item.item.BuyPrice;
                        price *= doctorData.medicinePriceMultiplier;
                        string stock = item.stock == -1 ? "∞" : item.stock.ToString();
                        sb.AppendLine($"• {item.item.ItemName} - {price:F0}金币 (库存:{stock})");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        private string GetBlacksmithInfo(BlacksmithNPC blacksmith)
        {
            var blacksmithData = blacksmith.Data as BlacksmithData;
            if (blacksmithData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            
            if (testServiceIndex == 0)
            {
                // 显示打造配方
                sb.AppendLine("=== 武器打造 ===");
                for (int i = 0; i < blacksmithData.recipes.Count; i++)
                {
                    var recipe = blacksmithData.recipes[i];
                    bool canCraft = blacksmith.CanCraft(gameObject, recipe.recipeName);
                    string status = canCraft ? "" : " [材料不足]";
                    string currentText = (i == testItemIndex) ? " ←" : "";
                    
                    sb.AppendLine($"[{i}] {recipe.recipeName}{currentText}");
                    sb.AppendLine($"  费用: {recipe.craftingFee}金币{status}");
                    
                    if (showDetailedInfo)
                    {
                        sb.AppendLine($"  {recipe.description}");
                        sb.AppendLine("  材料:");
                        foreach (var mat in recipe.requiredMaterials)
                        {
                            int has = playerInventory?.GetItemCount(mat.material) ?? 0;
                            string hasText = has >= mat.amount ? "✓" : "✗";
                            sb.AppendLine($"    {hasText} {mat.material.ItemName} x{mat.amount} (有:{has})");
                        }
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                // 显示强化服务
                sb.AppendLine("=== 武器强化 ===");
                sb.AppendLine($"最高强化等级: +{blacksmithData.maxUpgradeLevel}");
                
                var weapon = GetFirstWeaponInInventory();
                if (weapon != null)
                {
                    sb.AppendLine($"\\n选中武器: {weapon.ItemName}");
                    // 这里可以显示更多武器信息
                }
                else
                {
                    sb.AppendLine("\\n背包中没有武器");
                }
                
                if (showDetailedInfo && blacksmithData.upgradeOptions.Count > 0)
                {
                    sb.AppendLine("\\n强化选项:");
                    foreach (var upgrade in blacksmithData.upgradeOptions)
                    {
                        sb.AppendLine($"• 等级 {upgrade.minUpgradeLevel}-{upgrade.maxUpgradeLevel}");
                        sb.AppendLine($"  成功率: {upgrade.baseSuccessRate * 100:F0}%");
                        sb.AppendLine($"  伤害提升: {upgrade.damageIncrease * 100:F0}%");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        private string GetTailorInfo(TailorNPC tailor)
        {
            var tailorData = tailor.Data as TailorData;
            if (tailorData == null) return "";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== 裁缝服务 ===");
            
            // 获取当前背包容量
            int currentSlots = playerInventory != null ? playerInventory.GetMaxSlots() : 10;
            sb.AppendLine($"当前背包容量: {currentSlots}格\\n");
            
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
                    string status = canAfford ? "" : " [材料/金币不足]";
                    string currentText = (i == testItemIndex) ? " ←" : "";
                    
                    sb.AppendLine($"[{i}] {upgrade.upgradeName}{currentText}");
                    sb.AppendLine($"  升级到: {upgrade.maxSlots}格 (+{upgrade.slotsToAdd})");
                    sb.AppendLine($"  费用: {cost}金币{status}");
                    
                    if (showDetailedInfo && upgrade.requiredMaterials.Count > 0)
                    {
                        sb.AppendLine("  材料:");
                        foreach (var mat in upgrade.requiredMaterials)
                        {
                            int has = playerInventory?.GetItemCount(mat.material) ?? 0;
                            string hasText = has >= mat.amount ? "✓" : "✗";
                            sb.AppendLine($"    {hasText} {mat.material.ItemName} x{mat.amount} (有:{has})");
                        }
                    }
                    sb.AppendLine();
                }
            }
            
            if (!hasAvailableUpgrade)
            {
                sb.AppendLine("[已达到最大容量或没有可用升级]");
            }
            
            // 其他服务
            if (tailorData.canDyeClothes)
                sb.AppendLine("\\n• 染色服务 (暂未开放)");
            if (tailorData.canRepairArmor)
                sb.AppendLine("• 护甲修理 (暂未开放)");
            
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
        
        // 新的功能方法
        private void StartDialogue()
        {
            if (currentNPC?.Data == null) return;
            
            Debug.Log($"[NPCDebugger] Starting dialogue with {currentNPC.Data.npcName}");
            Debug.Log($"Dialogue: {currentNPC.Data.greetingText}");
            
            // 模拟开始对话
            currentNPC.StartInteraction(gameObject);
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
        
        private void TestSpecialFunction1()
        {
            if (currentNPC == null) return;
            
            if (currentNPC is BlacksmithNPC blacksmith)
            {
                TestBlacksmithCrafting(blacksmith);
            }
            else if (currentNPC is DoctorNPC doctor)
            {
                TestDoctorService(doctor);
            }
            else if (currentNPC is RestaurantNPC restaurant)
            {
                TestRestaurantService(restaurant);
            }
            else if (currentNPC is TailorNPC tailor)
            {
                TestTailorService(tailor);
            }
            else
            {
                Debug.Log("[NPCDebugger] This NPC doesn't have special function 1");
            }
        }
        
        private void TestSpecialFunction2()
        {
            if (currentNPC == null) return;
            
            if (currentNPC is BlacksmithNPC blacksmith)
            {
                TestBlacksmithUpgrade(blacksmith);
            }
            else
            {
                Debug.Log("[NPCDebugger] This NPC doesn't have special function 2");
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
            
            int index = Mathf.Clamp(testServiceIndex, 0, doctorData.services.Count - 1);
            var service = doctorData.services[index];
            
            if (doctor.CanProvideService(gameObject, service.serviceName))
            {
                doctor.ProvideService(gameObject, service.serviceName);
                int cost = doctor.GetServiceCost(service.serviceName);
                Debug.Log($"[NPCDebugger] 使用医疗服务: {service.serviceName}");
            }
            else
            {
                Debug.Log($"[NPCDebugger] 无法使用服务: {service.serviceName}");
            }
        }
        
        private void TestBlacksmithCrafting(BlacksmithNPC blacksmith)
        {
            var blacksmithData = blacksmith.Data as BlacksmithData;
            if (blacksmithData == null || blacksmithData.recipes.Count == 0) return;
            
            int index = Mathf.Clamp(testItemIndex, 0, blacksmithData.recipes.Count - 1);
            var recipe = blacksmithData.recipes[index];
            
            if (blacksmith.CanCraft(gameObject, recipe.recipeName))
            {
                blacksmith.CraftItem(gameObject, recipe.recipeName);
                Debug.Log($"[NPCDebugger] Started crafting: {recipe.recipeName}");
                Debug.Log($"Cost: {recipe.craftingFee} gold");
            }
            else
            {
                Debug.Log($"[NPCDebugger] Cannot craft: {recipe.recipeName} (insufficient materials)");
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
            
            // 测试免费水
            if (testServiceIndex == 0 && restaurantData.provideFreeWater)
            {
                restaurant.ProvideService(gameObject, "free_water");
                Debug.Log("[NPCDebugger] 获得免费水");
            }
            // 测试食物
            else if (restaurantData.menu.Count > 0)
            {
                int index = Mathf.Clamp(testItemIndex, 0, restaurantData.menu.Count - 1);
                var menuItem = restaurantData.menu[index];
                
                if (restaurant.CanProvideService(gameObject, menuItem.itemName))
                {
                    restaurant.ProvideService(gameObject, menuItem.itemName);
                    Debug.Log($"[NPCDebugger] 点餐: {menuItem.itemName}");
                }
                else
                {
                    Debug.Log($"[NPCDebugger] 无法购买: {menuItem.itemName} (金币不足)");
                }
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
            GUILayout.Space(10);
            
            // 标题
            GUILayout.Label("NPC INTERACTION DEBUGGER", headerStyle);
            GUILayout.Space(10);
            
            // 玩家信息
            DrawPlayerInfo();
            GUILayout.Space(10);
            
            // NPC信息
            if (currentNPC != null)
            {
                DrawNPCInfo();
            }
            else
            {
                GUILayout.Label("No NPC in interaction range", labelStyle);
            }
            
            GUILayout.Space(10);
            
            // 功能操作说明
            DrawFunctionGuide();
            
            // 控制提示
            GUILayout.FlexibleSpace();
            DrawControlHints();
            
            GUILayout.EndArea();
        }
        
        private void DrawPlayerInfo()
        {
            GUILayout.Label("=== PLAYER STATUS ===", headerStyle);
            
            if (currencyManager != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Gold:", labelStyle, GUILayout.Width(80));
                GUILayout.Label(currencyManager.CurrentGold.ToString(), valueStyle);
                GUILayout.EndHorizontal();
            }
            
            if (playerInventory != null)
            {
                int usedSlots = 0;
                for (int i = 0; i < playerInventory.Size; i++)
                {
                    if (!playerInventory.GetSlot(i).IsEmpty)
                        usedSlots++;
                }
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("Inventory:", labelStyle, GUILayout.Width(80));
                GUILayout.Label($"{usedSlots}/{playerInventory.Size}", valueStyle);
                GUILayout.EndHorizontal();
                
                // Show inventory items for selling
                GUILayout.Space(5);
                GUILayout.Label("=== INVENTORY (P+Number to sell) ===", headerStyle);
                for (int i = 0; i < playerInventory.Size && i < 10; i++)
                {
                    var slot = playerInventory.GetSlot(i);
                    int keyNumber = (i + 1) % 10;
                    
                    if (slot != null && !slot.IsEmpty && slot.Item != null)
                    {
                        var sellPrice = slot.Item.SellPrice * slot.Quantity;
                        GUILayout.Label($"[P+{keyNumber}] {slot.Item.ItemName} x{slot.Quantity} - {sellPrice}g", labelStyle);
                    }
                    else
                    {
                        GUILayout.Label($"[P+{keyNumber}] Empty", labelStyle);
                    }
                }
            }
            
            if (ammoManager != null)
            {
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Ammo:", labelStyle, GUILayout.Width(80));
                GUILayout.Label($"Bullets:{ammoManager.GetAmmo(AmmoType.Bullets)} " +
                               $"Arrows:{ammoManager.GetAmmo(AmmoType.Arrows)} " +
                               $"Mana:{ammoManager.GetAmmo(AmmoType.Mana)}", valueStyle);
                GUILayout.EndHorizontal();
            }
        }
        
        private void DrawNPCInfo()
        {
            GUILayout.Label("=== NPC INFO ===", headerStyle);
            GUILayout.Label(cachedNPCInfo, labelStyle);
            
            GUILayout.Space(10);
            
            if (!string.IsNullOrEmpty(cachedShopInfo))
            {
                // 使用滚动视图显示详细信息
                GUILayout.Label(cachedShopInfo, labelStyle);
            }
        }
        
        private void DrawFunctionGuide()
        {
            GUILayout.Label("=== AVAILABLE FUNCTIONS ===", headerStyle);
            
            if (currentNPC == null) return;
            
            GUILayout.Label("Alt+1 - Start Dialogue", labelStyle);
            
            // 显示每个NPC特有的功能
            if (currentNPC is MerchantNPC)
            {
                GUILayout.Label("B+Number - Buy Shop Item", labelStyle);
                GUILayout.Label("P+Number - Sell Inventory Item", labelStyle);
            }
            else if (currentNPC is DoctorNPC doctor)
            {
                var doctorData = doctor.Data as DoctorData;
                if (doctorData?.medicineShop?.items?.Count > 0)
                {
                    GUILayout.Label("B+Number - Buy Medicine", labelStyle);
                }
                GUILayout.Label("P+Number - Sell Inventory Item", labelStyle);
                GUILayout.Label("Alt+2 - Medical Service", labelStyle);
            }
            else if (currentNPC is BlacksmithNPC)
            {
                GUILayout.Label("P+Number - Sell Inventory Item", labelStyle);
                GUILayout.Label("Alt+2 - Craft Item", labelStyle);
                GUILayout.Label("Alt+3 - Upgrade Weapon", labelStyle);
            }
            else if (currentNPC is RestaurantNPC)
            {
                GUILayout.Label("Alt+2 - Order Food", labelStyle);
            }
            else if (currentNPC is TailorNPC)
            {
                GUILayout.Label("P+Number - Sell Inventory Item", labelStyle);
                GUILayout.Label("Alt+2 - Expand Bag", labelStyle);
            }
            
            GUILayout.Space(5);
            GUILayout.Label($"Selected Service Index: {testItemIndex} (use 1-9 to change)", valueStyle);
        }
        
        private void DrawControlHints()
        {
            GUILayout.Label("=== CONTROLS ===", headerStyle);
            GUILayout.Label("F4 - Toggle Panel", labelStyle);
            GUILayout.Label("L - Toggle Details", labelStyle);
            GUILayout.Label("1-9 - Select Service Index", labelStyle);
            GUILayout.Label("B+Number - Buy Item", labelStyle);
            GUILayout.Label("P+Number - Sell Item", labelStyle);
            GUILayout.Label("Alt+1~3 - Special Functions", labelStyle);
        }
        
    }
}