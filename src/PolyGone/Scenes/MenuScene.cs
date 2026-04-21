/* Main Menu
[] Level Select (sub-menu)
[] Options (sub-menu)
[] Exit Game
*/

/* Pause Menu
[] Continue
[] Exit to Menu
*/
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.IO;
using System.Text.Json;
using System.Linq;
using System;
using PolyGone.Core;
using System.Diagnostics;

namespace PolyGone
{
    internal class MenuScene : IScene
    {
        private Texture2D _pixel;
        private SpriteFont _font;
        private readonly ContentManager _content;
        private readonly SceneManager _sceneManager;
        private readonly AudioManager _audioManager;
        private readonly GraphicsDeviceManager _graphics;
        private readonly string[] _options = { "Play", "Options", "Log Out", "Exit to Desktop" };
        private int _selectedIndex;
        private bool _confirmingAction;
        private string? _confirmMessage;
        private Action? _confirmedAction;
        private int _confirmSelectedIndex; // 0 = Yes, 1 = No

        public MenuScene(ContentManager content, SceneManager sceneManager, AudioManager audioManager, GraphicsDeviceManager graphics)
        {
            _content = content;
            _sceneManager = sceneManager;
            _audioManager = audioManager;
            _graphics = graphics;
            _selectedIndex = 0;
        }

        public void Load()
        {
            if (_font == null)
            {
                // Only attempt to load the font if the compiled asset exists to avoid runtime exceptions
                var fontAssetPath = Path.Combine(_content.RootDirectory, "Fonts", "PauseMenu.xnb");
                if (File.Exists(fontAssetPath))
                {
                    _font = _content.Load<SpriteFont>("Fonts/PauseMenu");
                }
            }
            _audioManager.PlayAudio("null", false, "menuSong", true);
        }

        public void Update(GameTime gameTime)
        {

            if (_confirmingAction)
            {
                // Left/Right or A/D to switch between Yes and No
                if (InputManager.MenuLeft())
                    _confirmSelectedIndex = 0;
                if (InputManager.MenuRight())
                    _confirmSelectedIndex = 1;

                // Enter or Y to confirm
                if (InputManager.MenuConfirm())
                {
                    if (_confirmSelectedIndex == 0)
                        _confirmedAction?.Invoke();
                    else
                        _confirmingAction = false;
                }

                // Escape or N to cancel
                if (InputManager.MenuBack())
                    _confirmingAction = false;

                // Mouse support for confirmation buttons
                if (_font != null)
                {
                    var viewport = _graphics.GraphicsDevice.Viewport;
                    var centerX = viewport.Width / 2f;
                    var centerY = viewport.Height / 2f;
                    var yesSize = _font.MeasureString("Yes");
                    var noSize = _font.MeasureString("No");
                    var yesPos = new Vector2(centerX - 80f - yesSize.X / 2f, centerY + 20f);
                    var noPos = new Vector2(centerX + 80f - noSize.X / 2f, centerY + 20f);

                    var yesBounds = new Rectangle((int)yesPos.X, (int)yesPos.Y, (int)yesSize.X, (int)yesSize.Y);
                    var noBounds = new Rectangle((int)noPos.X, (int)noPos.Y, (int)noSize.X, (int)noSize.Y);

                    if (yesBounds.Contains(InputManager.GetMousePosition()))
                    {
                        _confirmSelectedIndex = 0;
                        if (InputManager.MenuConfirm())
                        {
                            _confirmedAction?.Invoke();
                            InputManager.ConsumeClick();
                        }
                    }
                    else if (noBounds.Contains(InputManager.GetMousePosition()))
                    {
                        _confirmSelectedIndex = 1;
                        if (InputManager.MenuConfirm())
                        {
                            _confirmingAction = false;
                            InputManager.ConsumeClick();
                        }
                    }
                }
                return;
            }

            // Mouse navigation
            if (_font != null)
            {
                var viewport = _graphics.GraphicsDevice.Viewport;
                var startY = viewport.Height / 2f - _options.Length * 40f / 2f;

                for (var i = 0; i < _options.Length; i++)
                {
                    var option = _options[i];
                    var textSize = _font.MeasureString(option);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                    var bounds = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);

                    if (bounds.Contains(InputManager.GetMousePosition()))
                    {
                        _selectedIndex = i;

                        // Mouse click with InputManager
                        if (InputManager.MenuConfirm())
                        {
                            ExecuteSelection();
                            InputManager.ConsumeClick();
                        }
                    }
                }
            }

            // Keyboard navigation
            if (InputManager.MenuUp())
            {
                _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;
            }

            if (InputManager.MenuDown())
            {
                _selectedIndex = (_selectedIndex + 1) % _options.Length;
            }

            if (InputManager.MenuConfirm())
            {
                ExecuteSelection();
            }
        }

        private void ExecuteSelection()
        {
            if (_selectedIndex == 0)
            {
                // If the player has already paid for all levels, go straight to loadout selection
                if (PurchaseTracker.HasPurchased(FormbarSession.UserId, FormbarSession.AllLevelsKey))
                {
                    _sceneManager.AddScene(new InventoryManagement(_content, _sceneManager, _audioManager, _graphics, "Hub"));
                }
                else
                {
                    // Safety net: payment should have happened upfront, but if not, require it now
                    _sceneManager.AddScene(new PaymentScene(_content, _sceneManager, _audioManager, _graphics));
                }
            }
            else if (_selectedIndex == 1)
            {
                // Options
                _sceneManager.AddScene(new OptionsScene(_content, _sceneManager, _audioManager, _graphics));
            }
            else if (_selectedIndex == 2)
            {
                // Log Out — ask for confirmation
                BeginConfirmation(
                    "Are you sure you want to log out?",
                    () =>
                    {
                        _confirmingAction = false;
                        FormbarSession.Clear();
                        _sceneManager.AddScene(new FormbarLoginScene(_content, _sceneManager, _audioManager, _graphics));
                    });
            }
            else if (_selectedIndex == 3)
            {
                // Exit to Desktop — ask for confirmation
                BeginConfirmation(
                    "Are you sure you want to exit?",
                    () => Environment.Exit(0));
            }
        }

        private void BeginConfirmation(string message, Action onConfirmed)
        {
            _confirmMessage = message;
            _confirmedAction = onConfirmed;
            _confirmSelectedIndex = 1; // default to "No"
            _confirmingAction = true;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }

            // Use the already-begun SpriteBatch from Game1.Draw
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height), Color.Gray);

            if (_font != null)
            {
                var viewport = spriteBatch.GraphicsDevice.Viewport;
                var startY = viewport.Height / 2f - _options.Length * 40f / 2f;

                for (var i = 0; i < _options.Length; i++)
                {
                    var option = _options[i];
                    var color = i == _selectedIndex ? Color.Yellow : Color.White;
                    var textSize = _font.MeasureString(option);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);

                    spriteBatch.DrawString(_font, option, position, color);
                }
            }

            // Confirmation overlay
            if (_confirmingAction && _font != null)
            {
                var viewport = spriteBatch.GraphicsDevice.Viewport;
                var overlayRect = new Rectangle(viewport.Width / 4, viewport.Height / 4,
                    viewport.Width / 2, viewport.Height / 2);

                // Dim box
                spriteBatch.Draw(_pixel, overlayRect, Color.Black * 0.85f);

                // Border
                var borderThickness = 2;
                spriteBatch.Draw(_pixel, new Rectangle(overlayRect.X, overlayRect.Y, overlayRect.Width, borderThickness), Color.White);
                spriteBatch.Draw(_pixel, new Rectangle(overlayRect.X, overlayRect.Bottom - borderThickness, overlayRect.Width, borderThickness), Color.White);
                spriteBatch.Draw(_pixel, new Rectangle(overlayRect.X, overlayRect.Y, borderThickness, overlayRect.Height), Color.White);
                spriteBatch.Draw(_pixel, new Rectangle(overlayRect.Right - borderThickness, overlayRect.Y, borderThickness, overlayRect.Height), Color.White);

                var centerX = viewport.Width / 2f;
                var centerY = viewport.Height / 2f;

                // Message
                var confirmMessage = _confirmMessage ?? string.Empty;
                var msgSize = _font.MeasureString(confirmMessage);
                spriteBatch.DrawString(_font, confirmMessage,
                    new Vector2(centerX - msgSize.X / 2f, centerY - msgSize.Y - 10f), Color.White);

                // Yes / No buttons
                var yesSize = _font.MeasureString("Yes");
                var noSize = _font.MeasureString("No");
                var yesPos = new Vector2(centerX - 80f - yesSize.X / 2f, centerY + 20f);
                var noPos = new Vector2(centerX + 80f - noSize.X / 2f, centerY + 20f);

                spriteBatch.DrawString(_font, "Yes", yesPos, _confirmSelectedIndex == 0 ? Color.Yellow : Color.White);
                spriteBatch.DrawString(_font, "No", noPos, _confirmSelectedIndex == 1 ? Color.Yellow : Color.White);
            }
        }

    }
}
