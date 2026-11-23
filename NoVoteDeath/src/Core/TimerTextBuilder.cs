namespace NoVoteDeath.Core
{
    /// <summary>
    /// Monta o texto do temporizador da reunião de forma IDEMPOTENTE: o jogo só reescreve o TimerText
    /// quando o segundo muda (campo MeetingHud.lastSecond), mas o nosso Postfix roda a cada chamada.
    /// Por isso removemos o sufixo que nós mesmos adicionamos antes de recolocá-lo — nunca duplica.
    /// Lógica pura, sem dependência do Unity.
    /// </summary>
    public static class TimerTextBuilder
    {
        /// <summary>Marca que identifica o início do que o mod acrescentou.</summary>
        public const string Marker = "\n<size=70%><color=#FF4040>NO VOTE = DEATH</color>";
        public const string WarningLine = "\n<color=#FFD000>Vote ou seja eliminado!</color>";
        public const string Closing = "</size>";
        public const int WarningSeconds = 10;

        /// <summary>Remove um sufixo já acrescentado pelo mod (se houver).</summary>
        public static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            int i = text.IndexOf(Marker, System.StringComparison.Ordinal);
            return i < 0 ? text : text.Substring(0, i);
        }

        /// <param name="current">Texto atual do TimerText (pode já conter o sufixo do mod).</param>
        /// <param name="isVotingEndsKey">True só quando o jogo mostra "a votação termina em X".</param>
        /// <param name="seconds">Segundos restantes informados pelo jogo.</param>
        /// <param name="showWarning">Config: avisar perto do fim.</param>
        /// <param name="localNeedsToVote">Jogador local vivo e que ainda não votou.</param>
        public static string Build(string current, bool isVotingEndsKey, int seconds, bool showWarning, bool localNeedsToVote)
        {
            string baseText = Strip(current);
            if (!isVotingEndsKey) return baseText;

            string text = baseText + Marker;
            if (showWarning && localNeedsToVote && seconds > 0 && seconds <= WarningSeconds)
                text += WarningLine;
            return text + Closing;
        }
    }
}
