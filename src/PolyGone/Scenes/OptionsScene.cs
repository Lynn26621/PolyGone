using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.IO;

namespace PolyGone;

internal class OptionsScene : IScene
{
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private KeyboardState _keyboardState;
    private KeyboardState _previousKeyboardState;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;
    private int _selectedIndex;
    private readonly (int Width, int Height)[] _availableResolutions;
    private int _pendingResolutionIndex;
    private int _appliedResolutionIndex;

    // Option labels are dynamic — they reflect current state
    private bool IsApplyEnabled => !_graphics.IsFullScreen && _pendingResolutionIndex != _appliedResolutionIndex;

    private string ResolutionLabel()
    {
        if (_graphics.IsFullScreen)
        {
            return "Resolution: (fullscreen)";
        }
        var r = _availableResolutions[_pendingResolutionIndex];
        return $"Resolution: < {r.Width}x{r.Height} >";
    }

    private string[] GetOptions() =>
    [
        _graphics.IsFullScreen ? "Display: Fullscreen" : "Display: Windowed",
        ResolutionLabel(),
        "Apply",
        "Back"
    ];

    public OptionsScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content = content;
        _sceneManager = sceneManager;
        _graphics = graphics;
        _previousKeyboardState = Keyboard.GetState();
        _selectedIndex = 0;
        _availableResolutions = DisplaySettings.GetAvailableResolutions();
        var currentRes = (DisplaySettings.WindowedWidth, DisplaySettings.WindowedHeight);
        _pendingResolutionIndex = Array.IndexOf(_availableResolutions, currentRes);
        if (_pendingResolutionIndex < 0) { _pendingResolutionIndex = 0; }
        _appliedResolutionIndex = _pendingResolutionIndex;
    }

    public void Load()
    {
        if (_font == null)
        {
            var fontAssetPath = Path.Combine(_content.RootDirectory, "Fonts", "PauseMenu.xnb");
            if (File.Exists(fontAssetPath))
            {
                _font = _content.Load<SpriteFont>("Fonts/PauseMenu");
            }
        }
    }

    public void Update(GameTime gameTime)
    {
        _keyboardState = Keyboard.GetState();
        var options = GetOptions();

        // Mouse navigation
        if (_font != null)
        {
            var viewport = _graphics.GraphicsDevice.Viewport;
            var startY = viewport.Height / 2f - options.Length * 40f / 2f;

            for (var i = 0; i < options.Length; i++)
            {
                var textSize = _font.MeasureString(options[i]);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                var bounds = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);

                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _selectedIndex = i;

                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        if (i == 1 && !_graphics.IsFullScreen)
                        {
                            // Left half of the label = go back, right half = go forward
                            var midX = position.X + textSize.X / 2f;
                            CycleResolution(InputManager.GetMousePosition().X < midX ? -1 : 1);
                        }
                        else
                        {
                            ExecuteSelection();
                        }
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        // Keyboard navigation
        if (IsKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + options.Length) % options.Length;
        }

        if (IsKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % options.Length;
        }

        if (IsKeyPressed(Keys.Enter))
        {
            ExecuteSelection();
        }

        // Left/Right cycles resolution when the resolution row is selected
        if (_selectedIndex == 1 && !_graphics.IsFullScreen)
        {
            if (IsKeyPressed(Keys.Left))
            {
                CycleResolution(-1);
            }
            if (IsKeyPressed(Keys.Right))
            {
                CycleResolution(1);
            }
        }

        if (IsKeyPressed(Keys.Escape))
        {
            _sceneManager.PopScene(this);
        }

        _previousKeyboardState = _keyboardState;
    }

    // Only updates the pending index — does not touch graphics until Apply is pressed
    private void CycleResolution(int dir)
    {
        var count = _availableResolutions.Length;
        _pendingResolutionIndex = (_pendingResolutionIndex + dir + count) % count;
    }

    private void ApplyPendingResolution()
    {
        var r = _availableResolutions[_pendingResolutionIndex];
        DisplaySettings.ResolutionIndex = Array.IndexOf(DisplaySettings.Resolutions, r);
        _graphics.PreferredBackBufferWidth  = r.Width;
        _graphics.PreferredBackBufferHeight = r.Height;
        _graphics.ApplyChanges();
        DisplaySettings.CenterWindowOnPrimaryDisplay(r.Width, r.Height);
        DisplaySettings.Save();
        _appliedResolutionIndex = _pendingResolutionIndex;
    }

    private void ExecuteSelection()
    {
        if (_selectedIndex == 0)
        {
            if (!_graphics.IsFullScreen)
            {
                // Going fullscreen: use native display resolution
                var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                _graphics.PreferredBackBufferWidth  = dm.Width;
                _graphics.PreferredBackBufferHeight = dm.Height;
                _graphics.IsFullScreen = true;
                DisplaySettings.IsFullScreen = true;
            }
            else
            {
                // Going windowed: restore the persisted windowed resolution
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth  = DisplaySettings.WindowedWidth;
                _graphics.PreferredBackBufferHeight = DisplaySettings.WindowedHeight;
                DisplaySettings.IsFullScreen = false;
                // Sync pending/applied to the current windowed resolution
                var currentRes = (DisplaySettings.WindowedWidth, DisplaySettings.WindowedHeight);
                _pendingResolutionIndex = Array.IndexOf(_availableResolutions, currentRes);
                if (_pendingResolutionIndex < 0) { _pendingResolutionIndex = 0; }
                _appliedResolutionIndex = _pendingResolutionIndex;
            }
            _graphics.ApplyChanges();
            DisplaySettings.Save();
            // Center on primary display when switching to windowed
            if (!_graphics.IsFullScreen)
            {
                DisplaySettings.CenterWindowOnPrimaryDisplay(
                    DisplaySettings.WindowedWidth, DisplaySettings.WindowedHeight);
            }
        }
        else if (_selectedIndex == 1)
        {
            // Click on resolution row cycles forward (if windowed)
            if (!_graphics.IsFullScreen)
            {
                CycleResolution(1);
            }
        }
        else if (_selectedIndex == 2)
        {
            // Apply pending resolution
            if (IsApplyEnabled)
            {
                ApplyPendingResolution();
            }
        }
        else if (_selectedIndex == 3)
        {
            // Back — discard any unapplied resolution change
            _pendingResolutionIndex = DisplaySettings.ResolutionIndex;
            _sceneManager.PopScene(this);
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        spriteBatch.Draw(_pixel, new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height), Color.Gray);

        if (_font != null)
        {
            var options = GetOptions();
            var viewport = spriteBatch.GraphicsDevice.Viewport;
            var startY = viewport.Height / 2f - options.Length * 40f / 2f;

            // Title
            var title = "Options";
            var titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(viewport.Width / 2f - titleSize.X / 2f, startY - 60f), Color.LightGray);

            for (var i = 0; i < options.Length; i++)
            {
                // Dim resolution row in fullscreen; dim Apply when there's nothing to apply
                Color color;
                if ((i == 1 && _graphics.IsFullScreen) || (i == 2 && !IsApplyEnabled))
                {
                    color = Color.DarkGray;
                }
                else
                {
                    color = i == _selectedIndex ? Color.Yellow : Color.White;
                }
                var textSize = _font.MeasureString(options[i]);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                spriteBatch.DrawString(_font, options[i], position, color);
            }
        }
    }

    private bool IsKeyPressed(Keys key)
    {
        return _keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
}