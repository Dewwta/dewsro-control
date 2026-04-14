#pragma once

#include "IFDecoratedStatic.h"

class CIFDootGuide : public CIFDecoratedStatic {
    GFX_DECLARE_DYNCREATE(CIFDootGuide)

public:
    bool OnCreate(long ln) override;

    int OnMouseLeftUp(int a1, int x, int y) override;

    void OnCIFReady() override;

};
