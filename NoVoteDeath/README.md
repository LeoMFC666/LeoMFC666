# NO VOTE = DEATH — "Vote ou seja eliminado!"

Mod BepInEx IL2CPP para Among Us, alvo: **FusionCore (Android)** — também segue sendo um plugin BepInEx comum.

## v1.2.1 — correção do Harmony/MonoMod no Android (FusionCore)

**Erro corrigido** (LogOutput.log): `System.UnauthorizedAccessException: Access to the path '/data/.cache' is denied`
em `MonoMod.Core.Platforms.Systems.AndroidSystem..ctor()` ← `HarmonyLib.Public.Patching.NativeDetourMethodPatcher.TryResolve()`.

**Causa (conferida nas DLLs do FusionCore 0.045, não suposta):**
1. HarmonyX 2.16: `NativeDetourMethodPatcher.TryResolve` é um resolvedor global e lê `PlatformTriple.Current` em **todo** `Harmony.Patch`,
   o que cria a plataforma do MonoMod.Core 1.4.0-alpha → `AndroidSystem..ctor()`.
2. Esse construtor faz `Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(LocalApplicationData), ".cache"))` e guarda a
   pasta como `HelperDropPath`, onde depois grava `mm-exhelper.so.XXXXXX` e faz `dlopen` dela.
3. No `System.Private.CoreLib` que o FusionCore traz, `LocalApplicationData` = `$HOME`. O processo do jogo não tem `HOME`, então o runtime usa
   `getpwuid()` → no Android, `/data` → `/data/.cache` → acesso negado.
4. Esse CoreLib **memoriza** o valor em `Environment.s_specialFolders` na 1ª leitura. Por isso só trocar `HOME` **não** resolveria se algo
   já tivesse lido a pasta (ex.: uma tentativa anterior que falhou).

**Suporte do Harmony/MonoMod a esse ambiente:** sim. O MonoMod.Core do FusionCore tem `AndroidSystem`, `Arm64Arch`, `Core100Runtime` (.NET 10)
e `exhelper_android_arm64.so`; o `AndroidSystem` não anuncia `MayUseNativeJitHooks`, então usa o hook de JIT gerenciado (o
`GetNativeJitHookConfig` não implementado nunca é chamado). Os detours de métodos IL2CPP usam o `NativeDetourProvider` do BepInEx Fusion
(`libfusion`), não o MonoMod. O único bloqueio era a pasta de cache.

**Correção** (`src/Plugin/MonoModAndroidFix.cs` + lógica testável em `src/Core/MonoModHome.cs`), só em volta do `Harmony.CreateAndPatchAll`:
- escolhe uma pasta **privada e interna** do app, gravável e sem `noexec` (exigência do `dlopen`), nesta ordem:
  `<app_fusion_runtime>/monomod-home` (ao lado do runtime .NET que o FusionCore extraiu), `/data/user/0/<pacote>/cache/monomod-home`,
  `/data/data/<pacote>/cache/monomod-home`, `$TMPDIR/monomod-home` — testando de verdade (cria a pasta e grava um arquivo);
- **nunca** toca em `/`, `/data`, `/system`, `/proc`… nem no armazenamento compartilhado (`/storage`, `/sdcard`: `noexec`) — esses são rejeitados sem acesso ao disco;
- redireciona `LocalApplicationData` para lá: `HOME`, `XDG_DATA_HOME` (runtimes .NET padrão) **e** a entrada memorizada em `Environment.s_specialFolders`;
- o MonoMod inicializa (uma vez por processo) e fixa `HelperDropPath` nessa pasta; depois tudo é **restaurado** ao valor anterior;
- se a pasta atual já serve (PC/Linux normal), não muda nada. Lógica do jogo e patches: **sem alteração**.

Também nesta versão (mudança sua, preservada): `TempoDeVotacao` padrão = **25** s.

## Estado da v1.2.0 — compatibilidade FusionCore

| Item | Estado |
|---|---|
| Testes de lógica xUnit (36) | ✅ **36/36 em net6.0 e 36/36 em net10.0** (runtime do FusionCore) |
| Testes de compatibilidade FusionCore (11, `tests/NoVoteDeath.FusionCompatTests`) | ✅ **11/11** contra as DLLs reais do FusionCore 0.045 |
| `dist/NoVoteDeath.dll` | ✅ **compilada localmente** contra BepInEx 6.0.0-fusion.621 + interop gerado pelo Il2CppInterop 1.5.2 do FusionCore |
| Constantes 255/254/253/252, `RpcCalls.Exiled = 4`, `MeetingStates.NotVoted`, `StringNames.MeetingVotingEnds` | ✅ conferidas por teste automatizado no `AmongUs.GameLibs.Android 2026.9.29` |
| Alvos do Harmony (`Start`, `VotingComplete`, `UpdateTimerText`) e nomes dos parâmetros dos Postfix | ✅ conferidos por teste no interop gerado pelo Il2CppInterop do FusionCore |
| Testado dentro do jogo no FusionCore | ❌ depende de você (não há aparelho Android aqui) |

## O que mudou da v1.1.0 para a v1.2.0 (e por quê)
Fonte dos requisitos: [README do FusionCore](https://github.com/All-Of-Us-Mods/FusionCore) e o `BepInEx-arm64.zip` que o próprio
app embarca (`fusionApp/src/main/assets/`, tag `0.045`) — versões lidas das DLLs, não supostas.

| | v1.1.0 (Starlight) | v1.2.0 (FusionCore) |
|---|---|---|
| BepInEx | 6.0.0-be.735 (PC, NuGet) | **6.0.0-fusion.621** ("BepInEx Fusion", fork do FusionCore) |
| Il2CppInterop | 1.4.6 | **1.5.2** |
| HarmonyX (`0Harmony`) | 2.10.2 | **2.16.0** |
| Runtime / TFM | net6.0 | **net10.0** (README do FusionCore: "FusionCore uses .NET 10.0"; `BepInEx.Unity.IL2CPP` e `Il2CppInterop.Runtime` do FusionCore são net10.0, então um projeto net6.0 nem consegue referenciá-los) |
| Interop do jogo | gerado pelo `BepInEx.IL2CPP.MSBuild` (precisa de `unity.bepinex.dev`) | gerado por `build/gen-interop.sh` com o **mesmo Il2CppInterop do FusionCore** (o mesmo passo que o BepInEx Fusion faz no aparelho) |

Arquivos alterados: `src/Plugin/NoVoteDeath.csproj` (referências), `src/Plugin/NoVoteDeathPlugin.cs` (só a constante `Version`: estava "1.0.0"
mesmo na v1.1.0 → "1.2.0"), `src/Core/*.csproj` e `tests/NoVoteDeath.Tests/*.csproj` (rodam também em net10.0), `build/` (scripts), docs.
**Nenhuma linha da lógica ou dos patches (`MeetingPatches.cs`, `Eliminator.cs`, `src/Core/*.cs`) mudou**: todo o código compilou sem alteração
contra a pilha do FusionCore. Não usamos MonoMod diretamente (o README do FusionCore só exige migração para quem usa). Nenhuma funcionalidade foi removida.

## Correções desta versão
- `NotificationPopper.AddItem` **não existe** nesta versão do jogo (erro de compilação) → `AddDisconnectMessage`.
- Texto do timer duplicava: o jogo só reescreve o `TimerText` quando o segundo muda (`lastSecond`) → `TimerTextBuilder` remove e recoloca o sufixo (idempotente; aviso some ao votar).
- Alvos do Harmony por string (`"VotingComplete"`…) em vez de `nameof` (membros não públicos).
- `Eliminator` valida host, jogador nulo/morto/desconectado e retorna se eliminou.
- Workflow sem `continue-on-error`: falha em teste/compilação = job vermelho, nada publicado; verifica que a DLL não está vazia.

## Regras implementadas
- Votou em alguém ou **pulou** → vive. Não votou até o fim → morre. Vários podem morrer na mesma reunião.
- Mortos, desconectados e o jogador já expulso pela votação normal **não** são afetados. O host também precisa votar.
- Voto com ID inexistente (voto falso) conta como "não votou".
- Cada reunião é processada uma vez (`MeetingSession`); ninguém é eliminado duas vezes.

## Como foi feito (e conferido)
- Hook: `MeetingHud.VotingComplete(states, exiled, tie, wasOverruled, overruleNonce)` (privado; conferido no código do jogo). Só o **host** calcula.
- Quem não vota até o tempo acabar recebe `VotedForId = 254` pelo `ForceSkipAll()` do host (conferido). Constantes: 255/254 = sem voto, 253 = pulou, 252 = morto.
- Morte: `player.Exiled()` no host + RPC `RpcCalls.Exiled` para todos + `Data.IsDead`/`MarkDirty()` — padrão usado por TownOfNext/PocketRoles. É o que o jogo faz para o expulso (`ExileController.WrapUp`).
- UI: `MeetingHud.UpdateTimerText` (valor calculado pelo próprio jogo) → etiqueta "NO VOTE = DEATH" e aviso nos últimos 10 s **só para quem ainda não votou**.

## Configuração
`config/dev.novotedeath.mod.cfg` (BepInEx): Ativado, TempoDeVotacao, EliminarQuemNaoVotou, AvisoAntesDoFim, EfeitoVisualDaEliminacao.

## Limitações conhecidas (honestas)
- **Tempo de votação 0 = sem limite** → ninguém é eliminado por tempo (a reunião só termina quando todos votam). Use um tempo > 0 na sala.
- O tempo configurado é aplicado só no host; o contador exibido nos clientes vem do próprio jogo e pode divergir se a opção não for replicada.
- Sem menu de opções dentro da sala (exigiria Reactor/Mira; a documentação do FusionCore não menciona suporte a eles) — config por arquivo.
- A mensagem de eliminação aparece só no host; os demais veem o jogador morrer normalmente.
- Em lobbies com jogadores sem o mod: só o **host precisa do mod** para eliminar; o contador/aviso só aparece para quem tem o mod.
- Versão do jogo: GameLibs 2026.9.29. O BepInEx Fusion gera o interop no aparelho a partir do **jogo instalado**; se o Among Us estiver
  em outra versão, ajuste `GamelibsVersion` em `build/GameLibs/GameLibs.csproj`, apague `.fusioncore/` e recompile.
- FusionCore ainda é 0.0.x ("expect many bugs"). Issue aberta [#24](https://github.com/All-Of-Us-Mods/FusionCore/issues/24): Among Us fecha
  ao abrir no **Android 16** mesmo sem mods — problema do FusionCore, não deste mod.
- Sincronização multiplayer e fim de reunião só são verificáveis em jogo real; os testes cobrem a lógica pura.
- iOS: não suportado (FusionCore é só Android). PC: esta DLL é net10.0 para o BepInEx Fusion; o BepInEx de PC usa .NET 6 — para PC compile a v1.1.0.

## Testes (`tests/`)
`NoVoteDeath.FusionCompatTests/MonoModAndroidTests` (v1.2.1): instancia o **`AndroidSystem` real** do MonoMod do FusionCore (com um shim
mínimo `__errno`/`sysconf` bionic→glibc compilado no teste) numa pasta existente e sem permissão → reproduz o erro do log; com
`MonoModAndroidFix` do plugin compilado → inicializa, `HelperDropPath` = pasta interna gravável, e HOME/XDG/LocalApplicationData voltam ao original;
HarmonyX 2.16 do FusionCore aplica um Postfix de verdade no .NET 10. `NoVoteDeath.Tests/MonoModHomeTests`: escolha da pasta, locais proibidos,
pacote do `/proc/self/cmdline`, sonda real, e o redirecionamento do valor memorizado.

`NoVoteDeath.FusionCompatTests`: alvos do Harmony existem (1 sobrecarga), parâmetros dos Postfix batem com o jogo, constantes, enums,
versões referenciadas = versões do FusionCore, net10.0/AnyCPU/IL-only, `[BepInPlugin]`, sem MonoMod direto. (Rodado contra a DLL v1.1.0, reprova: HarmonyX 2.10.2 e net6.0.)

`NoVoteDeath.Tests` (lógica, net6.0 + net10.0): votação normal, voto pulado, sem voto, todos votaram, ninguém votou, host sem voto, vários sem voto, morto, desconectado,
eliminação duplicada, encerramento (processa uma vez / reset por reunião), expulso não re-eliminado, sala com 15, voto falso, modo desligado.

## Compilar
`build/build.sh` (precisa de .NET SDK 10 e runtime 6; acesso a nuget.org, nuget.bepinex.dev e raw.githubusercontent.com).
Baixa o BepInEx do FusionCore, gera o interop, roda os testes, compila e copia para `dist/NoVoteDeath.dll`.

## Instalar no Android (FusionCore)
Ver `dist/INSTALL.md`.

## Estrutura
`src/` código · `tests/` xUnit · `build/` scripts (`build.sh`, `fetch-fusioncore.sh`, `gen-interop.sh`, `InteropGen/`) · `config/` exemplo de cfg · `dist/` o que o usuário instala · `.github/workflows/` compilação na nuvem.
