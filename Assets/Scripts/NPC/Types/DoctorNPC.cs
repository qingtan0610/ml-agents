using UnityEngine;
using NPC.Core;
using NPC.Data;
using NPC.Interfaces;
using NPC.Runtime;
using NPC.Managers;
using AI.Stats;
using Inventory.Managers;
using Inventory.Items;
using System.Collections.Generic;

namespace NPC.Types
{
    public class DoctorNPC : NPCBase, IServiceProvider, IShopkeeper
    {
        [Header("Doctor Settings")]
        [SerializeField] private GameObject medicalUIPrefab;
        
        private GameObject currentUI;
        private bool isInShopMode = false;
        private DoctorData doctorData => npcData as DoctorData;
        private RuntimeDoctorServices runtimeServices;
        private string doctorId;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 验证数据类型
            if (npcData != null && !(npcData is DoctorData))
            {
                Debug.LogError($"DoctorNPC requires DoctorData, but got {npcData.GetType().Name}");
            }
            
            // 初始化运行时服务
            runtimeServices = new RuntimeDoctorServices();
        }
        
        protected override void Start()
        {
            base.Start();
            
            // 生成唯一ID - 基于地图等级和实例索引
            int mapLevel = GetCurrentMapLevel();
            int instanceIndex = GetInstanceIndex();
            doctorId = $"{doctorData?.npcId ?? "doctor"}_map{mapLevel}_inst{instanceIndex}";
            
            // 初始化服务
            if (doctorData != null)
            {
                float seed = mapLevel * 10000f + instanceIndex;
                runtimeServices.InitializeRandomized(doctorData, seed);
                
                // 检查存档
                var saveData = NPCRuntimeDataManager.Instance.GetNPCData<DoctorServicesSaveData>(doctorId);
                if (saveData != null)
                {
                    runtimeServices.LoadSaveData(saveData, doctorData);
                }
            }
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            // 保存数据
            if (runtimeServices != null && doctorData != null)
            {
                NPCRuntimeDataManager.Instance.SaveNPCData(doctorId, runtimeServices.GetSaveData());
            }
        }
        
        protected override void OnInteractionStarted(GameObject interactor)
        {
            ExaminePatient(interactor);
        }
        
        protected override void OnInteractionEnded()
        {
            CloseUI();
            isInShopMode = false;
        }
        
        // IServiceProvider 实现
        public void ProvideService(GameObject customer, string serviceId)
        {
            if (doctorData == null)
            {
                Debug.LogError("DoctorNPC: No doctor data assigned!");
                return;
            }
            
            // 查找医疗服务
            MedicalService service = FindService(serviceId);
            if (service != null)
            {
                PerformMedicalService(customer, service);
            }
        }
        
        public bool CanProvideService(GameObject customer, string serviceId)
        {
            if (doctorData == null) return false;
            
            MedicalService service = FindService(serviceId);
            if (service == null) return false;
            
            // 检查等级要求
            // TODO: 实现等级系统后添加检查
            
            // 检查物品要求
            if (service.requiresItem && service.requiredItem != null)
            {
                var inventory = customer.GetComponent<Inventory.Inventory>();
                if (inventory == null || inventory.GetItemCount(service.requiredItem) < 1)
                    return false;
            }
            
            // 检查金币
            var currencyManager = customer.GetComponent<CurrencyManager>();
            if (currencyManager == null) return false;
            
            int cost = CalculateServiceCost(customer, service);
            return currencyManager.CanAfford(cost);
        }
        
        public int GetServiceCost(string serviceId)
        {
            MedicalService service = FindService(serviceId);
            return service?.basePrice ?? 0;
        }
        
        public string GetServiceDescription(string serviceId)
        {
            MedicalService service = FindService(serviceId);
            return service?.description ?? "未知服务";
        }
        
        // IShopkeeper 实现（药品商店）
        public void OpenShop(GameObject customer)
        {
            if (doctorData == null || doctorData.medicineShop == null)
            {
                Debug.LogError("DoctorNPC: No medicine shop configured!");
                return;
            }
            
            isInShopMode = true;
            ShowDialogue(doctorData.medicineShopText);
            
            // TODO: 显示药品商店UI
            DisplayMedicineShop();
        }
        
        public bool CanAfford(GameObject customer, string itemId, int quantity)
        {
            var currencyManager = customer.GetComponent<CurrencyManager>();
            if (currencyManager == null) return false;
            
            var shopItem = doctorData.medicineShop.GetShopItem(itemId);
            if (shopItem == null || shopItem.item == null) return false;
            
            float price = GetItemPrice(shopItem) * quantity;
            return currencyManager.CanAfford(Mathf.RoundToInt(price));
        }
        
        public bool PurchaseItem(GameObject customer, string itemId, int quantity)
        {
            if (!CanAfford(customer, itemId, quantity))
            {
                ShowDialogue("你的金币不够购买药品。");
                return false;
            }
            
            var inventory = customer.GetComponent<Inventory.Inventory>();
            var currencyManager = customer.GetComponent<CurrencyManager>();
            
            if (inventory == null || currencyManager == null) return false;
            
            var shopItem = doctorData.medicineShop.GetShopItem(itemId);
            if (shopItem == null || shopItem.item == null) return false;
            
            // 检查背包空间
            if (!inventory.CanAddItem(shopItem.item, quantity))
            {
                ShowDialogue("你的背包满了！");
                return false;
            }
            
            // 执行交易
            float totalPrice = GetItemPrice(shopItem) * quantity;
            if (currencyManager.SpendGold(Mathf.RoundToInt(totalPrice)))
            {
                inventory.AddItem(shopItem.item, quantity);
                ShowDialogue("希望这些药品对你有帮助。");
                return true;
            }
            
            return false;
        }
        
        public float GetPriceMultiplier()
        {
            return doctorData.medicinePriceMultiplier;
        }
        
        // 医生特有方法
        private void ExaminePatient(GameObject patient)
        {
            var aiStats = patient.GetComponent<AIStats>();
            if (aiStats == null)
            {
                Debug.LogError("Patient has no AIStats component!");
                return;
            }
            
            ShowDialogue(doctorData.examineText);
            
            // 检查健康状况
            float healthPercent = aiStats.GetStatPercentage(StatType.Health);
            
            if (healthPercent >= 0.95f)
            {
                ShowDialogue(doctorData.healthyText);
            }
            else if (healthPercent < 0.2f && doctorData.providesEmergencyCare)
            {
                ShowDialogue(doctorData.emergencyText);
                // 自动提供紧急治疗
                OfferEmergencyTreatment(patient);
            }
            else
            {
                // 显示可用的医疗服务
                DisplayAvailableServices(patient);
            }
        }
        
        private void PerformMedicalService(GameObject patient, MedicalService service)
        {
            var aiStats = patient.GetComponent<AIStats>();
            var currencyManager = patient.GetComponent<CurrencyManager>();
            
            if (aiStats == null || currencyManager == null) return;
            
            // 计算费用
            int cost = CalculateServiceCost(patient, service);
            
            // 检查物品要求
            if (service.requiresItem && service.requiredItem != null)
            {
                var inventory = patient.GetComponent<Inventory.Inventory>();
                if (inventory == null || !inventory.RemoveItem(service.requiredItem, 1))
                {
                    ShowDialogue($"你需要 {service.requiredItem.ItemName} 才能接受这项治疗。");
                    return;
                }
            }
            
            // 扣除费用
            if (!currencyManager.SpendGold(cost))
            {
                ShowDialogue("你的金币不足。");
                return;
            }
            
            // 应用治疗效果
            ApplyMedicalEffects(patient, service);
            
            ShowDialogue(doctorData.treatmentText);
            
            // 播放治疗音效
            if (doctorData.interactionSound != null)
            {
                AudioSource.PlayClipAtPoint(doctorData.interactionSound, transform.position);
            }
        }
        
        private void ApplyMedicalEffects(GameObject patient, MedicalService service)
        {
            var aiStats = patient.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            // 恢复生命值
            if (service.fullHeal)
            {
                float maxHealth = aiStats.GetStatPercentage(StatType.Health) > 0 ? 
                    aiStats.GetStat(StatType.Health) / aiStats.GetStatPercentage(StatType.Health) : 100f;
                float currentHealth = aiStats.GetStat(StatType.Health);
                aiStats.ModifyStat(StatType.Health, maxHealth - currentHealth, StatChangeReason.Item);
            }
            else if (service.healthRestore > 0)
            {
                aiStats.ModifyStat(StatType.Health, service.healthRestore, StatChangeReason.Item);
            }
            
            // TODO: 处理中毒和疾病治疗
            // if (service.curePoison) { ... }
            // if (service.cureDisease) { ... }
            
            // 应用免疫效果
            if (service.providesImmunity)
            {
                ApplyImmunity(patient, service);
            }
        }
        
        private void ApplyImmunity(GameObject patient, MedicalService service)
        {
            var aiStats = patient.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            // 创建免疫修饰器
            var modifier = new StatModifier(
                $"immunity_{service.immunityType}_{Time.time}",
                service.immunityType,
                StatModifierType.Percentage,  // 使用正确的枚举值
                100f,  // 100%免疫
                service.immunityDuration
            );
            
            // TODO: 实现免疫系统
            Debug.Log($"Applied immunity to {service.immunityType} for {service.immunityDuration} seconds");
        }
        
        private int CalculateServiceCost(GameObject patient, MedicalService service)
        {
            float baseCost = service.basePrice;
            
            // 根据损失的生命值计算价格
            if (service.priceBasedOnMissingHealth)
            {
                var aiStats = patient.GetComponent<AIStats>();
                if (aiStats != null)
                {
                    float maxHealth = aiStats.GetStatPercentage(StatType.Health) > 0 ? 
                        aiStats.GetStat(StatType.Health) / aiStats.GetStatPercentage(StatType.Health) : 100f;
                    float currentHealth = aiStats.GetStat(StatType.Health);
                    float missingHealth = maxHealth - currentHealth;
                    baseCost = missingHealth * 0.5f;  // 每点生命值0.5金币
                }
            }
            
            // 应用价格倍率
            baseCost *= doctorData.servicePriceMultiplier;
            
            // 紧急治疗折扣
            if (doctorData.providesEmergencyCare)
            {
                var aiStats = patient.GetComponent<AIStats>();
                if (aiStats != null && aiStats.GetStatPercentage(StatType.Health) < 0.2f)
                {
                    baseCost *= (1f - doctorData.emergencyCareDiscount / 100f);
                }
            }
            
            return Mathf.RoundToInt(baseCost);
        }
        
        private void OfferEmergencyTreatment(GameObject patient)
        {
            // 找到基础治疗服务
            var availableServices = runtimeServices.GetAvailableServices();
            MedicalService emergencyService = availableServices.Find(s => s.fullHeal || s.healthRestore >= 50);
            if (emergencyService != null)
            {
                PerformMedicalService(patient, emergencyService);
            }
        }
        
        private void DisplayAvailableServices(GameObject patient)
        {
            Debug.Log($"=== {doctorData.npcName}的医疗服务 ===");
            
            var availableServices = runtimeServices.GetAvailableServices();
            foreach (var service in availableServices)
            {
                int cost = CalculateServiceCost(patient, service);
                bool canAfford = CanProvideService(patient, service.serviceName);
                string affordText = canAfford ? "" : " [金币不足]";
                
                Debug.Log($"{service.serviceName} - {cost}金币{affordText}");
                Debug.Log($"  {service.description}");
            }
            
            if (doctorData.medicineShop != null && doctorData.medicineShop.items.Count > 0)
            {
                Debug.Log("\n[输入 'shop' 查看药品商店]");
            }
        }
        
        private void DisplayMedicineShop()
        {
            if (doctorData.medicineShop == null) return;
            
            Debug.Log($"=== {doctorData.npcName}的药品商店 ===");
            
            foreach (var shopItem in doctorData.medicineShop.items)
            {
                if (shopItem.item == null) continue;
                
                float price = GetItemPrice(shopItem);
                string stockText = shopItem.stock == -1 ? "无限" : shopItem.stock.ToString();
                
                Debug.Log($"{shopItem.item.ItemName} - {price:F0}金币 (库存: {stockText})");
                Debug.Log($"  {shopItem.item.Description}");
            }
        }
        
        private float GetItemPrice(ShopInventory.ShopItem shopItem)
        {
            if (shopItem.priceOverride > 0)
            {
                return shopItem.priceOverride;
            }
            
            return shopItem.item.BuyPrice * doctorData.medicinePriceMultiplier;
        }
        
        private MedicalService FindService(string serviceName)
        {
            return runtimeServices?.GetService(serviceName);
        }
        
        private void CloseUI()
        {
            if (currentUI != null)
            {
                Destroy(currentUI);
                currentUI = null;
            }
        }
        
        /// <summary>
        /// 处理AI请求
        /// </summary>
        public bool HandleAIRequest(string request, GameObject ai)
        {
            return AI.NPCAIInteractionHandler.HandleDoctorAIInteraction(this, ai, request);
        }
    }
}