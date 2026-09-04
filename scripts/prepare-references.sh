#!/usr/bin/env bash
set -euo pipefail

archive="${1:-$HOME/Downloads/Pharaoh A New Era.zip}"
project_root="$(cd "$(dirname "$0")/.." && pwd)"
reference_dir="$project_root/.game-reference"

mkdir -p "$reference_dir"
for file in \
  "Pharaoh A New Era/BepInEx/core/0Harmony.dll" \
  "Pharaoh A New Era/BepInEx/core/BepInEx.dll" \
  "Pharaoh A New Era/Pharaoh_Data/Managed/Assembly-CSharp.dll" \
  "Pharaoh A New Era/Pharaoh_Data/Managed/Unity.TextMeshPro.dll" \
  "Pharaoh A New Era/Pharaoh_Data/Managed/UnityEngine.dll" \
  "Pharaoh A New Era/Pharaoh_Data/Managed/UnityEngine.CoreModule.dll" \
  "Pharaoh A New Era/Pharaoh_Data/Managed/UnityEngine.UI.dll" \
  "Pharaoh A New Era/Pharaoh_Data/Managed/UnityEngine.UIModule.dll"
do
  unzip -j -o "$archive" "$file" -d "$reference_dir"
done
