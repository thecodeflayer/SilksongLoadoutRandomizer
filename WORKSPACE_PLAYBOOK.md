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
   - Uses a hardcoded `BossPatterns` mapping of precise `SceneName` -> `GameObjectName` derived from the Silksong DebugMod.
   - Automatically supports duo-bosses (e.g. Moss Mothers) by scanning `FindObjectsOfType<HealthManager>()` to ensure all boss entities in the scene are at `hp <= 0` before triggering the randomization.

5. **`LoadoutManager.cs`**
   - Helper class to extract valid Crest and Tool names from the native game managers at startup.

## Development Workflow
- **Framework:** .NET Standard 2.1
- **Build:** `dotnet build`
- **Post-Build:** `SilksongLoadoutRandomizer.csproj` contains a post-build event that automatically copies the compiled DLL to `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins`.
- **Distribution:** Packaged manually for Thunderstore (`SilksongLoadoutRandomizer.dll`, `README.md`, `icon.png`, `manifest.json`).

## Current Status
- **v1.0.0 Released.**
- Tested successfully for safe saving, hotkeys, and boss kill hooks.
- **Future Ideas (v1.1+):** Area transition triggers, integration with Godhome-style Pantheons, custom visual effects.
