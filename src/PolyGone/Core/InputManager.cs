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
    private static bool _windowIsActive = true;
    private const float MENU_AUTO_REPEAT_INTERVAL = 0.1f;
    private const float CLICK_COOLDOWN = 0.01f; // 10ms between clicks
    private const float ESCAPE_COOLDOWN = 0.2f; // 200ms between escape presses
    private const float DASH_COOLDOWN = 0.50f; // 500ms between dashes

    public static MouseState CurrentMouseState => _currentMouseState;
    public static MouseState PreviousMouseState => _previousMouseState;
    private static InputBindingProfile Bindings => InputBindings.Current;

    private static bool IsKeyPressed(Keys key)
    {
        return _currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
    }

    private static bool IsKeyHeld(Keys key)
    {
        return _currentKeyboardState.IsKeyDown(key);
    }

    private static bool IsButtonPressed(Buttons button)
    {
        return _currentGamepadState.IsButtonDown(button) && _previousGamepadState.IsButtonUp(button);
    }

    private static bool IsButtonHeld(Buttons button)
    {
        return _currentGamepadState.IsButtonDown(button);
    }

    private static bool IsMousePressed(MouseButtonBinding binding)
    {
        return binding switch
        {
            MouseButtonBinding.Left => _currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released,
            MouseButtonBinding.Right => _currentMouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released,
            MouseButtonBinding.Middle => _currentMouseState.MiddleButton == ButtonState.Pressed && _previousMouseState.MiddleButton == ButtonState.Released,
            MouseButtonBinding.XButton1 => _currentMouseState.XButton1 == ButtonState.Pressed && _previousMouseState.XButton1 == ButtonState.Released,
            MouseButtonBinding.XButton2 => _currentMouseState.XButton2 == ButtonState.Pressed && _previousMouseState.XButton2 == ButtonState.Released,
            MouseButtonBinding.None => false,
            _ => false,
        };
    }

    private static bool IsMouseHeld(MouseButtonBinding binding)
    {
        return binding switch
        {
            MouseButtonBinding.Left => _currentMouseState.LeftButton == ButtonState.Pressed,
            MouseButtonBinding.Right => _currentMouseState.RightButton == ButtonState.Pressed,
            MouseButtonBinding.Middle => _currentMouseState.MiddleButton == ButtonState.Pressed,
            MouseButtonBinding.XButton1 => _currentMouseState.XButton1 == ButtonState.Pressed,
            MouseButtonBinding.XButton2 => _currentMouseState.XButton2 == ButtonState.Pressed,
            MouseButtonBinding.None => false,
            _ => false,
        };
    }

    private static MouseButtonBinding ResolveMouseBinding(MouseButtonBinding? binding)
    {
        return binding ?? MouseButtonBinding.None;
    }

    private static bool IsAnyMouseButtonHeld()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed
               || _currentMouseState.RightButton == ButtonState.Pressed
               || _currentMouseState.MiddleButton == ButtonState.Pressed
               || _currentMouseState.XButton1 == ButtonState.Pressed
               || _currentMouseState.XButton2 == ButtonState.Pressed;
    }

    private static Vector2 GetMoveStickVector()
    {
        return Bindings.MoveStick == StickBinding.Left
            ? _currentGamepadState.ThumbSticks.Left
            : _currentGamepadState.ThumbSticks.Right;
    }

    private static Vector2 GetPreviousMoveStickVector()
    {
        return Bindings.MoveStick == StickBinding.Left
            ? _previousGamepadState.ThumbSticks.Left
            : _previousGamepadState.ThumbSticks.Right;
    }

    private static Vector2 GetAimStickVector()
    {
        return Bindings.AimStick == StickBinding.Left
            ? _currentGamepadState.ThumbSticks.Left
            : _currentGamepadState.ThumbSticks.Right;
    }

    /// <summary>
    /// Updates the input state. Should be called once per frame in Game1.Update()
    /// </summary>
    public static void Update(GameTime gameTime)
    {
        _previousMouseState = _currentMouseState;
        _previousKeyboardState = _currentKeyboardState;
        _previousGamepadState = _currentGamepadState;

        if (_windowIsActive)
        {
            _currentMouseState = Mouse.GetState();
            _currentKeyboardState = Keyboard.GetState();
            _currentGamepadState = GamePad.GetState(PlayerIndex.One);
        }
        else
        {
            // Keep current state equal to previous so no new input is detected while inactive
            _currentMouseState = _previousMouseState;
            _currentKeyboardState = _previousKeyboardState;
            _currentGamepadState = _previousGamepadState;
        }

        var moveStick = GetMoveStickVector();
        thumbstickX = moveStick.X;
        thumbstickY = moveStick.Y;

        var aimStick = GetAimStickVector();
        _rightThumbstick = new Vector2(aimStick.X, -aimStick.Y);

        // Detect input mode switching
        bool controllerInput = Math.Abs(_rightThumbstick.X) > 0.3f || Math.Abs(_rightThumbstick.Y) > 0.3f ||
                              Math.Abs(moveStick.X) > 0.1f ||
                              Math.Abs(moveStick.Y) > 0.1f ||
                              IsButtonHeld(Bindings.MenuConfirmButton) ||
                              IsButtonHeld(Bindings.ShootButton);

        bool mouseInput = _currentMouseState.Position != _previousMouseState.Position || IsAnyMouseButtonHeld();

        bool keyboardInput = _currentKeyboardState.GetPressedKeys().Length > 0;

        bool menuUpHeld = IsKeyHeld(Bindings.MenuUpKey) || IsButtonHeld(Bindings.MenuUpButton);
        bool menuDownHeld = IsKeyHeld(Bindings.MenuDownKey) || IsButtonHeld(Bindings.MenuDownButton);
        bool menuLeftHeld = IsKeyHeld(Bindings.MenuLeftKey) || IsButtonHeld(Bindings.MenuLeftButton);
        bool menuRightHeld = IsKeyHeld(Bindings.MenuRightKey) || IsButtonHeld(Bindings.MenuRightButton);

        if (controllerInput && !mouseInput && !keyboardInput)
        {
            _usingController = true;
        }
        else if (mouseInput || keyboardInput)
        {
            _usingController = false;
        }

        // Update click cooldown
        if (_mouseClickCooldown > 0f)
        {
            _mouseClickCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        // Update dash cooldown
        if (_dashCooldown > 0f)
        {
            _dashCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
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

        // Update escape key cooldown: always decrement over time and clamp to zero.
        if (_escapeKeyCooldown > 0f)
        {
            _escapeKeyCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_escapeKeyCooldown < 0f)
            {
                _escapeKeyCooldown = 0f;
            }
        }
        _menuUpAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _menuDownAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _menuLeftAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _menuRightAutoRepeatTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    public static void SetWindowActive(bool active)
    {
        _windowIsActive = active;
        if (!active)
        {
            // Reset cooldowns so returning focus doesn't immediately trigger actions
            _mouseClickCooldown = CLICK_COOLDOWN;
            _escapeKeyCooldown = ESCAPE_COOLDOWN;
        }
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
    /// Consumes the dash by starting the cooldown timer.
    /// Call this after handling a dash to prevent it from triggering multiple actions.
    /// </summary>
    public static void ConsumeDash()
    {
        _dashCooldown = DASH_COOLDOWN;
    }
    // Function for single shooting (left click for mouse and right trigger for gamepad)
    public static bool GameShootSingle()
    {
        bool mouseClicked = IsMousePressed(Bindings.ShootMouseButton)
                            && _mouseClickCooldown <= 0f;
        bool keyboardClicked = IsKeyPressed(Bindings.ShootKey)
                               && _mouseClickCooldown <= 0f;
        bool gamepadClicked = IsButtonPressed(Bindings.ShootButton)
                              && _mouseClickCooldown <= 0f;
        return mouseClicked || keyboardClicked || gamepadClicked;
    }
    // function for automatic shooting (holding left click for mouse and holding right trigger for gamepad)
    public static bool GameShootHold()
    {
        return IsMouseHeld(Bindings.ShootMouseButton)
               || IsKeyHeld(Bindings.ShootKey)
               || IsButtonHeld(Bindings.ShootButton);
    }
    // function for game jumping (space for keyboard and A button for gamepad)
    public static bool GameJump()
    {
        bool mouseJump = IsMousePressed(ResolveMouseBinding(Bindings.JumpMouseButton));
        bool keyboardJump = IsKeyPressed(Bindings.JumpKey);
        bool gamepadJump = IsButtonPressed(Bindings.JumpButton);
        return mouseJump || keyboardJump || gamepadJump;
    }

    // Function for checking if jump is currently held.
    public static bool GameJumpHeld()
    {
        bool mouseJump = IsMouseHeld(ResolveMouseBinding(Bindings.JumpMouseButton));
        bool keyboardJump = IsKeyHeld(Bindings.JumpKey);
        bool gamepadJump = IsButtonHeld(Bindings.JumpButton);
        return mouseJump || keyboardJump || gamepadJump;
    }

    // function for dashing (left shift for keyboard and B button for gamepad)
    public static bool GameDash()
    {
        bool mouseDash = IsMousePressed(ResolveMouseBinding(Bindings.DashMouseButton))
                         && _dashCooldown <= 0f;
        bool keyboardDash = IsKeyPressed(Bindings.DashKey)
                            && _dashCooldown <= 0f;
        bool gamepadDash = IsButtonPressed(Bindings.DashButton)
                           && _dashCooldown <= 0f;
        return mouseDash || keyboardDash || gamepadDash;
    }

    // function for moving left in game (A key for keyboard and left thumbstick left for gamepad)
    public static bool GameMoveLeft()
    {
        bool keyboardLeft = IsKeyHeld(Bindings.MoveLeftKey);
        bool gamepadLeft = thumbstickX < -0.3f;
        return keyboardLeft || gamepadLeft;
    }
    // function for moving right in game (D key for keyboard and left thumbstick right for gamepad)
    public static bool GameMoveRight()
    {
        bool keyboardRight = IsKeyHeld(Bindings.MoveRightKey);
        bool gamepadRight = thumbstickX > 0.3f;
        return keyboardRight || gamepadRight;
    }
    // Function for interacting with world objects like doors (W by default for keyboard, X by default for gamepad — both remappable).
    public static bool GameInteract()
    {
        bool mouseInteract = IsMousePressed(ResolveMouseBinding(Bindings.InteractMouseButton));
        bool keyboardInteract = IsKeyPressed(Bindings.InteractKey);
        bool gamepadInteract = IsButtonPressed(Bindings.InteractButton);
        return mouseInteract || keyboardInteract || gamepadInteract;
    }

    // Function for checking if the interact input is held (used to maintain door activation state).
    public static bool GameInteractHeld()
    {
        bool mouseInteract = IsMouseHeld(ResolveMouseBinding(Bindings.InteractMouseButton));
        bool keyboardInteract = IsKeyHeld(Bindings.InteractKey);
        bool gamepadInteract = IsButtonHeld(Bindings.InteractButton);
        return mouseInteract || keyboardInteract || gamepadInteract;
    }

    // function for dropping through platforms in game (S key for keyboard and left thumbstick down for gamepad)
    public static bool GameDrop()
    {
        bool mouseDrop = IsMouseHeld(ResolveMouseBinding(Bindings.DropMouseButton));
        bool keyboardDrop = IsKeyHeld(Bindings.DropKey);
        bool gamepadDrop = IsButtonHeld(Bindings.DropButton) || thumbstickY < -0.9f;
        return mouseDrop || keyboardDrop || gamepadDrop;
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
        bool mousePause = IsMousePressed(ResolveMouseBinding(Bindings.PauseMouseButton))
                          && _escapeKeyCooldown <= 0f;
        bool keyboardPause = IsKeyPressed(Bindings.PauseKey)
                             && _escapeKeyCooldown <= 0f;
        bool gamepadPause = IsButtonPressed(Bindings.PauseButton)
                            && _escapeKeyCooldown <= 0f;
        return mousePause || keyboardPause || gamepadPause;
    }
    //function for closing the pause menu while in game (Escape key for keyboard and Start button for gamepad)
    public static bool PauseMenuClose()
    {
        bool mousePause = IsMousePressed(ResolveMouseBinding(Bindings.PauseMouseButton))
                          && _escapeKeyCooldown <= 0f;
        bool keyboardPause = IsKeyPressed(Bindings.PauseKey)
                             && _escapeKeyCooldown <= 0f;
        bool gamepadPause = IsButtonPressed(Bindings.PauseButton)
                            && _escapeKeyCooldown <= 0f;
        return mousePause || keyboardPause || gamepadPause;
    }
    //function for navigating up in menus (W/Up key for keyboard and left thumbstick up for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuUp()
    {
        bool keyboardUp = IsKeyPressed(Bindings.MenuUpKey);
        bool gamepadUp = IsButtonPressed(Bindings.MenuUpButton);
        var moveStick = GetMoveStickVector();
        var previousMoveStick = GetPreviousMoveStickVector();
        bool gamepadStickUp = moveStick.Y > 0.3f && previousMoveStick.Y <= 0.3f;
        bool menuUpCurrentlyHeld = IsKeyHeld(Bindings.MenuUpKey)
                                   || IsButtonHeld(Bindings.MenuUpButton)
                                   || moveStick.Y > 0.3f;
        bool autoRepeat = menuUpCurrentlyHeld &&
                  _menuUpHoldTimer >= 1.0f &&
                  _menuUpAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuUpAutoRepeatTimer = 0f;
        }
        return keyboardUp || gamepadUp || gamepadStickUp || autoRepeat;

    }
    //function for navigating down in menus (S key for keyboard and left thumbstick down for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuDown()
    {
        bool keyboardDown = IsKeyPressed(Bindings.MenuDownKey);
        bool gamepadDown = IsButtonPressed(Bindings.MenuDownButton);
        var moveStick = GetMoveStickVector();
        var previousMoveStick = GetPreviousMoveStickVector();
        bool gamepadStickDown = moveStick.Y < -0.3f && previousMoveStick.Y >= -0.3f;
        bool menuDownCurrentlyHeld = IsKeyHeld(Bindings.MenuDownKey) || IsButtonHeld(Bindings.MenuDownButton) || moveStick.Y < -0.3f;
        bool autoRepeat = menuDownCurrentlyHeld &&
                  _menuDownHoldTimer >= 1.0f &&
                  _menuDownAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuDownAutoRepeatTimer = 0f;
        }
        return keyboardDown || gamepadDown || gamepadStickDown || autoRepeat;
    }
    //function for navigating left in menus (A key for keyboard and left thumbstick left for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuLeft()
    {
        bool keyboardLeft = IsKeyPressed(Bindings.MenuLeftKey);
        bool gamepadLeft = IsButtonPressed(Bindings.MenuLeftButton);
        var moveStick = GetMoveStickVector();
        var previousMoveStick = GetPreviousMoveStickVector();
        bool gamepadStickLeft = moveStick.X < -0.3f && previousMoveStick.X >= -0.3f;
        bool menuLeftCurrentlyHeld = IsKeyHeld(Bindings.MenuLeftKey) || IsButtonHeld(Bindings.MenuLeftButton) || moveStick.X < -0.3f;
        bool autoRepeat = menuLeftCurrentlyHeld &&
                  _menuLeftHoldTimer >= 0.5f &&
                  _menuLeftAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuLeftAutoRepeatTimer = 0f;
        }
        return keyboardLeft || gamepadLeft || gamepadStickLeft || autoRepeat;
    }
    //function for navigating right in menus (D key for keyboard and left thumbstick right for gamepad, and hovering over a button with mouse is already handled by GetMousePosition)
    public static bool MenuRight()
    {
        bool keyboardRight = IsKeyPressed(Bindings.MenuRightKey);
        bool gamepadRight = IsButtonPressed(Bindings.MenuRightButton);
        var moveStick = GetMoveStickVector();
        var previousMoveStick = GetPreviousMoveStickVector();
        bool gamepadStickRight = moveStick.X > 0.3f && previousMoveStick.X <= 0.3f;
        bool menuRightCurrentlyHeld = IsKeyHeld(Bindings.MenuRightKey) || IsButtonHeld(Bindings.MenuRightButton) || moveStick.X > 0.3f;
        bool autoRepeat = menuRightCurrentlyHeld &&
                  _menuRightHoldTimer >= 0.5f &&
                  _menuRightAutoRepeatTimer >= MENU_AUTO_REPEAT_INTERVAL;

        if (autoRepeat)
        {
            _menuRightAutoRepeatTimer = 0f;
        }
        return keyboardRight || gamepadRight || gamepadStickRight || autoRepeat;
    }

    //function for selecting a menu option with the mouse only (left click)
    public static bool MenuMouseConfirm()
    {
        return IsMousePressed(ResolveMouseBinding(Bindings.MenuConfirmMouseButton))
               && _mouseClickCooldown <= 0f;
    }
    //function for selecting a menu option without the mouse (Enter key for keyboard and A button for gamepad)
    public static bool MenuNonPointerConfirm()
    {
        bool keyboardConfirm = IsKeyPressed(Bindings.MenuConfirmKey)
                               && _escapeKeyCooldown <= 0f;
        bool gamepadConfirm = IsButtonPressed(Bindings.MenuConfirmButton)
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
        bool mouseHold = IsMouseHeld(ResolveMouseBinding(Bindings.MenuConfirmMouseButton))
                         && _mouseClickCooldown <= 0f;
        bool keyboardHold = IsKeyHeld(Bindings.MenuConfirmKey)
                            && _escapeKeyCooldown <= 0f;
        bool gamepadHold = IsButtonHeld(Bindings.MenuConfirmButton)
                           && _mouseClickCooldown <= 0f;
        return mouseHold || keyboardHold || gamepadHold;
    }

    public static bool MenuConfirmMouseClick()
    {
        bool mouseConfirm = IsMousePressed(ResolveMouseBinding(Bindings.MenuConfirmMouseButton))
                            && _mouseClickCooldown <= 0f;
        return mouseConfirm;
    }
    //function for going back in menus (Escape key always works; MenuBackKey is an optional additional binding; B button for gamepad)
    public static bool MenuBack()
    {
        // Escape always triggers MenuBack regardless of the configured binding so the
        // control remap screen (and every other menu) can always be exited with Escape.
        bool keyboardBack = (IsKeyPressed(Keys.Escape) ||
                             (Bindings.MenuBackKey != Keys.None && IsKeyPressed(Bindings.MenuBackKey)))
                            && _escapeKeyCooldown <= 0f;
        bool gamepadBack = IsButtonPressed(Bindings.MenuBackButton)
                           && _escapeKeyCooldown <= 0f;
        bool mouseBack = IsMousePressed(ResolveMouseBinding(Bindings.MenuBackMouseButton))
                         && _escapeKeyCooldown <= 0f;
        return keyboardBack || gamepadBack || mouseBack;
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
        bool mouseSkip = IsMousePressed(ResolveMouseBinding(Bindings.LoadoutSkipMouseButton))
                         && _escapeKeyCooldown <= 0f;
        bool keyboardSkip = IsKeyPressed(Bindings.LoadoutSkipKey) && _escapeKeyCooldown <= 0f;
        bool gamepadSkip = IsButtonPressed(Bindings.LoadoutSkipButton)
                            && _escapeKeyCooldown <= 0f;
        return mouseSkip || keyboardSkip || gamepadSkip;
    }
    //function for developer payment bypass in payment scene (Ctrl + Shift + D for keyboard, and Start + A for gamepad)
    public static bool DevPaymentBypass()
    {
        bool ctrlHeld = _currentKeyboardState.IsKeyDown(Keys.LeftControl)
                        || _currentKeyboardState.IsKeyDown(Keys.RightControl);
        bool shiftHeld = _currentKeyboardState.IsKeyDown(Keys.LeftShift)
                         || _currentKeyboardState.IsKeyDown(Keys.RightShift);
        bool keyboardBypass = ctrlHeld
                              && shiftHeld
                              && IsKeyPressed(Keys.D)
                              && _escapeKeyCooldown <= 0f;

        bool gamepadBypass = (
                                (IsButtonPressed(Buttons.Start) && IsButtonHeld(Buttons.A))
                                || (IsButtonPressed(Buttons.A) && IsButtonHeld(Buttons.Start))
                             )
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
    /// Forces a reset of the dash cooldown. Useful when transitioning between scenes
    /// to ensure old dashes don't carry over.
    /// </summary>
    public static void ResetDash()
    {
        _dashCooldown = DASH_COOLDOWN;
    }


    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    public static Point GetMousePosition()
    {
        return _currentMouseState.Position;
    }
}
