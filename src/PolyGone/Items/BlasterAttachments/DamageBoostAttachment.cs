using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PolyGone.Items
{
    /// <summary>
    /// Blaster attachment: increases all bullet damage by 50%.
    /// </summary>
    public class DamageBoostAttachment : Item
    {
        private const float Multiplier = 1.5f;

        public DamageBoostAttachment(Texture2D texture, Vector2 position, int[] size, Color color, Rectangle? srcRect = null)
            : base(texture, position, size, color, "Damage Amp", "+50% bullet damage on every shot", srcRect) { }

        public override void Apply(PolyGone.Entities.Player player)
        {
            base.Apply(player);
            if (player.GetBlaster() is Blaster b)
                b.DamageMultiplier *= Multiplier;
        }

        public override void Remove(PolyGone.Entities.Player player)
        {
            base.Remove(player);
            if (player.GetBlaster() is Blaster b)
                b.DamageMultiplier /= Multiplier;
        }

        protected override Color GetActiveColor()   => new Color(255, 60, 0, 200);
        protected override Color GetInactiveColor() => new Color(127, 30, 0, 150);
    }
}
