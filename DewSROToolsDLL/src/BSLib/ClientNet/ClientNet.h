#pragma once

#include "IClientNet.h"

#define g_CurrentIfUnderCursor (*(CGWndBase**)0x0110F608)
#define g_Region (*(uregion*)0xEEF68C)

#define SendMsg(x) reinterpret_cast<void (__cdecl *)(CMsgStreamBuffer&)>(0x008418D0)(x)

class CClientNet : public IClientNet
{
public:
    static CClientNet* get()
    {
        return *reinterpret_cast<CClientNet**>(0x0115B0C0);
    }
};

