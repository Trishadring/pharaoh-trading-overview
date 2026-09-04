# Trading Overview

Trading Overview is an independent BepInEx plugin for **Pharaoh: A New Era**. It adds current-year trade progress and the combined annual capacity of open, active trade routes to each Commerce Overseer row.

```text
              Trade Volume (Year / Max)
Stored        Exp 600 / 4K   Imp 300 / 2.5K   Status
```

The first number is the amount traded during the current game year. The second is the total annual amount supported by currently open routes. The game resets the completed amounts at the beginning of each year.

## Compatibility

- Game version: `2023_11_21a_patch1.5_steam`
- Mod loader: BepInEx 5.4.22
- Works independently and does not require `PANEOverseerFixes.dll`
- Designed to load alongside the Bug Fixes and Enhancements Package

## Install

1. Install BepInEx 5 or the Bug Fixes and Enhancements Package.
2. Remove any older `TradingOverview.dll` and `TradingOverview.Logic.dll` files from `BepInEx/plugins`.
3. Extract the release ZIP into the game directory containing `Pharaoh.exe`.

## Build

Game and BepInEx assemblies are required for compilation but are not included in this repository.

```bash
./scripts/prepare-references.sh "$HOME/Downloads/Pharaoh A New Era.zip"
dotnet build -c Release
dotnet test -c Release
./scripts/package.sh
```

## Design

The plugin follows the existing overseer mod's integration pattern: a Harmony postfix on `CommerceRow.UpdateData`, with private serialized fields supplied by Harmony. It clones the stock quantity's TextMesh Pro component so fonts and styling remain consistent, and it never replaces the game's Stored value or controls.
