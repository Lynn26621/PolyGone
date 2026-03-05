using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PolyGone.Items
{
    /// <summary>Increases the player's max dash charges from 1 to 2.</summary>
    public class DoubleDashItem : Item
    {
        public DoubleDashItem(Texture2D texture, Vector2 position, int[] size, Color color, Rectangle? srcRect = null)
            : base(texture, position, size, color, "Double Dash", "Store up to 2 dash charges", srcRect) { }

        public override void Apply(PolyGone.Entities.Player player)
        {
            base.Apply(player);
            player.MaxDashCharges = 2;
        }

        public override void Remove(PolyGone.Entities.Player player)
        {
            base.Remove(player);
            player.MaxDashCharges = 1;
        }

        protected override Color GetActiveColor()   => new Color(120, 255, 255, 200);
        protected override Color GetInactiveColor() => new Color(60, 127, 127, 150);
    }
}
