using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.Bots
{
    /// <summary>
    /// Polls inventory slots on a fixed interval and sets NeedsReturn
    /// when a configured threshold is breached.
    /// Runs as a background task alongside BotBrain.
    /// Does not send packets — read-only access to session inventory.
    /// </summary>
    public class ResourceMonitor
    {
        private readonly VSRO_CONTROL_API.VSRO.DTO.ISession _session;
        private readonly Func<BotSettings> _getSettings;
        private const int POLL_INTERVAL_MS = 5000;

        public bool NeedsReturn { get; private set; } = false;

        // The reason is stored so the brain can log/display it
        public string ReturnReason { get; private set; } = string.Empty;

        public ResourceMonitor(VSRO_CONTROL_API.VSRO.DTO.ISession session, Func<BotSettings> getSettings)
        {
            _session = session;
            _getSettings = getSettings;
        }

        /// <summary>
        /// Clears the return flag after the town loop has completed.
        /// Called by BotBrain when InTown state finishes.
        /// </summary>
        public void AcknowledgeReturn()
        {
            NeedsReturn = false;
            ReturnReason = string.Empty;
        }

        /// <summary>
        /// Long-running background loop. Cancel via the root CTS.
        /// </summary>
        public async Task RunAsync(CancellationToken ct)
        {
           
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Check();
                }
                catch (Exception ex)
                {
                    Logger.Warn("ResourceMonitor", $"Check error: {ex.Message}");
                }

                await Task.Delay(POLL_INTERVAL_MS, ct);
            }

            Logger.Info("ResourceMonitor", "Stopped");
        }

        private void Check()
        {
            // Already flagged
            if (NeedsReturn) return;

            var settings = _getSettings();
            var slots = _session.Inventory.Slots;

            // --- Arrows ---
            if (settings.Consumables.BuyAmmo)
            {
                int arrowCount = slots.Values
                    .Where(item => item.CodeName128 != null &&
                                   item.CodeName128.Contains("_ARROW_",
                                       StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.Stack);

                if (arrowCount < settings.Consumables.AmmoReturnThreshold)
                {
                    SetReturn($"Arrows low ({arrowCount} < {settings.Consumables.AmmoReturnThreshold})");
                    return;
                }
            }

            // --- HP Potions ---
            if (settings.Consumables.BuyHpPotions)
            {
                int hpCount = slots.Values
                    .Where(item => item.CodeName128 != null &&
                                   item.CodeName128.Contains("_HP_POTION_",
                                       StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.Stack);

                if (hpCount < settings.Consumables.HpPotionReturnThreshold)
                {
                    SetReturn($"HP potions low ({hpCount} < {settings.Consumables.HpPotionReturnThreshold})");
                    return;
                }
            }

            // --- MP Potions ---
            if (settings.Consumables.BuyMpPotions)
            {
                int mpCount = slots.Values
                    .Where(item => item.CodeName128 != null &&
                                   item.CodeName128.Contains("_MP_POTION_",
                                       StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.Stack);

                if (mpCount < settings.Consumables.MpPotionReturnThreshold)
                {
                    SetReturn($"MP potions low ({mpCount} < {settings.Consumables.MpPotionReturnThreshold})");
                    return;
                }
            }


            if (_session._attackLoop?.NeedsRepair == true)
            {
                SetReturn("Weapon durability critical");
                return;
            }
            // --- Weapon Durability ---
            //if (settings.Maintenance.RepairWeapon && _session.WeaponDurability < settings.Maintenance.RepairDurabilityThreshold)
            //     SetReturn($"Weapon durability low");
        }

        private void SetReturn(string reason)
        {
            ReturnReason = reason;
            NeedsReturn = true;
            Logger.Info("ResourceMonitor", $"Return needed: {reason}");
        }
    }
}