#include "AchievementWindow.h"
#include "imgui.h"

AchievementWindow g_achWindow;

static constexpr int   COLS = 5;
static constexpr float CELL_W = 100.0f;
static constexpr float ICON_SZ = 52.0f;
static constexpr float BAR_H = 6.0f;
static constexpr float LABEL_H = 32.0f;   // two lines of text under icon
static constexpr float CELL_H = ICON_SZ + BAR_H + 4.0f + LABEL_H + 8.0f;
static constexpr float WIN_W = 16.0f + COLS * (CELL_W + 8.0f) - 8.0f + 16.0f;
static constexpr float WIN_H = 460.0f;
static constexpr float DETAIL_W = 320.0f;
static constexpr float DETAIL_H = 200.0f;

static void DrawAchIcon(ImDrawList* dl, ImVec2 tl, float sz, bool completed)
{
    ImVec2 br(tl.x + sz, tl.y + sz);
    ImU32 bgCol = completed
        ? IM_COL32(30, 70, 30, 220)
        : IM_COL32(25, 28, 45, 200);
    ImU32 borderCol = completed
        ? IM_COL32(80, 200, 80, 220)
        : IM_COL32(55, 60, 80, 180);

    dl->AddRectFilled(tl, br, bgCol, 6.0f);
    dl->AddRect(tl, br, borderCol, 6.0f, 0, completed ? 2.0f : 1.0f);

    if (completed)
    {
        // Simple checkmark
        float cx = tl.x + sz * 0.5f;
        float cy = tl.y + sz * 0.5f;
        float s = sz * 0.22f;
        ImVec2 p0(cx - s, cy);
        ImVec2 p1(cx - s * 0.2f, cy + s * 0.9f);
        ImVec2 p2(cx + s, cy - s * 0.7f);
        dl->AddPolyline(&p0, 3, IM_COL32(80, 220, 80, 255), 0, 2.5f);
    }
    else
    {
        // Lock body
        float lx = tl.x + sz * 0.35f;
        float ly = tl.y + sz * 0.46f;
        float lw = sz * 0.30f;
        float lh = sz * 0.26f;
        float arc = sz * 0.13f;
        dl->AddRectFilled(ImVec2(lx, ly), ImVec2(lx + lw, ly + lh),
            IM_COL32(120, 120, 140, 200), 2.0f);
        // shackle arc (approximate with lines)
        dl->AddCircle(ImVec2(lx + lw * 0.5f, ly), arc,
            IM_COL32(120, 120, 140, 200), 12, 2.0f);
    }
}

static void DrawProgressBar(ImDrawList* dl, ImVec2 tl, float w, float frac,
    ImVec4 bgCol, ImVec4 fillCol)
{
    ImVec2 br(tl.x + w, tl.y + BAR_H);
    dl->AddRectFilled(tl, br,
        IM_COL32((int)(bgCol.x * 255), (int)(bgCol.y * 255), (int)(bgCol.z * 255), 200), 3.0f);
    if (frac > 0.0f)
    {
        ImVec2 fillBr(tl.x + w * frac, br.y);
        dl->AddRectFilled(tl, fillBr,
            IM_COL32((int)(fillCol.x * 255), (int)(fillCol.y * 255), (int)(fillCol.z * 255), 230), 3.0f);
    }
}

void AchievementWindow::Render()
{
    if (!isOpen) return;

    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_FirstUseEver, ImVec2(0.5f, 0.5f));
    ImGui::SetNextWindowSize(ImVec2(WIN_W, WIN_H), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowBgAlpha(0.97f);

    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoSavedSettings;

    int done = 0;
    for (auto& e : entries) if (e.completed) done++;
    char title[64];
    snprintf(title, sizeof(title), "Achievements (%d / %d)", done, (int)entries.size());

    if (!ImGui::Begin(title, &isOpen, flags)) { ImGui::End(); return; }

    ImGui::BeginChild("##ach_grid", ImVec2(0, showDetail ? WIN_H - DETAIL_H - 60.0f : 0),
        false, ImGuiWindowFlags_None);

    ImDrawList* dl = ImGui::GetWindowDrawList();

    int col = 0;
    for (int i = 0; i < (int)entries.size(); i++)
    {
        const auto& e = entries[i];
        const bool  selected = (detailIndex == i);

        if (col > 0) ImGui::SameLine(0.0f, 8.0f);

        ImVec2 cellTL = ImGui::GetCursorScreenPos();

        // Cell background
        ImU32 cellBg = selected
            ? IM_COL32(40, 55, 80, 220)
            : IM_COL32(20, 22, 35, 180);
        ImU32 cellBorder = selected
            ? IM_COL32(100, 160, 255, 220)
            : IM_COL32(45, 50, 70, 160);
        dl->AddRectFilled(cellTL, ImVec2(cellTL.x + CELL_W, cellTL.y + CELL_H), cellBg, 5.0f);
        dl->AddRect(cellTL, ImVec2(cellTL.x + CELL_W, cellTL.y + CELL_H), cellBorder, 5.0f, 0,
            selected ? 2.0f : 1.0f);

        // Icon centred in cell
        float iconX = cellTL.x + (CELL_W - ICON_SZ) * 0.5f;
        float iconY = cellTL.y + 6.0f;
        DrawAchIcon(dl, ImVec2(iconX, iconY), ICON_SZ, e.completed);

        // Progress bar
        float frac = (e.goal > 0)
            ? (float)std::min(e.progress, e.goal) / (float)e.goal
            : (e.completed ? 1.0f : 0.0f);
        float barY = iconY + ICON_SZ + 4.0f;
        float barX = cellTL.x + 6.0f;
        DrawProgressBar(dl, ImVec2(barX, barY), CELL_W - 12.0f, frac, BAR_BG, BAR_FILL);

        // Name label (truncated)
        float textY = barY + BAR_H + 4.0f;
        ImVec4 nameCol = e.completed ? COL_DONE : (e.progress > 0 ? COL_WIP : COL_LOCK);
        ImGui::SetCursorScreenPos(ImVec2(cellTL.x + 4.0f, textY));
        ImGui::PushStyleColor(ImGuiCol_Text, nameCol);
        ImGui::PushTextWrapPos(cellTL.x + CELL_W - 4.0f);
        ImGui::TextUnformatted(e.name.c_str());
        ImGui::PopTextWrapPos();
        ImGui::PopStyleColor();

        // Invisible button
        ImGui::SetCursorScreenPos(cellTL);
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0, 0, 0, 0));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(1, 1, 1, 0.06f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(1, 1, 1, 0.12f));
        char btnId[16]; snprintf(btnId, sizeof(btnId), "##ac%d", i);
        if (ImGui::Button(btnId, ImVec2(CELL_W, CELL_H)))
        {
            if (detailIndex == i) { showDetail = false; detailIndex = -1; }
            else { detailIndex = i;    showDetail = true; }
        }
        ImGui::PopStyleColor(3);

        col++;
        if (col >= COLS) { col = 0; ImGui::Dummy(ImVec2(0, 4.0f)); }
    }

    ImGui::EndChild();

    if (showDetail && detailIndex >= 0 && detailIndex < (int)entries.size())
    {
        const auto& sel = entries[detailIndex];

        ImGui::Separator();
        ImGui::Spacing();

        ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.10f, 0.12f, 0.20f, 0.95f));
        ImGui::BeginChild("##ach_detail", ImVec2(0, DETAIL_H - 16.0f), true);

        ImVec4 hdrCol = sel.completed ? COL_DONE : COL_WIP;
        ImGui::TextColored(hdrCol, "%s", sel.name.c_str());
        ImGui::Separator();
        ImGui::Spacing();
        ImGui::TextWrapped("%s", sel.desc.c_str());
        ImGui::Spacing();

        // Progress line
        if (sel.completed)
        {
            ImGui::TextColored(COL_DONE, "Completed");
            if (!sel.completedAt.empty())
            {
                ImGui::SameLine();
                ImGui::TextDisabled("on %s", sel.completedAt.c_str());
            }
        }
        else
        {
            ImGui::TextColored(COL_WIP, "Progress: %lld / %lld", sel.progress, sel.goal);

            ImDrawList* ddl = ImGui::GetWindowDrawList();
            ImVec2 bTL = ImGui::GetCursorScreenPos();
            float bw = ImGui::GetContentRegionAvail().x;
            float frac = (sel.goal > 0)
                ? (float)std::min(sel.progress, sel.goal) / (float)sel.goal : 0.0f;
            const float DETAIL_BAR_H = 10.0f;
            ddl->AddRectFilled(bTL, ImVec2(bTL.x + bw, bTL.y + DETAIL_BAR_H),
                IM_COL32(25, 45, 90, 220), 4.0f);
            if (frac > 0.0f)
                ddl->AddRectFilled(bTL, ImVec2(bTL.x + bw * frac, bTL.y + DETAIL_BAR_H),
                    IM_COL32((int)(BAR_FILL.x * 255), (int)(BAR_FILL.y * 255), (int)(BAR_FILL.z * 255), 230), 4.0f);
            ImGui::Dummy(ImVec2(0, DETAIL_BAR_H + 4.0f));
        }

        ImGui::TextDisabled("Type: %s", sel.type.c_str());

        ImGui::EndChild();
        ImGui::PopStyleColor();
    }

    ImGui::End();
}