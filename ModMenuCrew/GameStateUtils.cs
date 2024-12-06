using AmongUs.GameOptions;
using InnerNet;
using static ShipStatus;

namespace ModMenuCrew;

public static class GameStateUtils
{
    public static bool IsInGame => AmongUsClient.Instance?.GameState == InnerNetClient.GameStates.Started;
    public static bool IsInLobby => AmongUsClient.Instance?.GameState == InnerNetClient.GameStates.Joined;
    public static bool IsHost => AmongUsClient.Instance?.AmHost ?? false;
    public static bool IsFreePlay => AmongUsClient.Instance?.NetworkMode == NetworkModes.FreePlay;
    public static bool IsLocalGame => AmongUsClient.Instance?.NetworkMode == NetworkModes.LocalGame;

    public static MapType GetCurrentMap()
    {
        if (IsFreePlay)
            return (MapType)AmongUsClient.Instance.TutorialMapId;
        return (MapType)GameOptionsManager.Instance.CurrentGameOptions.MapId;
    }

    public static SystemTypes GetCurrentRoom()
    {
        return DestroyableSingleton<HudManager>.Instance?.roomTracker.LastRoom.RoomId ?? SystemTypes.Hallway;
    }
}