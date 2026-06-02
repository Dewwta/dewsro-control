
#include "IFGauge.h"
#include "IFStatic.h"
#include "IFTargetWindowSpecialMob.h"

GFX_IMPLEMENT_DYNAMIC_EXISTING(CIFTargetWindowSpecialMob, 0x00eea5fc)

GFX_IMPLEMENT_DYNCREATE_FN(CIFTargetWindowSpecialMob, CIFWnd)

enum {
    GDR_TWSM_GEM = 10, // CIFStatic
    GDR_TWSM_LEVEL = 4, // CIFStatic
    GDR_TWSM_ICON = 3, // CIFStatic
    GDR_TWSM_TEXT_LEV = 2, // CIFStatic
    GDR_TWSM_GAUGE_HPGAUGE = 1, // CIFGauge
    GDR_TWSM_TEXT_ID = 0, // CIFStatic
};

GFX_BEGIN_MESSAGE_MAP(CIFTargetWindowSpecialMob, CIFWnd)

GFX_END_MESSAGE_MAP()

bool CIFTargetWindowSpecialMob::OnCreate(long ln) {
    m_IRM.LoadFromFile("resinfo\\iftw_specialmob.txt");

    m_IRM.CreateInterfaceSection("Create", this);

    m_pGDR_TWSM_TEXT_LEV = m_IRM.GetResObj<CIFStatic>(GDR_TWSM_TEXT_ID, 1); // 0x370
    m_pGDR_TWSM_GAUGE_HPGAUGE = m_IRM.GetResObj<CIFGauge>(GDR_TWSM_GAUGE_HPGAUGE, 1); // 0x374
    m_pGDR_TWSM_TEXT_ID = m_IRM.GetResObj<CIFStatic>(GDR_TWSM_TEXT_LEV, 1); // 0x378

    m_pGDR_TWSM_GAUGE_HPGAUGE->field_0x38c = 0.1f;
    m_pGDR_TWSM_GAUGE_HPGAUGE->SetGWndSize(208, 4);

    return true;
}

void CIFTargetWindowSpecialMob::OnUpdate() {
    reinterpret_cast<void (__thiscall *)(const CIFTargetWindowSpecialMob *)>(0x0069b240)(this);
}
