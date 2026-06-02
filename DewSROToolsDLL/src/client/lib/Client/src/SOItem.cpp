#include "SOItem.h"
#include "GlobalDataManager.h"


const SItemData *CSOItem::GetItemData() const {
    const SItemData *data = NULL;

    if (m_refObjItemId != 0) {
        data = &g_CGlobalDataManager->GetItemData(m_refObjItemId);
    }

    return data;
}

int CSOItem::GetQuantity() const {
    return m_quantity;
}

bool CSOItem::IsSameItemType(const CSOItem *other) const {
    return reinterpret_cast<bool(__thiscall *)(const CSOItem *, const CSOItem *)>(0x008bac80)(this, other);
}
