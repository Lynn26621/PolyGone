using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace PolyGone;

public enum StickBinding
{
    Left,
    Right,
}

public enum MouseButtonBinding
{
    Left = 0,
    Right = 1,
    Middle = 2,
    XButton1 = 3,
    XButton2 = 4,
    None = 5,
}

public sealed class InputBindingProfile
{
    public Keys MenuUpKey { get; set; }
    public Keys MenuDownKey { get; set; }
    public Keys MenuLeftKey { get; set; }
    public Keys MenuRightKey { get; set; }
    public Keys MenuConfirmKey { get; set; }
    public Keys MenuBackKey { get; set; }
    public Keys PauseKey { get; set; }
    public Keys MoveLeftKey { get; set; }
    public Keys MoveRightKey { get; set; }
    public Keys JumpKey { get; set; }
    public Keys DashKey { get; set; }
    public Keys DropKey { get; set; }
    public Keys ShootKey { get; set; }
    public Keys LoadoutSkipKey { get; set; }
    public Keys InteractKey { get; set; }

    public Buttons MenuUpButton { get; set; }
    public Buttons MenuDownButton { get; set; }
    public Buttons MenuLeftButton { get; set; }
    public Buttons MenuRightButton { get; set; }
    public Buttons MenuConfirmButton { get; set; }
    public Buttons MenuBackButton { get; set; }
    public Buttons PauseButton { get; set; }
    public Buttons JumpButton { get; set; }
    public Buttons DashButton { get; set; }
    public Buttons DropButton { get; set; }
    public Buttons ShootButton { get; set; }
    public Buttons LoadoutSkipButton { get; set; }
    public Buttons InteractButton { get; set; }
    public MouseButtonBinding ShootMouseButton { get; set; }
    public MouseButtonBinding? DashMouseButton { get; set; }

    public StickBinding MoveStick { get; set; }
    public StickBinding AimStick { get; set; }

    public InputBindingProfile Clone()
    {
        return new InputBindingProfile
        {
            MenuUpKey = MenuUpKey,
            MenuDownKey = MenuDownKey,
            MenuLeftKey = MenuLeftKey,
            MenuRightKey = MenuRightKey,
            MenuConfirmKey = MenuConfirmKey,
            MenuBackKey = MenuBackKey,
            PauseKey = PauseKey,
            MoveLeftKey = MoveLeftKey,
            MoveRightKey = MoveRightKey,
            JumpKey = JumpKey,
            DashKey = DashKey,
            DropKey = DropKey,
            ShootKey = ShootKey,
            LoadoutSkipKey = LoadoutSkipKey,
            InteractKey = InteractKey,
            MenuUpButton = MenuUpButton,
            MenuDownButton = MenuDownButton,
            MenuLeftButton = MenuLeftButton,
            MenuRightButton = MenuRightButton,
            MenuConfirmButton = MenuConfirmButton,
            MenuBackButton = MenuBackButton,
            PauseButton = PauseButton,
            JumpButton = JumpButton,
            DashButton = DashButton,
            DropButton = DropButton,
            ShootButton = ShootButton,
            LoadoutSkipButton = LoadoutSkipButton,
            InteractButton = InteractButton,
            ShootMouseButton = ShootMouseButton,
            DashMouseButton = DashMouseButton,
            MoveStick = MoveStick,
            AimStick = AimStick,
        };
    }
}

public static class InputBindings
{
    // Keep defaults in one place for quick tuning.
    public static readonly InputBindingProfile Defaults = new()
    {
        MenuUpKey = Keys.W,
        MenuDownKey = Keys.S,
        MenuLeftKey = Keys.A,
        MenuRightKey = Keys.D,
        MenuConfirmKey = Keys.Enter,
        MenuBackKey = Keys.None,  // Escape is always active for MenuBack (hardcoded); set this for an additional key.
        PauseKey = Keys.Escape,
        MoveLeftKey = Keys.A,
        MoveRightKey = Keys.D,
        JumpKey = Keys.Space,
        DashKey = Keys.LeftShift,
        DropKey = Keys.S,
        ShootKey = Keys.F,
        LoadoutSkipKey = Keys.LeftControl,
        InteractKey = Keys.W,
        MenuUpButton = Buttons.DPadUp,
        MenuDownButton = Buttons.DPadDown,
        MenuLeftButton = Buttons.DPadLeft,
        MenuRightButton = Buttons.DPadRight,
        MenuConfirmButton = Buttons.A,
        MenuBackButton = Buttons.B,
        PauseButton = Buttons.Start,
        JumpButton = Buttons.A,
        DashButton = Buttons.B,
        DropButton = Buttons.DPadDown,
        ShootButton = Buttons.RightTrigger,
        LoadoutSkipButton = Buttons.Back,
        InteractButton = Buttons.X,
        ShootMouseButton = MouseButtonBinding.Left,
        DashMouseButton = MouseButtonBinding.None,
        MoveStick = StickBinding.Left,
        AimStick = StickBinding.Right,
    };

    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PolyGone",
        "controls.json");

    private static InputBindingProfile _current = Defaults.Clone();

    public static InputBindingProfile Current => _current;

    public static InputBindingProfile CreateDefaults()
    {
        return Defaults.Clone();
    }

    public static InputBindingProfile CloneCurrent()
    {
        return _current.Clone();
    }

    public static void Apply(InputBindingProfile profile, bool save)
    {
        _current = profile.Clone();
        if (save)
        {
            Save();
        }
    }

    public static void ResetToDefaults(bool save)
    {
        _current = Defaults.Clone();
        if (save)
        {
            Save();
        }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                _current = Defaults.Clone();
                return;
            }

            var data = JsonSerializer.Deserialize<InputBindingProfile>(File.ReadAllText(SavePath));
            _current = data != null ? Sanitize(data) : Defaults.Clone();
        }
        catch
        {
            _current = Defaults.Clone();
        }
    }

    public static void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SavePath);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(_current));
        }
        catch
        {
        }
    }

    private static bool IsValidKey(Keys key)
    {
        // Keys.None (= 0) is not an actionable binding; reject it so the default is used instead.
        return key != Keys.None && Enum.IsDefined(typeof(Keys), key);
    }

    private static bool IsValidButton(Buttons button)
    {
        // Value 0 means no button was set; reject it so the default is used instead.
        return (int)button != 0 && Enum.IsDefined(typeof(Buttons), button);
    }

    private static InputBindingProfile Sanitize(InputBindingProfile profile)
    {
        var defaults = Defaults;
        var sanitized = defaults.Clone();

        sanitized.MenuUpKey = IsValidKey(profile.MenuUpKey) ? profile.MenuUpKey : defaults.MenuUpKey;
        sanitized.MenuDownKey = IsValidKey(profile.MenuDownKey) ? profile.MenuDownKey : defaults.MenuDownKey;
        sanitized.MenuLeftKey = IsValidKey(profile.MenuLeftKey) ? profile.MenuLeftKey : defaults.MenuLeftKey;
        sanitized.MenuRightKey = IsValidKey(profile.MenuRightKey) ? profile.MenuRightKey : defaults.MenuRightKey;
        sanitized.MenuConfirmKey = IsValidKey(profile.MenuConfirmKey) ? profile.MenuConfirmKey : defaults.MenuConfirmKey;
        // MenuBackKey is optional (Keys.None = no extra binding; Esc is always on).
        sanitized.MenuBackKey = Enum.IsDefined(typeof(Keys), profile.MenuBackKey) ? profile.MenuBackKey : defaults.MenuBackKey;
        sanitized.PauseKey = IsValidKey(profile.PauseKey) ? profile.PauseKey : defaults.PauseKey;
        sanitized.MoveLeftKey = IsValidKey(profile.MoveLeftKey) ? profile.MoveLeftKey : defaults.MoveLeftKey;
        sanitized.MoveRightKey = IsValidKey(profile.MoveRightKey) ? profile.MoveRightKey : defaults.MoveRightKey;
        sanitized.JumpKey = IsValidKey(profile.JumpKey) ? profile.JumpKey : defaults.JumpKey;
        sanitized.DashKey = IsValidKey(profile.DashKey) ? profile.DashKey: defaults.DashKey;
        sanitized.DropKey = IsValidKey(profile.DropKey) ? profile.DropKey : defaults.DropKey;
        sanitized.ShootKey = IsValidKey(profile.ShootKey) ? profile.ShootKey : defaults.ShootKey;
        sanitized.LoadoutSkipKey = IsValidKey(profile.LoadoutSkipKey) ? profile.LoadoutSkipKey : defaults.LoadoutSkipKey;
        sanitized.InteractKey = IsValidKey(profile.InteractKey) ? profile.InteractKey : defaults.InteractKey;

        sanitized.MenuUpButton = IsValidButton(profile.MenuUpButton) ? profile.MenuUpButton : defaults.MenuUpButton;
        sanitized.MenuDownButton = IsValidButton(profile.MenuDownButton) ? profile.MenuDownButton : defaults.MenuDownButton;
        sanitized.MenuLeftButton = IsValidButton(profile.MenuLeftButton) ? profile.MenuLeftButton : defaults.MenuLeftButton;
        sanitized.MenuRightButton = IsValidButton(profile.MenuRightButton) ? profile.MenuRightButton : defaults.MenuRightButton;
        sanitized.MenuConfirmButton = IsValidButton(profile.MenuConfirmButton) ? profile.MenuConfirmButton : defaults.MenuConfirmButton;
        sanitized.MenuBackButton = IsValidButton(profile.MenuBackButton) ? profile.MenuBackButton : defaults.MenuBackButton;
        sanitized.PauseButton = IsValidButton(profile.PauseButton) ? profile.PauseButton : defaults.PauseButton;
        sanitized.JumpButton = IsValidButton(profile.JumpButton) ? profile.JumpButton : defaults.JumpButton;
        sanitized.DashButton = IsValidButton(profile.DashButton) ? profile.DashButton : defaults.DashButton;
        sanitized.DropButton = IsValidButton(profile.DropButton) ? profile.DropButton : defaults.DropButton;
        sanitized.ShootButton = IsValidButton(profile.ShootButton) ? profile.ShootButton : defaults.ShootButton;
        sanitized.LoadoutSkipButton = IsValidButton(profile.LoadoutSkipButton) ? profile.LoadoutSkipButton : defaults.LoadoutSkipButton;
        sanitized.InteractButton = IsValidButton(profile.InteractButton) ? profile.InteractButton : defaults.InteractButton;
        sanitized.ShootMouseButton = Enum.IsDefined(typeof(MouseButtonBinding), profile.ShootMouseButton)
            ? profile.ShootMouseButton
            : defaults.ShootMouseButton;
        sanitized.DashMouseButton = profile.DashMouseButton.HasValue && Enum.IsDefined(typeof(MouseButtonBinding), profile.DashMouseButton.Value)
            ? profile.DashMouseButton.Value
            : defaults.DashMouseButton;

        sanitized.MoveStick = Enum.IsDefined(typeof(StickBinding), profile.MoveStick) ? profile.MoveStick : defaults.MoveStick;
        sanitized.AimStick = Enum.IsDefined(typeof(StickBinding), profile.AimStick) ? profile.AimStick : defaults.AimStick;

        return sanitized;
    }
}
