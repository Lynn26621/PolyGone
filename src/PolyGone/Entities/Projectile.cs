using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using PolyGone.Core;

namespace PolyGone
{
    public enum Owner
    {
        Player,
        Enemy
    }

    public class Projectile : Entity
    {
        public readonly float XSpeed;
        public readonly float YSpeed;
        public float Lifetime; // Separate from Entity health for projectiles
        public readonly Owner FiredBy; // Who fired this projectile
        public readonly int Damage; // Fixed damage for now
        /// <summary>When true, the projectile passes through enemies instead of being destroyed on hit.</summary>
        public bool IsPiercing { get; }
        /// <summary>When true, the projectile instantly kills any enemy it hits (DEV only).</summary>
        public bool IsInstantKill { get; set; } = false;
        internal List<Enemy> EnemiesHit = new(); // Track enemies hit to prevent multiple hits from piercing projectiles

        public Projectile(Texture2D texture, Vector2 position, AudioManager audioManager, int[] size, float lifetime, int health, Color color, float xSpeed, float ySpeed, Owner owner, int damage, Rectangle? srcRect = null, Dictionary<Vector2, int>? collisionMap = null, bool isPiercing = false)
            : base(texture, position, audioManager, size, health, color, srcRect, collisionMap)
        {
            this.XSpeed = xSpeed;
            this.YSpeed = ySpeed;
            this.Lifetime = lifetime;
            this.FiredBy = owner;
            this.Damage = damage;
            this.IsPiercing = isPiercing;
        }

        // Fire projectiles from blaster to global mouse position
        public override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            position.X += (float)(XSpeed * deltaTime);
            position.Y += (float)(YSpeed * deltaTime);

            // Check for tile collision and destroy projectile if hit solid tile
            if (CollisionMap != null)
            {
                Rectangle projectileRect = new Rectangle((int)position.X, (int)position.Y, size[0], size[1]);
                var collisions = GetIntersectingTiles(projectileRect);
                // Only destroy if hit a solid collision type (not SemiSolid or None)
                if (collisions.Any(c => c.Item2 != CollisionType.None && c.Item2 != CollisionType.SemiSolid))
                {
                    Lifetime = 0;
                }
            }
        }
    }
}
