using System.Collections.Generic;

namespace NoVoteDeath.Core
{
    /// <summary>
    /// Estado de UMA reunião, no host. Garante: processa o fim da votação uma única vez
    /// (VotingComplete pode ser disparado mais de uma vez), nunca elimina o mesmo jogador
    /// duas vezes e não re-elimina o jogador já expulso pela votação normal do jogo.
    /// Lógica pura: sem dependência do Unity/Among Us.
    /// </summary>
    public sealed class MeetingSession
    {
        private readonly HashSet<byte> _eliminated = new HashSet<byte>();
        private bool _processed;

        public IReadOnlyCollection<byte> Eliminated => _eliminated;
        public bool Processed => _processed;

        /// <summary>Chamar no início de cada reunião.</summary>
        public void Reset()
        {
            _eliminated.Clear();
            _processed = false;
        }

        /// <param name="exiledId">Jogador expulso pela votação normal (o jogo já cuida dele), ou null.</param>
        /// <returns>IDs a eliminar agora. Vazio se já processado ou se o mod/regra estiver desligado.</returns>
        public List<byte> Finish(IReadOnlyCollection<VoterSnapshot> voters, byte? exiledId, bool enabled)
        {
            if (_processed) return new List<byte>();
            _processed = true;

            if (exiledId.HasValue) _eliminated.Add(exiledId.Value);

            var victims = VoteEvaluator.GetPlayersToEliminate(voters, _eliminated, enabled);
            foreach (var id in victims) _eliminated.Add(id);
            return victims;
        }
    }
}
