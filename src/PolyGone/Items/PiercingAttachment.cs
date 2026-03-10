using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PolyGone.Weapons;

namespace PolyGone.Items
{
    /// <summary>
    /// Blaster attachment: all shots pierce through enemies instead of stopping on contact.
    /// </summary>
    public class PiercingAttachment : Item
    {
        public PiercingAttachment(Texture2D texture, Vector2 position, int[] size, Color color, Rectangle? srcRect = null)
            : base(texture, position, size, color, "Piercing Rounds", "Bullets pass through all enemies", srcRect) { }

        public override void Apply(PolyGone.Entities.Player player)
        {
            base.Apply(player);
            if (player.GetBlaster() is Blaster b)
                b.IsPiercing = true;
        }

        public override void Remove(PolyGone.Entities.Player player)
        {
            base.Remove(player);
            if (player.GetBlaster() is Blaster b)
                b.IsPiercing = false;
        }

        protected override Color GetActiveColor()   => new Color(140, 0, 200, 200);
        protected override Color GetInactiveColor() => new Color(70, 0, 100, 150);
    }
}
