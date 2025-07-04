using System;

namespace AI.Stats
{
    [Serializable]
    public enum StatType
    {
        Health,
        Hunger,
        Thirst,
        Stamina,
        Armor,
        Toughness,
        
        // Ammo types
        Bullets,
        Arrows,
        Mana
    }
    
    [Serializable]
    public enum MoodDimension
    {
        Emotion,    // 沮丧(-100) ← → 开心(+100)
        Social,     // 孤独(-100) ← → 温暖(+100)
        Mentality   // 急躁(-100) ← → 平静(+100)
    }
    
    [Serializable]
    public enum StatModifierType
    {
        Flat,       // 直接加减
        Percentage  // 百分比修改
    }
    
    [Serializable]
    public enum StatChangeReason
    {
        Natural,    // 自然消耗/恢复
        Combat,     // 战斗伤害
        Item,       // 物品使用
        Interact,   // 交互（如餐馆、泉水）
        Buff,       // Buff效果
        Debuff,     // Debuff效果
        Death,      // 死亡重置
        Other
    }
}