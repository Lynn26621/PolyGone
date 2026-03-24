using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PolyGone
{
    internal class LevelSelect : IScene
    {
        private Texture2D? _pixel;
        private SpriteFont? _font;
        private KeyboardState keyboardState;
        private KeyboardState previousKeyboardState;
        private GamePadState gamePadState;
        private GamePadState previousGamePadState;
        private readonly ContentManager _content;
        private readonly SceneManager _sceneManager;
        private readonly GraphicsDeviceManager _graphics;
        private readonly string[] _levelNames = { "Test Level 1", "Test Level 2", "Test Level 3", "Back to Menu" };
        private readonly string?[] _levelFiles = { "TestLevel", "TestLevel2", "TestLevel3", null };
        private int _selectedIndex;

        public LevelSelect(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
        {
            _pixel = null;
            _content = content;
            _sceneManager = sceneManager;
            _graphics = graphics;
            previousKeyboardState = Keyboard.GetState();
            previousGamePadState = GamePad.GetState(PlayerIndex.One);
            _selectedIndex = 0;
        }

        public void Load()
        {
            if (_font == null)
            {
                try
                {
                    _font = _content.Load<SpriteFont>("Fonts/PauseMenu");
                }
                catch
                {
                    // Font not available
                }
            }
        }

        public void Update(GameTime gameTime)
        {
            keyboardState = Keyboard.GetState();
            gamePadState = GamePad.GetState(PlayerIndex.One);

            // Mouse navigation
            if (_font != null)
            {
                var viewport = _graphics.GraphicsDevice.Viewport;
                var startY = viewport.Height / 2f - _levelNames.Length * 40f / 2f;

                for (var i = 0; i < _levelNames.Length; i++)
                {
                    var levelName = _levelNames[i];
                    var textSize = _font.MeasureString(levelName);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                    var bounds = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);

                    if (bounds.Contains(InputManager.GetMousePosition()))
                    {
                        _selectedIndex = i;

                        // Mouse click with InputManager — ignore locked levels
                        if (InputManager.IsLeftMouseButtonClicked())
                        {
                            string? lf = _levelFiles[i];
                            if (lf == null || UnlockTracker.IsLevelUnlocked(lf))
                            {
                                ExecuteSelection();
                            }
                            InputManager.ConsumeClick();
                        }
                    }
                }
            }

            // Keyboard navigation
            if (IsKeyPressed(Keys.Up) || IsButtonPressed(Buttons.DPadUp))
            {
                int next = (_selectedIndex - 1 + _levelNames.Length) % _levelNames.Length;
                // Skip locked level entries
                while (next != _selectedIndex)
                {
                    string? lf = _levelFiles[next];
                    if (lf == null || UnlockTracker.IsLevelUnlocked(lf))
                        break;
                    next = (next - 1 + _levelNames.Length) % _levelNames.Length;
                }
                _selectedIndex = next;
            }

            if (IsKeyPressed(Keys.Down) || IsButtonPressed(Buttons.DPadDown))
            {
                int next = (_selectedIndex + 1) % _levelNames.Length;
                // Skip locked level entries
                while (next != _selectedIndex)
                {
                    string? lf = _levelFiles[next];
                    if (lf == null || UnlockTracker.IsLevelUnlocked(lf))
                        break;
                    next = (next + 1) % _levelNames.Length;
                }
                _selectedIndex = next;
            }

            if (IsKeyPressed(Keys.Enter) || IsButtonPressed(Buttons.A))
            {
                string? lf = _levelFiles[_selectedIndex];
                if (lf == null || UnlockTracker.IsLevelUnlocked(lf))
                    ExecuteSelection();
            }

            if (InputManager.IsEscapeKeyPressed())
            {
                // Also allow Escape to go back
                _sceneManager.PopScene(this);
            }

            previousKeyboardState = keyboardState;
            previousGamePadState = gamePadState;
        }

        private void ExecuteSelection()
        {
            if (_selectedIndex == _levelNames.Length - 1)
            {
                // Back to Menu
                _sceneManager.PopScene(this);
            }
            else
            {
                string? levelFile = _levelFiles[_selectedIndex];
                if (levelFile == null)
                    return;

                // Require Formbar login before accessing any level
                if (!FormbarSession.IsLoggedIn)
                {
                    _sceneManager.AddScene(new FormbarLoginScene(_content, _sceneManager, _graphics));
                    return;
                }

                // If the player has already paid for all levels, go straight to loadout selection
                if (PurchaseTracker.HasPurchased(FormbarSession.UserId, FormbarSession.AllLevelsKey))
                {
                    _sceneManager.AddScene(new InventoryManagement(_content, _sceneManager, _graphics, levelFile));
                }
                else
                {
                    // Safety net: payment should have happened upfront, but if not, require it now
                    _sceneManager.AddScene(new PaymentScene(_content, _sceneManager, _graphics));
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

            // Draw background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height), Color.DarkSlateGray);

            if (_font != null)
            {
                var viewport = spriteBatch.GraphicsDevice.Viewport;

                // Draw title
                string title = "Select a Level";
                var titleSize = _font.MeasureString(title);
                var titlePos = new Vector2(viewport.Width / 2f - titleSize.X / 2f, 100);
                spriteBatch.DrawString(_font, title, titlePos, Color.White);

                // Draw level options
                var startY = viewport.Height / 2f - _levelNames.Length * 40f / 2f;
                string? hintText = null;

                for (var i = 0; i < _levelNames.Length; i++)
                {
                    string? lf = _levelFiles[i];
                    bool isLocked = lf != null && !UnlockTracker.IsLevelUnlocked(lf);
                    bool isCursor = i == _selectedIndex;

                    string displayName = isLocked ? "[LOCKED] " + _levelNames[i] : _levelNames[i];
                    Color color;
                    if (isLocked)
                        color = isCursor ? Color.Orange : Color.DarkGray;
                    else
                        color = isCursor ? Color.Yellow : Color.White;

                    var textSize = _font.MeasureString(displayName);
                    var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                    spriteBatch.DrawString(_font, displayName, position, color);

                    if (isCursor && isLocked)
                        hintText = UnlockTracker.GetLevelUnlockHint(lf!);
                }

                // Show unlock hint below the list when a locked level is highlighted
                if (hintText != null)
                {
                    var hintSize = _font.MeasureString(hintText);
                    var hintPos = new Vector2(viewport.Width / 2f - hintSize.X / 2f,
                        startY + _levelNames.Length * 40f + 10f);
                    spriteBatch.DrawString(_font, hintText, hintPos, Color.Orange);
                }
            }
        }

        private bool IsKeyPressed(Keys key)
        {
            return keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }
        private bool IsButtonPressed(Buttons button)
        {
            return gamePadState.IsButtonDown(button) && !previousGamePadState.IsButtonDown(button);
        }
    }
}
