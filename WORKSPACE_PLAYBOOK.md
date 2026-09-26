# Silksong Loadout Randomizer - AI Workspace Playbook

This document serves as context for AI assistants to quickly understand the project structure, design decisions, and current state to seamlessly resume development in future sessions.

## Project Overview
A BepInEx 5 plugin for *Hollow Knight: Silksong* (Unity 6000.0.50) that randomizes the player's equipped Crest and Tools (Red, Blue, Yellow, and Silk Skills) based on configurable triggers.

## Key Systems & Architecture

1. **`Plugin.cs` (Core & Configuration)**
   - Defines BepInEx `ConfigEntry` fields for toggling triggers (Bench, Boss Kill, Hotkey) and item pools.
   - Uses `BepInEx.Configuration.KeyboardShortcut` for the F10 hotkey trigger.
   - Monitors `PlayerData.instance.atBench` in `Update()` to trigger randomization on bench rests.
   - Spawns an in-game startup notification via `Randomizer.ShowNotification` when the mod is loaded.

2. **`Randomizer.cs` (Loadout Generation)**
   - Dynamically pools unlocked tools via `ToolItemManager.GetAllTools()`.
   - Bypasses unlock logic if configured by the user via configs.
   - Excludes hardcoded temporary quest items like `"NeedlePhial"` from the generation pool.
   - Equips new tools via `ToolItemManager.SetEquippedTools()` and visually refreshes the game UI via `ToolItemManager.SendEquippedChangedEvent(true)`.

3. **`SaveGamePatch.cs` (Save File Protection)**
   - **Critical System:** To prevent the randomizer from permanently unlocking items the player doesn't legitimately own, this Harmony patch intercepts `GameManager.SaveGame()`.
   - Scans `PlayerData.instance.CurrentCrestID` and `ToolEquips` to scrub unowned items back to a valid state.
   - Wraps the internal `Action<bool> callback` parameter so the chaotic loadout is seamlessly restored to memory *after* the async save thread finishes writing to `user2.dat`.

4. **`BossKillPatch.cs` (Boss Defeat Trigger)**
   - Hooks into `HealthManager.Die` (specifically the `Nullable<float>` override).
   - Uses a hardcoded `BossPatterns` mapping of precise `SceneName` -> `GameObjectName` derived primarily from the Silksong DebugMod's `EnemyHandle.cs`.
   - **Note:** The initial DebugMod list was incomplete and missing bosses such as the **Father of the Flame** (Scene: `wisp02`, GameObject: `WispPyreEffigy`, tracked in `PlayerData` as `defeatedWispPyreEffigy`). Future AI should be aware that `EnemyHandle.cs` may not be a comprehensive source of truth for all bosses in the game.
   - Automatically supports duo-bosses (e.g. Moss Mothers) by scanning `FindObjectsOfType<HealthManager>()` to ensure all boss entities in the scene are at `hp <= 0` before triggering the randomization.

5. **`LoadoutManager.cs`**
   - Helper class to extract valid Crest and Tool names from the native game managers at startup.

## Technical Constraints & Data Sourcing for Future AI
When attempting to extract internal names, scenes, or GameObject data for *Hollow Knight: Silksong*, future AI assistants must note the following:
- **Compressed Asset Bundles:** Silksong uses Unity Addressables with LZ4 compression (`StreamingAssets\aa\StandaloneWindows64\*.bundle`). Running text searches (`Select-String`, `grep`, or Python regex) across the binary game directory will almost always fail to find Scene Names, GameObject Names, or FSM variables.
- **Reflection Limitations:** Writing C# reflection scripts to query the game's `Assembly-CSharp.dll` will only reveal class structures and global variables (like `PlayerData` flags). It cannot extract `ScriptableObject` instances (like `BossScene`) or GameObject hierarchy data because these are instantiated by the engine from the bundles at runtime.
- **Proper Data Sourcing:** Do **not** waste time writing custom asset extractors or reflection scripts to find level data. If you need a specific Scene Name, GameObject Name, or FSM string, directly ask the User to provide it using their local instance of **ILSpy**, **Unity Explorer**, or through community data searches.
- **Utilize the Debug Mod:** The adjacent workspace project `C:\workspace\Silksong.DebugMod` is an incredibly valuable resource. It contains a wealth of human-readable information, hardcoded reference lists (like `EnemyHandle.cs`), and proven examples of how to hook into and manipulate native game systems (tools, UI, FSMs, save states). Always explore this codebase for clues on game mechanics before attempting raw decompilation.
- **Log Location:** BepInEx and Unity logs are written to `$env:USERPROFILE\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\Player.log`. Use this for all runtime debugging.

## Development Workflow
- **Framework:** .NET Standard 2.1
- **Build:** `dotnet build`
- **Post-Build:** `SilksongLoadoutRandomizer.csproj` contains a post-build event that automatically copies the compiled DLL to `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins`.
- **Distribution:** Packaged manually for Thunderstore (`SilksongLoadoutRandomizer.dll`, `README.md`, `icon.png`, `manifest.json`).

## Current Status
- **v1.0.3 Released.** (Hotfixes applied: Fixed bug where forced-unlocked crests and GiveAllSlots were failing to populate all slots due to missing save data initialization).
- Tested successfully for safe saving, hotkeys, and boss kill hooks.
- **Future Ideas (v1.1+):** Area transition triggers, integration with Godhome-style Pantheons, custom visual effects.
