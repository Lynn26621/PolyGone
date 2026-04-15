using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PolyGone.Core;

namespace PolyGone
{
    // ---------------------------------------------------------------------------
    // Player-item types (movement / survival / abilities)
    // ---------------------------------------------------------------------------
    public enum ItemType
    {
        DoubleJump,
        SpeedBoost,
        HealingGlow,
        LowGravity,
        IronWill,
#if DEBUG
        DevMode
#endif
    }

    // ---------------------------------------------------------------------------
    // Blaster attachment types (weapon modifications)
    // ---------------------------------------------------------------------------
    public enum BlasterAttachmentType
    {
        MultiShot,
        RapidFire,
        Piercing,
        DamageBoost,
#if DEBUG
        DevBlaster
#endif
    }

    // ---------------------------------------------------------------------------
    // Inventory management scene
    // ---------------------------------------------------------------------------
    internal class InventoryManagement : IScene
    {
        private Texture2D? _pixel;
        private SpriteFont? _font;
        private readonly ContentManager _content;
        private readonly SceneManager _sceneManager;
        private readonly AudioManager _audioManager;
        private readonly GraphicsDeviceManager _graphics;
        private readonly string _levelFile;

        // -----------------------------------------------------------------------
        // Player item definitions
        // -----------------------------------------------------------------------
        private readonly string[] _playerItemNames =
        {
            "Double Jump",
            "Speed Boost",
            "Healing Glow",
            "Low Gravity",
            "Iron Will",
#if DEBUG
            "Dev Mode"
#endif
        };
        private readonly ItemType[] _playerItemTypes =
        {
            ItemType.DoubleJump,
            ItemType.SpeedBoost,
            ItemType.HealingGlow,
            ItemType.LowGravity,
            ItemType.IronWill,
#if DEBUG
            ItemType.DevMode
#endif
        };
        private readonly string[] _playerItemDescriptions =
        {
            "One additional jump while airborne",
            "Move 50% faster",
            "Regenerate 10 HP every 2 seconds",
            "40% gravity - rises and falls slowly, same jump height",
            "Once per 20s, survive a killing blow and stay at 1 HP",
#if DEBUG
            "[DEV] Invincibility + instant kills + infinite jumps"
#endif
        };

        // -----------------------------------------------------------------------
        // Blaster attachment definitions
        // -----------------------------------------------------------------------
        private readonly string[] _attachmentNames =
        {
            "Multi-Shot",
            "Rapid Fire",
            "Piercing Rounds",
            "Damage Amp",
#if DEBUG
            "Dev Blaster"
#endif
        };
        private readonly BlasterAttachmentType[] _attachmentTypes =
        {
            BlasterAttachmentType.MultiShot,
            BlasterAttachmentType.RapidFire,
            BlasterAttachmentType.Piercing,
            BlasterAttachmentType.DamageBoost,
#if DEBUG
            BlasterAttachmentType.DevBlaster
#endif
        };
        private readonly string[] _attachmentDescriptions =
        {
            "Adds 2 extra spread bullets per shot",
            "Reduces weapon cooldown to 1/3",
            "Bullets pass through all enemies",
            "+50% bullet damage on every shot",
#if DEBUG
            "[DEV] All attachment effects combined"
#endif
        };

        // -----------------------------------------------------------------------
        // Persistent last-selection state
        // -----------------------------------------------------------------------
        private static List<ItemType> _lastSelectedPlayerItems = new List<ItemType> { ItemType.DoubleJump };
        private static List<BlasterAttachmentType> _lastSelectedAttachments = new List<BlasterAttachmentType> { BlasterAttachmentType.MultiShot };

        private static readonly string _loadoutSavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PolyGone", "loadout.json");

        static InventoryManagement()
        {
            TryLoadLoadout();
        }

        // -----------------------------------------------------------------------
        // Navigation state
        // -----------------------------------------------------------------------
        private enum SelectionMode { Loadout, Confirm }
        private SelectionMode _currentMode = SelectionMode.Loadout;
        private int _confirmCursor = 0; // 0 = Start Game, 1 = Back


        private readonly List<ItemType> _selectedPlayerItems;
        private readonly List<BlasterAttachmentType> _selectedAttachments;

        private readonly int[] _playerItemSlotCosts =
        {
            2, // Double Jump
            1, // Speed Boost
            2, // Healing Glow
            1, // Low Gravity
            2, // Iron Will
    #if DEBUG
            0  // Dev Mode
    #endif
        };

        private readonly int[] _attachmentSlotCosts =
        {
            1, // Multi-Shot
            2, // Rapid Fire
            2, // Piercing
            2, // Damage Amp
    #if DEBUG
            0  // Dev Blaster
    #endif
        };

        public InventoryManagement(ContentManager content, SceneManager sceneManager, AudioManager audioManager, GraphicsDeviceManager graphics, string levelFile)
        {
            _content = content;
            _sceneManager = sceneManager;
            _audioManager = audioManager;
            _graphics = graphics;
            _levelFile = levelFile;

            // Initialize with last selected values, filtering out any that are now locked
            _selectedItems = new List<ItemType>(_lastSelectedItems.FindAll(UnlockTracker.IsItemUnlocked));
            _selectedWeapon = _lastSelectedWeapon;

            TrimSelectionsToCapacity();
        }

        public void Load()
        {
            if (_font == null)
            {
                try
                { _font = _content.Load<SpriteFont>("Fonts/PauseMenu"); }
                catch { }
            }
            _audioManager.PlayAudio("null", false, "menuSong", true);
        }

        // -----------------------------------------------------------------------
        // Update
        // -----------------------------------------------------------------------
        public void Update(GameTime gameTime)
        {

            // Check for Control key to skip inventory and start with current selections
            if (InputManager.LoadoutSkip())
            {
                StartGame();
                return;
            }

            // Check for Escape key to go back to previous screen
            if (InputManager.MenuBack())
            {
                _sceneManager.PopScene(this);
                return;
            }

            HandleKeyboardNavigation();
            HandleMouseNavigation();

            switch (_currentMode)
            {
                case SelectionMode.PlayerItems:  UpdatePlayerItemSelection();  break;
                case SelectionMode.Attachments:  UpdateAttachmentSelection();  break;
                case SelectionMode.Confirm:      UpdateConfirmSelection();     break;
            }
        }

        private void HandleKeyboardNavigation()
        {
            if (IsKeyPressed(Keys.Enter))
            {
                if (_currentMode == SelectionMode.Confirm)
                {
                    _currentMode      = SelectionMode.PlayerItems;
                    _playerItemCursor = i;

                    if (InputManager.MenuConfirm())
                    {
                        _sceneManager.PopScene(this);
                    }
                }
                else
                {
                    _currentMode = SelectionMode.Confirm;
                    _confirmCursor = 0;
                }
            }
        }

        private void HandleMouseNavigation()
        {
            if (_font == null || !InputManager.IsLeftMouseButtonClicked())
            {
                return;
            }

            var viewport = _graphics.GraphicsDevice.Viewport;
            var mousePos = InputManager.GetMousePosition();

                    if (InputManager.MenuConfirm())
                    {
                        ToggleAttachment(_attachmentTypes[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            if (GetBackButtonRect(viewport).Contains(mousePos))
            {
                _currentMode = SelectionMode.Confirm;
                _confirmCursor = 1;
                _sceneManager.PopScene(this);
                InputManager.ConsumeClick();
                return;
            }

                    if (InputManager.MenuConfirm())
                    {
                        if (_confirmCursor == 0) StartGame();
                        else _sceneManager.PopScene(this);
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        private bool TryHandleEquippedClick(Viewport viewport, Point mousePos)
        {
            if (InputManager.MenuUp())
            {
                _itemCursor = (_itemCursor - 1 + _itemNames.Length) % _itemNames.Length;
            }

            if (InputManager.MenuDown())
            {
                _itemCursor = (_itemCursor + 1) % _itemNames.Length;
            }

            if (InputManager.MenuConfirm())
            {
                ItemType selectedItem = _itemTypes[_itemCursor];

                if (!UnlockTracker.IsItemUnlocked(selectedItem))
                {
                    // Item is locked — do nothing
                }
                else if (_selectedItems.Contains(selectedItem))
                {
                    // Deselect item
                    _selectedItems.Remove(selectedItem);
                }
#if DEBUG
                else
                {
                    // DEV: no item limit
                    _selectedItems.Add(selectedItem);
                }
#else
                else if (_selectedItems.Count < 2)
                {
                    // Select item (max 2)
                    _selectedItems.Add(selectedItem);
                }
#endif
            }

            if (InputManager.LoadoutSectionRight())
            {
                // Move to weapon selection
                _currentMode = SelectionMode.Weapon;
            }
        }

        private void UpdateAttachmentSelection()
        {
            if (InputManager.MenuUp())
            {
                _weaponCursor = (_weaponCursor - 1 + _weaponNames.Length) % _weaponNames.Length;
            }

            if (InputManager.MenuDown())
            {
                _weaponCursor = (_weaponCursor + 1) % _weaponNames.Length;
            }

            if (InputManager.MenuConfirm())
            {
                // Select weapon
                _selectedWeapon = _weaponTypes[_weaponCursor];
            }

            if (InputManager.LoadoutSectionLeft())
            {
                // Move back to item selection
                _currentMode = SelectionMode.Items;
            }

            if (InputManager.LoadoutSectionRight())
            {
                // Move to confirm
                _currentMode = SelectionMode.Confirm;
            }
            var entry = equippedEntries[rowIndex];
            if (entry.IsAttachment)
            {
                var attachment = _attachmentTypes[entry.DefinitionIndex];
                _selectedAttachments.Remove(attachment);
            }
            else
            {
                var item = _playerItemTypes[entry.DefinitionIndex];
                _selectedPlayerItems.Remove(item);
            }
        }

        private bool TryHandleUnequippedClick(Viewport viewport, Point mousePos)
        {
            if (InputManager.MenuUp() || InputManager.MenuDown())
            {
                _confirmCursor = (_confirmCursor + 1) % 2;

            if (InputManager.MenuConfirm())
            var unequippedArea = GetUnequippedListRect(viewport);
            if (!unequippedArea.Contains(mousePos))
            {
                return false;
            }

            int rowIndex = GetClickedRowIndex(unequippedArea, mousePos, ListRowHeight, 10);
            if (rowIndex < 0)
            {
                return false;
            }

            if (InputManager.LoadoutSectionLeft())

            var entry = candidates[rowIndex];
            if (entry.IsAttachment)
            {
                var attachment = _attachmentTypes[entry.DefinitionIndex];
                if (!UnlockTracker.IsAttachmentUnlocked(attachment))
                {                    return true;
                }

                int cost = GetAttachmentSlotCost(attachment);
                int used = GetTotalUsedSlots();
                int max = GetSharedSlotCount();
                if (used + cost > max)
                {
                    return true;
                }

                _selectedAttachments.Add(attachment);
                return true;
            }

            var itemType = _playerItemTypes[entry.DefinitionIndex];
            if (!UnlockTracker.IsItemUnlocked(itemType))
            {
                return true;
            }

            int itemCost = GetPlayerItemSlotCost(itemType);
            int totalUsed = GetTotalUsedSlots();
            int sharedMax = GetSharedSlotCount();
            if (totalUsed + itemCost > sharedMax)
            {
                return true;
            }

            _selectedPlayerItems.Add(itemType);
            return true;
        }

        private static int GetClickedRowIndex(Rectangle area, Point mousePos, int rowHeight, int topPadding)
        {
            int row = (mousePos.Y - area.Y - topPadding) / rowHeight;
            return row < 0 ? -1 : row;
        }

        private void TrimSelectionsToCapacity()
        {
            int maxSlots = GetSharedSlotCount();
            int used = 0;

            var keptItems = new List<ItemType>();
            foreach (var item in _selectedPlayerItems)
            {
                int cost = Math.Max(0, GetPlayerItemSlotCost(item));
                if (used + cost > maxSlots)
                {
                    continue;
                }

                used += cost;
                keptItems.Add(item);
            }

            var keptAttachments = new List<BlasterAttachmentType>();
            foreach (var attachment in _selectedAttachments)
            {
                int cost = Math.Max(0, GetAttachmentSlotCost(attachment));
                if (used + cost > maxSlots)
                {
                    continue;
                }

                used += cost;
                keptAttachments.Add(attachment);
            }

            _selectedPlayerItems.Clear();
            _selectedPlayerItems.AddRange(keptItems);
            _selectedAttachments.Clear();
            _selectedAttachments.AddRange(keptAttachments);
        }

        private int GetTotalUsedSlots()
        {
            return _selectedPlayerItems.Sum(GetPlayerItemSlotCost)
                + _selectedAttachments.Sum(GetAttachmentSlotCost);
        }

        private int GetSharedSlotCount()
        {
            return Math.Min(
                UnlockTracker.GetPlayerItemSlotCount(),
                UnlockTracker.GetBlasterSlotCount());
        }

        // -----------------------------------------------------------------------
        // Start game / reset
        // -----------------------------------------------------------------------
        public static void ResetSavedLoadout()
        {
            _lastSelectedPlayerItems = new List<ItemType> { ItemType.DoubleJump };
            _lastSelectedAttachments = new List<BlasterAttachmentType> { BlasterAttachmentType.MultiShot };
        }

        private void StartGame()
        {
            TrimSelectionsToCapacity();

            _lastSelectedPlayerItems = new List<ItemType>(_selectedPlayerItems);
            _lastSelectedAttachments = new List<BlasterAttachmentType>(_selectedAttachments);
            SaveLoadout();

            _sceneManager.PopScene(this);
            _sceneManager.AddScene(new GameScene(_content, _sceneManager, _audioManager, _graphics, _levelFile,
                _selectedPlayerItems, _selectedAttachments));
            InputManager.ResetClickCooldown();
        }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------
        public void Draw(SpriteBatch spriteBatch)
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }

            spriteBatch.Draw(_pixel, new Rectangle(0, 0,
                spriteBatch.GraphicsDevice.Viewport.Width,
                spriteBatch.GraphicsDevice.Viewport.Height), new Color(14, 24, 52));

            if (_font == null)
            {
                return;
            }

            var viewport = spriteBatch.GraphicsDevice.Viewport;

            DrawCentered(spriteBatch, "Inventory", 16, Color.White);
            DrawSlotMeter(spriteBatch, viewport);
            DrawDualLists(spriteBatch, viewport);
            DrawActionButtons(spriteBatch, viewport);
            Color statusColor = Color.LightGray;
            string statusMessage = "";
            DrawCentered(spriteBatch, statusMessage, viewport.Height - 55, statusColor);
        }

        private void DrawSlotMeter(SpriteBatch spriteBatch, Viewport viewport)
        {
            int used = GetTotalUsedSlots();
            int max = GetSharedSlotCount();

            Rectangle meterOuter = GetMeterRect(viewport);
            Rectangle meterInner = new Rectangle(meterOuter.X + 3, meterOuter.Y + 3, meterOuter.Width - 6, meterOuter.Height - 6);
            float ratio = max <= 0 ? 0f : Math.Clamp(used / (float)max, 0f, 1f);
            Rectangle fill = new Rectangle(meterInner.X, meterInner.Y, (int)(meterInner.Width * ratio), meterInner.Height);

            spriteBatch.Draw(_pixel, meterOuter, new Color(30, 30, 40, 170));
            DrawRectOutline(spriteBatch, meterOuter, Color.White, 2);
            spriteBatch.Draw(_pixel, meterInner, new Color(52, 62, 82, 190));
            spriteBatch.Draw(_pixel, fill, used > max ? Color.IndianRed : Color.MediumSeaGreen);

            string meterLabel = $"Equipped Slots: {used} / {max}";
            Vector2 labelSize = _font!.MeasureString(meterLabel) * UiScale;
            DrawUiString(spriteBatch, meterLabel,
                new Vector2(meterOuter.X + (meterOuter.Width - labelSize.X) / 2f, meterOuter.Y - 34),
                Color.White);
        }

        private void DrawActionButtons(SpriteBatch spriteBatch, Viewport viewport)
        {
            DrawButton(spriteBatch, GetStartButtonRect(viewport), "Start Game", _currentMode == SelectionMode.Confirm && _confirmCursor == 0);
            DrawButton(spriteBatch, GetBackButtonRect(viewport), "Back", _currentMode == SelectionMode.Confirm && _confirmCursor == 1);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text, bool active)
        {
            spriteBatch.Draw(_pixel, rect, active ? new Color(255, 255, 255, 60) : new Color(15, 18, 26, 70));
            DrawRectOutline(spriteBatch, rect, active ? Color.Yellow : Color.White, 2);
            Vector2 size = _font!.MeasureString(text) * UiScale;
            Vector2 pos = new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);
            DrawUiString(spriteBatch, text, pos, Color.White);
        }

        private const int ListRowHeight = 52;

        private void DrawDualLists(SpriteBatch spriteBatch, Viewport viewport)
        {
            Rectangle equippedArea = GetEquippedListRect(viewport);
            Rectangle unequippedArea = GetUnequippedListRect(viewport);

            DrawListAreaFrame(spriteBatch, equippedArea, "Equipped");
            DrawListAreaFrame(spriteBatch, unequippedArea, "Unequipped");

            var equippedEntries = GetEquippedEntries();
            for (int i = 0; i < equippedEntries.Count; i++)
            {
                var entry = equippedEntries[i];
                DrawRow(spriteBatch, equippedArea, i,
                    GetEntryLabel(entry.IsAttachment, entry.DefinitionIndex),
                    GetEntryName(entry.IsAttachment, entry.DefinitionIndex),
                    GetEntryCost(entry.IsAttachment, entry.DefinitionIndex),
                    true,
                    true);
            }

            var unequippedEntries = GetUnequippedEntries();
            for (int i = 0; i < unequippedEntries.Count; i++)
            {
                var entry = unequippedEntries[i];
                bool unlocked = IsEntryUnlocked(entry.IsAttachment, entry.DefinitionIndex);
                DrawRow(spriteBatch, unequippedArea, i,
                    GetEntryLabel(entry.IsAttachment, entry.DefinitionIndex),
                    GetEntryName(entry.IsAttachment, entry.DefinitionIndex),
                    GetEntryCost(entry.IsAttachment, entry.DefinitionIndex),
                    false,
                    unlocked);
            }
        }

        private void DrawListAreaFrame(SpriteBatch spriteBatch, Rectangle area, string label)
        {
            spriteBatch.Draw(_pixel, area, new Color(0, 0, 0, 95));
            DrawRectOutline(spriteBatch, area, Color.White, 2);
            DrawUiString(spriteBatch, label, new Vector2(area.X + 4, area.Y - 36), Color.LightGray);
        }

        private readonly struct EntryRef
        {
            public readonly bool IsAttachment;
            public readonly int DefinitionIndex;

            public EntryRef(bool isAttachment, int definitionIndex)
            {
                IsAttachment = isAttachment;
                DefinitionIndex = definitionIndex;
            }
        }

        private List<EntryRef> GetEquippedEntries()
        {
            var result = new List<EntryRef>();
            foreach (var item in _selectedPlayerItems)
            {
                int idx = Array.IndexOf(_playerItemTypes, item);
                if (idx >= 0)
                {
                    result.Add(new EntryRef(false, idx));
                }
            }

            foreach (var attachment in _selectedAttachments)
            {
                int idx = Array.IndexOf(_attachmentTypes, attachment);
                if (idx >= 0)
                {
                    result.Add(new EntryRef(true, idx));
                }
            }

            return result;
        }

        private List<EntryRef> GetUnequippedEntries()
        {
            var result = new List<EntryRef>();
            for (int i = 0; i < _playerItemTypes.Length; i++)
            {
                if (!_selectedPlayerItems.Contains(_playerItemTypes[i]))
                {
                    result.Add(new EntryRef(false, i));
                }
            }

            for (int i = 0; i < _attachmentTypes.Length; i++)
            {
                if (!_selectedAttachments.Contains(_attachmentTypes[i]))
                {
                    result.Add(new EntryRef(true, i));
                }
            }

            return result;
        }

        private string GetEntryLabel(bool isAttachment, int definitionIndex)
            => isAttachment ? "ATT" : "ITEM";

        private string GetEntryName(bool isAttachment, int definitionIndex)
            => isAttachment ? _attachmentNames[definitionIndex] : _playerItemNames[definitionIndex];

        private int GetEntryCost(bool isAttachment, int definitionIndex)
            => isAttachment ? _attachmentSlotCosts[definitionIndex] : _playerItemSlotCosts[definitionIndex];

        private bool IsEntryUnlocked(bool isAttachment, int definitionIndex)
        {
            if (isAttachment)
            {
                return UnlockTracker.IsAttachmentUnlocked(_attachmentTypes[definitionIndex]);
            }

            return UnlockTracker.IsItemUnlocked(_playerItemTypes[definitionIndex]);
        }

        private void DrawRow(SpriteBatch spriteBatch, Rectangle listArea, int row, string typeLabel, string name, int slotCost, bool equipped, bool unlocked)
        {
            int y = listArea.Y + 10 + row * ListRowHeight;
            Rectangle rowRect = new Rectangle(listArea.X + 8, y, listArea.Width - 16, ListRowHeight - 6);
            if (rowRect.Bottom > listArea.Bottom - 6)
            {
                return;
            }

            Color fill = !unlocked
                ? new Color(70, 70, 70, 150)
                : equipped
                    ? new Color(72, 130, 78, 145)
                    : new Color(86, 96, 135, 130);

            spriteBatch.Draw(_pixel, rowRect, fill);
            DrawRectOutline(spriteBatch, rowRect, unlocked ? Color.White : Color.Gray, 1);

            string prefix = !unlocked ? "[LOCKED] " : "";
            DrawUiString(spriteBatch, $"{prefix}[{typeLabel}] {name}", new Vector2(rowRect.X + 10, rowRect.Y + 6), unlocked ? Color.White : Color.LightGray);

            string costText = $"{slotCost} slot" + (slotCost == 1 ? "" : "s");
            Vector2 costSize = _font!.MeasureString(costText) * UiScale;
            DrawUiString(spriteBatch, costText,
                new Vector2(rowRect.Right - costSize.X - 10, rowRect.Y + 6),
                unlocked ? Color.Gold : Color.Gray);
        }

        private static void SaveLoadout()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_loadoutSavePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = new
                {
                    PlayerItems = _lastSelectedPlayerItems.Select(i => (int)i).ToList(),
                    Attachments = _lastSelectedAttachments.Select(a => (int)a).ToList()
                };
                File.WriteAllText(_loadoutSavePath, JsonSerializer.Serialize(data));
            }
            catch { }
        }

        private static void TryLoadLoadout()
        {
            try
            {
                if (!File.Exists(_loadoutSavePath))
                {
                    return;
                }

                string json = File.ReadAllText(_loadoutSavePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("PlayerItems", out var itemsEl))
                {
                    _lastSelectedPlayerItems.Clear();
                    foreach (var item in itemsEl.EnumerateArray())
                    {
                        int val = item.GetInt32();
                        if (Enum.IsDefined(typeof(ItemType), val))
                        {
                            _lastSelectedPlayerItems.Add((ItemType)val);
                        }
                    }
                }

                if (root.TryGetProperty("Attachments", out var attachEl))
                {
                    _lastSelectedAttachments.Clear();
                    foreach (var att in attachEl.EnumerateArray())
                    {
                        int val = att.GetInt32();
                        if (Enum.IsDefined(typeof(BlasterAttachmentType), val))
                        {
                            _lastSelectedAttachments.Add((BlasterAttachmentType)val);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
