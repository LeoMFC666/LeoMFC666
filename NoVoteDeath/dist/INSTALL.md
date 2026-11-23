# Instalação e submissão (Starlight / Android)

> A `NoVoteDeath.dll` só existe depois de compilar (veja README). Esta pasta `dist/` é onde ela deve ficar.

## O que se sabe (guia oficial: allofus.dev/guides/starlight-dev-guide)
- Starlight roda **plugins BepInEx** como DLL .NET 6+ (AnyCPU).
- O Starlight **público não carrega mods não aprovados**.
- O guia **não descreve manifesto nem formato de pacote**. Não foi inventado nenhum aqui.

## Para testar no seu celular
1. Entre no Discord do All Of Us (AOU) e peça entrada no time de **Beta Testers** (builds que aceitam mods não aprovados).
2. Pergunte lá como carregar uma DLL local/qual formato de pacote usar.
3. Instale o Starlight de Beta, coloque `NoVoteDeath.dll` conforme instruído e (opcional) `config/dev.novotedeath.mod.cfg`.
4. O **anfitrião** deve ter o mod. Use tempo de votação > 0 na sala.

## Para publicar
1. Envie a build à equipe AOU para aprovação (Discord).
2. Informe: nome "NO VOTE = DEATH", descrição "Vote ou seja eliminado!", versão do GameLibs usada (2026.9.29) e que é host-autoritativo.
