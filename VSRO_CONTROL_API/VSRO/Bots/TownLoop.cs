using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;


namespace VSRO_CONTROL_API.VSRO.Bots
{
    /// <summary>
    /// Defines a single action to perform at a town NPC.
    /// Walk to ApproachPosition, find NPC by RefObjId, call Execute.
    /// </summary>
    public record TownTask(
        string Label,
        BotPosition ApproachPosition,
        uint NpcRefObjId,
        Func<BotSettings, VSRO_CONTROL_API.VSRO.DTO.ISession, bool> ShouldRun,
        Func<VSRO_CONTROL_API.VSRO.DTO.ISession, uint, Action<Packet>, CancellationToken, Task> Execute
    );

    /// <summary>
    /// Defines a known town: its name (matched via ResolveReadable),
    /// the center position the AutoWalker walks to first,
    /// and the ordered list of NPC tasks to perform.
    /// </summary>
    public class TownDefinition
    {
        public string TownName { get; init; } = string.Empty;
        public BotPosition CenterPosition { get; init; }
        public List<TownTask> Tasks { get; init; } = new();
    }

    public class TownLoop
    {

        public static bool Debug { get; private set; } = false;
        public static void SetDebug(bool debug) => Debug = debug;
        private static void Log(string handler, string message) { if (Debug == true) Logger.Trace($"TownLoop:{handler}", message); }

        private readonly Proxy _proxy;
        private readonly VSRO_CONTROL_API.VSRO.DTO.ISession _session;
        private readonly AutoWalker _walker;
        private readonly Action<Packet> _sendPacket;
        private readonly Func<BotSettings> _getSettings;

        private const int NPC_FIND_TIMEOUT_MS = 5000;
        private const int NPC_FIND_POLL_MS = 200;

        public static readonly HashSet<string> KnownTownNames = new()
        {
            "Jangan", "Donwhang", "Hotan Palace", "Samarakand",
            "Constantinople", "Alexandria City (S)", "Alexandria City (N)"
        };

        private async Task OpenShopAsync(uint npcUID, CancellationToken ct)
        {
            _session.BotShopOpen = true;
            Logger.Info("TownLoop", $"OpenShopAsync: BotShopOpen=true for NPC UID={npcUID}");

            // Target NPC
            _session.TargetGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var select = new Packet(0x7045);
            select.WriteUInt(npcUID);
            _sendPacket(select);

            using var targetTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            targetTimeout.CancelAfter(3000);
            try { await _session.TargetGate.Task.WaitAsync(targetTimeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            finally { _session.TargetGate = null; }

            // Close/reset — even on a cold session this is safe (server treats it as a no-op
            //    if no shop is open) and is required to get the server to send shop item data
            _session.ShopCloseGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var reset = new Packet(0x704B);
            reset.WriteUInt(npcUID);
            _sendPacket(reset);

            using var resetTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            resetTimeout.CancelAfter(3000);
            try { await _session.ShopCloseGate.Task.WaitAsync(resetTimeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            finally { _session.ShopCloseGate = null; }

            // Open shop
            _session.ShopOpenGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var open = new Packet(0x7046);
            open.WriteUInt(npcUID);
            open.WriteByte(0x01);
            _sendPacket(open);

            using var openTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            openTimeout.CancelAfter(5000);
            try
            {
                await _session.ShopOpenGate.Task.WaitAsync(openTimeout.Token);
                Logger.Info("TownLoop", $"Shop open confirmed for NPC UID={npcUID}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Logger.Warn("TownLoop", $"Shop open timed out for NPC UID={npcUID} — proceeding anyway");
            }
            finally { _session.ShopOpenGate = null; }

            await Task.Delay(200, ct);
        }

        private async Task CloseShopAsync(uint npcUID, CancellationToken ct)
        {
            _session.BotShopOpen = false;
            Logger.Info("TownLoop", $"CloseShopAsync: BotShopOpen=false for NPC UID={npcUID}");

            var close = new Packet(0x704B);
            close.WriteUInt(npcUID);
            _sendPacket(close);

            await Task.Delay(300, ct);
        }

        private readonly Dictionary<string, TownDefinition> Towns;

        private static readonly Dictionary<string, string> RegionToTown = new()
        {
            ["China"] = "Jangan",
            ["Donwhang"] = "Donwhang",
            ["Hotan"] = "Hotan Palace",
            ["Samarakand (Central Asia)"] = "Samarakand",
            ["Constantinople"] = "Constantinople",
            ["Alexandria"] = "Alexandria City (S)",
            ["Abundance Grounds (Alex Desert)"] = "Alexandria City (S)",
            ["Roc Mountain"] = "Hotan Palace",
            ["Kings Valley"] = "Alexandria City (S)",
            ["The Storm And Cloud Desert"] = "Alexandria City (S)",
            ["Qin-Shi Tomb|floor:1"] = "Jangan",
            ["Qin-Shi Tomb|floor:2"] = "Jangan",
            ["Qin-Shi Tomb|floor:3"] = "Jangan",
            ["Qin-Shi Tomb|floor:4"] = "Jangan",
            ["Stone Cave"] = "Donwhang",
        };

        public TownLoop(
            Proxy proxy,
            AutoWalker walker,
            Action<Packet> sendPacket,
            Func<BotSettings> getSettings)
        {
            _proxy = proxy;
            _session = proxy.Session!;
            _walker = walker;
            _sendPacket = sendPacket;
            _getSettings = getSettings;


            Towns = new Dictionary<string, TownDefinition>
            {
                ["Jangan"] = new TownDefinition
                {
                    TownName = "Jangan",
                    CenterPosition = BotPosition.FromDisplayWorld(6432, 1082, -32),
                    Tasks = new List<TownTask>
                    {
                        new TownTask(
                            Label: "Blacksmith maintenance",
                            ApproachPosition: BotPosition.FromDisplayWorld(6377, 1096, 0),
                            NpcRefObjId: 2003,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Maintenance.RepairWeapon ||
                                settings.Consumables.BuyAmmo,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2003;

                                // Repair first — only needs targeting, not shop open
                                if (settings.Maintenance.RepairWeapon)
                                {
                                    await SelectNpcAsync(npcUID, ct);

                                    var repair = new Packet(0x703E);
                                    repair.WriteUInt(npcUID);
                                    repair.WriteByte(0x02);
                                    sendPacket(repair);

                                    await Task.Delay(1500, ct);

                                    session._attackLoop?.ClearRepairFlag();
                                    Logger.Info("TownLoop", "Repair all sent");
                                }

                                // Buy ammo second — open shop only if needed
                                if (settings.Consumables.BuyAmmo)
                                {
                                    if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                    {
                                        int have = inv.Slots.Values.Where(i => i.CodeName128 == ammoCode).Sum(i => i.Stack);
                                        int need = settings.Consumables.AmmoRefillAmount - have;
                                        Logger.Info("TownLoop", $"Ammo check: code={ammoCode} have={have} refillAt={settings.Consumables.AmmoRefillAmount} need={need}");

                                        if (need > 0)
                                        {
                                            await OpenShopAsync(npcUID, ct);
                                            SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);
                                            await Task.Delay(300, ct);
                                            await StackItemAsync(ammoCode, ct);
                                            await CloseShopAsync(npcUID, ct);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warn("TownLoop", $"AmmoType '{settings.Consumables.AmmoType}' not found in ConsumableCodeNames.Ammunition");
                                    }
                                }

                                Logger.Info("TownLoop", "Blacksmith maintenance complete");
                            }
                        ),

                        #region Restock potions
                        new TownTask(
                            Label: "Restock potions",
                            ApproachPosition: BotPosition.FromDisplayWorld(6486, 1112, 0),
                            NpcRefObjId: 2005,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                            {
                                var inv = session.Inventory;

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values
                                       .Where(i => i.CodeName128 == codeName)
                                       .Sum(i => i.Stack);

                                if (settings.Consumables.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.Consumables.HPType, out var hpCode) &&
                                    CurrentStack(hpCode) < settings.Consumables.HpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.Consumables.MPType, out var mpCode) &&
                                    CurrentStack(mpCode) < settings.Consumables.MpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode) &&
                                    CurrentStack(vigCode) < settings.Consumables.VigorPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.Consumables.UniPillType, out var uniCode) &&
                                    CurrentStack(uniCode) < settings.Consumables.UniversalPillsRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.Consumables.PurificationPillType, out var purifCode) &&
                                    CurrentStack(purifCode) < settings.Consumables.PurifPillsRefillAmount)
                                    return true;

                                return false;
                            },

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2005;

                                await OpenShopAsync(npcUID, ct);

                                // Snapshot AFTER shop opens — server may send inventory sync during the delay,
                                // so any counts taken before the wait can be stale when items already exist.
                                var stackSnapshot = inv.Slots.Values.ToList()
                                    .GroupBy(i => i.CodeName128)
                                    .ToDictionary(g => g.Key!, g => g.Sum(i => i.Stack));

                                int CurrentStack(string codeName) =>
                                    stackSnapshot.TryGetValue(codeName, out var n) ? n : 0;

                                if (settings.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                                {
                                    int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                    if (need > 0)
                                    {
                                        await Task.Delay(1000, ct);
                                        SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket);
                                        Logger.Info("TownLoop", $"Buying {need} HP potions ({hpCode})");
                                        await Task.Delay(1000, ct);
                                        await StackItemAsync(hpCode, ct);
                                    }
                                }

                                if (settings.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                                {
                                    int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                    if (need > 0)
                                    {
                                        await Task.Delay(1000, ct);
                                        SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket);
                                        Logger.Info("TownLoop", $"Buying {need} MP potions ({mpCode})");
                                        await Task.Delay(1000, ct);
                                        await StackItemAsync(mpCode, ct);
                                    }
                                }

                                if (settings.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                                {
                                    int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                    if (need > 0)
                                    {
                                        await Task.Delay(1000, ct);
                                        SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket);
                                        Logger.Info("TownLoop", $"Buying {need} vigor potions ({vigCode})");
                                        await Task.Delay(1000, ct);
                                        await StackItemAsync(vigCode, ct);
                                    }
                                }

                                if (settings.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                                {
                                    int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                    if (need > 0)
                                    {
                                        await Task.Delay(1000, ct);
                                        SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket);
                                        Logger.Info("TownLoop", $"Buying {need} universal pills ({uniCode})");
                                        await Task.Delay(1000, ct);
                                        await StackItemAsync(uniCode, ct);
                                    }
                                }

                                if (settings.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                                {
                                    int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                    if (need > 0)
                                    {
                                        await Task.Delay(1000, ct);
                                        SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket);
                                        Logger.Info("TownLoop", $"Buying {need} purif pills ({purifCode})");
                                        await Task.Delay(1000, ct);
                                        await StackItemAsync(purifCode, ct);
                                    }
                                }

                                await CloseShopAsync(npcUID, ct);

                                Log("TownLoop", "Potion restock complete");
                            }
                        ),
                        #endregion

                        #region Restock grocery items
                        new TownTask(
                            Label: "Restock grocery items",
                            ApproachPosition: BotPosition.FromDisplayWorld(6487, 1072, 0),
                            NpcRefObjId: 2008,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuySpeedDrugs   ||
                                settings.Consumables.BuyReturnScrolls,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2008;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuySpeedDrugs &&
                                    ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                                {
                                    int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                    SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                if (settings.BuyReturnScrolls &&
                                    ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                                {
                                    int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                    SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);

                                Logger.Info("TownLoop", "Grocery restock complete");
                            }
                        ),
                        #endregion

                        #region Restock stable items
                        new TownTask(
                            Label: "Restock stable items",
                            ApproachPosition: BotPosition.FromDisplayWorld(6378, 1003, 0),
                            NpcRefObjId: 2009,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuyRecKits   ||
                                settings.Consumables.BuyHGPPotions,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2009;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuyRecKits &&
                                    ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                                {
                                    int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                    SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                if (settings.BuyHGPPotions)
                                {
                                    int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                    SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);

                                Logger.Info("TownLoop", "Stable restock complete");
                            }
                        ),
                        #endregion
                    }
                },

                ["Donwhang"] = new TownDefinition
                {
                    TownName = "Donwhang",
                    CenterPosition = BotPosition.FromDisplayWorld(3548, 2066, -106),
                    Tasks = new List<TownTask>
                    {
                        new TownTask(
                            Label: "Blacksmith maintenance",
                            ApproachPosition: BotPosition.FromDisplayWorld(3564, 2042, -106),
                            NpcRefObjId: 2051,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Maintenance.RepairWeapon ||
                                settings.Consumables.BuyAmmo,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2051;

                                if (settings.Maintenance.RepairWeapon)
                                {
                                    await SelectNpcAsync(npcUID, ct);

                                    var repair = new Packet(0x703E);
                                    repair.WriteUInt(npcUID);
                                    repair.WriteByte(0x02);
                                    sendPacket(repair);

                                    await Task.Delay(1500, ct);

                                    session._attackLoop?.ClearRepairFlag();
                                    Logger.Info("TownLoop", "Repair all sent");
                                }

                                if (settings.Consumables.BuyAmmo)
                                {
                                    if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                    {
                                        int have = inv.Slots.Values.Where(i => i.CodeName128 == ammoCode).Sum(i => i.Stack);
                                        int need = settings.Consumables.AmmoRefillAmount - have;
                                        Logger.Info("TownLoop", $"Ammo check: code={ammoCode} have={have} refillAt={settings.Consumables.AmmoRefillAmount} need={need}");

                                        if (need > 0)
                                        {
                                            await OpenShopAsync(npcUID, ct);
                                            SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);
                                            await Task.Delay(300, ct);
                                            await StackItemAsync(ammoCode, ct);
                                            await CloseShopAsync(npcUID, ct);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warn("TownLoop", $"AmmoType '{settings.Consumables.AmmoType}' not found in ConsumableCodeNames.Ammunition");
                                    }
                                }

                                Logger.Info("TownLoop", "Blacksmith maintenance complete");
                            }
                        ),

                        #region Restock potions
                        new TownTask(
                            Label: "Restock potions",
                            ApproachPosition: BotPosition.FromDisplayWorld(3520, 2033, -106),
                            NpcRefObjId: 2053,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                            {
                                var inv = session.Inventory;

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.Consumables.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.Consumables.HPType, out var hpCode) &&
                                    CurrentStack(hpCode) < settings.Consumables.HpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.Consumables.MPType, out var mpCode) &&
                                    CurrentStack(mpCode) < settings.Consumables.MpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode) &&
                                    CurrentStack(vigCode) < settings.Consumables.VigorPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.Consumables.UniPillType, out var uniCode) &&
                                    CurrentStack(uniCode) < settings.Consumables.UniversalPillsRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.Consumables.PurificationPillType, out var purifCode) &&
                                    CurrentStack(purifCode) < settings.Consumables.PurifPillsRefillAmount)
                                    return true;

                                return false;
                            },

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2053; // TODO: set NPC RefObjId for Donwhang potion vendor

                                await OpenShopAsync(npcUID, ct);

                                var stackSnapshot = inv.Slots.Values.ToList()
                                    .GroupBy(i => i.CodeName128)
                                    .ToDictionary(g => g.Key!, g => g.Sum(i => i.Stack));

                                int CurrentStack(string codeName) =>
                                    stackSnapshot.TryGetValue(codeName, out var n) ? n : 0;

                                if (settings.BuyHpPotions && ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                                {
                                    int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} HP potions ({hpCode})"); await Task.Delay(1000, ct); await StackItemAsync(hpCode, ct); }
                                }
                                if (settings.BuyMpPotions && ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                                {
                                    int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} MP potions ({mpCode})"); await Task.Delay(1000, ct); await StackItemAsync(mpCode, ct); }
                                }
                                if (settings.BuyVigorPotions && ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                                {
                                    int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} vigor potions ({vigCode})"); await Task.Delay(1000, ct); await StackItemAsync(vigCode, ct); }
                                }
                                if (settings.BuyUniversalPills && ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                                {
                                    int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} universal pills ({uniCode})"); await Task.Delay(1000, ct); await StackItemAsync(uniCode, ct); }
                                }
                                if (settings.BuyPurifPills && ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                                {
                                    int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} purif pills ({purifCode})"); await Task.Delay(1000, ct); await StackItemAsync(purifCode, ct); }
                                }

                                await CloseShopAsync(npcUID, ct);
                                Log("TownLoop", "Potion restock complete");
                            }
                        ),
                        #endregion

                        #region Restock grocery items
                        new TownTask(
                            Label: "Restock grocery items",
                            ApproachPosition: BotPosition.FromDisplayWorld(3520, 1994, -104),
                            NpcRefObjId: 2054,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuySpeedDrugs ||
                                settings.Consumables.BuyReturnScrolls,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2054;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuySpeedDrugs && ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                                {
                                    int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                    SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyReturnScrolls && ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                                {
                                    int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                    SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Grocery restock complete");
                            }
                        ),
                        #endregion

                        #region Restock stable items
                        new TownTask(
                            Label: "Restock stable items",
                            ApproachPosition: BotPosition.FromDisplayWorld(3595, 2092, -106),
                            NpcRefObjId: 2055,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuyRecKits ||
                                settings.Consumables.BuyHGPPotions,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2055;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuyRecKits && ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                                {
                                    int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                    SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyHGPPotions)
                                {
                                    int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                    SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Stable restock complete");
                            }
                        ),
                        #endregion
                    }
                },

                ["Hotan Palace"] = new TownDefinition
                {
                    TownName = "Hotan Palace",
                    CenterPosition = BotPosition.FromDisplayWorld(96, 57, 244),
                    Tasks = new List<TownTask>
                    {
                        new TownTask(
                            Label: "Blacksmith maintenance",
                            ApproachPosition: BotPosition.FromDisplayWorld(55, 70, 243),
                            NpcRefObjId: 2072,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Maintenance.RepairWeapon ||
                                settings.Consumables.BuyAmmo,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2072;

                                if (settings.Maintenance.RepairWeapon)
                                {
                                    await SelectNpcAsync(npcUID, ct);

                                    var repair = new Packet(0x703E);
                                    repair.WriteUInt(npcUID);
                                    repair.WriteByte(0x02);
                                    sendPacket(repair);

                                    await Task.Delay(1500, ct);

                                    session._attackLoop?.ClearRepairFlag();
                                    Logger.Info("TownLoop", "Repair all sent");
                                }

                                if (settings.Consumables.BuyAmmo)
                                {
                                    if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                    {
                                        int have = inv.Slots.Values.Where(i => i.CodeName128 == ammoCode).Sum(i => i.Stack);
                                        int need = settings.Consumables.AmmoRefillAmount - have;
                                        Logger.Info("TownLoop", $"Ammo check: code={ammoCode} have={have} refillAt={settings.Consumables.AmmoRefillAmount} need={need}");

                                        if (need > 0)
                                        {
                                            await OpenShopAsync(npcUID, ct);
                                            SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);
                                            await Task.Delay(300, ct);
                                            await StackItemAsync(ammoCode, ct);
                                            await CloseShopAsync(npcUID, ct);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warn("TownLoop", $"AmmoType '{settings.Consumables.AmmoType}' not found in ConsumableCodeNames.Ammunition");
                                    }
                                }

                                Logger.Info("TownLoop", "Blacksmith maintenance complete");
                            }
                        ),

                        #region Restock potions
                        new TownTask(
                            Label: "Restock potions",
                            ApproachPosition: BotPosition.FromDisplayWorld(88, 104, 243),
                            NpcRefObjId: 2074,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                            {
                                var inv = session.Inventory;

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.Consumables.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.Consumables.HPType, out var hpCode) &&
                                    CurrentStack(hpCode) < settings.Consumables.HpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.Consumables.MPType, out var mpCode) &&
                                    CurrentStack(mpCode) < settings.Consumables.MpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode) &&
                                    CurrentStack(vigCode) < settings.Consumables.VigorPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.Consumables.UniPillType, out var uniCode) &&
                                    CurrentStack(uniCode) < settings.Consumables.UniversalPillsRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.Consumables.PurificationPillType, out var purifCode) &&
                                    CurrentStack(purifCode) < settings.Consumables.PurifPillsRefillAmount)
                                    return true;

                                return false;
                            },

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2074;

                                await OpenShopAsync(npcUID, ct);

                                var stackSnapshot = inv.Slots.Values.ToList()
                                    .GroupBy(i => i.CodeName128)
                                    .ToDictionary(g => g.Key!, g => g.Sum(i => i.Stack));

                                int CurrentStack(string codeName) =>
                                    stackSnapshot.TryGetValue(codeName, out var n) ? n : 0;

                                if (settings.BuyHpPotions && ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                                {
                                    int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} HP potions ({hpCode})"); await Task.Delay(1000, ct); await StackItemAsync(hpCode, ct); }
                                }
                                if (settings.BuyMpPotions && ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                                {
                                    int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} MP potions ({mpCode})"); await Task.Delay(1000, ct); await StackItemAsync(mpCode, ct); }
                                }
                                if (settings.BuyVigorPotions && ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                                {
                                    int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} vigor potions ({vigCode})"); await Task.Delay(1000, ct); await StackItemAsync(vigCode, ct); }
                                }
                                if (settings.BuyUniversalPills && ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                                {
                                    int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} universal pills ({uniCode})"); await Task.Delay(1000, ct); await StackItemAsync(uniCode, ct); }
                                }
                                if (settings.BuyPurifPills && ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                                {
                                    int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} purif pills ({purifCode})"); await Task.Delay(1000, ct); await StackItemAsync(purifCode, ct); }
                                }

                                await CloseShopAsync(npcUID, ct);
                                Log("TownLoop", "Potion restock complete");
                            }
                        ),
                        #endregion

                        #region Restock grocery items
                        new TownTask(
                            Label: "Restock grocery items",
                            ApproachPosition: BotPosition.FromDisplayWorld(90, 2, 243),
                            NpcRefObjId: 2075,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuySpeedDrugs ||
                                settings.Consumables.BuyReturnScrolls,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2075;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuySpeedDrugs && ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                                {
                                    int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                    SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyReturnScrolls && ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                                {
                                    int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                    SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Grocery restock complete");
                            }
                        ),
                        #endregion

                        #region Restock stable items
                        new TownTask(
                            Label: "Restock stable items",
                            ApproachPosition: BotPosition.FromDisplayWorld(148, 1, 243),
                            NpcRefObjId: 2076,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuyRecKits ||
                                settings.Consumables.BuyHGPPotions,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 2076;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuyRecKits && ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                                {
                                    int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                    SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyHGPPotions)
                                {
                                    int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                    SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Stable restock complete");
                            }
                        ),
                        #endregion
                    }
                },

                ["Samarakand"] = new TownDefinition
                {
                    TownName = "Samarakand",
                    CenterPosition = BotPosition.FromDisplayWorld(-5194, 2956, 180),
                    Tasks = new List<TownTask>
                    {
                        new TownTask(
                            Label: "Blacksmith maintenance",
                            ApproachPosition: BotPosition.FromDisplayWorld(-5191, 2953, 180),
                            NpcRefObjId: 7530,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Maintenance.RepairWeapon ||
                                settings.Consumables.BuyAmmo,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7530;

                                if (settings.Maintenance.RepairWeapon)
                                {
                                    await SelectNpcAsync(npcUID, ct);

                                    var repair = new Packet(0x703E);
                                    repair.WriteUInt(npcUID);
                                    repair.WriteByte(0x02);
                                    sendPacket(repair);

                                    await Task.Delay(1500, ct);

                                    session._attackLoop?.ClearRepairFlag();
                                    Logger.Info("TownLoop", "Repair all sent");
                                }

                                if (settings.Consumables.BuyAmmo)
                                {
                                    if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                    {
                                        int have = inv.Slots.Values.Where(i => i.CodeName128 == ammoCode).Sum(i => i.Stack);
                                        int need = settings.Consumables.AmmoRefillAmount - have;
                                        Logger.Info("TownLoop", $"Ammo check: code={ammoCode} have={have} refillAt={settings.Consumables.AmmoRefillAmount} need={need}");

                                        if (need > 0)
                                        {
                                            await OpenShopAsync(npcUID, ct);
                                            SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);
                                            await Task.Delay(300, ct);
                                            await StackItemAsync(ammoCode, ct);
                                            await CloseShopAsync(npcUID, ct);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warn("TownLoop", $"AmmoType '{settings.Consumables.AmmoType}' not found in ConsumableCodeNames.Ammunition");
                                    }
                                }

                                Logger.Info("TownLoop", "Blacksmith maintenance complete");
                            }
                        ),

                        #region Restock potions
                        new TownTask(
                            Label: "Restock potions",
                            ApproachPosition: BotPosition.FromDisplayWorld(-5228, 2876, 180),
                            NpcRefObjId: 7532,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                            {
                                var inv = session.Inventory;

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.Consumables.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.Consumables.HPType, out var hpCode) &&
                                    CurrentStack(hpCode) < settings.Consumables.HpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.Consumables.MPType, out var mpCode) &&
                                    CurrentStack(mpCode) < settings.Consumables.MpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode) &&
                                    CurrentStack(vigCode) < settings.Consumables.VigorPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.Consumables.UniPillType, out var uniCode) &&
                                    CurrentStack(uniCode) < settings.Consumables.UniversalPillsRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.Consumables.PurificationPillType, out var purifCode) &&
                                    CurrentStack(purifCode) < settings.Consumables.PurifPillsRefillAmount)
                                    return true;

                                return false;
                            },

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7532;

                                await OpenShopAsync(npcUID, ct);

                                var stackSnapshot = inv.Slots.Values.ToList()
                                    .GroupBy(i => i.CodeName128)
                                    .ToDictionary(g => g.Key!, g => g.Sum(i => i.Stack));

                                int CurrentStack(string codeName) =>
                                    stackSnapshot.TryGetValue(codeName, out var n) ? n : 0;

                                if (settings.BuyHpPotions && ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                                {
                                    int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} HP potions ({hpCode})"); await Task.Delay(1000, ct); await StackItemAsync(hpCode, ct); }
                                }
                                if (settings.BuyMpPotions && ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                                {
                                    int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} MP potions ({mpCode})"); await Task.Delay(1000, ct); await StackItemAsync(mpCode, ct); }
                                }
                                if (settings.BuyVigorPotions && ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                                {
                                    int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} vigor potions ({vigCode})"); await Task.Delay(1000, ct); await StackItemAsync(vigCode, ct); }
                                }
                                if (settings.BuyUniversalPills && ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                                {
                                    int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} universal pills ({uniCode})"); await Task.Delay(1000, ct); await StackItemAsync(uniCode, ct); }
                                }
                                if (settings.BuyPurifPills && ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                                {
                                    int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} purif pills ({purifCode})"); await Task.Delay(1000, ct); await StackItemAsync(purifCode, ct); }
                                }

                                await CloseShopAsync(npcUID, ct);
                                Log("TownLoop", "Potion restock complete");
                            }
                        ),
                        #endregion

                        #region Restock grocery items
                        new TownTask(
                            Label: "Restock grocery items",
                            ApproachPosition: BotPosition.FromDisplayWorld(-5208, 2837, 180),
                            NpcRefObjId: 7533,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuySpeedDrugs ||
                                settings.Consumables.BuyReturnScrolls,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7533;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuySpeedDrugs && ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                                {
                                    int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                    SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyReturnScrolls && ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                                {
                                    int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                    SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Grocery restock complete");
                            }
                        ),
                        #endregion

                        #region Restock stable items
                        new TownTask(
                            Label: "Restock stable items",
                            ApproachPosition: BotPosition.FromDisplayWorld(-5115, 2896, 180),
                            NpcRefObjId: 7534,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuyRecKits ||
                                settings.Consumables.BuyHGPPotions,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7534;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuyRecKits && ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                                {
                                    int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                    SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyHGPPotions)
                                {
                                    int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                    SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Stable restock complete");
                            }
                        ),
                        #endregion
                    }
                },

                ["Constantinople"] = new TownDefinition
                {
                    TownName = "Constantinople",
                    CenterPosition = BotPosition.FromDisplayWorld(-10673, 2644, 83),
                    Tasks = new List<TownTask>
                    {
                        new TownTask(
                            Label: "Blacksmith maintenance",
                            ApproachPosition: BotPosition.FromDisplayWorld(-10673, 2640, 84),
                            NpcRefObjId: 7495,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Maintenance.RepairWeapon ||
                                settings.Consumables.BuyAmmo,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7495;

                                if (settings.Maintenance.RepairWeapon)
                                {
                                    await SelectNpcAsync(npcUID, ct);

                                    var repair = new Packet(0x703E);
                                    repair.WriteUInt(npcUID);
                                    repair.WriteByte(0x02);
                                    sendPacket(repair);

                                    await Task.Delay(1500, ct);

                                    session._attackLoop?.ClearRepairFlag();
                                    Logger.Info("TownLoop", "Repair all sent");
                                }

                                if (settings.Consumables.BuyAmmo)
                                {
                                    if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                    {
                                        int have = inv.Slots.Values.Where(i => i.CodeName128 == ammoCode).Sum(i => i.Stack);
                                        int need = settings.Consumables.AmmoRefillAmount - have;
                                        Logger.Info("TownLoop", $"Ammo check: code={ammoCode} have={have} refillAt={settings.Consumables.AmmoRefillAmount} need={need}");

                                        if (need > 0)
                                        {
                                            await OpenShopAsync(npcUID, ct);
                                            SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);
                                            await Task.Delay(300, ct);
                                            await StackItemAsync(ammoCode, ct);
                                            await CloseShopAsync(npcUID, ct);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warn("TownLoop", $"AmmoType '{settings.Consumables.AmmoType}' not found in ConsumableCodeNames.Ammunition");
                                    }
                                }

                                Logger.Info("TownLoop", "Blacksmith maintenance complete");
                            }
                        ),

                        #region Restock potions
                        new TownTask(
                            Label: "Restock potions",
                            ApproachPosition: BotPosition.FromDisplayWorld(-10623, 2635, 81),
                            NpcRefObjId: 7497,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                            {
                                var inv = session.Inventory;

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.Consumables.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.Consumables.HPType, out var hpCode) &&
                                    CurrentStack(hpCode) < settings.Consumables.HpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.Consumables.MPType, out var mpCode) &&
                                    CurrentStack(mpCode) < settings.Consumables.MpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode) &&
                                    CurrentStack(vigCode) < settings.Consumables.VigorPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.Consumables.UniPillType, out var uniCode) &&
                                    CurrentStack(uniCode) < settings.Consumables.UniversalPillsRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.Consumables.PurificationPillType, out var purifCode) &&
                                    CurrentStack(purifCode) < settings.Consumables.PurifPillsRefillAmount)
                                    return true;

                                return false;
                            },

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7497;

                                await OpenShopAsync(npcUID, ct);

                                var stackSnapshot = inv.Slots.Values.ToList()
                                    .GroupBy(i => i.CodeName128)
                                    .ToDictionary(g => g.Key!, g => g.Sum(i => i.Stack));

                                int CurrentStack(string codeName) =>
                                    stackSnapshot.TryGetValue(codeName, out var n) ? n : 0;

                                if (settings.BuyHpPotions && ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                                {
                                    int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} HP potions ({hpCode})"); await Task.Delay(1000, ct); await StackItemAsync(hpCode, ct); }
                                }
                                if (settings.BuyMpPotions && ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                                {
                                    int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} MP potions ({mpCode})"); await Task.Delay(1000, ct); await StackItemAsync(mpCode, ct); }
                                }
                                if (settings.BuyVigorPotions && ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                                {
                                    int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} vigor potions ({vigCode})"); await Task.Delay(1000, ct); await StackItemAsync(vigCode, ct); }
                                }
                                if (settings.BuyUniversalPills && ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                                {
                                    int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} universal pills ({uniCode})"); await Task.Delay(1000, ct); await StackItemAsync(uniCode, ct); }
                                }
                                if (settings.BuyPurifPills && ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                                {
                                    int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} purif pills ({purifCode})"); await Task.Delay(1000, ct); await StackItemAsync(purifCode, ct); }
                                }

                                await CloseShopAsync(npcUID, ct);
                                Log("TownLoop", "Potion restock complete");
                            }
                        ),
                        #endregion

                        #region Restock grocery items
                        new TownTask(
                            Label: "Restock grocery items",
                            ApproachPosition: BotPosition.FromDisplayWorld(-10682, 2528, 80),
                            NpcRefObjId: 7498,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuySpeedDrugs ||
                                settings.Consumables.BuyReturnScrolls,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7498;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuySpeedDrugs && ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                                {
                                    int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                    SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyReturnScrolls && ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                                {
                                    int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                    SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Grocery restock complete");
                            }
                        ),
                        #endregion

                        #region Restock stable items
                        new TownTask(
                            Label: "Restock stable items",
                            ApproachPosition: BotPosition.FromDisplayWorld(-10759, 2531, 82),
                            NpcRefObjId: 7499,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuyRecKits ||
                                settings.Consumables.BuyHGPPotions,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 7499;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuyRecKits && ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                                {
                                    int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                    SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyHGPPotions)
                                {
                                    int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                    SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Stable restock complete");
                            }
                        ),
                        #endregion
                    }
                },

                ["Alexandria City (S)"] = new TownDefinition
                {
                    TownName = "Alexandria City (S)",
                    CenterPosition = BotPosition.FromDisplayWorld(-16733, -271, 863),
                    Tasks = new List<TownTask>
                    {
                        new TownTask(
                            Label: "Blacksmith maintenance",
                            ApproachPosition: BotPosition.FromDisplayWorld(-16734, -274, 863),
                            NpcRefObjId: 26791,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Maintenance.RepairWeapon ||
                                settings.Consumables.BuyAmmo,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 26791;

                                if (settings.Maintenance.RepairWeapon)
                                {
                                    await SelectNpcAsync(npcUID, ct);

                                    var repair = new Packet(0x703E);
                                    repair.WriteUInt(npcUID);
                                    repair.WriteByte(0x02);
                                    sendPacket(repair);

                                    await Task.Delay(1500, ct);

                                    session._attackLoop?.ClearRepairFlag();
                                    Logger.Info("TownLoop", "Repair all sent");
                                }

                                if (settings.Consumables.BuyAmmo)
                                {
                                    if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                    {
                                        int have = inv.Slots.Values.Where(i => i.CodeName128 == ammoCode).Sum(i => i.Stack);
                                        int need = settings.Consumables.AmmoRefillAmount - have;
                                        Logger.Info("TownLoop", $"Ammo check: code={ammoCode} have={have} refillAt={settings.Consumables.AmmoRefillAmount} need={need}");

                                        if (need > 0)
                                        {
                                            await OpenShopAsync(npcUID, ct);
                                            SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);
                                            await Task.Delay(300, ct);
                                            await StackItemAsync(ammoCode, ct);
                                            await CloseShopAsync(npcUID, ct);
                                        }
                                    }
                                    else
                                    {
                                        Logger.Warn("TownLoop", $"AmmoType '{settings.Consumables.AmmoType}' not found in ConsumableCodeNames.Ammunition");
                                    }
                                }

                                Logger.Info("TownLoop", "Blacksmith maintenance complete");
                            }
                        ),

                        #region Restock potions
                        new TownTask(
                            Label: "Restock potions",
                            ApproachPosition: BotPosition.FromDisplayWorld(-16631, -347, 863),
                            NpcRefObjId: 26792,

                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                            {
                                var inv = session.Inventory;

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.Consumables.BuyHpPotions &&
                                    ConsumableCodeNames.HpPotions.TryGetValue(settings.Consumables.HPType, out var hpCode) &&
                                    CurrentStack(hpCode) < settings.Consumables.HpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyMpPotions &&
                                    ConsumableCodeNames.MpPotions.TryGetValue(settings.Consumables.MPType, out var mpCode) &&
                                    CurrentStack(mpCode) < settings.Consumables.MpPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyVigorPotions &&
                                    ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode) &&
                                    CurrentStack(vigCode) < settings.Consumables.VigorPotionRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyUniversalPills &&
                                    ConsumableCodeNames.UniversalPills.TryGetValue(settings.Consumables.UniPillType, out var uniCode) &&
                                    CurrentStack(uniCode) < settings.Consumables.UniversalPillsRefillAmount)
                                    return true;

                                if (settings.Consumables.BuyPurifPills &&
                                    ConsumableCodeNames.PurificationPills.TryGetValue(settings.Consumables.PurificationPillType, out var purifCode) &&
                                    CurrentStack(purifCode) < settings.Consumables.PurifPillsRefillAmount)
                                    return true;

                                return false;
                            },

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 26792;

                                await OpenShopAsync(npcUID, ct);

                                var stackSnapshot = inv.Slots.Values.ToList()
                                    .GroupBy(i => i.CodeName128)
                                    .ToDictionary(g => g.Key!, g => g.Sum(i => i.Stack));

                                int CurrentStack(string codeName) =>
                                    stackSnapshot.TryGetValue(codeName, out var n) ? n : 0;

                                if (settings.BuyHpPotions && ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                                {
                                    int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} HP potions ({hpCode})"); await Task.Delay(1000, ct); await StackItemAsync(hpCode, ct); }
                                }
                                if (settings.BuyMpPotions && ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                                {
                                    int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} MP potions ({mpCode})"); await Task.Delay(1000, ct); await StackItemAsync(mpCode, ct); }
                                }
                                if (settings.BuyVigorPotions && ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                                {
                                    int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} vigor potions ({vigCode})"); await Task.Delay(1000, ct); await StackItemAsync(vigCode, ct); }
                                }
                                if (settings.BuyUniversalPills && ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                                {
                                    int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} universal pills ({uniCode})"); await Task.Delay(1000, ct); await StackItemAsync(uniCode, ct); }
                                }
                                if (settings.BuyPurifPills && ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                                {
                                    int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                    if (need > 0) { await Task.Delay(1000, ct); SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket); Logger.Info("TownLoop", $"Buying {need} purif pills ({purifCode})"); await Task.Delay(1000, ct); await StackItemAsync(purifCode, ct); }
                                }

                                await CloseShopAsync(npcUID, ct);
                                Log("TownLoop", "Potion restock complete");
                            }
                        ),
                        #endregion

                        #region Restock grocery items
                        new TownTask(
                            Label: "Restock grocery items",
                            ApproachPosition: BotPosition.FromDisplayWorld(-16583, -274, 863),
                            NpcRefObjId: 26824,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuySpeedDrugs ||
                                settings.Consumables.BuyReturnScrolls,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 26824;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuySpeedDrugs && ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                                {
                                    int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                    SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyReturnScrolls && ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                                {
                                    int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                    SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Grocery restock complete");
                            }
                        ),
                        #endregion

                        #region Restock stable items
                        new TownTask(
                            Label: "Restock stable items",
                            ApproachPosition: BotPosition.FromDisplayWorld(-16431, -213, 856),
                            NpcRefObjId: 26798,
                            ShouldRun: (BotSettings settings, VSRO_CONTROL_API.VSRO.DTO.ISession session) =>
                                settings.Consumables.BuyRecKits ||
                                settings.Consumables.BuyHGPPotions,

                            Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket, CancellationToken ct) =>
                            {
                                var settings = session._botSettings!.Consumables;
                                var inv = session.Inventory!;
                                const int npcRefObjId = 26798;

                                await OpenShopAsync(npcUID, ct);

                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                                if (settings.BuyRecKits && ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                                {
                                    int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                    SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }
                                if (settings.BuyHGPPotions)
                                {
                                    int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                    SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                    await Task.Delay(300, ct);
                                }

                                await CloseShopAsync(npcUID, ct);
                                Logger.Info("TownLoop", "Stable restock complete");
                            }
                        ),
                        #endregion
                    }
                },
            };
        }

        /// <summary>
        /// Resolves which town to use based on the current region,
        /// walks to center, then runs each applicable task in order.
        /// Returns true if all tasks completed, false if aborted.
        /// </summary>
        public async Task<bool> RunAsync(CancellationToken ct)
        {
            _session.BotShopOpen = false;

            // Wait for walk to finish
            var currentRegion = RegionResolver.Resolve((short)_session.RegionId);

            if (!RegionToTown.TryGetValue(currentRegion, out var townName))
            {
                Logger.Warn("TownLoop", $"No town mapping for region '{currentRegion}' — aborting");
                return false;
            }

            if (!Towns.TryGetValue(townName, out var town))
            {
                Logger.Warn("TownLoop", $"No TownDefinition for '{townName}' — aborting");
                return false;
            }

            Logger.Info("TownLoop", $"Running town loop for {town.TownName}");

            try
            {
                // Walk to town center first
                Logger.Info("TownLoop", $"Walking to {town.TownName} center");
                await _walker.WalkTo(town.CenterPosition, ct, _session);

                // Run each task
                var settings = _getSettings();

                foreach (var task in town.Tasks)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!task.ShouldRun(settings, _session))
                    {
                        Log("TownLoop", $"Task '{task.Label}' skipped (condition false)");
                        continue;
                    }

                    Logger.Info("TownLoop", $"Task '{task.Label}' — walking to approach");

                    await _walker.WalkTo(task.ApproachPosition, ct, _session);
                    await Task.Delay(3000);
                    // Find NPC UID from SpawnedObjects by RefObjId
                    uint npcUID = await FindNpcUidAsync(task.NpcRefObjId, ct);
                    if (npcUID == 0)
                    {
                        Logger.Warn("TownLoop", $"Task '{task.Label}' — NPC RefObjId={task.NpcRefObjId} not found after {NPC_FIND_TIMEOUT_MS}ms, skipping");
                        continue;
                    }

                    Logger.Info("TownLoop", $"Task '{task.Label}' — found NPC UID={npcUID}, executing");

                    try
                    {
                        await task.Execute(_session, npcUID, _sendPacket, ct);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        Logger.Error("TownLoop", $"Task '{task.Label}' execute failed: {ex.Message}");
                    }
                    finally
                    {
                        // Guarantee flag is cleared even if Execute throws
                        if (_session.BotShopOpen)
                        {
                            Logger.Warn("TownLoop", $"Task '{task.Label}' ended with BotShopOpen=true — force clearing");
                            _session.BotShopOpen = false;
                        }
                    }
                }
            }
            finally
            {
                // Final net
                if (_session.BotShopOpen)
                {
                    Logger.Warn("TownLoop", "RunAsync finally: BotShopOpen was still true — force clearing");
                    _session.BotShopOpen = false;
                }
            }

            Logger.Info("TownLoop", $"Town loop complete for {town.TownName}");
            return true;
        }

        private async Task<uint> FindNpcUidAsync(uint refObjId, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(NPC_FIND_TIMEOUT_MS);

            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                var match = _session.SpawnedObjects
                    .FirstOrDefault(kvp => kvp.Value.RefObjID == refObjId);

                if (match.Key != 0)
                    return match.Key;

                await Task.Delay(NPC_FIND_POLL_MS, ct);
            }

            return 0;
        }

        public BotPosition? GetTownCenterForRegion(string resolvedRegion)
        {
            if (!RegionToTown.TryGetValue(resolvedRegion, out var townName)) return null;
            if (!Towns.TryGetValue(townName, out var town)) return null;
            return town.CenterPosition;
        }

        /// <summary>
        /// Buys up to <paramref name="quantity"/> of an item from the targeted NPC.
        /// Handles the 0x7034 packet structure: type=0x08, tab, slot, qty (ushort), npcUID (uint).
        /// </summary>
        private static void SendBuyPacket(
            string codeName,
            int npcRefObjId,
            uint npcUID,
            int quantity,
            Action<Packet> sendPacket)
        {
            if (quantity <= 0)
            {
                Logger.Info("TownLoop::Buy", $"Skipping {codeName} — need={quantity} (already at or above refill amount)");
                return;
            }

            var found = Overseer.FindShopSlot(npcRefObjId, codeName);
            if (found is null)
            {
                Logger.Warn("TownLoop::Buy", $"Item '{codeName}' not found in NPC shop {npcRefObjId} — ShopLookup has {Overseer.ShopLookup.Count} entries total");
                return;
            }

            var (tab, slot) = found.Value;
            int clamped = Math.Min(quantity, ushort.MaxValue);

            var pkt = new Packet(0x7034);
            pkt.WriteByte(0x08);            // movement type: ShopToInventory
            pkt.WriteByte((byte)tab);       // shop tab index (0-based)
            pkt.WriteByte((byte)slot);      // shop slot index
            pkt.WriteUShort((ushort)clamped);
            pkt.WriteUInt(npcUID);          // NPC spawn UID

            sendPacket(pkt);
            Logger.Info("TownLoop::Buy", $"Buying '{codeName}' x{clamped} tab={tab} slot={slot} npc=0x{npcUID:X}");
        }

        private async Task<bool> SendMoveAndWait(byte source, byte dest, ushort qty, CancellationToken ct, int timeoutMs = 3000)
        {
            ct.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<bool>();
            _proxy.PendingMoves[source] = tcs;

            var pkt = new Packet(0x7034);
            pkt.WriteByte(0x00);
            pkt.WriteByte(source);
            pkt.WriteByte(dest);
            pkt.WriteUShort(qty);
            _proxy.Server.Send(pkt);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, ct));
            _proxy.PendingMoves.Remove(source);

            if (ct.IsCancellationRequested) return false;
            if (completed == tcs.Task) return tcs.Task.Result;
            return false;
        }

        private async Task StackItemAsync(string codeName, CancellationToken ct)
        {
            bool merged = true;
            while (merged)
            {
                ct.ThrowIfCancellationRequested();
                merged = false;

                var slots = _session.Inventory.Slots
                    .Where(kvp => kvp.Key >= 13 && kvp.Value.CodeName128 == codeName)
                    .OrderBy(kvp => kvp.Key)
                    .ToList();

                for (int i = 0; i < slots.Count; i++)
                {
                    var (slotA, itemA) = slots[i];
                    if (itemA.MaxStack <= 1) continue;
                    if (itemA.Stack >= itemA.MaxStack) continue;

                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        ct.ThrowIfCancellationRequested();

                        var (slotB, itemB) = slots[j];
                        if (itemB.Stack <= 0) continue;

                        ushort qty = (ushort)Math.Min(itemB.Stack, itemA.MaxStack - itemA.Stack);
                        bool moved = await SendMoveAndWait(slotB, slotA, qty, ct);
                        if (moved)
                        {
                            await Task.Delay(300, ct);
                            merged = true;
                            break;
                        }
                    }

                    if (merged) break;
                }
            }
        }
        private async Task SelectNpcAsync(uint npcUID, CancellationToken ct)
        {
            var select = new Packet(0x7045);
            select.WriteUInt(npcUID);
            _sendPacket(select);

            await Task.Delay(300, ct);
        }

    }
}