// Credits: @florian0 https://www.elitepvpers.com/forum/sro-pserver-guides-releases/4256375-source-fix-old-exp-bar-writing-code.html

#include "NIFUnderMenuBar.h"

#include "ICPlayer.h"

#include "BSLib/Debug.h"
#include "Data\LevelData.h"
#include "GInterface.h"
#include "GlobalDataManager.h"
#include <stdio.h>

#include <cmath>

void CNIFUnderMenuBar::Update() {
    // Skip, if player object is not loaded yet (KEEP!)
    if (!g_pMyPlayerObj) {
        return;
    }

    // Some other call that is important (KEEP!)
    ((void (__thiscall *)(CNIFUnderMenuBar *)) 0x46CD80)(this);

    // Check if control is visible
    if (!((char (__thiscall *)(CNIFUnderMenuBar *)) 0x00B8F530)(this)) {
        return;
    }

    // Retrieve LevelData for current Level
    // (this is one line of Media\server_dep\silkroad\textdata\leveldata.txt)
    CLevelData *data = g_CGlobalDataManager->m_levelDataMap[g_pMyPlayerObj->m_btLevel];

    // Don't continue if level-data is invalid
    if (data == NULL) {
        return;
    }

    // Calculate EXP as percentage value
    const SLevelData &sData = data->GetData();
    double exp_percentage = g_pMyPlayerObj->m_i64CurrentExp * 100.0 / sData.m_expC;

    // Limit maximum percentage to 99.99%
    if (exp_percentage > 99.99) {
        exp_percentage = 99.99;
    }

    // Calculate the number of bars that are full
    int barnum = static_cast<int>(floor(exp_percentage / 10.0));


    for (int i = 0; i < 10; i++) {

        // Fill or empty bars
        if (barnum <= i) {
            gauges[i]->background_value = 0.0;
            gauges[i]->foreground_value = 0.0;
        } else {
            gauges[i]->background_value = 1.0;
            gauges[i]->foreground_value = 1.0;
        }
    }

    // Fill the bar that is neither full or empty with the remaining percentage
    float exp_remain = static_cast<float>((exp_percentage - (barnum * 10.0)) / 10.0);

    gauges[barnum]->background_value = exp_remain;
    gauges[barnum]->foreground_value = exp_remain;

    // Assign more texts
    this->lbl_level->SetTextFormatted(L"Level: %d", g_pMyPlayerObj->m_i64CurrentExp);
    this->lbl_percentage->SetTextFormatted(L"%.2lf %%", exp_percentage);

    // SkillPoints
    this->lbl_spcount->SetTextFormatted(L"%d", g_pMyPlayerObj->m_nSkillPoint);
    this->gauge_skillexp->background_value = g_pMyPlayerObj->m_nSkillPoint_Progress / 400.0f;
    this->gauge_skillexp->foreground_value = g_pMyPlayerObj->m_nSkillPoint_Progress / 400.0f;

    // You can also draw text directly at the gauge. It will be centered automatically
    // this->gauge_skillexp->SetTextFormatted(L"%d", g_CICPlayer->skill_exp);


    this->lbl_exp_bar_scaler->SetText(L""); // Prescaler is disabled

    // This label is right in the middle of the EXP-Bar
    //this->lbl_360->SetText(L"lbl_360");
}

bool CNIFUnderMenuBar::IsPotionOrPillInQuickslot(int slot) {
    return reinterpret_cast<bool(__thiscall*)(CNIFUnderMenuBar *, int)>(0x0060b1d0)(this, slot);
}

void CNIFUnderMenuBar::UseSlot(int slot) {
    CIFInventory *pInventory = g_pCGInterface->GetMainPopup()->GetInventory();
    CIFEquipment *pEquipment = g_pCGInterface->GetMainPopup()->GetEquipment();

    CIFSlotWithHelp *relatedSlot = m_pMySlots[slot]->m_pSlot;

    if (!relatedSlot->GetBGFilename().empty()) {

        switch (relatedSlot->GetItemSourceParentWindowId()) {
            case 70: {
                CIFSlotWithHelp *pSlot = this->m_pMySlots[slot]->m_pSlot;
                if (pSlot->GetItem() == NULL) {
                    return;
                }

                BS_WARNING("CNIFUnderMenuBar::UseSlot(%d) %p", slot, relatedSlot);

                CSOItem *item = pInventory->GetItemBySlot(pSlot->GetInventorySlotIndex());
                if ((item->m_blValid) &&
                    (item->IsSameItemType(&m_pMySlots[slot]->m_pSlot->GetMyItem())) &&
                    (CGWnd::GetDraggedGWnd() == NULL)) {

                    if (item->GetItemData()->IsItemEtc()) {
                        if (!item->GetItemData()->IsAmmo()) {
                            pInventory->GetItemSlotBySlotId(m_pMySlots[slot]->m_pSlot->GetInventorySlotIndex())->UseItem();
                            return;
                        }
                    }

                    if (item->GetItemData()->IsItemCOS()) {
                        pInventory->GetItemSlotBySlotId(m_pMySlots[slot]->m_pSlot->GetInventorySlotIndex())->UseItem();
                        return;
                    }

                    CIFSlotWithHelp *inventorySlot = pInventory->GetItemSlotBySlotId(m_pMySlots[slot]->m_pSlot->GetInventorySlotIndex());
                    if (g_pCGInterface->m_field_4fc.empty()) {
                        CGWnd::SetDraggedGWnd(inventorySlot);
                        pEquipment->Func_28(inventorySlot, 0, 0);
                        CGWnd::SetDraggedGWnd(NULL);
                        return;
                    }
                }
            }
                break;

            default:
                reinterpret_cast<void(__thiscall *)(CNIFUnderMenuBar *, int)>(0x0060b850)(this, slot);
                break;
        }

    }
};
