#pragma once

#include "NIFMainFrame.h"

class CNIFWorldMap : public CNIFMainFrame {
public:
private:
    char pad_0798[0x229ec - 0x0798];
private:
BEGIN_FIXTURE()
        ENSURE_SIZE(0x229ec) // dafug???!!! ha?!!
    END_FIXTURE()
    
    RUN_FIXTURE(CNIFWorldMap)
};
