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
            // Use InputManager so both keyboard (W) and gamepad (DPad Up / left-stick up) work.
            if (!PolyGone.InputManager.GameInteractHeld())
            {
                IsActivated = false;
            }
            if (IsTriggered && PolyGone.InputManager.GameInteract())
            {
                IsActivated = true;
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
