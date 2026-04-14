////////////////////////////////////////////////////////////////////////////////
//
// GInterfaceWndRelation.cpp: implementation of the CGInterface wnd realtion functions.
//
////////////////////////////////////////////////////////////////////////////////

#include "GInterface.h"

#include "GlobalDataManager.h"

#include "Game.h"

#include <BSLib/support/MemberFunctionHook.h>

const int CGInterface::nGWndSavePostionsID[] = {
    GDR_MAINPOPUP,
    GDR_STORE,
    GDR_STORAGEROOM,
    GDR_EXCHANGE,
    GDR_WORLDMAP,
    GDR_COS_WND,
    GDR_GAMEGUIDE,
    GDR_ALCHEMYBOX,
    GDR_AUTO_POTION,
    GDR_EXT_QUICK_SLOT,
    // Our windows
    GDR_FLORIAN0,
};

HOOK_ORIGINAL_MEMBER(0x0079D5B0, &CGInterface::ToggleActionWnd);
void CGInterface::ToggleActionWnd() {
    // If MainPopup is visible and page 'action' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_ACTION)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
    } else {
        m_mainPopup->ShowGWnd(true);
        m_mainPopup->ShowSubPage(GDR_ACTION);
        FUN_0079a7e0(m_mainPopup);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079B0B0, &CGInterface::ToggleApprenticeshipWnd);
void CGInterface::ToggleApprenticeshipWnd() {
    // If MainPopup is visible and page 'apprenticeship' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_APPRENTICESHIP)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
        g_CGlobalDataManager->FUN_008cbac0(GDR_APPRENTICESHIP);
    } else {
        m_mainPopup->ShowGWnd(true);
        if (!m_mainPopup->GetApprenticeShip()->IsVisible()) {
            m_mainPopup->ShowSubPage(GDR_APPRENTICESHIP);
        }
        FUN_0079a7e0(m_mainPopup);
        g_CGlobalDataManager->FUN_008cbac0(GDR_APPRENTICESHIP);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079ACE0, &CGInterface::TogglePlayerInfoWnd);
void CGInterface::TogglePlayerInfoWnd() {
    // If MainPopup is visible and page 'playerinfo' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_PLAYERINFO)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
    } else {
        m_mainPopup->ShowGWnd(true);
        if (!m_mainPopup->GetPlayerInfo()->IsVisible()) {
            m_mainPopup->ShowSubPage(GDR_PLAYERINFO);
        }
        FUN_0079a7e0(m_mainPopup);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079f690, &CGInterface::ToggleGameGuide);
void CGInterface::ToggleGameGuide() {
    if (m_IRM.GetResObj(GDR_GAMEGUIDE, true) == NULL) {
        ToggleGGuide(true);
        FUN_0079a7e0(m_IRM.GetResObj(GDR_GAMEGUIDE, true));
        return;
    }

    ToggleGGuide(false);
    FUN_0079a7e0(m_IRM.GetResObj(GDR_SYSTEM, true));
}

void CGInterface::ToggleGGuide(bool bVisible) {
    CIFWnd *pGGuide = m_IRM.GetResObj(GDR_GAMEGUIDE, true);
    if (!bVisible) {
        if (pGGuide == NULL)
            return;

        SaveGWndPostion(GDR_GAMEGUIDE, pGGuide->GetPos());

        pGGuide->ShowGWnd(false);
        pGGuide->GetParentControl()->SetFocus_MAYBE();
        FUN_0079a7e0(pGGuide);

        m_IRM.DeleteCreatedSection("GameGuide");
        return;
    }

    if (pGGuide != NULL) {
        pGGuide->ShowGWnd(true);
        return;
    }

    m_IRM.CreateInterfaceSection("GameGuide", this);

    pGGuide = m_IRM.GetResObj(GDR_GAMEGUIDE, true);
    pGGuide->MoveGWnd(230, 80);// why do i saving the pos if im setting it by my self? lol
    pGGuide->ShowGWnd(true);
    FUN_0079a7e0(pGGuide);
}

HOOK_ORIGINAL_MEMBER(0x0079B020, &CGInterface::ToggleInventoryWnd);
void CGInterface::ToggleInventoryWnd() {
    // If MainPopup is visible and page 'inventory' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_INVENTORY)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
    } else {
        m_mainPopup->ShowGWnd(true);
        if (!m_mainPopup->GetInventory()->IsVisible()) {
            m_mainPopup->ShowSubPage(GDR_INVENTORY);
        }
        FUN_0079a7e0(m_mainPopup);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079ad70, &CGInterface::ToggleEquipmentWnd);
void CGInterface::ToggleEquipmentWnd() {
    // If MainPopup is visible and page 'equipment' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_INVENTORY)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
    } else {
        m_mainPopup->ShowGWnd(true);
        if (!m_mainPopup->GetEquipment()->IsVisible()) {
            m_mainPopup->ShowSubPage(GDR_INVENTORY);
        }
        FUN_0079a7e0(m_mainPopup);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079AE90, &CGInterface::TogglePartyWnd);
void CGInterface::TogglePartyWnd() {
    // If MainPopup is visible and page 'party' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_PARTY)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
        g_CGlobalDataManager->FUN_008cbac0(GDR_PARTY);
    } else {
        m_mainPopup->ShowGWnd(true);
        if (!m_mainPopup->GetParty()->IsVisible()) {
            m_mainPopup->ShowSubPage(GDR_PARTY);
        }
        FUN_0079a7e0(m_mainPopup);
        g_CGlobalDataManager->FUN_008cbac0(GDR_PARTY);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079AE00, &CGInterface::ToggleSkillWnd);
void CGInterface::ToggleSkillWnd() {
    // If MainPopup is visible and page 'skill' is active
    if (m_mainPopup->IsVisible() && m_mainPopup->IsSubPageActive(GDR_SKILL)) {
        m_mainPopup->ShowGWnd(false);
        FUN_0079a7e0(m_mainPopup);
    } else {
        m_mainPopup->ShowGWnd(true);
        if (!m_mainPopup->GetSkill()->IsVisible()) {
            m_mainPopup->ShowSubPage(GDR_SKILL);
        }
        FUN_0079a7e0(m_mainPopup);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079af20, &CGInterface::ShowInventoryWnd);
void CGInterface::ShowInventoryWnd() {
    m_mainPopup->ShowGWnd(true);
    if (!m_mainPopup->GetInventory()->IsVisible()) {
        m_mainPopup->ShowSubPage(GDR_INVENTORY);
    }
    FUN_0079a7e0(m_mainPopup);
}

HOOK_ORIGINAL_MEMBER(0x0079af70, &CGInterface::ShowApprenticeshipWnd);
void CGInterface::ShowApprenticeshipWnd() {
    m_mainPopup->ShowGWnd(true);
    if (!m_mainPopup->GetApprenticeShip()->IsVisible()) {
        m_mainPopup->ShowSubPage(GDR_APPRENTICESHIP);
    }
    FUN_0079a7e0(m_mainPopup);
}

void CGInterface::ToggleMainPopup() {
    m_mainPopup->ShowGWnd(!m_mainPopup->IsVisible());
    FUN_0079a7e0(m_mainPopup);
}

HOOK_ORIGINAL_MEMBER(0x0079abe0, &CGInterface::ToggleWorldMap);
void CGInterface::ToggleWorldMap() {
    // Don't we have the window?
    if (GetWorldMap() == NULL) {
        g_sNewInterfaceMgr.InstantiateDimensional("res_ui\\worldmapguide.2dt", this, false);
        SetOnMiddleOfScreen(GDR_WORLDMAP);
        return;
        // Window is created, so let's un/visible it
    } else {
        g_sNewInterfaceMgr.GetInterfaceObj(GDR_WORLDMAP)->OnGWndShow();
        FUN_0079ab80(g_sNewInterfaceMgr.GetInterfaceObj(GDR_WORLDMAP));
        SetOnMiddleOfScreen(GDR_WORLDMAP);
    }
}

HOOK_ORIGINAL_MEMBER(0x0079c750, &CGInterface::ToggleAutoPotion);
void CGInterface::ToggleAutoPotion(bool bVisible) {
    CIFWnd *pAutoPotion = m_IRM.GetResObj(GDR_AUTO_POTION, true);

    // If close it up
    if (!bVisible) {
        // If the window is already not there
        if (pAutoPotion == NULL)
            return;

        SaveGWndPostion(GDR_AUTO_POTION, pAutoPotion->GetPos());

        pAutoPotion->ShowGWnd(false);
        m_IRM.DeleteCreatedSection("AutoPotion");
        return;
        // Open it up
    } else {
        // Do we have it already opened before?
        if (pAutoPotion != NULL)
            return;

        m_IRM.CreateInterfaceSection("AutoPotion", this);

        pAutoPotion = m_IRM.GetResObj(GDR_AUTO_POTION, true);
        SaveGWndPosOnMiddleOfScreen(GDR_AUTO_POTION, NULL);
        pAutoPotion->ShowGWnd(true);

        FUN_0079a7e0(pAutoPotion);
    }
}

void CGInterface::CreateFlorian0Event() {
    GetAlarmManager()->CreateGuideIcon(GDR_FLORIAN0_GUIDE);
}

CAlramGuideMgrWnd *CGInterface::GetAlarmManager() {
    return m_IRM.GetResObj<CAlramGuideMgrWnd>(GDR_ALRAM_GUIDE_MGR_WND, 1);
}

CIF_NPCWindow *CGInterface::Get_GDR_NPCWINDOW() {
    return (CIF_NPCWindow *) this->m_IRM.GetResObj(GDR_NPCWINDOW, 1);
}

HOOK_ORIGINAL_MEMBER(0x00798D00, &CGInterface::GetMainPopup);
CIFMainPopup *CGInterface::GetMainPopup() {
    return (CIFMainPopup *) this->m_IRM.GetResObj(GDR_MAINPOPUP, 1);
}

CIFStorageRoom *CGInterface::Get_GDR_STORAGEROOM() {
    return (CIFStorageRoom *) this->m_IRM.GetResObj(GDR_STORAGEROOM, 1);
}

CIFStorageRoom *CGInterface::Get_GDR_GUILDSTORAGEROOM() {
    return (CIFStorageRoom *) this->m_IRM.GetResObj(GDR_GUILDSTORAGEROOM, 1);
}

void CGInterface::ToggleQuestNew() {
    reinterpret_cast<void(__thiscall *)(CGInterface *)>(0x007990e0)(this);
}

CNIFCommunityWnd *CGInterface::GetCommunityWnd() {
    return g_sNewInterfaceMgr.GetInterfaceObj<CNIFGuildWnd>(GDR_GUILD)->m_pCommunity;
}

CIFSystemMessage *CGInterface::GetSystemMessageView() {
    return m_IRM.GetResObj<CIFSystemMessage>(GDR_SYSTEM_MESSAGE_VIEW, 1);
}

void CGInterface::FUN_0079a7e0(CGWndBase *pGWnd) const {
    reinterpret_cast<void(__thiscall *)(const CGInterface *, CGWndBase *)>(0x0079a7e0)(this, pGWnd);
}

void CGInterface::FUN_0079ab80(CNIFWnd *pWnd) {
    reinterpret_cast<void(__thiscall *)(const CGInterface *, CNIFWnd *)>(0x0079ab80)(this, pWnd);
}

void CGInterface::FUN_0079b8a0(undefined1 a1, undefined4 a2) {
    reinterpret_cast<void(__thiscall *)(CGInterface *, undefined1, undefined4)>(0x0079b8a0)(this, a1, a2);
}

CNIFUnderMenuBar *CGInterface::GetUnderMenuBar() {
    return reinterpret_cast<CNIFUnderMenuBar *(__thiscall *) (CGInterface *)>(0x007994e0)(this);
}

CIFExtQuickSlot *CGInterface::GetExtQuickSlot() {
    return m_IRM.GetResObj<CIFExtQuickSlot>(GDR_EXT_QUICK_SLOT, 1);
}

void CGInterface::FUN_00777a70(undefined4 param_1, undefined4 param_2) {
    reinterpret_cast<void(__thiscall *)(CGInterface *, undefined4, undefined4)>(0x00777a70)(this, param_1, param_2);
}

CNIFWorldMap *CGInterface::GetWorldMap() {
    return g_sNewInterfaceMgr.GetInterfaceObj<CNIFWorldMap>(GDR_WORLDMAP);
}

void CGInterface::SetOnMiddleOfScreen(DWORD dwNInterfaceID) {
    CNIFWnd *pWnd = g_sNewInterfaceMgr.GetInterfaceObj(dwNInterfaceID);
    if (pWnd == NULL)
        return;

    wnd_pos sNewPos = {(theApp.GetClientDimensionStuff().width / 2) - (pWnd->GetBounds().size.width / 2),
                       (theApp.GetClientDimensionStuff().height / 2) - (pWnd->GetBounds().size.height / 2)};
    pWnd->MoveGWnd2(sNewPos);
}

void CGInterface::SaveGWndPostion(DWORD dwID, CGWnd::wnd_pos sWndPos) {
    mapWndPos::iterator it = m_mapWindowPos.find(dwID);
    if (it == m_mapWindowPos.end()) {
        m_mapWindowPos.insert(mapWndPos::value_type(dwID, sWndPos));
        return;
    }

    it->second = sWndPos;
}

void CGInterface::SaveGWndPosOnMiddleOfScreen(DWORD dwID, CGWnd *pParent) {
    CGWnd *pParentWnd = pParent;
    if (pParentWnd == NULL) {
        pParentWnd = g_pCGInterface;
    }

    CIFWnd *pWnd = (CIFWnd *) pParentWnd->GetChildControl(dwID);
    if (pWnd == NULL) {
        return;
    }

    wnd_pos sNewPos = {(theApp.GetClientDimensionStuff().width / 2) - (pWnd->GetBounds().size.width / 2),
                       (theApp.GetClientDimensionStuff().height / 2) - (pWnd->GetBounds().size.height / 2)};
    pWnd->MoveGWnd2(sNewPos);

    SaveGWndPostion(dwID, sNewPos);
}

bool CGInterface::GetSavedGWndPostion(DWORD dwID, OUT CGWnd::wnd_pos &sPos, BOOL bPosOnScreen) {
    mapWndPos::iterator it = m_mapWindowPos.find(dwID);
    if (it == m_mapWindowPos.end()) {
        return false;
    }

    sPos = (*it).second;

    if (bPosOnScreen) {
        CIFWnd *pWnd = m_IRM.GetResObj(dwID, true);
        if (pWnd != NULL) {
            int nResolutionWidth = theApp.GetClientDimensionStuff().width;
            int nResolutionHeight = theApp.GetClientDimensionStuff().height;
            wnd_size sWndSize = pWnd->GetSize();

            if (nResolutionWidth < (sPos.x + sWndSize.width)) {
                sPos.x = (nResolutionWidth - sWndSize.width);
            }

            if (nResolutionHeight < (sPos.y + sWndSize.height)) {
                sPos.y = (nResolutionHeight - sWndSize.height);
            }
        }
    }
    return true;
}

void CGInterface::SaveGWndsPostionData() {
    CHAR szBuffer[0x200];// PATH_MAX define iguess?
    sprintf_s(szBuffer, "%s\\Setting\\wndpos.dat", theApp.GetWorkingDir());

    FILE *f = fopen(szBuffer, "wb+");
    if (f == NULL) {
        return;
    }

    int nUnknown = 3;
    fwrite(&nUnknown, sizeof(int), 1, f);

    int nCount = size(nGWndSavePostionsID);
    fwrite(&nCount, sizeof(int), 1, f);

    int nResolutionWidth = theApp.GetClientDimensionStuff().width;
    int nResolutionHeight = theApp.GetClientDimensionStuff().height;
    fwrite(&nResolutionWidth, sizeof(int), 1, f);
    fwrite(&nResolutionHeight, sizeof(int), 1, f);

    wnd_pos sPosData;

    //=======================================================
    for (int p = 0; p < size(nGWndSavePostionsID); ++p) {
        CIFWnd* pWnd = m_IRM.GetResObj(nGWndSavePostionsID[p], true);
        if (pWnd != NULL) {
            // 1. Store the temporary return value in a local variable
            wnd_pos tempPos = pWnd->GetPos();

            // 2. Safely take the address of the local variable
            memcpy(&sPosData, &tempPos, sizeof(wnd_pos));
        }
        else {
            GetSavedGWndPostion(nGWndSavePostionsID[p], sPosData, TRUE);
        }
        fwrite(&sPosData.x, sizeof(int), 1, f);
        fwrite(&sPosData.y, sizeof(int), 1, f);
    }
    //=======================================================

    fclose(f);
}

void CGInterface::LoadGWndsPostionData() {
    CHAR szBuffer[0x200];// PATH_MAX define iguess?
    sprintf_s(szBuffer, "%s\\Setting\\wndpos.dat", theApp.GetWorkingDir());

    FILE *f = fopen(szBuffer, "wb+");
    if (f == NULL) {
        return;
    }

    int nUnknwon;
    fread(&nUnknwon, sizeof(int), 1, f);

    if (nUnknwon != 3) {
        fclose(f);
        SaveGWndsPostionData();
        return;
    }

    int nCount;
    int nResolutionWidth;
    int nResolutionHeight;

    fread(&nCount, sizeof(int), 1, f);
    fread(&nResolutionWidth, sizeof(int), 1, f);
    fread(&nResolutionHeight, sizeof(int), 1, f);

    if (nResolutionWidth != theApp.GetClientDimensionStuff().width ||
        nResolutionHeight != theApp.GetClientDimensionStuff().height) {
        fclose(f);
        SaveGWndsPostionData();
        return;
    }

    wnd_pos sPosData;

    //=======================================================
    for (int p = 0; p < size(nGWndSavePostionsID); ++p) {
        fread(&sPosData.x, sizeof(int), 1, f);
        fread(&sPosData.y, sizeof(int), 1, f);

        CIFWnd *pWnd = m_IRM.GetResObj(nGWndSavePostionsID[p], true);
        if (pWnd != NULL) {
            pWnd->MoveGWnd(sPosData.x, sPosData.y);
        }
        SaveGWndPostion(nGWndSavePostionsID[p], sPosData);
    }
    //=======================================================

    fclose(f);
}