using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using PolyGone.Weapons;
using PolyGone.Items;

namespace PolyGone.Entities
{

    public class Player : Entity
    {
        // Constants for gap centering nudge strengths
        private const float VERTICAL_GAP_NUDGE_STRENGTH = 20f; // Strong nudge for vertical movement through gaps
        private const float HORIZONTAL_GAP_NUDGE_STRENGTH = 15f; // Medium nudge for horizontal gap funneling

        private KeyboardState keyboardState;
        private KeyboardState previousKeyboardState;
        private Item? currentWeapon; // Single selected weapon
        private readonly List<Item> itemInventory = new List<Item>(); // Pre-selected items (max 2)
        public readonly List<Projectile> bullets = new List<Projectile>(); // Shared projectile list for all weapons
        private float ffCooldown = 0f;
        private float coyoteTime = 0f; // Allows jumping shortly after leaving a platform
        private bool isTouchingWall = false; // True when currently against a wall
        private int wallDirection = 0; // -1 for left wall, 1 for right wall
        private int dashCharges = 1;
        public int MaxDashCharges { get; set; } = 1;
        public int DashCharges => dashCharges;
        public float DashCD = 0f;
        public float DashCD2 = 0f;

        // Public property to access changeY for items
        public float ChangeY => changeY;

        // Public property to access current weapon's cooldown for HUD
        public float Cooldown => GetBlaster()?.Cooldown ?? 0f;


        // Public property to control gravity (used by LowGravityItem)
        public float GravityScale { get => gravityScale; set => gravityScale = value; }

        // Jump velocity — scaled by LowGravityItem to keep peak height constant
        public float JumpStrength { get; set; } = -16.75f;

        public Player(
            Texture2D texture,
            Vector2 position,
            int[] size,
            int health,
            Color color,
            Rectangle? srcRect,
            Dictionary<Vector2, int> collisionMap,
            Texture2D blasterTexture,
            List<ItemType> selectedItems,
            WeaponType selectedWeapon,
            int[]? visualSize = null
        )
            : base(texture, position, size, health, color, srcRect, collisionMap, visualSize)
        {
            // Create the selected weapon
            switch (selectedWeapon)
            {
                case WeaponType.Blaster:
                    currentWeapon = new Blaster(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.White, collisionMap, bullets, srcRect);
                    break;
                case WeaponType.Shotgun:
                    currentWeapon = new Shotgun(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Red, collisionMap, bullets, srcRect);
                    break;
                case WeaponType.Rifle:
                    currentWeapon = new Rifle(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Black, collisionMap, bullets, srcRect);
                    break;
                case WeaponType.Automatic:
                    currentWeapon = new Automatic(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Cyan, collisionMap, bullets, srcRect);
                    break;
                case WeaponType.VoidLance:
                    currentWeapon = new VoidLance(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, new Color(180, 0, 220), collisionMap, bullets, srcRect);
                    break;
            }

            // Create and activate the selected items
            foreach (var itemType in selectedItems)
            {
                Item? item = null;
                switch (itemType)
                {
                    case ItemType.DoubleJump:
                        item = new DoubleJumpItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Blue, srcRect);
                        break;
                    case ItemType.DoubleDash:
                        item = new DoubleDashItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Cyan, srcRect);
                        break;
                    case ItemType.HealingGlow:
                        item = new HealingGlowItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Green, srcRect);
                        break;
                    case ItemType.MultiShot:
                        item = new MultiShotItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Red, srcRect);
                        break;
                    case ItemType.RapidFire:
                        item = new RapidFireItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Orange, srcRect);
                        break;
                    case ItemType.LowGravity:
                        item = new LowGravityItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Purple, srcRect);
                        break;
                    case ItemType.IronWill:
                        item = new IronWillItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Gold, srcRect);
                        break;
                }

                if (item != null)
                {
                    itemInventory.Add(item);
                    item.Apply(this); // Automatically activate all selected items
                }
            }

            dashCharges = MaxDashCharges;
            this.friction = .8f; // Player has more friction for tighter control
        }

        protected override void HandleVerticalCollision(ref bool onGround, ref float deltaY, List<(Rectangle, CollisionType)> collisions)
        {
            var (tileRect, colType) = collisions[0];
            switch (colType)
            {
                default:
                case CollisionType.Solid:
                case CollisionType.Rough:
                case CollisionType.Slippery:
                    if (deltaY > 0)
                    {
                        position.Y = tileRect.Top - size[1];
                        onGround = true;
                    }
                    else if (deltaY < 0)
                    {
                        position.Y = tileRect.Bottom;
                    }
                    deltaY = 0;
                    break;
                case CollisionType.SemiSolid:
                    if (keyboardState.IsKeyDown(Keys.S))
                    {
                        // Drop through platform
                        position.Y += deltaY;
                        ffCooldown = 4f; // Prevent bouncing back up
                    }
                    else if (deltaY > 0 && (position.Y + size[1]) <= tileRect.Top + 10 && ffCooldown <= 0f)
                    {
                        position.Y = tileRect.Top - size[1];
                        deltaY = 0;
                        onGround = true;
                    }
                    else
                    {
                        position.Y += deltaY;
                    }
                    break;
            }
        }

    protected override void HandleHorizontalCollision(ref float deltaX, List<(Rectangle, CollisionType)> collisions)
    {
        var (tileRect, colType) = collisions[0];
        switch (colType)
        {
            default:
            case CollisionType.Solid:
            case CollisionType.Rough:
            case CollisionType.Slippery:
                // Standard wall collision
                position.X = deltaX > 0 ? tileRect.Left - size[0] : tileRect.Right;
                // Track which wall we're touching
                isTouchingWall = true;
                wallDirection = deltaX > 0 ? 1 : -1; // 1 for right wall, -1 for left wall
                deltaX = 0;
                break;
            case CollisionType.SemiSolid:
                position.X += deltaX;
                break;
        }
    }

        // Handle player input and jumping
        private void HandleInput()
        {
            previousKeyboardState = keyboardState;
            keyboardState = Keyboard.GetState();
            float moveDirection = 0;

            // Horizontal movement with speed boost consideration
            if (keyboardState.IsKeyDown(Keys.A) && !keyboardState.IsKeyDown(Keys.D))
            {
                moveDirection = -1.5f;
            }
            else if (keyboardState.IsKeyDown(Keys.D) && !keyboardState.IsKeyDown(Keys.A))
            {
                moveDirection = 1.5f;
            }

            // Apply acceleration with speed boost
            float speedMultiplier = 1f;
            
            // Reduce acceleration when in the air
            float airControlMultiplier = isOnGround ? 1f : 0.3f;
            changeX += moveDirection * 1f * speedMultiplier * airControlMultiplier;
            
            // Clamp speed based on ground state
            if (isOnGround)
            {
                changeX = MathHelper.Clamp(changeX, -100 * speedMultiplier, 100f * speedMultiplier);
            }
            else
            {
                // Much tighter air control - limit max speed accumulation
                changeX = MathHelper.Clamp(changeX, -20f * speedMultiplier, 20f * speedMultiplier);
            }

            bool dashPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            bool dashPressedLastFrame = previousKeyboardState.IsKeyDown(Keys.LeftShift) || previousKeyboardState.IsKeyDown(Keys.RightShift);

            if (dashPressed && !dashPressedLastFrame && moveDirection != 0 && dashCharges > 0 && DashCD2 <= 0f)
            {
                // Apply dash by setting velocity directly (after clamp)
                changeX = moveDirection > 0 ? 40f : -40f;
                dashCharges--;
                DashCD2 = 10f; // short lockout between dashes

                // Start recharge timer when we are below max charges.
                if (dashCharges < MaxDashCharges && DashCD <= 0f)
                {
                    DashCD = 60f;
                }
            }
            // Jumping with coyote time and double jump
            bool spacePressed = keyboardState.IsKeyDown(Keys.Space);
            bool wasOnGroundLastFrame = isOnGround;
            
            bool spacePressedLastFrame = previousKeyboardState.IsKeyDown(Keys.Space);
            
            // Wall jump check - works if touching wall or within 2 pixels
            int nearWallDirection = 0;
            bool nearWall = isTouchingWall || IsNearWall(out nearWallDirection);
            int activeWallDirection = isTouchingWall ? wallDirection : nearWallDirection;
            
            if (nearWall && spacePressed && !spacePressedLastFrame && !isOnGround)
            {
                changeY = JumpStrength;
                // Add momentum away from the wall
                changeX = -activeWallDirection * 6f; // Push away from wall
                isTouchingWall = false; // Consume wall jump
                coyoteTime = 0f;
                GetActiveDoubleJumpItem()?.Reset(); // Allow double jump after wall jump
            }
            else if ((isOnGround || coyoteTime > 0f) && spacePressed)
            {
                changeY = JumpStrength;
                coyoteTime = 0f; // Reset coyote time after jumping
                GetActiveDoubleJumpItem()?.Reset(); // Allow double jump in the new air phase
            }
            else
            {
                // Check for double jump
                var doubleJumpItem = GetActiveDoubleJumpItem();
                if (doubleJumpItem != null && doubleJumpItem.TryDoubleJump(this, spacePressed, wasOnGroundLastFrame))
                {
                    changeY = JumpStrength; // Same jump strength for double jump
                }
            }
            
            // Reset wall touching state at end of input (will be set again by collision if still touching)
            isTouchingWall = false;
        }

        protected override void OnEntityCollision(Entity other)
        {
            switch (other)
            {
                default:
                    break;
                case Projectile projectile:
                    // Only take damage from enemy projectiles
                    if (projectile.owner == Owner.Enemy && invincibilityFrames <= 0f)
                    {
                        health -= projectile.damage;
                        if (health <= 0)
                        {
                            TryAbsorbLethalHit();
                        }

                        // Apply knockback in the direction the projectile was travelling
                        Vector2 projectileVelocity = new Vector2(projectile.xSpeed, projectile.ySpeed);
                        if (projectileVelocity != Vector2.Zero)
                        {
                            projectileVelocity.Normalize();
                            changeX = projectileVelocity.X * 10f;
                            changeY = projectileVelocity.Y * 10f - 5f; // extra upward bias
                        }

                        invincibilityFrames = 60f;

                        // Despawn the projectile on contact
                        projectile.lifetime = 0f;
                    }
                    break;
                case Enemy:
                    // Only take damage if not invincible
                    if (invincibilityFrames <= 0f)
                    {
                        // Take 40 damage
                        health -= 40;
                        if (health <= 0)
                            TryAbsorbLethalHit();

                        // Calculate knockback direction (away from enemy)
                        float knockbackX = position.X < other.position.X ? -10f : 10f;
                        float knockbackY = -20f;

                        // Apply knockback
                        changeX = knockbackX;
                        changeY = knockbackY / 2; // Reduced vertical knockback for better feel

                        // Set invincibility frames (roughly 1 second at 60fps)
                        invincibilityFrames = 60f;
                    }
                    break;
            }
        }

        protected override void PhysicsUpdate(float deltaTime)
        {
            // Call base physics update for standard collision handling
            base.PhysicsUpdate(deltaTime);
        }

        protected override void OnVerticalMovementComplete(float deltaTime)
        {
            // Player-specific vertical gap centering (stronger than base)
            if (Math.Abs(changeY) > 0.1f && collisionMap != null)
            {
                int playerTileX = (int)((position.X + size[0] / 2f) / TILE_SIZE);
                int playerTileY = (int)((position.Y + size[1] / 2f) / TILE_SIZE);

                var keyLeft = new Vector2(playerTileX - 1, playerTileY);
                var keyRight = new Vector2(playerTileX + 1, playerTileY);

                if (IsSolidWall(keyLeft) && IsSolidWall(keyRight))
                {
                    ApplyGapCentering(playerTileX, VERTICAL_GAP_NUDGE_STRENGTH);
                }
            }
        }

        protected override void OnHorizontalMovementComplete(float deltaTime)
        {
            // Gap funneling: when walking over a 1-tile gap and pressing S, funnel down through it
            if (isOnGround && keyboardState.IsKeyDown(Keys.S) && collisionMap != null)
            {
                int playerTileX = (int)((position.X + size[0] / 2f) / TILE_SIZE);
                int playerTileY = (int)((position.Y + size[1] / 2f) / TILE_SIZE);

                // Check if there's a semi-solid platform directly below
                var keyBelow = new Vector2(playerTileX, playerTileY + 1);
                bool semiSolidBelow = collisionMap.TryGetValue(keyBelow, out int belowTileId) &&
                                      belowTileId != -1 &&
                                      CollisionTypeMapper.GetCollisionType(belowTileId) == CollisionType.SemiSolid;

                if (semiSolidBelow)
                {
                    var keyLeft = new Vector2(playerTileX - 1, playerTileY);
                    var keyRight = new Vector2(playerTileX + 1, playerTileY);

                    if (IsSolidWall(keyLeft) && IsSolidWall(keyRight))
                    {
                        ApplyGapCentering(playerTileX, HORIZONTAL_GAP_NUDGE_STRENGTH);
                    }
                }
            }
        }

        private void ApplyGapCentering(int tileX, float nudgeStrength)
        {
            float tileCenter = (float)tileX * TILE_SIZE + TILE_HALF_SIZE;
            float playerCenter = position.X + size[0] / 2f;
            float offset = tileCenter - playerCenter;

            if (Math.Abs(offset) > 0.1f)
            {
                float nudge = Math.Sign(offset) * nudgeStrength;
                if (Math.Abs(nudge) > Math.Abs(offset))
                    nudge = offset;
                position.X += nudge;
            }
        }


        private DoubleJumpItem? GetActiveDoubleJumpItem()
        {
            return itemInventory.OfType<DoubleJumpItem>().FirstOrDefault(item => item.IsActive);
        }

        private DoubleDashItem? GetActiveDoubleDashItem()
        {
            return itemInventory.OfType<DoubleDashItem>().FirstOrDefault(item => item.IsActive);
        }

        private HealingGlowItem? GetActiveHealingGlowItem()
        {
            return itemInventory.OfType<HealingGlowItem>().FirstOrDefault(item => item.IsActive);
        }

        private IronWillItem? GetReadyIronWillItem()
        {
            return itemInventory.OfType<IronWillItem>().FirstOrDefault(item => item.IsReady);
        }

        /// <summary>Check if player is within 2 pixels of a wall.</summary>
        private bool IsNearWall(out int direction)
        {
            direction = 0;
            if (collisionMap == null) return false;

            int playerCenterY = (int)((position.Y + size[1] / 2f) / TILE_SIZE);
            
            // Check left side
            int leftEdgeX = (int)(position.X / TILE_SIZE);
            float leftDistance = position.X - (leftEdgeX * TILE_SIZE);
            if (leftDistance <= 2f)
            {
                var leftKey = new Vector2(leftEdgeX - 1, playerCenterY);
                if (IsSolidWall(leftKey))
                {
                    direction = -1;
                    return true;
                }
            }
            
            // Check right side
            int rightEdgeX = (int)((position.X + size[0]) / TILE_SIZE);
            float rightDistance = (rightEdgeX + 1) * TILE_SIZE - (position.X + size[0]);
            if (rightDistance <= 2f)
            {
                var rightKey = new Vector2(rightEdgeX + 1, playerCenterY);
                if (IsSolidWall(rightKey))
                {
                    direction = 1;
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>If Iron Will is ready, survive the killing blow at 1 HP.</summary>
        private void TryAbsorbLethalHit()
        {
            var ironWill = GetReadyIronWillItem();
            if (ironWill != null && ironWill.TryAbsorbLethalHit())
                health = 1;
        }

        // Helper method to get the currently equipped blaster/weapon
        public Blaster? GetBlaster()
        {
            return currentWeapon as Blaster;
        }

        // Public property to access current blaster for backward compatibility
        public Blaster blaster => GetBlaster();

        // Public method to access item inventory for UI
        public List<Item> GetAllItems()
        {
            return itemInventory;
        }

        public override void Update(GameTime gameTime)
        {
            Update(gameTime, Vector2.Zero);
        }

        public void Update(GameTime gameTime, Vector2 cameraOffset)
        {
            HandleInput();

            base.Update(gameTime);

            // Update coyote time after physics update to use current frame's ground state
            coyoteTime = isOnGround
                ? 6f // 0.1 seconds at 60fps
                : Math.Max(0f, coyoteTime - 1f);

            // Update only the currently equipped weapon
            var currentBlaster = GetBlaster();
            if (currentBlaster != null)
            {
                currentBlaster.Follow(new Rectangle((int)position.X, (int)position.Y, size[0], size[1]), cameraOffset);
                currentBlaster.Use();
                currentBlaster.Update(gameTime);
            }

            // Update all bullets (shared across all weapons) - iterate backwards for safe removal
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                bullet.lifetime -= 1f;
                if (bullet.lifetime <= 0f)
                {
                    bullets.RemoveAt(i);
                }
                else
                {
                    bullet.Update(gameTime);
                }
            }

            // Update active items
            foreach (var item in itemInventory.Where(item => item.IsActive))
            {
                item.Update(gameTime);

                // Apply healing over time from HealingGlowItem
                if (item is HealingGlowItem healingGlow)
                {
                    healingGlow.ApplyHealingOverTime(this, (float)gameTime.ElapsedGameTime.TotalSeconds);
                }
            }

            ffCooldown = Math.Max(0f, ffCooldown - 1f);
            DashCD = Math.Max(0f, DashCD - 1f);
            DashCD2 = Math.Max(0f, DashCD2 - 1f);

            // Keep charge count valid if max changes via items.
            dashCharges = Math.Min(dashCharges, MaxDashCharges);

            // Recharge one dash charge every 60 frames until full.
            if (dashCharges < MaxDashCharges && DashCD <= 0f)
            {
                dashCharges++;
                if (dashCharges < MaxDashCharges)
                {
                    DashCD = 60f;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 offset)
        {
            // Check if we should draw with healing glow
            var healingGlow = GetActiveHealingGlowItem();
            bool shouldGlow = healingGlow?.ShouldGlow() ?? false;

            if (shouldGlow)
            {
                // Draw glow outline first (behind the player) - 68x68 total size
                Color glowColor = healingGlow.GetGlowColor();
                for (int x = -4; x <= 4; x++)
                {
                    for (int y = -4; y <= 4; y++)
                    {
                        if (x == 0 && y == 0)
                            continue; // Skip center
                        Vector2 glowOffset = new Vector2(x, y);
                        spriteBatch.Draw(texture, position - offset + hitboxOffset + glowOffset, srcRect, glowColor);
                    }
                }
            }

            base.Draw(spriteBatch, offset);

            // Draw only the currently equipped weapon
            var currentBlaster = GetBlaster();
            currentBlaster?.Draw(spriteBatch, offset);

            // Draw all bullets (shared across all weapons)
            foreach (var bullet in bullets)
            {
                bullet.Draw(spriteBatch, offset);
            }
        }

        // Method to draw item indicators at fixed screen positions (called from GameScene)
        public void DrawItemIndicators(SpriteBatch spriteBatch, Texture2D itemTexture, Rectangle itemSrcRect)
        {
            const int MaxDisplayedItems = 3; // Maximum number of items to display

            // Draw item indicators in fixed screen position (not affected by camera)
            Vector2 indicatorStart = new Vector2(20, 20); // Fixed top-left screen position
            int spacing = 25;

            for (int i = 0; i < itemInventory.Count && i < MaxDisplayedItems; i++)
            {
                var item = itemInventory[i];
                Vector2 indicatorPos = indicatorStart + new Vector2(i * (float)spacing, 0);
                item.DrawIndicator(spriteBatch, indicatorPos, itemTexture, itemSrcRect, i);
            }
        }
    }
}
