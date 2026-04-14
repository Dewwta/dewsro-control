#include "AlramGuideMgrWnd.h"

#include "IFConfirmReputationGuide.h"
#include "IFEventGuide.h"
#include "IFQuestInfoGuide.h"
#include "IFLetterAlarmGuide.h"
#include "IFServerEventGuide.h"
#include "IFOpenMarketAlramGuide.h"
#include "IFEventGuideSecond.h"
#include "IFflorian0Guide.h"

#include "GInterface.h"

GFX_IMPLEMENT_DYNAMIC_EXISTING(CAlramGuideMgrWnd, 0x00ee99a8)

#define ALRAM_GUIDE_ICON_SIZE 42
#define ALRAM_GUIDE_MAX_ICONS_LINE 4

CAlramGuideMgrWnd::CAlramGuideMgrWnd() {
    m_btGuidesCount = 0;
}

void CAlramGuideMgrWnd::UpdateGuidesPos() {
    wnd_pos posMgr = GetPos();

    listGUIDES::iterator it = m_listGuides.begin();
    for (DWORD dwPosIndex = 1; it != m_listGuides.end(); ++it, dwPosIndex++) {
        posMgr.x -= ALRAM_GUIDE_ICON_SIZE;
        CIFWnd *pGuideIcon = (*it);

        pGuideIcon->MoveGWnd(posMgr.x, posMgr.y);
        pGuideIcon->ShowGWnd(true);

        if (m_btGuidesCount != 0) {
            // if we reached our max icons per line
            if (!(dwPosIndex % ALRAM_GUIDE_MAX_ICONS_LINE)) {
                posMgr.y += (ALRAM_GUIDE_ICON_SIZE + 2);
                posMgr.x = GetPos().x;
            }
        }
    }
}

void CAlramGuideMgrWnd::RemoveAllGuides() {
    listGUIDES::iterator it = m_listGuides.begin();
    for (; it != m_listGuides.end(); ++it)
        (*it)->EraseWindowObj();
}

bool CAlramGuideMgrWnd::IsAvailableGuide(DWORD dwID) {
    listGUIDES::iterator it = m_listGuides.begin();
    for (; it != m_listGuides.end(); ++it)
        if ((*it)->UniqueID() == dwID)
            return true;
    return false;
}

CIFWnd *CAlramGuideMgrWnd::GetGuide(DWORD dwID) {
    listGUIDES::iterator it = m_listGuides.begin();
    for (; it != m_listGuides.end(); ++it)
        if ((*it)->UniqueID() == dwID)
            return (*it);
    return NULL;
}

CAlramGuideMgrWnd::~CAlramGuideMgrWnd() {
    RemoveAllGuides();
}

CIFWnd *CAlramGuideMgrWnd::CreateGuideIcon(int nWndID) {
    // Try to find the element in the list
    listGUIDES::iterator it = m_listGuides.begin();
    for (; it != m_listGuides.end(); ++it) {
        if ((*it)->UniqueID() == nWndID) {
            return *it;
        }
    }

    // List did not contain the element, try to create it
    RECT rect = {0,
                 0,
                 ALRAM_GUIDE_ICON_SIZE, 
                 ALRAM_GUIDE_ICON_SIZE};

    CIFWnd* pObj = 0;

    switch(nWndID) {
        case GDR_CONFIRMREPUTATION_GUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFConfirmReputationGuide), rect, GDR_CONFIRMREPUTATION_GUIDE, 0);
            break;

        case GDR_EVENTGUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFEventGuide), rect, GDR_EVENTGUIDE, 0);
            break;

        case GDR_QUESTINFO_GUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFQuestInfoGuide), rect, GDR_QUESTINFO_GUIDE, 0);
            break;

        case GDR_LETTER_ALARM_GUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFLetterAlarmGuide), rect, GDR_LETTER_ALARM_GUIDE, 0);
            break;

        case GDR_SERVEREVENT_GUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFServerEventGuide), rect, GDR_SERVEREVENT_GUIDE, 0);
            break;

        case GDR_OPENMARKETALRAM_GUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFOpenMarketAlramGuide), rect, GDR_OPENMARKETALRAM_GUIDE, 0);
            break;

        case GDR_EVENTGUIDE_SECOND:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFEventGuideSecond), rect, GDR_EVENTGUIDE_SECOND, 0);
            break;

        case GDR_FLORIAN0_GUIDE:
            pObj = (CIFWnd *) CreateInstance(this, GFX_RUNTIME_CLASS(CIFDootGuide), rect, GDR_FLORIAN0_GUIDE, 0);
            break;

        default:
            assert(false);
    }

    m_listGuides.push_back(pObj);
    m_btGuidesCount++;

    UpdateGuidesPos();

    return pObj;
}

void CAlramGuideMgrWnd::RemoveGuide(DWORD dwID) {
    listGUIDES::iterator it = m_listGuides.begin();
    for (; it != m_listGuides.end(); ++it) {
        if ((*it)->UniqueID() != dwID)
            continue;

        (*it)->EraseWindowObj();
        m_listGuides.erase(it);
        break;
    }
    UpdateGuidesPos();
}
