namespace VSRO_CONTROL_API.VSRO.ServerCfg
{
    /// <summary>
    /// Rate fields sourced from the SR_GameServer block in server.cfg.
    /// Raw values use the 100 = 1x scale (e.g. 500 = 5x, 200 = 2x).
    /// </summary>
    public record ServerRates(
        int ExpRatio,
        int ExpRatioParty,
        int DropItemRatio,
        int DropGoldAmountCoef,
        bool WinterEvent2009,
        bool ThanksgivingEvent,
        bool ChristmasEvent2007
    );
}
