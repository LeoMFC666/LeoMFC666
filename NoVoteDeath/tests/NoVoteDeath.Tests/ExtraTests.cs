using System.Collections.Generic;
using System.Linq;
using NoVoteDeath.Core;
using Xunit;

namespace NoVoteDeath.Tests
{
    public class ExtraTests
    {
        private static VoterSnapshot P(byte id, byte vote, bool alive = true, bool conn = true) => new VoterSnapshot(id, alive, conn, vote);

        // Valores conferidos por reflexão em PlayerVoteArea (AmongUs.GameLibs.Android 2026.9.29).
        [Fact] public void ConstantesIguaisAoJogo()
        {
            Assert.Equal(255, VoteConstants.HasNotVoted);
            Assert.Equal(254, VoteConstants.MissedVote);
            Assert.Equal(253, VoteConstants.SkippedVote);
            Assert.Equal(252, VoteConstants.DeadVote);
        }

        [Fact] public void VotarEmJogadorMorto_ContaComoVoto() =>
            Assert.Empty(VoteEvaluator.GetPlayersToEliminate(new[] { P(0, 1), P(1, 0, alive: false) }, new HashSet<byte>()));

        [Fact] public void VotoComValorDeMorto_EmJogadorVivo_NaoContaComoVoto() =>
            Assert.Equal(new byte[] { 0 }, VoteEvaluator.GetPlayersToEliminate(new[] { P(0, VoteConstants.DeadVote), P(1, 0) }, new HashSet<byte>()).ToArray());

        [Fact] public void RegraDesligadaNoConfig_NinguemMorre()
        {
            var s = new MeetingSession();
            Assert.Empty(s.Finish(new[] { P(0, 255), P(1, 255) }, null, false));
        }

        [Fact] public void HostSemVoto_EOutrosNaoVotaram_TodosMorremUmaVez()
        {
            var s = new MeetingSession();
            var v = new[] { P(0, 255), P(1, 254), P(2, 253), P(3, 0) };
            Assert.Equal(new byte[] { 0, 1 }, s.Finish(v, null, true).ToArray());
            Assert.Empty(s.Finish(v, null, true));
            Assert.Equal(new byte[] { 0, 1 }, s.Eliminated.OrderBy(x => x).ToArray());
        }

        [Fact] public void ExpulsoPeloJogoNaoNaoVotou_NaoEhReEliminado()
        {
            var s = new MeetingSession();
            var v = new[] { P(0, 1), P(1, 254), P(2, 253) };
            Assert.Empty(s.Finish(v, exiledId: 1, enabled: true)); // 1 foi expulso pela votação normal
        }

        [Fact] public void VariasReunioes_CadaUmaIndependente()
        {
            var s = new MeetingSession();
            for (int i = 0; i < 3; i++)
            {
                s.Reset();
                Assert.Equal(new byte[] { 2 }, s.Finish(new[] { P(0, 2), P(2, 254) }, null, true).ToArray());
            }
        }

        [Fact] public void ListaNulaOuVazia_NaoQuebra()
        {
            Assert.Empty(VoteEvaluator.GetPlayersToEliminate(null, new HashSet<byte>()));
            Assert.Empty(VoteEvaluator.GetPlayersToEliminate(new VoterSnapshot[0], new HashSet<byte>()));
        }
    }
}
