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
        private enum SelectionMode { PlayerItems, Attachments, Confirm }
        private SelectionMode _currentMode = SelectionMode.PlayerItems;
        private int _playerItemCursor = 0;
        private int _attachmentCursor = 0;
        private int _confirmCursor = 0; // 0 = Start Game, 1 = Back

        private readonly List<ItemType> _selectedPlayerItems;
        private readonly List<BlasterAttachmentType> _selectedAttachments;

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
        }

        public void Load()
        {
            if (_font == null)
            {
                try { _font = _content.Load<SpriteFont>("Fonts/PauseMenu"); }
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

            // Ctrl = skip screen, keep current selections
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

            HandleMouseNavigation();

            switch (_currentMode)
            {
                case SelectionMode.PlayerItems:  UpdatePlayerItemSelection();  break;
                case SelectionMode.Attachments:  UpdateAttachmentSelection();  break;
                case SelectionMode.Confirm:      UpdateConfirmSelection();     break;
            }

            previousKeyboardState = keyboardState;
        }

        // -----------------------------------------------------------------------
        // Mouse navigation
        // -----------------------------------------------------------------------
        private void HandleMouseNavigation()
        {
            if (_font == null) return;

            var viewport = _graphics.GraphicsDevice.Viewport;
            int leftX  = 60;
            int rightX = viewport.Width / 2 + 20;
            int listY  = 200;

            // --- Player items column ---
            for (int i = 0; i < _playerItemNames.Length; i++)
            {
                var text   = GetPlayerItemPrefix(i) + _playerItemNames[i];
                var sz     = _font.MeasureString(text);
                var bounds = new Rectangle(leftX, listY + i * 40, (int)sz.X, (int)sz.Y);

                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _currentMode      = SelectionMode.PlayerItems;
                    _playerItemCursor = i;

                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        TogglePlayerItem(_playerItemTypes[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            // --- Attachments column ---
            for (int i = 0; i < _attachmentNames.Length; i++)
            {
                var text   = GetAttachmentPrefix(i) + _attachmentNames[i];
                var sz     = _font.MeasureString(text);
                var bounds = new Rectangle(rightX, listY + i * 40, (int)sz.X, (int)sz.Y);

                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _currentMode      = SelectionMode.Attachments;
                    _attachmentCursor = i;

                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        ToggleAttachment(_attachmentTypes[i]);
                        InputManager.ConsumeClick();
                    }
                }
            }

            // --- Confirm section ---
            int confirmStartY    = viewport.Height - 150;
            string[] confirmOpts = { "Start Game", "Back" };
            for (int i = 0; i < confirmOpts.Length; i++)
            {
                var prefix = (i == _confirmCursor && _currentMode == SelectionMode.Confirm) ? "> " : "  ";
                var text   = prefix + confirmOpts[i];
                var sz     = _font.MeasureString(text);
                var pos    = new Vector2(viewport.Width / 2f - sz.X / 2f, confirmStartY + i * 40);
                var bounds = new Rectangle((int)pos.X, (int)pos.Y, (int)sz.X, (int)sz.Y);

                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _currentMode   = SelectionMode.Confirm;
                    _confirmCursor = i;

                    if (InputManager.IsLeftMouseButtonClicked())
                    {
                        if (_confirmCursor == 0) StartGame();
                        else _sceneManager.PopScene(this);
                        InputManager.ConsumeClick();
                    }
                }
            }
        }

        // -----------------------------------------------------------------------
        // Keyboard navigation per mode
        // -----------------------------------------------------------------------
        private void UpdatePlayerItemSelection()
        {
            if (IsKeyPressed(Keys.Up))
                _playerItemCursor = (_playerItemCursor - 1 + _playerItemNames.Length) % _playerItemNames.Length;
            if (IsKeyPressed(Keys.Down))
                _playerItemCursor = (_playerItemCursor + 1) % _playerItemNames.Length;

            if (IsKeyPressed(Keys.Enter) || IsKeyPressed(Keys.Space))
                TogglePlayerItem(_playerItemTypes[_playerItemCursor]);

            if (IsKeyPressed(Keys.Right) || (IsKeyPressed(Keys.Tab) && !keyboardState.IsKeyDown(Keys.LeftShift)))
                _currentMode = SelectionMode.Attachments;
        }

        private void UpdateAttachmentSelection()
        {
            if (IsKeyPressed(Keys.Up))
                _attachmentCursor = (_attachmentCursor - 1 + _attachmentNames.Length) % _attachmentNames.Length;
            if (IsKeyPressed(Keys.Down))
                _attachmentCursor = (_attachmentCursor + 1) % _attachmentNames.Length;

            if (IsKeyPressed(Keys.Enter) || IsKeyPressed(Keys.Space))
                ToggleAttachment(_attachmentTypes[_attachmentCursor]);

            if (IsKeyPressed(Keys.Left) || (IsKeyPressed(Keys.Tab) && keyboardState.IsKeyDown(Keys.LeftShift)))
                _currentMode = SelectionMode.PlayerItems;
            if (IsKeyPressed(Keys.Right) || (IsKeyPressed(Keys.Tab) && !keyboardState.IsKeyDown(Keys.LeftShift)))
                _currentMode = SelectionMode.Confirm;
        }

        private void UpdateConfirmSelection()
        {
            if (IsKeyPressed(Keys.Up) || IsKeyPressed(Keys.Down))
                _confirmCursor = (_confirmCursor + 1) % 2;

            if (IsKeyPressed(Keys.Enter))
            {
                if (_confirmCursor == 0) StartGame();
                else _sceneManager.PopScene(this);
            }

            if (IsKeyPressed(Keys.Left) || (IsKeyPressed(Keys.Tab) && keyboardState.IsKeyDown(Keys.LeftShift)))
                _currentMode = SelectionMode.Attachments;
        }

        // -----------------------------------------------------------------------
        // Toggle helpers (enforce slot limits)
        // -----------------------------------------------------------------------
        private void TogglePlayerItem(ItemType item)
        {
            if (!UnlockTracker.IsItemUnlocked(item)) return;

            if (_selectedPlayerItems.Contains(item))
            {
                _selectedPlayerItems.Remove(item);
            }
            else
            {
#if DEBUG
                _selectedPlayerItems.Add(item);
#else
                int maxSlots = UnlockTracker.GetPlayerItemSlotCount();
                if (_selectedPlayerItems.Count < maxSlots)
                    _selectedPlayerItems.Add(item);
#endif
            }
        }

        private void ToggleAttachment(BlasterAttachmentType attachment)
        {
            if (!UnlockTracker.IsAttachmentUnlocked(attachment)) return;

            if (_selectedAttachments.Contains(attachment))
            {
                _selectedAttachments.Remove(attachment);
            }
            else
            {
#if DEBUG
                _selectedAttachments.Add(attachment);
#else
                int maxSlots = UnlockTracker.GetBlasterSlotCount();
                if (_selectedAttachments.Count < maxSlots)
                    _selectedAttachments.Add(attachment);
#endif
            }
        }

        // -----------------------------------------------------------------------
        // Prefix helpers
        // -----------------------------------------------------------------------
        private string GetPlayerItemPrefix(int index)
        {
            bool unlocked = UnlockTracker.IsItemUnlocked(_playerItemTypes[index]);
            if (!unlocked) return "[LOCKED] ";
            return _selectedPlayerItems.Contains(_playerItemTypes[index]) ? "[X] " : "[ ] ";
        }

        private string GetAttachmentPrefix(int index)
        {
            bool unlocked = UnlockTracker.IsAttachmentUnlocked(_attachmentTypes[index]);
            if (!unlocked) return "[LOCKED] ";
            return _selectedAttachments.Contains(_attachmentTypes[index]) ? "[X] " : "[ ] ";
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
                spriteBatch.GraphicsDevice.Viewport.Height), Color.DarkBlue);

            if (_font == null) return;

            var viewport = spriteBatch.GraphicsDevice.Viewport;

            // Title
            DrawCentered(spriteBatch, "Select Your Loadout", 50, Color.White);

            // Instructions
#if DEBUG
            string hint = "[DEV] Unlimited slots | Press Ctrl to skip";
#else
            string hint = "Fill your slots | Press Ctrl to skip";
#endif
            DrawCentered(spriteBatch, hint, 90, Color.Gray);

            DrawPlayerItemsSection(spriteBatch, viewport);
            DrawAttachmentsSection(spriteBatch, viewport);
            DrawConfirmSection(spriteBatch, viewport);
        }

        private void DrawPlayerItemsSection(SpriteBatch spriteBatch, Viewport viewport)
        {
            int x = 60, y = 200;

            int playerSlots = UnlockTracker.GetPlayerItemSlotCount();
#if DEBUG
            string sectionTitle = "Player Items (DEV - all):";
#else
            string sectionTitle = $"Player Items ({_selectedPlayerItems.Count}/{playerSlots} slots):";
#endif
            Color titleColor = _currentMode == SelectionMode.PlayerItems ? Color.Yellow : Color.White;
            spriteBatch.DrawString(_font, sectionTitle, new Vector2(x, y - 40), titleColor);

            for (int i = 0; i < _playerItemNames.Length; i++)
            {
                bool unlocked = UnlockTracker.IsItemUnlocked(_playerItemTypes[i]);
                bool selected = unlocked && _selectedPlayerItems.Contains(_playerItemTypes[i]);
                bool cursor   = i == _playerItemCursor && _currentMode == SelectionMode.PlayerItems;

                Color color;
                string prefix;
                if (!unlocked)
                {
                    color  = cursor ? Color.Orange : Color.DarkGray;
                    prefix = "[LOCKED] ";
                }
                else
                {
                    color  = cursor ? Color.Yellow : (selected ? Color.LimeGreen : Color.White);
                    prefix = selected ? "[X] " : "[ ] ";
                }

                spriteBatch.DrawString(_font, prefix + _playerItemNames[i], new Vector2(x, y + i * 40), color);
            }

            // Description below list
            if (_currentMode == SelectionMode.PlayerItems)
            {
                int descY = y + _playerItemNames.Length * 40 + 10;
                bool cursorUnlocked = UnlockTracker.IsItemUnlocked(_playerItemTypes[_playerItemCursor]);
                string desc = cursorUnlocked
                    ? _playerItemDescriptions[_playerItemCursor]
                    : (UnlockTracker.GetUnlockHint(_playerItemTypes[_playerItemCursor]) ?? "");
                if (desc.Length > 0)
                    spriteBatch.DrawString(_font, desc, new Vector2(x, descY),
                        cursorUnlocked ? Color.LightGray : Color.Orange);
            }
        }

        private void DrawAttachmentsSection(SpriteBatch spriteBatch, Viewport viewport)
        {
            int x = viewport.Width / 2 + 20, y = 200;

            int blasterSlots = UnlockTracker.GetBlasterSlotCount();
#if DEBUG
            string sectionTitle = "Blaster Attachments (DEV - all):";
#else
            string sectionTitle = $"Blaster Attachments ({_selectedAttachments.Count}/{blasterSlots} slots):";
#endif
            Color titleColor = _currentMode == SelectionMode.Attachments ? Color.Yellow : Color.White;
            spriteBatch.DrawString(_font, sectionTitle, new Vector2(x, y - 40), titleColor);

            for (int i = 0; i < _attachmentNames.Length; i++)
            {
                bool unlocked = UnlockTracker.IsAttachmentUnlocked(_attachmentTypes[i]);
                bool selected = unlocked && _selectedAttachments.Contains(_attachmentTypes[i]);
                bool cursor   = i == _attachmentCursor && _currentMode == SelectionMode.Attachments;

                Color color;
                string prefix;
                if (!unlocked)
                {
                    color  = cursor ? Color.Orange : Color.DarkGray;
                    prefix = "[LOCKED] ";
                }
                else
                {
                    color  = cursor ? Color.Yellow : (selected ? Color.LimeGreen : Color.White);
                    prefix = selected ? "[X] " : "[ ] ";
                }

                spriteBatch.DrawString(_font, prefix + _attachmentNames[i], new Vector2(x, y + i * 40), color);
            }

            // Description below list
            if (_currentMode == SelectionMode.Attachments)
            {
                int descY = y + _attachmentNames.Length * 40 + 10;
                bool cursorUnlocked = UnlockTracker.IsAttachmentUnlocked(_attachmentTypes[_attachmentCursor]);
                string desc = cursorUnlocked
                    ? _attachmentDescriptions[_attachmentCursor]
                    : (UnlockTracker.GetAttachmentUnlockHint(_attachmentTypes[_attachmentCursor]) ?? "");
                if (desc.Length > 0)
                    spriteBatch.DrawString(_font, desc, new Vector2(x, descY),
                        cursorUnlocked ? Color.LightGray : Color.Orange);
            }
        }

        private void DrawConfirmSection(SpriteBatch spriteBatch, Viewport viewport)
        {
            int startY = viewport.Height - 150;

            Color sectionColor  = _currentMode == SelectionMode.Confirm ? Color.Yellow : Color.White;
            string sectionTitle = _currentMode == SelectionMode.Confirm ? "> Ready?" : "  Ready?";
            DrawCentered(spriteBatch, sectionTitle, startY - 40, sectionColor);

            string[] opts = { "Start Game", "Back" };
            for (int i = 0; i < opts.Length; i++)
            {
                bool isCursor = i == _confirmCursor && _currentMode == SelectionMode.Confirm;
                var text = (isCursor ? "> " : "  ") + opts[i];
                DrawCentered(spriteBatch, text, startY + i * 40, isCursor ? Color.Yellow : Color.White);
            }
        }

        // -----------------------------------------------------------------------
        // Utilities
        // -----------------------------------------------------------------------
        private void DrawCentered(SpriteBatch spriteBatch, string text, int y, Color color)
        {
            var sz  = _font!.MeasureString(text);
            var pos = new Vector2(_graphics.GraphicsDevice.Viewport.Width / 2f - sz.X / 2f, y);
            spriteBatch.DrawString(_font, text, pos, color);
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
                    Directory.CreateDirectory(dir);

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
                if (!File.Exists(_loadoutSavePath)) return;

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
                            _lastSelectedPlayerItems.Add((ItemType)val);
                    }
                }

                if (root.TryGetProperty("Attachments", out var attachEl))
                {
                    _lastSelectedAttachments.Clear();
                    foreach (var att in attachEl.EnumerateArray())
                    {
                        int val = att.GetInt32();
                        if (Enum.IsDefined(typeof(BlasterAttachmentType), val))
                            _lastSelectedAttachments.Add((BlasterAttachmentType)val);
                    }
                }
            }
            catch { }
        }
    }
}
