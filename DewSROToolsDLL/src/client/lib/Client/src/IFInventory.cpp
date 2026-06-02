#include "IFInventory.h"

CSOItem *CIFInventory::GetItemBySlot(int slotId) {
    return reinterpret_cast<CSOItem *(__thiscall *)(CIFInventory *, int)>(0x6b3e50)(this, slotId);
}

CIFSlotWithHelp* CIFInventory::GetItemSlotBySlotId(int slotId) {
    return reinterpret_cast<CIFSlotWithHelp*(__thiscall*)(CIFInventory*, int)>(0x006b3ea0)(this, slotId);
}
