using System;
using UnityEngine;
using UnityEngine.Events;
using AI.Stats;

namespace AI.Stats
{
    [Serializable]
    public class StatChangeEventArgs
    {
        public StatType statType;
        public float oldValue;
        public float newValue;
        public float changeAmount;
        public StatChangeReason reason;
        
        public StatChangeEventArgs(StatType statType, float oldValue, float newValue, StatChangeReason reason)
        {
            this.statType = statType;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.changeAmount = newValue - oldValue;
            this.reason = reason;
        }
    }
    
    [Serializable]
    public class MoodChangeEventArgs
    {
        public MoodDimension dimension;
        public float oldValue;
        public float newValue;
        public float changeAmount;
        
        public MoodChangeEventArgs(MoodDimension dimension, float oldValue, float newValue)
        {
            this.dimension = dimension;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.changeAmount = newValue - oldValue;
        }
    }
    
    [Serializable]
    public class AIDeathEventArgs
    {
        public StatType causeOfDeath;
        public Vector3 deathPosition;
        public float timeSurvived;
        
        public AIDeathEventArgs(StatType causeOfDeath, Vector3 deathPosition, float timeSurvived)
        {
            this.causeOfDeath = causeOfDeath;
            this.deathPosition = deathPosition;
            this.timeSurvived = timeSurvived;
        }
    }
    
    [Serializable]
    public class StatChangeEvent : UnityEvent<StatChangeEventArgs> { }
    
    [Serializable]
    public class MoodChangeEvent : UnityEvent<MoodChangeEventArgs> { }
    
    [Serializable]
    public class AIDeathEvent : UnityEvent<AIDeathEventArgs> { }
}