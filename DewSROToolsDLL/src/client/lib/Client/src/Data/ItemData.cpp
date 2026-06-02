#include "ItemData.h"

const SItemData &CItemData::GetData() const {
    return mData;
}

bool SItemData::IsGlobalMessageScroll() const {
    return m_typeId.Is(TypeIdRegistry::ITEM_ETC_SCROLL_GLOBALCHATTING);
}

bool SItemData::IsItemEtc() const {
    return m_typeId.Is(TypeIdRegistry::ITEM_ETC);
}

bool SItemData::IsAmmo() const {
    return m_typeId.Is(TypeIdRegistry::ITEM_ETC_AMMO);
}

bool SItemData::IsItemCOS() const {
    return m_typeId.Is(TypeIdRegistry::ITEM_COS);
}
