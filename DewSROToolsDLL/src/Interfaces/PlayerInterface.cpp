#include "PlayerInterface.h"

std::n_wstring PlayerInterface::GetCharacterName() const {
    if (!g_pMyPlayerObj) return {};
    return g_pMyPlayerObj->GetCharName();
}

int PlayerInterface::GetCurrentLevel() const {
    if (!g_pMyPlayerObj) return 0;
    return g_pMyPlayerObj->GetCurrentLevel();
}

short PlayerInterface::GetSTR() const {
    if (!g_pMyPlayerObj) return 0;
    return g_pMyPlayerObj->GetStrength();
}

short PlayerInterface::GetINT() const {
    if (!g_pMyPlayerObj) return 0;
    return g_pMyPlayerObj->GetIntelligence();
}

short PlayerInterface::GetStatPoints() const {
    if (!g_pMyPlayerObj) return 0;
    return g_pMyPlayerObj->GetStatPointAvailable();
}

std::n_wstring PlayerInterface::GetJobAliasName() const {
    if (!g_pMyPlayerObj) return {};
    return g_pMyPlayerObj->GetJobAlias();
}

int PlayerInterface::GetCurrentJobEXP() const {
    if (!g_pMyPlayerObj) return 0;
    return g_pMyPlayerObj->GetCurrentJobExperiencePoints();
}

bool PlayerInterface::IsGM() const {
    if (!g_pMyPlayerObj) return false;
    return g_pMyPlayerObj->IsGameMaster();
}

