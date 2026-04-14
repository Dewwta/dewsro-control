#include "GInterface.h"

#include "IFflorian0Guide.h"
#include "IFNotify.h"
#include "IFChatViewer.h"
#include "IFNotify.h"

#include "Game.h"

bool CGInterface::OnCreateIMPL(long ln) {

    BeforeOnCreate();

    bool b = reinterpret_cast<bool (__thiscall *)(CGInterface *, long)>(0x0078c910)(this, ln);

    AfterOnCreate();

    return b;
}

void CGInterface::OnTimerIMPL(int timerId) {
    switch (timerId) {

        case TIMER_AUTOPOTION_HP:
            m_pAutoPotion->UseHPPotion();
            break;

        case TIMER_AUTOPOTION_MP:
            m_pAutoPotion->UseMPPotion();
            break;

        case TIMER_AUTOPOTION_CURE_PILL:
            m_pAutoPotion->UseCurePill();
            break;

        default:
            reinterpret_cast<void(__thiscall *)(CGInterface *, int)>(0x00789820)(this, timerId);
            break;
    }
}

BOOL CGInterface::OnCharInSusspendMode(UINT nChar, UINT nRepCnt, UINT nFlags) {
    return reinterpret_cast<BOOL (__thiscall *)(CGInterface *, UINT, UINT, UINT)>(0x0078ae00)(this, nChar, nRepCnt, nFlags);
}

UINT CGInterface::FUN_00778310(UINT &nChar, UINT &nFlags) {
    return reinterpret_cast<UINT (__thiscall *)(CGInterface *, UINT &, UINT &)>(0x00778310)(this, nChar, nFlags);
}

void CGInterface::OnEscapePressed() {
    reinterpret_cast<void (__thiscall *)(CGInterface *)>(0x0079d620)(this);
}

BOOL CGInterface::OnCharIMPL(UINT nChar, UINT nRepCnt, UINT nFlags) {
    if (m_bDisappeard)
        return TRUE;
    
    if (m_SuspendMode != SUSPEND_MODE_NONE)
        return OnCharInSusspendMode(nChar, nRepCnt, nFlags);

    if (nChar == CHEAT_CONSOLE_TOGGLE_KEY)
        CGame::GetCheatConsole()->SetVisibleMode(VISIBLE_MODE_OPENING);

    if (FUN_00778310(nChar, nFlags) != -1)
        return TRUE;
    
    if (nChar == VK_RETURN) {
        if (CGame::GetCheatConsole()->SetFocusOnInputBox() == FALSE)
            m_IRM.GetResObj<CIFChatViewer>(GDR_CHAT_BOARD, 1)->SetFocusToInputBox();

        return TRUE;
    }

    if (nChar == VK_ESCAPE)
        OnEscapePressed();

    return TRUE;
}

void CGInterface::BeforeOnCreate() {

}

void CGInterface::AfterOnCreate() {
    CreateFlorian0Event();
}

void CGInterface::ShowMessage_Quest(const std::n_wstring &msg) {
    CIFNotify *notify = m_IRM.GetResObj<CIFNotify>(GDR_UPDATE_QUEST_INFO, 1);
    notify->ShowMessage(msg);
}

void CGInterface::ShowMessage_Notice(const std::n_wstring &msg) {
    CIFNotify *notify = m_IRM.GetResObj<CIFNotify>(GDR_NOTICE, 1);
    notify->ShowMessage(msg);
}

void CGInterface::ShowMessage_Warning(const std::n_wstring &msg) {
    CIFNotify *notify = m_IRM.GetResObj<CIFNotify>(GDR_WARNING_WND, 1);
    notify->ShowMessage(msg);
}

int CGInterface::Get_SelectedObjectId() {
    return this->m_selectedObjectId;
}

CIFTimerWnd *CGInterface::Get_TimerWindow() {
    return this->m_timerWindow;
}

CIFQuickStateHalfWnd *CGInterface::Get_QuickStateHalfWnd() {
    return this->N00002637;
}

void CGInterface::WriteErrorMessage(byte errorType, unsigned __int16 errorCode, int colorARGB, int a5, int a6) {
    CIFSystemMessage *pSystemMsg = m_IRM.GetResObj<CIFSystemMessage>(GDR_SYSTEM_MESSAGE_VIEW, 1);
    if (pSystemMsg == NULL)
        return;

    pSystemMsg->WriteErrorMessage(errorType, errorCode, colorARGB, a5, a6);
}

void CGInterface::WriteSystemMessage(eLogType btLevel, LPCWSTR lpszText) {
    CIFSystemMessage *pSystemMsg = m_IRM.GetResObj<CIFSystemMessage>(GDR_SYSTEM_MESSAGE_VIEW, 1);
    if (!pSystemMsg->IsLogAble(btLevel))
        return;

    D3DCOLOR dwColor;
    if ((btLevel == SYSLOG_COMBAT) || (btLevel == SYSLOG_GUIDE)) {
        dwColor = D3DCOLOR_ARGB(255, 186, 207, 242);
    } else {
        dwColor = D3DCOLOR_ARGB(255, 220, 201, 155);
    }

    pSystemMsg->WriteMessage(255, dwColor, lpszText, 0, 1);
}

void CGInterface::sub_787C10(SChatMetaData &meta) {
    reinterpret_cast<void (__thiscall *)(CGInterface *, SChatMetaData *)>(0x787C10)(this, &meta);
}

int CGInterface::TryParseChatCommands(const wchar_t *text, RECT &r, std::vector<void *> &v) {
    /// \todo implement me.
    return reinterpret_cast<
            int (__thiscall *)(CGInterface *, const wchar_t *, RECT *, std::vector<void *> *)
            >(0x0078BEA0)(this, text, &r, &v);
}

void CGInterface::FUN_00777c30(ChatType type, const wchar_t *message, D3DCOLOR color, int a5) {
    CIFChatViewer *chatViewer = m_IRM.GetResObj<CIFChatViewer>(GDR_CHATVIEWER, 1);
    chatViewer->FUN_007aca30(type, color, message, 0, a5);
}

void CGInterface::WriteInMarketChatMsg(BYTE btChatType, LPCWSTR lpszMsg, D3DCOLOR dwColor) {
    m_pMarketChatModule[btChatType]->WriteChatMessage(lpszMsg, dwColor);
}

void CGInterface::AddTargetToWhisperList(std::n_wstring &recipient) {
    m_IRM.GetResObj<CIFChatViewer>(GDR_CHAT_BOARD, true)->AddWhisperTargetToList(recipient);
}
