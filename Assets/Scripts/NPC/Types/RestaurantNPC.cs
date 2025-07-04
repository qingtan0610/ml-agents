using UnityEngine;
using NPC.Core;
using NPC.Data;
using NPC.Interfaces;
using AI.Stats;
using System.Collections;
using System.Collections.Generic;
using Inventory.Managers;

namespace NPC.Types
{
    public class RestaurantNPC : NPCBase, IServiceProvider
    {
        [Header("Restaurant Settings")]
        [SerializeField] private GameObject menuUIPrefab;
        
        private GameObject currentMenuUI;
        private Coroutine eatingCoroutine;
        private GameObject currentCustomer;
        private RestaurantData restaurantData => npcData as RestaurantData;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 验证数据类型
            if (npcData != null && !(npcData is RestaurantData))
            {
                Debug.LogError($"RestaurantNPC requires RestaurantData, but got {npcData.GetType().Name}");
            }
        }
        
        protected override void OnInteractionStarted(GameObject interactor)
        {
            ShowMenu(interactor);
        }
        
        protected override void OnInteractionEnded()
        {
            CloseMenu();
            if (eatingCoroutine != null)
            {
                StopCoroutine(eatingCoroutine);
                eatingCoroutine = null;
            }
        }
        
        // IServiceProvider 实现
        public void ProvideService(GameObject customer, string serviceId)
        {
            if (restaurantData == null)
            {
                Debug.LogError("RestaurantNPC: No restaurant data assigned!");
                return;
            }
            
            // 处理免费水服务
            if (serviceId == "free_water" && restaurantData.provideFreeWater)
            {
                ProvideFreeWater(customer);
                return;
            }
            
            // 查找菜单项
            FoodMenuItem menuItem = FindMenuItem(serviceId);
            if (menuItem != null)
            {
                OrderFood(customer, menuItem);
            }
        }
        
        public bool CanProvideService(GameObject customer, string serviceId)
        {
            if (restaurantData == null) return false;
            
            // 免费水服务
            if (serviceId == "free_water" && restaurantData.provideFreeWater)
                return true;
            
            // 检查是否能负担菜品
            FoodMenuItem menuItem = FindMenuItem(serviceId);
            if (menuItem == null) return false;
            
            var currencyManager = customer.GetComponent<CurrencyManager>();
            if (currencyManager == null) return false;
            
            return currencyManager.CanAfford(menuItem.price);
        }
        
        public int GetServiceCost(string serviceId)
        {
            // 免费水
            if (serviceId == "free_water") return 0;
            
            // 查找菜品价格
            FoodMenuItem menuItem = FindMenuItem(serviceId);
            return menuItem?.price ?? 0;
        }
        
        public string GetServiceDescription(string serviceId)
        {
            if (serviceId == "free_water")
                return "免费的清水";
            
            FoodMenuItem menuItem = FindMenuItem(serviceId);
            return menuItem?.description ?? "未知服务";
        }
        
        // 餐厅特有方法
        private void ShowMenu(GameObject customer)
        {
            currentCustomer = customer;
            ShowDialogue(restaurantData.welcomeText);
            
            // TODO: 创建菜单UI
            Debug.Log($"=== {restaurantData.npcName}的菜单 ===");
            DisplayMenu();
        }
        
        private void DisplayMenu()
        {
            // 显示免费水选项
            if (restaurantData.provideFreeWater)
            {
                Debug.Log($"[免费] 清水 - 恢复 {restaurantData.waterRestoreAmount} 口渴值");
            }
            
            // 显示菜单
            for (int i = 0; i < restaurantData.menu.Count; i++)
            {
                var item = restaurantData.menu[i];
                string specialTag = item.isSpecialDish ? " [特色菜]" : "";
                string effects = GetEffectsDescription(item);
                
                Debug.Log($"{item.itemName} - {item.price}金币{specialTag}");
                Debug.Log($"  {item.description}");
                Debug.Log($"  效果: {effects}");
            }
        }
        
        private void OrderFood(GameObject customer, FoodMenuItem menuItem)
        {
            var currencyManager = customer.GetComponent<CurrencyManager>();
            var aiStats = customer.GetComponent<AIStats>();
            
            if (currencyManager == null || aiStats == null)
            {
                Debug.LogError("Customer missing required components!");
                return;
            }
            
            // 检查金币
            if (!currencyManager.SpendGold(menuItem.price))
            {
                ShowDialogue("您的金币不足。");
                return;
            }
            
            // 开始用餐
            ShowDialogue(restaurantData.orderText);
            
            if (eatingCoroutine != null)
            {
                StopCoroutine(eatingCoroutine);
            }
            eatingCoroutine = StartCoroutine(ServeFoodCoroutine(customer, menuItem));
        }
        
        private void ProvideFreeWater(GameObject customer)
        {
            var aiStats = customer.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            ShowDialogue("这是您的水，不收费。");
            
            // 恢复口渴值
            aiStats.ModifyStat(StatType.Thirst, restaurantData.waterRestoreAmount, StatChangeReason.Item);
            
            // 播放音效
            if (restaurantData.interactionSound != null)
            {
                AudioSource.PlayClipAtPoint(restaurantData.interactionSound, transform.position);
            }
        }
        
        private IEnumerator ServeFoodCoroutine(GameObject customer, FoodMenuItem menuItem)
        {
            // 等待一小段时间模拟准备
            yield return new WaitForSeconds(1f);
            
            ShowDialogue(restaurantData.servingText);
            
            // 播放用餐动画（如果有）
            // TODO: 触发用餐动画
            
            // 等待用餐时间
            yield return new WaitForSeconds(restaurantData.eatingDuration);
            
            // 应用食物效果
            ApplyFoodEffects(customer, menuItem);
            
            ShowDialogue(restaurantData.thankYouText);
            
            // 播放完成音效
            if (restaurantData.interactionSound != null)
            {
                AudioSource.PlayClipAtPoint(restaurantData.interactionSound, transform.position);
            }
            
            eatingCoroutine = null;
        }
        
        private void ApplyFoodEffects(GameObject customer, FoodMenuItem menuItem)
        {
            var aiStats = customer.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            // 应用基础效果
            if (menuItem.hungerRestore > 0)
                aiStats.ModifyStat(StatType.Hunger, menuItem.hungerRestore, StatChangeReason.Item);
            
            if (menuItem.thirstRestore > 0)
                aiStats.ModifyStat(StatType.Thirst, menuItem.thirstRestore, StatChangeReason.Item);
            
            if (menuItem.healthRestore > 0)
                aiStats.ModifyStat(StatType.Health, menuItem.healthRestore, StatChangeReason.Item);
            
            if (menuItem.staminaRestore > 0)
                aiStats.ModifyStat(StatType.Stamina, menuItem.staminaRestore, StatChangeReason.Item);
            
            // 应用buff效果
            if (menuItem.hasBuffEffect && menuItem.buffData != null)
            {
                ApplyFoodBuff(customer, menuItem.buffData);
            }
        }
        
        private void ApplyFoodBuff(GameObject customer, BuffData buffData)
        {
            var aiStats = customer.GetComponent<AIStats>();
            if (aiStats == null) return;
            
            // 创建buff修饰器
            var modifier = new StatModifier(
                $"food_buff_{buffData.buffName}_{Time.time}",
                buffData.affectedStat,
                buffData.modifierType,
                buffData.effectValue,
                buffData.duration
            );
            
            aiStats.AddModifier(modifier);
            Debug.Log($"Applied buff: {buffData.buffName} for {buffData.duration} seconds");
        }
        
        private FoodMenuItem FindMenuItem(string itemName)
        {
            if (restaurantData == null || restaurantData.menu == null) return null;
            
            return restaurantData.menu.Find(item => item.itemName == itemName);
        }
        
        private string GetEffectsDescription(FoodMenuItem item)
        {
            List<string> effects = new List<string>();
            
            if (item.hungerRestore > 0)
                effects.Add($"饥饿+{item.hungerRestore}");
            if (item.thirstRestore > 0)
                effects.Add($"口渴+{item.thirstRestore}");
            if (item.healthRestore > 0)
                effects.Add($"生命+{item.healthRestore}");
            if (item.staminaRestore > 0)
                effects.Add($"体力+{item.staminaRestore}");
            
            if (item.hasBuffEffect && item.buffData != null)
                effects.Add($"{item.buffData.buffName}({item.buffData.duration}秒)");
            
            return effects.Count > 0 ? string.Join(", ", effects) : "无特殊效果";
        }
        
        private void CloseMenu()
        {
            currentCustomer = null;
            if (currentMenuUI != null)
            {
                Destroy(currentMenuUI);
                currentMenuUI = null;
            }
        }
        
        // 特殊菜品推荐
        public FoodMenuItem GetDailySpecial()
        {
            if (restaurantData == null || restaurantData.menu.Count == 0) return null;
            
            // 找出所有特色菜
            var specials = restaurantData.menu.FindAll(item => item.isSpecialDish);
            if (specials.Count > 0)
            {
                return specials[Random.Range(0, specials.Count)];
            }
            
            // 如果没有特色菜，随机返回一个
            return restaurantData.menu[Random.Range(0, restaurantData.menu.Count)];
        }
    }
}