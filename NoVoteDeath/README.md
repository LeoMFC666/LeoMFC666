# NO VOTE = DEATH — "Vote ou seja eliminado!"

Mod BepInEx IL2CPP para Among Us, alvo: **FusionCore (Android)** — também segue sendo um plugin BepInEx comum.

## Estado real desta versão (v1.2.0 — compatibilidade FusionCore)

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
