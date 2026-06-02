#include "IGIDObject.h"

const std::n_wstring &CIGIDObject::GetName() const {
    return m_name;
}

const SCommonData *CIGIDObject::GetCommonData() const {
    return m_commonData;
}

const int CIGIDObject::GetUniqueId() const {
    return m_uniqueId;
}
