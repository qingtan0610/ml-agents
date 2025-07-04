using UnityEngine;
using System;
using System.Collections.Generic;
using AI.Stats;
using AI.Config;

namespace AI.Stats
{
    [Serializable]
    public class AIMood
    {
        [SerializeField] private float emotion;    // -100 to 100
        [SerializeField] private float social;     // -100 to 100
        [SerializeField] private float mentality;  // -100 to 100
        
        private AIStatsConfig config;
        private float lastSocialInteractionTime;
        private float lastFaceToFaceTime;
        
        public float Emotion => emotion;
        public float Social => social;
        public float Mentality => mentality;
        
        public event Action<MoodChangeEventArgs> OnMoodChanged;
        
        public AIMood(AIStatsConfig config)
        {
            this.config = config;
            emotion = 50f;    // 中性开始
            social = 50f;
            mentality = 50f;
            lastSocialInteractionTime = Time.time;
            lastFaceToFaceTime = Time.time;
        }
        
        public void UpdateMood(Dictionary<StatType, float> currentStats, float deltaTime)
        {
            // 更新情绪维度（受生命、饥饿、口渴影响）
            UpdateEmotion(currentStats);
            
            // 更新社交维度（随时间衰减，交互增加）
            UpdateSocial(deltaTime);
            
            // 更新心态维度（受多个因素综合影响）
            UpdateMentality(currentStats);
        }
        
        private void UpdateEmotion(Dictionary<StatType, float> stats)
        {
            float oldEmotion = emotion;
            float targetEmotion = 50f; // 基础值
            
            // 生命值影响
            if (stats.ContainsKey(StatType.Health))
            {
                float healthPercent = stats[StatType.Health] / config.maxHealth;
                if (healthPercent < config.criticalHealthThreshold / 100f)
                {
                    targetEmotion -= 30f * config.healthMoodImpact;
                }
                else
                {
                    targetEmotion += (healthPercent - 0.5f) * 20f * config.healthMoodImpact;
                }
            }
            
            // 饥饿值影响
            if (stats.ContainsKey(StatType.Hunger))
            {
                float hungerPercent = stats[StatType.Hunger] / config.maxHunger;
                if (hungerPercent < config.criticalHungerThreshold / 100f)
                {
                    targetEmotion -= 20f * config.hungerMoodImpact;
                }
                else
                {
                    targetEmotion += (hungerPercent - 0.5f) * 15f * config.hungerMoodImpact;
                }
            }
            
            // 口渴值影响
            if (stats.ContainsKey(StatType.Thirst))
            {
                float thirstPercent = stats[StatType.Thirst] / config.maxThirst;
                if (thirstPercent < config.criticalThirstThreshold / 100f)
                {
                    targetEmotion -= 25f * config.thirstMoodImpact;
                }
                else
                {
                    targetEmotion += (thirstPercent - 0.5f) * 15f * config.thirstMoodImpact;
                }
            }
            
            // 平滑过渡
            emotion = Mathf.Lerp(emotion, Mathf.Clamp(targetEmotion, -100f, 100f), Time.deltaTime * 0.5f);
            
            if (Mathf.Abs(emotion - oldEmotion) > 0.1f)
            {
                OnMoodChanged?.Invoke(new MoodChangeEventArgs(MoodDimension.Emotion, oldEmotion, emotion));
            }
        }
        
        private void UpdateSocial(float deltaTime)
        {
            float oldSocial = social;
            
            // 社交值随时间衰减
            float timeSinceInteraction = Time.time - lastSocialInteractionTime;
            if (timeSinceInteraction > 60f) // 超过60秒没有交互
            {
                social -= config.socialDecayRate * deltaTime * 60f;
            }
            
            social = Mathf.Clamp(social, -100f, 100f);
            
            if (Mathf.Abs(social - oldSocial) > 0.1f)
            {
                OnMoodChanged?.Invoke(new MoodChangeEventArgs(MoodDimension.Social, oldSocial, social));
            }
        }
        
        private void UpdateMentality(Dictionary<StatType, float> stats)
        {
            float oldMentality = mentality;
            float targetMentality = 50f;
            
            // 多个属性告急会导致急躁
            int criticalCount = 0;
            
            if (stats.ContainsKey(StatType.Health) && stats[StatType.Health] < config.criticalHealthThreshold)
                criticalCount++;
            if (stats.ContainsKey(StatType.Hunger) && stats[StatType.Hunger] < config.criticalHungerThreshold)
                criticalCount++;
            if (stats.ContainsKey(StatType.Thirst) && stats[StatType.Thirst] < config.criticalThirstThreshold)
                criticalCount++;
            
            // 体力不足也会影响心态
            if (stats.ContainsKey(StatType.Stamina) && stats[StatType.Stamina] < 20f)
            {
                targetMentality -= 10f * config.staminaMoodImpact;
            }
            
            // 孤独也会导致急躁
            if (social < -50f)
            {
                targetMentality -= 20f;
            }
            
            // 根据告急数量调整心态
            targetMentality -= criticalCount * 20f;
            
            // 平滑过渡
            mentality = Mathf.Lerp(mentality, Mathf.Clamp(targetMentality, -100f, 100f), Time.deltaTime * 0.3f);
            
            if (Mathf.Abs(mentality - oldMentality) > 0.1f)
            {
                OnMoodChanged?.Invoke(new MoodChangeEventArgs(MoodDimension.Mentality, oldMentality, mentality));
            }
        }
        
        // 交互机通信（小幅度增加社交值）
        public void OnCommunicatorInteraction()
        {
            float oldSocial = social;
            social = Mathf.Min(social + 5f, 100f);
            lastSocialInteractionTime = Time.time;
            
            if (Mathf.Abs(social - oldSocial) > 0.1f)
            {
                OnMoodChanged?.Invoke(new MoodChangeEventArgs(MoodDimension.Social, oldSocial, social));
            }
        }
        
        // 面对面交流（大幅度增加社交值）
        public void OnFaceToFaceInteraction(bool isTalking)
        {
            float oldSocial = social;
            float increase = isTalking ? 20f : 15f; // 倾诉比倾听获得更多
            social = Mathf.Min(social + increase, 100f);
            lastSocialInteractionTime = Time.time;
            lastFaceToFaceTime = Time.time;
            
            if (Mathf.Abs(social - oldSocial) > 0.1f)
            {
                OnMoodChanged?.Invoke(new MoodChangeEventArgs(MoodDimension.Social, oldSocial, social));
            }
        }
        
        // 获取综合心情评分
        public float GetOverallMoodScore()
        {
            return (emotion + social + mentality) / 3f;
        }
        
        // 获取心情描述
        public string GetMoodDescription(MoodDimension dimension)
        {
            float value = 0;
            switch (dimension)
            {
                case MoodDimension.Emotion:
                    value = emotion;
                    return value < -50 ? "非常沮丧" : value < -20 ? "沮丧" : value < 20 ? "平静" : value < 50 ? "开心" : "非常开心";
                case MoodDimension.Social:
                    value = social;
                    return value < -50 ? "非常孤独" : value < -20 ? "孤独" : value < 20 ? "正常" : value < 50 ? "温暖" : "非常温暖";
                case MoodDimension.Mentality:
                    value = mentality;
                    return value < -50 ? "非常急躁" : value < -20 ? "急躁" : value < 20 ? "正常" : value < 50 ? "平静" : "非常平静";
                default:
                    return "未知";
            }
        }
        
        // 保存/加载心情数据
        public MoodData GetMoodData()
        {
            return new MoodData
            {
                emotion = this.emotion,
                social = this.social,
                mentality = this.mentality
            };
        }
        
        public void LoadMoodData(MoodData data)
        {
            emotion = data.emotion;
            social = data.social;
            mentality = data.mentality;
        }
    }
    
    [Serializable]
    public class MoodData
    {
        public float emotion;
        public float social;
        public float mentality;
    }
}