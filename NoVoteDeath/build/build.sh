#!/usr/bin/env bash
# Uso: build/build.sh   (precisa de .NET SDK 10 + runtime 6; acesso a nuget.org, nuget.bepinex.dev e raw.githubusercontent.com)
# Gera dist/NoVoteDeath.dll para o FusionCore (Android) e roda todos os testes.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p dist
build/fetch-fusioncore.sh                     # BepInEx Fusion real (do APK do FusionCore)
build/gen-interop.sh                          # interop do Among Us com o Il2CppInterop do FusionCore
dotnet test tests/NoVoteDeath.Tests -c Release 2>&1 | tee build/test.log          # lógica (net6.0 e net10.0)
dotnet build src/Plugin -c Release -o .fusioncore/out -nologo 2>&1 | tee build/build.log
dotnet test tests/NoVoteDeath.FusionCompatTests -c Release 2>&1 | tee -a build/test.log  # compatibilidade FusionCore
test -s .fusioncore/out/NoVoteDeath.dll
cp .fusioncore/out/NoVoteDeath.dll dist/NoVoteDeath.dll
echo "OK: dist/NoVoteDeath.dll"
