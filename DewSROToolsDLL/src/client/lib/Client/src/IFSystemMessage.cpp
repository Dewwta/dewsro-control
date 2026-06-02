#include "IFSystemMessage.h"

void CIFSystemMessage::WriteMessage(int a1, D3DCOLOR color, const wchar_t *msg, int a2, int a3) {
    // Redirection to original client code
    reinterpret_cast<void (__thiscall *)(CIFSystemMessage *, int, int, const wchar_t*, int, int)>(0x007B89E0)(this, a1, color, msg, a2, a3);
}

void CIFSystemMessage::WriteErrorMessage(BYTE btType, WORD wErrorCode, DWORD dwColor, int a5, int a6) {
    /// \todo implement me.
    reinterpret_cast<void(__thiscall *)(CIFSystemMessage *, BYTE, WORD, DWORD, int, int)>(0x007b8b40)(this, btType, wErrorCode, dwColor, a5, a6);
}

bool CIFSystemMessage::IsLogAble(BYTE btLogType) {
    switch (btLogType) {
        case SYSLOG_NONE:
        case SYSLOG_GUIDE:
            return true;
        case SYSLOG_GAIN:
            return m_pOptionBoard->IsGainChecked();
        case SYSLOG_COMBAT:
            return m_pOptionBoard->IsFightChecked();
        case SYSLOG_STATUS:
           return m_pOptionBoard->IsStatusChecked();
        case SYSLOG_PARTY:
            return m_pOptionBoard->IsPartyChecked();
        case SYSLOG_GAME:
        case SYSLOG_7_UNK:
            return m_pOptionBoard->IsGameChecked();
        default:
            return false;
    }
}