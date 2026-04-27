using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PolyGone;

internal class ControlRemapScene : IScene
{
    private enum Tab { Keyboard, Gamepad }

    private enum RowKind
    {
        Header,
        KeyboardBinding,
        GamepadBinding,
        MoveStick,
        AimStick,
        Apply,
        ResetDefaults,
        Back,
    }

    private sealed record Row(RowKind Kind, string Label, int MappingIndex = -1);

    private static readonly Buttons[] AllowedRemapButtons =
    [
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftTrigger, Buttons.RightTrigger,
        Buttons.Start, Buttons.Back,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        Buttons.LeftStick, Buttons.RightStick
    ];

    private static readonly (string Label, Func<InputBindingProfile, Keys> Get, Action<InputBindingProfile, Keys> Set)[] KeyboardMappings =
    [
        ("Menu Up", p => p.MenuUpKey, (p, v) => p.MenuUpKey = v),
        ("Menu Down", p => p.MenuDownKey, (p, v) => p.MenuDownKey = v),
        ("Menu Left", p => p.MenuLeftKey, (p, v) => p.MenuLeftKey = v),
        ("Menu Right", p => p.MenuRightKey, (p, v) => p.MenuRightKey = v),
        ("Menu Confirm", p => p.MenuConfirmKey, (p, v) => p.MenuConfirmKey = v),
        // Escape is always active for Menu Back; this binding is an optional second key.
        ("Menu Back  [Esc always on]", p => p.MenuBackKey, (p, v) => p.MenuBackKey = v),
        ("Pause", p => p.PauseKey, (p, v) => p.PauseKey = v),
        ("Move Left", p => p.MoveLeftKey, (p, v) => p.MoveLeftKey = v),
        ("Move Right", p => p.MoveRightKey, (p, v) => p.MoveRightKey = v),
        ("Jump", p => p.JumpKey, (p, v) => p.JumpKey = v),
        ("Drop", p => p.DropKey, (p, v) => p.DropKey = v),
        ("Loadout Skip", p => p.LoadoutSkipKey, (p, v) => p.LoadoutSkipKey = v),
    ];

    private static readonly (string Label, Func<InputBindingProfile, Buttons> Get, Action<InputBindingProfile, Buttons> Set)[] GamepadMappings =
    [
        ("Menu Up", p => p.MenuUpButton, (p, v) => p.MenuUpButton = v),
        ("Menu Down", p => p.MenuDownButton, (p, v) => p.MenuDownButton = v),
        ("Menu Left", p => p.MenuLeftButton, (p, v) => p.MenuLeftButton = v),
        ("Menu Right", p => p.MenuRightButton, (p, v) => p.MenuRightButton = v),
        ("Menu Confirm", p => p.MenuConfirmButton, (p, v) => p.MenuConfirmButton = v),
        ("Menu Back", p => p.MenuBackButton, (p, v) => p.MenuBackButton = v),
        ("Pause", p => p.PauseButton, (p, v) => p.PauseButton = v),
        ("Jump", p => p.JumpButton, (p, v) => p.JumpButton = v),
        ("Drop", p => p.DropButton, (p, v) => p.DropButton = v),
        ("Shoot", p => p.ShootButton, (p, v) => p.ShootButton = v),
        ("Loadout Skip", p => p.LoadoutSkipButton, (p, v) => p.LoadoutSkipButton = v),
    ];

    // Layout constants
    private const float TitleY = 10f;
    private const float TabBarY = 78f;
    private const float TabBarHeight = 40f;
    private const float HintY = 122f;
    private const float RowSpacing = 48f;
    private const float ListStartY = 178f;
    private const float LabelX = 40f;
    private const float ValueX = 780f;

    private static readonly string[] TabLabels = ["Keyboard", "Gamepad"];

    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<Row> _keyboardRows = [];
    private readonly List<Row> _gamepadRows = [];
    private List<Row> CurrentRows => _currentTab == Tab.Keyboard ? _keyboardRows : _gamepadRows;

    private Texture2D? _pixel;
    private SpriteFont? _font;
    private Tab _currentTab;
    private int _selectedIndex;
    private int _scrollOffset;
    private bool _waitingForBinding;
    private Row? _bindingRow;
    private InputBindingProfile _pendingBindings;
    private KeyboardState _previousKeyboardState;
    private GamePadState _previousGamepadState;
    private MouseState _previousMouseState;
    private int _captureFramesToSkip;

    public ControlRemapScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content = content;
        _sceneManager = sceneManager;
        _graphics = graphics;
        _pendingBindings = InputBindings.CloneCurrent();
        _previousKeyboardState = Keyboard.GetState();
        _previousGamepadState = GamePad.GetState(PlayerIndex.One);
        _previousMouseState = Mouse.GetState();
        _currentTab = Tab.Keyboard;

        BuildRows();
        _selectedIndex = FirstSelectableIndex();
    }

    private void BuildRows()
    {
        _keyboardRows.Clear();
        for (int i = 0; i < KeyboardMappings.Length; i++)
            _keyboardRows.Add(new Row(RowKind.KeyboardBinding, KeyboardMappings[i].Label, i));
        _keyboardRows.Add(new Row(RowKind.Apply, "Apply"));
        _keyboardRows.Add(new Row(RowKind.ResetDefaults, "Reset To Defaults"));
        _keyboardRows.Add(new Row(RowKind.Back, "Back"));

        _gamepadRows.Clear();
        for (int i = 0; i < GamepadMappings.Length; i++)
            _gamepadRows.Add(new Row(RowKind.GamepadBinding, GamepadMappings[i].Label, i));
        _gamepadRows.Add(new Row(RowKind.Header, "-- Stick Settings --"));
        _gamepadRows.Add(new Row(RowKind.MoveStick, "Move Stick"));
        _gamepadRows.Add(new Row(RowKind.AimStick, "Aim Stick"));
        _gamepadRows.Add(new Row(RowKind.Apply, "Apply"));
        _gamepadRows.Add(new Row(RowKind.ResetDefaults, "Reset To Defaults"));
        _gamepadRows.Add(new Row(RowKind.Back, "Back"));
    }

    public void Load()
    {
        // Refresh pending bindings from the live profile each time this scene is shown.
        _pendingBindings = InputBindings.CloneCurrent();
        _scrollOffset = 0;
        _currentTab = Tab.Keyboard;
        _selectedIndex = FirstSelectableIndex();

        if (_font == null)
        {
            var fontAssetPath = Path.Combine(_content.RootDirectory, "Fonts", "PauseMenu.xnb");
            if (File.Exists(fontAssetPath))
                _font = _content.Load<SpriteFont>("Fonts/PauseMenu");
        }
    }

    public void Update(GameTime gameTime)
    {
        var keyboardState = Keyboard.GetState();
        var gamepadState = GamePad.GetState(PlayerIndex.One);
        var mouseState = Mouse.GetState();

        if (_waitingForBinding)
        {
            HandleBindingCapture(keyboardState, gamepadState);
            _previousKeyboardState = keyboardState;
            _previousGamepadState = gamepadState;
            _previousMouseState = mouseState;
            return;
        }

        // Tab switching: Q or LB = previous tab, E or RB = next tab
        bool tabPrev = (keyboardState.IsKeyDown(Keys.Q) && _previousKeyboardState.IsKeyUp(Keys.Q)) ||
                       (gamepadState.IsButtonDown(Buttons.LeftShoulder) && _previousGamepadState.IsButtonUp(Buttons.LeftShoulder));
        bool tabNext = (keyboardState.IsKeyDown(Keys.E) && _previousKeyboardState.IsKeyUp(Keys.E)) ||
                       (gamepadState.IsButtonDown(Buttons.RightShoulder) && _previousGamepadState.IsButtonUp(Buttons.RightShoulder));
        if (tabPrev)
            SwitchTab(-1);
        else if (tabNext)
            SwitchTab(1);

        // Mouse: click to activate row or switch tab
        bool mouseClicked = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
        if (mouseClicked)
        {
            HandleMouseClick(mouseState.Position);
        }
        else if (mouseState.Position != _previousMouseState.Position)
        {
            // Hover: update selected row when mouse moves (keyboard/gamepad can override)
            HandleMouseHover(mouseState.Position);
        }

        // Use raw, hardcoded navigation so this scene always works regardless of how the
        // user has remapped (or broken) their bindings.
        if (RawNavUp(keyboardState, gamepadState))
            MoveSelection(-1);

        if (RawNavDown(keyboardState, gamepadState))
            MoveSelection(1);

        var rows = CurrentRows;
        var currentRow = rows[_selectedIndex];
        if (currentRow.Kind == RowKind.MoveStick)
        {
            if (RawNavLeft(keyboardState, gamepadState) || RawNavRight(keyboardState, gamepadState))
            {
                _pendingBindings.MoveStick = _pendingBindings.MoveStick == StickBinding.Left
                    ? StickBinding.Right
                    : StickBinding.Left;
            }
        }
        else if (currentRow.Kind == RowKind.AimStick)
        {
            if (RawNavLeft(keyboardState, gamepadState) || RawNavRight(keyboardState, gamepadState))
            {
                _pendingBindings.AimStick = _pendingBindings.AimStick == StickBinding.Left
                    ? StickBinding.Right
                    : StickBinding.Left;
            }
        }

        if (RawConfirm(keyboardState, gamepadState))
            ActivateSelectedRow();

        // Escape always exits this scene (matching the hardcoded MenuBack behaviour).
        if (RawBack(keyboardState, gamepadState))
            _sceneManager.PopScene(this);

        _previousKeyboardState = keyboardState;
        _previousGamepadState = gamepadState;
        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData([Color.White]);
        }

        var viewport = spriteBatch.GraphicsDevice.Viewport;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(35, 35, 35));

        if (_font == null)
            return;

        // Title
        const string title = "Controls";
        var titleSize = _font.MeasureString(title);
        spriteBatch.DrawString(_font, title, new Vector2(viewport.Width / 2f - titleSize.X / 2f, TitleY), Color.White);

        // Tab bar
        DrawTabBar(spriteBatch, viewport);

        // Hint line
        var hint = _waitingForBinding
            ? "Press a key/button.  Esc = cancel."
            : "W/S = navigate  |  Enter or Click = remap  |  Q/E or LB/RB = switch tab  |  Esc = back";
        var hintSize = _font.MeasureString(hint);
        float hintScale = Math.Min(1f, (viewport.Width - 20f) / hintSize.X);
        spriteBatch.DrawString(_font, hint,
            new Vector2(viewport.Width / 2f - hintSize.X * hintScale / 2f, HintY),
            Color.LightGray, 0f, Vector2.Zero, hintScale, SpriteEffects.None, 0f);

        // Scroll window
        var rows = CurrentRows;
        int maxVisible = GetMaxVisibleRows(viewport.Height);
        int endIndex = Math.Min(rows.Count, _scrollOffset + maxVisible);

        // "More above" indicator
        if (_scrollOffset > 0)
        {
            const string above = "^ more above ^";
            var sz = _font.MeasureString(above);
            spriteBatch.DrawString(_font, above,
                new Vector2(viewport.Width / 2f - sz.X / 2f, ListStartY - RowSpacing),
                Color.Gray);
        }

        for (int i = _scrollOffset; i < endIndex; i++)
        {
            var row = rows[i];
            var isSelected = i == _selectedIndex;
            float y = ListStartY + (i - _scrollOffset) * RowSpacing;

            if (row.Kind == RowKind.Header)
            {
                spriteBatch.DrawString(_font, row.Label, new Vector2(LabelX, y), Color.Cyan);
                continue;
            }

            var color = isSelected ? Color.Yellow : Color.White;
            spriteBatch.DrawString(_font, row.Label, new Vector2(LabelX, y), color);

            var value = GetValueText(row, isSelected);
            if (!string.IsNullOrEmpty(value))
            {
                var valueSize = _font.MeasureString(value);
                spriteBatch.DrawString(_font, value, new Vector2(ValueX - valueSize.X, y), color);
            }
        }

        // "More below" indicator
        if (_scrollOffset + maxVisible < rows.Count)
        {
            const string below = "v more below v";
            float belowY = ListStartY + maxVisible * RowSpacing;
            var sz = _font.MeasureString(below);
            spriteBatch.DrawString(_font, below,
                new Vector2(viewport.Width / 2f - sz.X / 2f, belowY),
                Color.Gray);
        }
    }

    // ── Tab helpers ──────────────────────────────────────────────────────────

    private void SwitchTab(int direction)
    {
        int count = Enum.GetValues<Tab>().Length;
        _currentTab = (Tab)(((int)_currentTab + direction + count) % count);
        _scrollOffset = 0;
        _selectedIndex = FirstSelectableIndex();
    }

    private void DrawTabBar(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_pixel == null || _font == null)
            return;

        int tabCount = TabLabels.Length;
        float tabWidth = viewport.Width / (float)tabCount;

        for (int t = 0; t < tabCount; t++)
        {
            bool isActive = (Tab)t == _currentTab;
            float tabX = t * tabWidth;
            var bgColor = isActive ? new Color(60, 60, 130) : new Color(45, 45, 55);
            spriteBatch.Draw(_pixel, new Rectangle((int)tabX, (int)TabBarY, (int)tabWidth - 2, (int)TabBarHeight), bgColor);

            // Draw bottom border highlight for active tab
            if (isActive)
            {
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)tabX, (int)(TabBarY + TabBarHeight - 2), (int)tabWidth - 2, 2),
                    Color.CornflowerBlue);
            }

            var label = TabLabels[t];
            var labelSize = _font.MeasureString(label);
            float scale = Math.Min(1f, (tabWidth - 20f) / labelSize.X);
            var textColor = isActive ? Color.White : Color.Gray;
            spriteBatch.DrawString(_font, label,
                new Vector2(tabX + tabWidth / 2f - labelSize.X * scale / 2f,
                            TabBarY + (TabBarHeight - labelSize.Y * scale) / 2f),
                textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    // ── Mouse helpers ────────────────────────────────────────────────────────

    private void HandleMouseClick(Point mousePos)
    {
        var viewport = _graphics.GraphicsDevice.Viewport;

        // Check tab header clicks
        int tabCount = TabLabels.Length;
        float tabWidth = viewport.Width / (float)tabCount;
        for (int t = 0; t < tabCount; t++)
        {
            var tabRect = new Rectangle((int)(t * tabWidth), (int)TabBarY, (int)tabWidth - 2, (int)TabBarHeight);
            if (tabRect.Contains(mousePos))
            {
                if (_currentTab != (Tab)t)
                {
                    _currentTab = (Tab)t;
                    _scrollOffset = 0;
                    _selectedIndex = FirstSelectableIndex();
                }
                return;
            }
        }

        // Check row clicks
        var rows = CurrentRows;
        int maxVisible = GetMaxVisibleRows(viewport.Height);
        for (int i = _scrollOffset; i < Math.Min(rows.Count, _scrollOffset + maxVisible); i++)
        {
            if (rows[i].Kind == RowKind.Header)
                continue;
            float y = ListStartY + (i - _scrollOffset) * RowSpacing;
            var rowRect = new Rectangle(0, (int)y, viewport.Width, (int)RowSpacing);
            if (rowRect.Contains(mousePos))
            {
                _selectedIndex = i;
                ActivateSelectedRow();
                return;
            }
        }
    }

    private void HandleMouseHover(Point mousePos)
    {
        var viewport = _graphics.GraphicsDevice.Viewport;
        var rows = CurrentRows;
        int maxVisible = GetMaxVisibleRows(viewport.Height);
        for (int i = _scrollOffset; i < Math.Min(rows.Count, _scrollOffset + maxVisible); i++)
        {
            if (rows[i].Kind == RowKind.Header)
                continue;
            float y = ListStartY + (i - _scrollOffset) * RowSpacing;
            var rowRect = new Rectangle(0, (int)y, viewport.Width, (int)RowSpacing);
            if (rowRect.Contains(mousePos))
            {
                _selectedIndex = i;
                break;
            }
        }
    }

    // ── Raw navigation helpers ───────────────────────────────────────────────
    // These use hardcoded keys so navigation is always reliable regardless of
    // how the user's bindings are currently configured.

    private bool RawNavUp(KeyboardState kb, GamePadState gp)
    {
        return (kb.IsKeyDown(Keys.W) && _previousKeyboardState.IsKeyUp(Keys.W)) ||
               (kb.IsKeyDown(Keys.Up) && _previousKeyboardState.IsKeyUp(Keys.Up)) ||
               (gp.DPad.Up == ButtonState.Pressed && _previousGamepadState.DPad.Up == ButtonState.Released) ||
               (gp.ThumbSticks.Left.Y > 0.3f && _previousGamepadState.ThumbSticks.Left.Y <= 0.3f);
    }

    private bool RawNavDown(KeyboardState kb, GamePadState gp)
    {
        return (kb.IsKeyDown(Keys.S) && _previousKeyboardState.IsKeyUp(Keys.S)) ||
               (kb.IsKeyDown(Keys.Down) && _previousKeyboardState.IsKeyUp(Keys.Down)) ||
               (gp.DPad.Down == ButtonState.Pressed && _previousGamepadState.DPad.Down == ButtonState.Released) ||
               (gp.ThumbSticks.Left.Y < -0.3f && _previousGamepadState.ThumbSticks.Left.Y >= -0.3f);
    }

    private bool RawNavLeft(KeyboardState kb, GamePadState gp)
    {
        return (kb.IsKeyDown(Keys.A) && _previousKeyboardState.IsKeyUp(Keys.A)) ||
               (kb.IsKeyDown(Keys.Left) && _previousKeyboardState.IsKeyUp(Keys.Left)) ||
               (gp.DPad.Left == ButtonState.Pressed && _previousGamepadState.DPad.Left == ButtonState.Released);
    }

    private bool RawNavRight(KeyboardState kb, GamePadState gp)
    {
        return (kb.IsKeyDown(Keys.D) && _previousKeyboardState.IsKeyUp(Keys.D)) ||
               (kb.IsKeyDown(Keys.Right) && _previousKeyboardState.IsKeyUp(Keys.Right)) ||
               (gp.DPad.Right == ButtonState.Pressed && _previousGamepadState.DPad.Right == ButtonState.Released);
    }

    private bool RawConfirm(KeyboardState kb, GamePadState gp)
    {
        return (kb.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
               (gp.Buttons.A == ButtonState.Pressed && _previousGamepadState.Buttons.A == ButtonState.Released);
    }

    private bool RawBack(KeyboardState kb, GamePadState gp)
    {
        return kb.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape);
    }

    // ── Binding capture ──────────────────────────────────────────────────────

    private void HandleBindingCapture(KeyboardState keyboardState, GamePadState gamepadState)
    {
        if (_captureFramesToSkip > 0)
        {
            _captureFramesToSkip--;
            return;
        }

        if (TryGetNewKeyPress(keyboardState, out var key))
        {
            // Escape always cancels capture — it is never stored as a binding value.
            if (key == Keys.Escape)
            {
                _waitingForBinding = false;
                _bindingRow = null;
                return;
            }

            ApplyCapturedBinding(key, null);
            return;
        }

        if (TryGetNewGamepadPress(gamepadState, out var button))
        {
            ApplyCapturedBinding(null, button);
        }
    }

    private void ApplyCapturedBinding(Keys? key, Buttons? button)
    {
        if (_bindingRow == null)
        {
            _waitingForBinding = false;
            return;
        }

        if (_bindingRow.Kind == RowKind.KeyboardBinding && key.HasValue)
        {
            KeyboardMappings[_bindingRow.MappingIndex].Set(_pendingBindings, key.Value);
            _waitingForBinding = false;
            _bindingRow = null;
            return;
        }

        if (_bindingRow.Kind == RowKind.GamepadBinding && button.HasValue)
        {
            GamepadMappings[_bindingRow.MappingIndex].Set(_pendingBindings, button.Value);
            _waitingForBinding = false;
            _bindingRow = null;
        }
    }

    private bool TryGetNewKeyPress(KeyboardState keyboardState, out Keys key)
    {
        foreach (var pressedKey in keyboardState.GetPressedKeys())
        {
            if (_previousKeyboardState.IsKeyUp(pressedKey))
            {
                key = pressedKey;
                return true;
            }
        }

        key = Keys.None;
        return false;
    }

    private bool TryGetNewGamepadPress(GamePadState gamepadState, out Buttons button)
    {
        foreach (var candidate in AllowedRemapButtons)
        {
            if (gamepadState.IsButtonDown(candidate) && _previousGamepadState.IsButtonUp(candidate))
            {
                button = candidate;
                return true;
            }
        }

        button = default;
        return false;
    }

    // ── Row activation ───────────────────────────────────────────────────────

    private void ActivateSelectedRow()
    {
        var row = CurrentRows[_selectedIndex];
        switch (row.Kind)
        {
            case RowKind.KeyboardBinding:
            case RowKind.GamepadBinding:
                _waitingForBinding = true;
                _bindingRow = row;
                _captureFramesToSkip = 2; // skip the frame where Enter was pressed
                break;
            case RowKind.MoveStick:
                _pendingBindings.MoveStick = _pendingBindings.MoveStick == StickBinding.Left
                    ? StickBinding.Right
                    : StickBinding.Left;
                break;
            case RowKind.AimStick:
                _pendingBindings.AimStick = _pendingBindings.AimStick == StickBinding.Left
                    ? StickBinding.Right
                    : StickBinding.Left;
                break;
            case RowKind.Apply:
                InputBindings.Apply(_pendingBindings, save: true);
                _sceneManager.PopScene(this);
                break;
            case RowKind.ResetDefaults:
                _pendingBindings = InputBindings.CreateDefaults();
                break;
            case RowKind.Back:
                _sceneManager.PopScene(this);
                break;
        }
    }

    // ── Display helpers ──────────────────────────────────────────────────────

    private string GetValueText(Row row, bool isSelected)
    {
        if (_waitingForBinding && isSelected && (row.Kind == RowKind.KeyboardBinding || row.Kind == RowKind.GamepadBinding))
        {
            return "[Press key / button...]";
        }

        return row.Kind switch
        {
            RowKind.KeyboardBinding => KeyDisplayName(KeyboardMappings[row.MappingIndex].Get(_pendingBindings)),
            RowKind.GamepadBinding => ButtonDisplayName(GamepadMappings[row.MappingIndex].Get(_pendingBindings)),
            RowKind.MoveStick => _pendingBindings.MoveStick.ToString(),
            RowKind.AimStick => _pendingBindings.AimStick.ToString(),
            _ => string.Empty
        };
    }

    private static string KeyDisplayName(Keys key)
    {
        return key == Keys.None ? "-" : key.ToString();
    }

    private static string ButtonDisplayName(Buttons button)
    {
        return (int)button == 0 ? "-" : button.ToString();
    }

    // ── Scrolling helpers ────────────────────────────────────────────────────

    private int GetMaxVisibleRows(int viewportHeight)
    {
        return Math.Max(1, (int)((viewportHeight - ListStartY - 30) / RowSpacing));
    }

    private void EnsureSelectedVisible()
    {
        int maxVisible = GetMaxVisibleRows(_graphics.PreferredBackBufferHeight);
        if (_selectedIndex < _scrollOffset)
        {
            _scrollOffset = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollOffset + maxVisible)
        {
            _scrollOffset = _selectedIndex - maxVisible + 1;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, CurrentRows.Count - maxVisible));
    }

    // ── Selection helpers ────────────────────────────────────────────────────

    private int FirstSelectableIndex()
    {
        var rows = CurrentRows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Kind != RowKind.Header)
                return i;
        }
        return 0;
    }

    private void MoveSelection(int direction)
    {
        var rows = CurrentRows;
        int next = _selectedIndex;
        do
        {
            next = (next + direction + rows.Count) % rows.Count;
        }
        while (rows[next].Kind == RowKind.Header);

        _selectedIndex = next;
        EnsureSelectedVisible();
    }
}
