using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyGone.Core
{
    public class LevelDoor : SwitchTrigger
    {
        public string ConnectedLevel { get; private set; }
        public int LoadX;
        public int LoadY;
        public string? Requirement { get; private set; }

        public bool IsUnlocked => string.IsNullOrWhiteSpace(Requirement) || UnlockTracker.IsLevelCompleted(Requirement);

        public LevelDoor(Vector2 position, int width, int height, AudioManager audioManager, string connectedLevel, int loadX, int loadY, string? requirement = null)
        : base(position, width, height, audioManager)
        {
            ConnectedLevel = connectedLevel;
            LoadX = loadX;
            LoadY = loadY;
            Requirement = string.Equals(requirement, "none", StringComparison.OrdinalIgnoreCase) ? null : requirement;
        }
    }
}
