#pragma once

#include "BSLib/BSLib.h"
#include "../TypeId/TypeId.h"
#include <BSLib/support/undefined.h>


struct SCommonData
{
    TypeId m_typeId;
    int RefObjectId;
    std::n_wstring CodeName;
    std::n_wstring ObjName;
    std::n_wstring OrgObjCodeName;
    undefined pad_005C[4];
    std::n_wstring NameStrID;
    std::n_wstring DescStrID;
    int DecayTime;
    int Country;
    int Rarity;
    bool CanTrade;
    bool CanSell;
    bool CanBuy;
    bool CanBorrow;
    bool CanDrop;
    bool CanPick;
    bool CanRepair;
    bool CanRevive;
    bool CanUse;
    bool CanThrow;
    char pad_00AE[2];
    unsigned long long Price;
    unsigned long long SellPrice;
    int CostRepair;
    int CostRevive;
    int CostBorrow;
    int KeepingFee;
    int ReqLevelType1;
    int ReqLevelType2;
    int ReqLevelType3;
    int ReqLevelType4;
    int ReqLevel1;
    int ReqLevel2;
    int ReqLevel3;
    int ReqLevel4;
    int MaxContain;
    int RegionId;
    int Dir; 
    int OffsetX;
    int OffsetY; 
    int OffsetZ; 
    int Speed1; 
    int Speed2; 
    int Scale; 
    int BCHeight; 
    int BCRadius; 
    undefined pad_011C[8]; 
    std::n_string AssocFileObj; 
    std::n_string AssocFileDrop; 
    std::n_string AssocFileIcon; 
    std::n_string AssocFile1; 
    std::n_string AssocFile2; 
    undefined pad_01B0[16]; 

private:
    BEGIN_FIXTURE()
        ENSURE_OFFSET(m_typeId, 0x0000)
        ENSURE_OFFSET(RefObjectId, 0x0004)
        ENSURE_OFFSET(CodeName, 0x0008)
        ENSURE_OFFSET(ObjName, 0x0024)
        ENSURE_OFFSET(OrgObjCodeName, 0x0040)
        ENSURE_OFFSET(NameStrID, 0x0060)
        ENSURE_OFFSET(DescStrID, 0x007c)
        ENSURE_OFFSET(DecayTime, 0x0098)
        ENSURE_OFFSET(Country, 0x009C)
        ENSURE_OFFSET(Rarity, 0x00A0)
        ENSURE_OFFSET(CanTrade, 0x00A4)
        ENSURE_OFFSET(CanSell, 0x00A5)
        ENSURE_OFFSET(CanBuy, 0x00A6)
        ENSURE_OFFSET(CanBorrow, 0x00A7)
        ENSURE_OFFSET(CanDrop, 0x00A8)
        ENSURE_OFFSET(CanPick, 0x00A9)
        ENSURE_OFFSET(CanRepair, 0x00AA)
        ENSURE_OFFSET(CanRevive, 0x00AB)
        ENSURE_OFFSET(CanUse, 0x00AC)
        ENSURE_OFFSET(CanThrow, 0x00AD)
        ENSURE_OFFSET(Price, 0x00B0)
        ENSURE_OFFSET(SellPrice, 0x00B8)
        ENSURE_OFFSET(CostRepair, 0x00C0)
        ENSURE_OFFSET(CostRevive, 0x00C4)
        ENSURE_OFFSET(CostBorrow, 0x00C8)
        ENSURE_OFFSET(KeepingFee, 0x00CC)
        ENSURE_OFFSET(ReqLevelType1, 0x00D0)
        ENSURE_OFFSET(ReqLevelType2, 0x00D4)
        ENSURE_OFFSET(ReqLevelType3, 0x00D8)
        ENSURE_OFFSET(ReqLevelType4, 0x00DC)
        ENSURE_OFFSET(ReqLevel1, 0x00E0)
        ENSURE_OFFSET(ReqLevel2, 0x00E4)
        ENSURE_OFFSET(ReqLevel3, 0x00E8)
        ENSURE_OFFSET(ReqLevel4, 0x00EC)
        ENSURE_OFFSET(MaxContain, 0x00F0)
        ENSURE_OFFSET(RegionId, 0x00F4)
        ENSURE_OFFSET(Dir, 0x00F8)
        ENSURE_OFFSET(OffsetX, 0x00FC)
        ENSURE_OFFSET(OffsetY, 0x0100)
        ENSURE_OFFSET(OffsetZ, 0x0104)
        ENSURE_OFFSET(Speed1, 0x0108)
        ENSURE_OFFSET(Speed2, 0x010C)
        ENSURE_OFFSET(Scale, 0x0110)
        ENSURE_OFFSET(BCHeight, 0x0114)
        ENSURE_OFFSET(BCRadius, 0x0118)
        ENSURE_OFFSET(AssocFileObj, 0x0124)
        ENSURE_OFFSET(AssocFileDrop, 0x0140)
        ENSURE_OFFSET(AssocFileIcon, 0x015c)
        ENSURE_OFFSET(AssocFile1, 0x0178)
        ENSURE_OFFSET(AssocFile2, 0x0194)

        END_FIXTURE()

        RUN_FIXTURE(SCommonData)
};
