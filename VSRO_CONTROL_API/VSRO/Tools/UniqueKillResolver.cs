using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Enums;
using VSRO_CONTROL_API.VSRO;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public class UniqueKillResolver
    {

        public static readonly List<string> names = new()
        {
            "MOB_CH_TIGERWOMAN", // Could end with _L2 and _L3
            "MOB_EU_KERBEROS",
            "MOB_AM_IVY",
            "MOB_QT_01_IVY",
            "MOB_OA_URUCHI",
            "MOB_GNGWC_URUCHI",
            "MOB_GNGWC_ISYUTARU",
            "MOB_KK_ISYUTARU",
            "MOB_TQ_BLACKSNAKE",
            "MOB_TK_BONELORD",
            "MOB_RM_TAHOMET",
            "MOB_RM_ROC"
        };

        // i will make this better i promise lol
        public static readonly Dictionary<string, UniqueKillReward> _rewards = new Dictionary<string, UniqueKillReward>()
        {
            { 
               "MOB_CH_TIGERWOMAN",
               new UniqueKillReward()
               {
                   Gold = 250000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_EU_KERBEROS",
               new UniqueKillReward()
               {
                   Gold = 250000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_04_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_AM_IVY",
               new UniqueKillReward()
               {
                   Gold = 350000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_06_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_OA_URUCHI",
               new UniqueKillReward()
               {
                   Gold = 400000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_07_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_07_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_07_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_07_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_07_A_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_KK_ISYUTARU",
               new UniqueKillReward()
               {
                   Gold = 500000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_09_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_TQ_BLACKSNAKE",
               new UniqueKillReward()
               {
                   Gold = 1000000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_13_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_TK_BONELORD",
               new UniqueKillReward()
               {
                   Gold = 750000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_10_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_RM_TAHOMET",
               new UniqueKillReward()
               {
                   Gold = 800000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_11_B_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },
            {
               "MOB_RM_ROC",
               new UniqueKillReward()
               {
                   Gold = 1000000,
                   Reward = new List<Rewards>()
                   {
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SWORD_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BLADE_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_TBLADE_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_SPEAR_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_CH_BOW_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_AXE_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_CROSSBOW_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DAGGER_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_DARKSTAFF_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_SHIELD_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_STAFF_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSTAFF_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                       new Rewards()
                       {
                          ItemCodename = "ITEM_EU_TSWORD_13_C_RARE",
                          Quantity = 1,
                          Plus = 5
                       },
                   }
               }
            },

        };
        
        /// <summary>
        /// Returns true if the codename is a unique monster
        /// Otherwise returns false
        /// </summary>
        /// <param name="codename">e.g. MOB_CH_TIGERWOMAN</param>
        /// <returns>true or false</returns>
        public static bool Resolve(string codename)
        {
            if (string.IsNullOrEmpty(codename))
                return false;

            foreach (string name in names)
            {
                if (name.Contains(codename))
                    return true;
            }

            return false;
        }

        public static async Task OnUniqueKill(Proxy proxy, string codename)
        {
            if (proxy == null) return;
            if (string.IsNullOrEmpty(codename)) return;

            if (_rewards.TryGetValue(codename, out var uniqueReward))
            {
                Random rand = new Random();
                var randomItem = uniqueReward.Reward.ElementAt(rand.Next(_rewards.Count()));

                await DBConnect.GiveItemToPlayer(proxy.Session!.CharacterName!, randomItem.ItemCodename, randomItem.Plus, randomItem.Quantity, true);
            }
        }
    }
}
