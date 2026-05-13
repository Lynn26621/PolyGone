using Microsoft.Xna.Framework;

namespace PolyGone.Core
{
    public class GoalTrigger : SwitchTrigger
    {
        public GoalTrigger(Vector2 position, int width, int height, AudioManager audioManager)
            : base(position, width, height, audioManager)
        {
        }
    }
}