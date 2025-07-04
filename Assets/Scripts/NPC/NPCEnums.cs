using System;

namespace NPC
{
    [Serializable]
    public enum NPCType
    {
        Merchant,       // 商人
        Restaurant,     // 餐厅服务员
        Doctor,         // 医生
        Blacksmith,     // 铁匠
        Tailor,         // 裁缝
        QuestGiver,     // 任务发布者（预留）
        Traveler        // 旅行者（预留）
    }
    
    [Serializable]
    public enum NPCInteractionType
    {
        Shop,           // 商店交易
        Service,        // 服务（治疗、餐饮）
        Craft,          // 打造/强化
        Upgrade,        // 升级（背包扩容）
        Dialogue,       // 纯对话
        Quest           // 任务相关
    }
    
    [Serializable]
    public enum NPCMood
    {
        Happy,          // 开心（可能打折）
        Neutral,        // 中性
        Grumpy,         // 不高兴（可能加价）
        Excited,        // 兴奋（特殊商品）
        Suspicious      // 怀疑（需要好感度）
    }
}