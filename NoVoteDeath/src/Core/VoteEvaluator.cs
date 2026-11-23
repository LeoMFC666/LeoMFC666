using System.Collections.Generic;
using System.Linq;

namespace NoVoteDeath.Core
{
    /// <summary>Valores especiais de PlayerVoteArea (confirmados em código decompilado do jogo).</summary>
    public static class VoteConstants
    {
        public const byte HasNotVoted = 255;
        public const byte MissedVote = 254;
        public const byte SkippedVote = 253;
        public const byte DeadVote = 252;
    }

    /// <summary>Foto de um jogador no fim da votação. Sem dependência do Unity/Among Us.</summary>
    public readonly struct VoterSnapshot
    {
        public VoterSnapshot(byte playerId, bool isAlive, bool isConnected, byte votedFor)
        {
            PlayerId = playerId;
            IsAlive = isAlive;
            IsConnected = isConnected;
            VotedFor = votedFor;
        }

        public byte PlayerId { get; }
        public bool IsAlive { get; }
        public bool IsConnected { get; }
        public byte VotedFor { get; }
    }

    /// <summary>Lógica pura (testável) que decide quem morre por não votar.</summary>
    public static class VoteEvaluator
    {
        /// <summary>
        /// Voto válido = pular OU um jogador existente na sala.
        /// HasNotVoted, MissedVote, DeadVote e IDs inexistentes NÃO contam (anti-voto-falso).
        /// </summary>
        public static bool IsValidVote(byte votedFor, ISet<byte> knownPlayerIds)
        {
            if (votedFor == VoteConstants.SkippedVote) return true;
            if (votedFor >= VoteConstants.DeadVote) return false;
            return knownPlayerIds.Contains(votedFor);
        }

        /// <summary>
        /// Retorna os IDs a eliminar: vivos, conectados, sem voto válido, ainda não eliminados.
        /// O host também entra (sem imunidade).
        /// </summary>
        public static List<byte> GetPlayersToEliminate(
            IReadOnlyCollection<VoterSnapshot> voters,
            ISet<byte> alreadyEliminated,
            bool enabled = true)
        {
            var result = new List<byte>();
            if (!enabled || voters == null) return result;

            var known = new HashSet<byte>(voters.Select(v => v.PlayerId));
            foreach (var v in voters)
            {
                if (!v.IsAlive) continue;                      // mortos não são afetados
                if (!v.IsConnected) continue;                  // desconectado: já saiu da partida
                if (alreadyEliminated != null && alreadyEliminated.Contains(v.PlayerId)) continue; // sem dupla eliminação
                if (IsValidVote(v.VotedFor, known)) continue;  // votou ou pulou
                if (result.Contains(v.PlayerId)) continue;
                result.Add(v.PlayerId);
            }
            return result;
        }
    }
}
