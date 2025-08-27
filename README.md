<p align="center">
  <img src="Logo.jpeg" alt="Mod Menu Crew logo" width="640"/>
</p>

# Mod Menu Crew

[![.NET](https://img.shields.io/badge/.NET-6.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/) [![BepInEx IL2CPP](https://img.shields.io/badge/BepInEx-IL2CPP%206%20(be.735)-00B4CC?logo=csharp&logoColor=white)](https://builds.bepinex.dev/projects/bepinex_be) [![Among Us](https://img.shields.io/badge/Among__Us-2025.4.x-ff4757?logo=steam&logoColor=white)](https://store.steampowered.com/app/945360/Among_Us/) [![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](#compatibility-matrix) [![License](https://img.shields.io/badge/License-All%20Rights%20Reserved-red)](#safety-ethics-and-legal-notes)

Make your lobbies unforgettable. Mod Menu Crew is a BepInEx IL2CPP mod for Among Us that gives hosts and creators precision role control, quality‑of‑life toggles, and a toolbox of scoped cheats for private sessions, testing, and content creation.

— Built for .NET 6, powered by Harmony.

<br/>

## Quick Links
- [Feature Overview](#feature-overview)
- [How to Use (In‑Game)](#how-to-use-in-game)
- [Installation](#installation)
- [Build from Source](#build-from-source)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [FAQ](#faq)
- [Compatibility](#compatibility-matrix)
- [Contributing](#contributing)
- [Safety & Legal](#safety-ethics-and-legal-notes)

<br/>

## Feature Overview
- **Role Control (Core)**
  - Pre‑assign roles in lobby (e.g., Impostor, Shapeshifter, Engineer, Scientist, Tracker)
  - Live role switching during matches (host best‑effort)
  - Local fix for role desyncs when Unity hiccups
- **Cheat Manager (Core, host‑friendly)**
  - Quick actions: complete all tasks, close meeting, reveal impostors
  - Mass actions: kill all, kill crew only, kill impostors only
  - Role‑specific boosts: endless shapeshift/vents/tracking/battery and no‑cooldown toggles
- **QoL & Movement (Core)**
  - Teleport to players / teleport with cursor
  - Allow venting for all roles (toggle)
  - Vision multiplier
- **Lobby/Host Tools**
  - Smart lobby insights and optional countdown/auto‑extend (config‑driven)
- **UI & Effects (Optional)**
  - Subtle HUD enhancement for the version text with light glitch/CRT flourishes
  - Tiered scheduler with cooldowns to avoid visual noise
- **Soft Integrity**
  - Guardrails around certain actions; if altered, the action is skipped (the mod doesn’t disable itself)

<br/>

## How to Use (In‑Game)
- Open the mod menu (draggable window) and use these tabs/sections:
  - **Player Selection**: pick a player; use the Role dropdown to assign or switch; in lobby, use Pre‑Assign
  - **General Cheats**: quick actions (complete tasks, close meeting, reveal impostors), mass actions
  - **Role‑Specific Cheats**: toggles for Engineer/Shapeshifter/Scientist/Tracker perks
  - **QoL Toggles**: allow global venting, teleport with cursor, adjust vision multiplier
- Changes apply immediately where possible. Some actions require host privileges to propagate.

> Tip: Pre‑assign roles in the lobby for deterministic starts; switch live only if you understand the match impact.

<br/>

## Installation
1) Install BepInEx IL2CPP for Among Us.
   - Recommended: `BepInEx 6.0.0-be.735` and `BepInEx.IL2CPP 2.1.0-rc.1` (tested).
2) Build from source (below) or download the DLL from Releases.
3) Place `ModMenuCrew.dll` in:
   - Windows: `Among Us\BepInEx\plugins\ModMenuCrew\ModMenuCrew.dll`
4) Launch the game. Look for: `Plugin com.crewmod.oficial version x.y.z is loading.`

If BepInEx console doesn’t appear, revisit your IL2CPP install and file locations.

<br/>

## Build from Source
Requirements:
- .NET SDK 6.x
- Windows with Visual Studio or `dotnet` CLI
- IL2CPP headers/game libs (resolved via the provided `.csproj`)

CLI:
```powershell
dotnet restore
dotnet build -c Release
```

Cake (optional):
```powershell
dotnet tool restore
dotnet cake build.cake
```

Output: `ModMenuCrew\bin\Release\net6.0\ModMenuCrew.dll`

<br/>

## Configuration
Settings are stored via BepInEx (plugin ID: `com.crewmod.oficial`).

Common toggles include:
- In‑lobby countdown display and host auto‑extend threshold
- Streamer mode and lobby code masking

Path: `BepInEx\config\com.crewmod.oficial.cfg` (created on first run)

Advanced:
- Effect scheduler cooldown and optional bias for rarer sequences (cosmetic)
- Programmatic controls: `VersionShowerFx.EnableFnaf3Bias(bool)`, `ConfigureHeavyCooldown(float)`, `ConfigureIdleDelays(float,float)`

<br/>

## Architecture
- [`ModMenuCrewPlugin.cs`](ModMenuCrew/ModMenuCrewPlugin.cs) — BepInEx entry, Harmony bootstrap, config init
- [`PlayerPickMenu.cs`](ModMenuCrew/PlayerPickMenu.cs) — Player list UI, pre‑assignment, dropdowns
- [`ImpostorForcer.cs`](ModMenuCrew/ImpostorForcer.cs) — Role logic, pre‑game management, local fixes
- [`CheatManager.cs`](ModMenuCrew/CheatManager.cs) — Cheat UI tabs and toggles
- [`GameCheats.cs`](ModMenuCrew/GameCheats.cs) — Actions: complete tasks, close meeting, reveal, kill groups
- [`RoleCheats.cs`](ModMenuCrew/RoleCheats.cs) — Continuous role buffs (no cooldowns, endless timers)
- [`VersionShowerPatch.cs`](ModMenuCrew/VersionShowerPatch.cs) + `VersionShowerFx` — HUD overlay (optional cosmetic)
- [`LobbyHarmonyPatches.cs`](ModMenuCrew/LobbyHarmonyPatches.cs) — Lobby detection and signatures
- [`GuiStyles.cs`](ModMenuCrew/GuiStyles.cs), [`DragWindow.cs`](ModMenuCrew/DragWindow.cs), [`MenuSystem.cs`](ModMenuCrew/MenuSystem.cs) — Shared UI styles and composition

Design favors clear separation: UI invokes capability services (`GameCheats`, `RoleCheats`, `ImpostorForcer`); Harmony patches stay thin and localized.

<br/>

## FAQ
**Does changing the integrity hash disable the entire mod?**
No. Only specific guarded actions are skipped if altered. The rest keeps working.

**Public lobbies?**
No. Use in private/testing environments with consent.

**How do I make the game less flashy?**
Cosmetic effects are lightweight and cooled down. If desired, disable bias/adjust delays via code or contribute config toggles.

**BepInEx doesn’t load the DLL.**
Use BepInEx 6 (IL2CPP), place the DLL under `BepInEx\plugins`, and use a supported Among Us version.

**Supported game versions?**
Tested on `2025.4.15` (aka `16.1.0`) (Steam). Newer versions may need updates.

<br/>

## Compatibility Matrix
- Among Us: 2025.4.15 (16.1.0) — OK
- BepInEx: 6.0.0‑be.735 (IL2CPP) — Recommended/Tested ([Bleeding Edge builds](https://builds.bepinex.dev/projects/bepinex_be))


Other platforms (Switch/Console/Mobile) are not targets of this project.

<br/>

## Contributing
Issues and PRs welcome:
- Keep PRs focused and well‑scoped
- Describe user‑facing impact clearly
- Favor readability over cleverness
- Keep Harmony hooks precise

Dev tips:
- Optional automation via `build.cake`
- Use `.WrapToIl2Cpp()` for coroutines from IL2CPP contexts
- Share styles via `GuiStyles`

<br/>

## Safety, Ethics, and Legal Notes
- For educational and private lobby use only. Respect developers and communities.
- Do not use in competitive/ranked/public environments.
- No license file is distributed; by default, all rights reserved unless a `LICENSE` is added. Forks/redistribution should seek permission.

If you are an IP holder and have concerns, please open an issue.

<br/>

### Credits
- HarmonyX, BepInEx, IL2CPP Interop — foundational tech
- Among Us developers — the canvas we build upon

“Stay sus, but keep it classy.”
