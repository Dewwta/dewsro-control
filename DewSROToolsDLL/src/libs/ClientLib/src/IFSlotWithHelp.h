#pragma once

#include "IFWnd.h"

#include "SOItem.h"

class CIFSlotWithHelp : public CIFWnd {
public:
public:
    void SetSlotData(CSOItem *pItemSocket);

    int GetItemSourceParentWindowId() const;

    CSOItem* GetItem() const;

    const CSOItem &GetMyItem() const;

    /// \address 00666800
    int GetInventorySlotIndex() const;

    /// \address 00676570
    void UseItem();

private:
    char pad_036C[4]; //0x036C
    int m_itemSourceParentWindowId; // 0x0370
    char pad_0374[4]; //0x0374
    int m_inventorySlotIndex; //0x0378
    char pad_037c[0x0390 - 0x037c]; //0x037c
    CSOItem* m_pItem; // 0x0390
    char pad_0394[4]; //0x0394
    CSOItem m_myItem; // 0x0398
    char pad_039c[0x06C8 - 0x568]; //0x039c

private:
    BEGIN_FIXTURE()
        ENSURE_SIZE(0x06C8)
        ENSURE_OFFSET(m_itemSourceParentWindowId, 0x0370)
        ENSURE_OFFSET(m_inventorySlotIndex, 0x0378)
        ENSURE_OFFSET(m_pItem, 0x0390)
    END_FIXTURE()

    RUN_FIXTURE(CIFSlotWithHelp)
};