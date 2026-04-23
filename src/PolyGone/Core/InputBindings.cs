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
    public Keys DropKey { get; set; }
    public Keys LoadoutSkipKey { get; set; }

    public Buttons MenuUpButton { get; set; }
    public Buttons MenuDownButton { get; set; }
    public Buttons MenuLeftButton { get; set; }
    public Buttons MenuRightButton { get; set; }
    public Buttons MenuConfirmButton { get; set; }
    public Buttons MenuBackButton { get; set; }
    public Buttons PauseButton { get; set; }
    public Buttons JumpButton { get; set; }
    public Buttons DropButton { get; set; }
    public Buttons ShootButton { get; set; }
    public Buttons LoadoutSkipButton { get; set; }

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
            DropKey = DropKey,
            LoadoutSkipKey = LoadoutSkipKey,
            MenuUpButton = MenuUpButton,
            MenuDownButton = MenuDownButton,
            MenuLeftButton = MenuLeftButton,
            MenuRightButton = MenuRightButton,
            MenuConfirmButton = MenuConfirmButton,
            MenuBackButton = MenuBackButton,
            PauseButton = PauseButton,
            JumpButton = JumpButton,
            DropButton = DropButton,
            ShootButton = ShootButton,
            LoadoutSkipButton = LoadoutSkipButton,
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
        MenuBackKey = Keys.Escape,
        PauseKey = Keys.Escape,
        MoveLeftKey = Keys.A,
        MoveRightKey = Keys.D,
        JumpKey = Keys.Space,
        DropKey = Keys.S,
        LoadoutSkipKey = Keys.LeftControl,
        MenuUpButton = Buttons.DPadUp,
        MenuDownButton = Buttons.DPadDown,
        MenuLeftButton = Buttons.DPadLeft,
        MenuRightButton = Buttons.DPadRight,
        MenuConfirmButton = Buttons.A,
        MenuBackButton = Buttons.B,
        PauseButton = Buttons.Start,
        JumpButton = Buttons.A,
        DropButton = Buttons.DPadDown,
        ShootButton = Buttons.RightTrigger,
        LoadoutSkipButton = Buttons.Back,
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

    private static InputBindingProfile Sanitize(InputBindingProfile profile)
    {
        var defaults = Defaults;
        var sanitized = defaults.Clone();

        sanitized.MenuUpKey = Enum.IsDefined(typeof(Keys), profile.MenuUpKey) ? profile.MenuUpKey : defaults.MenuUpKey;
        sanitized.MenuDownKey = Enum.IsDefined(typeof(Keys), profile.MenuDownKey) ? profile.MenuDownKey : defaults.MenuDownKey;
        sanitized.MenuLeftKey = Enum.IsDefined(typeof(Keys), profile.MenuLeftKey) ? profile.MenuLeftKey : defaults.MenuLeftKey;
        sanitized.MenuRightKey = Enum.IsDefined(typeof(Keys), profile.MenuRightKey) ? profile.MenuRightKey : defaults.MenuRightKey;
        sanitized.MenuConfirmKey = Enum.IsDefined(typeof(Keys), profile.MenuConfirmKey) ? profile.MenuConfirmKey : defaults.MenuConfirmKey;
        sanitized.MenuBackKey = Enum.IsDefined(typeof(Keys), profile.MenuBackKey) ? profile.MenuBackKey : defaults.MenuBackKey;
        sanitized.PauseKey = Enum.IsDefined(typeof(Keys), profile.PauseKey) ? profile.PauseKey : defaults.PauseKey;
        sanitized.MoveLeftKey = Enum.IsDefined(typeof(Keys), profile.MoveLeftKey) ? profile.MoveLeftKey : defaults.MoveLeftKey;
        sanitized.MoveRightKey = Enum.IsDefined(typeof(Keys), profile.MoveRightKey) ? profile.MoveRightKey : defaults.MoveRightKey;
        sanitized.JumpKey = Enum.IsDefined(typeof(Keys), profile.JumpKey) ? profile.JumpKey : defaults.JumpKey;
        sanitized.DropKey = Enum.IsDefined(typeof(Keys), profile.DropKey) ? profile.DropKey : defaults.DropKey;
        sanitized.LoadoutSkipKey = Enum.IsDefined(typeof(Keys), profile.LoadoutSkipKey) ? profile.LoadoutSkipKey : defaults.LoadoutSkipKey;

        sanitized.MenuUpButton = Enum.IsDefined(typeof(Buttons), profile.MenuUpButton) ? profile.MenuUpButton : defaults.MenuUpButton;
        sanitized.MenuDownButton = Enum.IsDefined(typeof(Buttons), profile.MenuDownButton) ? profile.MenuDownButton : defaults.MenuDownButton;
        sanitized.MenuLeftButton = Enum.IsDefined(typeof(Buttons), profile.MenuLeftButton) ? profile.MenuLeftButton : defaults.MenuLeftButton;
        sanitized.MenuRightButton = Enum.IsDefined(typeof(Buttons), profile.MenuRightButton) ? profile.MenuRightButton : defaults.MenuRightButton;
        sanitized.MenuConfirmButton = Enum.IsDefined(typeof(Buttons), profile.MenuConfirmButton) ? profile.MenuConfirmButton : defaults.MenuConfirmButton;
        sanitized.MenuBackButton = Enum.IsDefined(typeof(Buttons), profile.MenuBackButton) ? profile.MenuBackButton : defaults.MenuBackButton;
        sanitized.PauseButton = Enum.IsDefined(typeof(Buttons), profile.PauseButton) ? profile.PauseButton : defaults.PauseButton;
        sanitized.JumpButton = Enum.IsDefined(typeof(Buttons), profile.JumpButton) ? profile.JumpButton : defaults.JumpButton;
        sanitized.DropButton = Enum.IsDefined(typeof(Buttons), profile.DropButton) ? profile.DropButton : defaults.DropButton;
        sanitized.ShootButton = Enum.IsDefined(typeof(Buttons), profile.ShootButton) ? profile.ShootButton : defaults.ShootButton;
        sanitized.LoadoutSkipButton = Enum.IsDefined(typeof(Buttons), profile.LoadoutSkipButton) ? profile.LoadoutSkipButton : defaults.LoadoutSkipButton;

        sanitized.MoveStick = Enum.IsDefined(typeof(StickBinding), profile.MoveStick) ? profile.MoveStick : defaults.MoveStick;
        sanitized.AimStick = Enum.IsDefined(typeof(StickBinding), profile.AimStick) ? profile.AimStick : defaults.AimStick;

        return sanitized;
    }
}
