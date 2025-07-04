using UnityEngine;
using System.Collections.Generic;
using Inventory.Items;

namespace Loot
{
    /// <summary>
    /// 掉落物数据
    /// </summary>
    [System.Serializable]
    public class LootItem
    {
        public ItemBase item;
        public int minQuantity = 1;
        public int maxQuantity = 1;
        [Range(0f, 1f)]
        public float dropChance = 0.5f;
    }
    
    /// <summary>
    /// 掉落配置
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "Loot/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Header("注意：金币和弹药应该通过SpecialConsumable物品掉落")]
        [Header("此处只配置药水、武器、装备等物品掉落")]
        
        [Header("Item Drops")]
        [SerializeField] private List<LootItem> possibleItems = new List<LootItem>();
        
        [Header("Drop Settings")]
        [SerializeField] private int maxItemDrops = 2;
        [SerializeField] private float luckMultiplier = 1f;
        
        /// <summary>
        /// 生成掉落物（只掉落物品，不掉落金币弹药）
        /// </summary>
        public LootResult GenerateLoot(float luckBonus = 0f)
        {
            var result = new LootResult();
            float luck = luckMultiplier + luckBonus;
            
            // 物品掉落
            int itemDropCount = 0;
            foreach (var lootItem in possibleItems)
            {
                if (itemDropCount >= maxItemDrops) break;
                
                if (lootItem.item != null && Random.value <= lootItem.dropChance * luck)
                {
                    int quantity = Random.Range(lootItem.minQuantity, lootItem.maxQuantity + 1);
                    result.items.Add(new LootDrop { item = lootItem.item, quantity = quantity });
                    itemDropCount++;
                }
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 掉落结果（简化后只包含物品）
    /// </summary>
    public class LootResult
    {
        public List<LootDrop> items = new List<LootDrop>();
        
        public bool HasLoot()
        {
            return items.Count > 0;
        }
    }
    
    /// <summary>
    /// 单个掉落物
    /// </summary>
    public class LootDrop
    {
        public ItemBase item;
        public int quantity;
    }
}