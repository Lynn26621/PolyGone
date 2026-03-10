#if DEBUG
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PolyGone.Items
{
    /// <summary>DEV ONLY — Grants invincibility, instant kills, and infinite jumps.</summary>
    public class DevModeItem : Item
    {
        public DevModeItem(Texture2D texture, Vector2 position, int[] size, Color color, Rectangle? srcRect = null)
            : base(texture, position, size, color, "Dev Mode", "[DEV] Invincibility + Instant Kill + Infinite Jumps", srcRect) { }

        protected override Color GetActiveColor()   => new Color(255, 0, 255, 220);
        protected override Color GetInactiveColor() => new Color(127, 0, 127, 150);
    }
}
#endif
