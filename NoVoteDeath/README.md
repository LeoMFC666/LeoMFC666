# NO VOTE = DEATH — "Vote ou seja eliminado!"

Mod BepInEx IL2CPP para Among Us, alvo: **Starlight (Android)**.

## Estado real desta versão (v1.1.0)

| Item | Estado |
|---|---|
| Testes xUnit (36) | ✅ **executados: 36 passaram, 0 falharam** (.NET 8 SDK, net6.0) |
| Constantes de voto 255/254/253/252 | ✅ conferidas por reflexão nas DLLs reais do `AmongUs.GameLibs.Android 2026.9.29` |
| Assinaturas usadas (VotingComplete, VoterState, Exiled, RpcCalls.Exiled=4, StartRpcImmediately…) | ✅ conferidas nas DLLs reais |
| `NoVoteDeath.dll` | ⏳ **compilada pelo GitHub Actions** (não foi possível compilar no ambiente de desenvolvimento: host `unity.bepinex.dev` bloqueado) — baixe em *Actions → NoVoteDeath build → Artifacts*. Só existe se o build passar |
| Testado dentro do jogo / aceito pelo Starlight | ❌ depende de você |

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
- Sem menu de opções dentro da sala (exigiria Reactor/Mira, não verificado) — config por arquivo.
- A mensagem de eliminação aparece só no host; os demais veem o jogador morrer normalmente.
- Em lobbies com jogadores sem o mod: só o **host precisa do mod** para eliminar; o contador/aviso só aparece para quem tem o mod.
- Versão do jogo: GameLibs 2026.9.29. Se o Among Us/Starlight estiverem em outra versão, ajuste a versão no `.csproj`.
- Sincronização multiplayer e fim de reunião só são verificáveis em jogo real; os testes cobrem a lógica pura.
- iOS: não suportado (Starlight é só Android). PC: não verificado.

## Testes (`tests/`)
Votação normal, voto pulado, sem voto, todos votaram, ninguém votou, host sem voto, vários sem voto, morto, desconectado,
eliminação duplicada, encerramento (processa uma vez / reset por reunião), expulso não re-eliminado, sala com 15, voto falso, modo desligado.

## Instalar no Android / submeter ao Starlight
Ver `dist/INSTALL.md`.

## Estrutura
`src/` código · `tests/` xUnit · `build/` script local · `config/` exemplo de cfg · `dist/` o que o usuário instala · `.github/workflows/` compilação na nuvem.
