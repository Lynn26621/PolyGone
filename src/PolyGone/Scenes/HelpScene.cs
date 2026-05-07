using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PolyGone.Core;

namespace PolyGone;

internal class HelpScene : IScene
{
    // -----------------------------------------------------------------------
    // Layout constants
    // -----------------------------------------------------------------------
    private const float TitleY = 12f;
    private const float TabBarY = 70f;
    private const float TabBarHeight = 44f;
    private const float HintY = 126f;
    private const float ContentStartY = 176f;
    private const float LineSpacing = 38f;
    private const float ContentScale = 0.6f;      // Body text drawn at 60% of font size
    private const float TileSwatchSize = 22f;     // Coloured tile square side length
    private const float TileSwatchGap = 10f;      // Gap between swatch and text

    // -----------------------------------------------------------------------
    // Tab definitions
    // -----------------------------------------------------------------------
    private enum HelpTab { Player, Enemies, Menus, Collisions }
    private static readonly string[] TabLabels = { "Player", "Enemies", "Menus", "Collisions" };

    // Each inner array is one tab's content lines.
    // A line starting with "## " is rendered as a section header (cyan, slightly larger).
    // A line starting with "##SWATCH:" draws a coloured tile square followed by the text.
    // A blank "" is a spacer. All other lines are body text.
    private static readonly string[][] StaticTabContent =
    {
        // ----- Enemies -----
        new[]
        {
            "## Walker",
            "Patrols back and forth. Chases the player on sight.",
            "Standard health. Knocked back by projectile hits.",
            "",
            "## Frog",
            "Patrols like a Walker but leaps toward the player when",
            "they are above it. Can reach elevated platforms.",
            "",
            "## Turret",
            "Stationary. Fires a fast projectile at the player every",
            "2 seconds when the player is within range. Low health.",
            "",
            "## Berserk",
            "High health. Chases the player aggressively. When its",
            "health drops below 150 it enters Berserk mode: moves",
            "faster and fires rapidly at the player.",
            "",
            "## Factory",
            "Stationary. Periodically spawns small, fast minions when",
            "the player is nearby. Destroy the Factory to stop spawning.",
        },

        // ----- Menus -----
        new[]
        {
            "## Main Menu",
            "The starting screen. Choose to Play, open the Help page,",
            "adjust Options, Log Out, or Exit to Desktop.",
            "",
            "## Pause Menu",
            "Press Escape during gameplay to pause.",
            "Continue resumes the game; Exit to Menu returns to the",
            "Main Menu. In the Hub, Inventory is also available.",
            "",
            "## Options",
            "Change display resolution, toggle fullscreen, adjust",
            "master volume, remap controls, or reset save data.",
            "",
            "## Inventory",
            "Accessible from the Hub level via the Pause Menu.",
            "Equip up to 2 items and weapon attachments before",
            "entering a level. Use Ctrl / Back button to skip items.",
            "",
            "## Win Screen",
            "Shown after completing a level. Return to the Hub,",
            "advance to the Next Level, or go back to the Main Menu.",
            "",
            "## Game Over",
            "Shown when the player dies. Restart the current level,",
            "return to the Hub, or go back to the Main Menu.",
        },

        // ----- Collisions -----
        new[]
        {
            "## Tile Types",
            "",
            "##SWATCH:GRAY Solid - Standard wall or floor tile.",
            "Blocks all movement in every direction.",
            "",
            "##SWATCH:BROWN Semi-Solid - One-way platform.",
            "Stand on top; drop through by pressing {DROP_KEY} (or {DROP_BUTTON}).",
            "",
            "##SWATCH:BLUE Slippery - Icy surface.",
            "Greatly reduced friction. You slide with little control.",
            "",
            "##SWATCH:GREEN Bouncy - Spring tile.",
            "Launches you away on contact with increased momentum.",
            "Only strong landings trigger a full bounce.",
            "",
            "##SWATCH:RED Damage - Hazard tile.",
            "Deals damage whenever you touch it. Avoid these tiles!",
        },
    };

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;

    private HelpTab _currentTab = HelpTab.Player;
    private int _scrollOffset;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public HelpScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content = content;
        _sceneManager = sceneManager;
        _graphics = graphics;
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------
    public void Load()
    {
        if (_font == null)
        {
            var fontAssetPath = Path.Combine(_content.RootDirectory, "Fonts", "PauseMenu.xnb");
            if (File.Exists(fontAssetPath))
                _font = _content.Load<SpriteFont>("Fonts/PauseMenu");
        }
        _scrollOffset = 0;
        InputManager.ResetClickCooldown();
    }

    public void Update(GameTime gameTime)
    {
        // Tab switching via Left/Right
        if (InputManager.MenuLeft())
            SwitchTab(-1);
        if (InputManager.MenuRight())
            SwitchTab(1);

        // Scroll content up / down
        if (InputManager.MenuUp())
            ScrollContent(-1);
        if (InputManager.MenuDown())
            ScrollContent(1);

        // Mouse wheel scrolling
        int wheelDelta = InputManager.CurrentMouseState.ScrollWheelValue - InputManager.PreviousMouseState.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            int steps = Math.Max(1, Math.Abs(wheelDelta) / 120);
            int direction = wheelDelta > 0 ? -1 : 1;
            for (int i = 0; i < steps; i++)
                ScrollContent(direction);
        }

        // Back / escape
        if (InputManager.MenuBack())
            _sceneManager.PopScene(this);

        // Mouse: click on tabs or the Back button
        if (_font != null)
        {
            var mouse = InputManager.GetMousePosition();
            var viewport = _graphics.GraphicsDevice.Viewport;

            // Tab bar hit-testing
            float tabTotalWidth = 0f;
            float[] tabWidths = new float[TabLabels.Length];
            for (int i = 0; i < TabLabels.Length; i++)
            {
                tabWidths[i] = _font.MeasureString(TabLabels[i]).X + 24f;
                tabTotalWidth += tabWidths[i] + 8f;
            }
            float tabX = viewport.Width / 2f - tabTotalWidth / 2f;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                var tabRect = new Rectangle((int)tabX, (int)TabBarY, (int)tabWidths[i], (int)TabBarHeight);
                if (tabRect.Contains(mouse))
                {
                    if (InputManager.MenuConfirmMouseClick())
                    {
                        _currentTab = (HelpTab)i;
                        _scrollOffset = 0;
                        InputManager.ConsumeClick();
                    }
                }
                tabX += tabWidths[i] + 8f;
            }

            // Back button hit-testing
            string backLabel = "< Back";
            var backSize = _font.MeasureString(backLabel);
            float backScale = 0.75f;
            var backPos = new Vector2(20f, viewport.Height - backSize.Y * backScale - 12f);
            var backRect = new Rectangle((int)backPos.X, (int)backPos.Y,
                (int)(backSize.X * backScale), (int)(backSize.Y * backScale));
            if (backRect.Contains(mouse) && InputManager.MenuConfirmMouseClick())
            {
                _sceneManager.PopScene(this);
                InputManager.ConsumeClick();
            }

            // Scroll arrows hit-testing
            HandleScrollArrowClicks(mouse, viewport);
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        var viewport = spriteBatch.GraphicsDevice.Viewport;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(35, 35, 35));

        if (_font == null)
            return;

        // Title
        const string title = "Help";
        var titleSize = _font.MeasureString(title);
        spriteBatch.DrawString(_font, title,
            new Vector2(viewport.Width / 2f - titleSize.X / 2f, TitleY), Color.White);

        // Tab bar
        DrawTabBar(spriteBatch, viewport);

        // Hint line
        string hint = BuildHintText();
        var hintSize = _font.MeasureString(hint);
        float hintScale = Math.Min(0.55f, (viewport.Width - 20f) / hintSize.X);
        spriteBatch.DrawString(_font, hint,
            new Vector2(viewport.Width / 2f - hintSize.X * hintScale / 2f, HintY),
            Color.LightGray, 0f, Vector2.Zero, hintScale, SpriteEffects.None, 0f);

        // Content
        DrawContent(spriteBatch, viewport);

        // Back button
        string backLabel = "< Back";
        var backSize = _font.MeasureString(backLabel);
        float backScale = 0.75f;
        var backPos = new Vector2(20f, viewport.Height - backSize.Y * backScale - 12f);
        spriteBatch.DrawString(_font, backLabel, backPos,
            Color.White, 0f, Vector2.Zero, backScale, SpriteEffects.None, 0f);
    }

    // -----------------------------------------------------------------------
    // Tab bar
    // -----------------------------------------------------------------------
    private void DrawTabBar(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_font == null || _pixel == null)
            return;

        float tabTotalWidth = 0f;
        float[] tabWidths = new float[TabLabels.Length];
        for (int i = 0; i < TabLabels.Length; i++)
        {
            tabWidths[i] = _font.MeasureString(TabLabels[i]).X + 24f;
            tabTotalWidth += tabWidths[i] + 8f;
        }

        float tabX = viewport.Width / 2f - tabTotalWidth / 2f;
        for (int i = 0; i < TabLabels.Length; i++)
        {
            bool isActive = (int)_currentTab == i;
            var bg = isActive ? Color.DimGray : new Color(55, 55, 55);
            var fg = isActive ? Color.Yellow : Color.White;

            spriteBatch.Draw(_pixel,
                new Rectangle((int)tabX, (int)TabBarY, (int)tabWidths[i], (int)TabBarHeight), bg);

            // Border
            int b = 1;
            spriteBatch.Draw(_pixel, new Rectangle((int)tabX, (int)TabBarY, (int)tabWidths[i], b), Color.Gray);
            spriteBatch.Draw(_pixel, new Rectangle((int)tabX, (int)(TabBarY + TabBarHeight - b), (int)tabWidths[i], b),
                isActive ? Color.Yellow : Color.Gray);
            spriteBatch.Draw(_pixel, new Rectangle((int)tabX, (int)TabBarY, b, (int)TabBarHeight), Color.Gray);
            spriteBatch.Draw(_pixel, new Rectangle((int)(tabX + tabWidths[i] - b), (int)TabBarY, b, (int)TabBarHeight), Color.Gray);

            var labelSize = _font.MeasureString(TabLabels[i]);
            spriteBatch.DrawString(_font, TabLabels[i],
                new Vector2(tabX + tabWidths[i] / 2f - labelSize.X / 2f, TabBarY + TabBarHeight / 2f - labelSize.Y / 2f), fg);

            tabX += tabWidths[i] + 8f;
        }
    }

    // -----------------------------------------------------------------------
    // Content drawing
    // -----------------------------------------------------------------------
    private void DrawContent(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_font == null || _pixel == null)
            return;

        var lines = GetCurrentTabContent();
        float availableHeight = viewport.Height - ContentStartY - 60f; // leave room for Back button
        int maxVisible = (int)(availableHeight / LineSpacing);

        // Clamp scroll
        int maxScroll = Math.Max(0, lines.Length - maxVisible);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

        // "More above" indicator
        if (_scrollOffset > 0)
        {
            const string above = "^ more above ^";
            var aboveSz = _font.MeasureString(above);
            spriteBatch.DrawString(_font, above,
                new Vector2(viewport.Width / 2f - aboveSz.X * ContentScale / 2f, ContentStartY - LineSpacing),
                Color.Gray, 0f, Vector2.Zero, ContentScale, SpriteEffects.None, 0f);
        }

        int endIndex = Math.Min(lines.Length, _scrollOffset + maxVisible);
        for (int i = _scrollOffset; i < endIndex; i++)
        {
            float y = ContentStartY + (i - _scrollOffset) * LineSpacing;
            string line = lines[i];

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                // Section header
                string header = line[3..];
                float headerScale = 0.75f;
                spriteBatch.DrawString(_font, header,
                    new Vector2(20f, y),
                    Color.Cyan, 0f, Vector2.Zero, headerScale, SpriteEffects.None, 0f);
            }
            else if (line.StartsWith("##SWATCH:", StringComparison.Ordinal))
            {
                // Tile swatch line: "##SWATCH:COLOR Text"
                int spaceIdx = line.IndexOf(' ', 9);
                string colorName = spaceIdx > 9 ? line[9..spaceIdx] : line[9..];
                string text = spaceIdx > 9 ? line[(spaceIdx + 1)..] : string.Empty;

                Color swatchColor = colorName switch
                {
                    "GRAY" => Color.Gray,
                    "LIGHTGRAY" => Color.LightGray,
                    "BROWN" => new Color(139, 94, 60),
                    "BLUE" => new Color(80, 160, 220),
                    "GREEN" => new Color(60, 180, 80),
                    "RED" => new Color(200, 60, 60),
                    _ => Color.White,
                };

                float swatchY = y + LineSpacing / 2f - TileSwatchSize / 2f;
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)20f, (int)swatchY, (int)TileSwatchSize, (int)TileSwatchSize),
                    swatchColor);
                // thin border around swatch
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)20f, (int)swatchY, (int)TileSwatchSize, 1), Color.DarkGray);
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)20f, (int)(swatchY + TileSwatchSize - 1), (int)TileSwatchSize, 1), Color.DarkGray);
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)20f, (int)swatchY, 1, (int)TileSwatchSize), Color.DarkGray);
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)(20f + TileSwatchSize - 1), (int)swatchY, 1, (int)TileSwatchSize), Color.DarkGray);

                float textX = 20f + TileSwatchSize + TileSwatchGap;
                spriteBatch.DrawString(_font, text,
                    new Vector2(textX, y),
                    Color.White, 0f, Vector2.Zero, ContentScale, SpriteEffects.None, 0f);
            }
            else if (string.IsNullOrEmpty(line))
            {
                // Spacer — nothing to draw
            }
            else
            {
                // Body line
                spriteBatch.DrawString(_font, line,
                    new Vector2(36f, y),
                    Color.White, 0f, Vector2.Zero, ContentScale, SpriteEffects.None, 0f);
            }
        }

        // "More below" indicator
        if (_scrollOffset < maxScroll)
        {
            const string below = "v more below v";
            var belowSz = _font.MeasureString(below);
            float belowY = ContentStartY + maxVisible * LineSpacing;
            spriteBatch.DrawString(_font, below,
                new Vector2(viewport.Width / 2f - belowSz.X * ContentScale / 2f, belowY),
                Color.Gray, 0f, Vector2.Zero, ContentScale, SpriteEffects.None, 0f);
        }
    }

    // -----------------------------------------------------------------------
    // Scroll arrows (mouse wheel / click on indicators)
    // -----------------------------------------------------------------------
    private void HandleScrollArrowClicks(Microsoft.Xna.Framework.Point mouse, Viewport viewport)
    {
        // Clicking "more above" or "more below" text areas scrolls content
        float availableHeight = viewport.Height - ContentStartY - 60f;
        int maxVisible = (int)(availableHeight / LineSpacing);

        if (_scrollOffset > 0)
        {
            // Rough region of "^ more above ^" text
            var aboveRect = new Rectangle(0, (int)(ContentStartY - LineSpacing), viewport.Width, (int)LineSpacing);
            if (aboveRect.Contains(mouse) && InputManager.MenuConfirmMouseClick())
            {
                ScrollContent(-1);
                InputManager.ConsumeClick();
            }
        }

        var lines = GetCurrentTabContent();
        int maxScroll = Math.Max(0, lines.Length - maxVisible);
        if (_scrollOffset < maxScroll)
        {
            float belowY = ContentStartY + maxVisible * LineSpacing;
            var belowRect = new Rectangle(0, (int)belowY, viewport.Width, (int)LineSpacing);
            if (belowRect.Contains(mouse) && InputManager.MenuConfirmMouseClick())
            {
                ScrollContent(1);
                InputManager.ConsumeClick();
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private void SwitchTab(int direction)
    {
        int count = TabLabels.Length;
        _currentTab = (HelpTab)(((int)_currentTab + direction + count) % count);
        _scrollOffset = 0;
    }

    private void ScrollContent(int direction)
    {
        _scrollOffset = Math.Max(0, _scrollOffset + direction);
    }

    private string[] GetCurrentTabContent()
    {
        if (_currentTab == HelpTab.Player)
            return BuildPlayerTabContent();
        if (_currentTab == HelpTab.Collisions)
            return BuildCollisionsTabContent();
        return StaticTabContent[(int)_currentTab - 1];
    }

    private static string[] BuildPlayerTabContent()
    {
        var bindings = InputBindings.Current;
        string moveStick = bindings.MoveStick == StickBinding.Left ? "Left" : "Right";
        string aimStick = bindings.AimStick == StickBinding.Left ? "Left" : "Right";

        return
        [
            "## Movement",
            $"Move Left / Right: {bindings.MoveLeftKey} and {bindings.MoveRightKey} keys, or {moveStick} Analog Stick",
            $"Jump:              {bindings.JumpKey} key, or {bindings.JumpButton} button (gamepad)",
            $"Drop through platform: {bindings.DropKey} key, or {bindings.DropButton} (gamepad)",
            "",
            "## Dash",
            $"Press {bindings.DashKey} (or {bindings.DashButton}) to dash 5 tiles in your",
            "current movement direction. Has a 2.5-second cooldown.",
            "",
            "## Wall Jump",
            "Move into a wall while airborne to cling to it.",
            "Clinging slows your fall. Press Jump to leap off the wall.",
            "You cannot jump off the same wall twice in a row.",
            "",
            "## Shooting",
            "Left Mouse Button to fire (aim with the mouse cursor).",
            $"{bindings.ShootButton} fires; {aimStick} Analog Stick aims (gamepad).",
            "",
            "## Interact",
            $"Press {bindings.InteractKey} key (or {bindings.InteractButton} button) near doors and switches to use them.",
        ];
    }

    private static string[] BuildCollisionsTabContent()
    {
        var lines = (string[])StaticTabContent[(int)HelpTab.Collisions - 1].Clone();
        var bindings = InputBindings.Current;
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i]
                .Replace("{DROP_KEY}", bindings.DropKey.ToString(), StringComparison.Ordinal)
                .Replace("{DROP_BUTTON}", bindings.DropButton.ToString(), StringComparison.Ordinal);
        }
        return lines;
    }

    private static string BuildHintText()
    {
        var bindings = InputBindings.Current;
        return $"{bindings.MenuLeftKey}/{bindings.MenuRightKey} or DPad Left/Right = switch tab  |  " +
               $"{bindings.MenuUpKey}/{bindings.MenuDownKey} or DPad Up/Down = scroll  |  Esc/{bindings.MenuBackButton} = back";
    }
}
