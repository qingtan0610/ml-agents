using UnityEngine;
using AI.Stats;

namespace Buffs.Types
{
    [CreateAssetMenu(fileName = "StunDebuff", menuName = "Buffs/Stun Debuff")]
    public class StunDebuff : BuffBase
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            
            // 设置基础属性
            buffName = "眩晕";
            description = "无法移动和攻击";
            buffType = BuffType.Debuff;
            duration = 2f;
            isPermanent = false;
            isStackable = false;
            buffColor = Color.yellow;
            
            // 眩晕没有属性修改，而是通过BuffManager检查实现
            effects.Clear();
        }
        
        public override BuffInstance CreateInstance(GameObject target)
        {
            var instance = base.CreateInstance(target);
            
            // 停止目标移动
            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            
            // 禁用控制
            var controller = target.GetComponent<Player.PlayerController2D>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            
            return instance;
        }
    }
    
    // 自定义眩晕实例
    public class StunBuffInstance : BuffInstance
    {
        private Player.PlayerController2D controller;
        
        public StunBuffInstance(BuffBase data, GameObject target) : base(data, target)
        {
            controller = target.GetComponent<Player.PlayerController2D>();
        }
        
        public new void Remove()
        {
            base.Remove();
            
            // 恢复控制
            if (controller != null)
            {
                controller.enabled = true;
            }
        }
    }
}