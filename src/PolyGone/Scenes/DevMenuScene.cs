using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;

namespace PolyGone;

#if DEBUG
internal class DevMenuScene : IScene
{
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly GraphicsDeviceManager _graphics;

    // Cursor: 0..LevelFiles.Length-1 = level toggles, then Unlock All, Lock All, Close
    private int _cursor = 0;
    private int _scrollOffset = 0;
    private int _previousScrollWheelValue;

    private static readonly string[] LevelFiles = UnlockTracker.GetPlannedLevels().ToArray();
    private static readonly string[] LevelDisplayNames = LevelFiles.Select(UnlockTracker.GetLevelDisplayName).ToArray();
    private static readonly string[] AbilityNames = { "Dash", "WallJump" };
    private static readonly string[] AbilityDisplayNames = { "Dash", "Wall Jump" };
    private static readonly string[] Actions = { "Unlock All Levels", "Lock All Levels", "Unlock All Abilities", "Lock All Abilities", "Close" };
    private const int RowHeight = 40;
    private const int ListStartX = 120;
    private const int ListStartY = 200;
    private const int ListBottomMargin = 80;
    private int EntryCount => LevelFiles.Length + AbilityNames.Length + Actions.Length;

    public DevMenuScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
    {
        _content               = content;
        _sceneManager          = sceneManager;
        _graphics              = graphics;
    }

    public void Load()
    {
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, EntryCount - 1));
        _scrollOffset = 0;
        _previousScrollWheelValue = Mouse.GetState().ScrollWheelValue;
        if (_font == null)
        {
            try { _font = _content.Load<SpriteFont>("Fonts/PauseMenu"); }
            catch { }
        }
    }

    public void Update(GameTime gameTime)
    {
        HandleMouseWheelScroll();
        EnsureCursorVisible();

        if (_font != null)
        {
            var viewport  = _graphics.GraphicsDevice.Viewport;
            int startX = ListStartX;

            for (int i = 0; i < LevelFiles.Length; i++)
            {
                int index = i;
                int rowY = GetRowY(index);
                if (!IsRowVisible(rowY, viewport.Height))
                {
                    continue;
                }

                bool completed = UnlockTracker.IsLevelCompleted(LevelFiles[i]);
                string text    = (completed ? "[X] " : "[ ] ") + LevelDisplayNames[i];
                var size       = _font.MeasureString(text);
                var bounds     = new Rectangle(startX, rowY, (int)size.X, (int)size.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _cursor = index;
                    if (InputManager.MenuConfirm())
                    {
                        UnlockTracker.ToggleLevelComplete(LevelFiles[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            for (int i = 0; i < AbilityNames.Length; i++)
            {
                int index = LevelFiles.Length + i;
                int rowY = GetRowY(index);
                if (!IsRowVisible(rowY, viewport.Height))
                {
                    continue;
                }

                bool completed = UnlockTracker.IsAbilityUnlocked(AbilityNames[i]);
                string text = (completed ? "[X] " : "[ ] ") + AbilityDisplayNames[i];
                var size = _font.MeasureString(text);
                var bounds = new Rectangle(startX, rowY, (int)size.X, (int)size.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _cursor = index;
                    if (InputManager.MenuConfirm())
                    {
                        UnlockTracker.ToggleAbilityUnlocked(AbilityNames[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            for (int i = 0; i < Actions.Length; i++)
            {
                int index = LevelFiles.Length + AbilityNames.Length + i;
                int rowY = GetRowY(index);
                if (!IsRowVisible(rowY, viewport.Height))
                {
                    continue;
                }

                var size   = _font.MeasureString(Actions[i]);
                var bounds = new Rectangle(startX, rowY, (int)size.X, (int)size.Y);
                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _cursor = index;
                    if (InputManager.MenuConfirm())
                    {
                        ExecuteAction(_cursor);
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        if (InputManager.MenuUp())
        {
            _cursor = (_cursor - 1 + EntryCount) % EntryCount;
            EnsureCursorVisible();
        }
        if (InputManager.MenuDown())
        {
            _cursor = (_cursor + 1) % EntryCount;
            EnsureCursorVisible();
        }
        if (InputManager.MenuNonPointerConfirm()) ExecuteAction(_cursor);
        if (InputManager.MenuBack()) _sceneManager.PopScene(this);
    }

    private void ExecuteAction(int index)
    {
        if (index < LevelFiles.Length)
        {
            UnlockTracker.ToggleLevelComplete(LevelFiles[index]);
        }
        else if (index >= LevelFiles.Length && index < (LevelFiles.Length + AbilityNames.Length))
        {
            UnlockTracker.ToggleAbilityUnlocked(AbilityNames[index - LevelFiles.Length]);
        }
        else
        {
            int action = index - (LevelFiles.Length + AbilityNames.Length);
            if (action == 0)      // Unlock All Levels
            {
                foreach (var lf in LevelFiles)
                    UnlockTracker.RecordLevelComplete(lf);
            }
            else if (action == 1) // Lock All Levels
            {
                foreach (var lf in LevelFiles)
                {
                    if (UnlockTracker.IsLevelCompleted(lf))
                    {
                        UnlockTracker.ToggleLevelComplete(lf);
                        InventoryManagement.ResetSavedLoadout();
                    }
                }
            }
            else if (action == 2) // Unlock All Abilities
            {
                foreach (var ability in AbilityNames)
                    UnlockTracker.RecordAbilityUnlocked(ability);
            }
            else if (action == 3) // Lock All Abilities
            {
                foreach (var ability in AbilityNames)
                {
                    if (UnlockTracker.IsAbilityUnlocked(ability))
                    {
                        UnlockTracker.ToggleAbilityUnlocked(ability);
                    }
                }
            }
            else                  // Close
            {
                _sceneManager.PopScene(this);
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        var vp = spriteBatch.GraphicsDevice.Viewport;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, vp.Width, vp.Height), new Color(15, 15, 35));

        if (_font == null) return;

        // Title
        string title    = "[DEV] Unlock Manager";
        var titleSize   = _font.MeasureString(title);
        spriteBatch.DrawString(_font, title, new Vector2(vp.Width / 2f - titleSize.X / 2f, 50), Color.Cyan);

        // ── Left column: level toggles ───────────────────────────────────
        int startX = ListStartX;
        int startY = ListStartY;
        DrawSectionHeaderIfVisible(spriteBatch, "Levels (toggle completed):", 0, startX, vp.Height);

        for (int i = 0; i < LevelFiles.Length; i++)
        {
            int idx = i;
            int rowY = GetRowY(idx);
            if (!IsRowVisible(rowY, vp.Height))
            {
                continue;
            }

            bool completed = UnlockTracker.IsLevelCompleted(LevelFiles[i]);
            bool isCursor  = _cursor == idx;
            string prefix  = completed ? "[X] " : "[ ] ";
            Color color    = isCursor ? Color.Yellow : (completed ? Color.LightGreen : Color.White);
            spriteBatch.DrawString(_font, prefix + LevelDisplayNames[i], new Vector2(startX, rowY), color);
        }

        // ── Left column: ability toggles ───────────────────────────────────
        DrawSectionHeaderIfVisible(spriteBatch, "Abilities (toggle unlocked):", LevelFiles.Length, startX, vp.Height);

        for (int i = 0; i < AbilityNames.Length; i++)
        {
            int idx = LevelFiles.Length + i;
            int rowY = GetRowY(idx);
            if (!IsRowVisible(rowY, vp.Height))
            {
                continue;
            }

            bool completed = UnlockTracker.IsAbilityUnlocked(AbilityNames[i]);
            bool isCursor = _cursor == idx;
            string prefix = completed ? "[X] " : "[ ] ";
            Color color = isCursor ? Color.Yellow : (completed ? Color.LightGreen : Color.White);
            spriteBatch.DrawString(_font, prefix + AbilityDisplayNames[i], new Vector2(startX, rowY), color);
        }

        // Action rows
        DrawSectionHeaderIfVisible(spriteBatch, "Actions:", LevelFiles.Length + AbilityNames.Length, startX, vp.Height);
        for (int i = 0; i < Actions.Length; i++)
        {
            int idx       = LevelFiles.Length + AbilityNames.Length + i;
            int rowY = GetRowY(idx);
            if (!IsRowVisible(rowY, vp.Height))
            {
                continue;
            }

            bool isCursor = _cursor == idx;
            Color color   = isCursor ? Color.Yellow : ((i == 1 || i == 3) ? Color.Tomato : Color.White);
            string prefix = isCursor ? "> " : "  ";
            spriteBatch.DrawString(_font, prefix + Actions[i], new Vector2(startX, rowY), color);
        }

        // ── Right column: item / attachment unlock status (read-only) ─────
        int rightX = vp.Width / 2 + 50;
        spriteBatch.DrawString(_font, "Player Item Unlock Status:", new Vector2(rightX, startY - 40), Color.LightCyan);

        var allItems = new (ItemType Type, string Name, string Req)[]
        {
            (ItemType.HealingGlow, "Healing Glow", "Level 1"),
            (ItemType.LowGravity,  "Low Gravity",  "Level 2"),
            (ItemType.IronWill,    "Iron Will",    "Level 3"),
            (ItemType.DevMode,     "Dev Mode",     "always [DEV]"),
        };

        for (int i = 0; i < allItems.Length; i++)
        {
            bool unlocked = UnlockTracker.IsItemUnlocked(allItems[i].Type);
            string prefix = unlocked ? "[+] " : "[ ] ";
            Color color   = unlocked ? Color.LightGreen : Color.DarkGray;
            spriteBatch.DrawString(_font,
                $"{prefix}{allItems[i].Name}  ({allItems[i].Req})",
                new Vector2(rightX, startY + i * 40), color);
        }

        // Blaster attachment unlock status
        int attY = startY + allItems.Length * 40 + 20;
        spriteBatch.DrawString(_font, "Blaster Attachment Unlocks:", new Vector2(rightX, attY - 30), Color.LightCyan);
        var allAttachments = new (BlasterAttachmentType Type, string Name, string Req)[]
        {
            (BlasterAttachmentType.MultiShot,   "Multi-Shot",     "Level 1"),
            (BlasterAttachmentType.RapidFire,   "Rapid Fire",     "Level 2"),
            (BlasterAttachmentType.Piercing,    "Piercing Rounds","Level 3"),
            (BlasterAttachmentType.DamageBoost, "Damage Amp",     "Level 4"),
            (BlasterAttachmentType.DevBlaster,  "Dev Blaster",    "always [DEV]"),
        };
        for (int i = 0; i < allAttachments.Length; i++)
        {
            bool unlocked = UnlockTracker.IsAttachmentUnlocked(allAttachments[i].Type);
            string prefix = unlocked ? "[+] " : "[ ] ";
            Color color   = unlocked ? Color.LightGreen : Color.DarkGray;
            spriteBatch.DrawString(_font,
                $"{prefix}{allAttachments[i].Name}  ({allAttachments[i].Req})",
                new Vector2(rightX, attY + i * 40), color);
        }
    }

    private int GetVisibleRowCount()
    {
        int viewportHeight = _graphics.GraphicsDevice.Viewport.Height;
        int usableHeight = Math.Max(RowHeight, viewportHeight - ListStartY - ListBottomMargin);
        return Math.Max(1, usableHeight / RowHeight);
    }

    private int GetMaxScrollOffset()
    {
        return Math.Max(0, EntryCount - GetVisibleRowCount());
    }

    private int GetRowY(int entryIndex)
    {
        return ListStartY + (entryIndex - _scrollOffset) * RowHeight;
    }

    private static bool IsRowVisible(int rowY, int viewportHeight)
    {
        return rowY + RowHeight >= ListStartY && rowY <= viewportHeight - ListBottomMargin;
    }

    private void EnsureCursorVisible()
    {
        int visible = GetVisibleRowCount();
        if (_cursor < _scrollOffset)
        {
            _scrollOffset = _cursor;
        }
        else if (_cursor >= _scrollOffset + visible)
        {
            _scrollOffset = _cursor - visible + 1;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, GetMaxScrollOffset());
    }

    private void HandleMouseWheelScroll()
    {
        var mouseState = Mouse.GetState();
        int delta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
        _previousScrollWheelValue = mouseState.ScrollWheelValue;
        if (delta == 0)
        {
            return;
        }

        if (delta > 0)
        {
            _scrollOffset = Math.Max(0, _scrollOffset - 1);
        }
        else
        {
            _scrollOffset = Math.Min(GetMaxScrollOffset(), _scrollOffset + 1);
        }

        int visible = GetVisibleRowCount();
        if (_cursor < _scrollOffset)
        {
            _cursor = _scrollOffset;
        }
        else if (_cursor >= _scrollOffset + visible)
        {
            _cursor = Math.Min(EntryCount - 1, _scrollOffset + visible - 1);
        }
    }

    private void DrawSectionHeaderIfVisible(SpriteBatch spriteBatch, string text, int sectionStartIndex, int x, int viewportHeight)
    {
        int headerY = GetRowY(sectionStartIndex) - 40;
        if (headerY + RowHeight < ListStartY || headerY > viewportHeight - ListBottomMargin)
        {
            return;
        }

        spriteBatch.DrawString(_font!, text, new Vector2(x, headerY), Color.LightCyan);
    }
}
#endif
