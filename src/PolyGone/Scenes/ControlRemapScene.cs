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
        ("Menu Back", p => p.MenuBackKey, (p, v) => p.MenuBackKey = v),
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

    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<Row> _rows = [];
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private int _selectedIndex;
    private bool _waitingForBinding;
    private Row? _bindingRow;
    private InputBindingProfile _pendingBindings;
    private KeyboardState _previousKeyboardState;
    private GamePadState _previousGamepadState;
    private int _captureFramesToSkip;

    public ControlRemapScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content = content;
        _sceneManager = sceneManager;
        _graphics = graphics;
        _pendingBindings = InputBindings.CloneCurrent();
        _previousKeyboardState = Keyboard.GetState();
        _previousGamepadState = GamePad.GetState(PlayerIndex.One);
        _captureFramesToSkip = 0;

        _rows.Add(new Row(RowKind.Header, "Keyboard"));
        for (int i = 0; i < KeyboardMappings.Length; i++)
        {
            _rows.Add(new Row(RowKind.KeyboardBinding, KeyboardMappings[i].Label, i));
        }

        _rows.Add(new Row(RowKind.Header, "Gamepad"));
        for (int i = 0; i < GamepadMappings.Length; i++)
        {
            _rows.Add(new Row(RowKind.GamepadBinding, GamepadMappings[i].Label, i));
        }

        _rows.Add(new Row(RowKind.MoveStick, "Move Stick"));
        _rows.Add(new Row(RowKind.AimStick, "Aim Stick"));
        _rows.Add(new Row(RowKind.Apply, "Apply"));
        _rows.Add(new Row(RowKind.ResetDefaults, "Reset To Defaults"));
        _rows.Add(new Row(RowKind.Back, "Back"));

        _selectedIndex = FirstSelectableIndex();
    }

    public void Load()
    {
        _pendingBindings = InputBindings.CloneCurrent();
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
        var keyboardState = Keyboard.GetState();
        var gamepadState = GamePad.GetState(PlayerIndex.One);

        if (_waitingForBinding)
        {
            HandleBindingCapture(keyboardState, gamepadState);
            _previousKeyboardState = keyboardState;
            _previousGamepadState = gamepadState;
            return;
        }

        if (InputManager.MenuUp())
        {
            MoveSelection(-1);
        }
        if (InputManager.MenuDown())
        {
            MoveSelection(1);
        }

        var currentRow = _rows[_selectedIndex];
        if (currentRow.Kind == RowKind.MoveStick)
        {
            if (InputManager.MenuLeft() || InputManager.MenuRight())
            {
                _pendingBindings.MoveStick = _pendingBindings.MoveStick == StickBinding.Left
                    ? StickBinding.Right
                    : StickBinding.Left;
            }
        }
        else if (currentRow.Kind == RowKind.AimStick)
        {
            if (InputManager.MenuLeft() || InputManager.MenuRight())
            {
                _pendingBindings.AimStick = _pendingBindings.AimStick == StickBinding.Left
                    ? StickBinding.Right
                    : StickBinding.Left;
            }
        }

        if (InputManager.MenuConfirm())
        {
            ActivateSelectedRow();
        }

        if (InputManager.MenuBack())
        {
            _sceneManager.PopScene(this);
        }

        _previousKeyboardState = keyboardState;
        _previousGamepadState = gamepadState;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData([Color.White]);
        }

        spriteBatch.Draw(_pixel, new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height), new Color(35, 35, 35));

        if (_font == null)
        {
            return;
        }

        var viewport = spriteBatch.GraphicsDevice.Viewport;
        var title = "Controls";
        var titleSize = _font.MeasureString(title);
        spriteBatch.DrawString(_font, title, new Vector2(viewport.Width / 2f - titleSize.X / 2f, 24f), Color.White);

        var hint = _waitingForBinding
            ? "Press a key/button. Esc cancels."
            : "Use menu navigation. Confirm edits binding. Apply to save.";
        var hintSize = _font.MeasureString(hint);
        spriteBatch.DrawString(_font, hint, new Vector2(viewport.Width / 2f - hintSize.X / 2f, 64f), Color.LightGray);

        float startY = 110f;
        float rowSpacing = 34f;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var isSelected = i == _selectedIndex;
            var y = startY + i * rowSpacing;
            var labelX = 120f;
            var valueX = viewport.Width - 120f;

            if (row.Kind == RowKind.Header)
            {
                spriteBatch.DrawString(_font, row.Label, new Vector2(labelX, y), Color.Cyan);
                continue;
            }

            var color = isSelected ? Color.Yellow : Color.White;
            spriteBatch.DrawString(_font, row.Label, new Vector2(labelX, y), color);

            var value = GetValueText(row, isSelected);
            if (!string.IsNullOrEmpty(value))
            {
                var valueSize = _font.MeasureString(value);
                spriteBatch.DrawString(_font, value, new Vector2(valueX - valueSize.X, y), color);
            }
        }
    }

    private void HandleBindingCapture(KeyboardState keyboardState, GamePadState gamepadState)
    {
        if (_captureFramesToSkip > 0)
        {
            _captureFramesToSkip--;
            return;
        }

        if (TryGetNewKeyPress(keyboardState, out var key))
        {
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

    private void ActivateSelectedRow()
    {
        var row = _rows[_selectedIndex];
        switch (row.Kind)
        {
            case RowKind.KeyboardBinding:
            case RowKind.GamepadBinding:
                _waitingForBinding = true;
                _bindingRow = row;
                _captureFramesToSkip = 1;
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

    private string GetValueText(Row row, bool isSelected)
    {
        if (_waitingForBinding && isSelected && (row.Kind == RowKind.KeyboardBinding || row.Kind == RowKind.GamepadBinding))
        {
            return "[Waiting...]";
        }

        return row.Kind switch
        {
            RowKind.KeyboardBinding => KeyboardMappings[row.MappingIndex].Get(_pendingBindings).ToString(),
            RowKind.GamepadBinding => GamepadMappings[row.MappingIndex].Get(_pendingBindings).ToString(),
            RowKind.MoveStick => _pendingBindings.MoveStick.ToString(),
            RowKind.AimStick => _pendingBindings.AimStick.ToString(),
            _ => string.Empty
        };
    }

    private int FirstSelectableIndex()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Kind != RowKind.Header)
            {
                return i;
            }
        }

        return 0;
    }

    private void MoveSelection(int direction)
    {
        int next = _selectedIndex;
        do
        {
            next = (next + direction + _rows.Count) % _rows.Count;
        }
        while (_rows[next].Kind == RowKind.Header);

        _selectedIndex = next;
    }
}
