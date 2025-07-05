using UnityEngine;
using System.Collections.Generic;
using Inventory.Items;

namespace NPC.Data
{
    [CreateAssetMenu(fileName = "DoctorData", menuName = "NPC/Doctor Data")]
    public class DoctorData : NPCData
    {
        [Header("Medical Services")]
        public List<MedicalService> services = new List<MedicalService>();
        
        [Header("Service Randomization")]
        public bool randomizeServices = true;
        public int minServices = 2;
        public int maxServices = 4;
        
        [Header("Medicine Shop")]
        public ShopInventory medicineShop;  // 使用MerchantData中的结构
        public bool randomizeMedicine = true;
        public int minMedicineTypes = 3;
        public int maxMedicineTypes = 6;
        
        [Header("Pricing")]
        [Range(0.5f, 2f)]
        public float servicePriceMultiplier = 1f;  // 服务价格倍率
        [Range(0.8f, 1.5f)]
        public float medicinePriceMultiplier = 1.2f;  // 药品价格倍率
        
        [Header("Special Conditions")]
        public bool providesEmergencyCare = true;  // 紧急治疗（生命值低于20%）
        public int emergencyCareDiscount = 50;  // 紧急治疗折扣
        public bool offersImmunity = true;  // 提供免疫服务
        
        [Header("Doctor Dialogue")]
        [TextArea(2, 4)]
        public string examineText = "让我看看你的状况...";
        [TextArea(2, 4)]
        public string healthyText = "你很健康，不需要治疗。";
        [TextArea(2, 4)]
        public string treatmentText = "治疗完成，感觉好多了吧？";
        [TextArea(2, 4)]
        public string emergencyText = "你的状况很危急！我会立即治疗你。";
        [TextArea(2, 4)]
        public string medicineShopText = "我这里也有一些药品出售。";
        
        protected override void OnValidate()
        {
            base.OnValidate();
            npcType = NPCType.Doctor;
            interactionType = NPCInteractionType.Service;
        }
    }
    
    [System.Serializable]
    public class MedicalService
    {
        public string serviceName = "基础治疗";
        public string description = "恢复一定生命值";
        
        [Header("Effects")]
        public int healthRestore = 50;
        public bool fullHeal = false;  // 是否完全治疗
        public bool curePoison = false;  // 治疗中毒
        public bool cureDisease = false;  // 治疗疾病
        
        [Header("Immunity")]
        public bool providesImmunity = false;
        public AI.Stats.StatType immunityType;  // 免疫类型
        public float immunityDuration = 300f;  // 免疫持续时间
        
        [Header("Pricing")]
        public int basePrice = 50;
        public bool priceBasedOnMissingHealth = false;  // 价格是否基于损失的生命值
        
        [Header("Requirements")]
        public int minPlayerLevel = 0;  // 最低等级要求
        public bool requiresItem = false;  // 是否需要特定物品
        public ItemBase requiredItem;  // 需要的物品
    }
}