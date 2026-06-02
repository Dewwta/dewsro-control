////////////////////////////////////////////////////////////////////////////////////
//
// GInterfaceSend.cpp: implementation of the CGInterface network msg send functions.
//
////////////////////////////////////////////////////////////////////////////////////

#include "GInterface.h"

#include <BSLib/multibyte.h>

#include "support/MemberFunctionHook.h"

HOOK_ORIGINAL_MEMBER(0x007901c0, &CGInterface::WriteGlobalMessage)
void CGInterface::WriteGlobalMessage(unsigned char nSlot, std::n_wstring message) {
    CIFMainPopup *popup = GetMainPopup();
    CIFInventory *inventory = popup->GetInventory();

    CSOItem *item = inventory->GetItemBySlot(nSlot);

    if (item->m_blValid == 0) {
        return;
    }

    const SItemData *pItemData = item->GetItemData();

    if (pItemData->IsGlobalMessageScroll()) {
        NEWMSG(0x704c)

        pReq << static_cast<unsigned char>(nSlot + 13u) << pItemData->m_typeId << acp_encode(message);

        SENDMSG()
    }
}

void CGInterface::RequestStatIncrement(undefined4 a1, undefined4 a2, undefined4 a3, undefined4 a4) {
    if (a1 == 0x4b) {
        if (a2 == 1) {
            NEWMSG(0x7050)
            SENDMSG()
        } else if (a2 == 2) {
            NEWMSG(0x7051)
            SENDMSG()
        }
    }
}
