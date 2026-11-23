using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NoVoteDeath.Core;

// Assinaturas conferidas por reflexão nas assemblies reais do AmongUs.GameLibs.Android 2026.9.29 (MeetingHud):
//   private void VotingComplete(VoterState[] states, NetworkedPlayerInfo exiled, bool tie, bool wasOverruled, ushort overruleNonce)
//   private void UpdateTimerText(StringNames key, int value)
//   public MeetingStates state;  enum MeetingStates { Animating, Discussion, NotVoted, Voted, Results, Proceeding }
// Quem não vota até o fim do tempo recebe VotedForId = 254 (MissedVote) via ForceSkipAll() do host.
namespace NoVoteDeath
{
    internal static class ModState
    {
        public static readonly MeetingSession Session = new MeetingSession();
    }

    [HarmonyPatch(typeof(MeetingHud), "Start")]
    internal static class MeetingStartPatch
    {
        public static void Postfix()
        {
            ModState.Session.Reset();

            // Tempo de votação: só o host aplica (é o host quem força o fim da votação).
            if (AmongUsClient.Instance.AmHost && NoVoteDeathPlugin.Enabled.Value && NoVoteDeathPlugin.VotingTime.Value > 0)
                GameOptionsManager.Instance.currentNormalGameOptions.VotingTime = NoVoteDeathPlugin.VotingTime.Value;
        }
    }

    /// <summary>
    /// Decisão final no HOST. O jogo chama VotingComplete em todos os clientes; só o host calcula
    /// e aplica a eliminação, e a propaga por RPC (ver Eliminator). Clientes não decidem nada.
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), "VotingComplete")]
    internal static class VotingCompletePatch
    {
        public static void Postfix(Il2CppStructArray<MeetingHud.VoterState> states, NetworkedPlayerInfo exiled)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (states == null) return;

            var snapshots = new List<VoterSnapshot>();
            foreach (var s in states)
            {
                var info = GameData.Instance.GetPlayerById(s.VoterId);
                if (info == null) continue;
                snapshots.Add(new VoterSnapshot(s.VoterId, !info.IsDead, !info.Disconnected, s.VotedForId));
            }

            bool enabled = NoVoteDeathPlugin.Enabled.Value && NoVoteDeathPlugin.EliminateNonVoters.Value;
            byte? exiledId = exiled != null ? exiled.PlayerId : (byte?)null;

            foreach (var id in ModState.Session.Finish(snapshots, exiledId, enabled))
                Eliminator.Eliminate(id);
        }
    }

    /// <summary>
    /// Interface sem substituir a original: acrescenta uma etiqueta ao TimerText do próprio jogo.
    /// O jogo só reescreve o texto quando o segundo muda (MeetingHud.lastSecond), então o sufixo é
    /// removido e recolocado a cada chamada (TimerTextBuilder) — nunca duplica e o aviso some assim
    /// que o jogador vota. Só altera texto; não mexe em botões/toques, então não afeta o touchscreen.
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), "UpdateTimerText")]
    internal static class MeetingTimerPatch
    {
        public static void Postfix(MeetingHud __instance, StringNames key, int value)
        {
            if (__instance == null || __instance.TimerText == null) return;

            bool active = NoVoteDeathPlugin.Enabled.Value && NoVoteDeathPlugin.EliminateNonVoters.Value;
            var local = PlayerControl.LocalPlayer;
            bool localNeedsToVote = local != null && local.Data != null && !local.Data.IsDead
                                    && __instance.state == MeetingHud.MeetingStates.NotVoted;

            string current = __instance.TimerText.text;
            string wanted = active
                ? TimerTextBuilder.Build(current, key == StringNames.MeetingVotingEnds, value,
                                         NoVoteDeathPlugin.ShowWarning.Value, localNeedsToVote)
                : TimerTextBuilder.Strip(current);

            if (wanted != current) __instance.TimerText.text = wanted; // só escreve se mudou
        }
    }
}
