namespace Rooms.Core
{
    /// <summary>
    /// 房间类型枚举
    /// </summary>
    public enum RoomType
    {
        Empty,          // 空房间
        Spawn,          // 出生房间
        Monster,        // 怪物房间
        Treasure,       // 宝箱房间
        Fountain,       // 泉水房间
        NPC,            // NPC房间（从池中随机抽取NPC）
        Teleport,       // 传送房间
        Boss,           // Boss房间（预留）
        
        // 以下为废弃类型，保留以兼容旧数据
        [System.Obsolete("Use NPC room type instead")]
        Restaurant,     // 餐饮房间
        [System.Obsolete("Use NPC room type instead")]
        Merchant,       // 商人房间
        [System.Obsolete("Use NPC room type instead")]
        Blacksmith,     // 铁匠房间
        [System.Obsolete("Use NPC room type instead")]
        Doctor,         // 医生房间
        [System.Obsolete("Use NPC room type instead")]
        Tailor          // 裁缝房间
    }
    
    /// <summary>
    /// 门的方向
    /// </summary>
    public enum DoorDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }
    
    /// <summary>
    /// 门的状态
    /// </summary>
    public enum DoorState
    {
        Open,           // 开启
        Closed,         // 关闭
        Locked,         // 锁定（需要钥匙）
        Sealed          // 封印（需要完成条件）
    }
}