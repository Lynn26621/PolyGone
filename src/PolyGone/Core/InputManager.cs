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
        _currentMouseState = Mouse.GetState();

        _previousKeyboardState = _currentKeyboardState;
        _currentKeyboardState = Keyboard.GetState();

        _previousGamepadState = _currentGamepadState;
        _currentGamepadState = GamePad.GetState(PlayerIndex.One);

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

        bool mouseInput = _currentMouseState.Position != _previousMouseState.Position ||
                         _currentMouseState.LeftButton == ButtonState.Pressed;

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
        bool gamepadClicked = IsButtonPressed(Bindings.ShootButton)
                              && _mouseClickCooldown <= 0f;
        return mouseClicked || gamepadClicked;
    }
    // function for automatic shooting (holding left click for mouse and holding right trigger for gamepad)
    public static bool GameShootHold()
    {
        return _currentMouseState.LeftButton == ButtonState.Pressed
               || IsButtonHeld(Bindings.ShootButton);
    }
    // function for game jumping (space for keyboard and A button for gamepad)
    public static bool GameJump()
    {
        bool keyboardJump = IsKeyPressed(Bindings.JumpKey);
        bool gamepadJump = IsButtonPressed(Bindings.JumpButton);
        return keyboardJump || gamepadJump;
    }

    // Function for checking if jump is currently held.
    public static bool GameJumpHeld()
    {
        bool keyboardJump = IsKeyHeld(Bindings.JumpKey);
        bool gamepadJump = IsButtonHeld(Bindings.JumpButton);
        return keyboardJump || gamepadJump;
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
        bool keyboardInteract = IsKeyPressed(Bindings.InteractKey);
        bool gamepadInteract = IsButtonPressed(Bindings.InteractButton);
        return keyboardInteract || gamepadInteract;
    }

    // Function for checking if the interact input is held (used to maintain door activation state).
    public static bool GameInteractHeld()
    {
        bool keyboardInteract = IsKeyHeld(Bindings.InteractKey);
        bool gamepadInteract = IsButtonHeld(Bindings.InteractButton);
        return keyboardInteract || gamepadInteract;
    }

    // function for dropping through platforms in game (S key for keyboard and left thumbstick down for gamepad)
    public static bool GameDrop()
    {
        bool keyboardDrop = IsKeyHeld(Bindings.DropKey);
        bool gamepadDrop = IsButtonHeld(Bindings.DropButton) || thumbstickY < -0.9f;
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
        bool keyboardPause = IsKeyPressed(Bindings.PauseKey)
                             && _escapeKeyCooldown <= 0f;
        bool gamepadPause = IsButtonPressed(Bindings.PauseButton)
                            && _escapeKeyCooldown <= 0f;
        return keyboardPause || gamepadPause;
    }
    //function for closing the pause menu while in game (Escape key for keyboard and Start button for gamepad)
    public static bool PauseMenuClose()
    {
        bool keyboardPause = IsKeyPressed(Bindings.PauseKey)
                             && _escapeKeyCooldown <= 0f;
        bool gamepadPause = IsButtonPressed(Bindings.PauseButton)
                            && _escapeKeyCooldown <= 0f;
        return keyboardPause || gamepadPause;
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
        return _currentMouseState.LeftButton == ButtonState.Pressed
               && _previousMouseState.LeftButton == ButtonState.Released
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
        bool mouseHold = _currentMouseState.LeftButton == ButtonState.Pressed
                         && _mouseClickCooldown <= 0f;
        bool keyboardHold = IsKeyHeld(Bindings.MenuConfirmKey)
                            && _escapeKeyCooldown <= 0f;
        bool gamepadHold = IsButtonHeld(Bindings.MenuConfirmButton)
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
        bool keyboardSkip = IsKeyPressed(Bindings.LoadoutSkipKey) && _escapeKeyCooldown <= 0f;
        bool gamepadSkip = IsButtonPressed(Bindings.LoadoutSkipButton)
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
