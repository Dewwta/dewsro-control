#pragma once

#include "IFWnd.h"

#include "IFEdit.h"

/// \note If u are planing to change this key chackout "CIFConsole::SetVisibleMode"
#define CHEAT_CONSOLE_TOGGLE_KEY '~'// VK_F15

class CIFConsole;

enum eVisibleMode : DWORD {
    VISIBLE_MODE_INVISIBLE,

    VISIBLE_MODE_OPENING,
    VISIBLE_MODE_CLOSING,

    VISIBLE_MODE_VISIBLE
};

typedef std::n_vector<std::n_wstring> vecCONSOLE_CHEATCODE;
typedef BOOL (CIFConsole::*fnConsoleCommand)(vecCONSOLE_CHEATCODE &);
typedef std::n_hash_map<std::n_wstring, fnConsoleCommand> hashCONSOLE_COMMANDS;

class CIFConsole : public CIFWnd {
public:
private:
    char pad_036C[4];                   //0x036C
    CIFEdit *m_pInputBox;               //0x0370
    char pad_0374[0x03B8 - 0x0374];     //0x0374
    eVisibleMode m_CurVisibleMode;      //0x03B8
    char pad_03BC[0x041C - 0x03BC];     //0x03BC
    std::n_wstring m_wstring[8];        //0x041C Why? for what i'm using for?
    char pad_04fc[0x057C - 0x04FC];     //0x04F0
    hashCONSOLE_COMMANDS m_hashCommands;//0x057C
public:
    /// \address 00537bc0
    BOOL IsConsolePermissioned();

    /// \address 00537c60
    void SetVisibleMode(DWORD dwMode);

    /// \address 00537ce0
    BOOL SetFocusOnInputBox();

    /// \address 00555db0
    void RegisterCommand(std::n_wstring wstrName, fnConsoleCommand fn);

    /// \address 00558910
    void InitializeCommandsList();

private:
    /// <summary>
    /// An example command just show an local notice msg
    /// </summary>
    /// <param name="vecCheatCode"></param>
    /// <returns></returns>
    BOOL OnCheatKey_SRO_DevKit(vecCONSOLE_CHEATCODE &vecCheatCode);

    /// <summary>
    /// An quick way to tp without warp and stuff
    /// Usage: '/wp2 RegionID X Y Z WorldID'
    /// </summary>
    /// <param name="vecCheatCode"></param>
    /// <returns></returns>
    BOOL OnCheatKey_Wp2(vecCONSOLE_CHEATCODE &vecCheatCode);

private:
    BEGIN_FIXTURE()
    ENSURE_SIZE(0x05A4)
    ENSURE_OFFSET(m_pInputBox, 0x0370)
    ENSURE_OFFSET(m_CurVisibleMode, 0x03B8)
    ENSURE_OFFSET(m_hashCommands, 0x057C)
    END_FIXTURE()

    RUN_FIXTURE(CIFConsole)
};
