using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
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
        Func<BotSettings, bool> ShouldRun,
        Func<VSRO_CONTROL_API.VSRO.DTO.ISession, uint, Action<Packet>, Task> Execute
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
        // =====================================================================
        // Town definitions — one entry per town.
        // CenterPosition: where the AutoWalker walks before any NPC tasks run.
        // Tasks: ordered list of NPC interactions.
        // Add approach positions and NPC RefObjIds when you have the coords.
        // =====================================================================
        private readonly Dictionary<string, TownDefinition> Towns = new()
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

                        ShouldRun: (BotSettings settings) =>
                            settings.Maintenance.RepairWeapon ||
                            settings.Consumables.BuyAmmo,

                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            var settings = session._botSettings!;
                            var inv = session.Inventory!;
                            const int npcRefObjId = 2003;

                            // Select blacksmith
                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);

                            await Task.Delay(500, CancellationToken.None);

                            // Repair equipment
                            if (settings.Maintenance.RepairWeapon)
                            {
                                var repair = new Packet(0x703E);
                                repair.WriteUInt(npcUID);
                                repair.WriteByte(0x02);
                                sendPacket(repair);

                                await Task.Delay(1000, CancellationToken.None);

                                session._attackLoop?.ClearRepairFlag();

                                Logger.Info("TownLoop", "Repair all sent");
                            }

                            // Restock ammunition
                            if (settings.Consumables.BuyAmmo)
                            {
                                int CurrentStack(string codeName) =>
                                    inv.Slots.Values
                                        .Where(i => i.CodeName128 == codeName)
                                        .Sum(i => i.Stack);

                                if (ConsumableCodeNames.Ammunition.TryGetValue(settings.Consumables.AmmoType, out var ammoCode))
                                {
                                    int need = settings.Consumables.AmmoRefillAmount - CurrentStack(ammoCode);

                                    if (need > 0)
                                    {
                                        SendBuyPacket(ammoCode, npcRefObjId, npcUID, need, sendPacket);

                                        await Task.Delay(300, CancellationToken.None);

                                        Logger.Info("TownLoop", $"Bought {need} ammo");
                                    }
                                }
                            }

                            Logger.Info("TownLoop", "Blacksmith maintenance complete");
                        }
                    ),
                    
                    // Potion
                    new TownTask(
                        Label: "Restock potions",
                        ApproachPosition: BotPosition.FromDisplayWorld(6486, 1112, 0),
                        NpcRefObjId: 2005,
                        ShouldRun: (BotSettings settings) =>
                            settings.Consumables.BuyHpPotions      ||
                            settings.Consumables.BuyMpPotions      ||
                            settings.Consumables.BuyVigorPotions   ||
                            settings.Consumables.BuyUniversalPills ||
                            settings.Consumables.BuyPurifPills,

                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            var settings = session._botSettings!.Consumables;
                            var inv = session.Inventory!;
                            const int npcRefObjId = 2005;

                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);
                            await Task.Delay(2500, CancellationToken.None);

                            int CurrentStack(string codeName) =>
                                inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                            if (settings.BuyHpPotions &&
                                ConsumableCodeNames.HpPotions.TryGetValue(settings.HPType, out var hpCode))
                            {
                                int need = settings.HpPotionRefillAmount - CurrentStack(hpCode);
                                SendBuyPacket(hpCode, npcRefObjId, npcUID, need, sendPacket);
                                Logger.Info("TownLoop", "HP potion restock complete");
                                await Task.Delay(750, CancellationToken.None);
                            }

                            if (settings.BuyMpPotions &&
                                ConsumableCodeNames.MpPotions.TryGetValue(settings.MPType, out var mpCode))
                            {
                                int need = settings.MpPotionRefillAmount - CurrentStack(mpCode);
                                SendBuyPacket(mpCode, npcRefObjId, npcUID, need, sendPacket);
                                Logger.Debug("TownLoop", "MP potion restock complete");
                                await Task.Delay(750, CancellationToken.None);
                            }

                            if (settings.BuyVigorPotions &&
                                ConsumableCodeNames.VigorPotions.TryGetValue(PotionType.Large, out var vigCode))
                            {
                                int need = settings.VigorPotionRefillAmount - CurrentStack(vigCode);
                                SendBuyPacket(vigCode, npcRefObjId, npcUID, need, sendPacket);
                                Logger.Debug("TownLoop", "Vigor potion restock complete");
                                await Task.Delay(750, CancellationToken.None);
                            }

                            if (settings.BuyUniversalPills &&
                                ConsumableCodeNames.UniversalPills.TryGetValue(settings.UniPillType, out var uniCode))
                            {
                                int need = settings.UniversalPillsRefillAmount - CurrentStack(uniCode);
                                SendBuyPacket(uniCode, npcRefObjId, npcUID, need, sendPacket);
                                Logger.Debug("TownLoop", "Univ. pill restock complete");
                                await Task.Delay(750, CancellationToken.None);
                            }

                            if (settings.BuyPurifPills &&
                                ConsumableCodeNames.PurificationPills.TryGetValue(settings.PurificationPillType, out var purifCode))
                            {
                                int need = settings.PurifPillsRefillAmount - CurrentStack(purifCode);
                                SendBuyPacket(purifCode, npcRefObjId, npcUID, need, sendPacket);
                                Logger.Debug("TownLoop", "Purif. pill restock complete");
                                await Task.Delay(750, CancellationToken.None);
                            }

                            Logger.Debug("TownLoop", "Potion restock complete");
                        }
                    ),

                    // Grocery
                    new TownTask(
                        Label: "Restock grocery items",
                        ApproachPosition: BotPosition.FromDisplayWorld(6487, 1072, 0),
                        NpcRefObjId: 2008,
                        ShouldRun: (BotSettings settings) =>
                            settings.Consumables.BuySpeedDrugs   ||
                            settings.Consumables.BuyReturnScrolls,

                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            var settings = session._botSettings!.Consumables;
                            var inv = session.Inventory!;
                            const int npcRefObjId = 2008;

                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);
                            await Task.Delay(500, CancellationToken.None);

                            int CurrentStack(string codeName) =>
                                inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                            if (settings.BuySpeedDrugs &&
                                ConsumableCodeNames.SpeedDrugs.TryGetValue(settings.DrugType, out var drugCode))
                            {
                                int need = settings.SpeedDrugsRefillAmount - CurrentStack(drugCode);
                                SendBuyPacket(drugCode, npcRefObjId, npcUID, need, sendPacket);
                                await Task.Delay(300, CancellationToken.None);
                            }

                            if (settings.BuyReturnScrolls &&
                                ConsumableCodeNames.ReturnScrolls.TryGetValue(settings.ReturnScrollType, out var scrollCode))
                            {
                                int need = settings.ReturnScrollRefillAmount - CurrentStack(scrollCode);
                                SendBuyPacket(scrollCode, npcRefObjId, npcUID, need, sendPacket);
                                await Task.Delay(300, CancellationToken.None);
                            }

                            Logger.Info("TownLoop", "Grocery restock complete");
                        }
                    ),

                    // Stables
                    new TownTask(
                        Label: "Restock stable items",
                        ApproachPosition: BotPosition.FromDisplayWorld(6378, 1003, 0),
                        NpcRefObjId: 2009,
                        ShouldRun: (BotSettings settings) =>
                            settings.Consumables.BuyRecKits   ||
                            settings.Consumables.BuyHGPPotions,

                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            var settings = session._botSettings!.Consumables;
                            var inv = session.Inventory!;
                            const int npcRefObjId = 2009; // TODO

                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);
                            await Task.Delay(500, CancellationToken.None);

                            int CurrentStack(string codeName) =>
                                inv.Slots.Values.Where(i => i.CodeName128 == codeName).Sum(i => i.Stack);

                            if (settings.BuyRecKits &&
                                ConsumableCodeNames.RecoveryKits.TryGetValue(settings.RecKitsType, out var recCode))
                            {
                                int need = settings.RecKitsRefillAmount - CurrentStack(recCode);
                                SendBuyPacket(recCode, npcRefObjId, npcUID, need, sendPacket);
                                await Task.Delay(300, CancellationToken.None);
                            }

                            if (settings.BuyHGPPotions)
                            {
                                int need = settings.HGPPotionsRefillAmount - CurrentStack(ConsumableCodeNames.HGPPotion);
                                SendBuyPacket(ConsumableCodeNames.HGPPotion, npcRefObjId, npcUID, need, sendPacket);
                                await Task.Delay(300, CancellationToken.None);
                            }

                            Logger.Info("TownLoop", "Stable restock complete");
                        }
                    ),

                }
            },

            ["Donwhang"] = new TownDefinition
            {
                TownName = "Donwhang",
                CenterPosition = BotPosition.FromDisplayWorld(3548, 2066, -106),
                Tasks = new List<TownTask>
                {
                    new TownTask(
                        Label: "Repair Weapons",
                        ApproachPosition: BotPosition.FromDisplayWorld(3564, 2042, -106),
                        NpcRefObjId: 2051,
                        ShouldRun: (BotSettings settings) => settings.Maintenance.RepairWeapon,
                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            // Select the blacksmith
                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);

                            await Task.Delay(500, CancellationToken.None);

                            // Send repair all
                            var repair = new Packet(0x703E);
                            repair.WriteUInt(npcUID);
                            repair.WriteByte(0x02);
                            sendPacket(repair);

                            // Wait for durability change packets confirming repair
                            await Task.Delay(1000, CancellationToken.None);

                            // Clear the repair flag
                            session._attackLoop?.ClearRepairFlag();

                            Logger.Info("TownLoop", "Repair all sent");
                        }
                    )
                }
            },

            ["Hotan Palace"] = new TownDefinition
            {
                TownName = "Hotan Palace",
                CenterPosition = BotPosition.FromDisplayWorld(96, 57, 244),
                Tasks = new List<TownTask>
                {
                    new TownTask(
                        Label: "Repair Weapons",
                        ApproachPosition: BotPosition.FromDisplayWorld(58, 72, 243),
                        NpcRefObjId: 2072,
                        ShouldRun: (BotSettings settings) => settings.Maintenance.RepairWeapon,
                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            // Select the blacksmith
                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);

                            await Task.Delay(500, CancellationToken.None);

                            // Send repair all
                            var repair = new Packet(0x703E);
                            repair.WriteUInt(npcUID);
                            repair.WriteByte(0x02);
                            sendPacket(repair);

                            // Wait for durability change packets confirming repair
                            await Task.Delay(1000, CancellationToken.None);

                            // Clear the repair flag
                            session._attackLoop?.ClearRepairFlag();

                            Logger.Info("TownLoop", "Repair all sent");
                        }
                    )
                }
            },

            ["Samarakand"] = new TownDefinition
            {
                TownName = "Samarakand",
                CenterPosition = BotPosition.FromDisplayWorld(-5157, 2831, 180),
                Tasks = new List<TownTask>
                {
                    new TownTask(
                        Label: "Repair Weapons",
                        ApproachPosition: BotPosition.FromDisplayWorld(-5191, 2953, 180),
                        NpcRefObjId: 7530,
                        ShouldRun: (BotSettings settings) => settings.Maintenance.RepairWeapon,
                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            // Select the blacksmith
                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);

                            await Task.Delay(500, CancellationToken.None);

                            // Send repair all
                            var repair = new Packet(0x703E);
                            repair.WriteUInt(npcUID);
                            repair.WriteByte(0x02);
                            sendPacket(repair);

                            // Wait for durability change packets confirming repair
                            await Task.Delay(1000, CancellationToken.None);

                            // Clear the repair flag
                            session._attackLoop?.ClearRepairFlag();

                            Logger.Info("TownLoop", "Repair all sent");
                        }
                    )
                }
            },

            ["Constantinople"] = new TownDefinition
            {
                TownName = "Constantinople",
                CenterPosition = BotPosition.FromDisplayWorld(-10636, 2606, 80),
                Tasks = new List<TownTask>
                {
                    new TownTask(
                        Label: "Repair Weapons",
                        ApproachPosition: BotPosition.FromDisplayWorld(-10673, 2640, 84),
                        NpcRefObjId: 7495,
                        ShouldRun: (BotSettings settings) => settings.Maintenance.RepairWeapon,
                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            // Select the blacksmith
                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);

                            await Task.Delay(500, CancellationToken.None);

                            // Send repair all
                            var repair = new Packet(0x703E);
                            repair.WriteUInt(npcUID);
                            repair.WriteByte(0x02);
                            sendPacket(repair);

                            // Wait for durability change packets confirming repair
                            await Task.Delay(1000, CancellationToken.None);

                            // Clear the repair flag
                            session._attackLoop?.ClearRepairFlag();

                            Logger.Info("TownLoop", "Repair all sent");
                        }
                    )
                }
            },

            ["Alexandria City (S)"] = new TownDefinition
            {
                TownName = "Alexandria City (S)",
                CenterPosition = BotPosition.FromDisplayWorld(-16621, -299, 863),
                Tasks = new List<TownTask>
                {
                    new TownTask(
                        Label: "Repair Weapons",
                        ApproachPosition: BotPosition.FromDisplayWorld(-16734, -274, 863),
                        NpcRefObjId: 26791,
                        ShouldRun: (BotSettings settings) => settings.Maintenance.RepairWeapon,
                        Execute: async (VSRO_CONTROL_API.VSRO.DTO.ISession session, uint npcUID, Action<Packet> sendPacket) =>
                        {
                            // Select the blacksmith
                            var select = new Packet(0x7045);
                            select.WriteUInt(npcUID);
                            sendPacket(select);

                            await Task.Delay(500, CancellationToken.None);

                            // Send repair all
                            var repair = new Packet(0x703E);
                            repair.WriteUInt(npcUID);
                            repair.WriteByte(0x02);
                            sendPacket(repair);

                            // Wait for durability change packets confirming repair
                            await Task.Delay(1000, CancellationToken.None);

                            // Clear the repair flag
                            session._attackLoop?.ClearRepairFlag();

                            Logger.Info("TownLoop", "Repair all sent");
                        }
                    )
                }
            },
        };

        // =====================================================================
        // Region -> Town name mapping.
        // Key = RegionResolver.Resolve() output (full region name).
        // Value = town name key into Towns dictionary above.
        // Regions that require a teleport back are handled by BotBrain before
        // TownLoop is ever called — by the time RunAsync fires, we are in
        // the correct region and just need to walk to center.
        // =====================================================================
        private static readonly Dictionary<string, string> RegionToTown = new()
        {
            ["China"] = "Jangan",
            ["Donwhang"] = "Donwhang",          // West_China maps to "Donwhang"
            ["Hotan"] = "Hotan Palace",       // Oasis_Kingdom maps to "Hotan"
            ["Samarakand (Central Asia)"] = "Samarakand",
            ["Constantinople"] = "Constantinople",     // "Eu" maps to "Constantinople"  
            ["Alexandria"] = "Alexandria City (S)", // "DELTA" maps to "Alexandria"
            ["Abundance Grounds (Alex Desert)"] = "Alexandria City (S)",
            ["Roc Mountain"] = "Hotan Palace",
            ["Kings Valley"] = "Alexandria City (S)",
            ["The Storm And Cloud Desert"] = "Alexandria City (S)",
            ["Qin-Shi Tomb|floor:1"] = "Jangan",
            ["Qin-Shi Tomb|floor:2"] = "Jangan",
            ["Qin-Shi Tomb|floor:3"] = "Jangan",
            ["Qin-Shi Tomb|floor:4"] = "Jangan",
            ["Stone Cave"] = "Donwhang",
            ["Stone Cave"] = "Alexandria City (S)",
        };

        public TownLoop(
            VSRO_CONTROL_API.VSRO.DTO.ISession session,
            AutoWalker walker,
            Action<Packet> sendPacket,
            Func<BotSettings> getSettings)
        {
            _session = session;
            _walker = walker;
            _sendPacket = sendPacket;
            _getSettings = getSettings;
        }

        /// <summary>
        /// Resolves which town to use based on the current region,
        /// walks to center, then runs each applicable task in order.
        /// Returns true if all tasks completed, false if aborted.
        /// </summary>
        public async Task<bool> RunAsync(CancellationToken ct)
        {
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

            // Walk to town center first
            Logger.Info("TownLoop", $"Walking to {town.TownName} center");
            await _walker.WalkTo(town.CenterPosition, ct, _session);
            

            // Run each task
            var settings = _getSettings();

            foreach (var task in town.Tasks)
            {
                ct.ThrowIfCancellationRequested();

                if (!task.ShouldRun(settings))
                {
                    Logger.Debug("TownLoop", $"Task '{task.Label}' skipped (condition false)");
                    continue;
                }

                Logger.Info("TownLoop", $"Task '{task.Label}' — walking to approach");

                await _walker.WalkTo(task.ApproachPosition, ct, _session);
                

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
                    await task.Execute(_session, npcUID, _sendPacket);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Error("TownLoop", $"Task '{task.Label}' execute failed: {ex.Message}");
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
            if (quantity <= 0) return;

            var found = Overseer.FindShopSlot(npcRefObjId, codeName);
            if (found is null)
            {
                Logger.Warn("TownLoop::Buy", $"Item {codeName} not found in NPC shop {npcRefObjId}");
                return;
            }

            var (tab, slot) = found.Value;

            var pkt = new Packet(0x7034);
            pkt.WriteByte(0x08);            // movement type: ShopToInventory
            pkt.WriteByte((byte)tab);       // shop tab index
            pkt.WriteByte((byte)slot);      // shop slot index
            pkt.WriteUShort((ushort)quantity);
            pkt.WriteUInt(npcUID);          // NPC spawn UID

            sendPacket(pkt);
            Logger.Info("TownLoop::Buy", $"Buying {codeName} x{quantity} tab={tab} slot={slot} npc=0x{npcUID:X}");
        }

    }
}