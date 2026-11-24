# Instalação — FusionCore (Android)

NO VOTE = DEATH v1.2.1 · plugin BepInEx · compilado para o **BepInEx Fusion 6.0.0-fusion.621** que o FusionCore 0.045 traz.

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
4. (Opcional) Copie `dev.novotedeath.mod.cfg` (tempo de votação padrão: 25 s) para:
   `/storage/emulated/0/FusionCore/com.innersloth.spacemafia/BepInEx/config/`
   (sem ele, o arquivo é criado com os valores padrão na primeira execução).
5. Abra o Among Us pelo FusionCore. No log do BepInEx deve aparecer `NO VOTE = DEATH v1.2.1 carregado.`
6. O **anfitrião** precisa ter o mod. Use tempo de votação > 0 na sala.

## Como verificar que carregou
Arquivo: `/storage/emulated/0/FusionCore/com.innersloth.spacemafia/BepInEx/LogOutput.log` (feche o jogo antes de abrir o log).
Procure, nesta ordem:
1. `[Info   :NO VOTE = DEATH] [MonoMod/Android] '/data/.cache' inutilizável; usando /data/user/0/<pacote>/.../monomod-home/.cache`
   — a correção agiu. (Se aparecer `HOME atual serve: ...`, o ambiente já estava bom e nada foi mudado.)
2. `[Info   :NO VOTE = DEATH] NO VOTE = DEATH v1.2.1 carregado.` — os 3 patches do Harmony foram aplicados.
3. **Não** deve haver `UnauthorizedAccessException`, `'/data/.cache'` nem `AndroidSystem..ctor` no log.
4. No jogo: numa reunião com tempo de votação, o contador mostra a etiqueta vermelha **NO VOTE = DEATH**, e nos últimos 10 s
   aparece "Vote ou seja eliminado!" para quem ainda não votou.

Se aparecer `[MonoMod/Android] nenhuma pasta interna gravável encontrada`, me envie o LogOutput.log inteiro (a lista de pastas testadas
depende de onde o FusionCore extraiu o runtime). Se outro mod que usa Harmony carregar ANTES deste, ele pode registrar o mesmo erro do
`/data/.cache` (não é corrigido por este mod); este mod e os que carregarem depois funcionam, porque o MonoMod tenta de novo.

## Se não carregar
- Logs/crash: o FusionCore salva crash logs na pasta `FusionCore` (release 0.0.2); o log do BepInEx fica em `BepInEx/LogOutput.log`.
- Versão do jogo diferente de 2026.9.29 → recompile (ver README, "Limitações").
- Android 16: issue aberta [#24](https://github.com/All-Of-Us-Mods/FusionCore/issues/24) — o jogo fecha mesmo sem mods.
