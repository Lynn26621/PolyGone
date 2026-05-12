using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyGone.Core
{
    public class SubDoor : LevelDoor
    {
        public override bool ChangesScene => false;

        public SubDoor(Vector2 position, int width, int height, AudioManager audioManager, string connects, int loadX, int loadY, string? requirement = null, string? displayName = null)
            : base(position, width, height, audioManager, connects, loadX, loadY, requirement, displayName)
        {
        }
    }
}
