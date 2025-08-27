using InnerNet;
using UnityEngine;

public static class GameEndManager
{
    public static void ForceGameEnd(GameOverReason endReason)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            Debug.LogWarning("[GameEndManager] Apenas o host pode forçar o fim do jogo.");
            return;
        }

        
        if (GameManager.Instance == null || AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Ended)
        {
            return;
        }

        GameManager.Instance.RpcEndGame(endReason, showAd: false);
    }
}