using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        { 
            sb.Append(_typedChars.Dequeue());
        }
        return sb.ToString();
    }

    private static MouseState _currentMouseState;
    private static MouseState _previousMouseState;
    private static KeyboardState _currentKeyboardState;
    private static KeyboardState _previousKeyboardState;
    private static GamePadState _currentGamepadState;
    private static GamePadState _previousGamepadState;
    private static float thumbstickX;
    private static float thumbstickY;
    private static bool _usingController = false;
    private static Vector2 _rightThumbstick;
    public static bool UsingController => _usingController;
    public static Vector2 RightThumbstick => _rightThumbstick;
    private static float _mouseClickCooldown = 0f;
    private static float _escapeKeyCooldown = 0f;
    private static float _dashCooldown = 0f;
    private static float _menuUpHoldTimer = 0f;
    private static float _menuDownHoldTimer = 0f;
    private static float _menuLeftHoldTimer = 0f;
    private static float _menuRightHoldTimer = 0f;
    private static float _menuUpAutoRepeatTimer = 0f;
    private static float _menuDownAutoRepeatTimer = 0f;
    private static float _menuLeftAutoRepeatTimer = 0f;
    private static float _menuRightAutoRepeatTimer = 0f;
    private const float MENU_AUTO_REPEAT_INTERVAL = 0.1f;
    private const float CLICK_COOLDOWN = 0.01f; // 10ms between clicks
    private const float ESCAPE_COOLDOWN = 0.2f; // 200ms between escape presses
    private const float DASH_COOLDOWN = 2.5f; // 2500ms between dashes

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

        thumbstickX = _currentGamepadState.ThumbSticks.Left.X;
        thumbstickY = _currentGamepadState.ThumbSticks.Left.Y;

        _rightThumbstick = new Vector2(_currentGamepadState.ThumbSticks.Right.X, -_currentGamepadState.ThumbSticks.Right.Y);

        // Detect input mode switching
        bool controllerInput = Math.Abs(_rightThumbstick.X) > 0.3f || Math.Abs(_rightThumbstick.Y) > 0.3f ||
                              Math.Abs(_currentGamepadState.ThumbSticks.Left.X) > 0.1f ||
                              Math.Abs(_currentGamepadState.ThumbSticks.Left.Y) > 0.1f ||
                              _currentGamepadState.Buttons.A == ButtonState.Pressed ||
                              _currentGamepadState.Triggers.Right > 0.1f;

        bool mouseInput = _currentMouseState.Position != _previousMouseState.Position ||
                         _currentMouseState.LeftButton == ButtonState.Pressed;

        bool keyboardInput = _currentKeyboardState.GetPressedKeys().Length > 0;

        bool menuUpHeld = _currentKeyboardState.IsKeyDown(Keys.W) ||
                          thumbstickY > 0.3f ||
                          _currentGamepadState.DPad.Up == ButtonState.Pressed ||
                            _currentKeyboardState.IsKeyDown(Keys.Up);
        bool menuDownHeld = _currentKeyboardState.IsKeyDown(Keys.S) ||
                            thumbstickY < -0.3f ||
                            _currentGamepadState.DPad.Down == ButtonState.Pressed ||
                            _currentKeyboardState.IsKeyDown(Keys.Down);
        bool menuLeftHeld = _currentKeyboardState.IsKeyDown(Keys.A) ||
                            thumbstickX < -0.3f ||
                            _currentGamepadState.DPad.Left == ButtonState.Pressed ||
                            _currentKeyboardState.IsKeyDown(Keys.Left);
        bool menuRightHeld = _currentKeyboardState.IsKeyDown(Keys.D) ||
                             thumbstickX > 0.3f ||
                             _currentGamepadState.DPad.Right == ButtonState.Pressed ||
                                _currentKeyboardState.IsKeyDown(Keys.Right);

        if (controllerInput && !mouseInput)
        {
            _usingController = true;
        }
        else if (mouseInput)
        {
            _usingController = false;
        }

        // Update click cooldown
        if (_mouseClickCooldown > 0f)
        {
            _mouseClickCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        // Update menu hold timers
        if (menuUpHeld)
        {
            _menuUpHoldTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        else
        {
            _menuUpHoldTimer = 0f;
        }

        if (menuDownHeld)
        {
            _menuDownHoldTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        }
        else
        {
            _menuDownHoldTimer = 0f;
        }

        if (menuLeftHeld)
        {
            _menuLeftHoldTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        }
        else
        {
            _menuLeftHoldTimer = 0f;
        }
        
        if (menuRightHeld)
        {
            _menuRightHoldTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

        }
        else
        {
            _menuRightHoldTimer = 0f;
        }

        // Update escape key cooldown 
        if (PauseMenuOpen() || PauseMenuClose() || MenuBack())
        {
            _escapeKeyCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        }
        _menuUpAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _menuDownAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _menuLeftAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _menuRightAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
    }


    /// <summary>
    /// Consumes the click by starting the cooldown timer.
    /// Call this after handling a click to prevent it from triggering multiple actions.
    /// </summary>
    public static void ConsumeClick()
    {
        _mouseClickCooldown = CLICK_COOLDOWN;
    }
    // Function for single shooting (left click for mouse and right trigger for gamepad)
    public static bool GameShootSingle()
        {
        bool mouseClicked = _currentMouseState.LeftButton == ButtonState.Pressed
                            && _previousMouseState.LeftButton == ButtonState.Released
                            && _mouseClickCooldown <= 0f;
        bool gamepadClicked = _currentGamepadState.Triggers.Right > 0.1f 
                              && _previousGamepadState.Triggers.Right <= 0.1f 
                              && _mouseClickCooldown <= 0f;
        return mouseClicked || gamepadClicked;
    }
    // function for automatic shooting (holding left click for mouse and holding right trigger for gamepad)
    public static bool GameShootHold()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed 
               || _currentGamepadState.Triggers.Right > 0.1f; 
    }
    // function for game jumping (space for keyboard and A button for gamepad)
    public static bool GameJump()
    {
        bool keyboardJump = _currentKeyboardState.IsKeyDown(Keys.Space)
                            && !_previousKeyboardState.IsKeyDown(Keys.Space);
        bool gamepadJump = _currentGamepadState.Buttons.A == ButtonState.Pressed
                           && _previousGamepadState.Buttons.A == ButtonState.Released;
        return keyboardJump || gamepadJump;
    }

    // function for moving left in game (A key for keyboard and left thumbstick left for gamepad)
    public static bool GameMoveLeft()
    {
        bool keyboardLeft = _currentKeyboardState.IsKeyDown(Keys.A);
        bool gamepadLeft = thumbstickX < -0.3f; 
        return keyboardLeft || gamepadLeft;
    }
    // function for moving right in game (D key for keyboard and left thumbstick right for gamepad)
    public static bool GameMoveRight()
    {
        bool keyboardRight = _currentKeyboardState.IsKeyDown(Keys.D);
        bool gamepadRight = thumbstickX > 0.3f; 
        return keyboardRight || gamepadRight;
    }
    // function for dropping through platforms in game (S key for keyboard and left thumbstick down for gamepad)
    public static bool GameDrop()
    {
        bool keyboardDrop = _currentKeyboardState.IsKeyDown(Keys.S);
        bool gamepadDrop = thumbstickY < -0.9f; 
        return keyboardDrop || gamepadDrop;
    }
    // function for aiming in game (mouse position for keyboard and right thumbstick for gamepad)
    public static Vector2 GameAim()
    {
        if (_usingController)
        {
            return _rightThumbstick;
        }
        else
        {
            Point mousePos = GetMousePosition();
            return new Vector2(mousePos.X, mousePos.Y);
        }
    }
    //function for opening the pause menu while in game (Escape key for keyboard and Start button for gamepad)
    public static bool PauseMenuOpen()
    {
        bool keyboardPause = _currentKeyboardState.IsKeyDown(Keys.Escape) 
                             && _previousKeyboardState.IsKeyUp(Keys.Escape) 
                             && _escapeKeyCooldown <= 0f;
        bool gamepadPause = _currentGamepadState.Buttons.Start == ButtonState.Pressed 
                            && _previousGamepadState.Buttons.Start == ButtonState.Released 
                            && _escapeKeyCooldown <= 0f;
        return keyboardPause || gamepadPause;
    }
    //function for closing the pause menu while in game (Escape key for keyboard and Start button for gamepad)
    public static bool PauseMenuClose()
    {
        bool keyboardPause = _currentKeyboardState.IsKeyDown(Keys.Escape) 
                             && _previousKeyboardState.IsKeyUp(Keys.Escape) 
                             && _escapeKeyCooldown <= 0f;
        bool gamepadPause = _currentGamepadState.Buttons.Start == ButtonState.Pressed 
                            && _previousGamepadState.Buttons.Start == ButtonState.Released 
                            && _escapeKeyCooldown <= 0f;
        return keyboardPause || gamepadPause;
    }
    //function for navigating up in menus (W/Up key for keyboard and left thumbstick up for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuUp()
    {
        bool keyboardUp = (_currentKeyboardState.IsKeyDown(Keys.W) && _previousKeyboardState.IsKeyUp(Keys.W))
                          || (_currentKeyboardState.IsKeyDown(Keys.Up) && _previousKeyboardState.IsKeyUp(Keys.Up));
        bool gamepadUp = thumbstickY > 0.3f
                          && _previousGamepadState.ThumbSticks.Left.Y <= 0.3f;
        bool dPadUp = _currentGamepadState.DPad.Up == ButtonState.Pressed
                      && _previousGamepadState.DPad.Up == ButtonState.Released;
        bool menuUpCurrentlyHeld = _currentKeyboardState.IsKeyDown(Keys.W)
                                   || _currentKeyboardState.IsKeyDown(Keys.Up)
                                   || thumbstickY > 0.3f
                                   || _currentGamepadState.DPad.Up == ButtonState.Pressed;
        bool autoRepeat = menuUpCurrentlyHeld &&
                  _menuUpHoldTimer >= 1.0f &&
                  _menuUpAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat) 
        {
            _menuUpAutoRepeatTimer = 0f;
        }
        return keyboardUp || gamepadUp || dPadUp || autoRepeat;
        
    }
    //function for navigating down in menus (S key for keyboard and left thumbstick down for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuDown()
    {
        bool keyboardDown = (_currentKeyboardState.IsKeyDown(Keys.S) && _previousKeyboardState.IsKeyUp(Keys.S))
                          || (_currentKeyboardState.IsKeyDown(Keys.Down) && _previousKeyboardState.IsKeyUp(Keys.Down));
        bool gamepadDown = thumbstickY < -0.3f
                            && _previousGamepadState.ThumbSticks.Left.Y >= -0.3f;
        bool dPadDown = _currentGamepadState.DPad.Down == ButtonState.Pressed
                        && _previousGamepadState.DPad.Down == ButtonState.Released;
        bool menuDownCurrentlyHeld = _currentKeyboardState.IsKeyDown(Keys.S) || _currentKeyboardState.IsKeyDown(Keys.Down) || thumbstickY < -0.3f || _currentGamepadState.DPad.Down == ButtonState.Pressed;
        bool autoRepeat = menuDownCurrentlyHeld &&
                  _menuDownHoldTimer >= 1.0f &&
                  _menuDownAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuDownAutoRepeatTimer = 0f;
        }
        return keyboardDown || gamepadDown || dPadDown || autoRepeat;
    }
    //function for navigating left in menus (A key for keyboard and left thumbstick left for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuLeft()
    {
        bool keyboardLeft = (_currentKeyboardState.IsKeyDown(Keys.A) && _previousKeyboardState.IsKeyUp(Keys.A))
                          || (_currentKeyboardState.IsKeyDown(Keys.Left) && _previousKeyboardState.IsKeyUp(Keys.Left));
        bool gamepadLeft = thumbstickX < -0.3f
                            && _previousGamepadState.ThumbSticks.Left.X >= -0.3f;
        bool dPadLeft = _currentGamepadState.DPad.Left == ButtonState.Pressed
                        && _previousGamepadState.DPad.Left == ButtonState.Released;
        bool menuLeftCurrentlyHeld = _currentKeyboardState.IsKeyDown(Keys.A) || _currentKeyboardState.IsKeyDown(Keys.Left) || thumbstickX < -0.3f || _currentGamepadState.DPad.Left == ButtonState.Pressed;
        bool autoRepeat = menuLeftCurrentlyHeld &&
                  _menuLeftHoldTimer >= 0.5f &&
                  _menuLeftAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuLeftAutoRepeatTimer = 0f;
        }
        return keyboardLeft || gamepadLeft || dPadLeft || autoRepeat;
    }
    //function for navigating right in menus (D key for keyboard and left thumbstick right for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuRight()
    {
        bool keyboardRight = (_currentKeyboardState.IsKeyDown(Keys.D) && _previousKeyboardState.IsKeyUp(Keys.D))
                           || (_currentKeyboardState.IsKeyDown(Keys.Right) && _previousKeyboardState.IsKeyUp(Keys.Right));
        bool gamepadRight = thumbstickX > 0.3f
                            && _previousGamepadState.ThumbSticks.Left.X <= 0.3f;
        bool dPadRight = _currentGamepadState.DPad.Right == ButtonState.Pressed
                        && _previousGamepadState.DPad.Right == ButtonState.Released;
        bool menuRightCurrentlyHeld = _currentKeyboardState.IsKeyDown(Keys.D) || _currentKeyboardState.IsKeyDown(Keys.Right) || thumbstickX > 0.3f || _currentGamepadState.DPad.Right == ButtonState.Pressed;
        bool autoRepeat = menuRightCurrentlyHeld &&
                  _menuRightHoldTimer >= 0.5f &&
                  _menuRightAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuRightAutoRepeatTimer = 0f;
        }
        return keyboardRight || gamepadRight || dPadRight || autoRepeat;
    }

    //function for selecting a menu option with the mouse only (left click)
    public static bool MenuMouseConfirm()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed
               && _previousMouseState.LeftButton == ButtonState.Released
               && _mouseClickCooldown <= 0f;
    }
    //function for selecting a menu option without the mouse (Enter key for keyboard and A button for gamepad)
    public static bool MenuNonPointerConfirm()
    {
        bool keyboardConfirm = _currentKeyboardState.IsKeyDown(Keys.Enter)
                               && _previousKeyboardState.IsKeyUp(Keys.Enter)
                               && _escapeKeyCooldown <= 0f;
        bool gamepadConfirm = _currentGamepadState.Buttons.A == ButtonState.Pressed
                              && _previousGamepadState.Buttons.A == ButtonState.Released
                              && _mouseClickCooldown <= 0f;
        return keyboardConfirm || gamepadConfirm;
    }
    //function for selecting a menu option from any supported input source
    public static bool MenuConfirm()
    {
        return MenuMouseConfirm() || MenuNonPointerConfirm();
    }

    public static bool MenuConfirmHold()
    {
        bool mouseHold = _currentMouseState.LeftButton == ButtonState.Pressed
                         && _mouseClickCooldown <= 0f;
        bool keyboardHold = _currentKeyboardState.IsKeyDown(Keys.Enter)
                            && _escapeKeyCooldown <= 0f;
        bool gamepadHold = _currentGamepadState.Buttons.A == ButtonState.Pressed
                           && _mouseClickCooldown <= 0f;
        return mouseHold || keyboardHold || gamepadHold;
    }

    public static bool MenuConfirmMouseClick()
    {
        bool mouseConfirm = _currentMouseState.LeftButton == ButtonState.Pressed
                            && _previousMouseState.LeftButton == ButtonState.Released
                            && _mouseClickCooldown <= 0f;
        return mouseConfirm;
    }
    //function for going back in menus (Escape key for keyboard, and B button for gamepad)
    public static bool MenuBack()
    {
        bool keyboardBack = _currentKeyboardState.IsKeyDown(Keys.Escape) 
                            && _previousKeyboardState.IsKeyUp(Keys.Escape) 
                            && _escapeKeyCooldown <= 0f;
        bool gamepadBack = _currentGamepadState.Buttons.B == ButtonState.Pressed 
                           && _previousGamepadState.Buttons.B == ButtonState.Released 
                           && _escapeKeyCooldown <= 0f;
        return keyboardBack || gamepadBack;
    }
    //function for navigating to the left section of the loadout selection screen (left arrow for keyboard, and left thumbstick left for gamepad)
    public static bool LoadoutSectionLeft()
    {
        bool keyboardLeft = _currentKeyboardState.IsKeyDown(Keys.Left)
                            && _previousKeyboardState.IsKeyUp(Keys.Left);
        bool gamepadLeft = thumbstickX < -0.3f
                            && _previousGamepadState.ThumbSticks.Left.X >= -0.3f;
        return keyboardLeft || gamepadLeft;
    }
    //function for navigating to the right section of the loadout selection screen (right arrow for keyboard, and left thumbstick right for gamepad)
    public static bool LoadoutSectionRight()
    {
        bool keyboardRight = _currentKeyboardState.IsKeyDown(Keys.Right)
                            && _previousKeyboardState.IsKeyUp(Keys.Right);
        bool gamepadRight = thumbstickX > 0.3f
                            && _previousGamepadState.ThumbSticks.Left.X <= 0.3f;
        return keyboardRight || gamepadRight;
    }
    //function for navigating to the bottom section of the loadout selection screen (down arrow for keyboard, and left thumbstick down for gamepad)
    public static bool LoadoutSectionDown()
    {
        bool keyboardDown = _currentKeyboardState.IsKeyDown(Keys.Down)
                            && _previousKeyboardState.IsKeyUp(Keys.Down);
        bool gamepadDown = thumbstickY < -0.3f
                            && _previousGamepadState.ThumbSticks.Left.Y >= -0.3f;
        return keyboardDown || gamepadDown;
    }
    //function for skipping inventory and starting with current selections in loadout selection screen (left or right control for keyboard, and A while holding down left thumbstick for gamepad)
    public static bool LoadoutSkip()
    {
        bool keyboardSkip = (_currentKeyboardState.IsKeyDown(Keys.LeftControl) || _currentKeyboardState.IsKeyDown(Keys.RightControl)) 
                            && (_previousKeyboardState.IsKeyUp(Keys.LeftControl) && _previousKeyboardState.IsKeyUp(Keys.RightControl)) 
                            && _escapeKeyCooldown <= 0f;
        bool gamepadSkip = _currentGamepadState.Buttons.Back == ButtonState.Pressed 
                           && _previousGamepadState.Buttons.Back == ButtonState.Released 
                           && thumbstickY < -0.3f 
                           && _escapeKeyCooldown <= 0f;
        return keyboardSkip || gamepadSkip;
    }
    //function for developer payment bypass in payment scene (Ctrl + Shift + D for keyboard, and Start + A for gamepad)
    public static bool DevPaymentBypass()
    {
        bool keyboardBypass = _currentKeyboardState.IsKeyDown(Keys.LeftControl) 
                            && _currentKeyboardState.IsKeyDown(Keys.LeftShift) 
                            && _currentKeyboardState.IsKeyDown(Keys.D) 
                            && !_previousKeyboardState.IsKeyDown(Keys.LeftControl) 
                            && !_previousKeyboardState.IsKeyDown(Keys.LeftShift) 
                            && !_previousKeyboardState.IsKeyDown(Keys.D) 
                            && _escapeKeyCooldown <= 0f;
        bool gamepadBypass = _currentGamepadState.Buttons.Start == ButtonState.Pressed
                                       && _previousGamepadState.Buttons.Start == ButtonState.Released
                                       && _currentGamepadState.Buttons.A == ButtonState.Pressed
                                       && _previousGamepadState.Buttons.A == ButtonState.Released
                                        && _escapeKeyCooldown <= 0f;
                return keyboardBypass || gamepadBypass;
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
    /// Gets the current mouse position.
    /// </summary>
    public static Point GetMousePosition()
    {
        return _currentMouseState.Position;
    }
}
