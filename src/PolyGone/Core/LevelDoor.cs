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
        public string Connects { get; private set; }
        public int LoadX;
        public int LoadY;
        public string? Requirement { get; private set; }
        public string? DisplayName { get; private set; }
        public virtual bool ChangesScene => true;

        public bool IsUnlocked => string.IsNullOrWhiteSpace(Requirement) || UnlockTracker.IsLevelCompleted(Requirement);

        public LevelDoor(Vector2 position, int width, int height, AudioManager audioManager, string connects, int loadX, int loadY, string? requirement = null, string? displayName = null)
        : base(position, width, height, audioManager)
        {
            this.Connects = connects;
            LoadX = loadX;
            LoadY = loadY;
            Requirement = string.Equals(requirement, "none", StringComparison.OrdinalIgnoreCase) ? null : requirement;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }
    }
}
