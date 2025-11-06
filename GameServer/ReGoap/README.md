# ReGoap Library

## Overview

This is a pure C# implementation of the ReGoap (Reactive Goal-Oriented Action Planning) library, ported from Unity to .NET 8.0 for the Caliburn project. ReGoap provides AI agents with goal-driven planning capabilities using A* pathfinding to generate optimal action sequences.

## Directory Structure

```
ReGoap/
├── Core/                           # Core GOAP classes
│   ├── ReGoapState.cs             # World state representation
│   ├── ReGoapMemory.cs            # Agent memory/blackboard
│   ├── IReGoapAction.cs           # Action interface
│   ├── ReGoapAction.cs            # Base action class
│   ├── IReGoapGoal.cs             # Goal interface
│   ├── ReGoapGoal.cs              # Base goal class
│   ├── IReGoapSensor.cs           # Sensor interface
│   ├── ReGoapSensor.cs            # Base sensor class
│   ├── IReGoapAgent.cs            # Agent interface
│   ├── ReGoapAgent.cs             # Base agent implementation
│   └── ReGoapPlanner.cs           # A* planner implementation
└── Manager/
    └── ReGoapPlannerManager.cs    # Centralized planning service
```

## Core Components

### ReGoapState
- Dictionary-based key-value storage for world states
- Supports preconditions, effects, and goal definitions
- Provides state comparison and difference calculation

### ReGoapMemory
- Blackboard pattern for agent world state
- Written to by sensors, read by actions and planner

### ReGoapAgent
- Owns goals, actions, sensors, and memory
- Manages plan execution
- Supports dynamic goal priorities

### ReGoapPlanner
- A* pathfinding algorithm for action planning
- Finds lowest-cost action sequence to achieve goal state
- Supports heuristic estimation and early exit conditions

### ReGoapPlannerManager
- Singleton service for centralized planning
- Thread-safe concurrent plan request handling
- Configurable processing time limits

## Usage Pattern

1. **Define Sensors**: Read game state, update agent memory
2. **Define Goals**: Specify desired world states with dynamic priorities
3. **Define Actions**: Implement state transitions with preconditions and effects
4. **Create Agent**: Register sensors, goals, and actions
5. **Execute**: Update sensors → Request plan → Execute actions

## Integration Notes

- **Namespace**: `DOL.GS.ReGoap.Core` and `DOL.GS.ReGoap.Manager`
- **Target Framework**: .NET 8.0
- **Thread Safety**: ReGoapPlannerManager is thread-safe for concurrent access
- **Generic Types**: Currently specialized for `<string, object>` in the manager

## Key Differences from Unity ReGoap

- Removed MonoBehaviour dependencies
- Replaced coroutines with callbacks
- Added thread-safe planner manager
- Pure C# implementation without Unity-specific features

## Performance Considerations

- Planning time configurable (default 100ms max per batch)
- Object pooling recommended for frequently allocated structures
- Sensor updates should reuse cached data where possible
- Action cost calculations should be lightweight

## Next Steps (Future Tasks)

The following integration work is planned in subsequent tasks:

1. **Sensor Framework**: Implement MimicNPC-specific sensors
2. **Role-Based Goals**: Define DAoC role goals (healer, tank, DPS, CC, puller)
3. **Class-Specific Actions**: Implement spell/ability actions per class
4. **MimicBrain Integration**: Replace FSM decision-making with ReGoap
5. **Configuration**: Enable/disable per class or globally

## References

- Original ReGoap: https://github.com/luxkun/ReGoap
- GOAP Theory: http://alumni.media.mit.edu/~jorkin/goap.html
- A* Pathfinding: https://en.wikipedia.org/wiki/A*_search_algorithm

---

**Status**: Task 1 Complete - ReGoap library source added to project structure
**Date**: 2025-11-05
**Compatibility**: .NET 8.0, compiles without errors
