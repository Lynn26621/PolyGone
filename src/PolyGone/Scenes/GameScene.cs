using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.IO;
using System.Text.Json;
using System.Linq;
using System;
using PolyGone.Entities;
using PolyGone.Graphics;

namespace PolyGone;

public class GameScene : IScene
{
    private ContentManager contentManager;
    private Texture2D texture;
    private SpriteFont hudFont;
    private SceneManager sceneManager;
    private Player player;
    private FollowCamera camera;
    private GameUI gameUI;
    private readonly GraphicsDeviceManager graphics;
    private Dictionary<Vector2, int> tileMap = null!;
    private Dictionary<Vector2, int> collisionMap = null!;
    private List<Rectangle> textureStore;
    private Vector2 playerPos;
    private bool playerSpawnFound = false;
    private readonly List<Vector2> enemySpawns = new(); // Store enemy spawn positions
    private readonly List<Vector2> turretEnemySpawns = new(); // Store turret spawn positions
    private readonly List<Entity> enemies = new(); // Placeholder for enemy list
    private readonly List<TurretEnemy> turretEnemies = new(); // Stationary blaster enemies
    private readonly List<Projectile> orphanedTurretBullets = new(); // Bullets that outlive their turret
    private GoalTrigger goalTrigger; // Win condition trigger
    private Gate gate; //  Gate object that can be opened/closed by triggers 
    private readonly List<Gate> gates = new(); // List to support multiple gates in a level
    private bool levelComplete = false;
    private bool gameOver = false;
    private readonly List<ItemType> selectedItems;
    private readonly List<BlasterAttachmentType> selectedAttachments;
    private readonly string levelName;

    public GameScene(ContentManager contentManager, SceneManager sceneManager, GraphicsDeviceManager graphics, string levelName = "TestLevel", List<ItemType>? selectedItems = null, List<BlasterAttachmentType>? selectedAttachments = null)
    {       
        this.contentManager = contentManager;
        this.sceneManager = sceneManager;
        this.graphics = graphics;
        this.selectedItems = selectedItems ?? new List<ItemType>();
        this.selectedAttachments = selectedAttachments ?? new List<BlasterAttachmentType>();
        this.levelName = levelName;
        LoadMapFromJson("Maps/" + levelName + ".json");
        textureStore = GetTextureStore(32, new int[2] { 4, 4 });
    }

    // Public method to get the level name for restart functionality
    public string GetLevelName() => levelName;

    // Public methods to get the current loadout for restart functionality
    public List<ItemType> GetSelectedItems() => new List<ItemType>(selectedItems);
    public List<BlasterAttachmentType> GetSelectedAttachments() => new List<BlasterAttachmentType>(selectedAttachments);

    // Generates a list of rectangles representing individual textures in a texture atlas
    public List<Rectangle> GetTextureStore(int textureSize, int[] gridSize)
    {
        List<Rectangle> textureStore = new();
        for (int y = 0; y < gridSize[1]; y++)
        {
            for (int x = 0; x < gridSize[0]; x++)
            {
                textureStore.Add(new Rectangle(x * textureSize, y * textureSize, textureSize, textureSize));
            }
        }
        return textureStore;
    }

    // Adjusts coordinates from Tiled's coordinate system to the game's coordinate system
    public Vector2 AdjustCoordinates(float x, float y)
    {
        // Scale from 32px Tiled tiles to 64px game tiles (x2), then shift y up 64px because
        // Tiled stores object y at the bottom of the object while the game uses a top-left origin.
        return new Vector2(x * 2, y * 2 - 64);
    }

    // Loads tile and collision maps from a JSON file exported from Tiled
    public void LoadMapFromJson(string filepath)
    {
        playerSpawnFound = false;
        playerPos = Vector2.Zero;
        enemySpawns.Clear();
        turretEnemySpawns.Clear();
        gates.Clear();
        goalTrigger = null!;

        // Read and parse JSON file
        string jsonContent = File.ReadAllText(filepath);
        using JsonDocument doc = JsonDocument.Parse(jsonContent);

        // Get root and layers
        JsonElement root = doc.RootElement;
        JsonElement layers = root.GetProperty("layers");
        int mapWidth = root.TryGetProperty("width", out JsonElement widthElement)
            ? widthElement.GetInt32()
            : 0;

        tileMap = new Dictionary<Vector2, int>();
        collisionMap = new Dictionary<Vector2, int>();

        foreach (JsonElement layer in layers.EnumerateArray())
        {
            string? layerType = layer.TryGetProperty("type", out JsonElement layerTypeElement)
                ? layerTypeElement.GetString()
                : null;

            if (layerType == "tilelayer")
            {
                LoadTileLayer(layer, mapWidth);
            }
            else if (layerType == "objectgroup")
            {
                LoadObjectLayer(layer);
            }
        }

        // Validate that a player spawn was found
        if (!playerSpawnFound)
        {
            throw new InvalidOperationException(
                $"Map file '{filepath}' is missing a required Player object in an object layer. " +
                "Please ensure the map contains exactly one object with type='Player'."
            );
        }
    }

    private void LoadTileLayer(JsonElement layer, int fallbackWidth)
    {
        string? layerName = layer.TryGetProperty("name", out JsonElement layerNameElement)
            ? layerNameElement.GetString()
            : null;

        Dictionary<Vector2, int>? targetMap = layerName switch
        {
            "Tiles" => tileMap,
            "Collisions" => collisionMap,
            _ => null
        };

        if (targetMap == null)
        {
            return;
        }

        if (layer.TryGetProperty("data", out JsonElement dataArray))
        {
            ProcessTileData(dataArray, fallbackWidth, 0, 0, targetMap);
            return;
        }

        if (layer.TryGetProperty("chunks", out JsonElement chunksArray))
        {
            foreach (JsonElement chunk in chunksArray.EnumerateArray())
            {
                JsonElement chunkData = chunk.GetProperty("data");
                int chunkWidth = chunk.GetProperty("width").GetInt32();
                int chunkX = chunk.GetProperty("x").GetInt32();
                int chunkY = chunk.GetProperty("y").GetInt32();
                ProcessTileData(chunkData, chunkWidth, chunkX, chunkY, targetMap);
            }

            return;
        }

        throw new InvalidOperationException(
            $"Tile layer '{layerName ?? "<unnamed>"}' does not contain either a data array or chunk data."
        );
    }

    private void ProcessTileData(JsonElement dataArray, int width, int startX, int startY, Dictionary<Vector2, int> targetMap)
    {
        if (width <= 0)
        {
            throw new InvalidOperationException("Tile layer width must be greater than zero.");
        }

        int index = 0;
        foreach (JsonElement tile in dataArray.EnumerateArray())
        {
            int tileValue = tile.GetInt32();
            int x = startX + index % width;
            int y = startY + index / width;

            if (tileValue > 0)
            {
                // Tiled's firstgid is 1, so convert to a 0-based atlas index.
                targetMap[new Vector2(x, y)] = (tileValue - 1) % 16;
            }

            index++;
        }
    }

    private void LoadObjectLayer(JsonElement layer)
    {
        if (!layer.TryGetProperty("objects", out JsonElement objectsArray))
        {
            return;
        }

        foreach (JsonElement obj in objectsArray.EnumerateArray())
        {
            string? objType = obj.TryGetProperty("type", out JsonElement objTypeElement)
                ? objTypeElement.GetString()
                : null;

            if (objType == null ||
                !obj.TryGetProperty("x", out JsonElement xElement) ||
                !obj.TryGetProperty("y", out JsonElement yElement))
            {
                continue;
            }

            switch (objType)
            {
                case "Player":
                    playerPos = AdjustCoordinates(xElement.GetSingle(), yElement.GetSingle());
                    playerSpawnFound = true;
                    break;
                case "Enemy":
                    enemySpawns.Add(AdjustCoordinates(xElement.GetSingle(), yElement.GetSingle()));
                    break;
                case "TurretEnemy":
                    turretEnemySpawns.Add(AdjustCoordinates(xElement.GetSingle(), yElement.GetSingle()));
                    break;
                case "Goal":
                    if (obj.TryGetProperty("width", out JsonElement goalWidthElement) &&
                        obj.TryGetProperty("height", out JsonElement goalHeightElement))
                    {
                        Vector2 goalPos = AdjustCoordinates(xElement.GetSingle(), yElement.GetSingle());
                        int goalWidth = (int)(goalWidthElement.GetSingle() * 2);
                        int goalHeight = (int)(goalHeightElement.GetSingle() * 2);
                        GoalConditionType goalConditions = ParseGoalConditions(obj);
                        goalTrigger = new GoalTrigger(goalPos, goalWidth, goalHeight, goalConditions);
                    }
                    break;
                case "Gate":
                    if (obj.TryGetProperty("width", out JsonElement gateWidthElement) &&
                        obj.TryGetProperty("height", out JsonElement gateHeightElement))
                    {
                        int gateWidth = (int)(gateWidthElement.GetSingle() * 2);
                        int gateHeight = (int)(gateHeightElement.GetSingle() * 2);
                        Vector2 gatePos = AdjustCoordinates(xElement.GetSingle(), yElement.GetSingle());
                        gate = new Gate(gatePos, gateWidth, gateHeight);
                        gates.Add(gate);
                    }
                    break;
            }
        }
    }

    public void Load()
    {
        // Reset input state to prevent carried over clicks from triggering actions
        InputManager.ResetClickCooldown();

        // Load texture atlas and initialize camera
        texture = contentManager.Load<Texture2D>("PolyGoneTileMap");
        try
        {
            hudFont = contentManager.Load<SpriteFont>("Fonts/PauseMenu");
        }
        catch (ContentLoadException)
        {
            Console.WriteLine("Warning: Could not load SpriteFont 'Fonts/PauseMenu'. HUD text will not be rendered with this font.");
        }
        camera = new(new Vector2(0, 0));
        // Initialize player with selected items and weapon
        player = new Player(
            texture: texture,
            position: playerPos,
            size: new int[2] { 60, 60 },
            health: 100,
            color: Color.White,
            srcRect: textureStore[1],
            collisionMap: collisionMap,
            blasterTexture: texture,
            selectedItems: selectedItems,
            selectedAttachments: selectedAttachments,
            visualSize: new int[2] { 64, 64 }
        );

        // Initialize GameUI
        gameUI = new GameUI(player, texture, textureStore[4], hudFont);
        // Initialize turret enemies
        turretEnemies.AddRange(turretEnemySpawns.Select(spawnPos => new TurretEnemy(
            texture: texture,
            position: spawnPos,
            size: new int[2] { 60, 60 },
            player: player,
            health: 80,
            color: Color.White,
            srcRect: textureStore[6],
            collisionMap: collisionMap,
            visualSize: new int[2] { 64, 64 }
        )));
        // Initialize patrol enemies from spawn positions
        enemies.AddRange(enemySpawns.Select(spawnPos => new Enemy(
            texture: texture,
            position: spawnPos,
            size: new int[2] { 60, 60 },
            health: 50,
            color: Color.White,
            srcRect: textureStore[2],
            collisionMap: collisionMap,
            patrolSpeed: 1f,
            visualSize: new int[2] { 64, 64 }
        )));
    }

    private void Reset()
    {
        // Reset player
        player.position = playerPos;
        player.health = 100;
        player.bullets.Clear();

        // Reset turret enemies
        orphanedTurretBullets.Clear();
        turretEnemies.Clear();
        turretEnemies.AddRange(turretEnemySpawns.Select(spawnPos => new TurretEnemy(
            texture: texture,
            position: spawnPos,
            size: new int[2] { 60, 60 },
            player: player,
            health: 80,
            color: Color.White,
            srcRect: textureStore[6],
            collisionMap: collisionMap,
            visualSize: new int[2] { 64, 64 }
        )));
        // Reset patrol enemies
        enemies.Clear();
        enemies.AddRange(enemySpawns.Select(spawnPos => new Enemy(
            texture: texture,
            position: spawnPos,
            size: new int[2] { 60, 60 },
            health: 50,
            color: Color.White,
            srcRect: textureStore[2],
            collisionMap: collisionMap,
            patrolSpeed: 1f,
            visualSize: new int[2] { 64, 64 }
        )));

        // Reset goal trigger and level completion
        if (goalTrigger != null)
        {
            goalTrigger.Reset();
        }
        levelComplete = false;
        gameOver = false;
    }

    public void Update(GameTime gameTime)
    {
        // Check if level is complete or game over
        if (levelComplete || gameOver)
        {
            return;
        }

        // Update player and camera
        player.Update(gameTime, camera.position);
        camera.Follow(player.Rectangle, new Vector2(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight), new Vector2( tileMap.Keys.Max(k => k.X + 1) * 64, tileMap.Keys.Max(k => k.Y + 1) * 64));
        
        // Check all entities for out-of-bounds
        float worldMaxY = tileMap.Keys.Max(k => k.Y + 1) * 64;
        float worldMaxX = tileMap.Keys.Max(k => k.X + 1) * 64;

        // Check player bounds
        if (player.position.Y > worldMaxY)
        {
            player.HandleDeath();
        }
        else if (player.position.X < 0)
        {
            player.position.X = 0;
        }
        else if (player.position.X + player.size[0] > worldMaxX)
        {
            player.position.X = worldMaxX - player.size[0];
        }

        // Trigger game over if player died
        if (!player.IsAlive && !gameOver)
        {
            gameOver = true;
            sceneManager.AddScene(new GameOverScene(contentManager, sceneManager, graphics, this));
            return;
        }

        // Check enemies for falling out of bounds
        foreach (var enemy in enemies)
        {
            if (enemy.position.Y > worldMaxY)
            {
                enemy.HandleDeath();
            }
            else if (enemy.position.X < 0)
            {
                enemy.position.X = 0;
            }
            else if (enemy.position.X + enemy.size[0] > worldMaxX)
            {
                enemy.position.X = worldMaxX - enemy.size[0];
            }
        }

        // Update alive patrol enemies
        foreach (var enemy in enemies)
        {
            if (enemy.IsAlive)
            {
                enemy.Update(gameTime);
            }
        }

        // Remove patrol enemies that died this frame
        enemies.RemoveAll(e => !e.IsAlive);

        // Check turret enemies for falling out of bounds
        foreach (var turret in turretEnemies)
        {
            if (turret.position.Y > worldMaxY)
            {
                turret.HandleDeath();
            }
        }

        // Update alive turret enemies
        foreach (var turret in turretEnemies)
        {
            if (turret.IsAlive)
            {
                turret.Update(gameTime);
            }
        }

        // Before removing dead turrets, rescue any live bullets they still own
        foreach (var turret in turretEnemies)
        {
            if (!turret.IsAlive)
            {
                orphanedTurretBullets.AddRange(turret.Bullets);
            }
        }

        // Remove turret enemies that died this frame
        turretEnemies.RemoveAll(t => !t.IsAlive);

        // Check if all enemies are defeated and open gates if so
        if (AreAllEnemiesDefeated())
        {
            foreach (var gate in gates)
            {
                    gate.Open();
            }
        }

        // Advance and prune orphaned bullets
        for (int i = orphanedTurretBullets.Count - 1; i >= 0; i--)
        {
            orphanedTurretBullets[i].Update(gameTime);
            if (orphanedTurretBullets[i].lifetime <= 0)
            {
                orphanedTurretBullets.RemoveAt(i);
            }
        }

        // Handle gate collisions for all entities - gates act as solid walls when closed
        List<Entity> gateBlockedEntities = [
            player,
            .. enemies,
            .. turretEnemies,
            .. player.bullets,
            .. turretEnemies.SelectMany(t => t.Bullets),
            .. orphanedTurretBullets
        ];

        foreach (var entity in gateBlockedEntities)
        {
            bool hitClosedGate = ResolveGateCollision(entity);
            if (hitClosedGate && entity is Enemy enemy)
            {
                enemy.ReversePatrolDirection();
            }
        }

        // Gather all entities for collision detection after all updates
        List<Entity> allEntities = [player, .. enemies, .. turretEnemies, .. player.bullets, .. turretEnemies.SelectMany(t => t.Bullets), .. orphanedTurretBullets];

        // Handle entity-to-entity collisions
        player.EntityCollisionUpdate(allEntities);
        foreach (var enemy in enemies)
        {
            enemy.EntityCollisionUpdate(allEntities);
        }
        foreach (var turret in turretEnemies)
        {
            turret.EntityCollisionUpdate(allEntities);
        }

        // Check for goal trigger
        if (goalTrigger != null && !levelComplete)
        {
            if (goalTrigger.CanTrigger(IsGoalConditionMet))
            {
                goalTrigger.CheckTrigger(player.Rectangle);
            }

            if (goalTrigger.IsTriggered)
            {
                levelComplete = true;
                // Transition to win scene with current loadout
                sceneManager.AddScene(new WinScene(contentManager, sceneManager, graphics, levelName, selectedItems, selectedAttachments));
            }
        }
    }

    private bool AreAllEnemiesDefeated()
    {
        return enemies.Count == 0 && turretEnemies.Count == 0;
    }

    private bool IsGoalConditionMet(GoalConditionType condition)
    {
        return condition switch
        {
            GoalConditionType.AllEnemiesDefeated => AreAllEnemiesDefeated(),
            _ => true
        };
    }

    private static GoalConditionType ParseGoalConditions(JsonElement goalObject)
    {
        if (!goalObject.TryGetProperty("properties", out JsonElement propertiesElement))
        {
            return GoalConditionType.None;
        }

        GoalConditionType conditions = GoalConditionType.None;

        foreach (JsonElement property in propertiesElement.EnumerateArray())
        {
            if (!property.TryGetProperty("name", out JsonElement nameElement))
            {
                continue;
            }

            string? propertyName = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                continue;
            }

            if (string.Equals(propertyName, "conditions", StringComparison.OrdinalIgnoreCase) &&
                property.TryGetProperty("value", out JsonElement conditionsValue) &&
                conditionsValue.ValueKind == JsonValueKind.String)
            {
                string[] configuredConditions = conditionsValue.GetString()!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (string configuredCondition in configuredConditions)
                {
                    if (Enum.TryParse(configuredCondition, ignoreCase: true, out GoalConditionType parsedCondition))
                    {
                        conditions |= parsedCondition;
                    }
                }

                continue;
            }

            if (string.Equals(propertyName, "requireAllEnemiesDefeated", StringComparison.OrdinalIgnoreCase) &&
                property.TryGetProperty("value", out JsonElement boolValue) &&
                boolValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                boolValue.GetBoolean())
            {
                conditions |= GoalConditionType.AllEnemiesDefeated;
            }
        }

        return conditions;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var tile in tileMap)
        {
            Rectangle dest = new Rectangle(
                (int)(tile.Key.X * 64 - camera.position.X),
                (int)(tile.Key.Y * 64 - camera.position.Y),
                64,
                64
            );
            Rectangle src = textureStore[tile.Value % textureStore.Count]; // Ensure we don't go out of bounds
            spriteBatch.Draw(texture, dest, src, Color.White);
        }
        foreach (var enemy in enemies)
        {
            enemy.Draw(spriteBatch, camera.position);
        }
        foreach (var turret in turretEnemies)
        {
            turret.Draw(spriteBatch, camera.position);
        }
        foreach (var bullet in orphanedTurretBullets)
        {
            bullet.Draw(spriteBatch, camera.position);
        }
        player.Draw(spriteBatch, camera.position);

        // Draw goal trigger (if it exists)
        if (goalTrigger != null)
        {
            Rectangle goalRect = goalTrigger.GetBounds();
            Rectangle goalDest = new Rectangle(
                (int)(goalRect.X - camera.position.X),
                (int)(goalRect.Y - camera.position.Y),
                goalRect.Width,
                goalRect.Height
            );
            // Draw goal with a green tint (using tile 0 or any appropriate texture)
            Color goalColor = goalTrigger.IsTriggered ? Color.Gold : Color.LimeGreen;
            spriteBatch.Draw(texture, goalDest, textureStore[0], goalColor * 0.5f);
        }
        foreach (var gate in gates)
        {
            // Only draw gates when they are closed
            if (!gate.IsOpen)
            {
                Rectangle gateRect = gate.GetBounds();
                Rectangle gateDest = new Rectangle(
                    (int)(gateRect.X - camera.position.X),
                    (int)(gateRect.Y - camera.position.Y),
                    gateRect.Width,
                    gateRect.Height
                );
                // Draw closed gate with a red tint
                spriteBatch.Draw(texture, gateDest, textureStore[0], Color.Red * 0.7f);
            }
        }

        // Draw new GameUI (health, cooldown, and active items)
        gameUI.Draw(spriteBatch);
    }

    private bool ResolveGateCollision(Entity entity)
    {
        bool hitClosedGate = false;

        foreach (Gate currentGate in gates)
        {
            if (currentGate.IsOpen)
            {
                continue;
            }

            Rectangle gateRect = currentGate.GetBounds();
            Rectangle entityRect = entity.Rectangle;

            if (!gateRect.Intersects(entityRect))
            {
                continue;
            }

            hitClosedGate = true;

            // Projectiles are blocked by closed gates and despawn on impact.
            if (entity is Projectile projectile)
            {
                projectile.lifetime = 0f;
                continue;
            }

            PushEntityOutOfGate(entity, entityRect, gateRect);
        }

        return hitClosedGate;
    }

    private static void PushEntityOutOfGate(Entity entity, Rectangle entityRect, Rectangle gateRect)
    {
        // Calculate overlap from each side.
        int overlapLeft = entityRect.Right - gateRect.Left;
        int overlapRight = gateRect.Right - entityRect.Left;
        int overlapTop = entityRect.Bottom - gateRect.Top;
        int overlapBottom = gateRect.Bottom - entityRect.Top;

        // Find the minimum overlap to determine which side to push out from.
        int minOverlap = Math.Min(Math.Min(overlapLeft, overlapRight), Math.Min(overlapTop, overlapBottom));

        if (minOverlap == overlapLeft)
        {
            entity.position.X = gateRect.Left - entityRect.Width;
        }
        else if (minOverlap == overlapRight)
        {
            entity.position.X = gateRect.Right;
        }
        else if (minOverlap == overlapTop)
        {
            entity.position.Y = gateRect.Top - entityRect.Height;
        }
        else
        {
            entity.position.Y = gateRect.Bottom;
        }
    }
}
