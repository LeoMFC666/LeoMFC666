using NoVoteDeath.Core;
using Xunit;

namespace NoVoteDeath.Tests
{
    public class TimerTextBuilderTests
    {
        [Fact] public void ChamadasRepetidas_NaoDuplicam()
        {
            string t = "Votação termina em: 20s";
            for (int i = 0; i < 50; i++) t = TimerTextBuilder.Build(t, true, 20, true, true);
            Assert.Equal(1, Count(t, "NO VOTE = DEATH"));
            Assert.StartsWith("Votação termina em: 20s", t);
        }

        [Fact] public void AvisoAparece_SoNosUltimos10s_ESoParaQuemNaoVotou()
        {
            Assert.Contains("Vote ou seja eliminado", TimerTextBuilder.Build("x", true, 10, true, true));
            Assert.DoesNotContain("Vote ou seja eliminado", TimerTextBuilder.Build("x", true, 11, true, true));
            Assert.DoesNotContain("Vote ou seja eliminado", TimerTextBuilder.Build("x", true, 0, true, true));
            Assert.DoesNotContain("Vote ou seja eliminado", TimerTextBuilder.Build("x", true, 5, true, false));
            Assert.DoesNotContain("Vote ou seja eliminado", TimerTextBuilder.Build("x", true, 5, false, true));
        }

        [Fact] public void AvisoSome_QuandoJogadorVota()
        {
            string t = TimerTextBuilder.Build("x", true, 5, true, true);
            t = TimerTextBuilder.Build(t, true, 5, true, false); // jogo não reescreveu o texto, mas o estado mudou
            Assert.DoesNotContain("Vote ou seja eliminado", t);
            Assert.Contains("NO VOTE = DEATH", t);
        }

        [Fact] public void OutraChave_RemoveSufixo()
        {
            string t = TimerTextBuilder.Build("Votação: 5", true, 5, true, true);
            Assert.Equal("Votação: 5", TimerTextBuilder.Build(t, false, 0, true, true));
        }

        [Fact] public void Strip_SemSufixo_NaoAltera() => Assert.Equal("abc", TimerTextBuilder.Strip("abc"));
        [Fact] public void TextoNulo_NaoQuebra() => Assert.Contains("NO VOTE", TimerTextBuilder.Build(null, true, 5, true, false));

        private static int Count(string s, string sub) { int n = 0, i = 0; while ((i = s.IndexOf(sub, i, System.StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; } return n; }
    }
}
