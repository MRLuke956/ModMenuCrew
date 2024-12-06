namespace ModMenuCrew;

public static class SabotageService
{
    public static void TriggerReactorMeltdown()
    {
       
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 128);
    }

    public static void TriggerOxygenDepletion()
    {
       
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 128);
    }

    public static void TriggerLightsOut()
    {
      
        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Electrical, 128);
    }

    
}
