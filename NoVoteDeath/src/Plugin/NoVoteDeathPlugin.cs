using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace NoVoteDeath
{
    [BepInPlugin(Id, Name, Version)]
    public class NoVoteDeathPlugin : BasePlugin
    {
        public const string Id = "dev.novotedeath.mod";
        public const string Name = "NO VOTE = DEATH";
        public const string Description = "Vote ou seja eliminado!";
        public const string Version = "1.2.0";

        // Configurações (arquivo de config do BepInEx; só o ANFITRIÃO precisa delas, pois só ele decide).
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> VotingTime;           // segundos; 0 = usa o valor da sala
        public static ConfigEntry<bool> EliminateNonVoters;
        public static ConfigEntry<bool> ShowWarning;
        public static ConfigEntry<bool> ShowEliminationEffect;

        public override void Load()
        {
            Enabled = Config.Bind("Geral", "Ativado", true, "Liga/desliga o modo NO VOTE = DEATH.");
            VotingTime = Config.Bind("Geral", "TempoDeVotacao", 0,
                new ConfigDescription("Segundos de votação (0 = usar o padrão da sala).", new AcceptableValueRange<int>(0, 300)));
            EliminateNonVoters = Config.Bind("Geral", "EliminarQuemNaoVotou", true, "Se desligado, ninguém morre.");
            ShowWarning = Config.Bind("Interface", "AvisoAntesDoFim", true, "Avisa quando restam poucos segundos.");
            ShowEliminationEffect = Config.Bind("Interface", "EfeitoVisualDaEliminacao", true, "Mostra a mensagem/efeito ao eliminar.");

            Harmony.CreateAndPatchAll(typeof(NoVoteDeathPlugin).Assembly, Id);
            Log.LogInfo($"{Name} v{Version} carregado.");
        }
    }
}
