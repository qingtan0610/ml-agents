using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Inventory.Items;

namespace Inventory
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemBase> allItems = new List<ItemBase>();
        
        private Dictionary<string, ItemBase> itemLookup;
        
        public List<ItemBase> AllItems => allItems;
        
        private void OnEnable()
        {
            BuildLookupTable();
        }
        
        private void BuildLookupTable()
        {
            itemLookup = new Dictionary<string, ItemBase>();
            foreach (var item in allItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.ItemId))
                {
                    if (itemLookup.ContainsKey(item.ItemId))
                    {
                        Debug.LogWarning($"Duplicate item ID found: {item.ItemId}");
                    }
                    else
                    {
                        itemLookup[item.ItemId] = item;
                    }
                }
            }
        }
        
        public ItemBase GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            
            if (itemLookup == null)
            {
                BuildLookupTable();
            }
            
            itemLookup.TryGetValue(itemId, out ItemBase item);
            return item;
        }
        
        public List<ItemBase> GetItemsByType(ItemType type)
        {
            return allItems.Where(item => item != null && item.ItemType == type).ToList();
        }
        
        public List<ItemBase> GetItemsByRarity(ItemRarity rarity)
        {
            return allItems.Where(item => item != null && item.Rarity == rarity).ToList();
        }
        
        public List<ConsumableItem> GetConsumables()
        {
            return allItems.OfType<ConsumableItem>().ToList();
        }
        
        public List<WeaponItem> GetWeapons()
        {
            return allItems.OfType<WeaponItem>().ToList();
        }
        
        // Editor helpers
        [ContextMenu("Find All Items in Project")]
        private void FindAllItems()
        {
            allItems.Clear();
            
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemBase");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemBase item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemBase>(path);
                if (item != null)
                {
                    allItems.Add(item);
                }
            }
            
            allItems = allItems.OrderBy(i => i.ItemType).ThenBy(i => i.ItemName).ToList();
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Found {allItems.Count} items in project");
        }
        
        private void OnValidate()
        {
            // Remove null entries
            allItems.RemoveAll(item => item == null);
            
            // Remove duplicates
            var uniqueItems = new HashSet<ItemBase>();
            var cleanList = new List<ItemBase>();
            
            foreach (var item in allItems)
            {
                if (uniqueItems.Add(item))
                {
                    cleanList.Add(item);
                }
            }
            
            allItems = cleanList;
        }
    }
}