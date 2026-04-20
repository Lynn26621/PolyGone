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
        private KeyboardState keyboardState;
        private KeyboardState prevKeyboardState;
        private AudioManager audioManager;

        public LevelDoor(Vector2 position, int width, int height, AudioManager audioManager, string connectedLevel, int loadX, int loadY)
        : base(position, width, height, audioManager)
        {
            ConnectedLevel = connectedLevel;
            LoadX = loadX;
            LoadY = loadY;
            this.audioManager = audioManager;
        }
    }
}
