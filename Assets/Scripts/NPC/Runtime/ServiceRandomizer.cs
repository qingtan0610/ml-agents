using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NPC.Runtime
{
    /// <summary>
    /// NPC服务随机化器 - 从配置池中随机选择服务
    /// </summary>
    public static class ServiceRandomizer
    {
        /// <summary>
        /// 从列表中随机选择指定数量的元素
        /// </summary>
        public static List<T> RandomSelect<T>(List<T> source, int count, float? seed = null)
        {
            if (source == null || source.Count == 0) return new List<T>();
            
            // 如果请求数量大于等于源数量，返回打乱顺序的完整列表
            if (count >= source.Count)
            {
                return ShuffleList(source, seed);
            }
            
            // 使用种子确保同一位置的NPC选择相同
            Random.State originalState = Random.state;
            if (seed.HasValue)
            {
                Random.InitState(Mathf.RoundToInt(seed.Value));
            }
            
            // Fisher-Yates 洗牌算法选择
            var indices = Enumerable.Range(0, source.Count).ToList();
            var selected = new List<T>();
            
            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(i, indices.Count);
                int temp = indices[i];
                indices[i] = indices[randomIndex];
                indices[randomIndex] = temp;
                
                selected.Add(source[indices[i]]);
            }
            
            // 恢复随机状态
            Random.state = originalState;
            
            return selected;
        }
        
        /// <summary>
        /// 根据权重随机选择
        /// </summary>
        public static List<T> WeightedRandomSelect<T>(List<(T item, float weight)> weightedSource, int count, float? seed = null)
        {
            if (weightedSource == null || weightedSource.Count == 0) return new List<T>();
            
            Random.State originalState = Random.state;
            if (seed.HasValue)
            {
                Random.InitState(Mathf.RoundToInt(seed.Value));
            }
            
            var selected = new List<T>();
            var availableItems = new List<(T item, float weight)>(weightedSource);
            
            for (int i = 0; i < count && availableItems.Count > 0; i++)
            {
                float totalWeight = availableItems.Sum(x => x.weight);
                float randomValue = Random.Range(0f, totalWeight);
                float currentWeight = 0f;
                
                for (int j = 0; j < availableItems.Count; j++)
                {
                    currentWeight += availableItems[j].weight;
                    if (randomValue <= currentWeight)
                    {
                        selected.Add(availableItems[j].item);
                        availableItems.RemoveAt(j);
                        break;
                    }
                }
            }
            
            Random.state = originalState;
            return selected;
        }
        
        /// <summary>
        /// 获取随机数量（在最小和最大值之间）
        /// </summary>
        public static int GetRandomCount(int min, int max, float? seed = null)
        {
            if (min >= max) return min;
            
            Random.State originalState = Random.state;
            if (seed.HasValue)
            {
                Random.InitState(Mathf.RoundToInt(seed.Value));
            }
            
            int result = Random.Range(min, max + 1);
            Random.state = originalState;
            
            return result;
        }
        
        /// <summary>
        /// 打乱列表顺序
        /// </summary>
        private static List<T> ShuffleList<T>(List<T> source, float? seed = null)
        {
            var shuffled = new List<T>(source);
            
            Random.State originalState = Random.state;
            if (seed.HasValue)
            {
                Random.InitState(Mathf.RoundToInt(seed.Value));
            }
            
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                T temp = shuffled[i];
                shuffled[i] = shuffled[randomIndex];
                shuffled[randomIndex] = temp;
            }
            
            Random.state = originalState;
            return shuffled;
        }
    }
    
    /// <summary>
    /// NPC服务随机化配置
    /// </summary>
    [System.Serializable]
    public class ServiceRandomConfig
    {
        [Header("商品/服务数量")]
        public int minItems = 3;
        public int maxItems = 6;
        
        [Header("随机选择设置")]
        public bool usePositionAsSeed = true; // 使用位置作为种子，确保同位置NPC一致
        public float customSeed = 0f; // 自定义种子
        
        public float GetSeed(Vector3 position)
        {
            return usePositionAsSeed ? (position.x * 1000f + position.y) : customSeed;
        }
    }
}