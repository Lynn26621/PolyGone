using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PolyGone.Core;
using PolyGone.Entities;
using System.Collections.Generic;

namespace PolyGone;

/// <summary>
/// A stationary enemy that aims and fires a blaster at the player.
/// </summary>
class TurretEnemy : Enemy
{
    private readonly Player player;
    private AudioManager audioManager;
    private float shootCooldown = 0f;
    private const float SHOOT_COOLDOWN = 120f; // 2 seconds at 60 fps
    private const float SHOOT_RANGE = 700f;    // Max firing range in pixels
    private const float BULLET_SPEED = 500f;
    private const int BULLET_DAMAGE = 20;

    /// <summary>Projectiles fired by this turret (flagged as Owner.Enemy).</summary>
    public readonly List<Projectile> Bullets = new();

    public TurretEnemy(
        Texture2D texture,
        Vector2 position,
        AudioManager audioManager,
        int[] size,
        Player player,
        int health = 80,
        Color color = default,
        Rectangle? srcRect = null,
        Dictionary<Vector2, int>? collisionMap = null,
        int[]? visualSize = null)
        : base(texture, position, audioManager, size, health, color, srcRect, collisionMap, patrolSpeed: 0f, visualSize: visualSize)
    {
        this.player = player;
        this.audioManager = audioManager;
    }

    private void ShootAtPlayer()
    {
        Vector2 myCenter = new Vector2(position.X + size[0] / 2f, position.Y + size[1] / 2f);
        Vector2 playerCenter = new Vector2(player.position.X + player.size[0] / 2f, player.position.Y + player.size[1] / 2f);
        Vector2 direction = playerCenter - myCenter;
        float distance = direction.Length();

        // Only shoot if the player is within range
        if (distance > SHOOT_RANGE || distance == 0f)
        {
            return;
        }

        direction.Normalize();

        Bullets.Add(new Projectile(
            texture: texture,
            position: new Vector2(myCenter.X - 5f, myCenter.Y - 5f),
            audioManager: audioManager,
            size: new int[2] { 10, 10 },
            lifetime: 200f,
            health: 1,
            damage: BULLET_DAMAGE,
            color: Color.OrangeRed,
            xSpeed: direction.X * BULLET_SPEED,
            ySpeed: direction.Y * BULLET_SPEED,
            owner: Owner.Enemy,
            srcRect: srcRect,
            collisionMap: CollisionMap
        ));

        audioManager.PlayAudio("shootSfx", true, "null", false); //Play shoot sound effect

    }

    public override void Update(GameTime gameTime)
    {
        // Count down and fire
        if (shootCooldown > 0f)
        {
            shootCooldown -= 1f;
        }
        else
        {
            ShootAtPlayer();
            shootCooldown = SHOOT_COOLDOWN;
        }

        // Advance own bullets and prune expired ones
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            Bullets[i].Update(gameTime);
            if (Bullets[i].Lifetime <= 0)
            {
                Bullets.RemoveAt(i);
            }
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch, Vector2 offset)
    {
        base.Draw(spriteBatch, offset);

        foreach (var bullet in Bullets)
        {
            bullet.Draw(spriteBatch, offset);
        }
    }
}
