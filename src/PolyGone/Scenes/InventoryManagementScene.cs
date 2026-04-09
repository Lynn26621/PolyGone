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
        private KeyboardState keyboardState;
        private KeyboardState previousKeyboardState;
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
        private const int MaxSlots = 5; // Unlocking slots handled in unlockTracker line 117
        private enum SelectionMode { PlayerItems, Attachments, Confirm }
        private SelectionMode _currentMode = SelectionMode.PlayerItems;
        private int _confirmCursor = 0; // 0 = Start Game, 1 = Back
        private int _playerListScroll = 0;
        private int _attachmentListScroll = 0;

        private ItemType? _pendingPlayerItem = null;
        private BlasterAttachmentType? _pendingAttachment = null;
        private string _statusMessage = "Click an item below to assign it to a slot.";

        private readonly List<ItemType> _selectedPlayerItems;
        private readonly List<BlasterAttachmentType> _selectedAttachments;
        private readonly ItemType?[] _playerItemSlots = new ItemType?[MaxSlots];
        private readonly BlasterAttachmentType?[] _attachmentSlots = new BlasterAttachmentType?[MaxSlots];

        public InventoryManagement(ContentManager content, SceneManager sceneManager, AudioManager audioManager, GraphicsDeviceManager graphics, string levelFile)
        {
            _content = content;
            _sceneManager = sceneManager;
            _audioManager = audioManager;
            _graphics = graphics;
            _levelFile = levelFile;
            previousKeyboardState = Keyboard.GetState();

            // Initialise with last selections, filtering any that became locked
            _selectedPlayerItems = new List<ItemType>(
                _lastSelectedPlayerItems.FindAll(UnlockTracker.IsItemUnlocked));
            _selectedAttachments = new List<BlasterAttachmentType>(
                _lastSelectedAttachments.FindAll(UnlockTracker.IsAttachmentUnlocked));

            InitializeSlotsFromSelections();
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
            keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl))
            {
                StartGame();
                previousKeyboardState = keyboardState;
                return;
            }

            if (InputManager.IsEscapeKeyPressed())
            {
                _sceneManager.PopScene(this);
                previousKeyboardState = keyboardState;
                return;
            }

            HandleKeyboardNavigation();
            HandleMouseWheelScrolling();
            HandleMouseNavigation();

            previousKeyboardState = keyboardState;
        }

        private void HandleKeyboardNavigation()
        {
            if (IsKeyPressed(Keys.Tab))
            {
                _currentMode = _currentMode == SelectionMode.PlayerItems
                    ? SelectionMode.Attachments
                    : SelectionMode.PlayerItems;
            }

            if (IsKeyPressed(Keys.Up))
            {
                ScrollActiveList(-1);
            }

            if (IsKeyPressed(Keys.Down))
            {
                ScrollActiveList(1);
            }

            if (IsKeyPressed(Keys.Enter))
            {
                if (_currentMode == SelectionMode.Confirm)
                {
                    if (_confirmCursor == 0)
                    {
                        StartGame();
                    }
                    else
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

            if (_pendingPlayerItem.HasValue || _pendingAttachment.HasValue)
            {
                int selectedSlot = GetPressedSlotIndex();
                if (selectedSlot >= 0)
                {
                    AssignPendingToSlot(selectedSlot);
                }
            }
        }

        private void HandleMouseWheelScrolling()
        {
            int delta = InputManager.CurrentMouseState.ScrollWheelValue - InputManager.PreviousMouseState.ScrollWheelValue;
            if (delta == 0)
            {
                return;
            }
            ScrollActiveList(delta > 0 ? -1 : 1);
        }

        private void ScrollActiveList(int amount)
        {
            int visibleRows = GetVisibleListRows(_graphics.GraphicsDevice.Viewport);
            if (_currentMode == SelectionMode.Attachments)
            {
                int max = Math.Max(0, _attachmentNames.Length - visibleRows);
                _attachmentListScroll = Math.Clamp(_attachmentListScroll + amount, 0, max);
            }
            else
            {
                int max = Math.Max(0, _playerItemNames.Length - visibleRows);
                _playerListScroll = Math.Clamp(_playerListScroll + amount, 0, max);
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

            if (GetPlayerTabRect(viewport).Contains(mousePos))
            {
                _currentMode = SelectionMode.PlayerItems;
                InputManager.ConsumeClick();
                return;
            }

            if (GetAttachmentTabRect(viewport).Contains(mousePos))
            {
                _currentMode = SelectionMode.Attachments;
                InputManager.ConsumeClick();
                return;
            }

            if (GetStartButtonRect(viewport).Contains(mousePos))
            {
                _currentMode = SelectionMode.Confirm;
                _confirmCursor = 0;
                StartGame();
                InputManager.ConsumeClick();
                return;
            }

            if (GetBackButtonRect(viewport).Contains(mousePos))
            {
                _currentMode = SelectionMode.Confirm;
                _confirmCursor = 1;
                _sceneManager.PopScene(this);
                InputManager.ConsumeClick();
                return;
            }

            for (int i = 0; i < MaxSlots; i++)
            {
                if (GetClearSlotButtonRect(viewport, i).Contains(mousePos))
                {
                    ClearSlot(i);
                    InputManager.ConsumeClick();
                    return;
                }
            }

            for (int i = 0; i < MaxSlots; i++)
            {
                if (GetSlotRect(viewport, i).Contains(mousePos))
                {
                    AssignPendingToSlot(i);
                    InputManager.ConsumeClick();
                    return;
                }
            }

            if (TryGetClickedListIndex(viewport, mousePos, out int clickedIndex))
            {
                if (_currentMode == SelectionMode.Attachments)
                {
                    if (!UnlockTracker.IsAttachmentUnlocked(_attachmentTypes[clickedIndex]))
                    {
                        _statusMessage = UnlockTracker.GetAttachmentUnlockHint(_attachmentTypes[clickedIndex]) ?? "That attachment is locked.";
                    }
                    else
                    {
                        _pendingAttachment = _attachmentTypes[clickedIndex];
                        _pendingPlayerItem = null;
                        _statusMessage = $"Pick an unlocked slot (1-{UnlockTracker.GetBlasterSlotCount()}) for {_attachmentNames[clickedIndex]}.";
                    }
                }
                else
                {
                    if (!UnlockTracker.IsItemUnlocked(_playerItemTypes[clickedIndex]))
                    {
                        _statusMessage = UnlockTracker.GetUnlockHint(_playerItemTypes[clickedIndex]) ?? "That item is locked.";
                    }
                    else
                    {
                        _pendingPlayerItem = _playerItemTypes[clickedIndex];
                        _pendingAttachment = null;
                        _statusMessage = $"Pick an unlocked slot (1-{UnlockTracker.GetPlayerItemSlotCount()}) for {_playerItemNames[clickedIndex]}.";
                    }
                }

                InputManager.ConsumeClick();
            }
        }

        private bool TryGetClickedListIndex(Viewport viewport, Point mousePos, out int clickedIndex)
        {
            clickedIndex = -1;
            Rectangle listArea = GetListAreaRect(viewport);
            if (!listArea.Contains(mousePos))
            {
                return false;
            }

            int rowHeight = 52;
            int row = (mousePos.Y - listArea.Y - 12) / rowHeight;
            if (row < 0)
            {
                return false;
            }

            int visibleRows = GetVisibleListRows(viewport);
            if (row >= visibleRows)
            {
                return false;
            }

            int scroll = _currentMode == SelectionMode.Attachments ? _attachmentListScroll : _playerListScroll;
            int maxItems = _currentMode == SelectionMode.Attachments ? _attachmentNames.Length : _playerItemNames.Length;
            int index = scroll + row;
            if (index < 0 || index >= maxItems)
            {
                return false;
            }

            clickedIndex = index;
            return true;
        }

        private int GetPressedSlotIndex()
        {
            if (IsKeyPressed(Keys.D1) || IsKeyPressed(Keys.NumPad1))
            {
                return 0;
            }

            if (IsKeyPressed(Keys.D2) || IsKeyPressed(Keys.NumPad2))
            {
                return 1;
            }

            if (IsKeyPressed(Keys.D3) || IsKeyPressed(Keys.NumPad3))
            {
                return 2;
            }

            return -1;
        }

        private void AssignPendingToSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
            {
                return;
            }

            if (_currentMode == SelectionMode.Attachments)
            {
                int unlockedCount = UnlockTracker.GetBlasterSlotCount();
                if (slotIndex >= unlockedCount)
                {
                    _statusMessage = "That slot is locked.";
                    return;
                }

                if (!_pendingAttachment.HasValue)
                {
                    _statusMessage = "Select an attachment from the bottom list first.";
                    return;
                }

                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_attachmentSlots[i] == _pendingAttachment.Value)
                    {
                        _attachmentSlots[i] = null;
                    }
                }

                _attachmentSlots[slotIndex] = _pendingAttachment.Value;
                _statusMessage = $"{GetAttachmentName(_pendingAttachment.Value)} placed in slot {slotIndex + 1}.";
                _pendingAttachment = null;
            }
            else
            {
                int unlockedCount = UnlockTracker.GetPlayerItemSlotCount();
                if (slotIndex >= unlockedCount)
                {
                    _statusMessage = "That slot is locked.";
                    return;
                }

                if (!_pendingPlayerItem.HasValue)
                {
                    _statusMessage = "Select an item from the bottom list first.";
                    return;
                }

                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_playerItemSlots[i] == _pendingPlayerItem.Value)
                    {
                        _playerItemSlots[i] = null;
                    }
                }

                _playerItemSlots[slotIndex] = _pendingPlayerItem.Value;
                _statusMessage = $"{GetPlayerItemName(_pendingPlayerItem.Value)} placed in slot {slotIndex + 1}.";
                _pendingPlayerItem = null;
            }

            RebuildSelectionsFromSlots();
        }

        private void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
            {
                return;
            }

            if (_currentMode == SelectionMode.Attachments)
            {
                int unlockedCount = UnlockTracker.GetBlasterSlotCount();
                if (slotIndex >= unlockedCount)
                {
                    _statusMessage = "That slot is locked.";
                    return;
                }

                if (!_attachmentSlots[slotIndex].HasValue)
                {
                    _statusMessage = "That slot is already empty.";
                    return;
                }

                string removedName = GetAttachmentName(_attachmentSlots[slotIndex]!.Value);
                _attachmentSlots[slotIndex] = null;
                _statusMessage = $"Removed {removedName} from slot {slotIndex + 1}.";
            }
            else
            {
                int unlockedCount = UnlockTracker.GetPlayerItemSlotCount();
                if (slotIndex >= unlockedCount)
                {
                    _statusMessage = "That slot is locked.";
                    return;
                }

                if (!_playerItemSlots[slotIndex].HasValue)
                {
                    _statusMessage = "That slot is already empty.";
                    return;
                }

                string removedName = GetPlayerItemName(_playerItemSlots[slotIndex]!.Value);
                _playerItemSlots[slotIndex] = null;
                _statusMessage = $"Removed {removedName} from slot {slotIndex + 1}.";
            }

            RebuildSelectionsFromSlots();
        }

        private void InitializeSlotsFromSelections()
        {
            Array.Clear(_playerItemSlots, 0, MaxSlots);
            Array.Clear(_attachmentSlots, 0, MaxSlots);

            int playerUnlocked = UnlockTracker.GetPlayerItemSlotCount();
            int p = 0;
            foreach (var item in _selectedPlayerItems)
            {
                if (p >= Math.Min(playerUnlocked, MaxSlots))
                {
                    break;
                }

                _playerItemSlots[p++] = item;
            }

            int attachmentUnlocked = UnlockTracker.GetBlasterSlotCount();
            int a = 0;
            foreach (var attachment in _selectedAttachments)
            {
                if (a >= Math.Min(attachmentUnlocked, MaxSlots))
                {
                    break;
                }

                _attachmentSlots[a++] = attachment;
            }

            RebuildSelectionsFromSlots();
        }

        private void RebuildSelectionsFromSlots()
        {
            _selectedPlayerItems.Clear();
            _selectedAttachments.Clear();

            int playerUnlocked = UnlockTracker.GetPlayerItemSlotCount();
            for (int i = 0; i < Math.Min(playerUnlocked, MaxSlots); i++)
            {
                if (_playerItemSlots[i].HasValue)
                {
                    _selectedPlayerItems.Add(_playerItemSlots[i]!.Value);
                }
            }

            int attachmentUnlocked = UnlockTracker.GetBlasterSlotCount();
            for (int i = 0; i < Math.Min(attachmentUnlocked, MaxSlots); i++)
            {
                if (_attachmentSlots[i].HasValue)
                {
                    _selectedAttachments.Add(_attachmentSlots[i]!.Value);
                }
            }
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
            RebuildSelectionsFromSlots();

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

            DrawCategoryTabs(spriteBatch, viewport);
            DrawSlotStack(spriteBatch, viewport);
            DrawActionButtons(spriteBatch, viewport);
            DrawListPanel(spriteBatch, viewport);

            Color statusColor = (_pendingPlayerItem.HasValue || _pendingAttachment.HasValue) ? Color.Yellow : Color.LightGray;
            DrawCentered(spriteBatch, _statusMessage, viewport.Height - 55, statusColor);
        }

        private void DrawCategoryTabs(SpriteBatch spriteBatch, Viewport viewport)
        {
            DrawTab(spriteBatch, GetPlayerTabRect(viewport), "Player Items", _currentMode == SelectionMode.PlayerItems);
            DrawTab(spriteBatch, GetAttachmentTabRect(viewport), "Attachments", _currentMode == SelectionMode.Attachments);
        }

        private void DrawTab(SpriteBatch spriteBatch, Rectangle rect, string text, bool active)
        {
            Color fill = active ? new Color(245, 248, 255, 55) : new Color(95, 105, 125, 45);
            Color border = active ? Color.White : Color.Gray;
            spriteBatch.Draw(_pixel, rect, fill);
            DrawRectOutline(spriteBatch, rect, border, 2);

            Vector2 size = _font!.MeasureString(text) * UiScale;
            Vector2 pos = new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);
            DrawUiString(spriteBatch, text, pos, active ? Color.Navy : Color.Black);
        }

        private void DrawSlotStack(SpriteBatch spriteBatch, Viewport viewport)
        {
            int unlockedCount = _currentMode == SelectionMode.Attachments
                ? UnlockTracker.GetBlasterSlotCount()
                : UnlockTracker.GetPlayerItemSlotCount();

            for (int i = 0; i < MaxSlots; i++)
            {
                Rectangle rect = GetSlotRect(viewport, i);
                bool unlocked = i < unlockedCount;
                bool pending = (_currentMode == SelectionMode.Attachments && _pendingAttachment.HasValue)
                    || (_currentMode == SelectionMode.PlayerItems && _pendingPlayerItem.HasValue);

                Color fill = unlocked ? new Color(240, 244, 255, 72) : new Color(60, 60, 60, 140);
                Color border = unlocked ? Color.White : Color.Gray;
                if (pending && unlocked)
                {
                    border = Color.Yellow;
                }

                Rectangle clearRect = GetClearSlotButtonRect(viewport, i);
                Color clearFill = unlocked ? new Color(120, 30, 35, 170) : new Color(55, 55, 55, 160);
                Color clearBorder = unlocked ? Color.OrangeRed : Color.Gray;
                spriteBatch.Draw(_pixel, clearRect, clearFill);
                DrawRectOutline(spriteBatch, clearRect, clearBorder, 2);
                Vector2 clearSize = _font!.MeasureString("X") * UiScale;
                Vector2 clearPos = new Vector2(
                    clearRect.X + (clearRect.Width - clearSize.X) / 2f,
                    clearRect.Y + (clearRect.Height - clearSize.Y) / 2f);
                DrawUiString(spriteBatch, "X", clearPos, Color.White);

                spriteBatch.Draw(_pixel, rect, fill);
                DrawRectOutline(spriteBatch, rect, border, 2);

                string slotLabel = unlocked ? $"Slot {i + 1}" : $"Slot {i + 1} (Locked)";
                string value = "Empty";
                if (_currentMode == SelectionMode.Attachments)
                {
                    if (_attachmentSlots[i].HasValue)
                    {
                        value = GetAttachmentName(_attachmentSlots[i]!.Value);
                    }
                }
                else
                {
                    if (_playerItemSlots[i].HasValue)
                    {
                        value = GetPlayerItemName(_playerItemSlots[i]!.Value);
                    }
                }

                Color textColor = unlocked ? Color.Navy : Color.LightGray;
                DrawUiString(spriteBatch, slotLabel, new Vector2(rect.X + 14, rect.Y + 10), textColor);
                DrawUiString(spriteBatch, value, new Vector2(rect.X + 14, rect.Y + 42), textColor);
            }
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

        private void DrawListPanel(SpriteBatch spriteBatch, Viewport viewport)
        {
            Rectangle area = GetListAreaRect(viewport);
            spriteBatch.Draw(_pixel, area, new Color(0, 0, 0, 90));
            DrawRectOutline(spriteBatch, area, Color.White, 2);

            DrawUiString(spriteBatch, "Mouse wheel / Up-Down to scroll", new Vector2(area.X, area.Y - 46), Color.LightGray);

            int rowHeight = 52;
            int visibleRows = GetVisibleListRows(viewport);
            int scroll = _currentMode == SelectionMode.Attachments ? _attachmentListScroll : _playerListScroll;
            int itemCount = _currentMode == SelectionMode.Attachments ? _attachmentNames.Length : _playerItemNames.Length;

            for (int r = 0; r < visibleRows; r++)
            {
                int index = scroll + r;
                if (index >= itemCount)
                {
                    break;
                }

                Rectangle rowRect = new Rectangle(area.X + 8, area.Y + 10 + r * rowHeight, area.Width - 16, rowHeight - 6);
                bool unlocked;
                bool selected;
                string name;

                if (_currentMode == SelectionMode.Attachments)
                {
                    unlocked = UnlockTracker.IsAttachmentUnlocked(_attachmentTypes[index]);
                    selected = _attachmentSlots.Any(x => x == _attachmentTypes[index]);
                    name = _attachmentNames[index];
                }
                else
                {
                    unlocked = UnlockTracker.IsItemUnlocked(_playerItemTypes[index]);
                    selected = _playerItemSlots.Any(x => x == _playerItemTypes[index]);
                    name = _playerItemNames[index];
                }

                spriteBatch.Draw(_pixel, rowRect, unlocked ? new Color(236, 241, 255, 78) : new Color(75, 75, 75, 140));
                DrawRectOutline(spriteBatch, rowRect, unlocked ? (selected ? Color.LimeGreen : Color.White) : Color.Gray, 1);

                string prefix = unlocked ? (selected ? "[EQUIPPED] " : "") : "[LOCKED] ";
                DrawUiString(spriteBatch, prefix + name, new Vector2(rowRect.X + 10, rowRect.Y + 12), unlocked ? Color.Navy : Color.LightGray);
            }
        }

        // -----------------------------------------------------------------------
        // Utilities
        // -----------------------------------------------------------------------
        private Rectangle GetPlayerTabRect(Viewport viewport)
            => new Rectangle(viewport.Width / 2 - 310, 90, 280, 42);

        private Rectangle GetAttachmentTabRect(Viewport viewport)
            => new Rectangle(viewport.Width / 2 + 30, 90, 280, 42);

        private Rectangle GetSlotRect(Viewport viewport, int slotIndex)
            => new Rectangle(viewport.Width / 2 - 280, 145 + slotIndex * 96, 560, 84);

        private Rectangle GetClearSlotButtonRect(Viewport viewport, int slotIndex)
        {
            Rectangle slotRect = GetSlotRect(viewport, slotIndex);
            return new Rectangle(slotRect.X - 56, slotRect.Y + 18, 44, 48);
        }

        private Rectangle GetListAreaRect(Viewport viewport)
            => new Rectangle(70, viewport.Height - 300, viewport.Width - 140, 240);

        private Rectangle GetStartButtonRect(Viewport viewport)
            => new Rectangle(viewport.Width - 315, 135, 270, 72);

        private Rectangle GetBackButtonRect(Viewport viewport)
            => new Rectangle(viewport.Width - 315, 220, 270, 72);

        private int GetVisibleListRows(Viewport viewport)
            => Math.Max(1, (GetListAreaRect(viewport).Height - 20) / 52);

        private string GetPlayerItemName(ItemType item)
        {
            int index = Array.IndexOf(_playerItemTypes, item);
            return index >= 0 ? _playerItemNames[index] : item.ToString();
        }

        private string GetAttachmentName(BlasterAttachmentType attachment)
        {
            int index = Array.IndexOf(_attachmentTypes, attachment);
            return index >= 0 ? _attachmentNames[index] : attachment.ToString();
        }

        private void DrawCentered(SpriteBatch spriteBatch, string text, int y, Color color)
        {
            var sz = _font!.MeasureString(text) * UiScale;
            var pos = new Vector2(_graphics.GraphicsDevice.Viewport.Width / 2f - sz.X / 2f, y);
            DrawUiString(spriteBatch, text, pos, color);
        }

        private void DrawUiString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(_font!, text, position, color, 0f, Vector2.Zero, UiScale, SpriteEffects.None, 0f);
        }

        private const float UiScale = 0.9f;

        private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(_pixel!, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel!, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel!, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(_pixel!, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private bool IsKeyPressed(Keys key)
            => keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);

        // -----------------------------------------------------------------------
        // Save / load
        // -----------------------------------------------------------------------
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
