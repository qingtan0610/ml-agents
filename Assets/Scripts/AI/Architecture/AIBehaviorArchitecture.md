# AI Behavior Architecture Design

## Overview
Based on Stanford's generative agents paper and modern game AI principles, we propose a hierarchical behavior system that combines:
1. **Memory Stream**: Comprehensive memory of experiences
2. **Reflection**: Periodic synthesis of memories into insights
3. **Planning**: Hierarchical goal planning with sub-goals
4. **Personality**: Individual traits affecting decisions
5. **Reinforcement Learning**: PPO with shaped rewards

## Core Components

### 1. Hierarchical Goal System
```
High-Level Goals (Strategic Layer):
├── Survival (Maintain health/hunger/thirst above 50%)
├── Progression (Find and activate portal)
├── Resource Accumulation (Gather gold/items)
└── Social Bonding (Reduce loneliness through interaction)

Mid-Level Goals (Tactical Layer):
├── Combat (Clear rooms, get loot)
├── Exploration (Discover new areas)
├── Trade (Buy needed items)
├── Communication (Share information)
└── Resource Management (Use items efficiently)

Low-Level Goals (Action Layer):
├── Move to target
├── Attack enemy
├── Pick up item
├── Use consumable
└── Interact with NPC/AI
```

### 2. Memory Architecture
```
Memory Types:
├── Short-term (Last 50 events)
├── Long-term (Important events)
├── Spatial (Map knowledge)
├── Social (AI relationships)
└── Strategic (Successful patterns)

Memory Importance Scoring:
- Combat victories: High
- Deaths: Very High
- Resource discoveries: Medium
- Social interactions: Medium
- Routine movements: Low
```

### 3. Reflection System
Every 100 steps or significant event:
- Analyze recent memories
- Extract patterns (e.g., "enemies are easier with ranged weapons")
- Update behavior preferences
- Adjust goal priorities

### 4. Personality Traits
Each AI has randomized traits:
- Aggression (0-1): Combat preference
- Curiosity (0-1): Exploration drive
- Sociability (0-1): Communication frequency
- Caution (0-1): Risk assessment
- Leadership (0-1): Decision influence

### 5. Enhanced Reward System

#### Combat Rewards
- Enemy damage dealt: +0.01 per damage
- Kill assist: +0.3
- Solo kill: +0.5
- Clear room: +1.0
- Take damage: -0.02 per damage

#### Exploration Rewards
- New room discovered: +0.5
- Find special room: +1.0
- Map coverage bonus: +0.001 per % explored

#### Social Rewards
- Successful communication: +0.2
- Help teammate: +0.5
- Coordinate portal activation: +2.0
- Reduce ally loneliness: +0.3

#### Resource Management
- Efficient item use: +0.3
- Trade success: +0.2
- Avoid waste: +0.1

### 6. Observation Enhancement
Add:
- Personality traits encoding
- Current goal encoding
- Team state summary
- Memory importance scores
- Reflection insights

### 7. Action Space Optimization
Reduce complexity:
- Group similar items
- Add macro actions (e.g., "go to nearest merchant")
- Context-sensitive action masking

## Implementation Plan

### Phase 1: Goal System
1. Create Goal class hierarchy
2. Implement goal selection based on state
3. Add goal-based rewards

### Phase 2: Memory Enhancement
1. Expand memory system with importance
2. Add reflection mechanism
3. Integrate memory into observations

### Phase 3: Personality System
1. Add personality traits
2. Modify heuristic based on traits
3. Include in observation space

### Phase 4: Reward Shaping
1. Implement detailed combat rewards
2. Add exploration incentives
3. Create social cooperation rewards

### Phase 5: Coordination
1. Add team goal sharing
2. Implement role assignment
3. Create coordination rewards

## Expected Improvements
1. **Purposeful Behavior**: Clear goal hierarchy prevents aimless wandering
2. **Combat Engagement**: Positive combat rewards encourage fighting
3. **Flexible Decision Making**: Personality and reflection create varied behaviors
4. **Team Coordination**: Social rewards promote cooperation
5. **Learning Efficiency**: Shaped rewards provide clearer feedback