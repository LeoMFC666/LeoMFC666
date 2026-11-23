#!/usr/bin/env bash
# Uso: build/build.sh   (precisa de .NET SDK 8 + runtime 6; acesso a nuget.bepinex.dev)
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p dist
dotnet test tests/NoVoteDeath.Tests -c Release --logger "trx;LogFileName=results.trx" 2>&1 | tee build/test.log
set -o pipefail; dotnet build src/Plugin -c Release -o build/out 2>&1 | tee build/build.log
cp build/out/NoVoteDeath.dll dist/NoVoteDeath.dll
echo "OK: dist/NoVoteDeath.dll"
