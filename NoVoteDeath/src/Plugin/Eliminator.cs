using Hazel;

namespace NoVoteDeath
{
    /// <summary>
    /// ÚNICO ponto que mexe no estado de morte. Executa apenas no host.
    /// Mesmo padrão de mods públicos (ex.: "RpcExile" do TownOfHost):
    ///   1) player.Exiled() localmente no host;
    ///   2) RPC RpcCalls.Exiled (valor 4, conferido no GameLibs 2026.9.29) enviado a todos (-1);
    ///   3) Data.IsDead = true + MarkDirty() para sincronizar o GameData.
    /// A eliminação é pulada se o jogador já estiver morto/desconectado, então chamar duas vezes é inofensivo.
    /// </summary>
    internal static class Eliminator
    {
        public static bool Eliminate(byte playerId)
        {
            if (!AmongUsClient.Instance.AmHost) return false;

            var info = GameData.Instance.GetPlayerById(playerId);
            if (info == null) return false;
            var player = info.Object;
            if (player == null || info.IsDead || info.Disconnected) return false; // nunca elimina duas vezes

            player.Exiled();

            var writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            info.IsDead = true;
            info.MarkDirty();

            if (NoVoteDeathPlugin.ShowEliminationEffect.Value && HudManager.Instance != null && HudManager.Instance.Notifier != null)
                HudManager.Instance.Notifier.AddDisconnectMessage($"{info.PlayerName} foi eliminado por não votar!");
            return true;
        }
    }
}
