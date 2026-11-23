using System.Collections.Generic;
using System.Linq;
using NoVoteDeath.Core;
using Xunit;

namespace NoVoteDeath.Tests
{
    public class VoteEvaluatorTests
    {
        private static VoterSnapshot P(byte id, byte vote, bool alive = true, bool conn = true) =>
            new VoterSnapshot(id, alive, conn, vote);

        private static List<byte> Run(params VoterSnapshot[] v) =>
            VoteEvaluator.GetPlayersToEliminate(v, new HashSet<byte>());

        private const byte None = VoteConstants.HasNotVoted;
        private const byte Skip = VoteConstants.SkippedVote;
        private const byte Missed = VoteConstants.MissedVote;

        [Fact] public void VotouEmAlguem_Vive() => Assert.Empty(Run(P(0, 1), P(1, 0)));
        [Fact] public void PulouVotacao_Vive() => Assert.Empty(Run(P(0, Skip), P(1, Skip)));
        [Fact] public void NaoVotou_Morre() => Assert.Equal(new byte[] { 1 }, Run(P(0, 1), P(1, None)).ToArray());
        [Fact] public void TodosVotaram_NinguemMorre() => Assert.Empty(Run(P(0, 1), P(1, 2), P(2, Skip)));
        [Fact] public void NinguemVotou_TodosVivosMorrem() => Assert.Equal(new byte[] { 0, 1, 2 }, Run(P(0, None), P(1, Missed), P(2, None)).ToArray());
        [Fact] public void MortoAntesDoFim_NaoAfetado() => Assert.Empty(Run(P(0, 1), P(1, None, alive: false)));
        [Fact] public void Desconectou_NaoAfetado() => Assert.Empty(Run(P(0, 1), P(1, None, conn: false)));
        [Fact] public void HostNaoVotou_Morre() => Assert.Equal(new byte[] { 0 }, Run(P(0, None), P(1, 0)).ToArray());
        [Fact] public void DoisOuMaisNaoVotaram_TodosMorrem() => Assert.Equal(new byte[] { 1, 3 }, Run(P(0, 1), P(1, None), P(2, Skip), P(3, Missed)).ToArray());
        [Fact] public void VotacaoNormal_ComEmpate_NinguemMorre() => Assert.Empty(Run(P(0, 1), P(1, 0), P(2, Skip), P(3, Skip)));

        [Fact] public void VotoFalso_IdInexistente_ContaComoNaoVotou() => Assert.Equal(new byte[] { 0 }, Run(P(0, 99), P(1, 0)).ToArray());
        [Fact] public void NaoEliminaDuasVezes()
        {
            var already = new HashSet<byte> { 1 };
            var r = VoteEvaluator.GetPlayersToEliminate(new[] { P(0, 1), P(1, None) }, already);
            Assert.Empty(r);
        }
        [Fact] public void ModoDesligado_NinguemMorre() =>
            Assert.Empty(VoteEvaluator.GetPlayersToEliminate(new[] { P(0, None) }, new HashSet<byte>(), enabled: false));
        [Fact] public void IdDuplicadoNaLista_ElimiaUmaVez() =>
            Assert.Single(Run(P(1, None), P(1, None), P(0, 1)));
    }
}
