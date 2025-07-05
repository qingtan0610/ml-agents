using UnityEngine;
using AI.Perception;

namespace Buffs
{
    /// <summary>
    /// 视野增强Buff - 允许看到周围四个房间
    /// </summary>
    [CreateAssetMenu(fileName = "Buff_Vision", menuName = "Buffs/Vision Buff")]
    public class VisionBuff : BuffBase
    {
        // Override CreateInstance to handle the vision enhancement logic
        public override BuffInstance CreateInstance(GameObject target)
        {
            // Create the base instance
            var instance = new VisionBuffInstance(this, target);
            return instance;
        }
        
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置默认值
            if (string.IsNullOrEmpty(buffName))
            {
                buffName = "增强视野";
            }
            
            if (string.IsNullOrEmpty(description))
            {
                description = "可以看到当前房间和周围四个房间";
            }
            
            // 视野buff是正面效果
            buffType = BuffType.Buff;
            
            // 默认持续时间
            if (duration <= 0)
            {
                duration = 60f;
            }
            
            // 不可叠加
            isStackable = false;
        }
    }
    
    /// <summary>
    /// 视野增强Buff的运行时实例
    /// </summary>
    public class VisionBuffInstance : BuffInstance
    {
        private AIPerception perception;
        
        public VisionBuffInstance(BuffBase data, GameObject target) : base(data, target)
        {
            // 获取AIPerception组件
            perception = target.GetComponent<AIPerception>();
            
            // 启用增强视野
            if (perception != null)
            {
                perception.SetEnhancedVision(true, data.Duration);
                Debug.Log($"[VisionBuff] 为 {target.name} 启用增强视野，持续 {data.Duration} 秒");
            }
        }
        
        public new void Remove()
        {
            // 先调用基类的Remove
            base.Remove();
            
            // 关闭增强视野
            if (perception != null)
            {
                perception.SetEnhancedVision(false);
                Debug.Log($"[VisionBuff] {Target.name} 的增强视野效果结束");
            }
        }
        
        // 需要添加Target属性访问
        private GameObject Target
        {
            get
            {
                // 使用反射或者修改基类来访问target字段
                var targetField = typeof(BuffInstance).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return targetField?.GetValue(this) as GameObject;
            }
        }
    }
}