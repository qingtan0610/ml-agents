using UnityEngine;
using System.Collections.Generic;
using Inventory.Items;

namespace NPC.Data
{
    [CreateAssetMenu(fileName = "BlacksmithData", menuName = "NPC/Blacksmith Data")]
    public class BlacksmithData : NPCData
    {
        [Header("Crafting Services")]
        public List<CraftingRecipe> recipes = new List<CraftingRecipe>();
        
        [Header("Recipe Randomization")]
        public bool randomizeRecipes = true;
        public int minRecipes = 2;
        public int maxRecipes = 5;
        
        [Header("Upgrade Services")]
        public List<WeaponUpgrade> upgradeOptions = new List<WeaponUpgrade>();
        
        [Header("Upgrade Randomization")]
        public bool randomizeUpgrades = true;
        public int minUpgradeTypes = 1;
        public int maxUpgradeTypes = 3;
        
        [Header("Pricing")]
        [Range(0.5f, 2f)]
        public float craftingPriceMultiplier = 1f;
        [Range(1f, 3f)]
        public float upgradePriceMultiplier = 1.5f;
        
        [Header("Special Features")]
        public bool canRepairWeapons = true;  // 修理武器（本游戏武器无耐久度，预留）
        public bool canEnhanceWeapons = true;  // 强化武器
        public int maxUpgradeLevel = 5;  // 最大强化等级
        
        [Header("Blacksmith Dialogue")]
        [TextArea(2, 4)]
        public string greetingCraftText = "需要打造什么武器吗？";
        [TextArea(2, 4)]
        public string craftingStartText = "开始锻造...";
        [TextArea(2, 4)]
        public string craftingCompleteText = "武器打造完成！";
        [TextArea(2, 4)]
        public string upgradeStartText = "让我看看如何强化这把武器...";
        [TextArea(2, 4)]
        public string upgradeCompleteText = "强化完成！武器变得更强了！";
        [TextArea(2, 4)]
        public string insufficientMaterialsText = "你没有足够的材料。";
        
        protected override void OnValidate()
        {
            base.OnValidate();
            npcType = NPCType.Blacksmith;
            interactionType = NPCInteractionType.Craft;
        }
    }
    
    [System.Serializable]
    public class CraftingRecipe
    {
        public string recipeName = "铁剑";
        public string description = "一把基础的铁剑";
        
        [Header("Result")]
        public WeaponItem resultWeapon;  // 产出的武器
        public int quantity = 1;  // 产出数量
        
        [Header("Materials")]
        public List<CraftingMaterial> requiredMaterials = new List<CraftingMaterial>();
        public int craftingFee = 50;  // 打造费用
        
        [Header("Requirements")]
        public int requiredPlayerLevel = 0;  // 等级要求
        public bool requiresBlueprint = false;  // 是否需要图纸
        public ItemBase blueprintItem;  // 图纸物品
        
        [Header("Crafting Time")]
        public float craftingDuration = 3f;  // 打造时长（秒）
        public string craftingAnimation = "Crafting";
    }
    
    [System.Serializable]
    public class CraftingMaterial
    {
        public ItemBase material;  // 材料物品
        public int amount = 1;  // 需要数量
    }
    
    [System.Serializable]
    public class WeaponUpgrade
    {
        public string upgradeName = "锋利强化";
        public string description = "提升武器的攻击力";
        
        [Header("Upgrade Effects")]
        public float damageIncrease = 5f;  // 攻击力增加
        public float attackSpeedIncrease = 0f;  // 攻速增加
        public float rangeIncrease = 0f;  // 射程增加
        public float critChanceIncrease = 0f;  // 暴击率增加
        
        [Header("Requirements")]
        public int minUpgradeLevel = 0;  // 最低强化等级
        public int maxUpgradeLevel = 5;  // 最高强化等级
        public List<CraftingMaterial> requiredMaterials = new List<CraftingMaterial>();
        
        [Header("Cost")]
        public int baseUpgradeFee = 100;  // 基础强化费用
        public bool scaleCostWithLevel = true;  // 费用是否随等级增长
        public float costScalingFactor = 1.5f;  // 费用增长系数
        
        [Header("Success Rate")]
        public float baseSuccessRate = 1f;  // 基础成功率（1 = 100%）
        public bool decreaseRateWithLevel = false;  // 成功率是否随等级降低
        public float successRateDecrease = 0.1f;  // 每级降低的成功率
    }
}