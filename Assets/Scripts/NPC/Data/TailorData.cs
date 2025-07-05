using UnityEngine;
using System.Collections.Generic;
using Inventory.Items;

namespace NPC.Data
{
    [CreateAssetMenu(fileName = "TailorData", menuName = "NPC/Tailor Data")]
    public class TailorData : NPCData
    {
        [Header("Bag Upgrade Services")]
        public List<BagUpgrade> bagUpgrades = new List<BagUpgrade>();
        
        [Header("Service Randomization")]
        public bool randomizeUpgrades = true;
        public int minUpgradeOptions = 1;
        public int maxUpgradeOptions = 3;
        
        [Header("Special Services")]
        public bool canRepairArmor = true;  // 修理护甲（预留）
        public bool canDyeClothes = true;  // 染色服务（预留）
        
        [Header("Pricing")]
        [Range(1f, 3f)]
        public float upgradePriceMultiplier = 1.5f;
        
        [Header("Tailor Dialogue")]
        [TextArea(2, 4)]
        public string examineText = "让我看看你的背包...";
        [TextArea(2, 4)]
        public string currentCapacityText = "你的背包目前有{0}个格子。";
        [TextArea(2, 4)]
        public string upgradeOfferText = "我可以帮你扩展背包容量。";
        [TextArea(2, 4)]
        public string upgradeCompleteText = "背包扩展完成！现在有更多空间了。";
        [TextArea(2, 4)]
        public string maxCapacityText = "你的背包已经达到最大容量了！";
        [TextArea(2, 4)]
        public string insufficientFundsText = "你没有足够的金币或材料。";
        
        protected override void OnValidate()
        {
            base.OnValidate();
            npcType = NPCType.Tailor;
            interactionType = NPCInteractionType.Upgrade;
        }
    }
    
    [System.Serializable]
    public class BagUpgrade
    {
        public string upgradeName = "小型背包扩展";
        public string description = "增加2个背包格子";
        
        [Header("Upgrade Details")]
        public int slotsToAdd = 2;  // 增加的格子数
        public int requiredCurrentSlots = 10;  // 需要的当前格子数
        public int maxSlots = 12;  // 升级后的最大格子数
        
        [Header("Requirements")]
        public List<UpgradeMaterial> requiredMaterials = new List<UpgradeMaterial>();
        public int upgradeFee = 500;  // 升级费用
        
        [Header("Visual")]
        public Sprite upgradeIcon;
        public string upgradeAnimation = "Sewing";
        public float upgradeDuration = 3f;
    }
    
    [System.Serializable]
    public class UpgradeMaterial
    {
        public ItemBase material;  // 所需材料
        public int amount = 1;  // 数量
    }
}