using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PolyGone.Core;

namespace PolyGone;

/// <summary>
/// Displays an in-game credits screen listing everyone who contributed to PolyGone.
/// </summary>
internal class CreditsScene : IScene
{
    // -----------------------------------------------------------------------
    // Layout constants
    // -----------------------------------------------------------------------
    private const float TitleY = 20f;
    private const float ContentStartY = 100f;
    private const float LineSpacing = 42f;
    private const float HeaderScale = 0.8f;
    private const float BodyScale = 0.6f;

    // -----------------------------------------------------------------------
    // Credits content
    // Lines starting with "## " are section headers (cyan).
    // Empty strings are blank spacers.
    // All other lines are body text (white).
    // -----------------------------------------------------------------------
    private static readonly string[] CreditLines =
    {
        "## Development Team",
        "",
        "Jesse  —  Lead Developer, Project Owner",
        "Lynn  —  Developer, QA & Bug Reporting",
        "",
        "## Tools & Frameworks",
        "",
        "MonoGame  —  Cross-platform game framework",
        "Tiled Map Editor  —  Level design tool",
        "Formbar  —  Authentication & Digipog payments",
        "Microsoft .NET 8  —  Runtime and SDK",
        "Visual Studio 2022  —  IDE",
        "",
        "## Special Thanks",
        "",
        "York County School of Technology",
        "For providing the tools, resources, and environment",
        "that made this project possible.",
        "",
        "",
        "PolyGone v0.2.0-Alpha  —  2026",
    };

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;
    private int _scrollOffset;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public CreditsScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
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

    public void Unload() { }

    public void Update(GameTime gameTime)
    {
        // Scroll content
        if (InputManager.MenuUp())
            _scrollOffset = Math.Max(0, _scrollOffset - 1);
        if (InputManager.MenuDown())
            _scrollOffset = Math.Min(MaxScroll(), _scrollOffset + 1);

        // Mouse wheel scrolling
        int wheelDelta = InputManager.CurrentMouseState.ScrollWheelValue
                       - InputManager.PreviousMouseState.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            int steps = Math.Max(1, Math.Abs(wheelDelta) / 120);
            int direction = wheelDelta > 0 ? -1 : 1;
            for (int i = 0; i < steps; i++)
            {
                _scrollOffset = Math.Clamp(_scrollOffset + direction, 0, MaxScroll());
            }
        }

        // Back / Escape
        if (InputManager.MenuBack())
            _sceneManager.PopScene(this);

        // Mouse: Back button hit-testing
        if (_font != null)
        {
            var mouse = InputManager.GetMousePosition();
            var viewport = _graphics.GraphicsDevice.Viewport;

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

        // Background
        spriteBatch.Draw(_pixel,
            new Rectangle(0, 0, viewport.Width, viewport.Height),
            new Color(25, 25, 35));

        if (_font == null)
            return;

        // Title
        const string title = "Credits";
        var titleSize = _font.MeasureString(title);
        spriteBatch.DrawString(_font, title,
            new Vector2(viewport.Width / 2f - titleSize.X / 2f, TitleY),
            Color.White);

        // Separator line under title
        int sepY = (int)(TitleY + titleSize.Y + 6f);
        spriteBatch.Draw(_pixel, new Rectangle(20, sepY, viewport.Width - 40, 1), Color.DimGray);

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
    // Helpers
    // -----------------------------------------------------------------------
    private int MaxScroll()
    {
        if (_font == null)
            return 0;
        var viewport = _graphics.GraphicsDevice.Viewport;
        float available = viewport.Height - ContentStartY - 60f;
        int maxVisible = (int)(available / LineSpacing);
        return Math.Max(0, CreditLines.Length - maxVisible);
    }

    private void DrawContent(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_font == null || _pixel == null)
            return;

        float available = viewport.Height - ContentStartY - 60f;
        int maxVisible = (int)(available / LineSpacing);

        // Clamp scroll
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScroll());

        // "More above" indicator
        if (_scrollOffset > 0)
        {
            const string above = "^ more above ^";
            var aboveSz = _font.MeasureString(above);
            spriteBatch.DrawString(_font, above,
                new Vector2(viewport.Width / 2f - aboveSz.X * BodyScale / 2f, ContentStartY - LineSpacing),
                Color.Gray, 0f, Vector2.Zero, BodyScale, SpriteEffects.None, 0f);
        }

        int endIndex = Math.Min(CreditLines.Length, _scrollOffset + maxVisible);
        for (int i = _scrollOffset; i < endIndex; i++)
        {
            float y = ContentStartY + (i - _scrollOffset) * LineSpacing;
            string line = CreditLines[i];

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                // Section header — cyan, slightly larger
                string header = line[3..];
                var headerSz = _font.MeasureString(header);
                spriteBatch.DrawString(_font, header,
                    new Vector2(viewport.Width / 2f - headerSz.X * HeaderScale / 2f, y),
                    Color.Cyan, 0f, Vector2.Zero, HeaderScale, SpriteEffects.None, 0f);
            }
            else if (string.IsNullOrEmpty(line))
            {
                // Blank spacer — nothing to draw
            }
            else
            {
                // Body text — white, centered
                var lineSz = _font.MeasureString(line);
                spriteBatch.DrawString(_font, line,
                    new Vector2(viewport.Width / 2f - lineSz.X * BodyScale / 2f, y),
                    Color.White, 0f, Vector2.Zero, BodyScale, SpriteEffects.None, 0f);
            }
        }

        // "More below" indicator
        if (_scrollOffset < MaxScroll())
        {
            const string below = "v more below v";
            var belowSz = _font.MeasureString(below);
            float belowY = ContentStartY + maxVisible * LineSpacing;
            spriteBatch.DrawString(_font, below,
                new Vector2(viewport.Width / 2f - belowSz.X * BodyScale / 2f, belowY),
                Color.Gray, 0f, Vector2.Zero, BodyScale, SpriteEffects.None, 0f);
        }
    }
}
