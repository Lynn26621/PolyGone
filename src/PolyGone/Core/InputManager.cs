using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace PolyGone;

/// <summary>
/// Centralized input manager to track mouse and keyboard state across scenes.
/// Prevents input events from carrying over between scene transitions.
/// </summary>
public static class InputManager
{
    // Text-input buffer for scenes that need keyboard text entry (login, PIN, etc.)
    private static readonly Queue<char> _typedChars = new();

    /// <summary>
    /// Subscribe this to <c>Game.Window.TextInput</c> in <see cref="Game1"/>.
    /// Enqueues every character (including backspace '\b') for consumption by scenes.
    /// </summary>
    public static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        _typedChars.Enqueue(e.Character);
    }

    /// <summary>
    /// Returns all characters typed since the last call and clears the internal buffer.
    /// Scenes should call this once per <c>Update</c> and process each character.
    /// A '\b' (backspace) character signals a delete-last action.
    /// </summary>
    public static string ConsumeTypedCharacters()
    {
        var sb = new StringBuilder();
        while (_typedChars.Count > 0)
            sb.Append(_typedChars.Dequeue());
        return sb.ToString();
    }

    private static MouseState _currentMouseState;
    private static MouseState _previousMouseState;
    private static KeyboardState _currentKeyboardState;
    private static KeyboardState _previousKeyboardState;
    private static GamePadState _currentGamepadState;
    private static GamePadState _previousGamepadState;
    private static bool _usingController = false;
    private static Vector2 _rightThumbstick;
    public static bool UsingController => _usingController;
    public static Vector2 RightThumbstick => _rightThumbstick;
    private static float _mouseClickCooldown = 0f;
    private static float _escapeKeyCooldown = 0f;
    private const float CLICK_COOLDOWN = 0.01f; // 10ms between clicks
    private const float ESCAPE_COOLDOWN = 0.2f; // 200ms between escape presses
    
    public static MouseState CurrentMouseState => _currentMouseState;
    public static MouseState PreviousMouseState => _previousMouseState;
    
    /// <summary>
    /// Updates the input state. Should be called once per frame in Game1.Update()
    /// </summary>
    public static void Update(GameTime gameTime)
    {
        _previousMouseState = _currentMouseState;
        _currentMouseState = Mouse.GetState();
        
        _previousKeyboardState = _currentKeyboardState;
        _currentKeyboardState = Keyboard.GetState();

        _previousGamepadState = _currentGamepadState;
        _currentGamepadState = GamePad.GetState(PlayerIndex.One);

        _rightThumbstick = new Vector2(_currentGamepadState.ThumbSticks.Right.X, -_currentGamepadState.ThumbSticks.Right.Y);

        // Detect input mode switching
        bool controllerInput = Math.Abs(_rightThumbstick.X) > 0.3f || Math.Abs(_rightThumbstick.Y) > 0.3f ||
                              Math.Abs(_currentGamepadState.ThumbSticks.Left.X) > 0.1f ||
                              Math.Abs(_currentGamepadState.ThumbSticks.Left.Y) > 0.1f ||
                              _currentGamepadState.Buttons.A == ButtonState.Pressed ||
                              _currentGamepadState.Triggers.Right > 0.1f;

        bool mouseInput = _currentMouseState.Position != _previousMouseState.Position ||
                         _currentMouseState.LeftButton == ButtonState.Pressed;

        if (controllerInput && !mouseInput)
            _usingController = true;
        else if (mouseInput)
            _usingController = false;

        // Update click cooldown
        if (_mouseClickCooldown > 0f)
            _mouseClickCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update escape key cooldown 
        if (_escapeKeyCooldown > 0f)
            _escapeKeyCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    /// <summary>
    /// Checks if the left mouse button was just clicked (pressed and released since last frame)
    /// and the cooldown has expired. This prevents double-clicks and scene transition issues. Also used for A button in gamepad mode for navigation and selection.
    /// </summary>
    public static bool IsLeftMouseButtonClicked()
    {
        bool mouseClicked = _currentMouseState.LeftButton == ButtonState.Pressed 
                            && _previousMouseState.LeftButton == ButtonState.Released 
                            && _mouseClickCooldown <= 0f;
        
        bool gamepadClicked = _currentGamepadState.Buttons.A == ButtonState.Pressed 
                              && _previousGamepadState.Buttons.A == ButtonState.Released 
                              && _mouseClickCooldown <= 0f;
        return mouseClicked || gamepadClicked;
    }

    /// <summary>
    /// Checks if the left mouse button is currently held down (any frame, no cooldown check).
    /// Use this for automatic weapons that fire while the button is held. Also checks for gamepad right trigger as that is used for firing in gamepad mode.
    /// </summary>
    public static bool IsLeftMouseButtonDown()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed 
               || _currentGamepadState.Triggers.Right > 0.1f; // Consider trigger pressed if beyond 10%
    }

    /// <summary>
    /// Consumes the click by starting the cooldown timer.
    /// Call this after handling a click to prevent it from triggering multiple actions.
    /// </summary>
    public static void ConsumeClick()
    {
        _mouseClickCooldown = CLICK_COOLDOWN;
    }

    /// <summary>
    /// Forces a reset of the click cooldown. Useful when transitioning between scenes
    /// to ensure old clicks don't carry over. 
    /// </summary>
    public static void ResetClickCooldown()
    {
        _mouseClickCooldown = CLICK_COOLDOWN;
    }

    /// <summary>
    /// Checks if the Escape key was just pressed (down now, up before).
    /// This prevents the key press from being processed by multiple scenes by tracking state globally. Also used for the start button and B button on gamepad in gamepad mode.
    /// </summary>
    public static bool IsEscapeKeyPressed()
    {
        bool escapePressed = _currentKeyboardState.IsKeyDown(Keys.Escape) 
                             && _previousKeyboardState.IsKeyUp(Keys.Escape) 
                             && _escapeKeyCooldown <= 0f;
        
        bool gamepadStartPressed = _currentGamepadState.Buttons.Start == ButtonState.Pressed 
                                   && _previousGamepadState.Buttons.Start == ButtonState.Released 
                                   && _escapeKeyCooldown <= 0f;
        
        return escapePressed || gamepadStartPressed;
    }

    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    public static Point GetMousePosition()
    {
        return _currentMouseState.Position;
    }
}
