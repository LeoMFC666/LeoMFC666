#!/usr/bin/env bash
# Gera .fusioncore/interop com o Il2CppInterop 1.5.2 do próprio FusionCore (mesmo passo que o BepInEx Fusion
# faz no aparelho), a partir de AmongUs.GameLibs.Android + bibliotecas base do Unity 2022.3.62 (UnityEngine.Modules).
# Não depende de unity.bepinex.dev (bloqueado em alguns ambientes de build).
set -euo pipefail
cd "$(dirname "$0")/.."
F=.fusioncore
[ -f "$F/interop/Assembly-CSharp.dll" ] && { echo "Interop já gerado"; exit 0; }
dotnet restore build/GameLibs >/dev/null
OUT=$(dotnet msbuild build/GameLibs -t:PrintPaths -nologo)
GAMELIBS=$(echo "$OUT" | sed -n 's/.*GAMELIBS=//p' | tr -d '\r')
UNITYLIBS=$(echo "$OUT" | sed -n 's/.*UNITYLIBS=//p' | tr -d '\r')
rm -rf "$F/dummy" "$F/unity-libs" && mkdir -p "$F/dummy" "$F/unity-libs"
cp "$GAMELIBS"/build/*.dll "$F/dummy/"
find "$UNITYLIBS/lib" -name '*.dll' -exec cp {} "$F/unity-libs/" \;
dotnet build build/InteropGen -c Release -o "$F/interopgen" -nologo -v q
dotnet "$F/interopgen/InteropGen.dll" "$F/dummy" "$F/unity-libs" "$F/interop"
