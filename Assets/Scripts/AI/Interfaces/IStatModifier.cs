using AI.Stats;

namespace AI.Interfaces
{
    public interface IStatModifier
    {
        string Id { get; }
        StatType TargetStat { get; }
        StatModifierType ModifierType { get; }
        float Value { get; }
        float Duration { get; }  // -1 for permanent
        bool IsExpired { get; }
        void Update(float deltaTime);
    }
}