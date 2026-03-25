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
    private GamePadState _gamePadState;
    private GamePadState _previousGamePadState;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;
    private int _selectedIndex;
    private readonly (int Width, int Height)[] _availableResolutions;
    private int _pendingResolutionIndex;
    private int _appliedResolutionIndex;
    private int _deferredCenterWidth;
    private int _deferredCenterHeight;
    private bool _pendingIsFullScreen;
    private bool _appliedIsFullScreen;
    private bool _confirmingDiscard;
    private int _confirmSelectedIndex;
    private int _buttonIndex; // 0 = Apply, 1 = Discard
    private int _resetConfirmStep;           // 0 = off, 1 = first prompt, 2 = second prompt
    private int _resetConfirmSelectedIndex;
    private int _resetProgressConfirmStep;   // 0 = off, 1 = first prompt, 2 = second prompt
    private int _resetProgressConfirmSelectedIndex;

    private const float RowSpacing   = 50f;
    private const float ButtonWidth  = 200f;
    private const float ButtonHeight = 45f;
    private const float ButtonGap    = 24f;

    // Option labels are dynamic — they reflect current pending state
    private bool HasPendingChanges =>
        _pendingIsFullScreen != _appliedIsFullScreen ||
        _pendingResolutionIndex != _appliedResolutionIndex;

    private string ResolutionLabel()
    {
        if (_pendingIsFullScreen) { return "Resolution: (fullscreen)"; }
        var r = _availableResolutions[_pendingResolutionIndex];
        return $"Resolution: < {r.Width}x{r.Height} >";
    }

    // Row 0=Display, Row 1=Resolution, Row 2=Buttons (drawn separately),
    // Row 3=Reset Purchases, Row 4=Reset Progress, Row 5 (DEBUG only)=Dev Menu, Row 5/6=Back
    private string[] GetRowLabels() =>
    [
        _pendingIsFullScreen ? "Display: Fullscreen" : "Display: Windowed",
        ResolutionLabel(),
        "",   // placeholder — button row is drawn separately
#if DEBUG
        "Reset Purchases",
#endif
        "Reset Progress",
#if DEBUG
        "Dev Menu",
#endif
        "Back",
    ];

    public OptionsScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content = content;
        _sceneManager = sceneManager;
        _graphics = graphics;
        _previousKeyboardState = Keyboard.GetState();
        _previousGamePadState = GamePad.GetState(PlayerIndex.One);
        _selectedIndex = 0;
        _availableResolutions = DisplaySettings.GetAvailableResolutions();
        var currentRes = (DisplaySettings.WindowedWidth, DisplaySettings.WindowedHeight);
        _pendingResolutionIndex = Array.IndexOf(_availableResolutions, currentRes);
        if (_pendingResolutionIndex < 0) { _pendingResolutionIndex = 0; }
        _appliedResolutionIndex = _pendingResolutionIndex;
        _pendingIsFullScreen  = DisplaySettings.IsFullScreen;
        _appliedIsFullScreen  = DisplaySettings.IsFullScreen;
        _confirmingDiscard         = false;
        _confirmSelectedIndex      = 1;
        _buttonIndex               = 0;
        _resetConfirmStep                  = 0;
        _resetConfirmSelectedIndex         = 1;
        _resetProgressConfirmStep          = 0;
        _resetProgressConfirmSelectedIndex = 1;
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
        // Apply deferred window centering (must happen on the frame after ApplyChanges)
        if (_deferredCenterWidth > 0)
        {
            DisplaySettings.CenterWindowOnPrimaryDisplay(_deferredCenterWidth, _deferredCenterHeight);
            _deferredCenterWidth = 0;
            _deferredCenterHeight = 0;
        }

        _keyboardState = Keyboard.GetState();
        _gamePadState = GamePad.GetState(PlayerIndex.One);

        // Handle the confirm-discard overlay independently
        if (_confirmingDiscard)
        {
            if (_font != null)
            {
                string[] confirmOpts = ["Discard & Go Back", "Keep Editing"];
                var viewport = _graphics.GraphicsDevice.Viewport;
                var confirmStartY = viewport.Height / 2f - 20f;
                for (var i = 0; i < confirmOpts.Length; i++)
                {
                    var textSize = _font.MeasureString(confirmOpts[i]);
                    var pos = new Vector2(viewport.Width / 2f - textSize.X / 2f, confirmStartY + i * 50f);
                    var bounds = new Rectangle((int)pos.X, (int)pos.Y, (int)textSize.X, (int)textSize.Y);
                    if (bounds.Contains(InputManager.GetMousePosition()))
                    {
                        _confirmSelectedIndex = i;
                        if (InputManager.IsLeftMouseButtonClicked())
                        {
                            ExecuteConfirm();
                            InputManager.ConsumeClick();
                        }
                    }
                }
            }
            if (IsKeyPressed(Keys.Up) || IsKeyPressed(Keys.Left) || IsButtonPressed(Buttons.DPadUp) || IsButtonPressed(Buttons.DPadLeft))
            {
                _confirmSelectedIndex = (_confirmSelectedIndex - 1 + 2) % 2;
            }
            if (IsKeyPressed(Keys.Down) || IsKeyPressed(Keys.Right) || IsButtonPressed(Buttons.DPadDown) || IsButtonPressed(Buttons.DPadRight))
            {
                _confirmSelectedIndex = (_confirmSelectedIndex + 1) % 2;
            }
            if (IsKeyPressed(Keys.Enter) || IsButtonPressed(Buttons.A))
            {
                ExecuteConfirm();
            }
            if (IsKeyPressed(Keys.Escape) || IsButtonPressed(Buttons.B))
            {
                _confirmingDiscard = false;
            }
            _previousKeyboardState = _keyboardState;
            _previousGamePadState = _gamePadState;
            return;
        }

        if (_resetConfirmStep > 0)
        {
            HandleResetConfirmInput();
            _previousKeyboardState = _keyboardState;
            _previousGamePadState = _gamePadState;
            return;
        }

        if (_resetProgressConfirmStep > 0)
        {
            HandleResetProgressConfirmInput();
            _previousKeyboardState = _keyboardState;
            _previousGamePadState = _gamePadState;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, GetRowLabels().Length - 1);

        if (_font != null)
        {
            var labels  = GetRowLabels();
            var viewport = _graphics.GraphicsDevice.Viewport;
            var startY   = viewport.Height / 2f - labels.Length * RowSpacing / 2f;

            // Text rows — skip row 2 (button row)
            for (var i = 0; i < labels.Length; i++)
            {
                if (i == 2) { continue; }
                var textSize = _font.MeasureString(labels[i]);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * RowSpacing);
                var bounds   = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _selectedIndex = i;
                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        if (i == 1 && !_pendingIsFullScreen)
                        {
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

            // Button row hit detection
            var buttonRowY  = startY + 2 * RowSpacing;
            var applyRect   = new Rectangle((int)(viewport.Width / 2f - ButtonGap / 2f - ButtonWidth), (int)buttonRowY, (int)ButtonWidth, (int)ButtonHeight);
            var discardRect = new Rectangle((int)(viewport.Width / 2f + ButtonGap / 2f),               (int)buttonRowY, (int)ButtonWidth, (int)ButtonHeight);

            if (applyRect.Contains(InputManager.GetMousePosition()))
            {
                _selectedIndex = 2; _buttonIndex = 0;
                if (InputManager.IsLeftMouseButtonClicked()) { ExecuteSelection(); InputManager.ConsumeClick(); }
            }
            else if (discardRect.Contains(InputManager.GetMousePosition()))
            {
                _selectedIndex = 2; _buttonIndex = 1;
                if (InputManager.IsLeftMouseButtonClicked()) { ExecuteSelection(); InputManager.ConsumeClick(); }
            }
        }

        // Keyboard navigation
        int rowCount = GetRowLabels().Length;
        if (IsKeyPressed(Keys.Up) || IsButtonPressed(Buttons.DPadUp))   { _selectedIndex = (_selectedIndex - 1 + rowCount) % rowCount; }
        if (IsKeyPressed(Keys.Down) || IsButtonPressed(Buttons.DPadDown)) { _selectedIndex = (_selectedIndex + 1) % rowCount; }

        if (_selectedIndex == 1 && !_pendingIsFullScreen)
        {
            if (IsKeyPressed(Keys.Left) || IsButtonPressed(Buttons.DPadLeft))  { CycleResolution(-1); }
            if (IsKeyPressed(Keys.Right) || IsButtonPressed(Buttons.DPadRight)) { CycleResolution(1); }
        }

        if (_selectedIndex == 2)
        {
            if (IsKeyPressed(Keys.Left) || IsButtonPressed(Buttons.DPadLeft))  { _buttonIndex = 0; }
            if (IsKeyPressed(Keys.Right) || IsButtonPressed(Buttons.DPadRight)) { _buttonIndex = 1; }
        }

        if (IsKeyPressed(Keys.Enter) || IsButtonPressed(Buttons.A)) { ExecuteSelection(); }

        if (IsKeyPressed(Keys.Escape) || IsButtonPressed(Buttons.B))
        {
            if (HasPendingChanges) { _confirmingDiscard = true; _confirmSelectedIndex = 1; }
            else { _sceneManager.PopScene(this); }
        }

        _previousKeyboardState = _keyboardState;
        _previousGamePadState = _gamePadState;
    }

    // Only updates the pending index — does not touch graphics until Apply is pressed
    private void CycleResolution(int dir)
    {
        var count = _availableResolutions.Length;
        _pendingResolutionIndex = (_pendingResolutionIndex + dir + count) % count;
    }

    private void ApplyChanges()
    {
        if (_pendingIsFullScreen)
        {
            var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth  = dm.Width;
            _graphics.PreferredBackBufferHeight = dm.Height;
            _graphics.IsFullScreen = true;
            DisplaySettings.IsFullScreen = true;
        }
        else
        {
            var r = _availableResolutions[_pendingResolutionIndex];
            DisplaySettings.ResolutionIndex = Array.IndexOf(DisplaySettings.Resolutions, r);
            _graphics.IsFullScreen = false;
            DisplaySettings.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth  = r.Width;
            _graphics.PreferredBackBufferHeight = r.Height;
            _deferredCenterWidth  = r.Width;
            _deferredCenterHeight = r.Height;
        }
        _graphics.ApplyChanges();
        DisplaySettings.Save();
        _appliedIsFullScreen      = _pendingIsFullScreen;
        _appliedResolutionIndex   = _pendingResolutionIndex;
    }

    private void DiscardChanges()
    {
        _pendingIsFullScreen    = _appliedIsFullScreen;
        _pendingResolutionIndex = _appliedResolutionIndex;
    }

    private void ExecuteConfirm()
    {
        if (_confirmSelectedIndex == 0)
        {
            _sceneManager.PopScene(this); // Discard & Go Back
        }
        else
        {
            _confirmingDiscard = false;   // Keep Editing
        }
    }

    private void ExecuteSelection()
    {
        switch (_selectedIndex)
        {
            case 0:
                _pendingIsFullScreen = !_pendingIsFullScreen;
                break;
            case 1:
                if (!_pendingIsFullScreen) { CycleResolution(1); }
                break;
            case 2:
                if (_buttonIndex == 0 && HasPendingChanges) { ApplyChanges(); }
                else if (_buttonIndex == 1 && HasPendingChanges) { DiscardChanges(); }
                break;
#if DEBUG
            case 3:
                _resetConfirmStep = 1;
                _resetConfirmSelectedIndex = 1; // default cursor on Cancel
                break;
            case 4:
                _resetProgressConfirmStep = 1;
                _resetProgressConfirmSelectedIndex = 1; // default cursor on Cancel
                break;
            case 5:
                _sceneManager.AddScene(new DevMenuScene(_content, _sceneManager, _graphics));
                break;
#else
            case 3:
                _resetProgressConfirmStep = 1;
                _resetProgressConfirmSelectedIndex = 1; // default cursor on Cancel
                break;
#endif
            default: // Back — always the last row
                if (HasPendingChanges) { _confirmingDiscard = true; _confirmSelectedIndex = 1; }
                else { _sceneManager.PopScene(this); }
                break;
        }
    }

    // ── Reset-purchases double-confirm ──────────────────────────────────────

    private void HandleResetConfirmInput()
    {
        if (_font != null)
        {
            var viewport = _graphics.GraphicsDevice.Viewport;
            string[] opts = _resetConfirmStep == 1
                ? ["Yes, continue", "Cancel"]
                : ["Confirm Reset",  "Cancel"];
            var confirmStartY = viewport.Height / 2f - 20f;
            for (var i = 0; i < opts.Length; i++)
            {
                var textSize = _font.MeasureString(opts[i]);
                var pos      = new Vector2(viewport.Width / 2f - textSize.X / 2f, confirmStartY + i * 50f);
                var bounds   = new Rectangle((int)pos.X, (int)pos.Y, (int)textSize.X, (int)textSize.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _resetConfirmSelectedIndex = i;
                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        ExecuteResetConfirm();
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        if (IsKeyPressed(Keys.Up)   || IsKeyPressed(Keys.Left) || IsButtonPressed(Buttons.DPadUp) || IsButtonPressed(Buttons.DPadLeft))  { _resetConfirmSelectedIndex = (_resetConfirmSelectedIndex - 1 + 2) % 2; }
        if (IsKeyPressed(Keys.Down) || IsKeyPressed(Keys.Right) || IsButtonPressed(Buttons.DPadDown) || IsButtonPressed(Buttons.DPadRight)) { _resetConfirmSelectedIndex = (_resetConfirmSelectedIndex + 1) % 2; }
        if (IsKeyPressed(Keys.Enter) || IsButtonPressed(Buttons.A))  { ExecuteResetConfirm(); }
        if (IsKeyPressed(Keys.Escape) || IsButtonPressed(Buttons.B)) { _resetConfirmStep = 0; _resetConfirmSelectedIndex = 1; }
    }

    private void ExecuteResetConfirm()
    {
        if (_resetConfirmSelectedIndex == 1) // Cancel
        {
            _resetConfirmStep          = 0;
            _resetConfirmSelectedIndex = 1;
            return;
        }
        // Selected index 0 = Yes / Confirm
        if (_resetConfirmStep == 1)
        {
            _resetConfirmStep          = 2;   // advance to second prompt
            _resetConfirmSelectedIndex = 1;   // keep cursor on Cancel for safety
        }
        else
        {
            PurchaseTracker.Reset();
            _resetConfirmStep          = 0;
            _resetConfirmSelectedIndex = 1;
            _sceneManager.PopScene(this);
            _sceneManager.AddScene(new PaymentScene(_content, _sceneManager, _graphics));
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
            var labels   = GetRowLabels();
            var viewport = spriteBatch.GraphicsDevice.Viewport;
            var startY   = viewport.Height / 2f - labels.Length * RowSpacing / 2f;

            // Title
            var title     = "Options";
            var titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title,
                new Vector2(viewport.Width / 2f - titleSize.X / 2f, startY - 60f),
                (_confirmingDiscard || _resetConfirmStep > 0 || _resetProgressConfirmStep > 0) ? Color.DimGray : Color.LightGray);

            // Text rows — skip row 2 (button row)
            for (var i = 0; i < labels.Length; i++)
            {
                if (i == 2) { continue; }
                Color color;
                if (_confirmingDiscard || _resetConfirmStep > 0 || _resetProgressConfirmStep > 0) { color = Color.DimGray; }
                else if (i == 1 && _pendingIsFullScreen)             { color = Color.DarkGray; }
#if DEBUG
                else if (i == 3 || i == 4)                           { color = i == _selectedIndex ? Color.OrangeRed : new Color(180, 80, 60); }
                else if (i == 5)                                     { color = i == _selectedIndex ? Color.Cyan : Color.DarkCyan; }
#else
                else if (i == 3)                                     { color = i == _selectedIndex ? Color.OrangeRed : new Color(180, 80, 60); }
#endif
                else                                                 { color = i == _selectedIndex ? Color.Yellow : Color.White; }
                var textSize = _font.MeasureString(labels[i]);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * RowSpacing);
                spriteBatch.DrawString(_font, labels[i], position, color);
            }

            // Button row
            var buttonRowY  = startY + 2 * RowSpacing;
            var applyRect   = new Rectangle((int)(viewport.Width / 2f - ButtonGap / 2f - ButtonWidth), (int)buttonRowY, (int)ButtonWidth, (int)ButtonHeight);
            var discardRect = new Rectangle((int)(viewport.Width / 2f + ButtonGap / 2f),               (int)buttonRowY, (int)ButtonWidth, (int)ButtonHeight);

            void DrawButton(Rectangle rect, string text, bool isSelected, bool enabled, bool isDanger)
            {
                Color fill, border, textColor;
                if (_confirmingDiscard || _resetConfirmStep > 0 || _resetProgressConfirmStep > 0 || !enabled)
                {
                    fill = new Color(55, 55, 55); border = new Color(85, 85, 85); textColor = new Color(110, 110, 110);
                }
                else if (isDanger)
                {
                    fill      = isSelected ? new Color(130, 40, 40) : new Color(85, 25, 25);
                    border    = isSelected ? Color.Tomato            : new Color(160, 60, 60);
                    textColor = isSelected ? Color.Yellow            : Color.Tomato;
                }
                else
                {
                    fill      = isSelected ? new Color(40, 110, 40)  : new Color(25, 70, 25);
                    border    = isSelected ? Color.LightGreen         : new Color(55, 130, 55);
                    textColor = isSelected ? Color.Yellow            : Color.White;
                }
                // Border rect (2px on each side)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), border);
                spriteBatch.Draw(_pixel, rect, fill);
                var ts = _font.MeasureString(text);
                spriteBatch.DrawString(_font, text,
                    new Vector2(rect.X + (rect.Width - ts.X) / 2f, rect.Y + (rect.Height - ts.Y) / 2f),
                    textColor);
            }

            DrawButton(applyRect,   "Apply",   _selectedIndex == 2 && _buttonIndex == 0, HasPendingChanges, false);
            DrawButton(discardRect, "Discard", _selectedIndex == 2 && _buttonIndex == 1, HasPendingChanges, true);

            // Confirm-discard overlay
            if (_confirmingDiscard)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f);

                var warning = "Unsaved changes will be lost.";
                var warningSize = _font.MeasureString(warning);
                spriteBatch.DrawString(_font, warning,
                    new Vector2(viewport.Width / 2f - warningSize.X / 2f, viewport.Height / 2f - 80f),
                    Color.Red);

                string[] confirmOpts = ["Discard & Go Back", "Keep Editing"];
                var confirmStartY = viewport.Height / 2f - 20f;
                for (var i = 0; i < confirmOpts.Length; i++)
                {
                    var color = i == _confirmSelectedIndex ? Color.Yellow : Color.White;
                    var textSize = _font.MeasureString(confirmOpts[i]);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, confirmStartY + i * 50f);
                    spriteBatch.DrawString(_font, confirmOpts[i], position, color);
                }
            }

            // Reset-purchases confirm overlay
            if (_resetConfirmStep > 0)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f);

                var overlayTitle     = "Reset Purchases";
                var overlayTitleSize = _font.MeasureString(overlayTitle);
                spriteBatch.DrawString(_font, overlayTitle,
                    new Vector2(viewport.Width / 2f - overlayTitleSize.X / 2f, viewport.Height / 2f - 120f),
                    Color.OrangeRed);

                string resetWarning = _resetConfirmStep == 1
                    ? "All purchase records will be permanently deleted."
                    : "This cannot be undone!";
                var resetWarningSize = _font.MeasureString(resetWarning);
                spriteBatch.DrawString(_font, resetWarning,
                    new Vector2(viewport.Width / 2f - resetWarningSize.X / 2f, viewport.Height / 2f - 70f),
                    Color.Red);

                string[] resetOpts = _resetConfirmStep == 1
                    ? ["Yes, continue", "Cancel"]
                    : ["Confirm Reset",  "Cancel"];
                var resetStartY = viewport.Height / 2f - 10f;
                for (var i = 0; i < resetOpts.Length; i++)
                {
                    var color    = i == _resetConfirmSelectedIndex ? Color.Yellow : Color.White;
                    var textSize = _font.MeasureString(resetOpts[i]);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, resetStartY + i * 50f);
                    spriteBatch.DrawString(_font, resetOpts[i], position, color);
                }
            }

            // Reset-progress confirm overlay
            if (_resetProgressConfirmStep > 0)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f);

                var overlayTitle     = "Reset Progress";
                var overlayTitleSize = _font.MeasureString(overlayTitle);
                spriteBatch.DrawString(_font, overlayTitle,
                    new Vector2(viewport.Width / 2f - overlayTitleSize.X / 2f, viewport.Height / 2f - 120f),
                    Color.OrangeRed);

                string resetWarning = _resetProgressConfirmStep == 1
                    ? "All level progress and item unlocks will be permanently deleted."
                    : "This cannot be undone!";
                var resetWarningSize = _font.MeasureString(resetWarning);
                spriteBatch.DrawString(_font, resetWarning,
                    new Vector2(viewport.Width / 2f - resetWarningSize.X / 2f, viewport.Height / 2f - 70f),
                    Color.Red);

                string[] resetOpts = _resetProgressConfirmStep == 1
                    ? ["Yes, continue", "Cancel"]
                    : ["Confirm Reset",  "Cancel"];
                var resetStartY = viewport.Height / 2f - 10f;
                for (var i = 0; i < resetOpts.Length; i++)
                {
                    var color    = i == _resetProgressConfirmSelectedIndex ? Color.Yellow : Color.White;
                    var textSize = _font.MeasureString(resetOpts[i]);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, resetStartY + i * 50f);
                    spriteBatch.DrawString(_font, resetOpts[i], position, color);
                }
            }
        }
    }

    // ── Reset-progress double-confirm ───────────────────────────────────────

    private void HandleResetProgressConfirmInput()
    {
        if (_font != null)
        {
            var viewport = _graphics.GraphicsDevice.Viewport;
            string[] opts = _resetProgressConfirmStep == 1
                ? ["Yes, continue", "Cancel"]
                : ["Confirm Reset",  "Cancel"];
            var confirmStartY = viewport.Height / 2f - 20f;
            for (var i = 0; i < opts.Length; i++)
            {
                var textSize = _font.MeasureString(opts[i]);
                var pos      = new Vector2(viewport.Width / 2f - textSize.X / 2f, confirmStartY + i * 50f);
                var bounds   = new Rectangle((int)pos.X, (int)pos.Y, (int)textSize.X, (int)textSize.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _resetProgressConfirmSelectedIndex = i;
                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        ExecuteResetProgressConfirm();
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        if (IsKeyPressed(Keys.Up)   || IsKeyPressed(Keys.Left) || IsButtonPressed(Buttons.DPadUp) || IsButtonPressed(Buttons.DPadLeft))  { _resetProgressConfirmSelectedIndex = (_resetProgressConfirmSelectedIndex - 1 + 2) % 2; }
        if (IsKeyPressed(Keys.Down) || IsKeyPressed(Keys.Right) || IsButtonPressed(Buttons.DPadDown) || IsButtonPressed(Buttons.DPadRight)) { _resetProgressConfirmSelectedIndex = (_resetProgressConfirmSelectedIndex + 1) % 2; }
        if (IsKeyPressed(Keys.Enter) || IsButtonPressed(Buttons.A))  { ExecuteResetProgressConfirm(); }
        if (IsKeyPressed(Keys.Escape) || IsButtonPressed(Buttons.B)) { _resetProgressConfirmStep = 0; _resetProgressConfirmSelectedIndex = 1; }
    }

    private void ExecuteResetProgressConfirm()
    {
        if (_resetProgressConfirmSelectedIndex == 1) // Cancel
        {
            _resetProgressConfirmStep          = 0;
            _resetProgressConfirmSelectedIndex = 1;
            return;
        }
        if (_resetProgressConfirmStep == 1)
        {
            _resetProgressConfirmStep          = 2;
            _resetProgressConfirmSelectedIndex = 1;
        }
        else
        {
            UnlockTracker.Reset();
            InventoryManagement.ResetSavedLoadout();
            _resetProgressConfirmStep          = 0;
            _resetProgressConfirmSelectedIndex = 1;
        }
    }

    private bool IsKeyPressed(Keys key)
    {
        return _keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
    private bool IsButtonPressed(Buttons button)
    {
        return _gamePadState.IsButtonDown(button) && !_previousGamePadState.IsButtonDown(button);
    }
}
