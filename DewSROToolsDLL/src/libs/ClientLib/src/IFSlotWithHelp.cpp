///******************************************************************************
/// \File IFSlotWithHelp.cpp
///
/// \Desc
///
/// \Author kyuubi09 on 6/21/2023.
///
/// \Copyright Copyright © 2023 SRO_DevKit.
///
///******************************************************************************

#include "IFSlotWithHelp.h"

void CIFSlotWithHelp::SetSlotData(CSOItem *pItemSocket) {
    reinterpret_cast<void (__thiscall *)(CIFSlotWithHelp *, CSOItem *)>(0x006871d0)(this, pItemSocket);
}

int CIFSlotWithHelp::GetItemSourceParentWindowId() const {
    return m_itemSourceParentWindowId;
}

const CSOItem &CIFSlotWithHelp::GetMyItem() const {
    return m_myItem;
}

CSOItem *CIFSlotWithHelp::GetItem() const {
    return m_pItem;
}

void CIFSlotWithHelp::UseItem() {
    reinterpret_cast<void(__thiscall *)(CIFSlotWithHelp *)>(0x00676570)(this);
}

int CIFSlotWithHelp::GetInventorySlotIndex() const {
    return m_inventorySlotIndex;
}
