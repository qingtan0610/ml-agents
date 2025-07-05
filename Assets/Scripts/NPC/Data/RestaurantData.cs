using UnityEngine;
using System.Collections.Generic;
using AI.Stats;

namespace NPC.Data
{
    [CreateAssetMenu(fileName = "RestaurantData", menuName = "NPC/Restaurant Data")]
    public class RestaurantData : NPCData
    {
        [Header("Restaurant Menu")]
        public List<FoodMenuItem> menu = new List<FoodMenuItem>();
        
        [Header("Menu Randomization")]
        public bool randomizeMenu = true;
        public int minDishes = 3;
        public int maxDishes = 6;
        
        [Header("Service Settings")]
        public float eatingDuration = 3f;  // 用餐动画时长
        public bool provideFreeWater = true;  // 是否提供免费水
        public int waterRestoreAmount = 50;  // 水恢复量
        
        [Header("Restaurant Dialogue")]
        [TextArea(2, 4)]
        public string welcomeText = "欢迎光临！请看菜单。";
        [TextArea(2, 4)]
        public string orderText = "您想要点什么？";
        [TextArea(2, 4)]
        public string servingText = "您的餐点来了，请慢用！";
        [TextArea(2, 4)]
        public string thankYouText = "感谢光临，吃饱了再上路！";
        
        protected override void OnValidate()
        {
            base.OnValidate();
            npcType = NPCType.Restaurant;
            interactionType = NPCInteractionType.Service;
        }
    }
    
    [System.Serializable]
    public class FoodMenuItem
    {
        public string itemName = "食物";
        public string description = "美味的食物";
        public Sprite foodIcon;
        
        [Header("Effects")]
        public int hungerRestore = 30;  // 饥饿值恢复
        public int thirstRestore = 0;   // 口渴值恢复
        public int healthRestore = 0;   // 生命值恢复
        public int staminaRestore = 0;  // 体力值恢复
        
        [Header("Buff Effects")]
        public bool hasBuffEffect = false;
        public BuffData buffData;
        
        [Header("Pricing")]
        public int price = 10;
        public bool isSpecialDish = false;  // 特色菜
        
        [Header("Visual")]
        public GameObject foodModelPrefab;  // 食物3D模型
        public string eatingAnimation = "Eat";
    }
    
    [System.Serializable]
    public class BuffData
    {
        public string buffName = "饱腹感";
        public float duration = 300f;  // 持续时间（秒）
        public StatType affectedStat;
        public float effectValue = 10f;
        public StatModifierType modifierType;
    }
}