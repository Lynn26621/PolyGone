using System;
using Microsoft.Xna.Framework;

namespace PolyGone;

public class GoalTrigger : Trigger
{
    public bool IsTriggered { get; private set; }
    public GoalConditionType RequiredConditions { get; }
    
    public GoalTrigger(Vector2 position, int width, int height, GoalConditionType requiredConditions = GoalConditionType.None) 
        : base(position, width, height)
    {
        IsTriggered = false;
        RequiredConditions = requiredConditions;
    }

    public bool CanTrigger(Func<GoalConditionType, bool> conditionEvaluator)
    {
        foreach (GoalConditionType condition in Enum.GetValues<GoalConditionType>())
        {
            if (condition == GoalConditionType.None)
            {
                continue;
            }

            if (RequiredConditions.HasFlag(condition) && !conditionEvaluator(condition))
            {
                return false;
            }
        }

        return true;
    }
    
    public void CheckTrigger(Rectangle playerBounds)
    {
        if (!IsTriggered && IsTriggeredBy(playerBounds))
        {
            IsTriggered = true;
        }
    }
    
    public void Reset()
    {
        IsTriggered = false;
    }
}