#include "AutoPotion.h"

#include "BSLib/Debug.h"
#include "GInterface.h"
#include "ICPlayer.h"
#include "SRIFLib/NIFUnderMenuBar.h"
#include "SRIFLib/NInterfaceResource.h"


void CAutoPotion::UsePotion(unsigned char byType) {
    BS_ASSERT(byType < AUTO_POTION_COUNT);

    if (this->m_data_in_array[byType].InitState == InitComplete) {
        CNIFUnderMenuBar *pMenuBar = g_sNewInterfaceMgr.GetInterfaceObj<CNIFUnderMenuBar>(0xaf);

        if (pMenuBar->IsPotionOrPillInQuickslot(m_data_in_array[byType].BeltAndSlotCombined)) {
            pMenuBar->UseSlot(m_data_in_array[byType].BeltAndSlotCombined);
        }
    }
}

void CAutoPotion::UseHPPotion() {
    if (g_pMyPlayerObj == NULL) {
        return;
    }

    if (!m_data_in_array[HP_POTION].bIsEnabled) {
        return;
    }

    if (((g_pMyPlayerObj->m_characterState.stateH & 1) == 0) ||
        (this->m_data_in_array[HP_POTION].ThresholdValue < g_pMyPlayerObj->GetCurrentHp())) {
        this->m_data_in_array[HP_POTION].bTimerRunning = false;
        g_pCGInterface->KillTimer(CGInterface::TIMER_AUTOPOTION_HP);
    } else {
        if ((g_pMyPlayerObj->m_abnormalState & ABNORMAL_STATE_ZOMBIE) == 0) {
            UsePotion(HP_POTION);
        }
        if (!this->m_data_in_array[HP_POTION].bTimerRunning) {
            this->m_data_in_array[HP_POTION].bTimerRunning = true;
            int interval = 500;
            if (m_bUsePotionDelay) {
                interval = GetPotionDelay();
            }
            g_pCGInterface->StartTimer(CGInterface::TIMER_AUTOPOTION_HP, interval);
            return;
        }
    }
}

void CAutoPotion::UseMPPotion() {
    if (g_pMyPlayerObj == NULL) {
        return;
    }

    if (!m_data_in_array[MP_POTION].bIsEnabled) {
        return;
    }

    if (((g_pMyPlayerObj->m_characterState.stateH & 1) == 0) ||
        (this->m_data_in_array[MP_POTION].ThresholdValue < g_pMyPlayerObj->GetCurrentMp())) {
        this->m_data_in_array[MP_POTION].bTimerRunning = false;
        g_pCGInterface->KillTimer(CGInterface::TIMER_AUTOPOTION_MP);
    } else {
        UsePotion(MP_POTION);

        if (!this->m_data_in_array[MP_POTION].bTimerRunning) {
            this->m_data_in_array[MP_POTION].bTimerRunning = true;
            int interval = 500;
            if (m_bUsePotionDelay) {
                interval = GetPotionDelay();
            }
            g_pCGInterface->StartTimer(CGInterface::TIMER_AUTOPOTION_MP, interval);
            return;
        }
    }
}

void CAutoPotion::UseCurePill() {
    if (g_pMyPlayerObj == NULL) {
        return;
    }

    if (!m_data_in_array[CURE_PILL].bIsEnabled) {
        return;
    }

    if (((g_pMyPlayerObj->m_characterState.stateH & 1) == 0) || (g_pMyPlayerObj->m_abnormalState == 0)) {
        m_data_in_array[CURE_PILL].bTimerRunning = false;
        g_pCGInterface->KillTimer(CGInterface::TIMER_AUTOPOTION_CURE_PILL);
    } else {
        if ((g_pMyPlayerObj->m_abnormalState & 0x4000) == 0) {
            UsePotion(CURE_PILL);
        }

        if (!m_data_in_array[CURE_PILL].bTimerRunning) {
            m_data_in_array[CURE_PILL].bTimerRunning = true;
            int interval = 500;
            if (m_bUsePotionDelay) {
                interval = GetPotionDelay();
            }
            g_pCGInterface->StartTimer(CGInterface::TIMER_AUTOPOTION_CURE_PILL, interval);
        }
    }
}

int CAutoPotion::GetPotionDelay() const {
    return m_nUseDelay;
}
