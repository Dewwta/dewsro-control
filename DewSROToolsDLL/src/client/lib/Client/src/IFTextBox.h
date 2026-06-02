#pragma once

#include "IFListCtrl.h"

class CIFTextBox : public CIFListCtrl {
// TODO: Add Runtime Class Info
public:

    /// \address 0064E380
    void SetIndentAfterBreak(bool indentAfterBreak);

    /// \address 0064F8A0
    void sub_64F8A0(std::n_wstring &str, int a3, int a4, int a5, int a6, int a7, int a8);

    /// \address 0064F660
    void sub_64F660(std::n_string &str, int a3, int a4, int a5, int a6, int a7, int a8);


private:
    bool m_identAfterBreak; //0x03CC
    char pad_03CD[15]; //0x03CD

    /// Number of lines currently stored in the text box
    int m_CurrentLines; //0x03DC


    BEGIN_FIXTURE()
        ENSURE_SIZE(992)
        ENSURE_OFFSET(m_identAfterBreak, 0x3cc)
        ENSURE_OFFSET(m_CurrentLines, 0x3DC)

    END_FIXTURE()

    RUN_FIXTURE(CIFTextBox)
};
