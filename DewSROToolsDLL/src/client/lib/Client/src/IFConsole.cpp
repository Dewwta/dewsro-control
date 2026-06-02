#include "IFConsole.h"

#include "ICPlayer.h"

#include "BSLib/Debug.h"

#include "Game.h"
#include <algorithm>
#include <GInterface.h>

#include "SRO_GLOBAL_DEF.h"
#include "SRO_MSG_DEF.h"

#include <BSLib/multibyte.h>
#include <string>

BOOL CIFConsole::IsConsolePermissioned() {
    if (g_pMyPlayerObj != NULL && g_pMyPlayerObj->IsGameMaster())
        return TRUE;

    return (Fun_GetCfgGame()->m_bDebugMode);
}

void CIFConsole::SetVisibleMode(DWORD dwMode) {
    // Do we have the permission to toggle it?
    if (IsConsolePermissioned() == FALSE)
        return;

    // If it trying to close or trying to open by checking if Shift was pressed.
    // Replace "(GetKeyState(VK_SHIFT) & SHIFTED)" with checking "dwMode" == "VISIBLE_MODE_OPENING"
    // if u planing to change the HotKey.
    if ((dwMode == VISIBLE_MODE_INVISIBLE) || (GetKeyState(VK_SHIFT) & SHIFTED)) {
        switch (m_CurVisibleMode) {
            // if we are already closed
            case VISIBLE_MODE_INVISIBLE:
                if (IsVisible())
                    break;
                // let's show it
                ShowGWnd(true);
            // if we are in closing
            case VISIBLE_MODE_CLOSING:
                // open it back
                m_CurVisibleMode = VISIBLE_MODE_OPENING;
                break;
            // if we are opening or already opened
            case VISIBLE_MODE_OPENING:
            case VISIBLE_MODE_VISIBLE:
                // close it back
                m_CurVisibleMode = VISIBLE_MODE_CLOSING;
                break;
        }
    }
}

BOOL CIFConsole::SetFocusOnInputBox() {
    if (!IsVisible())
        return FALSE;

    m_pInputBox->SetFocus_MAYBE();
    return TRUE;
}

void CIFConsole::RegisterCommand(std::n_wstring wstrName, fnConsoleCommand fn) {
    std::n_wstring wstrCommand = wstrName;
    // Convert it to lower case
    std::transform(wstrCommand.begin(), wstrCommand.end(), wstrCommand.begin(), ::tolower);

    hashCONSOLE_COMMANDS::iterator it = m_hashCommands.find(wstrCommand);
    BS_ASSERT_MSG(it == m_hashCommands.end(), "0");

    m_hashCommands.insert(std::make_pair(wstrCommand, fn));
}

void CIFConsole::InitializeCommandsList() {
    // SRO_Devkit
    RegisterCommand(L"SRO_Devkit", &CIFConsole::OnCheatKey_SRO_DevKit);
    // WP2
    RegisterCommand(L"Wp2", &CIFConsole::OnCheatKey_Wp2);

    reinterpret_cast<void(__thiscall *)(CIFConsole *)>(0x00558910)(this);
}

BOOL CIFConsole::OnCheatKey_SRO_DevKit(vecCONSOLE_CHEATCODE &vecCheatCode) {
    // if there is an msg afer "/sro_devkit" || nothing after it
    if (vecCheatCode.size() != 2)
        return FALSE;

    // let's show notice with what after "/sro_devkit" command
    g_pCGInterface->ShowMessage_Notice(vecCheatCode[1]);
    return TRUE;
}

BOOL CIFConsole::OnCheatKey_Wp2(vecCONSOLE_CHEATCODE &vecCheatCode) {
    // We want '/wp2 RegionID X Y Z WorldID'
    if (vecCheatCode.size() != 6)
        return FALSE;

    NEWMSG(SR_SPECIAL_COMMAND_REQ);
    pReq << SR_SPECIAL_COMMAND_MOVE_TO_WP;
    pReq << (WORD) (atoi(TO_NSTRING(vecCheatCode[1]).c_str()));
    pReq << (FLOAT) (atof(TO_NSTRING(vecCheatCode[2]).c_str()));
    pReq << (FLOAT) (atof(TO_NSTRING(vecCheatCode[3]).c_str()));
    pReq << (FLOAT) (atof(TO_NSTRING(vecCheatCode[4]).c_str()));
    pReq << (WORD) (atoi(TO_NSTRING(vecCheatCode[5]).c_str()));
    SENDMSG();

    return TRUE;
}