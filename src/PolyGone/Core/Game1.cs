using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PolyGone.Core;

namespace PolyGone;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SceneManager sceneManager;
    private AudioManager audioManager = null!;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        sceneManager = new();

        // Apply saved display settings (defaults to 1280x720 windowed on first run)
        DisplaySettings.Load();
        if (DisplaySettings.IsFullScreen)
        {
            var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = dm.Width;
            _graphics.PreferredBackBufferHeight = dm.Height;
            _graphics.IsFullScreen = true;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = DisplaySettings.WindowedWidth;
            _graphics.PreferredBackBufferHeight = DisplaySettings.WindowedHeight;
            _graphics.IsFullScreen = false;
        }
        _graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
        // Hook up text input so scenes can accept keyboard text entry
        Window.TextInput += InputManager.OnTextInput;

        // Give DisplaySettings a reference so it can center the window after resolution changes
        DisplaySettings.Window = Window;

        // Center on the primary display on startup (no-op in fullscreen)
        if (!DisplaySettings.IsFullScreen)
        {
            DisplaySettings.CenterWindowOnPrimaryDisplay(
                DisplaySettings.WindowedWidth, DisplaySettings.WindowedHeight);
        }

        base.Initialize();
    }


    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        audioManager = new AudioManager(Content); // Creates instance of AudioManager

        // Load purchase tracker before any scene that might need it
        PurchaseTracker.Load();
        // Load item unlock data
        UnlockTracker.Load();

        sceneManager.AddScene(new GameScene(Content, sceneManager, audioManager, _graphics));
        sceneManager.AddScene(new MenuScene(Content, sceneManager, audioManager, _graphics));

        // Try to restore a saved session (skips the login screen on subsequent launches)
        bool sessionRestored = FormbarSession.TryLoadSession();

        if (!sessionRestored)
        {
            // First launch or session expired: require login
            sceneManager.AddScene(new FormbarLoginScene(Content, sceneManager, audioManager, _graphics));
        }
        else if (!PurchaseTracker.HasPurchased(FormbarSession.UserId, FormbarSession.AllLevelsKey))
        {
            // Logged in but hasn't paid yet: require payment before accessing the menu
            sceneManager.AddScene(new PaymentScene(Content, sceneManager, audioManager, _graphics));
        }
        // else: logged in and paid – menu is immediately accessible
    }

    protected override void Update(GameTime gameTime)
    {
        InputManager.Update(gameTime);

        if (InputManager.PauseMenuOpen())
        {
            if (sceneManager.GetCurrentScene() is PauseScene)
            {
                sceneManager.PopScene(sceneManager.GetCurrentScene());
            }
            else if (sceneManager.GetCurrentScene() is GameScene gameScene)
            {
                sceneManager.AddScene(new PauseScene(Content, sceneManager, audioManager, _graphics, gameScene));
                // Reset click cooldown when opening pause menu
                InputManager.ResetClickCooldown();
            }
        }
        // TODO: Add your update logic here
        sceneManager.GetCurrentScene().Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.White);

        // TODO: Add your drawing code here
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        sceneManager.GetCurrentScene().Draw(_spriteBatch);

        _spriteBatch.End();


        base.Draw(gameTime);
    }
}
