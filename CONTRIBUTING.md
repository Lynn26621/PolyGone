# Contributing to PolyGone

Thank you for being part of the PolyGone project! This guide explains how to set up your development environment, how we handle issues and pull requests, and the code style conventions we follow.

> PolyGone is a school project created by students at **York County School of Technology**. Contributing is limited to current team members and approved YCST students. See [LICENSE](LICENSE) for terms.

---

## Table of Contents

1. [Development Setup](#development-setup)
2. [Working with Issues](#working-with-issues)
3. [Branching Strategy](#branching-strategy)
4. [Working with Pull Requests](#working-with-pull-requests)
5. [Code Style Guidelines](#code-style-guidelines)
6. [Level Design with Tiled](#level-design-with-tiled)
7. [Code of Conduct](#code-of-conduct)

---

## Development Setup

### Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0+ | Build and run the game |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) | 17.x+ | Recommended IDE (Community is free) |
| [Tiled Map Editor](https://www.mapeditor.org/) | Latest | Create and edit levels |
| [Git](https://git-scm.com/) | Any | Version control |

### 1. Fork the Repository

1. Navigate to [https://github.com/jesse26603/PolyGone](https://github.com/jesse26603/PolyGone).
2. Click the **Fork** button (top right of the page).
3. This creates a copy of the repository under your GitHub account.

### 2. Clone Your Fork

```bash
git clone https://github.com/<your-username>/PolyGone.git
cd PolyGone
```

Add the upstream remote so you can pull in future updates from the main repository:

```bash
git remote add upstream https://github.com/jesse26603/PolyGone.git
```

### 3. Open in Visual Studio

1. Launch **Visual Studio 2022**.
2. Go to **File → Open → Project/Solution**.
3. Navigate to your cloned folder and open `PolyGone.sln`.
4. Visual Studio will automatically restore NuGet packages (MonoGame is included this way).

### 4. Build and Run

From the command line:
```bash
dotnet build PolyGone.sln
dotnet run --project src/PolyGone/PolyGone.csproj
```

Or in Visual Studio: press **F5** to build and run with the debugger attached.

### 5. Keeping Your Fork Up to Date

Before starting any new work, sync your fork with the upstream `DEV` branch:

```bash
git fetch upstream
git checkout DEV
git merge upstream/DEV
git push origin DEV
```

---

## Working with Issues

Issues are used to track bugs, feature requests, and tasks. Good issues keep the project organised and make it easy to prioritise work.

### Creating an Issue

When creating a new issue:

- **Choose a clear, descriptive title.** Example: `Player clips through semi-solid tiles when dashing downward`
- **Fill out the appropriate template fields** if a template is provided.
- **For bugs**, include:
  - Steps to reproduce
  - Expected behaviour
  - Actual behaviour
  - Screenshots or error messages (if applicable)
  - Your OS, .NET version, and MonoGame version
- **For features/enhancements**, include:
  - A description of the proposed change and why it would improve the game
  - Any implementation ideas you have in mind
- **Assign labels** from the available set (e.g. `Bug`, `Enhancement`, `Tweak`, `High Priority`, `Low Priority`, `Good First Issue`, `Documentation`).
- **Link to a Milestone** if the issue fits into a planned release (e.g. `v0.2.0-Alpha`).

### Handling Issues

- When you begin work on an issue, **assign it to yourself** to signal that it is being worked on and avoid duplicate effort.
- Add a comment briefly explaining your approach before you start large changes.
- If you discover the issue is more complex than expected or requires splitting into sub-issues, comment and tag a maintainer.
- When an issue is resolved by a merged PR, close it with a reference comment (e.g. `Resolved by #123`), or use the GitHub `Closes #<number>` keyword in your PR description so GitHub closes it automatically.

### Issue Examples from This Project

| # | Title | What Made It Good |
|---|---|---|
| [#162](https://github.com/jesse26603/PolyGone/issues/162) | Pausing issues | Clear expected vs. actual, easy to reproduce |
| [#154](https://github.com/jesse26603/PolyGone/issues/154) | Holding Jump Not Working | Structured expected/reality format |
| [#150](https://github.com/jesse26603/PolyGone/issues/150) | Accidental Click Registration in Menus | Describes the exact problematic scenario |
| [#152](https://github.com/jesse26603/PolyGone/issues/152) | Sample Levels | Feature request with clear scope and priority |

---

## Branching Strategy

We use two primary persistent branches:

| Branch | Purpose |
|---|---|
| `main` | Stable release branch. Only merged into from `DEV` at release time. |
| `DEV` | Active development branch. All feature branches are created from and merged back into `DEV`. |

### Creating a Feature Branch

Always branch off `DEV`:

```bash
git checkout DEV
git pull upstream DEV
git checkout -b feature/your-feature-name
```

Branch naming conventions:
- `feature/<short-description>` — new functionality
- `fix/<short-description>` — bug fixes
- `tweak/<short-description>` — small adjustments or refactors
- `docs/<short-description>` — documentation-only changes

---

## Working with Pull Requests

Pull requests (PRs) are how code changes get reviewed and merged into `DEV`.

### Before Opening a PR

1. **Ensure your branch is up to date** with `DEV`:
   ```bash
   git fetch upstream
   git rebase upstream/DEV
   ```
2. **Build without errors**: `dotnet build PolyGone.sln`
3. **Test your changes** — play through affected areas of the game to verify nothing is broken.
4. **Fix compiler warnings** introduced by your changes. Do not push code with new warnings.

### Opening a PR

1. Push your branch to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
2. Go to the repository on GitHub and click **Compare & pull request**.
3. Set the **base branch to `DEV`** (never target `main` directly unless instructed by a maintainer).
4. Fill in the PR description:
   - **What** the change does
   - **Why** it was needed (link the related issue with `Closes #<number>` or `Relates to #<number>`)
   - **Screenshots** of any visual changes
   - Any **known limitations or follow-up work**

### PR Review Process

- At least **one maintainer review** is required before merging.
- Reviewers will leave comments — address each comment with either a code change or a written explanation.
- Once approved and all checks pass, a maintainer will merge the PR.
- **Do not merge your own PR** unless you are the sole maintainer and it has been reviewed by another team member.
- PRs that introduce new warnings, fail to build, or break existing functionality will be sent back for revisions.

### PR Examples from This Project

| # | Title | What Made It Good |
|---|---|---|
| [#166](https://github.com/jesse26603/PolyGone/pull/166) | Add Help screen to main menu | Clear description, tabbed UI, linked issue |
| [#147](https://github.com/jesse26603/PolyGone/pull/147) | Add configurable keyboard/gamepad control remapping | Thorough — covered all input paths |
| [#136](https://github.com/jesse26603/PolyGone/pull/136) | Eliminate build warning backlog | Focused, no behavioural changes mixed in |

---

## Code Style Guidelines

### C# Conventions

- Follow [Microsoft's C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- **PascalCase** for classes, methods, properties, and public fields.
- **camelCase** with a leading underscore (`_camelCase`) for private fields.
- **camelCase** for local variables and parameters.
- Use **meaningful, descriptive names**.
- Add **XML documentation comments** (`/// <summary>`) on all public types and members.
- Use `var` when the type is obvious from the right-hand side.

### File Organisation

- Each class lives in **its own file**, named after the class.
- Place files in the appropriate directory:
  - `Core/` — Core game systems: game loop, input, audio, scene management
  - `Entities/` — Game objects: player, enemies, projectiles
  - `Graphics/` — Visual components: sprites, camera
  - `Items/` — Item and inventory systems
  - `Maps/` — Map loading and tile collision
  - `Scenes/` — All game screens
  - `Weapons/` — Weapon systems and attachments
- All game code uses the `PolyGone` namespace.

### Formatting

- **4 spaces** for indentation (no tabs).
- Opening braces on a **new line** (Allman style).
- One blank line between methods.
- Line length: keep to **120 characters** where reasonable.
- Avoid trailing whitespace.

### Comments

- Write comments for **non-obvious logic**, not for things that are already clear from the code.
- Keep comments up to date when code changes.
- Use `// -----------------------------------------------------------------------` section dividers (as seen in existing scenes) to separate logical regions in long files.
- Avoid commenting out dead code — remove it and rely on Git history.

### Warnings

- **Do not introduce new compiler warnings.** All code pushed to `DEV` should build cleanly.
- Enable nullable reference types (already configured in the project) and resolve all `CS8600`/`CS8602`/`CS8603` nullability warnings.

---

## Level Design with Tiled

Levels are created with [Tiled Map Editor](https://www.mapeditor.org/).

1. Open or create a `.tmx` file in `TiledAssets/TiledMaps/`.
2. Use the tilesets in `TiledAssets/TiledSets/`. Do not create new tilesets without discussing with a maintainer.
3. Layers:
   - `Foreground` — visible tiles the player interacts with (collision is read from tile type)
   - `Background` — decorative tiles behind the player (no collision)
4. After finishing a map, **export it as JSON** and save it to `src/PolyGone/Content/Maps/`.
5. Register the new `.json` file in `src/PolyGone/Content/Content.mgcb` if it is a new file.
6. To add the level as a playable level, register it in the level sequence in the relevant game code (e.g. `DevMenuScene.cs` or level-door configuration).

---

## Code of Conduct

- Be **respectful and constructive** in all issue comments, PR reviews, and team discussions.
- Focus on **the work**, not the person — critique code, not the contributor.
- Assume good intent. We are all learning.
- Keep discussions on topic and related to the project.
- Show empathy to newer contributors — remember everyone starts somewhere.

---

Questions? Open an issue with the `question` label or reach out to a maintainer directly.

Thank you for contributing to PolyGone!
