using UnityEngine;
using AI.Stats;
using AI.Perception;
using Buffs;

namespace Inventory.Items
{
    /// <summary>
    /// 视野药水 - 使用后可以看到周围四个房间
    /// </summary>
    [CreateAssetMenu(fileName = "Potion_Vision", menuName = "Inventory/Items/Vision Potion")]
    public class VisionPotion : ConsumableItem
    {
        [Header("Vision Potion Settings")]
        [SerializeField] private float visionDuration = 60f; // 持续60秒
        [SerializeField] private VisionBuff visionBuffPrefab; // 视野增强Buff
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置视野药水的默认值
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = "视野药水";
            }
            
            if (string.IsNullOrEmpty(description))
            {
                description = "饮用后可以观察到当前房间和周围四个房间，持续60秒";
            }
            
            // 视野药水属于药水类
            // consumableType 是 protected 的，无法直接设置
            
            // 清空属性效果，因为视野药水的效果是特殊的
            // statEffects 是 protected 的，无法直接访问
            
            // 添加到appliedBuffs列表
            // appliedBuffs 是 protected 的，无法直接访问
            // 需要在Unity编辑器中手动设置
        }
    }
}