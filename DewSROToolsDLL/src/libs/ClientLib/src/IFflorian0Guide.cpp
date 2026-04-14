#include "IFflorian0Guide.h"

#include "GEffSoundBody.h"
#include "GInterface.h"

#include <BSLib/Debug.h>

GFX_IMPLEMENT_DYNCREATE(CIFDootGuide, CIFDecoratedStatic);

bool CIFDootGuide::OnCreate(long ln) {
    BS_DEBUG_LOW("> " __FUNCTION__ "(%d)", ln);
    CIFDecoratedStatic::OnCreate(ln);

    TB_Func_13("icon\\etc\\webgacha2_1.ddj", 0, 0);
    sub_634470("icon\\etc\\webgacha2_2.ddj");

    set_N00009BD4(2);
    set_N00009BD3(500);

    CGEffSoundBody::get()->PlaySound(L"snd_quest");

    return true;
}

int CIFDootGuide::OnMouseLeftUp(int a1, int x, int y) {
    BS_DEBUG_LOW("> " __FUNCTION__ "(%d, %d, %d)", a1, x, y);

    CGEffSoundBody::get()->PlaySound(L"snd_quest");
    g_pCGInterface->m_IRM.GetResObj(GDR_FLORIAN0, 1)->ShowGWnd(true);

    return 0;
}

void CIFDootGuide::OnCIFReady() {
    BS_DEBUG_LOW("> " __FUNCTION__);

    CIFDecoratedStatic::OnCIFReady();
    sub_633990();
}
