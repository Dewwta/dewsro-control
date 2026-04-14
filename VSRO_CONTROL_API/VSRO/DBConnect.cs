using CoreLib.Tools.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Text;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Enums;
using VSRO_CONTROL_API.VSRO.Tools;


namespace VSRO_CONTROL_API.VSRO
{

    public static class DBConnect
    {
        #region - Properties -

        // Not real - Test credentials.
        private static string _connectionString = string.Empty;

        /// <summary>Returns an unopened SqlConnection using the configured credentials, targeting the specified catalog.</summary>
        internal static SqlConnection OpenConnection(string catalog = "master")
        {
            var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = catalog };
            return new SqlConnection(builder.ConnectionString);
        }

        private static Dictionary<string, string> InitializeTable(ref Dictionary<string, string> table)
        {
            table = new Dictionary<string, string>(); //Key for .txt file name, Value for query

            //Items in Shops 
            table.Add("refshopgoods", "USE SRO_VT_SHARD Select * from _RefShopGoods where [Service] = 1;");
            table.Add("refscrapofpackageitem", "USE SRO_VT_SHARD Select * from _RefScrapOfPackageItem where [Service] = 1;");
            table.Add("refpackageitem", "USE SRO_VT_SHARD Select * from _RefPackageItem where [Service] = 1;");
            table.Add("refpricepolicyofitem", "USE SRO_VT_SHARD Select * from _RefPricePolicyOfItem where [Service] = 1;");

            //Optional Teleport - 
            table.Add("refoptionalteleport", "USE SRO_VT_SHARD SELECT [Service] ,[ID] ,[ObjName128] ,[ZoneName128] ,[RegionID] ,[Pos_X] ,[Pos_Y] ,[Pos_Z] ,[WorldID] ,[RegionIDGroup] ,[MapPoint] ,[LevelMin] ,[LevelMax] ,[Param1] ,[Param1_Desc_128] ,[Param2] ,[Param2_Desc_128] ,[Param3] ,[Param3_Desc_128] FROM [dbo].[_RefOptionalTeleport] WHERE [Service] = 1;");

            //Level data - Anything related to level, job level, regular level, needed exp etc....
            table.Add("leveldata", "USE SRO_VT_SHARD Select [Lvl] ,[Exp_C] ,[Exp_M] ,[Cost_M] ,[Cost_ST] ,[GUST_Mob_Exp] ,[JobExp_Trader] ,[JobExp_Robber] ,[JobExp_Hunter] FROM [dbo].[_RefLevel];");

            //Magic PoP stuff
            table.Add("gachaitemset", "USE SRO_VT_SHARD Select [Service],[Set_ID],[RefItemID],[Ratio],[Count],[GachaID],[Visible],[param1],[param1_Desc128],[param2],[param2_Desc128],[param3],[param3_Desc128],[param4],[param4_Desc128] FROM[dbo].[_RefGachaItemSet] WHERE [Service] = 1;");
            table.Add("gachanpcmap", "USE SRO_VT_SHARD Select * from _RefGachaNpcMap where [Service] = 1;");

            //World region settings and values
            table.Add("gameworlddata", "USE SRO_VT_SHARD Select [ID] ,[WorldCodeName128] ,[Type] ,[WorldMaxCount] ,[WorldMaxUserCount] ,[ConfigGroupCodeName128] FROM [dbo].[_RefGame_World];");
            table.Add("gameworldconfigdata", "USE SRO_VT_SHARD Select [GroupCodeName128] ,[ValueCodeName128] ,[Type] ,[Value] FROM [dbo].[_RefGame_World_Config];");
            table.Add("refregion", "USE SRO_VT_SHARD Select wRegionID,X,Z,ContinentName,AreaName,IsBattleField,Climate,MaxCapacity,AssocObjID,AssocServer,AssocFile256,'NULL','NULL','NULL','NULL','NULL','NULL','NULL','NULL','NULL','NULL' from _RefRegion;");


            //Magic Options Related
            table.Add("magicoption", "USE SRO_VT_SHARD Select [Service] ,[ID] ,[MOptName128] ,[AttrType] ,[MLevel] ,[Prob] ,[Weight] ,[Param1] ,[Param2] ,[Param3] ,[Param4] ,[Param5] ,[Param6] ,[Param7] ,[Param8] ,[Param9] ,[Param10] ,[Param11] ,[Param12] ,[Param13] ,[Param14] ,[Param15] ,[Param16] ,[ExcFunc1] ,[ExcFunc2] ,[ExcFunc3] ,[ExcFunc4] ,[ExcFunc5] ,[ExcFunc6] ,[AvailItemGroup1] ,[ReqClass1] ,[AvailItemGroup2] ,[ReqClass2] ,[AvailItemGroup3] ,[ReqClass3] ,[AvailItemGroup4] ,[ReqClass4] ,[AvailItemGroup5] ,[ReqClass5] ,[AvailItemGroup6] ,[ReqClass6] ,[AvailItemGroup7] ,[ReqClass7] ,[AvailItemGroup8] ,[ReqClass8] ,[AvailItemGroup9] ,[ReqClass9] ,[AvailItemGroup10] ,[ReqClass10] FROM [dbo].[_RefMagicOpt] WHERE [Service] = 1;");
            table.Add("magicoptionassign", "USE SRO_VT_SHARD Select [Service] ,[Race] ,[TID3] ,[TID4] ,[AvailMOpt1] ,[AvailMOpt2] ,[AvailMOpt3] ,[AvailMOpt4] ,[AvailMOpt5] ,[AvailMOpt6] ,[AvailMOpt7] ,[AvailMOpt8] ,[AvailMOpt9] ,[AvailMOpt10] ,[AvailMOpt11] ,[AvailMOpt12] ,[AvailMOpt13] ,[AvailMOpt14] ,[AvailMOpt15] ,[AvailMOpt16] ,[AvailMOpt17] ,[AvailMOpt18] ,[AvailMOpt19] ,[AvailMOpt20] ,[AvailMOpt21] ,[AvailMOpt22] ,[AvailMOpt23] ,[AvailMOpt24] ,[AvailMOpt25] FROM [dbo].[_RefMagicOptAssign] WHERE [Service] = 1;");
            table.Add("refmagicoptbyitemoptleveldata", "USE SRO_VT_SHARD Select [Link] ,[RefMagicOptID] ,[MagicOptValue] ,[TooltipType] ,[TooltipCodename] FROM [dbo].[_RefMagicOptByItemOptLevel];");
            table.Add("refmagicoptgroup", "USE SRO_VT_SHARD Select [Service] ,[LinkID] ,[MagicType] ,[CodeName128] ,[MOptID] ,[MOptLevel] ,[Value] ,[Param1] ,[Param1_Desc] ,[Param2] ,[Param2_Desc] FROM [dbo].[_RefMagicOptGroup] WHERE [Service] = 1;");

            //NPC/Shops Related
            table.Add("refmappingshopgroup", "USE SRO_VT_SHARD Select [Service] ,[Country] ,[RefShopGroupCodeName] ,[RefShopCodeName] FROM [dbo].[_RefMappingShopGroup] WHERE [Service] = 1;");
            table.Add("refmappingshopwithtab", "USE SRO_VT_SHARD Select [Service] ,[Country] ,[RefShopCodeName] ,[RefTabGroupCodeName] FROM [dbo].[_RefMappingShopWithTab] WHERE [Service] = 1;");
            table.Add("refshop", "USE SRO_VT_SHARD Select [Service] ,[Country] ,[ID] ,[CodeName128] ,[Param1] ,[Param1_Desc128] ,[Param2] ,[Param2_Desc128] ,[Param3] ,[Param3_Desc128] ,[Param4] ,[Param4_Desc128] FROM [dbo].[_RefShop] WHERE [Service] = 1;");
            table.Add("shopgroupdata", "USE SRO_VT_SHARD Select [Service] ,[GroupID] ,[CodeName128] ,[StrID128_Group] FROM [dbo].[_RefShopItemGroup] WHERE [Service] = 1;");
            table.Add("refshoptab", "USE SRO_VT_SHARD Select [Service] ,[Country] ,[ID] ,[CodeName128] ,[RefTabGroupCodeName] ,[StrID128_Tab] FROM [dbo].[_RefShopTab] WHERE [Service] = 1;");
            table.Add("refshoptabgroup", "USE SRO_VT_SHARD Select [Service] ,[Country] ,[ID] ,[CodeName128] ,[StrID128_Group] FROM [dbo].[_RefShopTabGroup] WHERE [Service] = 1;");
            table.Add("refshopgroup", "USE SRO_VT_SHARD Select * FROM [dbo].[_RefShopGroup] WHERE [Service] = 1");
            table.Add("refshopitemstockperiod", "USE SRO_VT_SHARD Select [Service], [Country], [RefShopGroupCodeName], [RefPackageItemCodeName], [StockOpeningDate], [StockExpireDate], [PeriodDevice] from _RefShopItemStockPeriod where [Service] = 1;");


            //Quest Related
            table.Add("questdata", "USE SRO_VT_SHARD Select [Service] ,[ID] ,[CodeName] ,[Level] ,[DescName] ,[NameString] ,[PayString] ,[ContentsString] ,[PayContents] ,[NoticeNPC] ,[NoticeCondition] FROM [dbo].[_RefQuest] WHERE [Service] = 1;");
            table.Add("refqusetreward", "USE SRO_VT_SHARD Select [QuestID] ,[QuestCodeName] ,[IsView] ,[IsBasicReward] ,[IsItemReward] ,[IsCheckCondition] ,[IsCheckCountry] ,[SelectionCnt] ,[IsCheckClass] ,[IsCheckGender] ,[Gold] ,[Exp] ,[SPExp] ,[SP] ,[AP] ,[APType] ,[Hwan] ,[Inventory] ,[ItemRewardType] ,[Param1] ,[Param1_Desc] ,[Param2] ,[Param2_Desc] ,[Param3] ,[Param3_Desc] FROM [dbo].[_RefQuestReward] WHERE [Service] = 1;");
            table.Add("refquestrewarditems", "USE SRO_VT_SHARD Select [QuestID] ,[QuestCodeName] ,[RewardType] ,[ItemCodeName] ,[RentItemCodeName] ,[OptionalItemCode] ,[OptionalItemCnt] ,[AchieveQuantity] ,[Param1] ,[Param1_Desc] ,[Param2] ,[Param2_Desc] FROM [dbo].[_RefQuestRewardItems] WHERE [Service] = 1;");


            //SetTypeGroups Like Egyptian set...
            table.Add("refsetitemgroup", "USE SRO_VT_SHARD Select [Service] ,[ID] ,[CodeName128] ,[ObjName128] ,[NameStrID128] ,[DescStrID128] ,[SetEffectMask] ,[SetMagicMask] ,[2SetMOptGroupID] ,[3SetMOptGroupID] ,[4SetMOptGroupID] ,[5SetMOptGroupID] ,[6SetMOptGroupID] ,[7SetMOptGroupID] ,[8SetMOptGroupID] ,[9SetMOptGroupID] ,[10SetMOptGroupID] ,[11SetMOptGroupID] FROM [dbo].[_RefSetItemGroup] WHERE [Service] = 1;");

            //Fortress Related
            table.Add("refsiegeblessbuff", "USE SRO_VT_SHARD Select [Service] ,[BlessID] ,[FortressID] ,[RefBlessBuffID] ,[NeedGold] ,[NeedGP] FROM [dbo].[_RefSiegeBlessBuff] WHERE [Service] = 1;");
            table.Add("refsiegedungeon", "USE SRO_VT_SHARD Select [Service] ,[FortressID] ,[WorldID] ,[MaxCreateCount] ,[EntryGold] ,[EntryGP] FROM [dbo].[_RefSiegeDungeon] WHERE [Service] = 1;");
            table.Add("siegefortress", "USE SRO_VT_SHARD Select [Service] ,[FortressID] ,[CodeName128] ,[Name] ,[NameID128] ,[LinkedTeleportCodeName] ,[Scale] ,[MaxAdmission] ,[MaxGuard] ,[MaxBarricade] ,[TaxTargets] ,[RequestFee] ,[CrestPath128] ,[RequestNPCName128] FROM [dbo].[_RefSiegeFortress] WHERE [Service] = 1;");
            table.Add("siegefortressbattlerank", "USE SRO_VT_SHARD Select [Service] ,[RankLvl] ,[RankName] ,[ReqPKCount] ,[BindedSkillID] ,[CrestPath128] FROM [dbo].[_RefSiegeFortressBattleRank] WHERE [Service] = 1;");
            table.Add("siegefortressguard", "USE SRO_VT_SHARD Select [Service] ,[FortressID] ,[GuardRefObjID] FROM [dbo].[_RefSiegeFortressGuard] WHERE [Service] = 1;");
            table.Add("siegefortressitemforge", "USE SRO_VT_SHARD Select [Service] ,[FortressID] ,[RefItemID] ,[ReqGold] ,[ReqGP] ,[ForgeTimeMin] FROM [dbo].[_RefSiegeFortressItemForge] WHERE [Service] = 1;");
            table.Add("siegestructupgradedata", "USE SRO_VT_SHARD Select [Service] ,[Structname] ,[BaseStructcodename] ,[UpgradeStructname1] ,[UpgradeStructname2] ,[UpgradeStructname3] ,[UpgradeStructname4] FROM [dbo].[_RefSiegeStructUpgrade] WHERE [Service] = 1;");

            //Teleport Related
            table.Add("teleportbuilding", "USE SRO_VT_SHARD Select [Service],[ID],[CodeName128],[ObjName128],[OrgObjCodeName128],[NameStrID128],[DescStrID128],[CashItem],[Bionic],[TypeID1],[TypeID2],[TypeID3],[TypeID4],[DecayTime],[Country],[Rarity],[CanTrade],[CanSell],[CanBuy],[CanBorrow],[CanDrop],[CanPick],[CanRepair],[CanRevive],[CanUse],[CanThrow],[Price],[CostRepair],[CostRevive],[CostBorrow],[KeepingFee],[SellPrice],[ReqLevelType1],[ReqLevel1],[ReqLevelType2],[ReqLevel2],[ReqLevelType3],[ReqLevel3],[ReqLevelType4],[ReqLevel4],[MaxContain],[RegionID],[Dir],[OffsetX],[OffsetY],[OffsetZ],[Speed1],[Speed2],[Scale],[BCHeight],[BCRadius],[EventID],[AssocFileObj128],[AssocFileDrop128],[AssocFileIcon128],[AssocFile1_128],[AssocFile2_128],'0' FROM [_RefObjCommon] where [Service] = 1 and TypeID1 = 4 and (TypeID2 = 1 OR TypeID2 = 2);");
            table.Add("teleportlink", "USE SRO_VT_SHARD Select [Service] ,[OwnerTeleport] ,[TargetTeleport] ,[Fee] ,[RestrictBindMethod] ,[RunTimeTeleportMethod] ,[CheckResult] ,[Restrict1] ,[Data1_1] ,[Data1_2] ,[Restrict2] ,[Data2_1] ,[Data2_2] ,[Restrict3] ,[Data3_1] ,[Data3_2] ,[Restrict4] ,[Data4_1] ,[Data4_2] ,[Restrict5] ,[Data5_1] ,[Data5_2] FROM [dbo].[_RefTeleLink] WHERE [Service] = 1;");
            table.Add("teleportdata", "USE SRO_VT_SHARD Select [Service] ,[ID] ,RTRIM([CodeName128]) ,[AssocRefObjID] ,[ZoneName128] ,[GenRegionID] ,[GenPos_X] ,[GenPos_Y] ,[GenPos_Z] ,[GenAreaRadius] ,[CanBeResurrectPos] ,[CanGotoResurrectPos] ,[GenWorldID] FROM [dbo].[_RefTeleport] WHERE [Service] = 1;");

            //Trades Related
            table.Add("maxtradescaledata", "USE SRO_VT_SHARD Select [Value] FROM [dbo].[_RefShardContentConfig] WHERE ID IN (1,4,5,6,7,8) AND [Service] = 1;");

            //Talisman Collection Related
            table.Add("collectionbook_theme", "USE SRO_VT_SHARD Select * from _RefCollectionBook_Theme where [Service] = 1;");
            table.Add("collectionbook_item", "USE SRO_VT_SHARD Select _RefCollectionBook_Item.[Service], _RefCollectionBook_Item.[CodeName128], _RefCollectionBook_Item.[ObjName128], [ThemeCodeName128], [_RefCollectionBook_Theme].ID, [SlotIndex], [Story128], [DDJFile128] FROM _RefCollectionBook_Item INNER JOIN _RefCollectionBook_Theme on _RefCollectionBook_Item.ThemeCodeName128 = _RefCollectionBook_Theme.CodeName128;");

            //Gold Drop
            table.Add("levelgold", "USE SRO_VT_SHARD Select [MonLevel], [GoldMin], [GoldMax] from _RefDropGold;");

            //EventZone
            table.Add("eventzonedata", "USE SRO_VT_SHARD Select * from _RefEventZone where [Service] = 1;");

            //Object Groups i guess
            table.Add("fmncategorytreedata", "USE SRO_VT_SHARD Select * from _RefFmnCategoryTree where [Service] = 1;");
            table.Add("fmntidgroupmapdata", "USE SRO_VT_SHARD Select * from _RefFmnTidGroupMap where [Service] = 1;");

            //Yellow title
            table.Add("hwanleveldata", "USE SRO_VT_SHARD Select [HwanLevel], [Title_CH70], [Title_EU70] from _RefHWANLevel;");
            table.Add("refservereventid", "USE SRO_VT_SHARD Select ID from _RefServerEvent where [Service] = 1;");

            return table;
        }
        private static Dictionary<string, string>? tables = new Dictionary<string, string>();
        public static bool TextdataGenerationRunning = false;
        public static int TextdataGenerationProgress = 0;

        #endregion

        #region - Initialization -

        public static async Task<(bool success, string reason)> Initialize(string _dbUrl)
        {
            string msg;
            try
            {
                _connectionString = _dbUrl;
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.CreateCharacterPlaytimeTable_q, conn))
                    {
                        int count = await cmd.ExecuteNonQueryAsync();
                        
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                msg = $"Error ocurred during DBConnect intialization!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (true, msg);
            }
        }

        #endregion

        #region - Querying -

        // Game
        public static async Task<(bool success, string codename, string reason)> LookupItemCodeName(uint _refItemId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetItemCodeNameByRefId_q, conn))
                    {
                        // FIX: Cast the uint to a standard int so SQL Server accepts it
                        cmd.Parameters.AddWithValue("@ItemID", (int)_refItemId);

                        var result = await cmd.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                            return (false, "", "Item not found");

                        return (true, result.ToString()!, "");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error retrieving item codename: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, "", msg);
            }
        }
        public static async Task<(bool success, ItemRecord? item, string reason)> GetItemRecord(uint _itemId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetItemTypeId_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemID", (int)_itemId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                                return (false, null, "Item not found");

                            var item = new ItemRecord
                            {
                                CodeName = reader["CodeName128"].ToString() ?? "",
                                T1 = Convert.ToByte(reader["TypeID1"]),
                                T2 = Convert.ToByte(reader["TypeID2"]),
                                T3 = Convert.ToByte(reader["TypeID3"]),
                                T4 = Convert.ToByte(reader["TypeID4"]),

                                // NOTE: assuming you added this field to your class
                                MaxStack = reader["MaxStack"] != DBNull.Value
                                    ? Convert.ToUInt16(reader["MaxStack"]) // Use UInt16 (ushort)
                                    : (ushort)1
                            };

                            return (true, item, "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error retrieving item record: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }
        }
        public static async Task<bool> DoesUsernameExist(string username)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.DoesUsernameExist_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", username);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error checking username existence: {ex.Message}");
                return false;
            }
        }
        public static async Task<(bool success, CharacterPosition? data, string reason)> GetCharacterPositionAsync(string charName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetCharacterPosByName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var result = new CharacterPosition
                                {
                                    CharacterName = charName,
                                    LatestRegion = Convert.ToInt32(reader["LatestRegion"]),
                                    PosX = Convert.ToSingle(reader["PosX"]),
                                    PosY = Convert.ToSingle(reader["PosY"]),
                                    PosZ = Convert.ToSingle(reader["PosZ"])
                                };

                                return (true, result, "");
                            }
                            else
                            {
                                return (false, null, "Character not found");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }
        public static async Task<(bool success, string reason)> GetOnlineUsers()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetOnlineUsers_q, conn))
                    {
                        int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        return (true, $"{count}");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error getting online users!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, List<(string CodeName128, string Dur_L, string ID, string MaxStack)>?, string reason)> GetItemDurability(string _itemCodeName)
        {
            if (!_itemCodeName.EndsWith("%"))
            {
                _itemCodeName += "%";
            }

            var items = new List<(string CodeName128, string Dur_L, string ID, string MaxStack)>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync(); // Open connection asynchronously

                    using (SqlCommand cmd = new SqlCommand(Constant.GetItemDurability_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ITEMCODE", _itemCodeName);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync()) // Execute asynchronously
                        {
                            while (await reader.ReadAsync()) // Read each row asynchronously
                            {
                                // Add data to the temporary list
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
                                items.Add((reader["CodeName128"].ToString(),
                                           reader["Dur_L"].ToString(),
                                           reader["ID"].ToString(),
                                           reader["MaxStack"].ToString()));
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
                            }
                        }
                    }
                }

                return (true, items, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error getting item durabilities!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }

        }
        public static async Task<(bool success, int charId, string reason)> GetCharIdByName(string charName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetCharIdByName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null)
                            return (true, Convert.ToInt32(result), "");
                        else
                            return (false, 0, "Character not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting CharID: {ex.Message}");
                return (false, 0, ex.Message);
            }
        }
        public static async Task<(bool success, List<(byte Slot, int ItemID)>? items, byte inventorySize, string reason)> GetInventoryByCharName(string charName)
        {
            try
            {
                var (found, charId, err) = await GetCharIdByName(charName);
                if (!found)
                    return (false, null, 0, err);

                byte invSize = 0;
                var items = new List<(byte Slot, int ItemID)>();

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Get inventory size
                    using (SqlCommand cmd = new SqlCommand(Constant.GetInventorySize_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharID", charId);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null)
                            invSize = Convert.ToByte(result);
                    }

                    // Get inventory items
                    using (SqlCommand cmd = new SqlCommand(Constant.GetInventoryByCharId_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharID", charId);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                items.Add((
                                    Convert.ToByte(reader["Slot"]),
                                    Convert.ToInt32(reader["ItemID"])
                                ));
                            }
                        }
                    }
                }

                return (true, items, invSize, "");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting inventory: {ex.Message}");
                return (false, null, 0, ex.Message);
            }
        }
        public static async Task<(bool success, List<(byte Slot, int ItemID, string CodeName, int MaxStack)>? items, string reason)> GetInventoryWithNamesByCharName(string charName)
        {
            try
            {
                var (found, charId, err) = await GetCharIdByName(charName);
                if (!found)
                    return (false, null, err);

                var items = new List<(byte Slot, int ItemID, string CodeName, int MaxStack)>();

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetInventoryWithNames_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharID", charId);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                items.Add((
                                    Convert.ToByte(reader["Slot"]),
                                    Convert.ToInt32(reader["ItemID"]),
                                    reader["CodeName128"].ToString()!,
                                    Convert.ToInt32(reader["MaxStack"])
                                ));
                            }
                        }
                    }
                }

                return (true, items, "");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting inventory with names: {ex.Message}");
                return (false, null, ex.Message);
            }
        }
        public static async Task<(bool success, int jid, string reason)> GetJIDByCharName(string charName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    

                    using (SqlCommand cmd = new SqlCommand(Constant.GetAccountJIDByCharName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);

                        var result = await cmd.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            return (true, Convert.ToInt32(result), "");
                        }

                        return (false, 0, "JID not found for character");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting JID by CharName: {ex.Message}");
                return (false, 0, ex.Message);
            }
        }
        public static async Task<(bool success, string codeName)> GetMonsterCodeName(uint refObjID)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetMonsterCodeName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@RefObjID", (int)refObjID);

                        object result = await cmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            return (true, result.ToString()!);
                        }
                        else
                        {
                            return (false, $"UNKNOWN_REF_{refObjID}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error fetching CodeName128: {ex.Message}");
                return (false, $"ERROR_REF_{refObjID}");
            }
        }
        public static async Task<(bool success, Dictionary<short, string>? regions, string reason)> GetRegionsWithContinentsDict()
        {
            try
            {
                var regions = new Dictionary<short, string>();

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetRegionsWithContinents, conn))
                    {
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                short regionId = Convert.ToInt16(reader["wRegionID"]);
                                string continent = reader["ContinentName"].ToString() ?? string.Empty;

                                regions[regionId] = continent;
                            }
                        }
                    }
                }

                return (true, regions, "");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting regions: {ex.Message}");
                return (false, null, ex.Message);
            }
        }
        
        // Accounts
        public static async Task<(bool success, List<UserDTO>?, string reason)> GetAllUsersInDB()
        {
            
            try
            {
                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();
                using SqlCommand cmd = new(Constant.GetAllUsersInDB_q, conn);
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                List<UserDTO> users = new();
                while (await reader.ReadAsync())
                {
                    users.Add(new UserDTO
                    {
                        JID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        HashedPassword = reader.GetString(2),
                        Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Nickname = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Email = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Sex = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Authority = reader.IsDBNull(7) ? (byte)3 : reader.GetByte(7),
                        totalPlaytimeMinutes = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                    });
                }
                return (true, users, $"{users.Count} users retrieved.");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error fetching all users: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }
        }
        public static async Task<(bool success, UserDTO? user, string reason)> GetUserAccountByUsername(string _username)
        {
            try
            {
                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();
                using SqlCommand cmd = new(Constant.GetUserAccountByUsername_q, conn);
                cmd.Parameters.AddWithValue("@Username", _username);
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    UserDTO user = new()
                    {
                        JID = reader.GetInt32(0),
                        Username = reader.GetString(1),
                        HashedPassword = reader.GetString(2),
                        Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Nickname = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Email = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Sex = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Authority = reader.IsDBNull(7) ? (byte)3 : reader.GetByte(7),
                        totalPlaytimeMinutes = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
                    };
                    return (true, user, "User found.");
                }
                return (false, null, $"No user found with username '{_username}'.");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error fetching user '{_username}': {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }
        }
        public static async Task<(bool success, LoginResponseObject? obj, string reason)> AuthenticateUserAccount(string _username, string _password)
        {
            try
            {
                var res = await GetUserAccountByUsername(_username);

                if (!res.success || res.user == null)
                    return (false, null, "User not found");

                string hashedInput = Hash.GetMD5Hash(_password);
                LoginResponseObject _userObj = new LoginResponseObject
                {
                    User = res.user.ToSafeUser(),
                    Jwt = "",
                };
                if (!string.Equals(res.user.HashedPassword, hashedInput, StringComparison.OrdinalIgnoreCase))
                    return (false, null, "Invalid password");

                return (true, _userObj, "Authenticated");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
        }
        public static async Task<(bool success, string reason)> ChangeUserPassword(string _usr, string _oldPsw, string _newPsw)
        {
            try
            {
                var res = await GetUserAccountByUsername(_usr);

                if (!res.success || res.user == null)
                    return (false, "User not found");

                string oldHash = Hash.GetMD5Hash(_oldPsw);

                if (!string.Equals(res.user.HashedPassword, oldHash, StringComparison.OrdinalIgnoreCase))
                    return (false, "Invalid current password");

                string newHash = Hash.GetMD5Hash(_newPsw);

                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();

                using SqlCommand cmd = new(Constant.ChangeUserPassword_q, conn);
                cmd.Parameters.AddWithValue("@NewPassword", newHash);
                cmd.Parameters.AddWithValue("@Username", _usr);

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return (false, "Password update failed");

                return (true, "Password updated successfully");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error changing password for '{_usr}': {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        
        // Admin
        public static async Task<(bool success, string reason)> UpdateUserAccount(UserDTO user)
        {
            try
            {
                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();

                using SqlCommand cmd = new(Constant.UpdateUserAccount_q, conn);

                cmd.Parameters.Add("@JID", SqlDbType.Int).Value = user.JID;

                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 32)
                    .Value = (object?)user.Nickname ?? DBNull.Value;

                cmd.Parameters.Add("@Email", SqlDbType.VarChar, 100)
                    .Value = (object?)user.Email ?? DBNull.Value;

                // sex column is nchar(1) in TB_User — must be 'M' or 'F'
                cmd.Parameters.Add("@Sex", SqlDbType.NChar, 1)
                    .Value = (object?)user.Sex ?? DBNull.Value;

                cmd.Parameters.Add("@Authority", SqlDbType.Int)
                    .Value = user.Authority;

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return (false, "No rows updated (user may not exist)");

                return (true, "User updated successfully");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error updating user account!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, int silk, string reason)> GetUserSilkByJID(int jid)
        {
            try
            {
                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();

                using SqlCommand cmd = new(Constant.GetSilkByJID_q, conn);
                cmd.Parameters.Add("@JID", SqlDbType.Int).Value = jid;

                object? result = await cmd.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                    return (false, 0, "Silk data not found");

                return (true, Convert.ToInt32(result), "Success");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error getting silk for JID '{jid}': {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, 0, msg);
            }
        }
        public static async Task<(bool success, List<(int nIdx, string szIPBegin, string szIPEnd, string szGM, DateTime dIssueDate, string? szISP, string? szDesc)>?, string reason)> GetPrivelagedIps()
        {
            var items = new List<(int, string, string, string, DateTime, string?, string?)>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetAllPrivelagedIp_q, conn))
                    {
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            int ord_nIdx = reader.GetOrdinal("nIdx");
                            int ord_ipBegin = reader.GetOrdinal("szIPBegin");
                            int ord_ipEnd = reader.GetOrdinal("szIPEnd");
                            int ord_gm = reader.GetOrdinal("szGM");
                            int ord_date = reader.GetOrdinal("dIssueDate");
                            int ord_isp = reader.GetOrdinal("szISP");
                            int ord_desc = reader.GetOrdinal("szDesc");

                            while (await reader.ReadAsync())
                            {
                                items.Add((
                                    reader.GetInt32(ord_nIdx),
                                    reader.GetString(ord_ipBegin),
                                    reader.GetString(ord_ipEnd),
                                    reader.GetString(ord_gm),
                                    reader.GetDateTime(ord_date),
                                    reader.IsDBNull(ord_isp) ? null : reader.GetString(ord_isp),
                                    reader.IsDBNull(ord_desc) ? null : reader.GetString(ord_desc)
                                ));
                            }
                        }
                    }
                }

                return (true, items, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error getting privelaged ip table!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }
        }
        public static async Task<(bool success, List<(string AreaName, bool Enabled)>?, string reason)> GetRegionAssoc()
        {
            var items = new List<(string, bool)>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetRegionAssoc_q, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        int ord_area = reader.GetOrdinal("AreaName");
                        int ord_assoc = reader.GetOrdinal("AssocServer");

                        while (await reader.ReadAsync())
                        {
                            items.Add((
                                reader.GetString(ord_area),
                                reader.GetInt32(ord_assoc) == 1
                            ));
                        }
                    }
                }

                return (true, items, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error getting region assoc table!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }
        }
        public static async Task<(bool success, List<(int ID, int ContentID, string Subject, string Article, DateTime EditDate)>?, string reason)> GetNotices(int contentID = 22)
        {
            var notices = new List<(int ID, int ContentID, string Subject, string Article, DateTime EditDate)>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetAllNotices_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ContentID", contentID);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                notices.Add((
                                    Convert.ToInt32(reader["ID"]),
                                    Convert.ToInt32(reader["ContentID"]),
                                    reader["Subject"].ToString() ?? "",
                                    reader["Article"].ToString() ?? "",
                                    Convert.ToDateTime(reader["EditDate"])
                                ));
                            }
                        }
                    }
                }

                return (true, notices, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error getting notices: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, null, msg);
            }
        }
        public static async Task<(bool success, string reason)> AddNotice(string subject, string article, int contentID = 22)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subject) || subject.Length > 80)
                    return (false, "Subject is required and must be 80 characters or less.");

                if (string.IsNullOrWhiteSpace(article) || article.Length > 1024)
                    return (false, "Article is required and must be 1024 characters or less.");

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.AddNotice_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ContentID", contentID);
                        cmd.Parameters.AddWithValue("@Subject", subject);
                        cmd.Parameters.AddWithValue("@Article", article);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0
                            ? (true, "")
                            : (false, "No rows were inserted.");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error adding notice: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> DeleteNotice(int noticeID)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.DeleteNotice_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", noticeID);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0
                            ? (true, "")
                            : (false, "Notice not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error deleting notice: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }

        #endregion

        #region - Achievements -

        // --- Achievements ---

        public static async Task<bool> InitAchievementTable()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.InitAchievementTable_q, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error initializing AchievementProgress table: {ex.Message}");
                return false;
            }
        }

        public static async Task<(long progress, bool completed)> GetAchievementProgress(
            string charName, string achievementName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetAchievementProgress_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);
                        cmd.Parameters.AddWithValue("@AchievementName", achievementName);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                long progress = Convert.ToInt64(reader["Progress"]);
                                bool completed = Convert.ToBoolean(reader["Completed"]);
                                return (progress, completed);
                            }
                            return (0, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting achievement progress: {ex.Message}");
                return (0, false);
            }
        }

        public static async Task<List<(string name, long progress, bool completed, DateTime? completedAt)>>
            GetAllAchievementsForChar(string charName)
        {
            var results = new List<(string, long, bool, DateTime?)>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetAllAchievementsForChar_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string name = reader["AchievementName"].ToString()!;
                                long progress = Convert.ToInt64(reader["Progress"]);
                                bool completed = Convert.ToBoolean(reader["Completed"]);
                                DateTime? completedAt = reader["CompletedAt"] == DBNull.Value
                                    ? null
                                    : Convert.ToDateTime(reader["CompletedAt"]);
                                results.Add((name, progress, completed, completedAt));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting all achievements for {charName}: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Increments progress by amount. Auto-completes if progress >= goal.
        /// Skips if already completed (handled in the MERGE query).
        /// Returns true if the increment resulted in completion (newly completed).
        /// </summary>
        public static async Task<bool> IncrementAchievementProgress(
            string charName, string achievementName, long amount, long goal)
        {
            try
            {
                // Check if already completed first to avoid unnecessary write
                var (currentProgress, alreadyCompleted) = await GetAchievementProgress(charName, achievementName);
                if (alreadyCompleted) return false;

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.IncrementAchievementProgress_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);
                        cmd.Parameters.AddWithValue("@AchievementName", achievementName);
                        cmd.Parameters.AddWithValue("@Amount", amount);
                        cmd.Parameters.AddWithValue("@Goal", goal);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // Check if this increment caused completion
                bool newlyCompleted = (currentProgress + amount) >= goal;
                return newlyCompleted;
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error incrementing achievement '{achievementName}' for {charName}: {ex.Message}");
                return false;
            }
        }

        public static async Task<List<string>> GetAchievementNamesAsync(string charName)
        {
            var results = new List<string>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetAchievementNames_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                                results.Add(reader["AchievementName"].ToString()!);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting achievement names for {charName}: {ex.Message}");
            }
            return results;
        }

        public static async Task<(long progress, bool completed, DateTime? completedAt)?> GetAchievementProgressAsync(
            string charName, string achievementName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetAchievementProgressByName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);
                        cmd.Parameters.AddWithValue("@AchievementName", achievementName);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                long progress = Convert.ToInt64(reader["Progress"]);
                                bool completed = Convert.ToBoolean(reader["Completed"]);
                                DateTime? completedAt = reader["CompletedAt"] == DBNull.Value
                                    ? null
                                    : Convert.ToDateTime(reader["CompletedAt"]);
                                return (progress, completed, completedAt);
                            }
                            return null; // not found
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Error getting achievement progress for '{achievementName}' ({charName}): {ex.Message}");
                return null;
            }
        }

        #endregion

        #region - Editing -

        // Edit
        public static async Task<(bool success, string reason)> AddNewUser(string _usr, string _psw, bool? _admin)
        {
            try
            {
                if (await DoesUsernameExist(_usr))
                {
                    Logger.Error(typeof(DBConnect), $"Username '{_usr}' already exists. Please choose a different username.");
                    return (false, "U001: Username already exists.");
                }
                string hashedPassword = Tools.Hash.GetMD5Hash(_psw);
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.AddNewUser_q, conn))
                    {
                        int isUserGM = _admin == true ? 1 : 3;

                        cmd.Parameters.AddWithValue("@UserID", _usr);
                        cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        cmd.Parameters.AddWithValue("@sec_Primary", isUserGM);
                        cmd.Parameters.AddWithValue("@sec_content", isUserGM);
                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        if (affectedRows <= 0)
                        {
                            string msg = $"D001: Database error occurred where no rows were affected on new user addition. affectedRows={affectedRows}";
                            Logger.Error(typeof(DBConnect), msg);
                            return (false, msg);
                        }
                        Logger.Info(typeof(DBConnect), $"Added New User: {_usr} GM: {_admin}{Environment.NewLine}");
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error occurred adding User: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> AddNewNPCToCharacterLocation(string _codeName, string _characterName, int _storeGroups, int _tabGroup1, int _tabGroup2, int _tabGroup3, int _tabGroup4, int _lookingDir)
        {
            string message = "";
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    conn.InfoMessage += (s, ev) =>
                    {
                        message += ev.Message + Environment.NewLine;
                    };
                    int affectedRows = 0;
                    using (SqlCommand cmd = new SqlCommand(Constant.AddNewNPCToCharacterLocation_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CODENAME15253", _codeName);
                        cmd.Parameters.AddWithValue("@TABGROUPS", _tabGroup1);
                        cmd.Parameters.AddWithValue("@TABGROUPS2", _tabGroup2);
                        cmd.Parameters.AddWithValue("@TABGROUPS3", _tabGroup3);
                        cmd.Parameters.AddWithValue("@TABGROUPS4", _tabGroup4);
                        cmd.Parameters.AddWithValue("@GROUPCOUNT", _storeGroups);
                        cmd.Parameters.AddWithValue("@CHARACTERNAMELOCATION", _characterName);
                        cmd.Parameters.AddWithValue("@LOOKDIRECTION", _lookingDir);
                        affectedRows = await cmd.ExecuteNonQueryAsync();

                    }
                    // Use the reason field to send the contents back here.
                    return (affectedRows > 0, message);
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error Adding NPC: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }

        }
        public static async Task<(bool success, string reason)> AddSilkToUserByName(string _usr, int _silkAmount)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.AddSilkToUserByName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@SILKAMOUNT", _silkAmount);
                        cmd.Parameters.AddWithValue("@ACCOUNTUSER", _usr);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        Logger.Info(typeof(DBConnect), $"Added silk! Amount: {_silkAmount} - User: {_usr}");

                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error adding silk to {_usr}: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> AddSilkToUserByJID(int _jid, int _silkAmount)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.AddSilkToUserByName_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@SILKAMOUNT", _silkAmount);
                        cmd.Parameters.AddWithValue("@JIDNUM", _jid);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        Logger.Info(typeof(DBConnect), $"Added silk! Amount: {_silkAmount} - User Id: {_jid}");

                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error adding silk to JID {_jid}: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> EditQuestRewardsBetweenLevel(RewardType _type, decimal _mult, int _min, int _max)
        {
            string column = _type switch
            {
                RewardType.Experience => "EXP",
                RewardType.SkillExperience => "SPExp",
                RewardType.Gold => "Gold",
                RewardType.SkillPoints => "SP",
                _ => throw new ArgumentOutOfRangeException()
            };

            try
            {
                string msg;
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    var sql = string.Format(Constant.EditQuestRewardsBetweenLevel_q, column);
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@MULTIPLIER", _mult);
                        cmd.Parameters.AddWithValue("@BETWEENLEVEL", _min);
                        cmd.Parameters.AddWithValue("@ANDLEVEL", _max);
                        int AffectedRows = await cmd.ExecuteNonQueryAsync();
                        msg = $"Multiplied {column} by {_mult} between levels {_min} and {_max}";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error has occurred editting quest rewards: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> SetAlchemyRates(int _param2, int _param3, int _param4)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.SetAlchemyRates_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@PARAM2", _param2);
                        cmd.Parameters.AddWithValue("@PARAM3", _param3);
                        cmd.Parameters.AddWithValue("@PARAM4", _param4);
                        int affectedRows = await cmd.ExecuteNonQueryAsync();

                        string msg = $"Alchemy rates have been updated in database.";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error ocurred updating alchemy rates in DB: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);

            }
        }
        public static async Task<(bool success, string reason)> FixUniqueSpawns()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    int i = 0;
                    foreach (var query in Constant.FixUniqueSpawns_q)
                    {
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            int affected_rows = await cmd.ExecuteNonQueryAsync();

                        }
                        i++;
                        Logger.Info(typeof(DBConnect), $"{i}/{Constant.FixUniqueSpawns_q.Length} Uniques Fixed");
                    }
                }
                return (true, "All finished successfully");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error Adding NPC: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }

        }
        public static async Task<(bool success, string reason)> AddItemToShop(int _itemId, int _itemPrice, string _shopTabCodeName, int _data, int _currencyType = 0)
        {
            string message = "";
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    conn.InfoMessage += (s, ev) =>
                    {
                        message += ev.Message + Environment.NewLine;
                    };
                    using (SqlCommand cmd = new SqlCommand(Constant.AddItemToShop_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ITEM_AID", _itemId);
                        cmd.Parameters.AddWithValue("@PRICE", _itemPrice);
                        cmd.Parameters.AddWithValue("@TABCODENAME", _shopTabCodeName);
                        cmd.Parameters.AddWithValue("@MONEYTYPE", _currencyType);
                        cmd.Parameters.AddWithValue("@DATADUR", _data);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        string msg = $"Item successfully added to shop: {_shopTabCodeName} | Item: {_itemId} | CurrencyType: {(_currencyType == 0 ? "Gold" : "Silk")}";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, message);
                    }
                }
                
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error adding item to shop: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> AddReversePointToCharacterPosition(string _zoneName, int _posX, int  _posY, int _posZ, int _regionId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.AddReverseToCharacterPosition_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ZONENAME", _zoneName);
                        cmd.Parameters.AddWithValue("@REGIONCODEID", _regionId);
                        cmd.Parameters.AddWithValue("@POSXCLIENT", _posX);
                        cmd.Parameters.AddWithValue("@POSYCLIENT", _posY);
                        cmd.Parameters.AddWithValue("@POSZCLIENT", _posZ);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        Logger.Info(typeof(DBConnect), $"Added New Reverse Point!: {_zoneName}");
                        string msg = "Add the following to textdata_object.txt (use DB2PK2 after): 1\tSN_{ZoneName}\t\t\t\t\t\t\t\tYOUR_NAME_HERE";
                        return (affectedRows > 0, msg);
                        
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error adding reverse point to character position!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> ChangeFWDropRates(TalismanGroup _group, decimal _dropRatio, bool _shouldAffectAll, int _dropAmountMin, int _dropAmountMax)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    if (_shouldAffectAll == true)
                    {
                        using (SqlCommand cmd = new SqlCommand(Constant.ChangeFWDropRatesAll_q, conn))
                        {
                            cmd.Parameters.AddWithValue("@DROPRATIOFW", _dropRatio);
                            cmd.Parameters.AddWithValue("@DROPAMOUNTMIN", _dropAmountMin);
                            cmd.Parameters.AddWithValue("@DROPAMOUNTMAX", _dropAmountMax);
                            int affectedRows = await cmd.ExecuteNonQueryAsync();

                            string msg = $"All talismans changed to: ratio={_dropRatio}, min={_dropAmountMin}, max={_dropAmountMax}";
                            Logger.Info(typeof(DBConnect), msg);
                            return (true, msg);
                        }
                    }
                    else
                    {
                        using (SqlCommand cmd = new SqlCommand(Constant.ChangeFWDropRates_q, conn))
                        {
                            cmd.Parameters.AddWithValue("@TALISMANGROUP", _group);
                            cmd.Parameters.AddWithValue("@DROPRATIOFW", _dropRatio);
                            cmd.Parameters.AddWithValue("@DROPAMOUNTMIN", _dropAmountMin);
                            cmd.Parameters.AddWithValue("@DROPAMOUNTMAX", _dropAmountMax);
                            int affectedRows = cmd.ExecuteNonQuery();

                            string msg = $"{_group} changed to: ratio={_dropRatio}, min={_dropAmountMin}, max={_dropAmountMax}";
                            Logger.Info(typeof(DBConnect), msg);
                            return (true, msg);

                        }
                    }

                    
                }
                
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error changing Forgotten World drop rates!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }

        }
        public static async Task<(bool success, string reason)> ChangeMonsterSpawns(string _monsterCodeName, int _maxSpawnerCount, bool exact = false)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    var query = exact ? Constant.ChangeMonsterSpawnsByExact_q : Constant.ChangeMonsterSpawns_q;
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@MaxSpawnerCount", _maxSpawnerCount);
                        cmd.Parameters.AddWithValue("@MobCodeName", _monsterCodeName);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        string msg = $"Changed spawner count of {_monsterCodeName} to {_maxSpawnerCount}{Environment.NewLine}Spawners affected: {affectedRows}";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error changing monster spawn counts!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }

        public static async Task<(bool success, string reason)> ChangeMonsterSpawnsAllGroups(int _maxSpawnerCount)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    int totalAffected = 0;
                    int i = 0;
                    foreach (var prefix in Constant.MonsterAreaPrefixes)
                    {
                        using (SqlCommand cmd = new SqlCommand(Constant.ChangeMonsterSpawns_q, conn))
                        {
                            cmd.Parameters.AddWithValue("@MaxSpawnerCount", _maxSpawnerCount);
                            cmd.Parameters.AddWithValue("@MobCodeName", prefix);
                            int affected = await cmd.ExecuteNonQueryAsync();
                            totalAffected += affected;
                        }
                        i++;
                        Logger.Info(typeof(DBConnect), $"{i}/{Constant.MonsterAreaPrefixes.Length} groups updated — {prefix}: spawners set to {_maxSpawnerCount}");
                    }
                    string msg = $"All {Constant.MonsterAreaPrefixes.Length} monster groups updated to max {_maxSpawnerCount}{Environment.NewLine}Total spawners affected: {totalAffected}";
                    Logger.Info(typeof(DBConnect), msg);
                    return (true, msg);
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error changing all monster spawn counts!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason, List<(string MobCode, int MaxCount)>? data)> GetMonsterSpawnCounts(string _monsterCodeName)
        {
            try
            {
                var results = new List<(string MobCode, int MaxCount)>();
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.GetMonsterSpawnCounts_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@MobCodeName", _monsterCodeName);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                                results.Add((reader.GetString(0), reader.GetInt32(1)));
                        }
                    }
                }
                Logger.Info(typeof(DBConnect), $"Spawn count query for {_monsterCodeName}: {results.Count} entries");
                return (true, $"{results.Count} entries", results);
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error querying spawn counts: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg, null);
            }
        }

        public static async Task<(bool success, string reason)> AddItemToMonsterDrop(int _monsterId, int _itemId, decimal _dropRatio)
        {
            string query = " USE SRO_VT_SHARD\r\n\r\nDECLARE @MonsterID int\r\nDECLARE @ItemID int\r\nDECLARE @DropRatio real\r\n\r\n\r\n\r\nSET @MonsterID = @MONSTERIDADM\r\nSET @DropRatio = @DROPRATIOADM\r\nSET @ItemID = @ITEMIDADM\r\n\r\n\r\nINSERT _RefMonster_AssignedItemDrop \r\n(\r\nRefMonsterID,\r\nRefItemID,\r\nDropGroupType,\r\nOptLevel,\r\nDropAmountMin,\r\nDropAmountMax,\r\nDropRatio,\r\nRefMagicOptionID1,\r\nCustomValue1,\r\nRefMagicOptionID2,\r\nCustomValue2,\r\nRefMagicOptionID3,\r\nCustomValue3,\r\nRefMagicOptionID4,\r\nCustomValue4,\r\nRefMagicOptionID5,\r\nCustomValue5,\r\nRefMagicOptionID6,\r\nCustomValue6,\r\nRefMagicOptionID7,\r\nCustomValue7,\r\nRefMagicOptionID8,\r\nCustomValue8,\r\nRefMagicOptionID9,\r\nCustomValue9,\r\nRentCodeName\r\n) \r\nVALUES(@MonsterID , @ItemID, 0, 0, 1, 1, @DropRatio, 0, 0, 0, 0, 0, 0, 0, 0,\r\n0, 0, 0, 0, 0, 0, 0, 0, 0 , 0 , 'xxx') ";

            try
            {
                string msg;
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {

                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@MONSTERIDADM", _monsterId);
                        cmd.Parameters.AddWithValue("@DROPRATIOADM", _dropRatio);
                        cmd.Parameters.AddWithValue("@ITEMIDADM", _itemId);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();

                        msg = $"Added Item Drop To monster: {_monsterId} - item id: {_itemId} drop ratio: {_dropRatio}";
                        Logger.Info(typeof(DBConnect), msg);
                    }
                    return (true, msg);
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error adding item to monster drop!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> AddNewTeleporter(string _teleporterCodeName, int _goldFee, Town _townLink, string _fromCharacterName, string _toCharacterName, int _requiredLevel)
        {
            string query = "USE SRO_VT_SHARD\r\n\r\n\r\nDECLARE @Teleport VARCHAR (64)\r\nDECLARE @CHARNAME1 VARCHAR (30)\r\nDECLARE @CHARNAME2 VARCHAR (30)\r\nDECLARE @OwnTeleport INT\r\nDECLARE @Fee INT\r\nDECLARE @RequiredLVL INT\r\n\r\n\r\nSET @Teleport = @TELEPORTERNAME\r\nSET @CHARNAME1 = @TELEPORTFROMCHARNAME\r\nSET @CHARNAME2 = @TELEPORTTOCHARNAME\r\nSET @OwnTeleport = @TELEPORTERADDTOCITY \r\nSET @Fee = @GOLDFEE\r\nSET @RequiredLVL = @REQUIREDLEVEL\r\n\r\n\r\nIF EXISTS (SELECT CodeName128 FROM _RefObjCommon WHERE CodeName128 = 'STORE_'+@Teleport)\r\n BEGIN\r\n  raiserror('The stated teleportname of %s is already exist!',11,1,@Teleport);\r\n  RETURN;\r\n END\r\n    \r\n    DECLARE @MAXOBJ INT = (SELECT MAX (ID) FROM _RefObjCommon)+1\r\n    DECLARE @REGION1 INT SET @REGION1 = (SELECT (LatestRegion) FROM _Char WHERE CharName16 = @CHARNAME1)\r\n    DECLARE @POSX1 INT SET @POSX1 = (SELECT (POSX) FROM _Char WHERE CharName16 = @CHARNAME1)\r\n    DECLARE @POSY1 INT SET @POSY1 = (SELECT (POSY) FROM _Char WHERE CharName16 = @CHARNAME1)\r\n    DECLARE @POSZ1 INT SET @POSZ1 = (SELECT (POSZ) FROM _Char WHERE CharName16 = @CHARNAME1)\r\n    DECLARE @LINK INT = (SELECT MAX (ID) FROM _RefObjStruct)+1\r\n     \r\n\tSET IDENTITY_INSERT _RefObjCommon ON\r\n    INSERT INTO _RefObjCommon (Service,ID,CodeName128,ObjName128,OrgObjCodeName128,NameStrID128,DescStrID128,CashItem,Bionic,TypeID1,TypeID2,TypeID3,TypeID4,DecayTime,Country,Rarity,CanTrade,CanSell,CanBuy,CanBorrow,CanDrop,CanPick,CanRepair,CanRevive,CanUse,CanThrow,Price,CostRepair,CostRevive,CostBorrow,KeepingFee,SellPrice,ReqLevelType1,ReqLevel1,ReqLevelType2,ReqLevel2,ReqLevelType3,ReqLevel3,ReqLevelType4,ReqLevel4,MaxContain,RegionID,Dir,OffsetX,OffsetY,OffsetZ,Speed1,Speed2,Scale,BCHeight,BCRadius,EventID,AssocFileObj128,AssocFileDrop128,AssocFileIcon128,AssocFile1_128,AssocFile2_128,Link) VALUES\r\n    (1,@MAXOBJ,'STORE_'+@Teleport,@Teleport,'xxx','SN_STORE_'+@Teleport,'xxx',0,0,4,1,1,0,0,3,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,-1,0,-1,0,-1,0,-1,0,-1,@REGION1,0,@POSX1,@POSY1,@POSZ1,0,0,50,50,30,0,'quest\\teleport01.bsr','xxx','xxx','xxx','xxx',@link)\r\n    SET IDENTITY_INSERT _RefObjCommon OFF\r\n\t\r\n\tprint ''\r\n\tprint 'Add the following line to textdata_object.txt @server_dep\\silkroad\\textdata folder'\r\n\tprint ''\r\n\tprint '1\t'+'SN_STORE_'+@Teleport+'\t\t\t\t\t\t\t\t'+@Teleport\r\n\tprint ''\r\n  \tprint ''\r\n\r\n  SET IDENTITY_INSERT _RefObjStruct ON\r\n  INSERT INTO _RefObjStruct (ID,Dummy_Data) VALUES\r\n  (@LINK,0)\r\n  SET IDENTITY_INSERT _RefObjStruct OFF\r\n  \r\n  DECLARE @MAXTELID INT = (SELECT MAX (ID) FROM _RefTeleport)+1\r\n  DECLARE @WORLDID INT = (SELECT (WorldID) FROM _Char where CharName16 = @CHARNAME2)\r\n  DECLARE @REGION2 INT SET @REGION2 = (SELECT (LatestRegion) FROM _Char WHERE CharName16 = @CHARNAME2)\r\n  DECLARE @POSX2 INT SET @POSX2 = (SELECT (POSX) FROM _Char WHERE CharName16 = @CHARNAME2)\r\n  DECLARE @POSY2 INT SET @POSY2 = (SELECT (POSY) FROM _Char WHERE CharName16 = @CHARNAME2)\r\n  DECLARE @POSZ2 INT SET @POSZ2 = (SELECT (POSZ) FROM _Char WHERE CharName16 = @CHARNAME2)\r\n\r\n  SET IDENTITY_INSERT _RefTeleport ON\r\n  INSERT INTO _RefTeleport (Service,ID,CodeName128,AssocRefObjCodeName128,AssocRefObjID,ZoneName128,GenRegionID,GenPos_X,GenPos_Y,GenPos_Z,GenAreaRadius,CanBeResurrectPos,CanGotoResurrectPos,GenWorldID,BindInteractionMask,FixedService) VALUES\r\n  (1,@MAXTELID,'GATE_'+@Teleport,'STORE_'+@Teleport,@MAXOBJ,'SN_STORE_'+@Teleport,@REGION2,@POSX2,@POSY2,@POSZ2,30,0,0,@WORLDID,1,0)\r\n  SET IDENTITY_INSERT _RefObjStruct OFF\r\n\r\n \r\n\r\n  IF @RequiredLVL = 0\r\n  BEGIN\r\n    INSERT INTO _RefTeleLink (Service,OwnerTeleport,TargetTeleport,Fee,RestrictBindMethod,RunTimeTeleportMethod,CheckResult,Restrict1,Data1_1,Data1_2,Restrict2,Data2_1,Data2_2,Restrict3,Data3_1,Data3_2,Restrict4,Data4_1,Data4_2,Restrict5,Data5_1,Data5_2) VALUES\r\n    (1,@OwnTeleport,@MAXTELID,@Fee,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)\r\n\tINSERT INTO _RefTeleLink (Service,OwnerTeleport,TargetTeleport,Fee,RestrictBindMethod,RunTimeTeleportMethod,CheckResult,Restrict1,Data1_1,Data1_2,Restrict2,Data2_1,Data2_2,Restrict3,Data3_1,Data3_2,Restrict4,Data4_1,Data4_2,Restrict5,Data5_1,Data5_2) VALUES\r\n    (1,@MAXTELID,@OwnTeleport,@Fee,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0)\r\n\t\r\n\r\n  END\r\n  ELSE BEGIN\r\n\tINSERT INTO _RefTeleLink (Service,OwnerTeleport,TargetTeleport,Fee,RestrictBindMethod,RunTimeTeleportMethod,CheckResult,Restrict1,Data1_1,Data1_2,Restrict2,Data2_1,Data2_2,Restrict3,Data3_1,Data3_2,Restrict4,Data4_1,Data4_2,Restrict5,Data5_1,Data5_2) VALUES\r\n    (1,@OwnTeleport,@MAXTELID,@Fee,0,0,0,1,@RequiredLVL,999,0,0,0,0,0,0,0,0,0,0,0,0)\r\n\r\n  END\r\n\r\nprint ''\r\nprint 'Done!'";
            string message = "";
            int cityLink = GetTownLink(_townLink);
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.InfoMessage += (s, e) =>
                {
                    message += e.Message + Environment.NewLine;
                };
                await conn.OpenAsync();
                try
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TELEPORTERNAME", _teleporterCodeName);
                        cmd.Parameters.AddWithValue("@TELEPORTFROMCHARNAME", _fromCharacterName);
                        cmd.Parameters.AddWithValue("@TELEPORTTOCHARNAME", _toCharacterName);
                        cmd.Parameters.AddWithValue("@TELEPORTERADDTOCITY", cityLink);
                        cmd.Parameters.AddWithValue("@GOLDFEE", _goldFee);
                        cmd.Parameters.AddWithValue("@REQUIREDLEVEL", _requiredLevel);

                        await cmd.ExecuteNonQueryAsync();
                        Logger.Info(typeof(DBConnect), $"Added new teleporter: {_teleporterCodeName} at {_fromCharacterName}'s position.");
                    }
                    return (true, message);
                }
                catch (Exception ex)
                {
                    string msg = $"FERROR: Error adding new teleporter!: {ex.Message}";
                    Logger.Error(typeof(DBConnect), msg);
                    return (false, msg);
                }
            }
        }
        public static async Task<(bool success, string reason)> ChangeItemMaxStack(string _itemCodeName, int _newStackSize)
        {
            
            try
            {
                string msg;
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.ChangeItemMaxStack_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@MAXITEMSTACK", _newStackSize);
                        cmd.Parameters.AddWithValue("@ITEMCODENAME", _itemCodeName);
                        int affectedRows = await cmd.ExecuteNonQueryAsync();
                        msg = $"Changed max stack of {_itemCodeName} to {_newStackSize}";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error editting max item stack for {_itemCodeName}!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> ChangePrivelagedIp(int nIdx, string ip, bool szGM)
        {
            string msg;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.UpdatePrivilegedIp_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@nIdx", nIdx);
                        cmd.Parameters.AddWithValue("@szIPBegin", ip);
                        cmd.Parameters.AddWithValue("@szIPEnd", ip);
                        cmd.Parameters.AddWithValue("@szGM", szGM ? "Yes" : "No");

                        // since it's NOT NULL, you MUST provide it
                        cmd.Parameters.AddWithValue("@dIssueDate", DateTime.Now);

                        // nullable column → safe to pass DBNull
                        cmd.Parameters.AddWithValue("@szISP", DBNull.Value);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();

                        if (affectedRows == 0)
                        {
                            msg = $"No rows updated for nIdx:{nIdx}";
                            return (false, msg);
                        }

                        msg = $"Modified privelaged IP for nIdx:{nIdx}";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                msg = $"FERROR: Error changing privelaged IP!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> SetRegionAssoc(string areaName, bool enabled)
        {
            string msg;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.UpdateRegionAssoc_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@AreaName", areaName);
                        cmd.Parameters.AddWithValue("@AssocServer", enabled ? 1 : 0);

                        int affectedRows = await cmd.ExecuteNonQueryAsync();

                        if (affectedRows == 0)
                        {
                            msg = $"No region found for AreaName:{areaName}";
                            return (false, msg);
                        }

                        msg = $"Updated region '{areaName}' → {(enabled ? "Enabled" : "Disabled")}";
                        Logger.Info(typeof(DBConnect), msg);
                        return (true, msg);
                    }
                }
            }
            catch (Exception ex)
            {
                msg = $"FERROR: Error updating region assoc!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        
        // Truncate
        public static async Task<(bool success, string reason)> TruncateCharactersByJID(int jid)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.TruncateCharactersByJID_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@DUSERID", jid);
                        await cmd.ExecuteNonQueryAsync();
                        return (true, $"Characters for JID {jid} deleted successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error deleting characters for JID {jid}: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> TruncateOnlineUsersForShutdown()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(Constant.TruncateIPLogs_q, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                        return (true, "Online user table truncated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error truncating online user table!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }

        #endregion

        #region - Textdata Generate -

        // ALL TEXTDATA LOGIC INCLUDING GETTERS SHOULD BE HERE
        public static async Task<(bool success, string reason)> DumpAllData()
        {
            TextdataGenerationRunning = true;
            TextdataGenerationProgress = 0;
            Stopwatch sw = new Stopwatch();
            try
            {
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "textdata"));
                DeleteAllFilesInDirectory();

                Logger.Info(typeof(DBConnect), "Dumping all textdata from database for client...");
                sw.Start();
                await Task.Run(() => DumpQuestData());           TextdataGenerationProgress = 12;
                await Task.Run(() => DumpTeleportData());        TextdataGenerationProgress = 24;
                await Task.Run(() => DumpRefPackageData());      TextdataGenerationProgress = 36;
                await Task.Run(() => DumpRegionData()); TextdataGenerationProgress = 48;
                await Task.Run(() => OptionalTeleportDump());    TextdataGenerationProgress = 60;
                await Task.Run(() => DumpSkillData(GetAmountOfPages(RetInt("USE SRO_VT_SHARD Select MAX(ID) from _RefSkill where [Service] = 1;"))));
                TextdataGenerationProgress = 72;
                await Task.Run(() => DumpItemData(GetAmountOfPages(RetInt("USE SRO_VT_SHARD Select MAX(ID) from _RefObjCommon where [Service] = 1;"))));
                TextdataGenerationProgress = 86;
                await Task.Run(() => DumpCharacterData(GetAmountOfPages(RetInt("USE SRO_VT_SHARD Select MAX(ID) from _RefObjCommon WHERE (CodeName128 Like 'MOB%' OR CodeName128 like 'NPC%' OR CodeName128 like 'CHAR%' OR CodeName128 like 'COS%' OR CodeName128 like 'STRUCTURE%' OR CodeName128 like 'MOV%') AND [Service] = 1;"))));
                TextdataGenerationProgress = 100;
                sw.Stop();
                Logger.Info(typeof(DBConnect), $"All Client Files finished generating in {sw.ElapsedMilliseconds} milliseconds");
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping all data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
            finally
            {
                TextdataGenerationRunning = false;
            }
        }
        public static async Task<(bool success, string reason)> DumpItemData(int RoundedPages)
        {
            try
            {
                InitializeTable(ref tables!);
                List<string> lines = new List<string>();
                for (int i = 1; i <= RoundedPages; i++)
                    lines.Add($"itemdata_{5000 * i}.txt");
                WriteCustomLines($"{Environment.CurrentDirectory}\\textdata\\itemdata.txt", lines);


                //Dump Itemdata_XXX sectors
                for (int i = 1; i <= RoundedPages; i++)
                {
                    //AddProgressToBar(3);
                    await Construct($"itemdata_{5000 * i}", $"USE SRO_VT_SHARD Select _RefObjCommon.[Service], _RefObjCommon.ID, _RefObjCommon.CodeName128, _RefObjCommon.ObjName128, _RefObjCommon.OrgObjCodeName128, _RefObjCommon.NameStrID128, _RefObjCommon.DescStrID128, _RefObjCommon.CashItem, _RefObjCommon.Bionic, _RefObjCommon.TypeID1, _RefObjCommon.TypeID2, _RefObjCommon.TypeID3, _RefObjCommon.TypeID4, _RefObjCommon.DecayTime, _RefObjCommon.Country, _RefObjCommon.Rarity, _RefObjCommon.CanTrade, _RefObjCommon.CanSell, _RefObjCommon.CanBuy, _RefObjCommon.CanBorrow, _RefObjCommon.CanDrop, _RefObjCommon.CanPick, _RefObjCommon.CanRepair, _RefObjCommon.CanRevive, _RefObjCommon.CanUse, _RefObjCommon.CanThrow, _RefObjCommon.Price, _RefObjCommon.CostRepair, _RefObjCommon.CostRevive, _RefObjCommon.CostBorrow, _RefObjCommon.KeepingFee, _RefObjCommon.SellPrice, _RefObjCommon.ReqLevelType1, _RefObjCommon.ReqLevel1, _RefObjCommon.ReqLevelType2, _RefObjCommon.ReqLevel2, _RefObjCommon.ReqLevelType3, _RefObjCommon.ReqLevel3, _RefObjCommon.ReqLevelType4, _RefObjCommon.ReqLevel4, _RefObjCommon.MaxContain, _RefObjCommon.RegionID, _RefObjCommon.Dir, _RefObjCommon.OffsetX, _RefObjCommon.OffsetY, _RefObjCommon.OffsetZ, _RefObjCommon.Speed1, _RefObjCommon.Speed2, _RefObjCommon.Scale, _RefObjCommon.BCHeight, _RefObjCommon.BCRadius, _RefObjCommon.EventID, _RefObjCommon.AssocFileObj128, _RefObjCommon.AssocFileDrop128, _RefObjCommon.AssocFileIcon128, _RefObjCommon.AssocFile1_128, _RefObjCommon.AssocFile2_128, _RefObjItem.MaxStack, _RefObjItem.ReqGender, _RefObjItem.ReqStr, _RefObjItem.ReqInt, _RefObjItem.ItemClass, _RefObjItem.SetID, cast(_RefObjItem.Dur_L as decimal(10,1)) 'Dur_L', CAST(_RefObjItem.Dur_U as decimal(10,1)) 'Dur_U', CAST(_RefObjItem.PD_L as decimal(10,1)) 'PD_L', CAST(_RefObjItem.PD_U as decimal(10,1)) 'PD_U', CAST(_RefObjItem.PDInc as decimal(10,1)) 'DD_Inc', CAST(_RefObjItem.ER_L as decimal(10,1)) 'ER_L', CAST(_RefObjItem.ER_U as decimal(10,1)) 'ER_U', CAST(_RefObjItem.ERInc as decimal(10,1)) 'ERInc', CAST(_RefObjItem.PAR_L as decimal(10,1)) 'PAR_L', CAST(_RefObjItem.PAR_U as decimal(10,1)) 'PAR_U', CAST(_RefObjItem.PARInc as decimal(10,1)) 'PARInc', CAST(_RefObjItem.BR_L as decimal(10,1)) 'BR_L', CAST(_RefObjItem.BR_U as decimal(10,1)) 'BR_U', CAST(_RefObjItem.MD_L as decimal(10,1)) 'MD_L', CAST(_RefObjItem.MD_U as decimal(10,1)) 'DM_L', CAST(_RefObjItem.MDInc as decimal(10,1)) 'MDInc', CAST(_RefObjItem.MAR_L as decimal(10,1)) 'MAR_L', CAST(_RefObjItem.MAR_U as decimal(10,1)) 'MAR_U', CAST(_RefObjItem.MARInc as decimal(10,1)) 'MARInc', CAST(_RefObjItem.PDStr_L as decimal(10,1)) 'PDStr_L', CAST(_RefObjItem.PDStr_U as decimal(10,1)) 'PDStr_U', CAST(_RefObjItem.MDInt_L as decimal(10,1)) 'MDInt_L', CAST(_RefObjItem.MDInt_U as decimal(10,1)) 'MDInt_U', _RefObjItem.Quivered, _RefObjItem.Ammo1_TID4, _RefObjItem.Ammo2_TID4, _RefObjItem.Ammo3_TID4, _RefObjItem.Ammo4_TID4, _RefObjItem.Ammo5_TID4, _RefObjItem.SpeedClass, _RefObjItem.TwoHanded, _RefObjItem.Range, CAST(_RefObjItem.PAttackMin_L as decimal(10,1)) 'PAttackMin_L', CAST(_RefObjItem.PAttackMin_U as decimal(10,1)) 'PAttackMin_U', CAST(_RefObjItem.PAttackMax_L as decimal(10,1)) 'PAttackMax_L', CAST(_RefObjItem.PAttackMax_U as decimal(10,1)) 'PAttackMax_U', CAST(_RefObjItem.PAttackInc as decimal(10,1)) 'PAttackInc', CAST(_RefObjItem.MAttackMin_L as decimal(10,1)) 'MAttackMin_L', CAST(_RefObjItem.MAttackMin_U as decimal(10,1)) 'MAttackMin_U', CAST(_RefObjItem.MAttackMax_L as decimal(10,1)) 'MAttackMax_L', CAST(_RefObjItem.MAttackMax_U as decimal(10,1)) 'MAttackMax_U', CAST(_RefObjItem.MAttackInc as decimal(10,1)) 'MAttackInc', CAST(_RefObjItem.PAStrMin_L as decimal(10,1)) 'PAStrMin_L', CAST(_RefObjItem.PAStrMin_U as decimal(10,1)) 'PAStrMin_U', CAST(_RefObjItem.PAStrMax_L as decimal(10,1)) 'PAStrMax_L', CAST(_RefObjItem.PAStrMax_U as decimal(10,1)) 'PAStrMax_U', CAST(_RefObjItem.MAInt_Min_L as decimal(10,1)) 'MAInt_Min_L', CAST(_RefObjItem.MAInt_Min_U as decimal(10,1)) 'MAInt_Min_U', CAST(_RefObjItem.MAInt_Max_L as decimal(10,1)) 'MAInt_Max_L', CAST(_RefObjItem.MAInt_Max_U as decimal(10,1)) 'MAInt_Max_U', CAST(_RefObjItem.HR_L as decimal(10,1)) 'HR_L', CAST(_RefObjItem.HR_U as decimal(10,1)) 'HR_U', CAST(_RefObjItem.HRInc as decimal(10,1)) 'HRInc', CAST(_RefObjItem.CHR_L as decimal(10,1)) 'CHR_L', CAST(_RefObjItem.CHR_U as decimal(10,1)) 'CHR_U', _RefObjItem.Param1, _RefObjItem.Desc1_128, _RefObjItem.Param2,  RTRIM(LTRIM(_RefObjItem.Desc2_128)) AS 'Desc2_128', _RefObjItem.Param3, _RefObjItem.Desc3_128, _RefObjItem.Param4, _RefObjItem.Desc4_128, _RefObjItem.Param5, _RefObjItem.Desc5_128, _RefObjItem.Param6, _RefObjItem.Desc6_128, _RefObjItem.Param7, _RefObjItem.Desc7_128, _RefObjItem.Param8, _RefObjItem.Desc8_128, _RefObjItem.Param9, _RefObjItem.Desc9_128, _RefObjItem.Param10, _RefObjItem.Desc10_128, _RefObjItem.Param11, _RefObjItem.Desc11_128, _RefObjItem.Param12, _RefObjItem.Desc12_128, _RefObjItem.Param13, _RefObjItem.Desc13_128, _RefObjItem.Param14, _RefObjItem.Desc14_128, _RefObjItem.Param15, _RefObjItem.Desc15_128, _RefObjItem.Param16, _RefObjItem.Desc16_128, _RefObjItem.Param17, _RefObjItem.Desc17_128, _RefObjItem.Param18, _RefObjItem.Desc18_128, _RefObjItem.Param19, _RefObjItem.Desc19_128, _RefObjItem.Param20, _RefObjItem.Desc20_128, _RefObjItem.MaxMagicOptCount, _RefObjItem.ChildItemCount FROM _RefObjCommon INNER JOIN _RefObjItem ON _RefObjCommon.Link = _RefObjItem.ID WHERE _RefObjCommon.[Service] = 1 AND _RefObjCommon.ID BETWEEN {(5000 * i - 1) - 5000 + 1} AND {(5000 * i - 1)} AND _RefObjCommon.CodeName128 like 'ITEM%' order by ID asc;");

                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping item data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> DumpSkillData(int RoundedPages)
        {
            try
            {
                InitializeTable(ref tables!);
                List<string> lines = new List<string>();
                for (int i = 1; i <= RoundedPages; i++)
                    lines.Add($"SkillData_{5000 * i}.txt");

                WriteCustomLines($"{Environment.CurrentDirectory}\\textdata\\skilldata.txt", lines);

                List<string> enc_lines = new List<string>();
                for (int i = 1; i <= RoundedPages; i++)
                    enc_lines.Add($"skilldata_{5000 * i}ENC.txt");

                WriteCustomLines($"{Environment.CurrentDirectory}\\textdata\\skilldataenc.txt", enc_lines);

                for (int i = 1; i <= RoundedPages; i++)
                {
                    await Construct($"skilldata_{5000 * i}", $"USE SRO_VT_SHARD Select * FROM _RefSkill WHERE [Service] = 1 AND ID BETWEEN {(5000 * i - 1) - 5000 + 1} AND {(5000 * i - 1)} order by ID asc;");
                    //AddProgressToBar(2);
                    Encryptor.Encrypt($"{Environment.CurrentDirectory}\\textdata\\skilldata_{5000 * i}.txt");
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping skill data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> DumpQuestData()
        {
            try
            {
                InitializeTable(ref tables!);
                foreach (KeyValuePair<string, string> pair in tables)
                {
                    if (pair.Key == "questdata")
                    {
                        //AddProgressToBar(2);
                        await Construct(pair.Key, pair.Value);
                        continue;
                    }

                    if (pair.Key == "refqusetreward")
                    {
                        //AddProgressToBar(2);
                        await Construct(pair.Key, pair.Value);
                        continue;
                    }

                    if (pair.Key == "refquestrewarditems")
                    {
                        //AddProgressToBar(2);
                        await Construct(pair.Key, pair.Value);
                        continue;
                    }

                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping quest data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, string reason)> DumpRefPackageData()
        {
            try
            {
                InitializeTable(ref tables!);
                foreach (KeyValuePair<string, string> pair in tables)
                {
                    if (pair.Key == "refpackageitem")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(3);
                        continue;
                    }
                    if (pair.Key == "refpricepolicyofitem")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(3);
                        continue;
                    }
                    if (pair.Key == "refscrapofpackageitem")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(3);
                        continue;
                    }
                    if (pair.Key == "refshopgoods")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(4);
                        continue;
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping package data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
            
        }
        public static async Task<(bool success, string reason)> OptionalTeleportDump()
        {
            try
            {
                InitializeTable(ref tables!);
                foreach (KeyValuePair<string, string> pair in tables)
                {
                    if (pair.Key == "refoptionalteleport")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(4);
                        continue;
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping optional teleport data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
            
        }
        public static async Task<(bool success, string reason)> DumpCharacterData(int RoundedPages)
        {
            try
            {
                InitializeTable(ref tables!);
                List<string> lines = new List<string>();
                for (int i = 1; i <= RoundedPages; i++)
                    lines.Add($"CharacterData_{5000 * i}.txt");
                WriteCustomLines($"{Environment.CurrentDirectory}\\textdata\\characterdata.txt", lines);


                for (int i = 1; i <= RoundedPages; i++)
                {
                    //AddProgressToBar(2);
                    await Construct($"characterdata_{5000 * i}", $"USE SRO_VT_SHARD Select _RefObjCommon.[Service], _RefObjCommon.ID, _RefObjCommon.CodeName128, _RefObjCommon.ObjName128, _RefObjCommon.OrgObjCodeName128, _RefObjCommon.NameStrID128, _RefObjCommon.DescStrID128, _RefObjCommon.CashItem, _RefObjCommon.Bionic, _RefObjCommon.TypeID1, _RefObjCommon.TypeID2, _RefObjCommon.TypeID3, _RefObjCommon.TypeID4, _RefObjCommon.DecayTime, _RefObjCommon.Country, _RefObjCommon.Rarity, _RefObjCommon.CanTrade, _RefObjCommon.CanSell, _RefObjCommon.CanBuy, _RefObjCommon.CanBorrow, _RefObjCommon.CanDrop, _RefObjCommon.CanPick, _RefObjCommon.CanRepair, _RefObjCommon.CanRevive, _RefObjCommon.CanUse, _RefObjCommon.CanThrow, _RefObjCommon.Price, _RefObjCommon.CostRepair, _RefObjCommon.CostRevive, _RefObjCommon.CostBorrow, _RefObjCommon.KeepingFee, _RefObjCommon.SellPrice, _RefObjCommon.ReqLevelType1, _RefObjCommon.ReqLevel1, _RefObjCommon.ReqLevelType2, _RefObjCommon.ReqLevel2, _RefObjCommon.ReqLevelType3, _RefObjCommon.ReqLevel3, _RefObjCommon.ReqLevelType4, _RefObjCommon.ReqLevel4, _RefObjCommon.MaxContain, _RefObjCommon.RegionID, _RefObjCommon.Dir, _RefObjCommon.OffsetX, _RefObjCommon.OffsetY, _RefObjCommon.OffsetZ, _RefObjCommon.Speed1, _RefObjCommon.Speed2, _RefObjCommon.Scale, _RefObjCommon.BCHeight, _RefObjCommon.BCRadius, _RefObjCommon.EventID, _RefObjCommon.AssocFileObj128, _RefObjCommon.AssocFileDrop128, _RefObjCommon.AssocFileIcon128, _RefObjCommon.AssocFile1_128, _RefObjCommon.AssocFile2_128, _RefObjChar.Lvl, _RefObjChar.CharGender, _RefObjChar.MaxHP, _RefObjChar.MaxMP, _RefObjChar.InventorySize, _RefObjChar.CanStore_TID1, _RefObjChar.CanStore_TID2, _RefObjChar.CanStore_TID3, _RefObjChar.CanStore_TID4, _RefObjChar.CanBeVehicle, _RefObjChar.CanControl, _RefObjChar.DamagePortion, _RefObjChar.MaxPassenger, _RefObjChar.AssocTactics, _RefObjChar.PD, _RefObjChar.MD, _RefObjChar.PAR, _RefObjChar.MAR, _RefObjChar.ER, _RefObjChar.BR, _RefObjChar.HR, _RefObjChar.CHR, _RefObjChar.ExpToGive, _RefObjChar.CreepType, _RefObjChar.Knockdown, _RefObjChar.KO_RecoverTime, _RefObjChar.DefaultSkill_1, _RefObjChar.DefaultSkill_2, _RefObjChar.DefaultSkill_3, _RefObjChar.DefaultSkill_4, _RefObjChar.DefaultSkill_5, _RefObjChar.DefaultSkill_6, _RefObjChar.DefaultSkill_7, _RefObjChar.DefaultSkill_8, _RefObjChar.DefaultSkill_9, _RefObjChar.DefaultSkill_10, _RefObjChar.TextureType, _RefObjChar.Except_1, _RefObjChar.Except_2, _RefObjChar.Except_3, _RefObjChar.Except_4, _RefObjChar.Except_5, _RefObjChar.Except_6, _RefObjChar.Except_7, _RefObjChar.Except_8, _RefObjChar.Except_9, _RefObjChar.Except_10 FROM _RefObjCommon INNER JOIN _RefObjChar On _RefObjCommon.Link = _RefObjChar.ID WHERE _RefObjCommon.[Service] = 1 AND _RefObjCommon.ID BETWEEN {(5000 * i - 1) - 5000 + 1} AND {(5000 * i - 1)} AND TypeID1 = 1 and ((TypeID2 = 2) OR (TypeID2 = 1 AND TypeID3 = 0 AND TypeID4 = 0)) order by ID asc;");

                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping character data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
            
        }
        public static async Task<(bool success, string reason)> DumpTeleportData()
        {
            try
            {
                InitializeTable(ref tables!);
                foreach (KeyValuePair<string, string> pair in tables)
                {
                    if (pair.Key == "teleportbuilding")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(3);
                        continue;

                    }

                    if (pair.Key == "teleportdata")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(3);
                        continue;
                    }

                    if (pair.Key == "teleportlink")
                    {
                        await Construct(pair.Key, pair.Value);
                        //AddProgressToBar(4);
                        continue;
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping teleport data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }   
        }
        public static async Task<(bool success, string reason)> DumpRegionData()
        {
            try
            {
                InitializeTable(ref tables!);
                foreach (KeyValuePair<string, string> pair in tables)
                {
                    if (pair.Key == "refregion")
                    {
                        await Construct(pair.Key, pair.Value);
                        continue;
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error dumping region data!: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
      
        #endregion

        #region - Helpers -

        private static int RetInt(string query)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (SqlCommand cmd = new SqlCommand(query, con))
                        return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception EX) { Logger.Error(typeof(DBConnect), "Fatal Error: " + EX.Message); return 0; }
        }
        private static void WriteCustomLines(string path, List<string> lines)
        {
            using (FileStream stream = File.OpenWrite(path))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.Unicode))
            {
                if (lines.Count > 0)
                {
                    for (int i = 0; i < lines.Count - 1; i++)
                    {
                        writer.WriteLine(lines[i]);
                    }
                    writer.Write(lines[lines.Count - 1]);
                }
            }
        }
        private static int GetAmountOfPages(int tot_lines_count)
        {
            return (tot_lines_count + 5000 - 1) / 5000;
        }
        private static async Task<(bool success, string reason)> Construct(string filename, string query)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand($"{query}", conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            List<string> lines = new List<string>();
                            while (await reader.ReadAsync()) //Reading row by row
                            {

                                string line = string.Empty; //We are building a string that contains all the columns in the line

                                for (int i = 0; i < reader.FieldCount; i++) //Looping through all available columns
                                {
                                    string column = reader.GetValue(i).ToString()!;
                                    if (filename == "refshopitemstockperiod" && (i == 4 || i == 5)) //Certain date correction
                                        column = Convert.ToDateTime(column).ToString("yyyy-MM-dd hh:mm:ss");
                                    if (i != (reader.FieldCount - 1)) //If its not the last column in the row...
                                        column += "\t";


                                    line += column;
                                }
                                lines.Add(line);
                            }
                            WriteCustomLines($"{Environment.CurrentDirectory}\\textdata\\{filename}.txt", lines);
                        }
                    }
                }
                return (true, "");
            }
            catch (Exception ex)
            {
                string msg = $"Error constructing data: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }

        }
        private static void DeleteAllFilesInDirectory()
        {
            DirectoryInfo di = new DirectoryInfo($"{Environment.CurrentDirectory}//textdata");

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }
        private static int GetTownLink(Town _town)
        {
            return Constant.TownLinks.TryGetValue(_town, out var value) ? value : -1;
        }

        #endregion

        #region - Raw Query -

        public record QueryResult(string[] Columns, List<string?[]> Rows, int RowsAffected, string? Error);
        public static async Task<QueryResult> ExecuteRawQuery(string sql)
        {
            try
            {
                using SqlConnection conn = new(_connectionString);
                await conn.OpenAsync();

                using SqlCommand cmd = new(sql, conn);
                cmd.CommandTimeout = 30;

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (reader.FieldCount == 0)
                {
                    // Non-SELECT (INSERT/UPDATE/DELETE) — reader won't have rows
                    int affected = reader.RecordsAffected;
                    return new QueryResult([], [], affected, null);
                }

                string[] columns = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.GetName(i))
                    .ToArray();

                var rows = new List<string?[]>();
                while (await reader.ReadAsync())
                {
                    var row = new string?[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                    rows.Add(row);
                }

                return new QueryResult(columns, rows, rows.Count, null);
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"ExecuteRawQuery error: {ex.Message}");
                return new QueryResult([], [], 0, ex.Message);
            }
        }

        #endregion

        #region - Schema Cache -

        private static readonly string _schemaCachePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs", "schema.json");

        /// <summary>
        /// Returns the persisted schema from disk, or null if it hasn't been built yet.
        /// </summary>
        public static Dictionary<string, Dictionary<string, List<string>>>? LoadSchemaCache()
        {
            try
            {
                if (!File.Exists(_schemaCachePath)) return null;
                string json = File.ReadAllText(_schemaCachePath);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<
                    Dictionary<string, Dictionary<string, List<string>>>>(json);
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Schema cache load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Queries INFORMATION_SCHEMA across all known databases, saves result to disk, and returns it.
        /// </summary>
        public static async Task<Dictionary<string, Dictionary<string, List<string>>>> BuildAndSaveSchema()
        {
            var result = new Dictionary<string, Dictionary<string, List<string>>>();
            var databases = new[] { "SRO_VT_SHARD", "SRO_VT_ACCOUNT" };

            foreach (var db in databases)
            {
                var tables = new Dictionary<string, List<string>>();
                try
                {
                    using SqlConnection conn = new(_connectionString);
                    await conn.OpenAsync();

                    string query = $@"
                        SELECT c.TABLE_NAME, c.COLUMN_NAME
                        FROM [{db}].INFORMATION_SCHEMA.TABLES  t
                        JOIN [{db}].INFORMATION_SCHEMA.COLUMNS c
                          ON  t.TABLE_NAME   = c.TABLE_NAME
                          AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
                        WHERE t.TABLE_TYPE = 'BASE TABLE'
                        ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";

                    using SqlCommand cmd = new(query, conn);
                    cmd.CommandTimeout = 60;
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string tName = reader.GetString(0);
                        string cName = reader.GetString(1);
                        if (!tables.TryGetValue(tName, out var cols))
                            tables[tName] = cols = new List<string>();
                        cols.Add(cName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(typeof(DBConnect), $"Schema build failed for {db}: {ex.Message}");
                }
                result[db] = tables;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_schemaCachePath)!);
                File.WriteAllText(_schemaCachePath,
                    Newtonsoft.Json.JsonConvert.SerializeObject(result,
                        Newtonsoft.Json.Formatting.Indented));
                Logger.Info(typeof(DBConnect), $"Schema cache saved to {_schemaCachePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"Schema cache save failed: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region - Proxy -

        public static async Task<(bool success, string reason)> AddPlayTimeAsync(string charName, TimeSpan playTime)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.AddPlayTime_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);
                        cmd.Parameters.AddWithValue("@Seconds", (long)playTime.TotalSeconds);

                        await cmd.ExecuteNonQueryAsync();

                        return (true, "");
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"FERROR: Error saving playtime for {charName}: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, msg);
            }
        }
        public static async Task<(bool success, long seconds, string reason)> GetPlayTimeAsync(string charName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(Constant.GetPlayTime_q, conn))
                    {
                        cmd.Parameters.AddWithValue("@CharName", charName);

                        object result = await cmd.ExecuteScalarAsync();

                        if (result != null)
                            return (true, Convert.ToInt64(result), "");

                        return (false, 0, "Character not found");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, 0, ex.Message);
            }
        }

        /// <summary>
        /// Batch-fetches AssocFileIcon128 paths from _RefObjCommon for the supplied code names.
        /// Returns a case-insensitive dictionary of CodeName → icon path (e.g. item\Avatar\foo.ddj).
        /// Missing entries are simply absent from the result.
        /// </summary>
        public static async Task<Dictionary<string, string>> GetItemIconPaths(IEnumerable<string> codeNames)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var list = codeNames.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
            if (list.Count == 0) return result;

            // Build parameterised IN-list
            var paramNames = list.Select((_, i) => $"@p{i}").ToList();
            string sql = string.Format(Constant.GetItemIconPaths_q, string.Join(",", paramNames));

            try
            {
                using var conn = OpenConnection("SRO_VT_SHARD");
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                for (int i = 0; i < list.Count; i++)
                    cmd.Parameters.AddWithValue($"@p{i}", list[i]);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string code = reader["CodeName128"]?.ToString() ?? "";
                    string icon = reader["AssocFileIcon128"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(icon))
                        result[code] = icon;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"GetItemIconPaths error: {ex.Message}");
            }

            return result;
        }
        public static async Task<HashSet<int>> GetRegionIds()
        {
            var result = new HashSet<int>();

            try
            {
                using var conn = OpenConnection("SRO_VT_SHARD");
                await conn.OpenAsync();

                using var cmd = new SqlCommand(Constant.GetIdsFromRefRegion, conn);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (reader["wRegionID"] != DBNull.Value)
                    {
                        result.Add(Convert.ToInt32(reader["wRegionID"]));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(DBConnect), $"GetRegionIds error: {ex.Message}");
            }

            return result;
        }
        
        #endregion

        #region - Characters -

        public static async Task<(bool success, List<CharacterRecord> characters, string reason)> GetCharactersByJID(int jid)
        {
            var list = new List<CharacterRecord>();
            try
            {
                using SqlConnection conn = OpenConnection("SRO_VT_SHARD");
                await conn.OpenAsync();

                using SqlCommand cmd = new(Constant.GetCharactersByJID_q, conn);
                cmd.Parameters.AddWithValue("@JID", jid);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new CharacterRecord
                    {
                        Login    = reader["Login"].ToString()   ?? "",
                        CharName = reader["CharName"].ToString() ?? "",
                        SilkOwn  = Convert.ToInt32(reader["silk_own"]),
                        SilkGift = Convert.ToInt32(reader["silk_gift"]),
                        JID      = Convert.ToInt32(reader["JID"]),
                        CharID   = Convert.ToUInt32(reader["CharID"])
                    });
                }

                return (true, list, "");
            }
            catch (Exception ex)
            {
                string msg = $"GetCharactersByJID failed for JID {jid}: {ex.Message}";
                Logger.Error(typeof(DBConnect), msg);
                return (false, list, msg);
            }
        }

        #endregion

    }
}
