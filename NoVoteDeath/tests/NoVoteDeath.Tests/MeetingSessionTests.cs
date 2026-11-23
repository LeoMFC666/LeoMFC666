using System.Collections.Generic;
using System.Linq;
using NoVoteDeath.Core;
using Xunit;

namespace NoVoteDeath.Tests
{
    public class MeetingSessionTests
    {
        private static VoterSnapshot P(byte id, byte vote, bool alive = true, bool conn = true) =>
            new VoterSnapshot(id, alive, conn, vote);
        private const byte None = VoteConstants.HasNotVoted;
        private const byte Missed = VoteConstants.MissedVote;

        [Fact] public void EncerramentoDaReuniao_ProcessaUmaVezSo()
        {
            var s = new MeetingSession();
            var v = new[] { P(0, 1), P(1, Missed) };
            Assert.Equal(new byte[] { 1 }, s.Finish(v, null, true).ToArray());
            Assert.Empty(s.Finish(v, null, true)); // 2a chamada de VotingComplete: nada
            Assert.True(s.Processed);
        }

        [Fact] public void NovaReuniao_ResetPermiteProcessarDeNovo()
        {
            var s = new MeetingSession();
            var v = new[] { P(0, 1), P(1, Missed) };
            s.Finish(v, null, true);
            s.Reset();
            Assert.Equal(new byte[] { 1 }, s.Finish(v, null, true).ToArray());
        }

        [Fact] public void ExpulsoPelaVotacao_NaoEhEliminadoDeNovo()
        {
            var s = new MeetingSession();
            // jogador 1 não votou E foi o mais votado (o jogo já o expulsa)
            var r = s.Finish(new[] { P(0, 1), P(1, Missed), P(2, 1) }, exiledId: 1, enabled: true);
            Assert.Empty(r);
        }

        [Fact] public void VariosSemVoto_TodosEliminados_ExcetoExpulso()
        {
            var s = new MeetingSession();
            var r = s.Finish(new[] { P(0, None), P(1, Missed), P(2, None), P(3, 0) }, exiledId: 0, enabled: true);
            Assert.Equal(new byte[] { 1, 2 }, r.ToArray());
            Assert.Contains((byte)2, s.Eliminated);
        }

        [Fact] public void ModoDesligado_ProcessaMasNaoElimina()
        {
            var s = new MeetingSession();
            Assert.Empty(s.Finish(new[] { P(0, None) }, null, enabled: false));
            Assert.True(s.Processed);
        }

        [Fact] public void SalaCheia15_NinguemVotou_TodosVivosMorrem()
        {
            var s = new MeetingSession();
            var v = Enumerable.Range(0, 15).Select(i => P((byte)i, Missed, alive: i % 5 != 0)).ToArray();
            var r = s.Finish(v, null, true);
            Assert.Equal(12, r.Count);           // 3 mortos (0,5,10) não são afetados
            Assert.DoesNotContain((byte)5, r);
        }

        [Fact] public void ListaNula_NaoQuebra() => Assert.Empty(new MeetingSession().Finish(null, null, true));

        [Fact] public void VotoEmJogadorDesconectado_ContaComoVoto()
        {
            // jogador 1 votou no 2, que depois desconectou (ainda existe na lista) -> 1 vive
            var r = VoteEvaluator.GetPlayersToEliminate(new[] { P(0, 1), P(1, 2), P(2, None, conn: false) }, new HashSet<byte>());
            Assert.Empty(r);
        }
    }
}
