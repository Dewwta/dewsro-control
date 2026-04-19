#include "RewardWindow.h"
#include "imgui.h"
#include "../net/DllBridge.h"
#include "../net/NetActions.h"

RewardWindow g_rewardWindow;

void RewardWindow::Render() {
    if (!isOpen) return;

    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_Always,
        ImVec2(0.5f, 0.5f));
    ImGui::SetNextWindowSize(ImVec2(600, 300), ImGuiCond_Always);
    ImGui::SetNextWindowBgAlpha(0.97f);

    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoMove |
        ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoCollapse |
        ImGuiWindowFlags_NoSavedSettings;

    char title[64];
    snprintf(title, sizeof(title), "Level %d Reward", level);

    ImGui::Begin(title, nullptr, flags);

    ImGui::Spacing();
    ImGui::TextColored(ImVec4(1.0f, 0.85f, 0.3f, 1.0f),
        "Congratulations! You reached level %d!", level);
    ImGui::TextDisabled("Choose one reward:");
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    const float cellSize = 110.0f;
    const float btnH = 80.0f;
    const int   perRow = 4;

    for (int i = 0; i < (int)options.size(); i++) {
        if (i % perRow != 0) ImGui::SameLine();

        ImGui::BeginGroup();

        IDirect3DTexture9* tex = GetIcon(options[i].icon);

        // draw icon or placeholder
        ImVec2 iconSize(64.0f, 64.0f);
        ImVec2 cellSize(110.0f, 90.0f);

        // center the icon horizontally in the cell
        float padX = (cellSize.x - iconSize.x) * 0.5f;
        ImGui::SetCursorPosX(ImGui::GetCursorPosX() + padX);

        ImVec2 iconPos = ImGui::GetCursorScreenPos();
        SealType seal = GetSealType(options[i].code);

        if (tex) {
            ImGui::Image((ImTextureID)tex, iconSize);
        }
        else {
            // grey placeholder box
            ImGui::GetWindowDrawList()->AddRectFilled(
                iconPos,
                ImVec2(iconPos.x + iconSize.x, iconPos.y + iconSize.y),
                IM_COL32(40, 50, 70, 255));
            ImGui::Dummy(iconSize);
        }

        
        if (seal != SealType::None)
            g_soxOverlay.Render(iconPos, iconSize);

        ImGui::SetCursorScreenPos(iconPos);
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0, 0, 0, 0));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(1, 1, 1, 0.08f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(1, 1, 1, 0.15f));

        char btnLabel[32];
        snprintf(btnLabel, sizeof(btnLabel), "##slot%d", i);
        bool clicked = ImGui::Button(btnLabel, iconSize);
        
        ImGui::PopStyleColor(3);

        if (ImGui::IsItemHovered()) {
            ImGui::BeginTooltip();
            if (options[i].plus > 0) {
                char nameBuf[256];
                snprintf(nameBuf, sizeof(nameBuf), "%s (+%d)",
                    options[i].name.empty() ? options[i].code.c_str() : options[i].name.c_str(),
                    options[i].plus);
                ImGui::Text("%s", nameBuf);
            }
            else {
                ImGui::Text("%s", options[i].name.empty()
                    ? options[i].code.c_str()
                    : options[i].name.c_str());
            }

            if (seal != SealType::None) {
                ImGui::Separator();
                const char* sealName =
                    seal == SealType::Star ? "Seal of Star" :
                    seal == SealType::Moon ? "Seal of Moon" :
                                             "Seal of Sun";

                ImVec4 sealColor =
                    seal == SealType::Star ? ImVec4(1.0f, 0.9f, 0.3f, 1.0f) :  // gold
                    seal == SealType::Moon ? ImVec4(0.5f, 0.7f, 1.0f, 1.0f) :  // blue
                    ImVec4(1.0f, 0.5f, 0.2f, 1.0f);   // orange

                ImGui::TextColored(sealColor, "%s", sealName);
            }

            if (options[i].qty > 1)
                ImGui::TextColored(ImVec4(0.8f, 0.8f, 0.4f, 1.0f), "x%d", options[i].qty);

            ImGui::EndTooltip();
        }

        if (clicked) {
            NetActions::SendRewardClaim(level, options[i].code, options[i].qty, options[i].plus);
            ImGui::EndGroup();
            Close();
            break;
        }

        // label below icon
        ImGui::PushTextWrapPos(ImGui::GetCursorPosX() + cellSize.x);
        ImGui::TextDisabled("%s", options[i].name.empty()
            ? options[i].code.c_str()
            : options[i].name.c_str());
        if (options[i].plus > 0)
            ImGui::TextColored(ImVec4(0.4f, 0.8f, 0.4f, 1.0f), "+%d", options[i].plus);
        if (options[i].qty > 1)
            ImGui::TextColored(ImVec4(0.8f, 0.8f, 0.4f, 1.0f), "x%d", options[i].qty);
        ImGui::PopTextWrapPos();

        ImGui::EndGroup();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // dismiss — in final version this would store as unclaimed instead
    if (ImGui::Button("Claim Later", ImVec2(-1, 28))) {
        Close();
    }

    ImGui::End();
}