using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PolyGone.Core;
using System.Collections.Generic;

namespace PolyGone;

class Enemy : Entity
{
    private readonly float patrolSpeed;
    private float patrolDirection = 1f; // 1 for right, -1 for left
    private AudioManager audioManager;

    // Multi-hit damage system
    private float damageWindow = 0f; // Frames remaining in damage window
    private int accumulatedDamage = 0; // Damage accumulated during current window
    private readonly List<Projectile> hitProjectiles = new List<Projectile>(); // Track projectiles that hit during window
    private const float DAMAGE_WINDOW_DURATION = 2f; // 2 frames to accumulate damage

    public Enemy(Texture2D texture, Vector2 position, AudioManager audioManager, int[] size, int health = 100, Color color = default, Rectangle? srcRect = null, Dictionary<Vector2, int>? collisionMap = null, float patrolSpeed = 1f, int[]? visualSize = null)
        : base(texture, position, audioManager, size, health, color, srcRect, collisionMap, visualSize)
    {
        this.Friction = 0.9f; // Enemy has default friction
        this.patrolSpeed = patrolSpeed;
        this.audioManager = audioManager;
    }

    protected override void OnEntityCollision(Entity other)
    {
        switch (other)
        {
            case Projectile projectile:
                // Only take damage from player projectiles
                if (projectile.FiredBy == Owner.Player)
                {
                    HandleProjectileHit(projectile);
                }
                break;
        }
    }

    private void HandleProjectileHit(Projectile projectile)
    {
        if (projectile.Lifetime <= 0f)
        {
            return;
        }

        if (projectile.EnemiesHit.Contains(this))
        {
            return; // Already hit by this projectile
        }

        // If no damage window is active, start a new one
        if (damageWindow <= 0f)
        {
            damageWindow = DAMAGE_WINDOW_DURATION;
            accumulatedDamage = 0;
            hitProjectiles.Clear();
        }

        audioManager.PlayAudio("collisionSfx", true, "null", false); //Play collision sound effect

        // Add this projectile's damage to accumulated damage
        accumulatedDamage += projectile.Damage;
        hitProjectiles.Add(projectile);
        projectile.EnemiesHit.Add(this); // Track that this enemy has been hit by this projectile
        // Expire the projectile unless it's piercing (piercing goes through enemies)
        if (!projectile.IsPiercing)
        {
            projectile.Lifetime = 0f;
        }

        // Apply knockback from the first projectile only (to prevent excessive knockback)
        if (hitProjectiles.Count == 1)
        {
            ApplyKnockback(projectile);
        }
    }

    private void ApplyKnockback(Projectile projectile)
    {
        float knockbackStrength = 8f;
        Vector2 projectileVelocity = new Vector2(projectile.XSpeed, projectile.YSpeed);
        if (projectileVelocity != Vector2.Zero)
        {
            projectileVelocity.Normalize();
            ChangeX += projectileVelocity.X * knockbackStrength;
            ChangeY += projectileVelocity.Y * knockbackStrength;
        }
        else
        {
            // Fallback: if projectile has no velocity, apply a simple upward knockback
            ChangeY -= knockbackStrength;
        }
    }

    private void UpdateDamageWindow()
    {
        if (damageWindow > 0f)
        {
            damageWindow -= 1f;

            // When damage window closes, apply accumulated damage and start invincibility
            if (damageWindow <= 0f && accumulatedDamage > 0)
            {
                Health -= accumulatedDamage; // Apply accumulated damage
                accumulatedDamage = 0;
                hitProjectiles.Clear();
            }
        }
    }

    private void PatrolUpdate()
    {

        // Only check ahead if we're on the ground
        if (!IsOnGround)
        {
            return;
        }

        // Check for walls ahead by looking a bit further ahead
        float checkDistance = 10f;
        float nextX = position.X + (patrolDirection * checkDistance);
        Rectangle nextRect = new Rectangle((int)nextX, (int)position.Y, size[0], size[1]);
        var horizontalCollisions = GetIntersectingTiles(nextRect);

        // Check if there's ground ahead
        bool groundAhead = IsGroundAhead(patrolDirection);

        // Reverse direction if hitting a wall or reaching an edge
        if (horizontalCollisions.Count > 0 || !groundAhead)
        {
            patrolDirection *= -1f;
        }

        // Set horizontal velocity (not position directly)
        ChangeX = patrolDirection * patrolSpeed;
    }


    public override void Update(GameTime gameTime)
    {
        // Update damage window system
        UpdateDamageWindow();

        // Update patrol behavior
        PatrolUpdate();

        base.Update(gameTime);
    }
}
