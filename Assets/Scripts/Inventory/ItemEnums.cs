using System;

namespace Inventory
{
    [Serializable]
    public enum ItemType
    {
        Consumable,     // 消耗品（食物、药水）
        Weapon,         // 武器
        Equipment,      // 装备（预留）
        Misc,           // 杂项
        Quest,          // 任务物品
    }
    
    [Serializable]
    public enum ItemRarity
    {
        Common,         // 普通
        Uncommon,       // 优秀
        Rare,           // 稀有
        Epic,           // 史诗
        Legendary       // 传说
    }
    
    [Serializable]
    public enum EquipmentSlot
    {
        None,
        Head,           // 头部
        Body,           // 身体
        Legs,           // 腿部
        Feet,           // 脚部
        MainHand,       // 主手武器
        OffHand,        // 副手
        Accessory1,     // 饰品1
        Accessory2      // 饰品2
    }
    
    [Serializable]
    public enum WeaponType
    {
        Melee,          // 近战
        Ranged,         // 远程
        Magic           // 魔法
    }
    
    [Serializable]
    public enum AmmoType
    {
        None,
        Bullets,        // 子弹
        Arrows,         // 箭矢
        Mana            // 魔力
    }
    
    [Serializable]
    public enum ConsumableType
    {
        Food,           // 食物
        Drink,          // 饮料
        Potion,         // 药水
        Buff,           // 增益物品
        Other           // 其他
    }
    
    [Serializable]
    public enum AttackShape
    {
        Circle,         // 圆形（全方位）
        Sector,         // 扇形（正面扇形）
        Rectangle,      // 矩形（前方矩形）
        Line            // 直线（穿透直线）
    }
}