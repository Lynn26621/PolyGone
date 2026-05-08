using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PolyGone.Core;
using PolyGone.Items;
using PolyGone.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Channels;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace PolyGone.Entities
{

    public class Player : Entity
    {
        // Constants for gap centering nudge strengths
        private const float VERTICAL_GAP_NUDGE_STRENGTH = 20f; // Strong nudge for vertical movement through gaps
        private const float HORIZONTAL_GAP_NUDGE_STRENGTH = 15f; // Medium nudge for horizontal gap funneling
        private const float BOUNCY_BOUNCE_THRESHOLD = 4f; // Only stronger landings should bounce
        private const float WALL_CLING_START_SLOW_FACTOR = 0.55f; // Initial damp on downward speed while clinging
        private const float WALL_CLING_START_SPEED_CAP = 3f; // Initial max downward speed while clinging
        private const float WALL_CLING_RAMP_FRAMES = 45f; // Time to ramp cling effect back to normal fall
        private const float SAME_WALL_JUMP_LOCK_FRAMES = 40f; // Stricter lock to prevent same-wall climb loops
        private const float WALL_CLING_PROBE_TOP_RATIO = 0.35f; // Upper torso wall probe
        private const float WALL_CLING_PROBE_BOTTOM_RATIO = 0.75f; // Lower torso wall probe
        private const float NORMAL_MOVE_ACCELERATION = 1.15f;
        private const float SLIPPERY_REVERSE_ACCELERATION_FACTOR = 0.18f;
        private const float NORMAL_MAX_MOVE_SPEED = 7.5f;
        private const float PLAYER_GROUND_DECEL = 2.2f;
        private const float PLAYER_AIR_DECEL = 0.25f;
        private const float PLAYER_SLIPPERY_GROUND_DECEL = 0.03f;
        private const float PLAYER_EXTRA_VELOCITY_DECEL = 1.9f;
        private const float DASH_EXTRA_SPEED_CAP = 25f;
        private const float DASH_TOTAL_SPEED_CAP = 25f;

        private AudioManager audioManager;
        private Item? currentWeapon; // Single selected weapon
        private readonly List<Item> itemInventory = new List<Item>(); // Pre-selected items (max 2)
        public readonly List<Projectile> Bullets = new List<Projectile>(); // Shared projectile list for all weapons
        private float ffCooldown = 0f;
        private float coyoteTime = 0f; // Allows jumping shortly after leaving a platform
        private float wallCoyoteTime = 0f; // Allows wall jump shortly after leaving a wall
        private float wallBoostLength = 0f; // Prevents player from immediately moving back towards the wall after a wall jump
        private int wallDirection = 0; // 1 for left wall, -1 for right wall (player should be boosted away from the wall when wall jumping)
        protected bool IsOnWall = false; // Indicates if the player is currently clinging to a wall
        protected bool JustWallJumped = false; // Track if the player just performed a wall jump, used to prevent repeat wall jumps
        private int lastWallJumpDirection = 0; // Wall side used for last wall jump (1 left, -1 right)
        private float sameWallJumpLockTimer = 0f;
        private float wallClingFrames = 0f;
        private int wallCoyoteDirection = 0;
        private float wallDashLockTimer = 0f; // Prevents dashing back towards a wall after wall jumping
        private int wallDashLockDirection = 0; // Direction to prevent dashing towards (1 for left, -1 for right)

        // -----------------------------------------------------------------------
        // Player Ability Definitions
        // -----------------------------------------------------------------------
        private readonly string[] playerAbilityNames =
        {
            "Dash",
            "WallJump"
        };

        private readonly string[] playerAbilityDescriptions =
        {
            "Quickly moves the player 5 blocks in the direction they're moving with a cooldown of 2.5 seconds",
            "Allows the player to cling to a wall, slowing their descent and giving them a chance to jump from the wall"
        };

        // Public property to access changeY for items
        public new float ChangeY
        {
            get => base.ChangeY; set => base.ChangeY = value;
        }

        // Public property to access current weapon's cooldown for HUD
        public float Cooldown => GetBlaster()?.Cooldown ?? 0f;

        // Public property to control gravity (used by LowGravityItem)
        public new float GravityScale { get => base.GravityScale; set => base.GravityScale = value; }

        // Exposed for items that must defer to wall-jump/wall-coyote behavior.
        public bool HasWallJumpAssist => IsOnWall || wallCoyoteTime > 0f;

        // Jump velocity — scaled by LowGravityItem to keep peak height constant
        public float JumpStrength { get; set; } = -16.75f;
        private bool isOnBouncyTile = false;
        private bool wasOnBouncyTile = false;

        // Dash velocity boost applied when dashing
        public float DashStrength { get; set; } = 20f;

        // Strength of horizontal boost applied when wall jumping
        public float WallBoostStrength { get; set; } = 5f;

        // Frame-based contact counter to make wall cling detection slightly slower
        private int wallContactFrames = 0;
        private const int WALL_CONTACT_REQUIRED = 6; // ~0.1s at 60fps

        public Player(
            Texture2D texture,
            Vector2 position,
            int[] size,
            int health,
            Color color,
            Rectangle? srcRect,
            Dictionary<Vector2, int> CollisionMap,
            Texture2D blasterTexture,
            List<ItemType> selectedItems,
            List<BlasterAttachmentType> selectedAttachments,
            AudioManager audioManager,
            int[]? visualSize = null
        )
            : base(texture, position, audioManager, size, health, color, srcRect, CollisionMap, visualSize)
        {
            // Always use the Blaster as the base weapon
            currentWeapon = new Blaster(blasterTexture, Vector2.Zero, audioManager, new int[] { 32, 32 }, Color.White, CollisionMap, Bullets, srcRect);

            // Apply blaster attachments to the freshly created blaster
            foreach (var attachmentType in selectedAttachments)
            {
                Item? attachment = null;
                switch (attachmentType)
                {
                    case BlasterAttachmentType.MultiShot:
                        attachment = new MultiShotItem(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Red, srcRect);
                        break;
                    case BlasterAttachmentType.RapidFire:
                        attachment = new RapidFireItem(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Orange, srcRect);
                        break;
                    case BlasterAttachmentType.Piercing:
                        attachment = new PiercingAttachment(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, new Color(140, 0, 200), srcRect);
                        break;
                    case BlasterAttachmentType.DamageBoost:
                        attachment = new DamageBoostAttachment(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, new Color(255, 60, 0), srcRect);
                        break;
#if DEBUG
                    case BlasterAttachmentType.DevBlaster:
                        // DEV: Apply all attachment effects at once
                        new MultiShotItem(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Red, srcRect).Apply(this);
                        new RapidFireItem(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, Color.Orange, srcRect).Apply(this);
                        new PiercingAttachment(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, new Color(140, 0, 200), srcRect).Apply(this);
                        new DamageBoostAttachment(blasterTexture, Vector2.Zero, new int[] { 32, 32 }, new Color(255, 60, 0), srcRect).Apply(this);
                        break;
#endif
                }
                if (attachment != null)
                {
                    itemInventory.Add(attachment);
                    attachment.Apply(this);
                }
            }

            // Create and activate the selected player items
            foreach (var itemType in selectedItems)
            {
                Item? item = null;
                switch (itemType)
                {
                    case ItemType.DoubleJump:
                        item = new DoubleJumpItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Blue, srcRect);
                        break;
                    case ItemType.HealingGlow:
                        item = new HealingGlowItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Green, srcRect);
                        break;
                    case ItemType.LowGravity:
                        item = new LowGravityItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Purple, srcRect);
                        break;
                    case ItemType.IronWill:
                        item = new IronWillItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Gold, srcRect);
                        break;
#if DEBUG
                    case ItemType.DevMode:
                        item = new DevModeItem(texture, Vector2.Zero, new int[] { 32, 32 }, Color.Magenta, srcRect);
                        break;
#endif
                }

                if (item != null)
                {
                    itemInventory.Add(item);
                    item.Apply(this); // Automatically activate all selected items
                }
            }

            ConfigureHorizontalDamping(
                PLAYER_GROUND_DECEL,
                PLAYER_AIR_DECEL,
                PLAYER_SLIPPERY_GROUND_DECEL,
                PLAYER_EXTRA_VELOCITY_DECEL,
                slipperyControlMultiplier: 0.2f
            );
            this.audioManager = audioManager; // Store reference to AudioManager for playing audio
        }

        protected override void HandleVerticalCollision(ref bool onGround, ref float deltaY, List<(Rectangle, CollisionType)> collisions)
        {
            var (tileRect, colType) = collisions[0];
            switch (colType)
            {
                default:
                case CollisionType.Solid:
                    position.Y = deltaY > 0 ? tileRect.Top - size[1] : tileRect.Bottom;
                    onGround = deltaY > 0;
                    deltaY = 0;
                    break;
                case CollisionType.SemiSolid:
                    if (InputManager.GameDrop())
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
                case CollisionType.Slippery:
                    position.Y = deltaY > 0 ? tileRect.Top - size[1] : tileRect.Bottom;
                    onGround = deltaY > 0;
                    deltaY = 0;
                    break;
                case CollisionType.Bouncy: //Keep track of velocity. Reverse it when colliding with bouncy tile top. holding down input when landing on bouncy tile will negate the bounce effect. Also, when the player is just standing on the tile and jumps, their jump is boosted by 50%.
                    if (deltaY > 0)
                    {
                        position.Y = tileRect.Top - size[1];
                        onGround = true;
                        isOnBouncyTile = true;
                        deltaY = (!InputManager.GameDrop() && deltaY > BOUNCY_BOUNCE_THRESHOLD)
                            ? -ChangeY // Reverse and boost vertical velocity
                            : 0;
                    }
                    else
                    {
                        position.Y = tileRect.Bottom;
                        deltaY = 0;
                    }
                    break;
                case CollisionType.Damage:
                    position.Y = deltaY > 0 ? tileRect.Top - size[1] : tileRect.Bottom;
                    onGround = deltaY > 0;
                    deltaY = 0;
                    TakeDamage(10);
                    break;
            }
        }

        // Handle horizontal collisions
        protected override void HandleHorizontalCollision(ref float deltaX, List<(Rectangle, CollisionType)> collisions)
        {
            var (tileRect, colType) = collisions[0];
            switch (colType)
            {
                default:
                case CollisionType.Slippery:
                case CollisionType.Bouncy:
                case CollisionType.Solid:
                    position.X = deltaX > 0 ? tileRect.Left - size[0] : tileRect.Right;
                    deltaX = 0;
                    break;
                case CollisionType.SemiSolid: // Player passes through semi-solid platforms horizontally without collision
                    position.X += deltaX;
                    break;
                case CollisionType.Damage:
                    position.X = deltaX > 0 ? tileRect.Left - size[0] : tileRect.Right;
                    deltaX = 0;
                    TakeDamage(10);
                    break;
            }
        }

        // Handle player input and jumping
        private void HandleInput()
        {
            float moveDirection = 0f;
            bool performedWallJumpThisFrame = false;

            // Horizontal movement with speed boost consideration
            if (InputManager.GameMoveLeft() && !InputManager.GameMoveRight())
            {
                moveDirection = -1f;
            }
            else if (InputManager.GameMoveRight() && !InputManager.GameMoveLeft())
            {
                moveDirection = 1f;
            }

            // Prevent moving back toward wall immediately after a wall jump
            if (JustWallJumped && wallBoostLength > 0f && wallDirection != 0)
            {
                // If wall is on left (1) prevent left input; if on right (-1) prevent right input
                if ((wallDirection == 1 && InputManager.GameMoveLeft()) || (wallDirection == -1 && InputManager.GameMoveRight()))
                {
                    moveDirection = 0f;
                }
            }

            // Apply acceleration with speed boost
            float reverseFactor = IsStandingOnCollisionType(CollisionType.Slippery)
                ? SLIPPERY_REVERSE_ACCELERATION_FACTOR
                : 1f;
            ApplyHorizontalIntent(moveDirection, NORMAL_MAX_MOVE_SPEED, NORMAL_MOVE_ACCELERATION, reverseFactor);

            // Jumping with coyote time and double jump
            bool JumpTriggered = InputManager.GameJump();
            bool JumpHeld = InputManager.GameJumpHeld();
            bool wasOnGroundLastFrame = IsOnGround;

            if ((IsOnGround || coyoteTime > 0f) && JumpHeld)
            {
                // Check if standing on bouncy tile for jump boost
                float jumpPower = JumpStrength;

                // Check if the player is standing on a bouncy tile: use wasOnBouncyTile (from last frame),
                // isOnBouncyTile (set during this frame's collision), and a direct ground probe for consistency.
                // This ensures that holding jump will always trigger the larger jump on bouncy tiles.
                bool standingOnBouncy = wasOnBouncyTile || isOnBouncyTile || IsStandingOnCollisionType(CollisionType.Bouncy);

                if (standingOnBouncy)
                {
                    jumpPower = JumpStrength * 1.5f; // 50% jump boost on bouncy tiles
                }

                base.ChangeY = jumpPower;
                audioManager.PlayAudio("jumpSfx", true, "null", false);
                coyoteTime = 0f;
                GetActiveDoubleJumpItem()?.Reset();
            }
            else
            {
                var wallJump = playerAbilityNames[1];

                int jumpWallDirection = ResolveWallJumpDirection();
                bool usingWallAssist = IsOnWall || wallCoyoteTime > 0f;
                bool lockedToSameWall =
                    sameWallJumpLockTimer > 0f &&
                    jumpWallDirection != 0 &&
                    jumpWallDirection == lastWallJumpDirection;

                bool consumedAirJump = false;

                // Check for wall jump with coyote time
                if (usingWallAssist && !lockedToSameWall && jumpWallDirection != 0 && (JumpTriggered && UnlockTracker.IsAbilityUnlocked(wallJump)))
                {
                    base.ChangeY = JumpStrength * 0.95f; // Slightly reduced jump strength for wall jumps to balance the added mobility
                    wallCoyoteTime = 0f; // Reset wall coyote time after wall jumping
                    JustWallJumped = true; // Set to true to prevent repeat wall jumps
                    wallBoostLength = 15f; // prevent immediate movement back toward the wall
                    ExtraX += jumpWallDirection * WallBoostStrength; // Apply horizontal boost away from the wall
                    // After jumping off the wall, clear wall cling state and lock same-side re-jumps briefly
                    IsOnWall = false;
                    wallContactFrames = 0;
                    lastWallJumpDirection = jumpWallDirection;
                    sameWallJumpLockTimer = SAME_WALL_JUMP_LOCK_FRAMES;
                    // Prevent dashing back towards the wall for ~1 second (60 frames at 60fps)
                    wallDashLockTimer = 60f;
                    wallDashLockDirection = jumpWallDirection; // Lock the direction the player jumped from
                    audioManager.PlayAudio("jumpSfx", true, "null", false); //Play jump sound effect
                    consumedAirJump = true;
                    performedWallJumpThisFrame = true;
                }
                // Check for double jump (only when wall jump is not currently available)
                else
                {
                    var doubleJumpItem = GetActiveDoubleJumpItem();
                    if (!usingWallAssist && doubleJumpItem != null && doubleJumpItem.TryDoubleJump(this, JumpTriggered, wasOnGroundLastFrame))
                    {
                        base.ChangeY = JumpStrength; // Same jump strength for double jump
                        audioManager.PlayAudio("jumpSfx", true, "null", false); //Play jump sound effect
                        consumedAirJump = true;
                    }
                }
#if DEBUG
                if (!consumedAirJump && GetDevModeItem()?.IsActive == true && JumpTriggered)
                {
                    base.ChangeY = JumpStrength; // DEV: infinite jumps
                    audioManager.PlayAudio("jumpSfx", true, "null", false); //Play jump sound effect
                }
#endif
            }

            // Dashing with coyote time (require explicit left/right input)
            if (InputManager.GameDash() && (InputManager.GameMoveLeft() || InputManager.GameMoveRight()))
            {
                var dash = playerAbilityNames[0];
                if (UnlockTracker.IsAbilityUnlocked(dash))
                {
                    // Check if player is trying to dash back towards a locked wall
                    bool dashingTowardLockedWall = wallDashLockTimer > 0f && wallDashLockDirection != 0 &&
                        ((wallDashLockDirection == 1 && moveDirection < 0) || (wallDashLockDirection == -1 && moveDirection > 0));

                    if (!dashingTowardLockedWall)
                    {
                        ExtraX = MathHelper.Clamp(ExtraX + DashStrength * moveDirection, -DASH_EXTRA_SPEED_CAP, DASH_EXTRA_SPEED_CAP);
                        InputManager.ConsumeDash();
                    }
                }
            }

            // If player inputs opposite direction to current ExtraX (dash/wall boost), reduce ExtraX so player can slow/stop the dash
            if (!performedWallJumpThisFrame && Math.Abs(ExtraX) > 0.1f && moveDirection != 0)
            {
                if (Math.Sign(ExtraX) != Math.Sign(moveDirection))
                {
                    float opposeStrength = DashStrength * 0.6f; // How quickly opposing input cancels ExtraX
                    ExtraX += moveDirection * opposeStrength;
                    // Prevent overshoot reversing ExtraX immediately; clamp toward zero
                    if (Math.Sign(ExtraX) == Math.Sign(moveDirection))
                    {
                        ExtraX = 0f;
                    }
                }
            }

            ChangeX += ExtraX; // Apply extra x movement from dashing or wall boosts
            ChangeX = MathHelper.Clamp(ChangeX, -DASH_TOTAL_SPEED_CAP, DASH_TOTAL_SPEED_CAP);
        }

        protected override void OnEntityCollision(Entity other)
        {
            switch (other)
            {
                default:
                    break;
                case Projectile projectile:
                    // Only take damage from enemy projectiles, not from player-fired ones
                    if (projectile.FiredBy == Owner.Enemy && InvincibilityFrames <= 0f)
                    {
#if DEBUG
                        if (GetDevModeItem()?.IsActive == true)
                            break;

                        audioManager.PlayAudio("collisionSfx", true, "null", false); //Play collision sound effect
#endif
                        Health -= projectile.Damage;
                        if (Health <= 0)
                        {
                            TryAbsorbLethalHit();
                        }

                        // Apply knockback in the direction the projectile was travelling
                        Vector2 projectileVelocity = new Vector2(projectile.XSpeed, projectile.YSpeed);
                        if (projectileVelocity != Vector2.Zero)
                        {
                            projectileVelocity.Normalize();
                            ChangeX = projectileVelocity.X * 10f;
                            base.ChangeY = projectileVelocity.Y * 10f - 5f; // extra upward bias
                        }

                        InvincibilityFrames = 60f;

                        // Despawn the projectile on contact
                        projectile.Lifetime = 0f;
                    }
                    break;
                case BerserkEnemy:
                    // Only take damage if not invincible
                    if (InvincibilityFrames <= 0f)
                    {
#if DEBUG
                        if (GetDevModeItem()?.IsActive == true)
                        {
                            break;
                        }
#endif
                        audioManager.PlayAudio("collisionSfx", true, "null", false); //Play collision sound effect
                        // Take 40 damage
                        Health -= 40;
                        if (Health <= 0)
                        {
                            TryAbsorbLethalHit();
                        }

                        // Calculate knockback direction (away from enemy)
                        float knockbackX = position.X < other.position.X ? -10f : 10f;
                        float knockbackY = -20f;

                        // Apply knockback
                        ChangeX = knockbackX;
                        ChangeY = knockbackY / 2; // Reduced vertical knockback for better feel

                        // Set invincibility frames (roughly 1 second at 60fps)
                        InvincibilityFrames = 60f;
                    }
                    break;
                case Enemy:
                    // Only take damage if not invincible
                    if (InvincibilityFrames <= 0f)
                    {
#if DEBUG
                        if (GetDevModeItem()?.IsActive == true)
                        {
                            break;
                        }
#endif
                        audioManager.PlayAudio("collisionSfx", true, "null", false); //Play collision sound effect
                        // Take 40 damage
                        Health -= 40;
                        if (Health <= 0)
                        {
                            TryAbsorbLethalHit();
                        }

                        // Calculate knockback direction (away from enemy)
                        float knockbackX = position.X < other.position.X ? -10f : 10f;
                        float knockbackY = -20f;

                        // Apply knockback
                        ChangeX = knockbackX;
                        base.ChangeY = knockbackY / 2; // Reduced vertical knockback for better feel

                        // Set invincibility frames (roughly 1 second at 60fps)
                        InvincibilityFrames = 60f;
                    }
                    break;
            }
        }

        private void HandleWallCling()
        {
            if (CollisionMap != null)
            {
                float probeTopY = position.Y + size[1] * WALL_CLING_PROBE_TOP_RATIO;
                float probeBottomY = position.Y + size[1] * WALL_CLING_PROBE_BOTTOM_RATIO;
                float leftProbeX = position.X - 1f;
                float rightProbeX = position.X + size[0] + 1f;

                var wallJump = playerAbilityNames[1];

                if (!IsOnGround)
                {
                    // Smaller cling zone: only side probes along the torso count as valid wall contact.
                    bool leftWall =
                        IsWallJumpSurfaceAtWorldPoint(leftProbeX, probeTopY) ||
                        IsWallJumpSurfaceAtWorldPoint(leftProbeX, probeBottomY);
                    bool rightWall =
                        IsWallJumpSurfaceAtWorldPoint(rightProbeX, probeTopY) ||
                        IsWallJumpSurfaceAtWorldPoint(rightProbeX, probeBottomY);

                    // Cling should only engage while actively holding into the wall.
                    int horizontalInput = 0;
                    if (InputManager.GameMoveLeft() && !InputManager.GameMoveRight())
                    {
                        horizontalInput = -1;
                    }
                    else if (InputManager.GameMoveRight() && !InputManager.GameMoveLeft())
                    {
                        horizontalInput = 1;
                    }

                    bool movingIntoLeft = leftWall && horizontalInput < 0;
                    bool movingIntoRight = rightWall && horizontalInput > 0;

                    // Only count contact frames when player is both colliding with a wall and moving into it
                    if ((movingIntoLeft || movingIntoRight) && UnlockTracker.IsAbilityUnlocked(wallJump))
                    {
                        wallContactFrames = Math.Min(WALL_CONTACT_REQUIRED, wallContactFrames + 1);
                    }
                    else
                    {
                        wallContactFrames = Math.Max(0, wallContactFrames - 1);
                    }

                    // Determine whether we should allow a cling.
                    // Normal cling requires sustained contact frames, but we also allow
                    // a short grace period (wall coyote time) so players can still
                    // cling / re-cling for a few frames after stepping off the wall
                    // as long as they're pressing into the wall.
                    bool pressingIntoWall = movingIntoLeft || movingIntoRight;
                    bool clingFromCoyote = wallCoyoteTime > 0f && pressingIntoWall;

                    if ((wallContactFrames >= WALL_CONTACT_REQUIRED || clingFromCoyote) && UnlockTracker.IsAbilityUnlocked(wallJump))
                    {
                        IsOnWall = true;

                        // Determine wall direction for wall jump boost direction
                        if (leftWall && !rightWall)
                        {
                            wallDirection = 1; // Left wall
                        }
                        else if (rightWall && !leftWall)
                        {
                            wallDirection = -1; // Right wall
                        }
                        else if (clingFromCoyote && wallCoyoteDirection != 0)
                        {
                            // If we're relying on coyote grace but no tile probe currently
                            // indicates a wall, fall back to the last known wall direction
                            wallDirection = wallCoyoteDirection;
                        }
                        else
                        {
                            wallDirection = 0; // Both sides solid, no directional bias
                        }
                    }
                    else
                    {
                        IsOnWall = false;
                    }
                }
                else
                {
                    IsOnWall = false;

                    // Set wall direction to 0 after wall boost length expires to allow normal movement again
                    if (wallBoostLength <= 0f)
                    {
                        wallDirection = 0;
                    }
                }

                if (IsOnGround)
                {
                    JustWallJumped = false; // Reset wall jump when touching the ground
                    wallBoostLength = 0f;
                    wallContactFrames = 0;
                    wallClingFrames = 0f;
                    sameWallJumpLockTimer = 0f;
                    lastWallJumpDirection = 0;
                    wallCoyoteDirection = 0;
                    wallDashLockTimer = 0f; // Allow dashing normally when on ground
                    wallDashLockDirection = 0;
                }
            }
        }

        private bool IsWallJumpSurfaceAtWorldPoint(float worldX, float worldY)
        {
            int tileX = (int)(worldX / TILE_SIZE);
            int tileY = (int)(worldY / TILE_SIZE);
            return IsWallJumpSurface(new Vector2(tileX, tileY));
        }

        private bool IsWallJumpSurface(Vector2 tileKey)
        {
            if (CollisionMap == null)
            {
                return false;
            }

            if (!CollisionMap.TryGetValue(tileKey, out int tileId) || tileId == -1)
            {
                return false;
            }

            CollisionType collisionType = CollisionTypeMapper.GetCollisionType(tileId);
            return collisionType != CollisionType.None && collisionType != CollisionType.SemiSolid;
        }

        private int ResolveWallJumpDirection()
        {
            if (IsOnWall && wallDirection != 0)
            {
                return wallDirection;
            }

            if (wallCoyoteTime > 0f && wallCoyoteDirection != 0)
            {
                return wallCoyoteDirection;
            }

            if (CollisionMap != null)
            {
                int playerTileX = (int)((position.X + size[0] / 2f) / TILE_SIZE);
                int playerTileY = (int)((position.Y + size[1] / 2f) / TILE_SIZE);

                var keyLeft = new Vector2(playerTileX - 1, playerTileY);
                var keyRight = new Vector2(playerTileX + 1, playerTileY);

                bool leftWall = IsWallJumpSurface(keyLeft);
                bool rightWall = IsWallJumpSurface(keyRight);

                if (leftWall && !rightWall)
                {
                    return 1;
                }

                if (rightWall && !leftWall)
                {
                    return -1;
                }

                if (leftWall && rightWall)
                {
                    if (InputManager.GameMoveLeft() && !InputManager.GameMoveRight())
                    {
                        return 1;
                    }

                    if (InputManager.GameMoveRight() && !InputManager.GameMoveLeft())
                    {
                        return -1;
                    }

                    if (ChangeX < -0.1f)
                    {
                        return 1;
                    }

                    if (ChangeX > 0.1f)
                    {
                        return -1;
                    }

                    if (lastWallJumpDirection != 0)
                    {
                        return -lastWallJumpDirection;
                    }
                }
            }

            return 0;
        }

        protected override void PhysicsUpdate(float deltaTime)
        {
            wasOnBouncyTile = isOnBouncyTile; // Remember previous bouncy-ground state
            isOnBouncyTile = false; // Reset each frame; collision handling sets it when standing on bounce tiles

            // Wall cling descent ramp: start slow, then ease back to normal gravity behavior over time.
            if (IsOnWall)
            {
                wallClingFrames = Math.Min(WALL_CLING_RAMP_FRAMES, wallClingFrames + 1f);

                if (ChangeY > 0f)
                {
                    float clingT = MathHelper.Clamp(wallClingFrames / WALL_CLING_RAMP_FRAMES, 0f, 1f);
                    float currentSlowFactor = MathHelper.Lerp(WALL_CLING_START_SLOW_FACTOR, 1f, clingT);
                    float currentSpeedCap = MathHelper.Lerp(WALL_CLING_START_SPEED_CAP, 70f, clingT);

                    ChangeY *= currentSlowFactor;
                    ChangeY = Math.Min(ChangeY, currentSpeedCap);
                }
            }
            else
            {
                wallClingFrames = 0f;
            }

            base.PhysicsUpdate(deltaTime);
        }

        protected override void OnVerticalMovementComplete(float deltaTime)
        {
            // Player-specific vertical gap centering (stronger than base)
            if (Math.Abs(base.ChangeY) > 0.1f && CollisionMap != null)
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
            if ((IsOnGround || coyoteTime > 0f) && InputManager.GameDrop() && CollisionMap != null)
            {
                int playerTileX = (int)((position.X + size[0] / 2f) / TILE_SIZE);
                int playerTileY = (int)((position.Y + size[1] / 2f) / TILE_SIZE);

                // Check if there's a semi-solid platform directly below
                var keyBelow = new Vector2(playerTileX, playerTileY + 1);
                bool semiSolidBelow = CollisionMap.TryGetValue(keyBelow, out int belowTileId) &&
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
                {
                    nudge = offset;
                }

                position.X += nudge;
            }
        }

        private DoubleJumpItem? GetActiveDoubleJumpItem()
        {
            return itemInventory.OfType<DoubleJumpItem>().FirstOrDefault(item => item.IsActive);
        }

        private HealingGlowItem? GetActiveHealingGlowItem()
        {
            return itemInventory.OfType<HealingGlowItem>().FirstOrDefault(item => item.IsActive);
        }

        private IronWillItem? GetReadyIronWillItem()
        {
            return itemInventory.OfType<IronWillItem>().FirstOrDefault(item => item.IsReady);
        }

#if DEBUG
        private PolyGone.Items.DevModeItem? GetDevModeItem()
        {
            return itemInventory.OfType<PolyGone.Items.DevModeItem>().FirstOrDefault(i => i.IsActive);
        }
#endif

        /// <summary>If Iron Will is ready, survive the killing blow at 1 HP.</summary>
        private void TryAbsorbLethalHit()
        {
            var ironWill = GetReadyIronWillItem();
            if (ironWill != null && ironWill.TryAbsorbLethalHit())
            {
                Health = 1;
            }
        }

        // Helper method to get the currently equipped blaster/weapon
        public Blaster? GetBlaster()
        {
            return currentWeapon as Blaster;
        }

        // Public property to access current blaster for backward compatibility
        public Blaster? Blaster => GetBlaster();

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
            HandleWallCling();

            base.Update(gameTime);

            HandleInput();
            // Update coyote time after physics update to use current frame's ground state
            coyoteTime = IsOnGround
                ? 6f // 0.1 seconds at 60fps
                : Math.Max(0f, coyoteTime - 1f);

            // Update wall coyote time after physics update to use current frame's wall state
            if (IsOnWall)
            {
                wallCoyoteTime = 6f; // 0.1 seconds at 60fps
                wallCoyoteDirection = wallDirection;
            }
            else
            {
                wallCoyoteTime = Math.Max(0f, wallCoyoteTime - 1f);
                if (wallCoyoteTime <= 0f)
                {
                    wallCoyoteDirection = 0;
                }
            }

            // Update wall boost length after physics update to use current frame's wall state
            wallBoostLength = IsOnWall
                ? 15f // 0.25 seconds at 60fps
                : Math.Max(0f, wallBoostLength - 1f);

            sameWallJumpLockTimer = Math.Max(0f, sameWallJumpLockTimer - 1f);
            wallDashLockTimer = Math.Max(0f, wallDashLockTimer - 1f);

            // Update only the currently equipped weapon
            var currentBlaster = GetBlaster();
            if (currentBlaster != null)
            {
#if DEBUG
                int bulletCountBefore = Bullets.Count;
#endif
                currentBlaster.Follow(new Rectangle((int)position.X, (int)position.Y, size[0], size[1]), cameraOffset);
                currentBlaster.Use();
                currentBlaster.Update(gameTime);
#if DEBUG
                if (GetDevModeItem()?.IsActive == true)
                {
                    for (int i = bulletCountBefore; i < Bullets.Count; i++)
                    {
                        Bullets[i].IsInstantKill = true;
                    }
                }
#endif
            }

            // Update all bullets (shared across all weapons) - iterate backwards for safe removal
            for (int i = Bullets.Count - 1; i >= 0; i--)
            {
                var bullet = Bullets[i];
                bullet.Lifetime -= 1f;
                if (bullet.Lifetime <= 0f)
                {
                    Bullets.RemoveAt(i);
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
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 offset)
        {
            // Check if we should draw with healing glow
            var healingGlow = GetActiveHealingGlowItem();
            if (healingGlow?.ShouldGlow() == true)
            {
                // Draw glow outline first (behind the player) - 68x68 total size
                Color glowColor = healingGlow.GetGlowColor();
                for (int x = -4; x <= 4; x++)
                {
                    for (int y = -4; y <= 4; y++)
                    {
                        if (x == 0 && y == 0)
                        {
                            continue; // Skip center
                        }

                        Vector2 glowOffset = new Vector2(x, y);
                        spriteBatch.Draw(texture, position - offset + HitboxOffset + glowOffset, srcRect, glowColor);
                    }
                }
            }

            base.Draw(spriteBatch, offset);

            // Draw only the currently equipped weapon
            var currentBlaster = GetBlaster();
            currentBlaster?.Draw(spriteBatch, offset);

            // Draw all bullets (shared across all weapons)
            foreach (var bullet in Bullets)
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
