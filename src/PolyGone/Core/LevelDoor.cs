using Microsoft.Xna.Framework;
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
        public bool DoorEntered { get; private set; }
        public string ConnectedLevel { get; private set; }

        public LevelDoor(Vector2 position, int width, int height, string connectedLevel)
        : base(position, width, height)
        {
            IsTriggered = false;
            DoorEntered = false;
            ConnectedLevel = connectedLevel;
        }

        public void CheckTrigger(Rectangle playerBounds)
        {
            if (!IsTriggered && IsTriggeredBy(playerBounds))
            {
                IsTriggered = true;
                DoorEntered = true;
            }
        }

        public void Reset()
        {
            IsTriggered = false;
            DoorEntered = false;
        }
    }
}
