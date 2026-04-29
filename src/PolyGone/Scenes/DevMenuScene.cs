using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PolyGone;

#if DEBUG
internal class DevMenuScene : IScene
{
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;

    // Cursor: 0..LevelFiles.Length-1 = level toggles, then Unlock All, Lock All, Close
    private int _cursor = 0;

    private static readonly string[] LevelFiles        = { "TestLevel", "TestLevel2", "TestLevel3" };
    private static readonly string[] LevelDisplayNames = { "Level 1",   "Level 2",    "Level 3"    };
    private static readonly string[] AbilityNames = { "Dash", "WallJump" };
    private static readonly string[] AbilityDisplayNames = { "Dash", "Wall Jump" };
    private int EntryCount => LevelFiles.Length + AbilityNames.Length + 3;

    public DevMenuScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content               = content;
        _sceneManager          = sceneManager;
        _graphics              = graphics;
    }

    public void Load()
    {
        if (_font == null)
        {
            try { _font = _content.Load<SpriteFont>("Fonts/PauseMenu"); }
            catch { }
        }
    }

    public void Update(GameTime gameTime)
    {

        if (_font != null)
        {
            var viewport  = _graphics.GraphicsDevice.Viewport;
            int startX    = 120;
            int startY    = 200;
            int abilityY = startY + LevelFiles.Length * 40 + 40;
            int actionBaseY = abilityY + AbilityNames.Length * 40 + 20;

            for (int i = 0; i < LevelFiles.Length; i++)
            {
                bool completed = UnlockTracker.IsLevelCompleted(LevelFiles[i]);
                string text    = (completed ? "[X] " : "[ ] ") + LevelDisplayNames[i];
                var size       = _font.MeasureString(text);
                var bounds     = new Rectangle(startX, startY + i * 40, (int)size.X, (int)size.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _cursor = i;
                    if (InputManager.MenuConfirm())
                    {
                        UnlockTracker.ToggleLevelComplete(LevelFiles[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            for (int i = 0; i < AbilityNames.Length; i++)
            {
                bool completed = UnlockTracker.IsAbilityUnlocked(AbilityNames[i]);
                string text = (completed ? "[X] " : "[ ] ") + AbilityDisplayNames[i];
                var size = _font.MeasureString(text);
                var bounds = new Rectangle(startX, abilityY + i * 40, (int)size.X, (int)size.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _cursor = LevelFiles.Length + i;
                    if (InputManager.MenuConfirm())
                    {
                        UnlockTracker.ToggleAbilityUnlocked(AbilityNames[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            string[] actions = { "Unlock All Levels", "Lock All Levels", "Unlock All Abilities", "Lock All Abilities", "Close" };
            for (int i = 0; i < actions.Length; i++)
            {
                var size   = _font.MeasureString(actions[i]);
                var bounds = new Rectangle(startX, actionBaseY + i * 40, (int)size.X, (int)size.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _cursor = LevelFiles.Length + AbilityNames.Length + i;
                    if (InputManager.MenuConfirm())
                    {
                        ExecuteAction(_cursor);
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        if (InputManager.MenuUp()) _cursor = (_cursor - 1 + EntryCount) % EntryCount;
        if (InputManager.MenuDown()) _cursor = (_cursor + 1) % EntryCount;
        if (InputManager.MenuConfirm()) ExecuteAction(_cursor);
        if (InputManager.MenuBack()) _sceneManager.PopScene(this);
    }

    private void ExecuteAction(int index)
    {
        if (index < LevelFiles.Length)
        {
            UnlockTracker.ToggleLevelComplete(LevelFiles[index]);
        }
        else if ((index < (LevelFiles.Length + AbilityNames.Length)) && (index > LevelFiles.Length))
        {
            UnlockTracker.ToggleAbilityUnlocked(AbilityNames[index - AbilityNames.Length]);
        }
        else
        {
            int action = index - (LevelFiles.Length + AbilityNames.Length);
            if (action == 0)      // Unlock All Levels
            {
                foreach (var lf in LevelFiles)
                    UnlockTracker.RecordLevelComplete(lf);
            }
            else if (action == 1) // Lock All Levels
            {
                foreach (var lf in LevelFiles)
                {
                    if (UnlockTracker.IsLevelCompleted(lf))
                    {
                        UnlockTracker.ToggleLevelComplete(lf);
                        InventoryManagement.ResetSavedLoadout();
                    }
                }
            }
            else if (action == 2) // Unlock All Abilities
            {
                foreach (var ability in AbilityNames)
                    UnlockTracker.RecordAbilityUnlocked(ability);
            }
            else if (action == 3) // Lock All Abilities
            {
                foreach (var ability in AbilityNames)
                {
                    if (UnlockTracker.IsAbilityUnlocked(ability))
                    {
                        UnlockTracker.ToggleAbilityUnlocked(ability);
                    }
                }
            }
            else                  // Close
            {
                _sceneManager.PopScene(this);
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        var vp = spriteBatch.GraphicsDevice.Viewport;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, vp.Width, vp.Height), new Color(15, 15, 35));

        if (_font == null) return;

        // Title
        string title    = "[DEV] Unlock Manager";
        var titleSize   = _font.MeasureString(title);
        spriteBatch.DrawString(_font, title, new Vector2(vp.Width / 2f - titleSize.X / 2f, 50), Color.Cyan);

        // ── Left column: level toggles ───────────────────────────────────
        int startX = 120;
        int startY = 200;
        spriteBatch.DrawString(_font, "Levels (toggle completed):", new Vector2(startX, startY - 40), Color.LightCyan);

        for (int i = 0; i < LevelFiles.Length; i++)
        {
            bool completed = UnlockTracker.IsLevelCompleted(LevelFiles[i]);
            bool isCursor  = _cursor == i;
            string prefix  = completed ? "[X] " : "[ ] ";
            Color color    = isCursor ? Color.Yellow : (completed ? Color.LightGreen : Color.White);
            spriteBatch.DrawString(_font, prefix + LevelDisplayNames[i], new Vector2(startX, startY + i * 40), color);
        }

        // ── Left column: ability toggles ───────────────────────────────────
        int abilityY = startY + LevelFiles.Length * 40 + 45;
        spriteBatch.DrawString(_font, "Abilities (toggle unlocked):", new Vector2(startX, abilityY - 40), Color.LightCyan);

        for (int i = 0; i < AbilityNames.Length; i++)
        {
            bool completed = UnlockTracker.IsAbilityUnlocked(AbilityNames[i]);
            int idx = LevelFiles.Length + i;
            bool isCursor = _cursor == idx;
            string prefix = completed ? "[X] " : "[ ] ";
            Color color = isCursor ? Color.Yellow : (completed ? Color.LightGreen : Color.White);
            spriteBatch.DrawString(_font, prefix + AbilityDisplayNames[i], new Vector2(startX, abilityY + i * 40), color);
        }

            // Action rows
            string[] actions = { "Unlock All Levels", "Lock All Levels", "Unlock All Abilities", "Lock All Abilities", "Close" };
        int actionBaseY  = abilityY + AbilityNames.Length * 40 + 20;
        for (int i = 0; i < actions.Length; i++)
        {
            int idx       = LevelFiles.Length + AbilityNames.Length + i;
            bool isCursor = _cursor == idx;
            Color color   = isCursor ? Color.Yellow : ((i == 1 || i == 3) ? Color.Tomato : Color.White);
            string prefix = isCursor ? "> " : "  ";
            spriteBatch.DrawString(_font, prefix + actions[i], new Vector2(startX, actionBaseY + i * 40), color);
        }

        // ── Right column: item / attachment unlock status (read-only) ─────
        int rightX = vp.Width / 2 + 50;
        spriteBatch.DrawString(_font, "Player Item Unlock Status:", new Vector2(rightX, startY - 40), Color.LightCyan);

        var allItems = new (ItemType Type, string Name, string Req)[]
        {
            (ItemType.DoubleJump,  "Double Jump",  "always"),
            (ItemType.HealingGlow, "Healing Glow", "Level 1"),
            (ItemType.LowGravity,  "Low Gravity",  "Level 2"),
            (ItemType.IronWill,    "Iron Will",    "Level 3"),
            (ItemType.DevMode,     "Dev Mode",     "always [DEV]"),
        };

        for (int i = 0; i < allItems.Length; i++)
        {
            bool unlocked = UnlockTracker.IsItemUnlocked(allItems[i].Type);
            string prefix = unlocked ? "[+] " : "[ ] ";
            Color color   = unlocked ? Color.LightGreen : Color.DarkGray;
            spriteBatch.DrawString(_font,
                $"{prefix}{allItems[i].Name}  ({allItems[i].Req})",
                new Vector2(rightX, startY + i * 40), color);
        }

        // Blaster attachment unlock status
        int attY = startY + allItems.Length * 40 + 20;
        spriteBatch.DrawString(_font, "Blaster Attachment Unlocks:", new Vector2(rightX, attY - 30), Color.LightCyan);
        var allAttachments = new (BlasterAttachmentType Type, string Name, string Req)[]
        {
            (BlasterAttachmentType.MultiShot,   "Multi-Shot",     "always"),
            (BlasterAttachmentType.RapidFire,   "Rapid Fire",     "Level 1"),
            (BlasterAttachmentType.Piercing,    "Piercing Rounds","Level 2"),
            (BlasterAttachmentType.DamageBoost, "Damage Amp",     "Level 3"),
            (BlasterAttachmentType.DevBlaster,  "Dev Blaster",    "always [DEV]"),
        };
        for (int i = 0; i < allAttachments.Length; i++)
        {
            bool unlocked = UnlockTracker.IsAttachmentUnlocked(allAttachments[i].Type);
            string prefix = unlocked ? "[+] " : "[ ] ";
            Color color   = unlocked ? Color.LightGreen : Color.DarkGray;
            spriteBatch.DrawString(_font,
                $"{prefix}{allAttachments[i].Name}  ({allAttachments[i].Req})",
                new Vector2(rightX, attY + i * 40), color);
        }
    }
}
#endif
