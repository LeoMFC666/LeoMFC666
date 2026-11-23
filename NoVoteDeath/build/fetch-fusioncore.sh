#!/usr/bin/env bash
# Baixa o BepInEx que o FusionCore realmente usa no Android (asset BepInEx-arm64.zip da tag do FusionCore)
# para .fusioncore/BepInEx. O plugin é compilado contra essas DLLs (BepInEx 6.0.0-fusion.621,
# Il2CppInterop 1.5.2, HarmonyX 2.16), e não contra o BepInEx de PC.
set -euo pipefail
cd "$(dirname "$0")/.."
FC_TAG="${FC_TAG:-0.045}"
DEST=.fusioncore
[ -f "$DEST/BepInEx/core/BepInEx.Unity.IL2CPP.dll" ] && { echo "FusionCore BepInEx já presente"; exit 0; }
mkdir -p "$DEST"
curl -fsSL -o "$DEST/BepInEx-arm64.zip" \
  "https://raw.githubusercontent.com/All-Of-Us-Mods/FusionCore/${FC_TAG}/fusionApp/src/main/assets/BepInEx-arm64.zip"
rm -rf "$DEST/BepInEx" && mkdir -p "$DEST/BepInEx" && unzip -q -o "$DEST/BepInEx-arm64.zip" -d "$DEST/BepInEx"
echo "OK: FusionCore ${FC_TAG} BepInEx em $DEST/BepInEx"
