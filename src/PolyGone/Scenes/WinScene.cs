using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PolyGone.Core;

namespace PolyGone;

public class WinScene : IScene
{
    private readonly ContentManager contentManager;
    private readonly SceneManager sceneManager;
    private readonly AudioManager audioManager;
    private readonly GraphicsDeviceManager graphics;
    private readonly string currentLevel;
    private readonly List<ItemType> selectedItems;
    private readonly List<BlasterAttachmentType> selectedAttachments;
    private SpriteFont? font;
    private Texture2D? pixel;
    private readonly string[] options;
    private int selectedIndex;

    public WinScene(ContentManager contentManager, SceneManager sceneManager, AudioManager audioManager, GraphicsDeviceManager graphics, string currentLevel = "TestLevel", List<ItemType>? selectedItems = null, List<BlasterAttachmentType>? selectedAttachments = null)
    {
        this.contentManager = contentManager;
        this.sceneManager = sceneManager;
        this.audioManager = audioManager;
        this.graphics = graphics;
        this.currentLevel = currentLevel;
        this.selectedItems = selectedItems ?? new List<ItemType>();
        this.selectedAttachments = selectedAttachments ?? new List<BlasterAttachmentType>();

        // Default options for the win screen
        options = new string[] { "Hub", "Main Menu" }; // "Next Level" option will not be used

        selectedIndex = 0;

        // Record this level as completed so locked items can be unlocked
        UnlockTracker.RecordLevelComplete(currentLevel);
    }

    public void Load()
    {
        if (font == null)
        {
            font = contentManager.Load<SpriteFont>("Fonts/PauseMenu");
        }
        audioManager.PlayAudio("null", false, "goalAchievedSong", true);
    }

    // Clean up resources to prevent memory leaks
    public void Unload()
    {
        pixel?.Dispose();
        pixel = null;
    }

    public void Update(GameTime gameTime)
    {

        // Mouse navigation
        if (font != null)
        {
            var viewport = graphics.GraphicsDevice.Viewport;
            var startY = viewport.Height / 2f + 50f; // Below the "Level Cleared!" text

            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var textSize = font.MeasureString(option);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);
                var bounds = new Rectangle((int)position.X, (int)position.Y, (int)textSize.X, (int)textSize.Y);

                if (bounds.Contains(InputManager.GetMousePosition()))
                {
                    selectedIndex = i;
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
            selectedIndex = (selectedIndex - 1 + options.Length) % options.Length;
        }

        if (InputManager.MenuDown())
        {
            selectedIndex = (selectedIndex + 1) % options.Length;
        }

        if (InputManager.MenuNonPointerConfirm())
        {
            ExecuteSelection();
        }

    }

    private void ExecuteSelection()
    {
        string selectedOption = options[selectedIndex];

        if (selectedOption == "Hub")
        {
            sceneManager.PopScene(this); // Remove WinScene
            sceneManager.PopScene(sceneManager.GetCurrentScene()); // Remove old GameScene
            sceneManager.AddScene(new GameScene(contentManager, sceneManager, audioManager, graphics, "Hub", selectedItems, selectedAttachments));
        }
        else if (selectedOption == "Main Menu")
        {
            sceneManager.PopScene(this); // Remove WinScene

            // Keep popping until we reach MenuScene
            while (sceneManager.GetCurrentScene() != null && sceneManager.GetCurrentScene() is not MenuScene)
            {
                sceneManager.PopScene(sceneManager.GetCurrentScene());
            }

            InputManager.ResetClickCooldown();
            audioManager.PlayAudio("null", false, "menuSong", true); //Plays menu song, will not play song otherwise
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (pixel == null)
        {
            pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
        }

        // Draw gray background
        spriteBatch.Draw(pixel, new Rectangle(0, 0, spriteBatch.GraphicsDevice.Viewport.Width, spriteBatch.GraphicsDevice.Viewport.Height), Color.Gray);

        if (font != null)
        {
            var viewport = spriteBatch.GraphicsDevice.Viewport;

            // Draw "Level Cleared!" text
            string winText = "Level Cleared!";
            var winTextSize = font.MeasureString(winText);
            var winTextPosition = new Vector2(viewport.Width / 2f - winTextSize.X / 2f, viewport.Height / 2f - 100f);
            spriteBatch.DrawString(font, winText, winTextPosition, Color.Gold);

            // Draw menu options
            var startY = viewport.Height / 2f + 50f;

            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var color = i == selectedIndex ? Color.Yellow : Color.White;
                var textSize = font.MeasureString(option);
                var position = new Vector2(viewport.Width / 2f - textSize.X / 2f, startY + i * 40f);

                spriteBatch.DrawString(font, option, position, color);
            }
        }
    }
}
