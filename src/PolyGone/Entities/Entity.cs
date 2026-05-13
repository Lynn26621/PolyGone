using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PolyGone.Core;
using PolyGone.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PolyGone;

public class Entity : Sprite
{

    protected readonly Dictionary<Vector2, int>? CollisionMap;
    protected float ChangeX;
    protected float ChangeY;
    protected float ExtraX; //Added to help smooth dashing
    protected bool IsOnGround;
    public int Health;
    public readonly int MaxHealth;
    protected float InvincibilityFrames;
    // Horizontal damping model (units of velocity reduced per frame at 60 FPS)
    protected float GroundDeceleration;
    protected float AirDeceleration;
    protected float SlipperyGroundDeceleration;
    protected float ExtraVelocityDeceleration;
    protected float StopSpeedThreshold;
    protected float SlipperyControlMultiplier;
    protected readonly int[] VisualSize; // Visual size for drawing (can be larger than hitbox)
    protected Vector2 HitboxOffset; // Offset to center the hitbox within the visual sprite
    public bool IsAlive = true;
    private AudioManager audioManager;
    /// <summary>Multiplier applied to gravity each physics tick. 1 = normal, lower = floatier.</summary>
    protected float GravityScale = 1f;
    protected bool IsOnSlipperyTile = false;

    // Constants for tile-based calculations
    protected const int TILE_SIZE = 64;
    protected const int TILE_HALF_SIZE = TILE_SIZE / 2;


    public Entity(Texture2D texture, Vector2 position, AudioManager audioManager, int[] size, int health = 100, Color color = default, Rectangle? srcRect = null, Dictionary<Vector2, int>? CollisionMap = null, int[]? visualSize = null)
        : base(texture, position, size, color, srcRect)
    {
        this.CollisionMap = CollisionMap;
        this.ChangeX = 0f;
        this.ChangeY = 0f;
        this.IsOnGround = false;
        this.Health = health;
        this.MaxHealth = health; // Set max health to initial health
        this.InvincibilityFrames = 0f;
        this.GroundDeceleration = 1.4f;
        this.AirDeceleration = 0.2f;
        this.SlipperyGroundDeceleration = 0.25f;
        this.ExtraVelocityDeceleration = 1.8f;
        this.StopSpeedThreshold = 0.05f;
        this.SlipperyControlMultiplier = 0.35f;
        this.VisualSize = visualSize ?? size; // Use provided visual size or default to hitbox size
        // Calculate offset so hitbox bottom aligns with visual bottom
        this.HitboxOffset = new Vector2(
            (this.VisualSize[0] - size[0]) / 2f, // Center horizontally
            this.VisualSize[1] - size[1] // Align bottom edges
        );
        this.audioManager = audioManager;
        ExtraX = 0f;
    }

    protected virtual List<(Rectangle, CollisionType)> GetIntersectingTiles(Rectangle target)
    {
        var intersectingTiles = new List<(Rectangle, CollisionType)>();
        if (CollisionMap == null)
        {
            return intersectingTiles;
        }

        foreach (var tile in CollisionMap)
        {
            if (tile.Value == -1)
            {
                continue;
            }

            Rectangle tileRect = new Rectangle((int)tile.Key.X * TILE_SIZE, (int)tile.Key.Y * TILE_SIZE, TILE_SIZE, TILE_SIZE);
            CollisionType colType = CollisionTypeMapper.GetCollisionType(tile.Value);
            if (target.Intersects(tileRect))
            {
                intersectingTiles.Add((tileRect, colType));
            }
        }
        return intersectingTiles;
    }

    // Helper method to check if a tile is a solid wall (not semi-solid platform)
    protected bool IsSolidWall(Vector2 tileKey)
    {
        if (CollisionMap == null)
        {
            return false;
        }

        if (!CollisionMap.TryGetValue(tileKey, out int tileId) || tileId == -1)
        {
            return false;
        }

        CollisionType colType = CollisionTypeMapper.GetCollisionType(tileId);
        return colType == CollisionType.Solid || colType == CollisionType.Damage || colType == CollisionType.Slippery;
    }

    protected virtual void HandleVerticalCollision(ref bool onGround, ref float deltaY, List<(Rectangle, CollisionType)> collisions)
    {
        var (tileRect, colType) = collisions[0];
        switch (colType)
        {
            default:
            case CollisionType.Solid:
                position.Y = deltaY > 0 ? tileRect.Top - size[1] : tileRect.Bottom;
                onGround = deltaY > 0;  // Check deltaY before setting it to 0
                deltaY = 0;
                break;
            case CollisionType.SemiSolid:
                if (deltaY > 0 && (position.Y + size[1]) <= tileRect.Top + 10)
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
                IsOnSlipperyTile = true;
                break;
            case CollisionType.Bouncy: //Keep track of velocity. Reverse it when colliding with bouncy tile top.
                if (deltaY > 0)
                {
                    position.Y = tileRect.Top - size[1];
                    deltaY = -deltaY; // Reverse velocity
                    onGround = false;
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

    protected virtual void HandleHorizontalCollision(ref float deltaX, List<(Rectangle, CollisionType)> collisions)
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
            case CollisionType.SemiSolid:
                position.X += deltaX;
                break;
            case CollisionType.Damage:
                position.X = deltaX > 0 ? tileRect.Left - size[0] : tileRect.Right;
                deltaX = 0;
                TakeDamage(10);
                break;
        }
    }

    protected bool IsStandingOnCollisionType(CollisionType collisionType)
    {
        if (CollisionMap == null)
        {
            return false;
        }

        int probeWidth = Math.Max(1, size[0] - 4);
        int probeX = (int)position.X + 2;
        int probeY = (int)(position.Y + size[1] - 2);
        Rectangle groundProbe = new Rectangle(probeX, probeY, probeWidth, 4);

        return GetIntersectingTiles(groundProbe).Any(tile => tile.Item2 == collisionType);
    }

    protected virtual List<Entity> GetIntersectingEntities(Rectangle target, List<Entity> others)
    {
        var intersectingEntities = new List<Entity>();
        foreach (var other in others)
        {
            if (other == this)
            {
                continue;
            }

            Rectangle otherRect = other.Rectangle;
            if (target.Intersects(otherRect))
            {
                intersectingEntities.Add(other);
            }
        }
        return intersectingEntities;
    }

    protected virtual void OnEntityCollision(Entity other)
    {
        // Default implementation does nothing
        // Override in derived classes to handle specific collision behavior
    }


    public virtual void HandleDeath()
    {
        // Default implementation marks entity as not alive
        IsAlive = false;
        audioManager.PlayAudio("deathSfx", true, "null", false); // Play death sound effect
    }

    // Physics and collision update for non-player entities (no input)
    protected virtual void PhysicsUpdate(float deltaTime)
    {
        IsOnSlipperyTile = false; // Reset each frame, collision handlers will set it if needed

        // Apply gravity
        // Stronger gravity when moving slower to create a more responsive feel, but allow for slower falling if the entity is already moving down quickly
        if (ChangeY < 20f)
        {
            ChangeY += 0.7f * GravityScale;
            // Weaker gravity when past 20f to create a floaty terminal velocity effect, but still allow for faster falling if needed for bouncy tiles
        }
        else
        {
            ChangeY += 0.3f * GravityScale;
        }
        ChangeY = Math.Clamp(ChangeY, -70f, 70f); // Terminal velocity cap

        // Handle vertical movement and collisions
        HandleVerticalMovement(deltaTime);

        // Handle horizontal movement and collisions
        HandleHorizontalMovement(deltaTime);

        // Apply horizontal damping after movement/collision resolution.
        ApplyFriction(deltaTime);
    }

    protected virtual void HandleVerticalMovement(float deltaTime)
    {
        IsOnGround = false;
        float nextY = position.Y + (ChangeY * deltaTime);
        Rectangle nextRectY = new Rectangle((int)position.X, (int)nextY, size[0], size[1]);
        var verticalCollisions = GetIntersectingTiles(nextRectY);

        if (verticalCollisions.Count > 0)
        {
            verticalCollisions = verticalCollisions
                .OrderBy(c => Math.Abs(ChangeY > 0 ? c.Item1.Top - (position.Y + size[1]) : c.Item1.Bottom - position.Y))
                .ThenByDescending(c => c.Item2 == CollisionType.Solid ? 1 : 0)
                .ToList();
            HandleVerticalCollision(ref IsOnGround, ref ChangeY, verticalCollisions.Take(1).ToList());
        }
        else
        {
            position.Y = nextY;
            OnVerticalMovementComplete(deltaTime);
        }
    }

    protected virtual void HandleHorizontalMovement(float deltaTime)
    {
        float nextX = position.X + (ChangeX * deltaTime);
        Rectangle nextRectX = new Rectangle((int)nextX, (int)position.Y, size[0], size[1]);
        var horizontalCollisions = GetIntersectingTiles(nextRectX);


        if (horizontalCollisions.Count > 0)
        {
            horizontalCollisions = horizontalCollisions
                .OrderBy(c => Math.Abs(ChangeX > 0 ? c.Item1.Left - (position.X + size[0]) : c.Item1.Right - position.X))
                .ToList();
            HandleHorizontalCollision(ref ChangeX, horizontalCollisions.Take(1).ToList());
        }
        else
        {
            position.X = nextX;
            OnHorizontalMovementComplete(deltaTime);
        }
    }

    protected virtual void OnVerticalMovementComplete(float deltaTime)
    {
        // Default implementation does nothing
        // Override in derived classes for specific behavior
    }

    protected virtual void OnHorizontalMovementComplete(float deltaTime)
    {
        // Gap centering: when moving horizontally through a 1-tile vertical gap, center the entity
        if (Math.Abs(ChangeX) > 0.5f && CollisionMap != null)
        {
            // Check for walls above and below to detect a 1-tile gap
            int tileAbove = (int)((position.Y - 1f) / TILE_SIZE);
            int tileBelow = (int)((position.Y + size[1] + 1f) / TILE_SIZE);
            int currentTileX = (int)((position.X + size[0] / 2f) / TILE_SIZE);

            var keyAbove = new Vector2(currentTileX, tileAbove);
            var keyBelow = new Vector2(currentTileX, tileBelow);

            bool wallAbove = IsSolidWall(keyAbove);
            bool wallBelow = IsSolidWall(keyBelow);

            // If there are walls above and below (1-tile gap), center horizontally in the tile
            if (wallAbove && wallBelow)
            {
                float tileCenter = currentTileX * TILE_SIZE + TILE_HALF_SIZE;
                float playerCenter = position.X + size[0] / 2f;
                float offset = tileCenter - playerCenter;

                // Gently nudge toward center (max 1 pixel per frame)
                if (Math.Abs(offset) > 1f)
                {
                    position.X += Math.Sign(offset) * 1f;
                }
                else if (Math.Abs(offset) > 0.1f)
                {
                    position.X += offset;
                }
            }
        }
    }

    protected virtual void ApplyFriction(float deltaTime)
    {
        bool onSlipperyGround = IsOnGround && IsStandingOnCollisionType(CollisionType.Slippery);
        float groundDecel = onSlipperyGround ? SlipperyGroundDeceleration : GroundDeceleration;
        float horizontalDecel = IsOnGround ? groundDecel : AirDeceleration;

        ChangeX = MoveTowards(ChangeX, 0f, horizontalDecel * deltaTime);
        if (Math.Abs(ChangeX) <= StopSpeedThreshold)
        {
            ChangeX = 0f;
        }

        ExtraX = MoveTowards(ExtraX, 0f, ExtraVelocityDeceleration * deltaTime);
        if (Math.Abs(ExtraX) <= StopSpeedThreshold)
        {
            ExtraX = 0f;
        }
    }

    protected void ConfigureHorizontalDamping(
        float groundDeceleration,
        float airDeceleration,
        float slipperyGroundDeceleration,
        float extraVelocityDeceleration,
        float stopSpeedThreshold = 0.05f,
        float slipperyControlMultiplier = 0.35f)
    {
        GroundDeceleration = Math.Max(0f, groundDeceleration);
        AirDeceleration = Math.Max(0f, airDeceleration);
        SlipperyGroundDeceleration = Math.Max(0f, slipperyGroundDeceleration);
        ExtraVelocityDeceleration = Math.Max(0f, extraVelocityDeceleration);
        StopSpeedThreshold = Math.Max(0f, stopSpeedThreshold);
        SlipperyControlMultiplier = Math.Clamp(slipperyControlMultiplier, 0f, 1f);
    }

    protected static float MoveTowards(float current, float target, float maxDelta)
    {
        if (maxDelta <= 0f)
        {
            return current;
        }

        float delta = target - current;
        if (Math.Abs(delta) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(delta) * maxDelta;
    }

    protected void ApplyHorizontalIntent(float direction, float maxSpeed, float acceleration, float reverseAccelerationFactor = 1f)
    {
        if (Math.Abs(direction) < 0.001f)
        {
            return;
        }

        float intent = Math.Sign(direction);
        float targetSpeed = intent * Math.Abs(maxSpeed);
        float appliedAcceleration = Math.Max(0f, acceleration);

        if (Math.Abs(ChangeX) > StopSpeedThreshold && Math.Sign(ChangeX) != Math.Sign(intent))
        {
            appliedAcceleration *= Math.Max(0f, reverseAccelerationFactor);
        }

        if (IsOnGround && IsStandingOnCollisionType(CollisionType.Slippery))
        {
            appliedAcceleration *= SlipperyControlMultiplier;
        }

        ChangeX = MoveTowards(ChangeX, targetSpeed, appliedAcceleration);
    }

    public void EntityCollisionUpdate(List<Entity> others)
    {
        Rectangle currentRect = new Rectangle((int)position.X, (int)position.Y, size[0], size[1]);
        var intersectingEntities = GetIntersectingEntities(currentRect, others);
        foreach (var other in intersectingEntities)
        {
            OnEntityCollision(other);
        }
    }

    // Check if there's ground ahead in the movement direction
    protected virtual bool IsGroundAhead(float direction)
    {
        if (CollisionMap == null)
        {
            return true;
        }

        // Check from the bottom corner in the direction of movement
        float checkX = direction > 0
            ? position.X + size[0] + 1f   // Moving right: check from bottom-right corner, slightly ahead
            : position.X - 1f;            // Moving left: check from bottom-left corner, slightly ahead (to the left)

        float checkY = position.Y + size[1] + 1f; // Just below feet

        // Check if there's a tile at that position
        Rectangle checkRect = new Rectangle((int)checkX, (int)checkY, 1, 1);
        var tiles = GetIntersectingTiles(checkRect);

        return tiles.Count > 0;
    }

    public void TakeDamage(int damage, float invincibilityDuration = 60f)
    {
        if (InvincibilityFrames > 0)
        {
            return; // Currently invincible, ignore damage
        }

        Health -= damage;
        InvincibilityFrames = invincibilityDuration; // Set invincibility frames after taking damage
    }

    public virtual void UseItems(List<Item> items)
    {
        // Default implementation does nothing
        // Override in derived classes to handle item usage
    }

    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)Math.Round(gameTime.ElapsedGameTime.TotalSeconds * 60f, 3);
        PhysicsUpdate(deltaTime);
        InvincibilityFrames = Math.Max(0f, InvincibilityFrames - 1f);
        if (Health <= 0)
        {
            HandleDeath();
        }
        base.Update(gameTime);
    }


    public override void Draw(SpriteBatch spriteBatch, Vector2 offset)
    {
        // Draw using visual size, offset by hitboxOffset to center the sprite on the hitbox
        // Round positions only for drawing to prevent sub-pixel rendering issues
        float drawX = (float)Math.Floor(position.X - HitboxOffset.X - offset.X);
        float drawY = (float)Math.Floor(position.Y - HitboxOffset.Y - offset.Y);

        // Check if visual sprite would clip through tiles and adjust if needed
        if (CollisionMap != null)
        {
            Rectangle visualRect = new Rectangle(
                (int)(position.X - HitboxOffset.X),
                (int)(position.Y - HitboxOffset.Y),
                VisualSize[0],
                VisualSize[1]
            );

            var visualCollisions = GetIntersectingTiles(visualRect);

            // Adjust drawing position to prevent clipping
            foreach (var (tileRect, colType) in visualCollisions)
            {
                // Only adjust for solid collision types
                if (colType == CollisionType.Solid || colType == CollisionType.Damage || colType == CollisionType.Slippery)
                {
                    // Check if hitbox is not colliding (meaning only visual extends into tile)
                    Rectangle hitboxRect = new Rectangle((int)position.X, (int)position.Y, size[0], size[1]);
                    if (!hitboxRect.Intersects(tileRect))
                    {
                        // Adjust Y if visual top extends above the tile
                        float visualTop = position.Y - HitboxOffset.Y;
                        if (visualTop < tileRect.Bottom && position.Y >= tileRect.Bottom)
                        {
                            drawY = tileRect.Bottom - offset.Y;
                        }

                        // Adjust X if visual extends into tile horizontally
                        float visualLeft = position.X - HitboxOffset.X;
                        float visualRight = visualLeft + VisualSize[0];

                        if (visualLeft < tileRect.Right && position.X >= tileRect.Right)
                        {
                            drawX = tileRect.Right - offset.X;
                        }
                        else if (visualRight > tileRect.Left && (position.X + size[0]) <= tileRect.Left)
                        {
                            drawX = tileRect.Left - VisualSize[0] - offset.X;
                        }
                    }
                }
            }
        }

        Rectangle adjustedRectangle = new Rectangle(
            (int)drawX,
            (int)drawY,
            VisualSize[0],
            VisualSize[1]
        );
        spriteBatch.Draw(texture, adjustedRectangle, srcRect, color);
    }


}
