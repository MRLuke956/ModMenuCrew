using UnityEngine;

namespace ModMenuCrew.UI.Extensions;

public static class VectorExtensions
{
    public static SystemTypes? GetRoom(this Vector2 position)
    {
        if (!ShipStatus.Instance) return null;

        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            if (room && room.roomArea.OverlapPoint(position))
            {
                return room.RoomId;
            }
        }

        return null;
    }
}