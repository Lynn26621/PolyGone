using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyGone.Core
{
    public class LevelDoor : Trigger
    {
        public bool IsTriggered { get; private set; }
        public bool IsActivated { get; private set; }
        public string ConnectedLevel { get; private set; }
        public string LocatedLevel { get; private set; }
        private KeyboardState keyboardState;
        private AudioManager audioManager;

        public LevelDoor(Vector2 position, int width, int height, string connectedLevel, string locatedLevel, AudioManager audioManager)
        : base(position, width, height)
        {
            IsTriggered = false;
            IsActivated = false;
            ConnectedLevel = connectedLevel;
            LocatedLevel = locatedLevel;
            this.audioManager = audioManager;
        }

        public void CheckTrigger(Rectangle playerBounds)
        {
            if (!IsTriggered && IsTriggeredBy(playerBounds))
            {
                IsTriggered = true;
            }
            else if (IsTriggered && !IsTriggeredBy(playerBounds))
            {
                IsTriggered = false;
            }
        }

        public void HandleInput()
        {
            keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyUp(Keys.W))
            {
                IsActivated = false;
            }
            if (IsTriggered)
            {
                if (keyboardState.IsKeyDown(Keys.W))
                {
                    IsActivated = true;
                }
            }
        }

        public void Reset()
        {
            IsTriggered = false;
            IsActivated = false;
        }

        public void Update()
        {
            HandleInput();
        }
    }
}
