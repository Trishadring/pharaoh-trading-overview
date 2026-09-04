#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
package_root="$project_root/artifacts/TradingOverview"

rm -rf "$package_root"
mkdir -p "$package_root/BepInEx/plugins/TradingOverview"
cp "$project_root/src/TradingOverview/bin/Release/TradingOverview.dll" "$package_root/BepInEx/plugins/TradingOverview/"

cd "$package_root"
zip -r "../TradingOverview-1.1.0.zip" BepInEx
