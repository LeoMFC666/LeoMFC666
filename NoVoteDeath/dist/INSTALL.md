# Instalação — FusionCore (Android)

NO VOTE = DEATH v1.2.0 · plugin BepInEx · compilado para o **BepInEx Fusion 6.0.0-fusion.621** que o FusionCore 0.045 traz.

## O que é confirmado (fontes)
- FusionCore é "só um carregador de mods": não tem loja; você coloca as DLLs nos seus próprios arquivos
  (mantenedor, [issue #30](https://github.com/All-Of-Us-Mods/FusionCore/issues/30)).
- Não precisa editar manifesto nem APK do jogo: "FusionCore scans installed apps for IL2CPP unity games"
  ([README](https://github.com/All-Of-Us-Mods/FusionCore)). Android 7.0+ (testado pelo autor só no Android 11+).
- Pasta do BepInEx, no código do FusionCore (`BootstrapActivity.prepareConfig` → `Utilities.getExternalFusionCoreDirectory`):
  `/storage/emulated/0/FusionCore/<pacote do jogo>/BepInEx`. Plugins ficam em `BepInEx/plugins` e configs em
  `BepInEx/config` (`BepInEx.Paths` do BepInEx Fusion).
- O pacote do Among Us Android é `com.innersloth.spacemafia`.
- Não existe manifesto/zip de mod no FusionCore: é só a DLL. Nada foi inventado aqui.

## Passos
1. Instale o Among Us da Play Store e o APK do FusionCore ([Releases](https://github.com/All-Of-Us-Mods/FusionCore/releases)).
2. Abra o FusionCore, escolha **Among Us** e inicie uma vez (ele extrai o BepInEx, gera o interop e cria as pastas). Feche o jogo.
3. Copie `NoVoteDeath.dll` para:
   `/storage/emulated/0/FusionCore/com.innersloth.spacemafia/BepInEx/plugins/`
4. (Opcional) Copie `dev.novotedeath.mod.cfg` para:
   `/storage/emulated/0/FusionCore/com.innersloth.spacemafia/BepInEx/config/`
   (sem ele, o arquivo é criado com os valores padrão na primeira execução).
5. Abra o Among Us pelo FusionCore. No log do BepInEx deve aparecer `NO VOTE = DEATH v1.2.0 carregado.`
6. O **anfitrião** precisa ter o mod. Use tempo de votação > 0 na sala.

## Se não carregar
- Logs/crash: o FusionCore salva crash logs na pasta `FusionCore` (release 0.0.2); o log do BepInEx fica em `BepInEx/LogOutput.log`.
- Versão do jogo diferente de 2026.9.29 → recompile (ver README, "Limitações").
- Android 16: issue aberta [#24](https://github.com/All-Of-Us-Mods/FusionCore/issues/24) — o jogo fecha mesmo sem mods.
