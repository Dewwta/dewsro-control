using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public class InventoryTracker
    {
        // 0x30C9 is sometime delayed after 0x30C8. so we need to defer it from parsing until the pet spawn packet is parsed.
        public ConcurrentDictionary<uint, (Packet Packet, byte ItemCount)> PendingCosPages { get; set; } = new();

        // Avatars
        public ConcurrentDictionary<byte, SR_Item> Avatars { get; set; } = new();

        // Storage
        public ConcurrentDictionary<byte, SR_Item> Storage { get; set; } = new();

        // Player inventory
        public ConcurrentDictionary<byte, SR_Item> Slots { get; set; } = new();

        // Pets
        public ConcurrentDictionary<uint, Pet> Pets { get; set; } = new();
        // Equipment
        public ConcurrentDictionary<byte, SR_Item> Equipment { get; set; } = new();

        public bool IsReady { get; set; } = false;

        public void DumpInventory()
        {
            // Equipment
            if (!Equipment.IsEmpty)
            {
                Logger.Info("InventoryDump", $"═══ EQUIPMENT — {Equipment.Count} Slots ═══");
                foreach (var kv in Equipment.OrderBy(k => k.Key))
                {
                    Logger.Info("InventoryDump",
                        $"  [{kv.Key,2}] {Truncate(kv.Value.CodeName128 ?? "Name was null", 30)}");
                }
            }

            // Player Inventory
            Logger.Info("InventoryDump", $"═══ INVENTORY — {Slots.Count} Items ═══");
            foreach (var kv in Slots.OrderBy(k => k.Key))
            {
                Logger.Info("InventoryDump",
                    $"  [{kv.Key,2}] {Truncate(kv.Value.CodeName128 ?? "Name was null", 30)} ({kv.Value.Stack}/{kv.Value.MaxStack})");
            }

            // Pet Inventories
            foreach (var pet in Pets)
            {
                var petInv = pet.Value.Inventory;
                if (petInv.IsEmpty) continue;

                Logger.Info("InventoryDump", $"═══ PET 0x{pet.Key:X} — {petInv.Count} Items ═══");

                foreach (var kv in petInv.OrderBy(k => k.Key))
                {
                    Logger.Info("InventoryDump",
                        $"  [{kv.Key,2}] {Truncate(kv.Value.CodeName128 ?? "Name was null", 30)} ({kv.Value.Stack}/{kv.Value.MaxStack})");
                }
            }
        }

        private static string Truncate(string s, int max) =>
            s.Length > max ? s.Substring(0, max - 1) + "…" : s;
    }
}