#!/bin/bash
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
game_managed="${GAME_MANAGED:-$HOME/Library/Application Support/Steam/steamapps/common/Escape from Duckov/Duckov.app/Contents/Resources/Data/Managed}"
dotnet_cmd="${DOTNET:-dotnet}"
if [[ ! -f "$game_managed/Assembly-CSharp.dll" ]]; then
    echo "Game assemblies not found. Set GAME_MANAGED to your Mac game's Managed folder." >&2
    exit 1
fi
if ! command -v "$dotnet_cmd" >/dev/null 2>&1; then
    echo "Install the .NET 10 SDK, or set DOTNET to its dotnet executable." >&2
    exit 1
fi
"$dotnet_cmd" build "$repo_dir/src/DuckovPad/DuckovPad.csproj" -c Release "-p:GameManaged=$game_managed"
"$dotnet_cmd" run --project "$repo_dir/tests/ControllerChecks/ControllerChecks.csproj" -c Release
"$dotnet_cmd" run --project "$repo_dir/tests/NavigationChecks/NavigationChecks.csproj" -c Release "-p:GameManaged=$game_managed"
cp "$repo_dir/src/DuckovPad/bin/Release/DuckovPad.dll" "$repo_dir/dist/DuckovPad/DuckovPad.dll"
echo "Built and checked: $repo_dir/dist/DuckovPad/DuckovPad.dll"
