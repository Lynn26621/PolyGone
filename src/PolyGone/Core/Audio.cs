using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace PolyGone.Core
{
    public class Audio
    {
        private ContentManager contentManager;
        private SoundEffect effect;
        public Audio(ContentManager contentManager)
        {
            this.contentManager = contentManager;
        }
        public void PlaySfx(string sfx)
        {
            string path = "Audio/" + sfx;
            effect = contentManager.Load<SoundEffect>(path);
        }
    }
}
