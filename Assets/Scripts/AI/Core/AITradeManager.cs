using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AI.Stats;
using AI.Perception;
using AI.Decision;
using Inventory;
using Inventory.Items;
using Inventory.Managers;

namespace AI.Core
{
    /// <summary>
    /// AI间智能交易管理器 - 处理AI之间的物品交易
    /// </summary>
    public class AITradeManager : MonoBehaviour
    {
        [Header("Trade Settings")]
        [SerializeField] private float tradeRange = 3f;
        [SerializeField] private float tradeEvaluationInterval = 5f;
        [SerializeField] private float tradeRequestCooldown = 10f;
        [SerializeField] private bool debugTrade = true;
        
        [Header("Trade Preferences")]
        [SerializeField] private float desperationMultiplier = 2f;  // 急需时的交易倍率
        [SerializeField] private float fairnessThreshold = 0.8f;    // 交易公平性阈值
        [SerializeField] private float trustDecay = 0.95f;          // 信任度衰减
        
        // 组件引用
        private AIStats aiStats;
        private AIPerception perception;
        private Inventory.Inventory inventory;
        private CurrencyManager currencyManager;
        private AICommunicator communicator;
        private AIGoalSystem goalSystem;
        
        // 交易状态
        private float lastTradeEvaluation;
        private float lastTradeRequest;
        private bool isInTrade = false;
        private Dictionary<AITradeManager, float> trustLevels = new Dictionary<AITradeManager, float>();
        private List<AITradeOffer> pendingOffers = new List<AITradeOffer>();
        
        // 交易历史
        private List<AITradeRecord> tradeHistory = new List<AITradeRecord>();
        
        private void Awake()
        {
            aiStats = GetComponent<AIStats>();
            perception = GetComponent<AIPerception>();
            inventory = GetComponent<Inventory.Inventory>();
            currencyManager = GetComponent<CurrencyManager>();
            communicator = GetComponent<AICommunicator>();
            goalSystem = GetComponent<AIGoalSystem>();
        }
        
        private void Update()
        {
            // 定期评估交易机会
            if (Time.time - lastTradeEvaluation > tradeEvaluationInterval)
            {
                EvaluateTradeOpportunities();
                lastTradeEvaluation = Time.time;
            }
            
            // 处理等待中的交易提议
            ProcessPendingOffers();
            
            // 信任度衰减
            DecayTrustLevels();
        }
        
        /// <summary>
        /// 评估与附近AI的交易机会
        /// </summary>
        private void EvaluateTradeOpportunities()
        {
            if (isInTrade || Time.time - lastTradeRequest < tradeRequestCooldown)
                return;
                
            var nearbyAIs = FindNearbyTradeableAIs();
            
            foreach (var ai in nearbyAIs)
            {
                var tradeOffer = CreateTradeOffer(ai);
                if (tradeOffer != null && IsOfferWorthwhile(tradeOffer))
                {
                    ProposeTradeToAI(ai, tradeOffer);
                    lastTradeRequest = Time.time;
                    break; // 一次只提议一个交易
                }
            }
        }
        
        /// <summary>
        /// 寻找附近可交易的AI
        /// </summary>
        private List<AITradeManager> FindNearbyTradeableAIs()
        {
            var nearbyAIs = new List<AITradeManager>();
            var allAIs = FindObjectsOfType<AITradeManager>();
            
            foreach (var ai in allAIs)
            {
                if (ai != this && 
                    Vector2.Distance(transform.position, ai.transform.position) <= tradeRange &&
                    !ai.isInTrade &&
                    ai.aiStats != null && !ai.aiStats.IsDead)
                {
                    nearbyAIs.Add(ai);
                }
            }
            
            return nearbyAIs;
        }
        
        /// <summary>
        /// 创建对特定AI的交易提议
        /// </summary>
        private AITradeOffer CreateTradeOffer(AITradeManager targetAI)
        {
            var myNeeds = AnalyzeMyNeeds();
            var theirNeeds = AnalyzeTheirNeeds(targetAI);
            var myAssets = AnalyzeMyAssets();
            var theirAssets = AnalyzeTheirAssets(targetAI);
            
            // 寻找互补的交易机会
            var offer = FindMutuallyBeneficialTrade(myNeeds, theirNeeds, myAssets, theirAssets);
            
            if (offer != null)
            {
                offer.proposer = this;
                offer.target = targetAI;
                offer.proposalTime = Time.time;
                offer.trustLevel = GetTrustLevel(targetAI);
            }
            
            return offer;
        }
        
        /// <summary>
        /// 分析自己的需求
        /// </summary>
        private AITradeNeeds AnalyzeMyNeeds()
        {
            var needs = new AITradeNeeds();
            
            // 基于AI状态分析需求
            float healthRatio = aiStats.CurrentHealth / aiStats.Config.maxHealth;
            float hungerRatio = aiStats.CurrentHunger / aiStats.Config.maxHunger;
            float thirstRatio = aiStats.CurrentThirst / aiStats.Config.maxThirst;
            
            // 生存物品需求
            if (healthRatio < 0.4f)
                needs.healingItems = CalculateUrgency(healthRatio);
            if (hungerRatio < 0.4f)
                needs.foodItems = CalculateUrgency(hungerRatio);
            if (thirstRatio < 0.4f)
                needs.drinkItems = CalculateUrgency(thirstRatio);
            
            // 武器需求
            var currentWeapon = inventory.EquippedWeapon;
            if (currentWeapon == null || IsWeaponOutdated(currentWeapon))
                needs.weaponUpgrade = 0.7f;
            
            // 金币需求
            int currentGold = currencyManager?.CurrentGold ?? 0;
            if (currentGold < 50)
                needs.goldCoins = 0.8f;
            else if (currentGold < 200)
                needs.goldCoins = 0.4f;
            
            // 背包空间需求
            float inventoryFullness = GetInventoryFullness();
            if (inventoryFullness > 0.8f)
                needs.inventorySpace = 0.9f;
            
            return needs;
        }
        
        /// <summary>
        /// 分析目标AI的需求（基于观察）
        /// </summary>
        private AITradeNeeds AnalyzeTheirNeeds(AITradeManager targetAI)
        {
            var needs = new AITradeNeeds();
            var targetStats = targetAI.aiStats;
            
            if (targetStats == null) return needs;
            
            // 观察目标AI的状态
            float healthRatio = targetStats.CurrentHealth / targetStats.Config.maxHealth;
            float hungerRatio = targetStats.CurrentHunger / targetStats.Config.maxHunger;
            float thirstRatio = targetStats.CurrentThirst / targetStats.Config.maxThirst;
            
            // 推测需求
            if (healthRatio < 0.5f)
                needs.healingItems = CalculateUrgency(healthRatio);
            if (hungerRatio < 0.5f)
                needs.foodItems = CalculateUrgency(hungerRatio);
            if (thirstRatio < 0.5f)
                needs.drinkItems = CalculateUrgency(thirstRatio);
                
            // 基于目标AI的目标推测需求
            var targetGoalSystem = targetAI.goalSystem;
            if (targetGoalSystem != null)
            {
                if (targetGoalSystem.CurrentHighLevelGoal?.Type == GoalType.ResourceAccumulation)
                    needs.goldCoins = 0.6f;
                if (targetGoalSystem.CurrentMidLevelGoal?.Type == GoalType.Combat)
                    needs.weaponUpgrade = 0.5f;
            }
            
            return needs;
        }
        
        /// <summary>
        /// 分析自己的资产
        /// </summary>
        private AITradeAssets AnalyzeMyAssets()
        {
            var assets = new AITradeAssets();
            
            // 分析背包物品
            for (int i = 0; i < inventory.Size; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    var item = slot.Item;
                    int quantity = slot.Quantity;
                    
                    if (item is ConsumableItem consumable)
                    {
                        if (IsHealingItem(consumable))
                            assets.healingItems.Add(new TradeableItem(item, quantity, CalculateItemValue(item)));
                        else if (IsFoodItem(consumable))
                            assets.foodItems.Add(new TradeableItem(item, quantity, CalculateItemValue(item)));
                        else if (IsDrinkItem(consumable))
                            assets.drinkItems.Add(new TradeableItem(item, quantity, CalculateItemValue(item)));
                    }
                    else if (item is WeaponItem weapon)
                    {
                        assets.weapons.Add(new TradeableItem(item, quantity, CalculateItemValue(item)));
                    }
                }
            }
            
            // 金币资产
            assets.goldCoins = currencyManager?.CurrentGold ?? 0;
            
            return assets;
        }
        
        /// <summary>
        /// 分析目标AI的资产（基于观察和推测）
        /// </summary>
        private AITradeAssets AnalyzeTheirAssets(AITradeManager targetAI)
        {
            var assets = new AITradeAssets();
            
            // 观察装备的武器
            var targetInventory = targetAI.inventory;
            if (targetInventory?.EquippedWeapon != null)
            {
                assets.weapons.Add(new TradeableItem(
                    targetInventory.EquippedWeapon, 
                    1, 
                    CalculateItemValue(targetInventory.EquippedWeapon)
                ));
            }
            
            // 推测金币（基于行为和状态）
            var targetCurrency = targetAI.currencyManager;
            if (targetCurrency != null)
            {
                // 如果能观察到金币，直接使用
                assets.goldCoins = targetCurrency.CurrentGold;
            }
            
            // 推测背包物品（基于AI行为）
            // 如果目标AI最近使用了治疗物品，可能还有更多
            // 这里可以基于观察历史来推测
            
            return assets;
        }
        
        /// <summary>
        /// 寻找互利的交易
        /// </summary>
        private AITradeOffer FindMutuallyBeneficialTrade(
            AITradeNeeds myNeeds, 
            AITradeNeeds theirNeeds,
            AITradeAssets myAssets, 
            AITradeAssets theirAssets)
        {
            var offer = new AITradeOffer();
            
            // 尝试不同的交易组合
            
            // 1. 治疗物品交易
            if (myNeeds.healingItems > 0.5f && theirAssets.healingItems.Count > 0)
            {
                var bestHealing = theirAssets.healingItems.OrderByDescending(h => h.value).First();
                offer.wantedItems.Add(bestHealing);
                
                // 寻找合适的报酬
                var compensation = FindCompensation(bestHealing.value, myAssets, theirNeeds);
                if (compensation != null)
                {
                    offer.offeredItems.AddRange(compensation);
                }
            }
            
            // 2. 武器交易
            if (myNeeds.weaponUpgrade > 0.5f && theirAssets.weapons.Count > 0)
            {
                var betterWeapon = theirAssets.weapons.FirstOrDefault(w => 
                    IsWeaponBetter(w.item as WeaponItem, inventory.EquippedWeapon));
                    
                if (betterWeapon != null)
                {
                    offer.wantedItems.Add(betterWeapon);
                    
                    var compensation = FindCompensation(betterWeapon.value, myAssets, theirNeeds);
                    if (compensation != null)
                    {
                        offer.offeredItems.AddRange(compensation);
                    }
                }
            }
            
            // 3. 金币交易
            if (myNeeds.goldCoins > 0.5f && theirAssets.goldCoins > 50)
            {
                int wantedGold = Mathf.Min(theirAssets.goldCoins / 2, 100); // 最多要一半，不超过100
                offer.wantedGold = wantedGold;
                
                var compensation = FindCompensation(wantedGold, myAssets, theirNeeds);
                if (compensation != null)
                {
                    offer.offeredItems.AddRange(compensation);
                }
            }
            
            // 验证交易合理性
            if (offer.wantedItems.Count == 0 && offer.wantedGold == 0)
                return null;
                
            if (offer.offeredItems.Count == 0 && offer.offeredGold == 0)
                return null;
                
            // 计算交易价值比
            float wantedValue = CalculateOfferValue(offer.wantedItems) + offer.wantedGold;
            float offeredValue = CalculateOfferValue(offer.offeredItems) + offer.offeredGold;
            
            offer.valueRatio = offeredValue / Mathf.Max(wantedValue, 1f);
            
            return offer;
        }
        
        /// <summary>
        /// 寻找合适的补偿
        /// </summary>
        private List<TradeableItem> FindCompensation(float targetValue, AITradeAssets myAssets, AITradeNeeds theirNeeds)
        {
            var compensation = new List<TradeableItem>();
            float remainingValue = targetValue;
            
            // 优先提供对方需要的物品
            if (theirNeeds.healingItems > 0.3f)
            {
                var healingToOffer = myAssets.healingItems.Where(h => h.value <= remainingValue).ToList();
                foreach (var healing in healingToOffer)
                {
                    compensation.Add(healing);
                    remainingValue -= healing.value;
                    if (remainingValue <= 0) break;
                }
            }
            
            if (remainingValue > 0 && theirNeeds.goldCoins > 0.3f)
            {
                int goldToOffer = Mathf.Min((int)remainingValue, myAssets.goldCoins);
                if (goldToOffer > 0)
                {
                    // 金币通过特殊方式处理
                    remainingValue -= goldToOffer;
                }
            }
            
            // 如果还不够，提供其他有价值的物品
            if (remainingValue > 5f) // 允许小幅差异
            {
                var otherItems = myAssets.foodItems.Concat(myAssets.drinkItems)
                    .Where(item => item.value <= remainingValue * 1.2f)
                    .OrderBy(item => Mathf.Abs(item.value - remainingValue))
                    .Take(3);
                    
                compensation.AddRange(otherItems);
            }
            
            return compensation.Count > 0 ? compensation : null;
        }
        
        /// <summary>
        /// 向目标AI提议交易
        /// </summary>
        private void ProposeTradeToAI(AITradeManager targetAI, AITradeOffer offer)
        {
            if (debugTrade)
            {
                Debug.Log($"[AITrade] {name} 向 {targetAI.name} 提议交易: " +
                    $"想要 {offer.wantedItems.Count} 物品 + {offer.wantedGold} 金币, " +
                    $"提供 {offer.offeredItems.Count} 物品 + {offer.offeredGold} 金币");
            }
            
            targetAI.ReceiveTradeOffer(offer);
            
            // 通过通信系统发送交易请求
            if (communicator != null)
            {
                // 使用"到我这来"类型的消息，表示请求交易
                communicator.SendMessage(CommunicationType.ComeHere, targetAI.transform.position);
            }
        }
        
        /// <summary>
        /// 接收交易提议
        /// </summary>
        public void ReceiveTradeOffer(AITradeOffer offer)
        {
            pendingOffers.Add(offer);
            
            if (debugTrade)
            {
                Debug.Log($"[AITrade] {name} 收到来自 {offer.proposer.name} 的交易提议");
            }
        }
        
        /// <summary>
        /// 处理等待中的交易提议
        /// </summary>
        private void ProcessPendingOffers()
        {
            for (int i = pendingOffers.Count - 1; i >= 0; i--)
            {
                var offer = pendingOffers[i];
                
                // 超时的提议自动拒绝
                if (Time.time - offer.proposalTime > 30f)
                {
                    RejectTradeOffer(offer, "超时");
                    pendingOffers.RemoveAt(i);
                    continue;
                }
                
                // 评估交易提议
                var decision = EvaluateTradeOffer(offer);
                if (decision.accept)
                {
                    AcceptTradeOffer(offer);
                    pendingOffers.RemoveAt(i);
                }
                else if (decision.reject)
                {
                    RejectTradeOffer(offer, decision.reason);
                    pendingOffers.RemoveAt(i);
                }
                // 否则继续等待
            }
        }
        
        /// <summary>
        /// 评估交易提议
        /// </summary>
        private AITradeDecision EvaluateTradeOffer(AITradeOffer offer)
        {
            var decision = new AITradeDecision();
            
            // 基础可行性检查
            if (!CanAffordTrade(offer))
            {
                decision.reject = true;
                decision.reason = "无法承担交易成本";
                return decision;
            }
            
            // 计算交易价值
            float benefit = CalculateTradeBenefit(offer);
            float cost = CalculateTradeCost(offer);
            float trustMultiplier = GetTrustLevel(offer.proposer);
            
            // 考虑信任度调整
            benefit *= trustMultiplier;
            
            // 决策阈值
            float netBenefit = benefit - cost;
            
            if (netBenefit > 10f) // 明显有利
            {
                decision.accept = true;
                decision.reason = $"有利交易 (净收益: {netBenefit:F1})";
            }
            else if (netBenefit < -5f) // 明显不利
            {
                decision.reject = true;
                decision.reason = $"不利交易 (净损失: {-netBenefit:F1})";
            }
            else if (offer.valueRatio < fairnessThreshold) // 不公平
            {
                decision.reject = true;
                decision.reason = $"交易不公平 (比例: {offer.valueRatio:F2})";
            }
            // 否则继续考虑
            
            return decision;
        }
        
        /// <summary>
        /// 接受交易提议
        /// </summary>
        private void AcceptTradeOffer(AITradeOffer offer)
        {
            if (debugTrade)
            {
                Debug.Log($"[AITrade] {name} 接受了来自 {offer.proposer.name} 的交易");
            }
            
            // 执行交易
            ExecuteTrade(offer);
            
            // 更新信任度
            AdjustTrustLevel(offer.proposer, 0.1f);
            
            // 记录交易历史
            RecordTrade(offer, true);
            
            // 通知提议者
            offer.proposer.OnTradeAccepted(offer);
        }
        
        /// <summary>
        /// 拒绝交易提议
        /// </summary>
        private void RejectTradeOffer(AITradeOffer offer, string reason)
        {
            if (debugTrade)
            {
                Debug.Log($"[AITrade] {name} 拒绝了来自 {offer.proposer.name} 的交易: {reason}");
            }
            
            // 轻微降低信任度
            AdjustTrustLevel(offer.proposer, -0.02f);
            
            // 通知提议者
            offer.proposer.OnTradeRejected(offer, reason);
        }
        
        /// <summary>
        /// 执行交易
        /// </summary>
        private void ExecuteTrade(AITradeOffer offer)
        {
            isInTrade = true;
            
            try
            {
                // 转移物品给提议者
                foreach (var item in offer.wantedItems)
                {
                    TransferItemTo(offer.proposer, item.item, item.quantity);
                }
                
                // 转移金币给提议者
                if (offer.wantedGold > 0)
                {
                    TransferGoldTo(offer.proposer, offer.wantedGold);
                }
                
                // 从提议者接收物品
                foreach (var item in offer.offeredItems)
                {
                    offer.proposer.TransferItemTo(this, item.item, item.quantity);
                }
                
                // 从提议者接收金币
                if (offer.offeredGold > 0)
                {
                    offer.proposer.TransferGoldTo(this, offer.offeredGold);
                }
                
                if (debugTrade)
                {
                    Debug.Log($"[AITrade] 交易完成: {name} <-> {offer.proposer.name}");
                }
            }
            finally
            {
                isInTrade = false;
            }
        }
        
        /// <summary>
        /// 转移物品给目标AI
        /// </summary>
        private void TransferItemTo(AITradeManager target, ItemBase item, int quantity)
        {
            if (inventory.RemoveItem(item, quantity))
            {
                target.inventory.AddItem(item, quantity);
            }
        }
        
        /// <summary>
        /// 转移金币给目标AI
        /// </summary>
        private void TransferGoldTo(AITradeManager target, int amount)
        {
            if (currencyManager.SpendGold(amount))
            {
                target.currencyManager.AddGold(amount);
            }
        }
        
        // 辅助方法
        private float CalculateUrgency(float ratio)
        {
            return Mathf.Clamp01((1f - ratio) * desperationMultiplier);
        }
        
        private bool IsWeaponOutdated(WeaponItem weapon)
        {
            // 简单的武器过时检查
            return weapon.Damage < 20f; // 可以基于更复杂的逻辑
        }
        
        private bool IsWeaponBetter(WeaponItem weapon1, WeaponItem weapon2)
        {
            if (weapon2 == null) return true;
            return weapon1.Damage > weapon2.Damage;
        }
        
        private float GetInventoryFullness()
        {
            if (inventory == null) return 0f;
            
            int usedSlots = 0;
            for (int i = 0; i < inventory.Size; i++)
            {
                if (!inventory.GetSlot(i).IsEmpty)
                    usedSlots++;
            }
            return (float)usedSlots / inventory.Size;
        }
        
        private bool IsHealingItem(ConsumableItem item)
        {
            // 检查物品是否恢复健康
            return item.name.ToLower().Contains("heal") || 
                   item.name.ToLower().Contains("potion") ||
                   item.name.ToLower().Contains("medicine");
        }
        
        private bool IsFoodItem(ConsumableItem item)
        {
            return item.name.ToLower().Contains("food") || 
                   item.name.ToLower().Contains("bread") ||
                   item.name.ToLower().Contains("meat");
        }
        
        private bool IsDrinkItem(ConsumableItem item)
        {
            return item.name.ToLower().Contains("water") || 
                   item.name.ToLower().Contains("drink") ||
                   item.name.ToLower().Contains("juice");
        }
        
        private float CalculateItemValue(ItemBase item)
        {
            // 基础价值计算
            float baseValue = 10f;
            
            if (item is WeaponItem weapon)
            {
                baseValue = weapon.Damage * 2f;
            }
            else if (item is ConsumableItem consumable)
            {
                baseValue = 15f; // 消耗品基础价值
            }
            
            return baseValue;
        }
        
        private float CalculateOfferValue(List<TradeableItem> items)
        {
            return items.Sum(item => item.value * item.quantity);
        }
        
        private bool CanAffordTrade(AITradeOffer offer)
        {
            // 检查是否有足够的物品和金币
            foreach (var item in offer.wantedItems)
            {
                if (inventory.GetItemCount(item.item) < item.quantity)
                    return false;
            }
            
            if (offer.wantedGold > (currencyManager?.CurrentGold ?? 0))
                return false;
                
            return true;
        }
        
        private float CalculateTradeBenefit(AITradeOffer offer)
        {
            float benefit = 0f;
            var myNeeds = AnalyzeMyNeeds();
            
            // 计算获得物品的收益
            foreach (var item in offer.offeredItems)
            {
                if (item.item is ConsumableItem)
                {
                    if (IsHealingItem(item.item as ConsumableItem) && myNeeds.healingItems > 0.3f)
                        benefit += item.value * myNeeds.healingItems;
                    else if (IsFoodItem(item.item as ConsumableItem) && myNeeds.foodItems > 0.3f)
                        benefit += item.value * myNeeds.foodItems;
                    else if (IsDrinkItem(item.item as ConsumableItem) && myNeeds.drinkItems > 0.3f)
                        benefit += item.value * myNeeds.drinkItems;
                }
                else if (item.item is WeaponItem && myNeeds.weaponUpgrade > 0.3f)
                {
                    benefit += item.value * myNeeds.weaponUpgrade;
                }
            }
            
            // 金币收益
            if (myNeeds.goldCoins > 0.3f)
                benefit += offer.offeredGold * myNeeds.goldCoins;
            
            return benefit;
        }
        
        private float CalculateTradeCost(AITradeOffer offer)
        {
            float cost = 0f;
            
            // 失去物品的成本
            foreach (var item in offer.wantedItems)
            {
                cost += item.value;
            }
            
            cost += offer.wantedGold;
            
            return cost;
        }
        
        private float GetTrustLevel(AITradeManager other)
        {
            if (trustLevels.ContainsKey(other))
                return trustLevels[other];
                
            return 0.5f; // 默认中等信任
        }
        
        private void AdjustTrustLevel(AITradeManager other, float adjustment)
        {
            if (!trustLevels.ContainsKey(other))
                trustLevels[other] = 0.5f;
                
            trustLevels[other] = Mathf.Clamp01(trustLevels[other] + adjustment);
        }
        
        private void DecayTrustLevels()
        {
            var keys = trustLevels.Keys.ToList();
            foreach (var key in keys)
            {
                trustLevels[key] *= trustDecay;
            }
        }
        
        private bool IsOfferWorthwhile(AITradeOffer offer)
        {
            return offer.valueRatio >= fairnessThreshold && 
                   CalculateOfferValue(offer.offeredItems) + offer.offeredGold > 5f;
        }
        
        private void RecordTrade(AITradeOffer offer, bool successful)
        {
            tradeHistory.Add(new AITradeRecord
            {
                partner = offer.proposer,
                successful = successful,
                timestamp = Time.time,
                valueExchanged = CalculateOfferValue(offer.offeredItems) + offer.offeredGold
            });
            
            // 限制历史记录数量
            if (tradeHistory.Count > 50)
                tradeHistory.RemoveAt(0);
        }
        
        // 交易完成回调
        public void OnTradeAccepted(AITradeOffer offer)
        {
            if (debugTrade)
            {
                Debug.Log($"[AITrade] {name} 的交易提议被 {offer.target.name} 接受");
            }
            
            AdjustTrustLevel(offer.target, 0.1f);
            RecordTrade(offer, true);
        }
        
        public void OnTradeRejected(AITradeOffer offer, string reason)
        {
            if (debugTrade)
            {
                Debug.Log($"[AITrade] {name} 的交易提议被 {offer.target.name} 拒绝: {reason}");
            }
            
            RecordTrade(offer, false);
        }
        
        // 公共查询方法
        public bool IsTrading() => isInTrade;
        public int GetTradeHistoryCount() => tradeHistory.Count;
        public float GetTrustLevelWith(AITradeManager other) => GetTrustLevel(other);
    }
    
    // 数据结构
    [System.Serializable]
    public class AITradeNeeds
    {
        public float healingItems = 0f;
        public float foodItems = 0f;
        public float drinkItems = 0f;
        public float weaponUpgrade = 0f;
        public float goldCoins = 0f;
        public float inventorySpace = 0f;
    }
    
    [System.Serializable]
    public class AITradeAssets
    {
        public List<TradeableItem> healingItems = new List<TradeableItem>();
        public List<TradeableItem> foodItems = new List<TradeableItem>();
        public List<TradeableItem> drinkItems = new List<TradeableItem>();
        public List<TradeableItem> weapons = new List<TradeableItem>();
        public int goldCoins = 0;
    }
    
    [System.Serializable]
    public class TradeableItem
    {
        public ItemBase item;
        public int quantity;
        public float value;
        
        public TradeableItem(ItemBase item, int quantity, float value)
        {
            this.item = item;
            this.quantity = quantity;
            this.value = value;
        }
    }
    
    [System.Serializable]
    public class AITradeOffer
    {
        public AITradeManager proposer;
        public AITradeManager target;
        public List<TradeableItem> wantedItems = new List<TradeableItem>();
        public List<TradeableItem> offeredItems = new List<TradeableItem>();
        public int wantedGold = 0;
        public int offeredGold = 0;
        public float valueRatio = 1f;
        public float trustLevel = 0.5f;
        public float proposalTime;
    }
    
    [System.Serializable]
    public class AITradeDecision
    {
        public bool accept = false;
        public bool reject = false;
        public string reason = "";
    }
    
    [System.Serializable]
    public class AITradeRecord
    {
        public AITradeManager partner;
        public bool successful;
        public float timestamp;
        public float valueExchanged;
    }
}