using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Math = System.Math;
using System.Collections.Generic;
using System.Linq;
using PolyGone.Items;
using PolyGone.Entities;
using PolyGone.Core;

namespace PolyGone.Weapons
{
    public class Blaster : Item
    {
        public float rotation = 0f;
        protected readonly List<Projectile> bullets; // Reference to shared bullets list
        protected float cooldown;
        private AudioManager audioManager;
        public float Cooldown => cooldown;
        public virtual float MaxCooldown => 12f; // Default blaster cooldown
        /// <summary>Extra bullets fired per shot (in addition to the base bullet). Set by MultiShotItem.</summary>
        public int ExtraBulletsPerShot { get; set; } = 0;
        /// <summary>Multiplier applied to the reset cooldown after firing. Set by RapidFireItem.</summary>
        public float CooldownMultiplier { get; set; } = 1f;
        /// <summary>Multiplier applied to bullet damage. Set by DamageBoostAttachment.</summary>
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>When true, all bullets pierce through enemies. Set by PiercingAttachment.</summary>
        public bool IsPiercing { get; set; } = false;
        /// <summary>When true, holding the mouse button fires continuously. Set by RapidFireItem.</summary>
        public bool IsAutoFire { get; set; } = false;
        protected readonly Dictionary<Vector2, int> collisionMap;
        public Blaster(Texture2D texture, Vector2 position, AudioManager audioManager, int[] size, Color color, Dictionary<Vector2, int> collisionMap, List<Projectile> sharedBullets, Rectangle? srcRect = null)
            : base(texture, position, size, color, "Blaster", "Basic energy weapon", srcRect)
        {
            this.bullets = sharedBullets; // Use shared bullets list
            this.cooldown = 0f;
            this.collisionMap = collisionMap;
            this.audioManager = audioManager;
        }

        public void Follow(Rectangle target, Vector2 cameraOffset)
        {
            Vector2 targetCenter = new Vector2(target.Center.X, target.Center.Y);
            float angle;

            if (InputManager.UsingController)
            {
                // Controller aiming: use right thumbstick direction
                Vector2 rightStick = InputManager.RightThumbstick;

                if (rightStick.Length() > 0.1f) // Lower threshold for smoother response
                {
                    float targetAngle = (float)Math.Atan2(rightStick.Y, rightStick.X);

                    // Fix angle wrapping by finding the shortest rotation path
                    float angleDifference = targetAngle - rotation;

                    // Wrap the difference to [-π, π] range
                    while (angleDifference > Math.PI)
                        angleDifference -= 2f * (float)Math.PI;
                    while (angleDifference < -Math.PI)
                        angleDifference += 2f * (float)Math.PI;

                    // Smooth interpolation using the corrected difference
                    float lerpSpeed = 0.2f;
                    angle = rotation + angleDifference * lerpSpeed;
                }
                else
                {
                    // Keep current rotation when stick is in deadzone
                    angle = rotation;
                }
            }

            else
            {
                // Mouse aiming: calculate angle to mouse from player center
                Vector2 mousePosition = InputManager.GetMousePosition().ToVector2();
                Vector2 worldMousePosition = mousePosition + cameraOffset;
                angle = (float)Math.Atan2(worldMousePosition.Y - targetCenter.Y, worldMousePosition.X - targetCenter.X);
            }

            // Set rotation to calculated angle
            rotation = angle;

            // Position blaster in circle around target center
            float radius = 50f; // Adjust this value to change orbit distance
            position = targetCenter + new Vector2(
                (float)Math.Cos(angle) * radius,
                (float)Math.Sin(angle) * radius
            );

            // Offset to center the blaster sprite on its position
            position -= new Vector2(size[0] / 2f, size[1] / 2f);
        }

        public override void Use()
        {
            // Handle shooting with InputManager to prevent click carryover
            if (InputManager.GameShootSingle() && cooldown <= 0f)
            {
                int baseDamage = (int)(40 * DamageMultiplier);

                // Central / base bullet
                bullets.Add(new Projectile(
                    texture: texture,
                    position: new Vector2(position.X + size[0] / 2f - 5f, position.Y + size[1] / 2f - 5f),
                    audioManager: audioManager,
                    size: new int[2] { 10, 10 },
                    lifetime: 200f,
                    health: 1,
                    damage: baseDamage,
                    color: IsPiercing ? new Color(140, 0, 200) : Color.White,
                    xSpeed: (float)(Math.Cos(rotation) * 750f),
                    ySpeed: (float)(Math.Sin(rotation) * 750f),
                    owner: Owner.Player,
                    srcRect: srcRect,
                    collisionMap: collisionMap,
                    isPiercing: IsPiercing
                ));

                audioManager.PlayAudio("shootSfx", true, "null", false); //Play shoot sound effect

                // Extra spread bullets added by MultiShotItem
                if (ExtraBulletsPerShot > 0)
                {
                    const float SpreadStep = 0.18f; // radians between each extra bullet
                    int half = ExtraBulletsPerShot / 2;
                    for (int i = 0; i < ExtraBulletsPerShot; i++)
                    {
                        float spreadOffset = (i - half + (ExtraBulletsPerShot % 2 == 0 ? 0.5f : 0f)) * SpreadStep;
                        float angle = rotation + spreadOffset;
                        bullets.Add(new Projectile(
                            texture: texture,
                            position: new Vector2(position.X + size[0] / 2f - 5f, position.Y + size[1] / 2f - 5f),
                            audioManager: audioManager,
                            size: new int[2] { 10, 10 },
                            lifetime: 200f,
                            health: 1,
                            damage: baseDamage,
                            color: IsPiercing ? new Color(140, 0, 200) : Color.White,
                            xSpeed: (float)(Math.Cos(angle) * 750f),
                            ySpeed: (float)(Math.Sin(angle) * 750f),
                            owner: Owner.Player,
                            srcRect: srcRect,
                            collisionMap: collisionMap,
                            isPiercing: IsPiercing
                        ));
                    }
                }

                cooldown = MaxCooldown * CooldownMultiplier;
                if (!IsAutoFire) InputManager.ConsumeClick(); // Prevent multi-shot from same click press
            }
        }

        public override void Update(GameTime gameTime)
        {
            // Update cooldown only - bullets are managed by Player
            cooldown = Math.Max(0f, cooldown - 1f);

            base.Update(gameTime);
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 offset)
        {
            // Draw the blaster with rotation around its center
            Vector2 origin = new Vector2(size[0] / 2f, size[1] / 2f);
            Vector2 drawPosition = position - offset + origin;
            spriteBatch.Draw(texture, drawPosition, srcRect, color, rotation, origin, 1f, SpriteEffects.None, 0f);
            
            // Bullets are drawn by Player
        }
    }
}
