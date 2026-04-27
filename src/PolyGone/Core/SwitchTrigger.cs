using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyGone.Core
{
    public class SwitchTrigger : PolyGone.Trigger
    {
        public bool IsTriggered { get; private set; }
        public bool IsActivated { get; private set; }
        private KeyboardState keyboardState;
        private KeyboardState prevKeyboardState;
        private AudioManager audioManager;
        public SwitchTrigger(Vector2 position, int width, int height, AudioManager audioManager)
        : base(position, width, height)
        {
            IsTriggered = false;
            IsActivated = false;
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
                if (keyboardState.IsKeyDown(Keys.W) && prevKeyboardState.IsKeyUp(Keys.W))
                {
                    IsActivated = true;
                }
            }
            prevKeyboardState = Keyboard.GetState();
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
