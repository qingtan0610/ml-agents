using UnityEngine;
using NPC.Core;
using NPC.Data;
using NPC.Interfaces;
using Inventory;
using Inventory.Items;
using Inventory.Managers;
using System.Collections;

namespace NPC.Types
{
    public class TailorNPC : NPCBase, IServiceProvider
    {
        [Header("Tailor Settings")]
        [SerializeField] private GameObject upgradeUIPrefab;
        
        private GameObject currentUI;
        private Coroutine upgradeCoroutine;
        private TailorData tailorData => npcData as TailorData;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 验证数据类型
            if (npcData != null && !(npcData is TailorData))
            {
                Debug.LogError($"TailorNPC requires TailorData, but got {npcData.GetType().Name}");
            }
        }
        
        
        protected override void OnInteractionStarted(GameObject interactor)
        {
            // 确保有默认数据
            if (tailorData != null)
            {
                if (tailorData.bagUpgrades == null || tailorData.bagUpgrades.Count == 0)
                {
                    CreateDefaultBagUpgrades();
                }
            }
            
            ExamineBag(interactor);
        }
        
        protected override void OnInteractionEnded()
        {
            CloseUI();
            if (upgradeCoroutine != null)
            {
                StopCoroutine(upgradeCoroutine);
                upgradeCoroutine = null;
            }
        }
        
        // IServiceProvider 实现
        public void ProvideService(GameObject customer, string serviceId)
        {
            if (tailorData == null)
            {
                Debug.LogError("TailorNPC: No tailor data assigned!");
                return;
            }
            
            // 查找背包升级服务
            BagUpgrade upgrade = FindUpgrade(serviceId);
            if (upgrade != null)
            {
                PerformBagUpgrade(customer, upgrade);
            }
        }
        
        public bool CanProvideService(GameObject customer, string serviceId)
        {
            if (tailorData == null) return false;
            
            BagUpgrade upgrade = FindUpgrade(serviceId);
            if (upgrade == null) return false;
            
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            // 检查当前背包容量是否符合升级要求
            int currentSlots = inventory.GetMaxSlots();
            if (currentSlots < upgrade.requiredCurrentSlots || currentSlots >= upgrade.maxSlots)
                return false;
            
            // 检查材料
            foreach (var material in upgrade.requiredMaterials)
            {
                if (inventory.GetItemCount(material.material) < material.amount)
                    return false;
            }
            
            // 检查金币
            int cost = CalculateUpgradeCost(upgrade);
            return currencyManager.CanAfford(cost);
        }
        
        public int GetServiceCost(string serviceId)
        {
            BagUpgrade upgrade = FindUpgrade(serviceId);
            return upgrade != null ? CalculateUpgradeCost(upgrade) : 0;
        }
        
        public string GetServiceDescription(string serviceId)
        {
            BagUpgrade upgrade = FindUpgrade(serviceId);
            return upgrade?.description ?? "未知服务";
        }
        
        // 裁缝特有方法
        private void ExamineBag(GameObject customer)
        {
            var inventory = customer.GetComponent<Inventory.Inventory>();
            if (inventory == null)
            {
                Debug.LogError("Customer has no Inventory component!");
                return;
            }
            
            ShowDialogue(tailorData.examineText);
            
            // 检查当前背包容量
            int currentSlots = inventory.GetMaxSlots();
            ShowDialogue(string.Format(tailorData.currentCapacityText, currentSlots));
            
            // 显示所有升级选项，让玩家看到可用的和不可用的
            DisplayAllUpgradeOptions(customer);
        }
        
        private void PerformBagUpgrade(GameObject customer, BagUpgrade upgrade)
        {
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return;
            
            // 再次检查是否可以升级
            if (!CanProvideService(customer, upgrade.upgradeName))
            {
                ShowDialogue(tailorData.insufficientFundsText);
                return;
            }
            
            // 扣除材料和金币
            foreach (var material in upgrade.requiredMaterials)
            {
                inventory.RemoveItem(material.material, material.amount);
            }
            
            int cost = CalculateUpgradeCost(upgrade);
            currencyManager.SpendGold(cost);
            
            // 开始升级动画
            if (upgradeCoroutine != null)
            {
                StopCoroutine(upgradeCoroutine);
            }
            upgradeCoroutine = StartCoroutine(UpgradeCoroutine(customer, upgrade));
        }
        
        private IEnumerator UpgradeCoroutine(GameObject customer, BagUpgrade upgrade)
        {
            // 播放缝纫动画（如果有）
            // TODO: 触发缝纫动画
            
            // 等待升级时间
            yield return new WaitForSeconds(upgrade.upgradeDuration);
            
            // 应用升级
            var inventory = customer.GetComponent<Inventory.Inventory>();
            if (inventory != null)
            {
                // 扩展背包容量
                inventory.ExpandCapacity(upgrade.slotsToAdd);
                
                ShowDialogue(tailorData.upgradeCompleteText);
                
                // 播放完成音效
                if (tailorData.interactionSound != null)
                {
                    AudioSource.PlayClipAtPoint(tailorData.interactionSound, transform.position);
                }
            }
            
            upgradeCoroutine = null;
        }
        
        private void DisplayUpgradeOptions(GameObject customer, BagUpgrade upgrade)
        {
            Debug.Log($"=== {tailorData.npcName}的背包升级服务 ===");
            
            int cost = CalculateUpgradeCost(upgrade);
            bool canAfford = CanProvideService(customer, upgrade.upgradeName);
            string statusText = canAfford ? "" : " [材料或金币不足]";
            
            Debug.Log($"{upgrade.upgradeName} - {cost}金币{statusText}");
            Debug.Log($"  {upgrade.description}");
            Debug.Log($"  当前容量: {upgrade.requiredCurrentSlots} → {upgrade.maxSlots}");
            
            if (upgrade.requiredMaterials.Count > 0)
            {
                Debug.Log("  所需材料:");
                foreach (var material in upgrade.requiredMaterials)
                {
                    Debug.Log($"    - {material.material.ItemName} x{material.amount}");
                }
            }
            
            // 显示其他服务（如果有）
            if (tailorData.canDyeClothes)
            {
                Debug.Log("\n[染色服务即将推出]");
            }
        }
        
        private BagUpgrade FindUpgrade(string upgradeName)
        {
            if (tailorData == null || tailorData.bagUpgrades == null) return null;
            
            return tailorData.bagUpgrades.Find(u => u.upgradeName == upgradeName);
        }
        
        private void DisplayAllUpgradeOptions(GameObject customer)
        {
            if (tailorData == null)
            {
                ShowDialogue("抱歉，我这里没有背包升级服务。");
                return;
            }
            
            // 如果没有配置升级选项，创建默认的
            if (tailorData.bagUpgrades == null || tailorData.bagUpgrades.Count == 0)
            {
                CreateDefaultBagUpgrades();
            }
            
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            int currentSlots = inventory.GetMaxSlots();
            
            Debug.Log($"=== {tailorData.npcName}的背包扩容服务 ===");
            Debug.Log($"当前背包容量: {currentSlots}格");
            Debug.Log("\n【可用升级】:");
            
            bool hasAvailableUpgrade = false;
            
            foreach (var upgrade in tailorData.bagUpgrades)
            {
                bool canUpgrade = currentSlots == upgrade.requiredCurrentSlots;
                bool canAfford = CanProvideService(customer, upgrade.upgradeName);
                
                string statusText = "";
                if (!canUpgrade)
                {
                    if (currentSlots < upgrade.requiredCurrentSlots)
                        statusText = " [容量不足]";
                    else
                        statusText = " [已超过]";
                }
                else if (!canAfford)
                {
                    statusText = " [材料不足]";
                }
                else
                {
                    statusText = " [可升级]";
                    hasAvailableUpgrade = true;
                }
                
                Debug.Log($"{upgrade.upgradeName}{statusText}");
                Debug.Log($"  要求: {upgrade.requiredCurrentSlots}格 -> {upgrade.maxSlots}格 (+{upgrade.slotsToAdd}格)");
                Debug.Log($"  费用: {CalculateUpgradeCost(upgrade)}金币");
                
                if (upgrade.requiredMaterials.Count > 0)
                {
                    Debug.Log("  材料需求:");
                    foreach (var material in upgrade.requiredMaterials)
                    {
                        int owned = inventory.GetItemCount(material.material);
                        Debug.Log($"    - {material.material.ItemName} x{material.amount} (拥有:{owned})");
                    }
                }
                Debug.Log("");
            }
            
            if (hasAvailableUpgrade)
            {
                ShowDialogue(tailorData.upgradeOfferText);
            }
            else
            {
                ShowDialogue("目前没有适合你的升级选项，请提升背包容量后再来。");
            }
        }
        
        private BagUpgrade FindAvailableUpgrade(Inventory.Inventory inventory)
        {
            if (tailorData == null || tailorData.bagUpgrades == null) return null;
            
            int currentSlots = inventory.GetMaxSlots();
            
            // 修改条件：当前容量必须等于要求的容量
            return tailorData.bagUpgrades.Find(u => 
                currentSlots == u.requiredCurrentSlots);
        }
        
        private void CreateDefaultBagUpgrades()
        {
            if (tailorData.bagUpgrades == null)
            {
                tailorData.bagUpgrades = new System.Collections.Generic.List<BagUpgrade>();
            }
            
            // 创建默认的背包升级选项
            tailorData.bagUpgrades.Add(new BagUpgrade
            {
                upgradeName = "小型背包扩展",
                description = "增加2个背包格子",
                slotsToAdd = 2,
                requiredCurrentSlots = 10,
                maxSlots = 12,
                upgradeFee = 500,
                requiredMaterials = new System.Collections.Generic.List<UpgradeMaterial>()
            });
            
            tailorData.bagUpgrades.Add(new BagUpgrade
            {
                upgradeName = "中型背包扩展",
                description = "增加3个背包格子",
                slotsToAdd = 3,
                requiredCurrentSlots = 12,
                maxSlots = 15,
                upgradeFee = 1000,
                requiredMaterials = new System.Collections.Generic.List<UpgradeMaterial>()
            });
            
            tailorData.bagUpgrades.Add(new BagUpgrade
            {
                upgradeName = "大型背包扩展",
                description = "增加5个背包格子",
                slotsToAdd = 5,
                requiredCurrentSlots = 15,
                maxSlots = 20,
                upgradeFee = 2000,
                requiredMaterials = new System.Collections.Generic.List<UpgradeMaterial>()
            });
        }
        
        private int CalculateUpgradeCost(BagUpgrade upgrade)
        {
            return Mathf.RoundToInt(upgrade.upgradeFee * tailorData.upgradePriceMultiplier);
        }
        
        private void CloseUI()
        {
            if (currentUI != null)
            {
                Destroy(currentUI);
                currentUI = null;
            }
        }
        
        // 额外服务（预留）
        public void ProvideDyeService(GameObject customer, Color newColor)
        {
            if (!tailorData.canDyeClothes)
            {
                ShowDialogue("抱歉，染色服务暂时不可用。");
                return;
            }
            
            // TODO: 实现染色服务
            Debug.Log($"Dyeing clothes to color: {newColor}");
        }
        
        public void ProvideRepairService(GameObject customer, ItemBase armorItem)
        {
            if (!tailorData.canRepairArmor)
            {
                ShowDialogue("抱歉，护甲修理服务暂时不可用。");
                return;
            }
            
            // TODO: 实现护甲修理服务
            Debug.Log($"Repairing armor: {armorItem.ItemName}");
        }
    }
}