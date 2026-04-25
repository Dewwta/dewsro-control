#include "AchievementWindow.h"
#include "imgui.h"
#include <algorithm>
#include <cmath>

AchievementWindow g_achWindow;

static constexpr float CELL_W = 95.0f;
static constexpr float CELL_GAP = 8.0f;
static constexpr float ICON_SZ = 48.0f;
static constexpr float BAR_H = 5.0f;
static constexpr float LABEL_H = 28.0f;
static constexpr float CELL_H = ICON_SZ + BAR_H + 6.0f + LABEL_H + 10.0f;
static constexpr float WIN_PAD = 12.0f;
static constexpr float DETAIL_H = 140.0f;
static constexpr float FONT_SCALE = 0.78f;

static void DrawAchIcon(ImDrawList* dl, ImVec2 tl, float sz, bool completed)
{
    ImVec2 br(tl.x + sz, tl.y + sz);
    ImU32 bgCol = completed ? IM_COL32(30, 70, 30, 220) : IM_COL32(25, 28, 45, 200);
    ImU32 borderCol = completed ? IM_COL32(80, 200, 80, 220) : IM_COL32(55, 60, 80, 180);
    dl->AddRectFilled(tl, br, bgCol, 6.0f);
    dl->AddRect(tl, br, borderCol, 6.0f, 0, completed ? 2.0f : 1.0f);

    if (completed)
    {
        float cx = tl.x + sz * 0.5f, cy = tl.y + sz * 0.5f;
        float s = sz * 0.22f;
        ImVec2 pts[3] = {
            { cx - s,        cy              },
            { cx - s * 0.2f, cy + s * 0.9f  },
            { cx + s,        cy - s * 0.7f  }
        };
        dl->AddPolyline(pts, 3, IM_COL32(80, 220, 80, 255), 0, 2.5f);
    }
    else
    {
        float lx = tl.x + sz * 0.35f, ly = tl.y + sz * 0.46f;
        float lw = sz * 0.30f, lh = sz * 0.26f;
        float arc = sz * 0.13f;
        dl->AddRectFilled({ lx, ly }, { lx + lw, ly + lh }, IM_COL32(120, 120, 140, 200), 2.0f);
        dl->AddCircle({ lx + lw * 0.5f, ly }, arc, IM_COL32(120, 120, 140, 200), 12, 2.0f);
    }
}

static void DrawBar(ImDrawList* dl, ImVec2 tl, float w, float frac,
    ImVec4 bg, ImVec4 fill)
{
    auto toU32 = [](ImVec4 c) {
        return IM_COL32((int)(c.x * 255), (int)(c.y * 255), (int)(c.z * 255), (int)(c.w * 255));
        };
    ImVec2 br(tl.x + w, tl.y + BAR_H);
    dl->AddRectFilled(tl, br, toU32(bg), 3.0f);
    if (frac > 0.0f)
        dl->AddRectFilled(tl, { tl.x + w * frac, br.y }, toU32(fill), 3.0f);
}

void AchievementWindow::Render()
{
    if (!isOpen) return;

    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_FirstUseEver, ImVec2(0.5f, 0.5f));
    ImGui::SetNextWindowSize(ImVec2(570.0f, 460.0f), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(300.0f, 200.0f), ImVec2(900.0f, 800.0f));
    ImGui::SetNextWindowBgAlpha(0.97f);

    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoCollapse |
        ImGuiWindowFlags_NoSavedSettings;

    int done = 0;
    for (auto& e : entries) if (e.completed) done++;
    char title[64];
    snprintf(title, sizeof(title), "Achievements (%d / %d)", done, (int)entries.size());

    if (!ImGui::Begin(title, &isOpen, flags)) { ImGui::End(); return; }

    float winW = ImGui::GetContentRegionAvail().x;
    int   cols = std::max(1, (int)((winW + CELL_GAP) / (CELL_W + CELL_GAP)));
    float actualCellW = (winW - (cols - 1) * CELL_GAP) / (float)cols;

    float gridH = ImGui::GetContentRegionAvail().y
        - (showDetail ? DETAIL_H + 8.0f : 0.0f);

    ImGui::BeginChild("##ach_grid", ImVec2(0, gridH), false,
        ImGuiWindowFlags_HorizontalScrollbar);
    ImDrawList* dl = ImGui::GetWindowDrawList();

    int col = 0;
    for (int i = 0; i < (int)entries.size(); i++)
    {
        const auto& e = entries[i];
        const bool  selected = (detailIndex == i);

        if (col > 0) ImGui::SameLine(0.0f, CELL_GAP);

        ImVec2 cellTL = ImGui::GetCursorScreenPos();
        float  cw = actualCellW;

        // Cell background
        ImU32 cellBg = selected ? IM_COL32(40, 55, 80, 220) : IM_COL32(20, 22, 35, 180);
        ImU32 cellBdr = selected ? IM_COL32(100, 160, 255, 220) : IM_COL32(45, 50, 70, 160);
        dl->AddRectFilled(cellTL, { cellTL.x + cw, cellTL.y + CELL_H }, cellBg, 5.0f);
        dl->AddRect(cellTL, { cellTL.x + cw, cellTL.y + CELL_H }, cellBdr, 5.0f, 0,
            selected ? 2.0f : 1.0f);

        // Icon centred
        float iconX = cellTL.x + (cw - ICON_SZ) * 0.5f;
        float iconY = cellTL.y + 6.0f;
        DrawAchIcon(dl, { iconX, iconY }, ICON_SZ, e.completed);

        // Progress bar
        float frac = (e.goal > 0)
            ? (float)std::min(e.progress, e.goal) / (float)e.goal
            : (e.completed ? 1.0f : 0.0f);
        float barY = iconY + ICON_SZ + 4.0f;
        DrawBar(dl, { cellTL.x + 5.0f, barY }, cw - 10.0f, frac, BAR_BG, BAR_FILL);

        float textY = barY + BAR_H + 4.0f;
        ImVec4 nameCol = e.completed ? COL_DONE : (e.progress > 0 ? COL_WIP : COL_LOCK);

        ImGui::SetCursorScreenPos({ cellTL.x + 4.0f, textY });
        ImGui::PushStyleColor(ImGuiCol_Text, nameCol);
        ImGui::SetWindowFontScale(FONT_SCALE);

        // Truncate name to fit cell manually
        const char* fullName = e.name.c_str();
        char truncated[64];
        strncpy(truncated, fullName, sizeof(truncated) - 1);
        truncated[sizeof(truncated) - 1] = '\0';
        // Shorten until it fits
        float maxTextW = cw - 8.0f;
        while (strlen(truncated) > 3) {
            if (ImGui::CalcTextSize(truncated).x <= maxTextW) break;
            truncated[strlen(truncated) - 1] = '\0';
        }
        // Append ellipsis if truncated
        if (strlen(truncated) < e.name.size()) {
            size_t len = strlen(truncated);
            if (len > 2) { truncated[len - 2] = '.'; truncated[len - 1] = '.'; truncated[len] = '.'; truncated[len + 1] = '\0'; }
        }

        ImGui::TextUnformatted(truncated);
        ImGui::SetWindowFontScale(1.0f);
        ImGui::PopStyleColor();

        // Invisible button over full cell
        ImGui::SetCursorScreenPos(cellTL);
        ImGui::PushStyleColor(ImGuiCol_Button, { 0,0,0,0 });
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, { 1,1,1,0.06f });
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, { 1,1,1,0.12f });
        char btnId[16]; snprintf(btnId, sizeof(btnId), "##ac%d", i);
        if (ImGui::Button(btnId, ImVec2(cw, CELL_H)))
        {
            detailIndex = (detailIndex == i) ? -1 : i; showDetail = (detailIndex >= 0);
        }
        ImGui::PopStyleColor(3);

        col++;
        if (col >= cols) { col = 0; ImGui::Dummy({ 0, 4.0f }); }
    }

    ImGui::EndChild();


    if (showDetail && detailIndex >= 0 && detailIndex < (int)entries.size())
    {
        const auto& sel = entries[detailIndex];
        ImGui::Separator();
        ImGui::Spacing();

        ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.10f, 0.12f, 0.20f, 0.95f));
        ImGui::BeginChild("##ach_detail", ImVec2(0, DETAIL_H - 12.0f), true);

        ImGui::TextColored(sel.completed ? COL_DONE : COL_WIP, "%s", sel.name.c_str());
        ImGui::Separator();
        ImGui::Spacing();
        ImGui::TextWrapped("%s", sel.desc.c_str());
        ImGui::Spacing();

        if (sel.completed)
        {
            ImGui::TextColored(COL_DONE, "Completed");
            if (!sel.completedAt.empty()) {
                ImGui::SameLine();
                ImGui::TextDisabled("on %s", sel.completedAt.c_str());
            }
        }
        else
        {
            ImGui::TextColored(COL_WIP, "Progress: %lld / %lld", sel.progress, sel.goal);
            ImDrawList* ddl = ImGui::GetWindowDrawList();
            ImVec2 bTL = ImGui::GetCursorScreenPos();
            float  bw = ImGui::GetContentRegionAvail().x;
            float  frac = sel.goal > 0
                ? (float)std::min(sel.progress, sel.goal) / (float)sel.goal : 0.0f;
            const float DBH = 10.0f;
            ddl->AddRectFilled(bTL, { bTL.x + bw, bTL.y + DBH },
                IM_COL32(25, 45, 90, 220), 4.0f);
            if (frac > 0.0f)
                ddl->AddRectFilled(bTL, { bTL.x + bw * frac, bTL.y + DBH },
                    IM_COL32((int)(BAR_FILL.x * 255), (int)(BAR_FILL.y * 255),
                        (int)(BAR_FILL.z * 255), 230), 4.0f);
            ImGui::Dummy({ 0, DBH + 4.0f });
        }
        ImGui::TextDisabled("Type: %s", sel.type.c_str());
        ImGui::EndChild();
        ImGui::PopStyleColor();
    }

    ImGui::End();
}