using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PolyGone;

/// <summary>
/// Tracks which levels the player has completed and derives item unlock status from that.
/// Data is persisted to a local JSON file in the user's application-data folder.
/// </summary>
public static class UnlockTracker
{
    public const int PlannedLevelCount = 20;

    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PolyGone",
        "unlocks.json");

    private static HashSet<string> _completedLevels = new();

    private static HashSet<string> _unlockedAbilities = new();

    /// <summary>
    /// Maps each locked player item to the level name that must be completed to unlock it.
    /// Items not listed here are always unlocked.
    /// </summary>
    private static readonly Dictionary<ItemType, string> _itemUnlockRequirements = new()
    {
        { ItemType.HealingGlow, "Level15" },
        { ItemType.LowGravity,  "Level20" },
        { ItemType.IronWill,    "Level3" },
    };

    /// <summary>
    /// Maps each locked blaster attachment to the level name required to unlock it.
    /// Attachments not listed here are always unlocked.
    /// </summary>
    private static readonly Dictionary<BlasterAttachmentType, string> _attachmentUnlockRequirements = new()
    {
        { BlasterAttachmentType.MultiShot,   "Level12" },
        { BlasterAttachmentType.RapidFire,   "Level2" },
        { BlasterAttachmentType.Piercing,    "Level5" },
        { BlasterAttachmentType.DamageBoost, "Level15" },
    };

    /// <summary>
    /// The ordered list of level file names. Index 0 is always unlocked;
    /// every subsequent level requires the one before it to be completed.
    /// </summary>
    private static readonly string[] _levelOrder = Enumerable
        .Range(1, PlannedLevelCount)
        .Select(i => $"Level{i}")
        .ToArray();

    /// <summary>
    /// List of all unlockable abilites.
    /// </summary>
    private static readonly string[] _abilityNames = { "Dash", "WallJump" };

    /// <summary>Returns true if the level is available to play.</summary>
    public static bool IsLevelUnlocked(string levelFile)
    {
        int idx = Array.IndexOf(_levelOrder, levelFile);
        if (idx <= 0)
            return true; // first level (or unknown) always unlocked
        return _completedLevels.Contains(_levelOrder[idx - 1]);
    }

    public static IReadOnlyList<string> GetPlannedLevels() => _levelOrder;

    /// <summary>Returns true if the level has been completed.</summary>
    public static bool IsLevelCompleted(string levelFile) => _completedLevels.Contains(levelFile);

    /// <summary>Returns a human-readable hint for a locked level, or null if unlocked.</summary>
    public static string? GetLevelUnlockHint(string levelFile)
    {
        int idx = Array.IndexOf(_levelOrder, levelFile);
        if (idx <= 0)
            return null;
        if (_completedLevels.Contains(_levelOrder[idx - 1]))
            return null;
        return $"Complete {GetLevelDisplayName(_levelOrder[idx - 1])} to unlock";
    }

    /// <summary>Returns true if the level is available to play.</summary>
    public static bool IsAbilityUnlocked(string abilityName)
    {
        return _unlockedAbilities.Contains(abilityName);
    }

    /// <summary>Returns true if the item is available for selection.</summary>
    public static bool IsItemUnlocked(ItemType item)
    {
#if DEBUG
        if (item == ItemType.DevMode)
            return true;
#endif
        if (!_itemUnlockRequirements.TryGetValue(item, out var requiredLevel))
            return true; // No requirement — always unlocked
        return _completedLevels.Contains(requiredLevel);
    }

    /// <summary>
    /// Returns a human-readable hint describing how to unlock the item,
    /// or null if the item is always unlocked.
    /// </summary>
    public static string? GetUnlockHint(ItemType item)
    {
        if (!_itemUnlockRequirements.TryGetValue(item, out var level))
            return null;
        return $"Complete {GetLevelDisplayName(level)} to unlock";
    }

    /// <summary>Returns the unlock requirement level for this item, or null if always unlocked.</summary>
    public static string? GetUnlockRequirement(ItemType item)
    {
        return _itemUnlockRequirements.TryGetValue(item, out var level) ? level : null;
    }

    /// <summary>Returns true if the blaster attachment is available for selection.</summary>
    public static bool IsAttachmentUnlocked(BlasterAttachmentType attachment)
    {
#if DEBUG
        if (attachment == BlasterAttachmentType.DevBlaster)
            return true;
#endif
        if (!_attachmentUnlockRequirements.TryGetValue(attachment, out var requiredLevel))
            return true;
        return _completedLevels.Contains(requiredLevel);
    }

    /// <summary>Returns a human-readable hint describing how to unlock the attachment, or null if always unlocked.</summary>
    public static string? GetAttachmentUnlockHint(BlasterAttachmentType attachment)
    {
        if (!_attachmentUnlockRequirements.TryGetValue(attachment, out var level))
            return null;
        return $"Complete {GetLevelDisplayName(level)} to unlock";
    }

    /// <summary>Returns the unlock requirement level for this attachment, or null if always unlocked.</summary>
    public static string? GetAttachmentUnlockRequirement(BlasterAttachmentType attachment)
    {
        return _attachmentUnlockRequirements.TryGetValue(attachment, out var level) ? level : null;
    }

    /// <summary>
    /// Returns the number of player item slots available.
    /// Starts at 2; completing Level5, Level10, and Level15 each add one slot.
    /// </summary>
    public static int GetPlayerItemSlotCount()
    {
        int slots = 2;
        if (_completedLevels.Contains("Level5"))
            slots++;
        if (_completedLevels.Contains("Level10"))
            slots++;
        if (_completedLevels.Contains("Level15"))
            slots++;
        return slots;
    }

    /// <summary>
    /// Returns the number of blaster attachment slots available.
    /// Starts at 2; completing Level5, Level10, and Level15 each add one slot.
    /// </summary>
    public static int GetBlasterSlotCount()
    {
        int slots = 2;
        if (_completedLevels.Contains("Level5"))
            slots++;
        if (_completedLevels.Contains("Level10"))
            slots++;
        if (_completedLevels.Contains("Level15"))
            slots++;
        return slots;
    }

    /// <summary>Converts an internal level file name to a display name.</summary>
    public static string GetLevelDisplayName(string levelName)
    {
        if (levelName.StartsWith("Level", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(levelName.AsSpan("Level".Length), out int levelNumber))
        {
            return $"Level {levelNumber}";
        }

        return levelName;
    }

    /// <summary>Loads unlock data from disk. Safe to call multiple times.</summary>
    public static void Load()
    {
        _completedLevels = new HashSet<string>();
        _unlockedAbilities = new HashSet<string>();
        try
        {
            if (!File.Exists(SavePath))
                return;
            string json = File.ReadAllText(SavePath);
            var raw = JsonSerializer.Deserialize<List<string>>(json);
            if (raw != null)
            {
                _completedLevels = new HashSet<string>(raw);
                _unlockedAbilities = new HashSet<string>(raw);
            }
        }
        catch { }
        foreach (var ability in _abilityNames)
        {
            _unlockedAbilities.Add(ability);
        }
    }

    /// <summary>Records that a level has been completed and saves to disk.</summary>
    public static void RecordLevelComplete(string levelName)
    {
        if (_completedLevels.Add(levelName))
            Save();
    }

    // <summary>Records that an ability has been unlocked and saves to disk.</summary>
    public static void RecordAbilityUnlocked(string abilityName)
    {
        if (_unlockedAbilities.Add(abilityName))
            Save();
    }

    /// <summary>Clears all unlock data from memory and disk (used for testing/reset).</summary>
    public static void Reset()
    {
        _completedLevels = new HashSet<string>();
        _unlockedAbilities = new HashSet<string>();
        try
        { if (File.Exists(SavePath)) File.Delete(SavePath); }
        catch { }
    }

#if DEBUG
    /// <summary>[DEV] Toggles a level's completed state and saves.</summary>
    public static void ToggleLevelComplete(string levelFile)
    {
        if (!_completedLevels.Add(levelFile))
            _completedLevels.Remove(levelFile);
        Save();
    }

    /// <summary>
    public static void ToggleAbilityUnlocked(string abilityName)
    {
        if (!_unlockedAbilities.Add(abilityName))
            _unlockedAbilities.Remove(abilityName);
        Save();
    }
#endif

    private static void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SavePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(SavePath, JsonSerializer.Serialize(new List<string>(_completedLevels)));
        }
        catch { }
    }
}
