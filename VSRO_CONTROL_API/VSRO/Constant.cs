using System.Data.Common;
using VSRO_CONTROL_API.VSRO.Enums;

namespace VSRO_CONTROL_API.VSRO
{
    public static class Constant
    {
        #region - SQL Queries -
        public const string GetIdsFromRefRegion = @"
            USE SRO_VT_SHARD
            SELECT wRegionID
            FROM _RefRegion";

        public const string GetRegionsWithContinents = @"
            USE SRO_VT_SHARD
            SELECT 
                wRegionID,
                ContinentName
            FROM _RefRegion
            GROUP BY wRegionID, ContinentName
            ORDER BY wRegionID";
        // --- Achievement Queries (SRO_VT_PROXY) ---
        public const string GetAchievementNames_q = @"
            USE SRO_VT_PROXY;
            SELECT AchievementName
            FROM AchievementProgress
            WHERE CharName = @CharName
            ORDER BY AchievementName;";

        public const string GetAchievementProgressByName_q = @"
            USE SRO_VT_PROXY;
            SELECT Progress, Completed, CompletedAt
            FROM AchievementProgress
            WHERE CharName = @CharName AND AchievementName = @AchievementName;";

        public const string InitAchievementTable_q = @"
            USE SRO_VT_PROXY;
            IF NOT EXISTS (
                SELECT * FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = 'AchievementProgress'
            )
            BEGIN
                CREATE TABLE AchievementProgress (
                    Id              INT IDENTITY(1,1) PRIMARY KEY,
                    CharName        NVARCHAR(64)  NOT NULL,
                    AchievementName NVARCHAR(128) NOT NULL,
                    Progress        BIGINT        NOT NULL DEFAULT 0,
                    Completed       BIT           NOT NULL DEFAULT 0,
                    CompletedAt     DATETIME      NULL,
                    CONSTRAINT UQ_Char_Achievement UNIQUE (CharName, AchievementName)
                );
                CREATE INDEX IX_AchProgress_CharName ON AchievementProgress (CharName);
            END;";

        public const string GetAchievementProgress_q = @"
            USE SRO_VT_PROXY;
            SELECT Progress, Completed
            FROM AchievementProgress
            WHERE CharName = @CharName AND AchievementName = @AchievementName;";

        public const string GetAllAchievementsForChar_q = @"
            USE SRO_VT_PROXY;
            SELECT AchievementName, Progress, Completed, CompletedAt
            FROM AchievementProgress
            WHERE CharName = @CharName;";

        // MERGE = upsert in MSSQL. Increments progress and auto-completes if threshold hit.
        public const string IncrementAchievementProgress_q = @"
            USE SRO_VT_PROXY;
            MERGE AchievementProgress AS target
            USING (SELECT @CharName AS CharName, @AchievementName AS AchievementName) AS source
            ON target.CharName = source.CharName AND target.AchievementName = source.AchievementName
            WHEN MATCHED AND target.Completed = 0 THEN
                UPDATE SET
                    target.Progress = target.Progress + @Amount,
                    target.Completed = CASE WHEN (target.Progress + @Amount) >= @Goal THEN 1 ELSE 0 END,
                    target.CompletedAt = CASE WHEN (target.Progress + @Amount) >= @Goal THEN GETDATE() ELSE NULL END
            WHEN NOT MATCHED THEN
                INSERT (CharName, AchievementName, Progress, Completed, CompletedAt)
                VALUES (
                    @CharName,
                    @AchievementName,
                    @Amount,
                    CASE WHEN @Amount >= @Goal THEN 1 ELSE 0 END,
                    CASE WHEN @Amount >= @Goal THEN GETDATE() ELSE NULL END
                );";


        public const string GetItemTypeId_q = @"
            USE SRO_VT_SHARD
            SELECT 
                common.CodeName128, 
                common.TypeID1, common.TypeID2, common.TypeID3, common.TypeID4,
                item.MaxStack -- This is what you need for the 4th tuple slot
            FROM _RefObjCommon AS common
            INNER JOIN _RefObjItem AS item ON common.Link = item.ID
            WHERE common.ID = @ItemID;";

        public const string GetItemCodeNameByRefId_q = @"
            USE SRO_VT_SHARD
            SELECT CodeName128
            FROM _RefObjCommon
            WHERE ID = @ItemID;";

        public const string CreateCharacterPlaytimeTable_q = @"
            IF NOT EXISTS (
                SELECT 1 
                FROM SRO_VT_PROXY.sys.tables t
                INNER JOIN SRO_VT_PROXY.sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.name = '_CharacterPlaytime' AND s.name = 'dbo'
            )
            BEGIN
                CREATE TABLE SRO_VT_PROXY.dbo._CharacterPlaytime
                (
                    CharacterName NVARCHAR(64) NOT NULL PRIMARY KEY,
                    TotalPlaySeconds BIGINT NOT NULL DEFAULT 0,
                    LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                )
            END
            ";
        public const string DoesUsernameExist_q = "SELECT COUNT(*) FROM TB_User WHERE StrUserID = @UserID;";
        public const string GetCharacterPosByName_q = @"
                USE SRO_VT_SHARD
                SELECT LatestRegion, PosX, PosY, PosZ 
                FROM _Char 
                WHERE CharName16 = @CharName";
        public const string GetOnlineUsers_q = "SELECT COUNT(*) FROM SRO_VT_LOG.dbo._IPLogs";

        public const string GetCharactersByJID_q = @"
            USE SRO_VT_SHARD
            SELECT
                t.StrUserID  AS Login,
                c.CharName16 AS CharName,
                s.silk_own,
                s.silk_gift,
                t.JID,
                c.CharID
            FROM SRO_VT_ACCOUNT..TB_User t
            INNER JOIN SRO_VT_SHARD.._User u  ON u.UserJID = t.JID
            INNER JOIN SRO_VT_SHARD.._Char c  ON c.CharID  = u.CharID
            INNER JOIN SRO_VT_ACCOUNT..SK_Silk s ON t.JID  = s.JID
            WHERE t.JID = @JID
            ORDER BY c.CharID ASC";

        public const string GetItemIconPaths_q = @"
            USE SRO_VT_SHARD
            SELECT CodeName128, AssocFileIcon128
            FROM _RefObjCommon
            WHERE CodeName128 IN ({0})";
        public const string GetMonsterCodeName_q = @"
            USE SRO_VT_SHARD
            SELECT CodeName128 
            FROM _RefObjCommon 
            WHERE ID = @RefObjID";
        public const string GetItemDurability_q = @"USE SRO_VT_SHARD;
                 SELECT
                     c.CodeName128,
                     i.Dur_L,
                     c.ID,
                     i.MaxStack
                 FROM
                     _RefObjCommon AS c
                 JOIN
                     _RefObjItem AS i ON c.Link = i.ID
                 WHERE
                     c.CodeName128 LIKE @ITEMCODE;";
        public const string GetCharIdByName_q = "USE SRO_VT_SHARD\r\nSELECT CharID FROM _Char WHERE CharName16 = @CharName AND Deleted = 0";
        public const string GetInventoryByCharId_q = "USE SRO_VT_SHARD\r\nSELECT Slot, ItemID FROM _Inventory WHERE CharID = @CharID ORDER BY Slot";
        public const string GetInventorySize_q = "USE SRO_VT_SHARD\r\nSELECT InventorySize FROM _Char WHERE CharID = @CharID";
        public const string GetInventoryWithNames_q = @"
                USE SRO_VT_SHARD
                SELECT inv.Slot, inv.ItemID, obj.CodeName128, ri.MaxStack
                FROM _Inventory inv
                JOIN _Items itm ON itm.ID64 = inv.ItemID
                JOIN _RefObjCommon obj ON obj.ID = itm.RefItemID
                JOIN _RefObjItem ri ON ri.ID = obj.Link
                WHERE inv.CharID = @CharID AND inv.ItemID > 0 AND inv.Slot >= 13
                ORDER BY inv.Slot";
        public const string GetAllUsersInDB_q = @"
                SELECT JID, StrUserID, password, CAST(Status AS varchar) AS Status,
                       Name, Email, sex, sec_primary, AccPlayTime
                FROM SRO_VT_ACCOUNT.dbo.TB_User";
        public const string GetUserAccountByUsername_q = @"
                SELECT JID, StrUserID, password, CAST(Status AS varchar) AS Status,
                       Name, Email, sex, sec_primary, AccPlayTime
                FROM SRO_VT_ACCOUNT.dbo.TB_User
                WHERE StrUserID = @Username";
        public const string GetUserNameByJID_q = @"
                SELECT StrUserID
                FROM SRO_VT_ACCOUNT.dbo.TB_User
                WHERE JID = @JID";
        public const string GetSilkByUsername_q = @"
                SELECT s.silk_own
                FROM SRO_VT_ACCOUNT.dbo.TB_User u
                JOIN SRO_VT_ACCOUNT.dbo.SK_Silk s ON u.JID = s.JID
                WHERE u.StrUserID = @ACCOUNTUSER";
        public const string GetSilkByJID_q = @"
                SELECT silk_own
                FROM SRO_VT_ACCOUNT.dbo.SK_Silk
                WHERE JID = @JID";
        public const string GetAllPrivelagedIp_q = @"
                SELECT
                    nIdx, szIPBegin, szIPEnd, szGM, dIssueDate, szISP, szDesc
                FROM SRO_VT_ACCOUNT.dbo._PrivilegedIP";
        public const string GetRegionAssoc_q = @"
                SELECT
                    AreaName,
                    AssocServer
                FROM SRO_VT_SHARD.dbo._RefRegionBindAssocServer;";
        public const string GetAllNotices_q = "SELECT ID, ContentID, Subject, Article, EditDate FROM _Notice WHERE ContentID = @ContentID ORDER BY EditDate DESC";
        public const string GetPlayTime_q = @"
                SELECT TotalPlaySeconds 
                FROM SRO_VT_PROXY.dbo._CharacterPlaytime
                WHERE CharacterName = @CharName
                ";
        public const string AddPlayTime_q = @"
                UPDATE SRO_VT_PROXY.dbo._CharacterPlaytime
                SET 
                    TotalPlaySeconds = TotalPlaySeconds + @Seconds,
                    LastUpdated = GETUTCDATE()
                WHERE CharacterName = @CharName

                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO SRO_VT_PROXY.dbo._CharacterPlaytime (CharacterName, TotalPlaySeconds, LastUpdated)
                    VALUES (@CharName, @Seconds, GETUTCDATE())
                END
                ";
        public const string AddNotice_q = "INSERT INTO _Notice (ContentID, Subject, Article, EditDate) VALUES (@ContentID, @Subject, @Article, GETDATE())";
        public const string DeleteNotice_q = "DELETE FROM _Notice WHERE ID = @ID";
        public const string ChangeUserPassword_q = @"
                UPDATE SRO_VT_ACCOUNT.dbo.TB_User
                SET password = @NewPassword
                WHERE StrUserID = @Username";
        public const string UpdateUserAccount_q = @"
                UPDATE SRO_VT_ACCOUNT.dbo.TB_User
                SET
                    Name = @Name,
                    Email = @Email,
                    sex = @Sex,
                    sec_primary = @Authority,
                    sec_content = @Authority
                WHERE JID = @JID";
        public const string AddNewUser_q = "INSERT INTO TB_User (StrUserID, password, sec_primary, sec_content) VALUES (@UserID, @PasswordHash, @sec_Primary, @sec_content);";
        public const string AddNewNPCToCharacterLocation_q = "\r\nUse SRO_VT_SHARD \r\n\r\nDeclare \r\n@NpcCodeName varchar(60),\t@ID\t  INT,\t\t\t@CodeName128 varchar(128),\t\t@NameStrID128 varchar(128), \r\n@Link INT,\t\t\t\t\t@ShopID int,\t\t@CountGroups smallint,\r\n@ShopGroupID1 int,\t\t\t@ShopGroupID2 int,\t@ShopGroupID3 int,\t\t\t\t@ShopGroupID4 int,\r\n@ShopItemGroupID1 int,\t\t@ShopItemGroupID2 int,@ShopItemGroupID3 int,\t\t@ShopItemGroupID4 int,\r\n@CountTabGroup1 smallint,\t@CountTabGroup2 smallint,@CountTabGroup3 smallint,\t@CountTabGroup4 smallint,\r\n@ShopTab1Group1 int,\t\t@ShopTab2Group1 int,\t@ShopTab3Group1 int,\t\t@ShopTab4Group1 int,\r\n@ShopTab1Group2 int,\t\t@ShopTab2Group2 int,\t@ShopTab3Group2 int,\t\t@ShopTab4Group2 int,\r\n@ShopTab1Group3 int,\t\t@ShopTab2Group3 int,\t@ShopTab3Group3 int,\t\t@ShopTab4Group3 int,\r\n@ShopTab1Group4 int,\t\t@ShopTab2Group4 int,\t@ShopTab3Group4 int,\t\t@ShopTab4Group4 int,\r\n@ShoptabGroupID1 int,\t\t@ShoptabGroupID2 int,\t@ShoptabGroupID3 int,\t\t@ShoptabGroupID4 int,\r\n@HiveID int,\t\t\t\t@TacticsID int,\t\t\t@NestID int,\t\t\t\t@CharnameLocation varchar(64),\r\n@WorldID smallint,\t\t\t@RegionID smallint,\t\t@PosX real,\t\t\t\t\t@PosY real,\t\t\t@PosZ real,\r\n@NpcLooking smallint\r\n----------------------------Setting---------------------------------\r\nSet @NpcCodeName = @CODENAME15253 --<--Please Type Name Capital\r\nSet @CharnameLocation = @CHARACTERNAMELOCATION --<-- Charname Location\r\nSet @NpcLooking = @LOOKDIRECTION  --<-- West = -32767 South = -16384 Earth = 0 North = +16,384 --\r\nSet @CountGroups = @GROUPCOUNT ---<----Count of Groups  Max {4 Groups}\r\nSet @CountTabGroup1 = @TABGROUPS ----<----Count Tabs in Group {1} Max {4 tabs}\r\nSet @CountTabGroup2 = @TABGROUPS2 ----<----Count Tabs in Group {2} If Exists  Max {4 tabs}\r\nSet @CountTabGroup3 = @TABGROUPS3 ----<----Count Tabs in Group {3} If Exists Max {4 tabs}\r\nSet @CountTabGroup4 = @TABGROUPS4 ----<----Count Tabs in Group {4} If Exists Max {4 tabs}\r\n----------------------------Setting End\r\n\r\n-------------------------------------Dont Change Any Thing more\r\nSet @ID = (Select Max(ID)+1 From SRO_VT_SHARD.dbo._RefObjCommon)\r\nSet @CodeName128 = 'NPC_'+@NpcCodeName+''\r\nSet @NameStrID128 = 'SN_NPC_'+@NpcCodeName+''\r\nSet @Link = (Select Max(ID)+1 From SRO_VT_SHARD.dbo._RefObjChar)\r\nSet @ShopID = (Select Max(ID)+1 From SRO_VT_SHARD.dbo._RefShop)\r\nSet @ShopGroupID1 = (Select Max(ID)+1 From SRO_VT_SHARD.dbo._RefShopGroup)\r\nSet @ShopGroupID2 = (Select Max(ID)+2 From SRO_VT_SHARD.dbo._RefShopGroup)\r\nSet @ShopGroupID3 = (Select Max(ID)+3 From SRO_VT_SHARD.dbo._RefShopGroup)\r\nSet @ShopGroupID4 = (Select Max(ID)+4 From SRO_VT_SHARD.dbo._RefShopGroup)\r\nSet @ShopItemGroupID1 = (Select Max(GroupID)+1 From SRO_VT_SHARD.dbo._RefShopItemGroup)\r\nSet @ShopItemGroupID2 = (Select Max(GroupID)+2 From SRO_VT_SHARD.dbo._RefShopItemGroup)\r\nSet @ShopItemGroupID3 = (Select Max(GroupID)+3 From SRO_VT_SHARD.dbo._RefShopItemGroup)\r\nSet @ShopItemGroupID4 = (Select Max(GroupID)+4 From SRO_VT_SHARD.dbo._RefShopItemGroup)\r\nSet @ShopTab1Group1\t=(Select Max(ID)+1 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab2Group1\t=(Select Max(ID)+2 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab3Group1\t=(Select Max(ID)+3 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab4Group1\t=(Select Max(ID)+4 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab1Group2\t=(Select Max(ID)+5 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab2Group2\t=(Select Max(ID)+6 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab3Group2\t=(Select Max(ID)+7 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab4Group2\t=(Select Max(ID)+8 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab1Group3\t=(Select Max(ID)+9 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab2Group3\t=(Select Max(ID)+10 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab3Group3\t=(Select Max(ID)+11 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab4Group3\t=(Select Max(ID)+12 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab1Group4\t=(Select Max(ID)+13 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab2Group4\t=(Select Max(ID)+14 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab3Group4\t=(Select Max(ID)+15 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShopTab4Group4\t=(Select Max(ID)+16 From SRO_VT_SHARD.dbo._RefShopTab)\r\nSet @ShoptabGroupID1 = (Select Max(ID)+1 From SRO_VT_SHARD.dbo._RefShopTabGroup)\r\nSet @ShoptabGroupID2 = (Select Max(ID)+2 From SRO_VT_SHARD.dbo._RefShopTabGroup)\r\nSet @ShoptabGroupID3 = (Select Max(ID)+3 From SRO_VT_SHARD.dbo._RefShopTabGroup)\r\nSet @ShoptabGroupID4 = (Select Max(ID)+4 From SRO_VT_SHARD.dbo._RefShopTabGroup)\r\nSet @HiveID = (Select Max(dwHiveID)+1 From SRO_VT_SHARD.dbo.Tab_RefHive)\r\nSet @TacticsID = (Select Max(dwTacticsID)+1 From SRO_VT_SHARD.dbo.Tab_RefTactics)\r\nSet @NestID = (Select Max(dwNestID)+1 From SRO_VT_SHARD.dbo.Tab_RefNest)\r\nSet @WorldID = (Select WorldID From SRO_VT_SHARD.dbo._Char Where CharName16 = @CharnameLocation)\r\nSet @RegionID = (Select LatestRegion From SRO_VT_SHARD.dbo._Char Where CharName16 = @CharnameLocation)\r\nSet @PosX = (Select PosX From SRO_VT_SHARD.dbo._Char Where CharName16 = @CharnameLocation)\r\nSet @PosY = (Select PosY From SRO_VT_SHARD.dbo._Char Where CharName16 = @CharnameLocation)\r\nSet @PosZ = (Select PosZ From SRO_VT_SHARD.dbo._Char Where CharName16 = @CharnameLocation)\r\n---------------------------------------------------------------------------------------------\r\nIF Not EXISTS (Select * From _RefObjCommon Where CodeName128 = @CodeName128)\r\nBEGIN\r\n----------------------------Print side to media-------------------------------------------\r\nPrint '\r\nMedia Side\r\ncharacterdata_xxx\r\n1\t' + CAST(@ID AS NVARCHAR) + '\t'+@CodeName128+'\txxx\txxx\t'+@NameStrID128+'\txxx\t0\t1\t1\t2\t2\t0\t5000\t3\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t-1\t0\t-1\t0\t-1\t0\t-1\t0\t-1\t0\t0\t0\t0\t0\t0\t0\t100\t0\t0\t0\tnpc\\npc\\khotansystem_turtleshipticketagent.bsr\txxx\txxx\txxx\txxx\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t336860180\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\r\n----------------\r\nRefShop\r\n1\t15\t' + CAST(@ShopID AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'\t-1\txxx\t-1\txxx\t-1\txxx\t-1\txxx\r\n--------------------'\r\nIF @CountGroups >= 1\r\nBEGIN\r\nPrint'\r\nRefShopGroup\r\n1\t15\t' + CAST(@ShopGroupID1 AS NVARCHAR) + '\tGROUP1_STORE_'+@NpcCodeName+'\t'+@CodeName128+'\t-1\txxx\t-1\txxx\t-1\txxx\t-1\txxx'\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopGroupID2 AS NVARCHAR) + '\tGROUP2_STORE_'+@NpcCodeName+'\t'+@CodeName128+'\t-1\txxx\t-1\txxx\t-1\txxx\t-1\txxx'\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopGroupID3 AS NVARCHAR) + '\tGROUP3_STORE_'+@NpcCodeName+'\t'+@CodeName128+'\t-1\txxx\t-1\txxx\t-1\txxx\t-1\txxx'\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopGroupID4 AS NVARCHAR) + '\tGROUP4_STORE_'+@NpcCodeName+'\t'+@CodeName128+'\t-1\txxx\t-1\txxx\t-1\txxx\t-1\txxx'\r\nEND\r\nIF @CountGroups >= 1\r\nBEGIN\r\nPrint'----------------------------\r\nShopGroupData\r\n1\t' + CAST(@ShopItemGroupID1 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP1\tSN_STORE_'+@NpcCodeName+'_GROUP1'\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nPrint'1\t' + CAST(@ShopItemGroupID2 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP2\tSN_STORE_'+@NpcCodeName+'_GROUP2'\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nPrint'1\t' + CAST(@ShopItemGroupID3 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP3\tSN_STORE_'+@NpcCodeName+'_GROUP3'\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nPrint'1\t' + CAST(@ShopItemGroupID4 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP4\tSN_STORE_'+@NpcCodeName+'_GROUP4'\r\nEND\r\nIF @CountGroups >= 1\r\nBEGIN\r\nPrint'-------------------\r\nrefshoptabgroup\r\n1\t15\t' + CAST(@ShoptabGroupID1 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP1\tSN_STORE_'+@NpcCodeName+'_GROUP1'\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShoptabGroupID2 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP2\tSN_STORE_'+@NpcCodeName+'_GROUP2'\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShoptabGroupID3 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP3\tSN_STORE_'+@NpcCodeName+'_GROUP3'\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShoptabGroupID4 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_GROUP4\tSN_STORE_'+@NpcCodeName+'_GROUP4'\r\nEND\r\nIF @CountGroups >= 1\r\nBEGIN\r\nPrint'-------------------------\r\nrefmappingshopgroup\r\n1\t15\tGROUP1_STORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+''\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nPrint'1\t15\tGROUP2_STORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+''\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nPrint'1\t15\tGROUP3_STORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+''\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nPrint'1\t15\tGROUP4_STORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+''\r\nEND\r\nIF @CountGroups >= 1\r\nBEGIN\r\nPrint'--------------------------\r\nrefmappingshopwithtab\r\n1\t15\tSTORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+'_GROUP1'\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nPrint'1\t15\tSTORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+'_GROUP2'\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nPrint'1\t15\tSTORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+'_GROUP3'\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nPrint'1\t15\tSTORE_'+@NpcCodeName+'\tSTORE_'+@NpcCodeName+'_GROUP4'\r\nEND\r\nIF @CountGroups >= 1\r\nBEGIN\r\nIF @CountTabGroup1 >=1\r\nBEGIN\r\nPrint'---------------------------\r\nrefshoptab\r\n1\t15\t' + CAST(@ShopTab1Group1 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_TAB1\tSTORE_'+@NpcCodeName+'_GROUP1\tSN_STORE_'+@NpcCodeName+'_TAB1'\r\nEND\r\nIF @CountTabGroup1 >=2\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab2Group1 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_TAB2\tSTORE_'+@NpcCodeName+'_GROUP1\tSN_STORE_'+@NpcCodeName+'_TAB2'\r\nEND\r\nIF @CountTabGroup1 >=3\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab3Group1 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_TAB3\tSTORE_'+@NpcCodeName+'_GROUP1\tSN_STORE_'+@NpcCodeName+'_TAB3'\r\nEND\r\nIF @CountTabGroup1 >=4\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab4Group1 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_TAB4\tSTORE_'+@NpcCodeName+'_GROUP1\tSN_STORE_'+@NpcCodeName+'_TAB4'\r\nEND\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nIF @CountTabGroup2 >=1\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab1Group2 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G2_TAB1\tSTORE_'+@NpcCodeName+'_GROUP2\tSN_STORE_'+@NpcCodeName+'_G2_TAB1'\r\nEND\r\nIF @CountTabGroup2 >=2\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab2Group2 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G2_TAB2\tSTORE_'+@NpcCodeName+'_GROUP2\tSN_STORE_'+@NpcCodeName+'_G2_TAB2'\r\nEND\r\nIF @CountTabGroup2 >=3\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab3Group2 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G2_TAB3\tSTORE_'+@NpcCodeName+'_GROUP2\tSN_STORE_'+@NpcCodeName+'_G2_TAB3'\r\nEND\r\nIF @CountTabGroup2 >=4\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab4Group2 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G2_TAB4\tSTORE_'+@NpcCodeName+'_GROUP2\tSN_STORE_'+@NpcCodeName+'_G2_TAB4'\r\nEND\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nIF @CountTabGroup3 >=1\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab1Group3 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G3_TAB1\tSTORE_'+@NpcCodeName+'_GROUP3\tSN_STORE_'+@NpcCodeName+'_G3_TAB1'\r\nEND\r\nIF @CountTabGroup3 >=2\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab2Group3 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G3_TAB2\tSTORE_'+@NpcCodeName+'_GROUP3\tSN_STORE_'+@NpcCodeName+'_G3_TAB2'\r\nEND\r\nIF @CountTabGroup3 >=3\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab3Group3 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G3_TAB3\tSTORE_'+@NpcCodeName+'_GROUP3\tSN_STORE_'+@NpcCodeName+'_G3_TAB3'\r\nEND\r\nIF @CountTabGroup3 >=4\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab4Group3 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G3_TAB4\tSTORE_'+@NpcCodeName+'_GROUP3\tSN_STORE_'+@NpcCodeName+'_G3_TAB4'\r\nEND\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nIF @CountTabGroup4 >=1\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab1Group4 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G4_TAB1\tSTORE_'+@NpcCodeName+'_GROUP4\tSN_STORE_'+@NpcCodeName+'_G4_TAB1'\r\nEND\r\nIF @CountTabGroup4 >=2\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab2Group4 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G4_TAB2\tSTORE_'+@NpcCodeName+'_GROUP4\tSN_STORE_'+@NpcCodeName+'_G4_TAB2'\r\nEND\r\nIF @CountTabGroup4 >=3\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab3Group4 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G4_TAB3\tSTORE_'+@NpcCodeName+'_GROUP4\tSN_STORE_'+@NpcCodeName+'_G4_TAB3'\r\nEND\r\nIF @CountTabGroup4 >=4\r\nBEGIN\r\nPrint'1\t15\t' + CAST(@ShopTab4Group4 AS NVARCHAR) + '\tSTORE_'+@NpcCodeName+'_G4_TAB4\tSTORE_'+@NpcCodeName+'_GROUP4\tSN_STORE_'+@NpcCodeName+'_G4_TAB4'\r\nEND\r\nEND\r\nPrint '-------------------------------\r\ntextdata_object\r\n1\t'+@NameStrID128+'\t\t\t\t\t\t\t\tNPC Name\r\n-----------\r\ntextquest_speech&name\r\n1\t'+@NameStrID128+'_BS\t??? ?? ?? ??? ??? ?????…\t0\t0\t0\t0\t0\tBest Challenge that make game More fun Now Want see who the Best .. (MyvsroTeam)…\tBest Challenge that make game More fun Now Want see who the Best .. (MyvsroTeam)…\t0\t0\t0\t0\t0\t0\r\n--------------\r\nnpcchat\r\n1\t'+@CodeName128+'\t'+@NameStrID128+'_BS\t'+@NameStrID128+'_PS\r\n------------------\r\ntextuisystem\r\n'\r\nIF @CountGroups >= 1\r\nBEGIN\r\nPrint'1\tSN_STORE_'+@NpcCodeName+'_GROUP1\t상인 여자용 직업복 구입/판매\t0\t0\t0\t0\t0\t0\tGroup1Name  \t0\t0\t0\t0\t0\t0\t0\t0'\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nPrint'1\tSN_STORE_'+@NpcCodeName+'_GROUP2\t상인 여자용 직업복 구입/판매\t0\t0\t0\t0\t0\t0\tGroup2Name  \t0\t0\t0\t0\t0\t0\t0\t0'\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nPrint'1\tSN_STORE_'+@NpcCodeName+'_GROUP3\t상인 여자용 직업복 구입/판매\t0\t0\t0\t0\t0\t0\tGroup3Name  \t0\t0\t0\t0\t0\t0\t0\t0'\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nPrint'1\tSN_STORE_'+@NpcCodeName+'_GROUP4\t상인 여자용 직업복 구입/판매\t0\t0\t0\t0\t0\t0\tGroup4Name  \t0\t0\t0\t0\t0\t0\t0\t0'\r\nEND\r\nIF @CountGroups >= 1\r\nBEGIN\r\nIF @CountTabGroup1 >=1\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_TAB1\t무기\t0\t0\t0\t0\t0\t0\tTab1Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup1 >=2\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_TAB2\t무기\t0\t0\t0\t0\t0\t0\tTab2Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup1 >=3\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_TAB3\t무기\t0\t0\t0\t0\t0\t0\tTab3Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup1 >=4\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_TAB4\t무기\t0\t0\t0\t0\t0\t0\tTab4Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nIF @CountTabGroup2 >=1\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G2_TAB1\t무기\t0\t0\t0\t0\t0\t0\tTab1G2Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup2 >=2\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G2_TAB2\t무기\t0\t0\t0\t0\t0\t0\tTab2G2Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup2 >=3\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G2_TAB3\t무기\t0\t0\t0\t0\t0\t0\tTab3G2Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup2 >=4\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G2_TAB4\t무기\t0\t0\t0\t0\t0\t0\tTab4G2Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nIF @CountTabGroup3 >=1\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G3_TAB1\t무기\t0\t0\t0\t0\t0\t0\tTab1G3Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup3 >=2\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G3_TAB2\t무기\t0\t0\t0\t0\t0\t0\tTab2G3Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup3 >=3\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G3_TAB3\t무기\t0\t0\t0\t0\t0\t0\tTab3G3Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup3 >=4\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G3_TAB4\t무기\t0\t0\t0\t0\t0\t0\tTab4G3Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nIF @CountTabGroup4 >=1\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G4_TAB1\t무기\t0\t0\t0\t0\t0\t0\tTab1G4Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup4 >=2\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G4_TAB2\t무기\t0\t0\t0\t0\t0\t0\tTab2G4Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup4 >=3\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G4_TAB3\t무기\t0\t0\t0\t0\t0\t0\tTab3G4Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nIF @CountTabGroup4 >=4\r\nBEGIN\r\nPrint '1\tSN_STORE_'+@NpcCodeName+'_G4_TAB4\t무기\t0\t0\t0\t0\t0\t0\tTab4G4Name\t0\t0\t0\t0\t0\t0\t0\t0\t'\r\nEND\r\nEND\r\n---------------------------------end Print----------------------------------------------------\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefObjCommon'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefObjCommon] ON \r\nINSERT [dbo].[_RefObjCommon] ([Service], [ID], [CodeName128], [ObjName128], [OrgObjCodeName128], [NameStrID128], [DescStrID128], [CashItem], [Bionic], [TypeID1], [TypeID2], [TypeID3], [TypeID4], [DecayTime], [Country], [Rarity], [CanTrade], [CanSell], [CanBuy], [CanBorrow], [CanDrop], [CanPick], [CanRepair], [CanRevive], [CanUse], [CanThrow], [Price], [CostRepair], [CostRevive], [CostBorrow], [KeepingFee], [SellPrice], [ReqLevelType1], [ReqLevel1], [ReqLevelType2], [ReqLevel2], [ReqLevelType3], [ReqLevel3], [ReqLevelType4], [ReqLevel4], [MaxContain], [RegionID], [Dir], [OffsetX], [OffsetY], [OffsetZ], [Speed1], [Speed2], [Scale], [BCHeight], [BCRadius], [EventID], [AssocFileObj128], [AssocFileDrop128], [AssocFileIcon128], [AssocFile1_128], [AssocFile2_128], [Link]) VALUES (1, @ID, @CodeName128,'xxx','xxx',@NameStrID128,'xxx', 0, 1, 1, 2, 2, 0, 5000, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0, 0, 0, 0, 0, 0, 0, 100, 0, 0, 0,'npc\\npc\\khotansystem_turtleshipticketagent.bsr','xxx','xxx','xxx','xxx',@Link)\r\nSET IDENTITY_INSERT [dbo].[_RefObjCommon] OFF \r\nEND ELSE BEGIN\r\nINSERT [dbo].[_RefObjCommon] ([Service], [ID], [CodeName128], [ObjName128], [OrgObjCodeName128], [NameStrID128], [DescStrID128], [CashItem], [Bionic], [TypeID1], [TypeID2], [TypeID3], [TypeID4], [DecayTime], [Country], [Rarity], [CanTrade], [CanSell], [CanBuy], [CanBorrow], [CanDrop], [CanPick], [CanRepair], [CanRevive], [CanUse], [CanThrow], [Price], [CostRepair], [CostRevive], [CostBorrow], [KeepingFee], [SellPrice], [ReqLevelType1], [ReqLevel1], [ReqLevelType2], [ReqLevel2], [ReqLevelType3], [ReqLevel3], [ReqLevelType4], [ReqLevel4], [MaxContain], [RegionID], [Dir], [OffsetX], [OffsetY], [OffsetZ], [Speed1], [Speed2], [Scale], [BCHeight], [BCRadius], [EventID], [AssocFileObj128], [AssocFileDrop128], [AssocFileIcon128], [AssocFile1_128], [AssocFile2_128], [Link]) VALUES (1, @ID, @CodeName128,'xxx','xxx',@NameStrID128,'xxx', 0, 1, 1, 2, 2, 0, 5000, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0, 0, 0, 0, 0, 0, 0, 100, 0, 0, 0,'npc\\npc\\khotansystem_turtleshipticketagent.bsr','xxx','xxx','xxx','xxx',@Link)\r\nEND\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefObjChar'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefObjChar] ON \r\nINSERT [dbo].[_RefObjChar] ([ID], [Lvl], [CharGender], [MaxHP], [MaxMP], [ResistFrozen], [ResistFrostbite], [ResistBurn], [ResistEShock], [ResistPoison], [ResistZombie], [ResistSleep], [ResistRoot], [ResistSlow], [ResistFear], [ResistMyopia], [ResistBlood], [ResistStone], [ResistDark], [ResistStun], [ResistDisea], [ResistChaos], [ResistCsePD], [ResistCseMD], [ResistCseSTR], [ResistCseINT], [ResistCseHP], [ResistCseMP], [Resist24], [ResistBomb], [Resist26], [Resist27], [Resist28], [Resist29], [Resist30], [Resist31], [Resist32], [InventorySize], [CanStore_TID1], [CanStore_TID2], [CanStore_TID3], [CanStore_TID4], [CanBeVehicle], [CanControl], [DamagePortion], [MaxPassenger], [AssocTactics], [PD], [MD], [PAR], [MAR], [ER], [BR], [HR], [CHR], [ExpToGive], [CreepType], [Knockdown], [KO_RecoverTime], [DefaultSkill_1], [DefaultSkill_2], [DefaultSkill_3], [DefaultSkill_4], [DefaultSkill_5], [DefaultSkill_6], [DefaultSkill_7], [DefaultSkill_8], [DefaultSkill_9], [DefaultSkill_10], [TextureType], [Except_1], [Except_2], [Except_3], [Except_4], [Except_5], [Except_6], [Except_7], [Except_8], [Except_9], [Except_10], [Link]) VALUES (@Link, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 336860180, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)\r\nSET IDENTITY_INSERT [dbo].[_RefObjChar] OFF \r\nEND ELSE BEGIN\r\nINSERT [dbo].[_RefObjChar] ([ID], [Lvl], [CharGender], [MaxHP], [MaxMP], [ResistFrozen], [ResistFrostbite], [ResistBurn], [ResistEShock], [ResistPoison], [ResistZombie], [ResistSleep], [ResistRoot], [ResistSlow], [ResistFear], [ResistMyopia], [ResistBlood], [ResistStone], [ResistDark], [ResistStun], [ResistDisea], [ResistChaos], [ResistCsePD], [ResistCseMD], [ResistCseSTR], [ResistCseINT], [ResistCseHP], [ResistCseMP], [Resist24], [ResistBomb], [Resist26], [Resist27], [Resist28], [Resist29], [Resist30], [Resist31], [Resist32], [InventorySize], [CanStore_TID1], [CanStore_TID2], [CanStore_TID3], [CanStore_TID4], [CanBeVehicle], [CanControl], [DamagePortion], [MaxPassenger], [AssocTactics], [PD], [MD], [PAR], [MAR], [ER], [BR], [HR], [CHR], [ExpToGive], [CreepType], [Knockdown], [KO_RecoverTime], [DefaultSkill_1], [DefaultSkill_2], [DefaultSkill_3], [DefaultSkill_4], [DefaultSkill_5], [DefaultSkill_6], [DefaultSkill_7], [DefaultSkill_8], [DefaultSkill_9], [DefaultSkill_10], [TextureType], [Except_1], [Except_2], [Except_3], [Except_4], [Except_5], [Except_6], [Except_7], [Except_8], [Except_9], [Except_10], [Link]) VALUES (@Link, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 336860180, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)\r\nEND\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo.Tab_RefTactics'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[Tab_RefTactics] ON \r\nINSERT [dbo].[Tab_RefTactics] ([dwTacticsID], [dwObjID], [btAIQoS], [nMaxStamina], [btMaxStaminaVariance], [nSightRange], [btAggressType], [AggressData], [btChangeTarget], [btHelpRequestTo], [btHelpResponseTo], [btBattleStyle], [BattleStyleData], [btDiversionBasis], [DiversionBasisData1], [DiversionBasisData2], [DiversionBasisData3], [DiversionBasisData4], [DiversionBasisData5], [DiversionBasisData6], [DiversionBasisData7], [DiversionBasisData8], [btDiversionKeepBasis], [DiversionKeepBasisData1], [DiversionKeepBasisData2], [DiversionKeepBasisData3], [DiversionKeepBasisData4], [DiversionKeepBasisData5], [DiversionKeepBasisData6], [DiversionKeepBasisData7], [DiversionKeepBasisData8], [btKeepDistance], [KeepDistanceData], [btTraceType], [btTraceBoundary], [TraceData], [btHomingType], [HomingData], [btAggressTypeOnHoming], [btFleeType], [dwChampionTacticsID], [AdditionOptionFlag], [szDescString128]) VALUES (@TacticsID, @ID, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,@CodeName128)\r\nSET IDENTITY_INSERT [dbo].[Tab_RefTactics] OFF\r\nEND ELSE BEGIN\r\nINSERT [dbo].[Tab_RefTactics] ([dwTacticsID], [dwObjID], [btAIQoS], [nMaxStamina], [btMaxStaminaVariance], [nSightRange], [btAggressType], [AggressData], [btChangeTarget], [btHelpRequestTo], [btHelpResponseTo], [btBattleStyle], [BattleStyleData], [btDiversionBasis], [DiversionBasisData1], [DiversionBasisData2], [DiversionBasisData3], [DiversionBasisData4], [DiversionBasisData5], [DiversionBasisData6], [DiversionBasisData7], [DiversionBasisData8], [btDiversionKeepBasis], [DiversionKeepBasisData1], [DiversionKeepBasisData2], [DiversionKeepBasisData3], [DiversionKeepBasisData4], [DiversionKeepBasisData5], [DiversionKeepBasisData6], [DiversionKeepBasisData7], [DiversionKeepBasisData8], [btKeepDistance], [KeepDistanceData], [btTraceType], [btTraceBoundary], [TraceData], [btHomingType], [HomingData], [btAggressTypeOnHoming], [btFleeType], [dwChampionTacticsID], [AdditionOptionFlag], [szDescString128]) VALUES (@TacticsID, @ID, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,@CodeName128)\r\nEND\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo.Tab_RefHive'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[Tab_RefHive] ON \r\nINSERT [dbo].[Tab_RefHive] ([dwHiveID], [btKeepMonsterCountType], [dwOverwriteMaxTotalCount], [fMonsterCountPerPC], [dwSpawnSpeedIncreaseRate], [dwMaxIncreaseRate], [btFlag], [GameWorldID], [HatchObjType], [szDescString128]) VALUES (@HiveID, 0, 0, 0, 0, 0, 0,@WorldID, 2,@CodeName128)\r\nSET IDENTITY_INSERT [dbo].[Tab_RefHive] OFF\r\nEND ELSE BEGIN\r\nINSERT [dbo].[Tab_RefHive] ([dwHiveID], [btKeepMonsterCountType], [dwOverwriteMaxTotalCount], [fMonsterCountPerPC], [dwSpawnSpeedIncreaseRate], [dwMaxIncreaseRate], [btFlag], [GameWorldID], [HatchObjType], [szDescString128]) VALUES (@HiveID, 0, 0, 0, 0, 0, 0,@WorldID, 2,@CodeName128)\r\nEND\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo.Tab_RefNest'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[Tab_RefNest] ON \r\nINSERT [dbo].[Tab_RefNest] ([dwNestID], [dwHiveID], [dwTacticsID], [nRegionDBID], [fLocalPosX], [fLocalPosY], [fLocalPosZ], [wInitialDir], [nRadius], [nGenerateRadius], [nChampionGenPercentage], [dwDelayTimeMin], [dwDelayTimeMax], [dwMaxTotalCount], [btFlag], [btRespawn], [btType]) VALUES (@NestID, @HiveID, @TacticsID, @RegionID, @PosX, @PosY, @PosZ, @NPCLooking, 0, 0, 0, 0, 0, 1, 0, 1, 0)\r\nSET IDENTITY_INSERT [dbo].[Tab_RefNest] OFF\r\nEND ELSE BEGIN\r\nINSERT [dbo].[Tab_RefNest] ([dwNestID], [dwHiveID], [dwTacticsID], [nRegionDBID], [fLocalPosX], [fLocalPosY], [fLocalPosZ], [wInitialDir], [nRadius], [nGenerateRadius], [nChampionGenPercentage], [dwDelayTimeMin], [dwDelayTimeMax], [dwMaxTotalCount], [btFlag], [btRespawn], [btType]) VALUES (@NestID, @HiveID, @TacticsID, @RegionID, @PosX, @PosY, @PosZ, @NPCLooking, 0, 0, 0, 0, 0, 1, 0, 1, 0)\r\nEND\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefShop'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefShop] ON \r\nINSERT [dbo].[_RefShop] ([Service], [Country], [ID], [CodeName128], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15,@ShopID, 'STORE_'+@NpcCodeName+'', -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nSET IDENTITY_INSERT [dbo].[_RefShop] OFF \r\nEND ELSE BEGIN\r\nINSERT [dbo].[_RefShop] ([Service], [Country], [ID], [CodeName128], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15,@ShopID, 'STORE_'+@NpcCodeName+'', -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefShopGroup'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefShopGroup] ON \r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID1,'GROUP1_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID2,'GROUP2_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID3,'GROUP3_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID4,'GROUP4_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nSET IDENTITY_INSERT [dbo].[_RefShopGroup] OFF \r\nEND ELSE BEGIN\r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID1,'GROUP1_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID2,'GROUP2_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID3,'GROUP3_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefShopGroup] ([Service], [Country], [ID], [CodeName128], [RefNPCCodeName], [Param1], [Param1_Desc128], [Param2], [Param2_Desc128], [Param3], [Param3_Desc128], [Param4], [Param4_Desc128]) VALUES (1, 15, @ShopGroupID4,'GROUP4_STORE_'+@NpcCodeName+'',@CodeName128, -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx')\r\nEND\r\nEND\r\n-------------------------------------------------------\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefShopItemGroup'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefShopItemGroup] ON \r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID1,'STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_GROUP1')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID2,'STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_GROUP2')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID3,'STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_GROUP3')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID4,'STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_GROUP4')\r\nEND\r\nSET IDENTITY_INSERT [dbo].[_RefShopItemGroup] OFF \r\nEND ELSE BEGIN\r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID1,'STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_GROUP1')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID2,'STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_GROUP2')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID3,'STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_GROUP3')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefShopItemGroup] ([Service], [GroupID], [CodeName128], [StrID128_Group]) VALUES (1, @ShopItemGroupID4,'STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_GROUP4')\r\nEND\r\nEND\r\n------------------------------------------------------------------\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefShopTabGroup'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefShopTabGroup] ON \r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID1,'STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_GROUP1')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID2,'STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_GROUP2')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID3,'STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_GROUP3')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID4,'STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_GROUP4')\r\nEND\r\nSET IDENTITY_INSERT [dbo].[_RefShopTabGroup] OFF \r\nEND ELSE BEGIN\r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID1,'STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_GROUP1')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID2,'STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_GROUP2')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID3,'STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_GROUP3')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTabGroup] ([Service], [Country], [ID], [CodeName128], [StrID128_Group]) VALUES (1, 15, @ShoptabGroupID4,'STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_GROUP4')\r\nEND\r\nEND\r\n------------------------------------------------------------------\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefMappingShopGroup'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefMappingShopGroup] ON \r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP1_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP2_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP3_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP4_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nSET IDENTITY_INSERT [dbo].[_RefMappingShopGroup] OFF \r\nEND ELSE BEGIN\r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP1_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP2_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP3_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopGroup] ([Service], [Country], [RefShopGroupCodeName], [RefShopCodeName]) VALUES (1, 15, 'GROUP4_STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'')\r\nEND\r\nEND\r\n----------------------------------------------------------------------------------------------------------------------------\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefMappingShopWithTab'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefMappingShopWithTab] ON \r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP1')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP2')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP3')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP4')\r\nEND\r\nSET IDENTITY_INSERT [dbo].[_RefMappingShopWithTab] OFF \r\nEND ELSE BEGIN\r\nIF @CountGroups >= 1\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP1')\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP2')\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP3')\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nINSERT [dbo].[_RefMappingShopWithTab] ([Service], [Country], [RefShopCodeName], [RefTabGroupCodeName]) VALUES (1, 15,'STORE_'+@NpcCodeName+'','STORE_'+@NpcCodeName+'_GROUP4')\r\nEND\r\nEND\r\n----------------------------------------------------------------------------------------------------------------------------\r\nIF OBJECTPROPERTY(OBJECT_ID('dbo._RefShopTab'), 'TableHasIdentity') = 1\r\nBEGIN\r\nSET IDENTITY_INSERT [dbo].[_RefShopTab] ON \r\nIF @CountGroups >= 1\r\nBEGIN\r\nIF @CountTabGroup1 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group1,'STORE_'+@NpcCodeName+'_TAB1','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB1')\r\nEND\r\nIF @CountTabGroup1 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group1,'STORE_'+@NpcCodeName+'_TAB2','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB2')\r\nEND\r\nIF @CountTabGroup1 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group1,'STORE_'+@NpcCodeName+'_TAB3','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB3')\r\nEND\r\nIF @CountTabGroup1 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group1,'STORE_'+@NpcCodeName+'_TAB4','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB4')\r\nEND\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nIF @CountTabGroup2 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group2,'STORE_'+@NpcCodeName+'_G2_TAB1','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB1')\r\nEND\r\nIF @CountTabGroup2 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group2,'STORE_'+@NpcCodeName+'_G2_TAB2','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB2')\r\nEND\r\nIF @CountTabGroup2 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group2,'STORE_'+@NpcCodeName+'_G2_TAB3','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB3')\r\nEND\r\nIF @CountTabGroup2 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group2,'STORE_'+@NpcCodeName+'_G2_TAB4','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB4')\r\nEND\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nIF @CountTabGroup3 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group3,'STORE_'+@NpcCodeName+'_G3_TAB1','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB1')\r\nEND\r\nIF @CountTabGroup3 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group3,'STORE_'+@NpcCodeName+'_G3_TAB2','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB2')\r\nEND\r\nIF @CountTabGroup3 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group3,'STORE_'+@NpcCodeName+'_G3_TAB3','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB3')\r\nEND\r\nIF @CountTabGroup3 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group3,'STORE_'+@NpcCodeName+'_G3_TAB4','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB4')\r\nEND\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nIF @CountTabGroup4 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group4,'STORE_'+@NpcCodeName+'_G4_TAB1','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB1')\r\nEND\r\nIF @CountTabGroup4 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group4,'STORE_'+@NpcCodeName+'_G4_TAB2','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB2')\r\nEND\r\nIF @CountTabGroup4 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group4,'STORE_'+@NpcCodeName+'_G4_TAB3','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB3')\r\nEND\r\nIF @CountTabGroup4 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group4,'STORE_'+@NpcCodeName+'_G4_TAB4','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB4')\r\nEND\r\nEND\r\nSET IDENTITY_INSERT [dbo].[_RefShopTab] OFF\r\nEND ELSE BEGIN\r\nIF @CountGroups >= 1\r\nBEGIN\r\nIF @CountTabGroup1 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group1,'STORE_'+@NpcCodeName+'_TAB1','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB1')\r\nEND\r\nIF @CountTabGroup1 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group1,'STORE_'+@NpcCodeName+'_TAB2','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB2')\r\nEND\r\nIF @CountTabGroup1 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group1,'STORE_'+@NpcCodeName+'_TAB3','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB3')\r\nEND\r\nIF @CountTabGroup1 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group1,'STORE_'+@NpcCodeName+'_TAB4','STORE_'+@NpcCodeName+'_GROUP1','SN_STORE_'+@NpcCodeName+'_TAB4')\r\nEND\r\nEND\r\nIF @CountGroups >= 2\r\nBEGIN\r\nIF @CountTabGroup2 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group2,'STORE_'+@NpcCodeName+'_G2_TAB1','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB1')\r\nEND\r\nIF @CountTabGroup2 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group2,'STORE_'+@NpcCodeName+'_G2_TAB2','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB2')\r\nEND\r\nIF @CountTabGroup2 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group2,'STORE_'+@NpcCodeName+'_G2_TAB3','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB3')\r\nEND\r\nIF @CountTabGroup2 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group2,'STORE_'+@NpcCodeName+'_G2_TAB4','STORE_'+@NpcCodeName+'_GROUP2','SN_STORE_'+@NpcCodeName+'_G2_TAB4')\r\nEND\r\nEND\r\nIF @CountGroups >= 3\r\nBEGIN\r\nIF @CountTabGroup3 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group3,'STORE_'+@NpcCodeName+'_G3_TAB1','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB1')\r\nEND\r\nIF @CountTabGroup3 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group3,'STORE_'+@NpcCodeName+'_G3_TAB2','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB2')\r\nEND\r\nIF @CountTabGroup3 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group3,'STORE_'+@NpcCodeName+'_G3_TAB3','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB3')\r\nEND\r\nIF @CountTabGroup3 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group3,'STORE_'+@NpcCodeName+'_G3_TAB4','STORE_'+@NpcCodeName+'_GROUP3','SN_STORE_'+@NpcCodeName+'_G3_TAB4')\r\nEND\r\nEND\r\nIF @CountGroups >= 4\r\nBEGIN\r\nIF @CountTabGroup4 >=1\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab1Group4,'STORE_'+@NpcCodeName+'_G4_TAB1','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB1')\r\nEND\r\nIF @CountTabGroup4 >=2\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab2Group4,'STORE_'+@NpcCodeName+'_G4_TAB2','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB2')\r\nEND\r\nIF @CountTabGroup4 >=3\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab3Group4,'STORE_'+@NpcCodeName+'_G4_TAB3','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB3')\r\nEND\r\nIF @CountTabGroup4 >=4\r\nBEGIN\r\nINSERT [dbo].[_RefShopTab] ([Service], [Country], [ID], [CodeName128], [RefTabGroupCodeName], [StrID128_Tab]) VALUES (1, 15, @ShopTab4Group4,'STORE_'+@NpcCodeName+'_G4_TAB4','STORE_'+@NpcCodeName+'_GROUP4','SN_STORE_'+@NpcCodeName+'_G4_TAB4')\r\nEND\r\nEND\r\nEND\r\nEND ELSE BEGIN\r\nPrint 'This Code already exists'\r\nEND\r\n\r\n";
        public const string AddSilkToUserByName_q =
            @"USE SRO_VT_ACCOUNT;

            DECLARE @JID INT;

            SELECT @JID = JID 
            FROM TB_User 
            WHERE StrUserID = @ACCOUNTUSER;

            IF @JID IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM SK_Silk WHERE JID = @JID)
                BEGIN
                    INSERT INTO SK_Silk (JID, silk_own, silk_gift, silk_point)
                    VALUES (@JID, 0, 0, 0);
                END

                UPDATE SK_Silk
                SET silk_own = silk_own + @SILKAMOUNT
                WHERE JID = @JID;
            END";
        public const string AddSilkToUserByJID_q =
                @"USE SRO_VT_ACCOUNT;

                IF NOT EXISTS (SELECT 1 FROM SK_Silk WHERE JID = @JID)
                BEGIN
                    INSERT INTO SK_Silk (JID, silk_own, silk_gift, silk_point)
                    VALUES (@JID, 0, 0, 0);
                END

                UPDATE SK_Silk
                SET silk_own = silk_own + @SILKAMOUNT
                WHERE JID = @JID;";

        public const string EditQuestRewardsBetweenLevel_q = @"
                USE SRO_VT_SHARD
                UPDATE _RefQuestReward
                SET [{0}] = [{0}] * @MULTIPLIER
                WHERE QuestID IN (
                    SELECT ID
                    FROM _RefQuest
                    WHERE Level > @BETWEENLEVEL AND Level < @ANDLEVEL
                );";
        public const string SetAlchemyRates_q = "USE [SRO_VT_SHARD]\r\nUPDATE _RefObjItem SET Param2 = @PARAM2, Param3 = @PARAM3, Param4 = @PARAM4 WHERE ID IN (SELECT Link FROM _RefObjCommon WHERE CodeName128 LIKE '%PROB_UP_A%')\r\n";
        public const string AddItemToShop_q = @"
            SET XACT_ABORT ON;
            USE SRO_VT_SHARD;

            DECLARE @ItemID INT = @ITEM_AID;
            DECLARE @yourPrice INT = @PRICE;
            DECLARE @WhichTAB VARCHAR(74) = @TABCODENAME;
            DECLARE @WhichCurrency INT = @MONEYTYPE;
            DECLARE @Durability INT = @DATADUR;

            DECLARE @CodeName VARCHAR(128);
            SELECT @CodeName = CodeName128 FROM SRO_VT_SHARD.dbo._RefObjCommon WHERE ID = @ItemID;

            IF @CodeName IS NULL
            BEGIN
                RAISERROR('INVALID ITEM ID: %d', 16, 1, @ItemID);
                RETURN;
            END;

            UPDATE SRO_VT_SHARD.dbo._RefObjCommon
            SET Price = @yourPrice
            WHERE ID = @ItemID;

            -- _RefPackageItem
            IF NOT EXISTS (
                SELECT 1 FROM SRO_VT_SHARD.dbo._RefPackageItem
                WHERE CodeName128 = 'PACKAGE_' + @CodeName
                  AND Country = 15
            )
            BEGIN
                DECLARE @SN VARCHAR(128);
                DECLARE @DESC VARCHAR(128);
                DECLARE @DDJ VARCHAR(128);

                SELECT 
                    @SN = NameStrID128,
                    @DESC = DescStrID128,
                    @DDJ = AssocFileIcon128
                FROM SRO_VT_SHARD.dbo._RefObjCommon
                WHERE ID = @ItemID;

                INSERT INTO SRO_VT_SHARD.dbo._RefPackageItem (
                    Service, Country, CodeName128, SaleTag, ExpandTerm,
                    NameStrID, DescStrID, AssocFileIcon,
                    Param1, Param1_Desc128,
                    Param2, Param2_Desc128,
                    Param3, Param3_Desc128,
                    Param4, Param4_Desc128
                )
                VALUES (
                    1, 15, 'PACKAGE_' + @CodeName, 0, 'EXPAND_TERM_ALL',
                    @SN, @DESC, @DDJ,
                    -1, 'xxx', -1, 'xxx', -1, 'xxx', -1, 'xxx'
                );
            END;

            -- _RefPricePolicyOfItem (FIXED: added Country = 15 to EXISTS check)
            IF NOT EXISTS (
                SELECT 1 FROM SRO_VT_SHARD.dbo._RefPricePolicyOfItem
                WHERE RefPackageItemCodeName = 'PACKAGE_' + @CodeName
                  AND PaymentDevice = 1
                  AND Country = 15
            )
            BEGIN
                INSERT INTO SRO_VT_SHARD.dbo._RefPricePolicyOfItem (
                    Service, Country, RefPackageItemCodeName,
                    PaymentDevice, PreviousCost, Cost,
                    Param1, Param1_Desc128,
                    Param2, Param2_Desc128,
                    Param3, Param3_Desc128,
                    Param4, Param4_Desc128
                )
                VALUES (
                    1, 15, 'PACKAGE_' + @CodeName,
                    1, @WhichCurrency, @yourPrice,
                    -1, 'xxx', -1, 'xxx', -1, 'xxx', -1, 'xxx'
                );
            END;

            -- _RefShopGoods (FIXED: added Country = 15 to EXISTS check)
            IF NOT EXISTS (
                SELECT 1 FROM SRO_VT_SHARD.dbo._RefShopGoods
                WHERE RefPackageItemCodeName = 'PACKAGE_' + @CodeName
                  AND RefTabCodeName = @WhichTAB
                  AND Country = 15
            )
            BEGIN
                DECLARE @newSLOTINDEX INT;

                SELECT @newSLOTINDEX = CASE 
                    WHEN EXISTS (
                        SELECT 1 FROM SRO_VT_SHARD.dbo._RefShopGoods
                        WHERE RefTabCodeName = @WhichTAB
                          AND Country = 15
                    )
                    THEN (
                        SELECT MAX(SlotIndex) + 1
                        FROM SRO_VT_SHARD.dbo._RefShopGoods
                        WHERE RefTabCodeName = @WhichTAB
                          AND Country = 15
                    )
                    ELSE 0
                END;

                INSERT INTO SRO_VT_SHARD.dbo._RefShopGoods (
                    Service, Country, RefTabCodeName,
                    RefPackageItemCodeName, SlotIndex,
                    Param1, Param1_Desc128,
                    Param2, Param2_Desc128,
                    Param3, Param3_Desc128,
                    Param4, Param4_Desc128
                )
                VALUES (
                    1, 15, @WhichTAB,
                    'PACKAGE_' + @CodeName,
                    @newSLOTINDEX,
                    -1, 'xxx', -1, 'xxx', -1, 'xxx', -1, 'xxx'
                );
            END;

            -- _RefScrapOfPackageItem
            IF NOT EXISTS (
                SELECT 1 FROM SRO_VT_SHARD.dbo._RefScrapOfPackageItem
                WHERE RefPackageItemCodeName = 'PACKAGE_' + @CodeName
                  AND RefItemCodeName = @CodeName
                  AND Country = 15
            )
            BEGIN
                INSERT INTO SRO_VT_SHARD.dbo._RefScrapOfPackageItem (
                    Service, Country, RefPackageItemCodeName,
                    RefItemCodeName, OptLevel, Variance,
                    Data, MagParamNum,
                    MagParam1, MagParam2, MagParam3, MagParam4,
                    MagParam5, MagParam6, MagParam7, MagParam8,
                    MagParam9, MagParam10, MagParam11, MagParam12,
                    Param1, Param1_Desc128,
                    Param2, Param2_Desc128,
                    Param3, Param3_Desc128,
                    Param4, Param4_Desc128
                )
                VALUES (
                    1, 15, 'PACKAGE_' + @CodeName,
                    @CodeName,
                    0, 0,
                    @Durability, 0,
                    0,0,0,0,0,0,0,0,0,0,0,0,
                    -1,'xxx', -1,'xxx', -1,'xxx', -1,'xxx'
                );
            END;

            PRINT 'DONE';
            ";
        public const string AddReverseToCharacterPosition_q = "USE SRO_VT_Shard\r\nDECLARE @Name128 varchar(50), @RegionCode int, @POSX int, @POSY int, @POSZ int\r\n\r\nSET @Name128 = @ZONENAME\r\nSet @RegionCode = @REGIONCODEID\r\nSet @POSX = @POSXCLIENT\r\nSet @POSY = @POSYCLIENT\r\nSet @POSZ = @POSZCLIENT\r\n\r\nINSERT INTO _RefOptionalTeleport (Service, ObjName128, ZoneName128,\r\n\t\t\tRegionID, Pos_X, Pos_Y, Pos_Z, WorldID, RegionIDGroup, MapPoint,\r\n\t\t\tLevelMin, LevelMax, Param1, Param1_Desc_128, Param2, Param2_Desc_128,\r\n\t\t\tParam3, Param3_Desc_128)\r\n\r\nVALUES (1, '?????','SN_'+ @Name128, @RegionCode, @POSX, @POSY, @POSZ, 1, -1, 1, 0, 0, -1, 'xxx', -1, 'xxx', -1, 'xxx')";
        public const string ChangeFWDropRatesAll_q = "Use SRO_VT_SHARD\r\nUpdate _RefMonster_AssignedItemRndDrop\r\nset DropRatio = @DROPRATIOFW, DropAmountMin = @DROPAMOUNTMIN, DropAmountMax = @DROPAMOUNTMAX\r\nWhere ItemGroupCodeName128 like 'ITEM_TALISMAN_%'";
        public const string ChangeFWDropRates_q = "Use SRO_VT_SHARD\r\nUpdate _RefMonster_AssignedItemRndDrop\r\nset DropRatio = @DROPRATIOFW, DropAmountMin = @DROPAMOUNTMIN, DropAmountMax = @DROPAMOUNTMAX\r\nWhere ItemGroupCodeName128 like @TALISMANGROUP";
        public const string ChangeMonsterSpawns_q = @"
                USE SRO_VT_SHARD;

                UPDATE N
                SET N.dwMaxTotalCount = @MaxSpawnerCount
                FROM dbo.Tab_RefNest AS N
                WHERE N.dwTacticsID IN
                (
                    SELECT T.dwTacticsID
                    FROM dbo.Tab_RefTactics AS T
                    WHERE T.dwObjID IN
                    (
                        SELECT C.ID
                        FROM dbo._RefObjCommon AS C
                        WHERE C.CodeName128 LIKE @MobCodeName + '%'
                        AND C.CodeName128 NOT LIKE '%_NPC_%'
                    )
                );
                ";

        public const string ChangeMonsterSpawnsByExact_q = @"
                USE SRO_VT_SHARD;

                UPDATE N
                SET N.dwMaxTotalCount = @MaxSpawnerCount
                FROM dbo.Tab_RefNest AS N
                WHERE N.dwTacticsID IN
                (
                    SELECT T.dwTacticsID
                    FROM dbo.Tab_RefTactics AS T
                    WHERE T.dwObjID IN
                    (
                        SELECT C.ID
                        FROM dbo._RefObjCommon AS C
                        WHERE C.CodeName128 = @MobCodeName
                        AND C.CodeName128 NOT LIKE '%_NPC_%'
                    )
                );
                ";

        // Known area prefixes that identify actual monsters.
        // Run individually — some MOB_* entries are not monsters (e.g. MOB_ prefixed objects).
        public static readonly string[] MonsterAreaPrefixes = new string[]
        {
            "MOB_CH_",   // Jangan
            "MOB_WC_",   // Donwhang (West China)
            "MOB_KT_",   // Hotan
            "MOB_KK_",   // Karakoram
            "MOB_TQ_",   // Qin-Shi Tomb
            "MOB_RM_",   // Roc Mountain
            "MOB_OA_",   // Oasis
            "MOB_SD_",   // Abundance Grounds / King's Valley (Alex Desert)
            "MOB_EU_",   // Europe / Constantinople
            "MOB_AM_",   // Asia Minor
            "MOB_CA_",   // Central Asia (Samarkand)
            "MOB_TK_",   // Taklaman
            "MOB_GOD_",  // Special / God Zones
        };

        public const string GetMonsterSpawnCounts_q = @"
                USE SRO_VT_SHARD;
                SELECT
                    C.CodeName128      AS MobCode,
                    MAX(N.dwMaxTotalCount) AS MaxCount
                FROM dbo.Tab_RefNest AS N
                INNER JOIN dbo.Tab_RefTactics AS T ON N.dwTacticsID = T.dwTacticsID
                INNER JOIN dbo._RefObjCommon  AS C ON T.dwObjID    = C.ID
                WHERE C.CodeName128 LIKE @MobCodeName + '%'
                  AND C.CodeName128 NOT LIKE '%_NPC_%'
                GROUP BY C.CodeName128
                ORDER BY C.CodeName128;
                ";

        public const string ChangeItemMaxStack_q = "USE SRO_VT_SHARD\r\nUPDATE _RefObjItem\r\nSET MaxStack = @MAXITEMSTACK\r\nWHERE ID = (\r\n    SELECT Link\r\n    FROM _RefObjCommon\r\n    WHERE CodeName128 = @ITEMCODENAME\r\n);\r\n";
        public const string UpdatePrivilegedIp_q = @"
                UPDATE SRO_VT_ACCOUNT.dbo._PrivilegedIP
                SET
                    szIPBegin  = @szIPBegin,
                    szIPEnd    = @szIPEnd,
                    szGM       = @szGM,
                    dIssueDate = @dIssueDate,
                    szISP      = @szISP
                WHERE nIdx = @nIdx;";
        public const string UpdateRegionAssoc_q = @"
                UPDATE SRO_VT_SHARD.dbo._RefRegionBindAssocServer
                SET AssocServer = @AssocServer
                WHERE AreaName = @AreaName;";

        public const string TruncateIPLogs_q = "TRUNCATE TABLE SRO_VT_LOG.dbo._IPLogs";
        public const string TruncateCharactersByJID_q = "DECLARE @JID int = @DUSERID  -- replace this\r\nDROP TABLE IF EXISTS #CharIDs\r\nCREATE TABLE #CharIDs (CharID int)\r\nINSERT INTO #CharIDs\r\n    SELECT CharID FROM SRO_VT_SHARD.dbo._User WHERE UserJID = @JID\r\n\r\nDELETE FROM SRO_VT_SHARD.dbo._CharCollectionBook         WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharInstanceWorldData      WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharNameList               WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharNickNameList           WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharQuest                  WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharSkill                  WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharSkillMastery           WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharTrijob                 WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._CharTrijobSafeTrade        WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._ClientConfig               WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._DeletedChar                WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._ExploitLog                 WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._FleaMarketNetwork          WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._Friend                     WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._GuildMember                WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._Inventory                  WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._InventoryForAvatar         WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._Memo                       WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._OldTrijob                  WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._RefObjCharExtraSkill       WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._ResultOfPackageItemToMappingWithServerSide WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._SiegeFortressBattleRecord  WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._Skill_BaoHiem_TNET         WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._StaticAvatar               WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._TrainingCampMember         WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._TrainingCampSubMentorHonorPoint WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._TimedJob                   WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._TimedJobForPet             WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_LOG.dbo._IPLogs\r\nWHERE CharID IN (SELECT CharID FROM #CharIDs)\r\nDELETE FROM SRO_VT_SHARD.dbo._User                       WHERE UserJID = @JID\r\nDELETE FROM SRO_VT_SHARD.dbo._Char                       WHERE CharID IN (SELECT CharID FROM #CharIDs)\r\n\r\nDROP TABLE #CharIDs";
        public const string GetAccountJIDByCharName_q = @"
                        SELECT TOP 1 us.UserJID
                        FROM SRO_VT_SHARD.dbo._Char c
                        JOIN SRO_VT_SHARD.dbo._User us ON c.CharID = us.CharID
                        WHERE c.CharName16 = @CharName
                          AND c.Deleted = 0";
        public static readonly string[] FixUniqueSpawns_q = new string[]
            {
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_CH_TIGERWOMAN%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_OA_URUCHI%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TK_BONELORD%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_EU_KERBEROS%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_AM_IVY%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_GOD_%%TREASURE%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_KK_ISYUTARU%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_BLACKSNAKE%'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_APIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_TOMBGENERAL'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_TOMBGENERAL_CLON'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_TOMBGENERAL_CLON2'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_TOMBGENERAL_CLON3'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_SNAKEGENERAL'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_SNAKEGENERAL_CLON'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_SNAKEGENERAL_CLON2'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_SNAKEGENERAL_CLON3'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_NORTHGUARDIAN'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_SOUTHGUARDIAN'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_WESTGUARDIAN'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_EASTGUARDIAN'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_BLACKSNAKE'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_BLACKSNAKE_L2'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_BLACKSNAKE_L3'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_RM_TAHOMET'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_RM_TAHOMET_L2'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_RM_TAHOMET_L3'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_SEPTU'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_SOPEDU'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_PETBE'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_SPHINX'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_SEKHMET'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_NEPHTHYS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_HORUS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_OSIRIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_SELKIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_NEITH'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_ANUBIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_ISIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_HAROERIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_ERIS'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_SD_SETH'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_TREASURE_GUARD'))",
                 "Use [SRO_VT_SHARD]\r\nUpdate Tab_RefNest\r\nSet dwMaxTotalCount = 1\r\n\r\nWHERE dwTacticsID IN\r\n(SELECT [dwTacticsID] FROM [dbo].[Tab_RefTactics] WHERE dwObjID IN\r\n(select ID from _RefObjCommon where CodeName128 like 'MOB_TQ_TREASURE_GUARD_CLON'))",
            };

        #endregion

        #region - Matching -

        public static readonly Dictionary<Town, int> TownLinks = new()
        {
            { Town.Jangan, 1 },
            { Town.Donwhang, 2 },
            { Town.Hotan, 5 },
            { Town.Samarkand, 25 },
            { Town.Constantinople, 20 },
            { Town.AlexandriaNorth, 176 },
            { Town.AlexandriaSouth, 175 }
        };

        #endregion

        #region - Opcodes -

        public const ushort
                LOGIN_CLIENT_INFO = 0x2001,
                LOGIN_CLIENT_KEEP_ALIVE = 0x2002,
                LOGIN_CLIENT_PATCH_REQUEST = 0x6100,
                LOGIN_CLIENT_SERVERLIST_REQUEST = 0x6101,
                LOGIN_CLIENT_AUTH = 0x6102,
                LOGIN_CLIENT_ACCEPT_HANDSHAKE = 0x9000,
                LOGIN_CLIENT_LAUNCHER = 0x6104,
                LOGIN_CLIENT_SECONDARY_PASSCODE = 0x7625,

                LOGIN_SERVER_INFO = 0x2001,
                LOGIN_SERVER_HANDSHAKE = 0x5000,
                LOGIN_SERVER_PATCH_INFO = 0x600D,
                LOGIN_SERVER_LAUNCHER = 0x600D,
                LOGIN_SERVER_LIST = 0xA101,
                LOGIN_SERVER_AUTH_INFO = 0xA102,
                LOGIN_SERVER_RESULT = 0xB624,

                 CLIENT_INFO = 0x2001,
                 CLIENT_ACCEPT_HANDSHAKE = 0x9000,
                 CLIENT_KEEP_ALIVE = 0x2002,
                 CLIENT_PATCH_REQUEST = 0x6100,
                 CLIENT_AUTH = 0x6103,
                 CLIENT_ITEM_MOVE = 0x7034,
                 CLIENT_INGAME_NOTIFY = 0x3012,//0x70EA,
                 CLIENT_CLOSE = 0x7005,
                 CLIENT_COUNTDOWN_INTERRUPT = 0x7006,
                 CLIENT_CHARACTER = 0x7007,
                 CLIENT_CHAT = 0x7025,
                 CLIENT_INGAME_REQUEST = 0x7001,
                 CLIENT_TARGET = 0x7045,
                 CLIENT_GM = 0x7010,
                 CLIENT_MOVEMENT = 0x7021,
                 CLIENT_TRANSPORT_MOVE = 0x70C5,
                 CLIENT_PLAYER_ACTION = 0x7074,
                 CLIENT_STR_UPDATE = 0x7050,
                 CLIENT_INT_UPDATE = 0x7051,
                 CLIENT_CHARACTER_STATE = 0x704F,
                 CLIENT_RESPAWN = 0x3053,
                 CLIENT_MASTERYUPDATE = 0x70A2,
                 CLIENT_SKILLUPDATE = 0x70A1,
                 CLIENT_EMOTION = 0x3091,
                 CLIENT_ITEM_USE = 0x704C,
                 CLIENT_HOTKEY_CHANGE = 0x7158,
                 CLIENT_OPEN_SHOP = 0x7046,
                 CLIENT_CLOSE_SHOP = 0x704B,
                 CLIENT_TELEPORT = 0x705A,
                 CLIENT_PARTY_FORM = 0x7069,
                 CLIENT_PARTY_EDIT = 0x706A,
                 CLIENT_PARTY_DELETE = 0x706B,
                 CLIENT_PARTY_MATCHING = 0x706C,
                 CLIENT_PARTY_REQUEST = 0x706D,
                 CLIENT_PARTY_ACCEPT = 0x306E,
                 CLIENT_PARTY_INVITE = 0x7060,
                 CLIENT_PARTY_DISMISS = 0x7061,
                 CLIENT_PARTY_KICK = 0x7063,
                 CLIENT_ANIMATION_INVITE = 0x3080,
                 CLIENT_ALCHEMY = 0x7150,
                 CLIENT_TRANSPORT_HOME = 0x70CB,
                 CLIENT_TRANSPORT_DELETE = 0x70CB,
                 CLIENT_OPEN_STORAGE = 0x703C,
                 CLIENT_REPAIR = 0x703E,
                 CLIENT_USE_BERSERK = 0x70A7,

                 SERVER_INFO = 0x2001,
                 SERVER_HANDSHAKE = 0x5000,
                 SERVER_PATCH_INFO = 0x600D,
                 SERVER_LOGIN_RESULT = 0xA103,

                 SERVER_CHARACTER = 0xB007,
                 SERVER_CHARDATA = 0x3013,
                 SERVER_INGAME_ACCEPT = 0xB001,
                 SERVER_LOADING_START = 0x34A5,
                 SERVER_LOADING_END = 0x34A6,
                 SERVER_CHAR_ID = 0x3020,

                 SERVER_SPAWN = 0x3015,
                 SERVER_DESPAWN = 0x3016,

                 SERVER_GROUPSPAWN_HEAD = 0x3017,
                 SERVER_GROUPSPAWN_BODY = 0x3019,
                 SERVER_GROUPSPAWN_TAIL = 0x3018,

                 SERVER_ITEM_EQUIP = 0x3038,
                 SERVER_ITEM_UNEQUIP = 0x3039,
                 SERVER_ITEM_MOVEMENT = 0xB034,
                 SERVER_NEW_GOLD_AMOUNT = 0x304E,
                 SERVER_ANIMATION_ITEM_PICKUP = 0x3036,
                 SERVER_ITEM_USE = 0xB04C,
                 SERVER_ANIMATION_ITEM_USE = 0x305C,
                 SERVER_ANIMATION_CAPE = 0x3041,
                 SERVER_ITEM_QUANTITY_UPDATE = 0x3040,

                 SERVER_QUIT_GAME = 0x300A,
                 SERVER_COUNTDOWN = 0xB005,
                 SERVER_COUNTDOWN_INTERRUPT = 0xB006,

                 SERVER_STATS = 0x303D,
                 SERVER_STR_UPDATE = 0xB050,
                 SERVER_INT_UPDATE = 0xB051,
                 SERVER_CHARACTER_STATE = 0x30BF,
                 SERVER_HPMP_UPDATE = 0x3057,
                 SERVER_ANIMATION_LEVEL_UP = 0x3054,
                 SERVER_EXP = 0x3056,
                 SERVER_MASTERYUPDATE = 0xB0A2,
                 SERVER_SKILLPOINTS = 0x304E,
                 SERVER_SKILLUPDATE = 0xB0A1,

                 SERVER_CHAT = 0x3026,
                 SERVER_CHAT_ACCEPT = 0xB025,

                 SERVER_TARGET = 0xB045,
                 SERVER_MOVEMENT = 0xB021,
                 SERVER_UNIQUE = 0x300C,

                 SERVER_ANIMATION_COS_SPAWN = 0x30C8,
                 SERVER_COS_SIT_UP = 0xB0CB,
                 SERVER_ANIMATION_COS_REMOVE_MENU = 0x30C9,
                 SERVER_COS_DELETE = 0xB0C6,

                 SERVER_ATTACK = 0xB070,
                 SERVER_SKILL_ATTACK = 0xB074,
                 SERVER_END_SKILL = 0xB071,

                 SERVER_BUFF_START = 0xB0BD,
                 SERVER_BUFF_END = 0xB072,

                 SERVER_DEAD = 0x3011,
                 SERVER_DEAD2 = 0x30D2,

                 SERVER_PARTY_FORM = 0xB069,
                 SERVER_PARTY_EDIT = 0xB06A,
                 SERVER_PARTY_DELETE = 0xB06B,
                 SERVER_PARTY_MATCHING = 0xB06C,
                 SERVER_PARTY_ACCEPT = 0xB06D,
                 SERVER_PARTY_REQUEST = 0x706D,
                 SERVER_PARTY_NEW_PARTY = 0x3065,
                 SERVER_PARTY_CHANGES = 0x3864,
                 SERVER_PARTY_INVITE = 0xB060,
                 SERVER_ANIMATION_INVITE = 0x3080,

                 SERVER_OPEN_SHOP = 0xB046,
                 SERVER_CLOSE_SHOP = 0xB04B,
                 SERVER_SILK_AMOUNT = 0x3153,

                 SERVER_TELEPORT = 0xB05A,
                 SERVER_ANIMATION_TELEPORT = 0x34B5,

                 SERVER_STORAGE_GOLD = 0x3047,
                 SERVER_STORAGE_ITEMS = 0x3049,
                 SERVER_STORAGE_END = 0x3048,

                 SERVER_ALCHEMY = 0xB150,

                 SERVER_REPAIR = 0xB03E,
                 SERVER_ITEM_DURABILITY_CHANGE = 0x3052,

                 SERVER_CHARACTER_STUCK = 0xB023,

                 // ------------ Custom -------------
                 DEW_SORT = 0xE101,
                 DEW_SESSION_TIME = 0xE102,
                 DEW_TOTAL_TIME = 0xE103;

        #endregion

    }
}
