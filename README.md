# PolyGone

A 2D action-platformer developed by students at **York County School of Technology** using MonoGame and C#.

## About the Game

PolyGone is a tile-based 2D platformer set inside a perilous tower. Players fight through a series of handcrafted levels filled with enemies, hazardous terrain, and challenging platforming. Unlock new abilities and weapon attachments as you ascend the tower, and survive long enough to reach the top.

The game is currently in **v0.2.0-Alpha** and is actively in development.

## How to Play

### 1. Log In
PolyGone uses **Formbar** for authentication. When you first launch the game, you will be taken to the login screen. Click **Login with Formbar** — your web browser will open to the Formbar login page at your school's Formbar server. After completing the login there, the game will automatically continue.

> Your session is saved so you only need to log in once per session expiry.

### 2. Purchase the Game
After logging in, you will be prompted to pay **200 Digipogs** to unlock all levels. Digipogs are the currency used on the Formbar platform. Once purchased, you will not need to pay again.

### 3. Enter the Hub
After purchasing, you land in the **Main Menu**. Click **Play** to enter the **Hub** level — a safe starting area where you can access your inventory and choose which levels to enter.

From the Hub, walk up to a **Level Door** and press **W** (or **DPad Up / Left Stick Up**) to enter that level.

### 4. Gameplay
- Move through the level, defeat enemies, and navigate platforming hazards.
- Reach the **Goal** at the end of a level to win and unlock the next one.
- If you die, you can restart the level, return to the Hub, or go to the Main Menu.

### Controls (defaults — all remappable)

| Action | Keyboard | Gamepad |
|---|---|---|
| Move Left / Right | A / D or Arrow Keys | Left Stick / DPad |
| Jump | Space | A (South) |
| Dash | Left Shift | B (East) |
| Shoot | Left Mouse Button / F | Right Trigger |
| Aim | Mouse | Right Stick |
| Interact / Enter Door | W | DPad Up / Left Stick Up |
| Pause | Escape | Start |
| Drop Through Platform | Left Ctrl | Left Bumper |

Controls can be remapped in **Options → Controls**.

## Features

- **Physics-based movement** — coyote time, wall cling, wall jump, and dash abilities
- **Unlockable abilities** — wall jump and dash unlocked through progression
- **Enemy variety** — Walker, Frog, Turret, Berserk, and Factory enemy types
- **Weapon attachments** — modify your blaster (e.g. piercing shots)
- **Item system** — equip up to 2 items before entering a level
- **Tile-based levels** — multiple collision types: solid, semi-solid (one-way), slippery, bouncy, and damage tiles
- **Parallax backgrounds** — visually rich environments
- **Audio** — music and sound effects throughout
- **Fully remappable controls** — keyboard, mouse, and gamepad supported
- **In-game Help** — tab-based help screen covering player, enemies, menus, and collision types

## Setting Up Development

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 or later | Required to build and run |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) | 17.x or later | Recommended IDE (Community edition is free) |
| [MonoGame](https://www.monogame.net/) | 3.8 or later | Installed via NuGet, no manual install needed |
| [Tiled Map Editor](https://www.mapeditor.org/) | Latest | For creating and editing levels |

### 1. Fork and Clone the Repository

1. Go to [https://github.com/jesse26603/PolyGone](https://github.com/jesse26603/PolyGone).
2. Click **Fork** (top right) to create your own copy under your GitHub account.
3. Clone **your fork** locally:
   ```bash
   git clone https://github.com/<your-username>/PolyGone.git
   cd PolyGone
   ```
4. Add the upstream remote so you can pull in future changes:
   ```bash
   git remote add upstream https://github.com/jesse26603/PolyGone.git
   ```

### 2. Open in Visual Studio

1. Open **Visual Studio 2022**.
2. Select **File → Open → Project/Solution**.
3. Navigate to your cloned folder and open `PolyGone.sln`.
4. Visual Studio will restore NuGet packages (including MonoGame) automatically.

### 3. Build and Run

Build from the command line:
```bash
dotnet build PolyGone.sln
```

Run the game:
```bash
dotnet run --project src/PolyGone/PolyGone.csproj
```

Or press **F5** in Visual Studio to build and run with the debugger.

### 4. Setting Up Tiled

Levels are created with [Tiled Map Editor](https://www.mapeditor.org/).

1. Download and install Tiled.
2. Open `TiledAssets/TiledMaps/` to find the existing `.tmx` map files.
3. Tilesets (`.tsx`) are stored in `TiledAssets/TiledSets/`.
4. When saving a map, export it to JSON format and place it in `src/PolyGone/Content/Maps/`.
5. Register the new map in the MonoGame Content Pipeline (`Content.mgcb`) if adding a brand-new map file.

> See [CONTRIBUTING.md](CONTRIBUTING.md) for a detailed guide on how to contribute level designs and code changes.

## Project Structure

```
PolyGone/
├── src/
│   └── PolyGone/               # Main game project
│       ├── Core/               # Core systems (game loop, input, audio, scenes)
│       ├── Entities/           # Game objects (player, enemies, projectiles)
│       ├── Graphics/           # Visual components (sprites, camera)
│       ├── Items/              # Item and inventory systems
│       ├── Maps/               # Map loading and tile collision logic
│       ├── Scenes/             # All game screens (menu, game, pause, etc.)
│       ├── Weapons/            # Weapon systems and attachments
│       └── Content/            # Compiled game assets (textures, audio, maps)
├── TiledAssets/                # Tiled map editor source files
│   ├── TiledMaps/              # .tmx map files
│   ├── TiledSets/              # .tsx tileset files
│   └── TileMaps/               # Tilemap images
└── PolyGone.sln                # Visual Studio solution file
```

## Technologies Used

- **C# / .NET 8.0** — Primary programming language and runtime
- **MonoGame 3.8** — Cross-platform game framework
- **Tiled Map Editor** — Level design tool
- **Formbar** — Authentication and Digipog payment platform

## Contributing

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for full guidelines on reporting bugs, requesting features, and submitting pull requests.

## Credits

See [CREDITS.md](CREDITS.md) for a full list of everyone who contributed to the project.

## License

PolyGone is proprietary software. See [LICENSE](LICENSE) for full terms. Unauthorized use, copying, or distribution is prohibited except as permitted for students of York County School of Technology.
