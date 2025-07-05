using UnityEngine;
using NPC.Core;
using NPC.Data;
using NPC.Interfaces;
using NPC.Runtime;
using NPC.Managers;
using Inventory;
using Inventory.Items;
using Inventory.Managers;
using System.Collections;
using System.Collections.Generic;

namespace NPC.Types
{
    public class BlacksmithNPC : NPCBase, ICrafter
    {
        [Header("Blacksmith Settings")]
        [SerializeField] private GameObject craftingUIPrefab;
        
        private GameObject currentUI;
        private Coroutine craftingCoroutine;
        private BlacksmithData blacksmithData => npcData as BlacksmithData;
        private RuntimeBlacksmithServices runtimeServices;
        private string blacksmithId;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 验证数据类型
            if (npcData != null && !(npcData is BlacksmithData))
            {
                Debug.LogError($"BlacksmithNPC requires BlacksmithData, but got {npcData.GetType().Name}");
            }
            
            // 初始化运行时服务
            runtimeServices = new RuntimeBlacksmithServices();
        }
        
        protected override void Start()
        {
            base.Start();
            
            // 生成唯一ID - 基于地图等级和实例索引
            int mapLevel = GetCurrentMapLevel();
            int instanceIndex = GetInstanceIndex();
            blacksmithId = $"{blacksmithData?.npcId ?? "blacksmith"}_map{mapLevel}_inst{instanceIndex}";
            
            // 初始化服务
            if (blacksmithData != null)
            {
                float seed = mapLevel * 10000f + instanceIndex;
                runtimeServices.InitializeRandomized(blacksmithData, seed);
                
                // 检查存档
                var saveData = NPCRuntimeDataManager.Instance.GetNPCData<BlacksmithServicesSaveData>(blacksmithId);
                if (saveData != null)
                {
                    runtimeServices.LoadSaveData(saveData, blacksmithData);
                }
            }
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            // 保存数据
            if (runtimeServices != null && blacksmithData != null)
            {
                NPCRuntimeDataManager.Instance.SaveNPCData(blacksmithId, runtimeServices.GetSaveData());
            }
        }
        
        
        protected override void OnInteractionStarted(GameObject interactor)
        {
            OpenCraftingMenu(interactor);
        }
        
        protected override void OnInteractionEnded()
        {
            CloseCraftingMenu();
            if (craftingCoroutine != null)
            {
                StopCoroutine(craftingCoroutine);
                craftingCoroutine = null;
            }
        }
        
        // ICrafter 实现
        public void OpenCraftingMenu(GameObject customer)
        {
            if (blacksmithData == null)
            {
                Debug.LogError("BlacksmithNPC: No blacksmith data assigned!");
                return;
            }
            
            ShowDialogue(blacksmithData.greetingCraftText);
            
            // TODO: 显示打造UI
            DisplayCraftingOptions(customer);
        }
        
        public bool CanCraft(GameObject customer, string recipeId)
        {
            if (blacksmithData == null) return false;
            
            CraftingRecipe recipe = runtimeServices.GetRecipe(recipeId);
            if (recipe == null) return false;
            
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            // 检查等级要求
            // TODO: 实现等级系统后添加检查
            
            // 检查图纸要求
            if (recipe.requiresBlueprint && recipe.blueprintItem != null)
            {
                if (inventory.GetItemCount(recipe.blueprintItem) < 1)
                    return false;
            }
            
            // 检查材料
            foreach (var material in recipe.requiredMaterials)
            {
                if (inventory.GetItemCount(material.material) < material.amount)
                    return false;
            }
            
            // 检查金币
            return currencyManager.CanAfford(recipe.craftingFee);
        }
        
        public bool CraftItem(GameObject customer, string recipeId)
        {
            if (!CanCraft(customer, recipeId))
            {
                ShowDialogue(blacksmithData.insufficientMaterialsText);
                return false;
            }
            
            CraftingRecipe recipe = runtimeServices.GetRecipe(recipeId);
            if (recipe == null) return false;
            
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            // 检查背包空间
            if (!inventory.CanAddItem(recipe.resultWeapon, recipe.quantity))
            {
                ShowDialogue("你的背包满了！");
                return false;
            }
            
            // 扣除材料和金币
            foreach (var material in recipe.requiredMaterials)
            {
                inventory.RemoveItem(material.material, material.amount);
            }
            currencyManager.SpendGold(recipe.craftingFee);
            
            // 开始打造
            ShowDialogue(blacksmithData.craftingStartText);
            
            if (craftingCoroutine != null)
            {
                StopCoroutine(craftingCoroutine);
            }
            craftingCoroutine = StartCoroutine(CraftingCoroutine(customer, recipe));
            
            return true;
        }
        
        public bool UpgradeItem(GameObject customer, string itemId)
        {
            var inventory = customer.GetComponent<Inventory.Inventory>();
            if (inventory == null) return false;
            
            // 查找要升级的武器
            WeaponItem weapon = FindWeaponInInventory(inventory, itemId);
            if (weapon == null)
            {
                ShowDialogue("你没有这把武器。");
                return false;
            }
            
            // 检查是否可以升级
            int currentLevel = GetWeaponUpgradeLevel(weapon);
            if (currentLevel >= blacksmithData.maxUpgradeLevel)
            {
                ShowDialogue("这把武器已经达到最高强化等级！");
                return false;
            }
            
            // 找到合适的升级选项
            var availableUpgrades = runtimeServices.GetAvailableUpgrades();
            WeaponUpgrade upgrade = availableUpgrades.Find(u => 
                currentLevel >= u.minUpgradeLevel && 
                currentLevel < u.maxUpgradeLevel);
            if (upgrade == null)
            {
                ShowDialogue("没有适合这把武器的强化方案。");
                return false;
            }
            
            // 检查材料和费用
            if (!CanAffordUpgrade(customer, upgrade, currentLevel))
            {
                ShowDialogue("你没有足够的材料或金币进行强化。");
                return false;
            }
            
            // 执行升级
            PerformWeaponUpgrade(customer, weapon, upgrade, currentLevel);
            return true;
        }
        
        // 铁匠特有方法
        private IEnumerator CraftingCoroutine(GameObject customer, CraftingRecipe recipe)
        {
            // 播放打造动画（如果有）
            // TODO: 触发打造动画
            
            // 等待打造时间
            yield return new WaitForSeconds(recipe.craftingDuration);
            
            // 给予物品
            var inventory = customer.GetComponent<Inventory.Inventory>();
            if (inventory != null)
            {
                inventory.AddItem(recipe.resultWeapon, recipe.quantity);
                ShowDialogue(blacksmithData.craftingCompleteText);
                
                // 播放完成音效
                if (blacksmithData.interactionSound != null)
                {
                    AudioSource.PlayClipAtPoint(blacksmithData.interactionSound, transform.position);
                }
            }
            
            craftingCoroutine = null;
        }
        
        private void PerformWeaponUpgrade(GameObject customer, WeaponItem weapon, WeaponUpgrade upgrade, int currentLevel)
        {
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return;
            
            // 计算费用
            int cost = CalculateUpgradeCost(upgrade, currentLevel);
            
            // 扣除材料和金币
            foreach (var material in upgrade.requiredMaterials)
            {
                inventory.RemoveItem(material.material, material.amount);
            }
            currencyManager.SpendGold(cost);
            
            ShowDialogue(blacksmithData.upgradeStartText);
            
            // 计算成功率
            float successRate = CalculateSuccessRate(upgrade, currentLevel);
            bool success = Random.Range(0f, 1f) <= successRate;
            
            if (success)
            {
                // 应用强化效果
                ApplyUpgradeEffects(weapon, upgrade);
                SetWeaponUpgradeLevel(weapon, currentLevel + 1);
                
                ShowDialogue(blacksmithData.upgradeCompleteText);
            }
            else
            {
                ShowDialogue("强化失败！武器没有变化。");
            }
            
            // 播放音效
            if (blacksmithData.interactionSound != null)
            {
                AudioSource.PlayClipAtPoint(blacksmithData.interactionSound, transform.position);
            }
        }
        
        private void ApplyUpgradeEffects(WeaponItem weapon, WeaponUpgrade upgrade)
        {
            // 使用WeaponUpgradeManager应用强化效果
            WeaponUpgradeManager.Instance.ApplyUpgrade(weapon, upgrade);
        }
        
        private void DisplayCraftingOptions(GameObject customer)
        {
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            Debug.Log($"=== {blacksmithData.npcName}的打造服务 ===");
            Debug.Log($"当前金币: {currencyManager?.CurrentGold ?? 0}");
            
            var availableRecipes = runtimeServices.GetAvailableRecipes();
            if (availableRecipes.Count == 0)
            {
                Debug.Log("抱歉，我这里没有可用的打造配方。");
            }
            else
            {
                // 显示可打造的物品
                Debug.Log("\n【可打造武器】");
                foreach (var recipe in availableRecipes)
                {
                    bool canCraft = CanCraft(customer, recipe.recipeName);
                    string statusText = canCraft ? " [可打造]" : " [材料不足]";
                    
                    Debug.Log($"{recipe.recipeName} - {recipe.craftingFee}金币{statusText}");
                    Debug.Log($"  {recipe.description}");
                    Debug.Log($"  制作时间: {recipe.craftingDuration}秒");
                    Debug.Log($"  所需材料:");
                    
                    foreach (var material in recipe.requiredMaterials)
                    {
                        int owned = inventory?.GetItemCount(material.material) ?? 0;
                        string materialStatus = owned >= material.amount ? "✓" : "✗";
                        Debug.Log($"    {materialStatus} {material.material.ItemName} x{material.amount} (拥有:{owned})");
                    }
                    Debug.Log("");
                }
            }
            
            // 显示升级服务
            if (blacksmithData.canEnhanceWeapons)
            {
                Debug.Log("【武器强化】");
                var equippedWeapon = inventory?.EquippedWeapon;
                if (equippedWeapon != null)
                {
                    var upgradeManager = WeaponUpgradeManager.Instance;
                    int currentLevel = upgradeManager.GetWeaponUpgradeLevel(equippedWeapon);
                    Debug.Log($"当前装备: {equippedWeapon.GetDisplayName()}");
                    
                    if (currentLevel >= blacksmithData.maxUpgradeLevel)
                    {
                        Debug.Log("这把武器已经达到最高强化等级！");
                    }
                    else
                    {
                        Debug.Log($"可以强化到 +{currentLevel + 1} (最高 +{blacksmithData.maxUpgradeLevel})");
                        
                        // 显示强化需求（如果有可用的升级）
                        var availableUpgrades = runtimeServices.GetAvailableUpgrades();
                        var availableUpgrade = availableUpgrades.Find(u => 
                            currentLevel >= u.minUpgradeLevel && 
                            currentLevel < u.maxUpgradeLevel);
                        if (availableUpgrade != null)
                        {
                            int cost = CalculateUpgradeCost(availableUpgrade, currentLevel);
                            float successRate = CalculateSuccessRate(availableUpgrade, currentLevel);
                            Debug.Log($"强化费用: {cost}金币");
                            Debug.Log($"成功率: {successRate:P0}");
                            
                            if (availableUpgrade.requiredMaterials.Count > 0)
                            {
                                Debug.Log("所需材料:");
                                foreach (var material in availableUpgrade.requiredMaterials)
                                {
                                    int owned = inventory?.GetItemCount(material.material) ?? 0;
                                    string materialStatus = owned >= material.amount ? "✓" : "✗";
                                    Debug.Log($"  {materialStatus} {material.material.ItemName} x{material.amount} (拥有:{owned})");
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log("请先装备一把武器。");
                }
            }
        }
        
        
        private bool CanAffordUpgrade(GameObject customer, WeaponUpgrade upgrade, int currentLevel)
        {
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            // 检查材料
            foreach (var material in upgrade.requiredMaterials)
            {
                if (inventory.GetItemCount(material.material) < material.amount)
                    return false;
            }
            
            // 检查金币
            int cost = CalculateUpgradeCost(upgrade, currentLevel);
            return currencyManager.CanAfford(cost);
        }
        
        private int CalculateUpgradeCost(WeaponUpgrade upgrade, int currentLevel)
        {
            float cost = upgrade.baseUpgradeFee;
            
            if (upgrade.scaleCostWithLevel)
            {
                cost *= Mathf.Pow(upgrade.costScalingFactor, currentLevel);
            }
            
            cost *= blacksmithData.upgradePriceMultiplier;
            
            return Mathf.RoundToInt(cost);
        }
        
        private float CalculateSuccessRate(WeaponUpgrade upgrade, int currentLevel)
        {
            float rate = upgrade.baseSuccessRate;
            
            if (upgrade.decreaseRateWithLevel)
            {
                rate -= upgrade.successRateDecrease * currentLevel;
            }
            
            return Mathf.Clamp01(rate);
        }
        
        private WeaponItem FindWeaponInInventory(Inventory.Inventory inventory, string itemId)
        {
            return inventory.FindItemById(itemId) as WeaponItem;
        }
        
        private int GetWeaponUpgradeLevel(WeaponItem weapon)
        {
            return WeaponUpgradeManager.Instance.GetWeaponUpgradeLevel(weapon);
        }
        
        private void SetWeaponUpgradeLevel(WeaponItem weapon, int level)
        {
            // 等级已经在ApplyUpgrade中更新，这里不需要额外操作
        }
        
        private string GetBaseWeaponName(WeaponItem weapon)
        {
            var data = WeaponUpgradeManager.Instance.GetUpgradeData(weapon);
            return data != null ? data.baseName : weapon.ItemName;
        }
        
        private void CloseCraftingMenu()
        {
            if (currentUI != null)
            {
                Destroy(currentUI);
                currentUI = null;
            }
        }
    }
}