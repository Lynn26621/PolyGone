using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PolyGone;

/// <summary>
/// Persists the player's display preferences (fullscreen, resolution) across sessions.
/// </summary>
public static class DisplaySettings
{
    /// <summary>All selectable windowed resolutions, in ascending order.</summary>
    public static readonly (int Width, int Height)[] Resolutions =
    [
        (1280,  720),
        (1600,  900),
        (1920, 1080),
        (2560, 1440),
    ];

    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PolyGone",
        "settings.json");

    public static bool IsFullScreen    { get; set; } = false;
    public static int  ResolutionIndex { get; set; } = 0;

    // Derived from the current ResolutionIndex
    public static int WindowedWidth  => Resolutions[ResolutionIndex].Width;
    public static int WindowedHeight => Resolutions[ResolutionIndex].Height;

    /// <summary>Reference to the game window, set from Game1.Initialize() for centering.</summary>
    public static GameWindow? Window { get; set; }

    /// <summary>
    /// Returns only the resolutions from <see cref="Resolutions"/> that fit within the primary display.
    /// </summary>
    public static (int Width, int Height)[] GetAvailableResolutions()
    {
        var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        return Array.FindAll(Resolutions, r => r.Width <= dm.Width && r.Height <= dm.Height);
    }

    /// <summary>
    /// Centers the game window on the primary display so it never spills onto a second monitor.
    /// </summary>
    public static void CenterWindowOnPrimaryDisplay(int windowWidth, int windowHeight)
    {
        if (Window == null) { return; }
        var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var x = Math.Max(0, (dm.Width  - windowWidth)  / 2);
        var y = Math.Max(0, (dm.Height - windowHeight) / 2);
        Window.Position = new Point(x, y);
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SavePath)) { return; }
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SavePath));
            if (data == null) { return; }
            IsFullScreen    = data.IsFullScreen;
            ResolutionIndex = Math.Clamp(data.ResolutionIndex, 0, Resolutions.Length - 1);
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(new SettingsData
            {
                IsFullScreen    = IsFullScreen,
                ResolutionIndex = ResolutionIndex,
            }));
        }
        catch { }
    }

    private sealed class SettingsData
    {
        public bool IsFullScreen    { get; set; }
        public int  ResolutionIndex { get; set; }
    }
}
