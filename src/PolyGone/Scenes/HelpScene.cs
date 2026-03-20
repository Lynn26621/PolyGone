using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PolyGone
{
    internal class HelpScene : IScene
    {
        private Texture2D _pixel;
        private SpriteFont _font;
        private KeyboardState keyboardState;
        private KeyboardState previousKeyboardState;
        private readonly ContentManager _content;
        private readonly SceneManager _sceneManager;
        private readonly GraphicsDeviceManager _graphics;
        private int _selectedIndex;

        public HelpScene(ContentManager content, SceneManager sceneManager, GraphicsDeviceManager graphics)
        {
            _pixel = null;
            _content = content;
            _sceneManager = sceneManager;
            _graphics = graphics;
            previousKeyboardState = Keyboard.GetState();
            _selectedIndex = 0;
        }

        public void Load()
        {
            if (_font == null)
            {
                try
                {
                    _font = _content.Load<SpriteFont>("Fonts/HelpMenu");
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
            if (keyboardState.IsKeyDown(Keys.Escape) && previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                _sceneManager.AddScene(new MenuScene(_content, _sceneManager, _graphics));
            }
            previousKeyboardState = keyboardState;
        }
        public void Draw(SpriteBatch spriteBatch)
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
            // Draw background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height), Color.Gray);
            if (_font != null)
            {
                var viewport = _graphics.GraphicsDevice.Viewport;
                var helpText = "Use WASD or Arrow Keys to move, and press Space to jump." +
                    "\nLeft Click to use your weapon" +
                    "\n" +
                    "\nBefore entering a level, select an item and your weapon of choice. " +
                    "\nTo beat a level, touch the gray box at the end." +
                    "\n" +
                    "\nThere are 2 types of enemies, roamers and turrets." +
                    "\nRoamers are red squares and deal damage when you collide with them." +
                    "\nTurrets are stationary enemies that shoot bullets at you." +
                    "\n" +
                    "\nGood Luck!" +
                    "\n" +
                    "\n" +
                    "\nPress Escape to return to the main menu.";
                var textSize = _font.MeasureString(helpText);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, viewport.Height / 2f - textSize.Y / 2f);
                spriteBatch.DrawString(_font, helpText, position, Color.White);
            }
        }
    }
}
