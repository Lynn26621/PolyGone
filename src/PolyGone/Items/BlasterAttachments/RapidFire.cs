using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PolyGone.Items
{
    /// <summary>Reduces the weapon cooldown to 1/2 of its base value and enables hold-to-fire.</summary>
    public class RapidFireItem : Item
    {
        private const float CooldownMultiplier = 0.5f;

        public RapidFireItem(Texture2D texture, Vector2 position, int[] size, Color color, Rectangle? srcRect = null)
            : base(texture, position, size, color, "Rapid Fire", "Halves weapon cooldown, hold to fire", srcRect) { }

        public override void Apply(PolyGone.Entities.Player player)
        {
            base.Apply(player);
            if (player.GetBlaster() is Blaster b)
            {
                b.CooldownMultiplier *= CooldownMultiplier;
                b.IsAutoFire = true;
            }
        }

        public override void Remove(PolyGone.Entities.Player player)
        {
            base.Remove(player);
            if (player.GetBlaster() is Blaster b)
            {
                b.CooldownMultiplier /= CooldownMultiplier; // undo the reduction
                b.IsAutoFire = false;
            }
        }

        protected override Color GetActiveColor()   => new Color(255, 140, 0, 200);
        protected override Color GetInactiveColor() => new Color(127, 70, 0, 150);
    }
}
