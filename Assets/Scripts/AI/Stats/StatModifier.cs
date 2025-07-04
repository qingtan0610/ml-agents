using System;
using AI.Stats;
using AI.Interfaces;

namespace AI.Stats
{
    [Serializable]
    public class StatModifier : IStatModifier
    {
        private string id;
        private StatType targetStat;
        private StatModifierType modifierType;
        private float value;
        private float duration;
        private float remainingTime;
        
        public string Id => id;
        public StatType TargetStat => targetStat;
        public StatModifierType ModifierType => modifierType;
        public float Value => value;
        public float Duration => duration;
        public bool IsExpired => duration > 0 && remainingTime <= 0;
        
        public StatModifier(string id, StatType targetStat, StatModifierType modifierType, 
                          float value, float duration = -1)
        {
            this.id = id;
            this.targetStat = targetStat;
            this.modifierType = modifierType;
            this.value = value;
            this.duration = duration;
            this.remainingTime = duration;
        }
        
        public void Update(float deltaTime)
        {
            if (duration > 0)
            {
                remainingTime -= deltaTime;
            }
        }
        
        public float GetRemainingTime()
        {
            return remainingTime;
        }
    }
}