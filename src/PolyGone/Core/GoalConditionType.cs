using System;

namespace PolyGone;

[Flags]
public enum GoalConditionType
{
    None = 0,
    AllEnemiesDefeated = 1 << 0,
}