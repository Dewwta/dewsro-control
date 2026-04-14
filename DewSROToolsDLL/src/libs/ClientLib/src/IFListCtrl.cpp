#include "IFListCtrl.h"

GFX_IMPLEMENT_DYNAMIC_EXISTING(CIFListCtrl, 0x00ee9580)

void CIFListCtrl::SetBottomAligned(bool bottomAligned) {
    m_bottomAligned = bottomAligned;
}

void CIFListCtrl::SetRespondToMouseMove(bool respondToMouseMove) {
    m_respondToMouseMove = respondToMouseMove;
}

void CIFListCtrl::SetHighlightColor(D3DCOLOR color) {
    m_BackgroundColor = color;
}

void CIFListCtrl::SetHightlineLine(bool a2) {
    m_bHighlighLine = a2;
}

void CIFListCtrl::sub_638D40(undefined1 param_1) {
    field_0x385 = param_1;
}

void CIFListCtrl::sub_638D50(undefined1 param_1) {
    field_0x388 = param_1;
}

bool CIFListCtrl::OnCreate(long ln) {
    return true;
}

CIFListCtrl::SLineOfText *CIFListCtrl::sub_63A940() {
    return reinterpret_cast<CIFListCtrl::SLineOfText *(__thiscall *) (CIFListCtrl *)>(0x63A940)(this);
}

int CIFListCtrl::GetNumberOfItems() const {
    return m_numberOfItems;
}

void CIFListCtrl::SetLineHeight(int height) {
    m_LineHeight = height;
}
