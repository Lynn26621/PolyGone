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

namespace PolyGone;

internal class PauseScene : IScene
{
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private readonly ContentManager _content;
    private readonly SceneManager _sceneManager;
    private readonly AudioManager _audioManager;
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameScene _gameScene;
    private readonly string[] _options;
    private int _selectedIndex;
    private readonly bool _isHub;

    public PauseScene(ContentManager content, SceneManager sceneManager, AudioManager audioManager, GraphicsDeviceManager graphics, GameScene gameScene)
    {
        _content = content;
        _sceneManager = sceneManager;
        _audioManager = audioManager;
        _graphics = graphics;
        _gameScene = gameScene;
        _isHub = gameScene.GetLevelName() == "Hub";
        _options = _isHub
            ? new[] { "Continue", "Inventory", "Exit to Menu" }
            : new[] { "Continue", "Exit to Menu" };
        _selectedIndex = 0;
    }

    public void Load()
    {
        if (_font == null)
        {
            _font = _content.Load<SpriteFont>("Fonts/PauseMenu");
        }
    }

    public void Update(GameTime gameTime)
    {

        // Mouse navigation
        if (_font != null)
        {
            var viewport = _graphics.GraphicsDevice.Viewport;
            var startY = viewport.Height / 2f - (_options.Length * 40f) / 2f;

            for (var i = 0; i < _options.Length; i++)
            {
                var option = _options[i];
                var textSize = _font.MeasureString(option);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                var bounds = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);

                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    _selectedIndex = i;
                    if (InputManager.MenuConfirmMouseClick())
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

        if (InputManager.MenuNonPointerConfirm())
        {
            ExecuteSelection();
        }

    }

    private void ExecuteSelection()
    {
        if (_selectedIndex == 0)
        {
            // Continue
            _sceneManager.PopScene(this);
        }
        else if (_selectedIndex == 1 && _isHub)
        {
            // Inventory — only available in the hub
            _sceneManager.PopScene(this);
            _sceneManager.AddScene(new InventoryManagement(_content, _sceneManager, _audioManager, _graphics, "Hub"));
            InputManager.ResetClickCooldown();
        }
        else
        {
            // Exit to Menu — clear all and go to menu
            _sceneManager.PopScene(this);
            _sceneManager.PopScene(_gameScene);
            // Pop any remaining scenes to get to a clean menu
            _sceneManager.AddScene(new MenuScene(_content, _sceneManager, _audioManager, _graphics));
        }
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
            var startY = viewport.Height / 2f - (_options.Length * 40f) / 2f;

            for (var i = 0; i < _options.Length; i++)
            {
                var option = _options[i];
                var color = i == _selectedIndex ? Color.Yellow : Color.White;
                var textSize = _font.MeasureString(option);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);

                spriteBatch.DrawString(_font, option, position, color);
            }
        }
    }

}
