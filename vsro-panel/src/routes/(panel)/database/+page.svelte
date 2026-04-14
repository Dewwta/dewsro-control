<script lang="ts">
    import { onMount, tick } from "svelte";
    import PageHeader from "$lib/components/layout/PageHeader.svelte";
    import {
        databaseApi,
        savedQueriesApi,
        type QueryResult,
        type DbSchema,
        type SavedQuery,
    } from "$lib/api/serverApi";

    function uuid(): string {
        if (typeof crypto !== "undefined" && crypto.randomUUID) {
            return crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
            const r = (Math.random() * 16) | 0;
            return (c === "x" ? r : (r & 0x3) | 0x8).toString(16);
        });
    }

    // ── Types ──────────────────────────────────────────────────────────────────
    type SuggestionType = "keyword" | "database" | "table" | "column";
    interface Suggestion {
        type: SuggestionType;
        label: string;
        insertText: string;
        detail?: string;
    }

    interface Tab {
        id: string;
        name: string;
        sql: string;
        result: QueryResult | null;
        running: boolean;
        savedQueryId?: string;
    }

    // ── Tab state ───────────────────────────────────────────────────────────────
    let tabs: Tab[] = [
        { id: "main", name: "main", sql: "", result: null, running: false },
    ];
    let activeTabId = "main";

    $: activeTab = tabs.find((t) => t.id === activeTabId) ?? tabs[0];
    $: activeResult = activeTab?.result ?? null;
    $: activeRunning = activeTab?.running ?? false;

    // sql is the live binding for the textarea — kept in sync with activeTab
    let sql = "";

    function switchTab(id: string) {
        // Persist current sql back into the active tab before leaving
        const cur = tabs.find((t) => t.id === activeTabId);
        if (cur) cur.sql = sql;

        activeTabId = id;
        const next = tabs.find((t) => t.id === id);
        sql = next?.sql ?? "";
        dropVisible = false;
        tick().then(() => textareaEl?.focus());
    }

    function addTab() {
        const id = uuid();
        tabs = [
            ...tabs,
            { id, name: "untitled", sql: "", result: null, running: false },
        ];
        switchTab(id);
    }

    function closeTab(id: string) {
        if (id === "main") return;
        const idx = tabs.findIndex((t) => t.id === id);
        tabs = tabs.filter((t) => t.id !== id);
        if (activeTabId === id) {
            const fallback = tabs[Math.max(0, idx - 1)];
            if (fallback) switchTab(fallback.id);
        }
    }

    // ── Saved-queries panel ────────────────────────────────────────────────────
    let savedPanelOpen = false;
    let savedQueries: SavedQuery[] = [];
    let savedLoading = false;
    let savedError = "";

    async function loadSavedQueries() {
        savedLoading = true;
        savedError = "";
        try {
            savedQueries = await savedQueriesApi.list();
        } catch (e: any) {
            savedError = e.message ?? "Failed to load";
        } finally {
            savedLoading = false;
        }
    }

    function toggleSavedPanel() {
        savedPanelOpen = !savedPanelOpen;
        if (savedPanelOpen) loadSavedQueries();
    }

    function openSavedQuery(q: SavedQuery) {
        // Switch to existing tab if already open for this saved query
        const existing = tabs.find((t) => t.savedQueryId === q.id);
        if (existing) {
            switchTab(existing.id);
            return;
        }

        const id = uuid();
        tabs = [
            ...tabs,
            {
                id,
                name: q.name,
                sql: q.sql,
                result: null,
                running: false,
                savedQueryId: q.id,
            },
        ];
        switchTab(id);
    }

    async function deleteSavedQuery(id: string) {
        try {
            await savedQueriesApi.delete(id);
            savedQueries = savedQueries.filter((q) => q.id !== id);
        } catch (e: any) {
            savedError = e.message ?? "Delete failed";
        }
    }

    // ── Save-query inline form ─────────────────────────────────────────────────
    let saving = false;
    let saveName = "";
    let saveError = "";

    function startSave() {
        saveName =
            activeTab.name !== "main" && activeTab.name !== "untitled"
                ? activeTab.name
                : "";
        saveError = "";
        saving = true;
        tick().then(() => saveInputEl?.focus());
    }

    async function confirmSave() {
        const name = saveName.trim();
        if (!name) {
            saveError = "Name is required";
            return;
        }
        try {
            const entry = await savedQueriesApi.save(name, sql);
            // Update the tab so it's linked to the saved query and renamed
            const cur = tabs.find((t) => t.id === activeTabId);
            if (cur) {
                cur.name = name;
                cur.savedQueryId = entry.id;
                tabs = tabs;
            }
            savedQueries = [...savedQueries, entry];
            saving = false;
            saveName = "";
            saveError = "";
        } catch (e: any) {
            saveError = e.message ?? "Save failed";
        }
    }

    function handleSaveKeydown(e: KeyboardEvent) {
        if (e.key === "Enter") {
            e.preventDefault();
            confirmSave();
        }
        if (e.key === "Escape") {
            saving = false;
            saveName = "";
            saveError = "";
        }
    }

    let saveInputEl: HTMLInputElement;

    // ── Schema state ───────────────────────────────────────────────────────────
    type SchemaStatus =
        | "idle"
        | "loading"
        | "ready"
        | "missing"
        | "building"
        | "error";
    let schemaStatus: SchemaStatus = "idle";
    let schemaMsg = "";
    let schemaSummary = "";

    let allTableSuggestions: Suggestion[] = [];
    let columnsByTable = new Map<string, Suggestion[]>();

    // ── SQL keyword list ───────────────────────────────────────────────────────
    const KEYWORDS: Suggestion[] = [
        "SELECT",
        "FROM",
        "WHERE",
        "AND",
        "OR",
        "NOT",
        "NULL",
        "IS",
        "IS NOT",
        "LIKE",
        "IN",
        "NOT IN",
        "BETWEEN",
        "EXISTS",
        "USE",
        "JOIN",
        "INNER JOIN",
        "LEFT JOIN",
        "RIGHT JOIN",
        "FULL JOIN",
        "ON",
        "ORDER BY",
        "GROUP BY",
        "HAVING",
        "DISTINCT",
        "TOP",
        "AS",
        "INSERT INTO",
        "VALUES",
        "UPDATE",
        "SET",
        "DELETE FROM",
        "UNION",
        "UNION ALL",
        "CASE",
        "WHEN",
        "THEN",
        "ELSE",
        "END",
        "COUNT",
        "SUM",
        "AVG",
        "MIN",
        "MAX",
        "LEN",
        "UPPER",
        "LOWER",
        "ISNULL",
        "COALESCE",
        "CAST",
        "CONVERT",
        "GETDATE",
        "NEWID",
    ].map((k) => ({ type: "keyword" as const, label: k, insertText: k }));

    const DB_NAMES: Suggestion[] = [
        "SRO_VT_SHARD",
        "SRO_VT_ACCOUNT",
        "dbo",
    ].map((d) => ({
        type: "database" as const,
        label: d,
        insertText: d,
    }));

    // ── Autocomplete state ─────────────────────────────────────────────────────
    let textareaEl: HTMLTextAreaElement;
    let suggestions: Suggestion[] = [];
    let activeIdx = 0;
    let dropVisible = false;
    let dropTop = 0;
    let dropLeft = 0;
    let acWordStart = 0;

    // ── Schema load / build ────────────────────────────────────────────────────
    function applySchema(schema: DbSchema) {
        allTableSuggestions = [];
        columnsByTable = new Map();
        let totalCols = 0;
        for (const [dbName, tables] of Object.entries(schema)) {
            for (const [tName, cols] of Object.entries(tables)) {
                allTableSuggestions.push({
                    type: "table",
                    label: tName,
                    insertText: tName,
                    detail: dbName,
                });
                columnsByTable.set(
                    tName.toLowerCase(),
                    cols.map((c) => ({
                        type: "column" as const,
                        label: c,
                        insertText: c,
                        detail: tName,
                    })),
                );
                totalCols += cols.length;
            }
        }
        const dbCount = Object.keys(schema).length;
        const tableCount = allTableSuggestions.length;
        schemaSummary = `${dbCount} databases · ${tableCount} tables · ${totalCols} columns`;
    }

    async function loadSchema() {
        schemaStatus = "loading";
        schemaMsg = "";
        try {
            const schema = await databaseApi.getSchema();
            applySchema(schema);
            schemaStatus = "ready";
        } catch (e: any) {
            if (e.message?.startsWith("404")) schemaStatus = "missing";
            else {
                schemaStatus = "error";
                schemaMsg = e.message;
            }
        }
    }

    async function buildSchema() {
        schemaStatus = "building";
        schemaMsg = "";
        try {
            const res = await databaseApi.buildSchema();
            schemaMsg = res.message;
            await loadSchema();
        } catch (e: any) {
            schemaStatus = "error";
            schemaMsg = e.message;
        }
    }

    onMount(loadSchema);

    // ── Mirror-div caret coordinate calculation ────────────────────────────────
    function getCaretCoords(el: HTMLTextAreaElement, pos: number) {
        const mirror = document.createElement("div");
        const cs = window.getComputedStyle(el);

        [
            "boxSizing",
            "width",
            "paddingTop",
            "paddingRight",
            "paddingBottom",
            "paddingLeft",
            "borderTopWidth",
            "borderRightWidth",
            "borderBottomWidth",
            "borderLeftWidth",
            "fontFamily",
            "fontSize",
            "fontWeight",
            "fontStyle",
            "letterSpacing",
            "lineHeight",
            "tabSize",
        ].forEach((p) => {
            (mirror.style as any)[p] = (cs as any)[p];
        });

        Object.assign(mirror.style, {
            position: "absolute",
            visibility: "hidden",
            top: "-9999px",
            left: "-9999px",
            whiteSpace: "pre-wrap",
            wordWrap: "break-word",
            overflowWrap: "break-word",
        });

        document.body.appendChild(mirror);
        mirror.textContent = el.value.slice(0, pos);

        const marker = document.createElement("span");
        marker.textContent = "\u200b";
        mirror.appendChild(marker);

        const coords = {
            top: marker.offsetTop - el.scrollTop,
            left: marker.offsetLeft - el.scrollLeft,
        };
        document.body.removeChild(mirror);
        return coords;
    }

    // ── Word extraction ────────────────────────────────────────────────────────
    function wordAtCursor(text: string, cursor: number) {
        let s = cursor;
        while (s > 0 && /[\w_]/.test(text[s - 1])) s--;

        const word = text.slice(s, cursor);
        const precededByDot = s > 0 && text[s - 1] === ".";

        let tableBeforeDot: string | null = null;
        if (precededByDot) {
            let te = s - 1,
                ts = te;
            while (ts > 0 && /[\w_]/.test(text[ts - 1])) ts--;
            tableBeforeDot = text.slice(ts, te);
        }

        return { word, start: s, precededByDot, tableBeforeDot };
    }

    // ── Parse FROM/JOIN tables referenced in the query ─────────────────────────
    function extractTablesFromSql(text: string): string[] {
        const pattern = /\b(?:FROM|JOIN|UPDATE|INTO)\s+((?:[\w_]+\.)*[\w_]+)/gi;
        const found = new Set<string>();
        let m: RegExpExecArray | null;
        while ((m = pattern.exec(text)) !== null) {
            const parts = m[1].split(".");
            const tName = parts[parts.length - 1];
            if (columnsByTable.has(tName.toLowerCase())) found.add(tName);
        }
        return [...found];
    }

    // ── Suggestion filtering ───────────────────────────────────────────────────
    function filterSuggestions(
        word: string,
        precededByDot: boolean,
        tableBeforeDot: string | null,
        referencedTables: string[],
    ): Suggestion[] {
        const lo = word.toLowerCase();

        if (precededByDot && tableBeforeDot) {
            const cols = columnsByTable.get(tableBeforeDot.toLowerCase()) ?? [];
            return lo
                ? cols
                      .filter((c) => c.label.toLowerCase().startsWith(lo))
                      .slice(0, 20)
                : cols.slice(0, 20);
        }

        if (lo.length < 2) return [];

        const refCols: Suggestion[] = [];
        for (const tName of referencedTables) {
            const cols = columnsByTable.get(tName.toLowerCase()) ?? [];
            refCols.push(
                ...cols.filter((c) => c.label.toLowerCase().startsWith(lo)),
            );
        }

        const dbs = DB_NAMES.filter((d) =>
            d.label.toLowerCase().startsWith(lo),
        );
        const kws = KEYWORDS.filter((k) =>
            k.label.toLowerCase().startsWith(lo),
        );
        const tbls = allTableSuggestions
            .filter((t) => t.label.toLowerCase().includes(lo))
            .slice(0, 15);

        return [...refCols.slice(0, 15), ...dbs, ...kws, ...tbls];
    }

    // ── Autocomplete update ────────────────────────────────────────────────────
    async function updateAC() {
        if (!textareaEl) return;
        const cursor = textareaEl.selectionStart;
        const { word, start, precededByDot, tableBeforeDot } = wordAtCursor(
            sql,
            cursor,
        );
        const referencedTables = extractTablesFromSql(sql);

        acWordStart = start;
        const list = filterSuggestions(
            word,
            precededByDot,
            tableBeforeDot,
            referencedTables,
        );

        if (!list.length) {
            dropVisible = false;
            return;
        }

        suggestions = list;
        activeIdx = 0;
        dropVisible = true;

        await tick();
        const lh =
            parseFloat(window.getComputedStyle(textareaEl).lineHeight) || 19;
        const coords = getCaretCoords(textareaEl, start);
        dropTop = coords.top + lh + 4;
        dropLeft = Math.max(
            0,
            Math.min(coords.left, textareaEl.clientWidth - 270),
        );
    }

    // ── Apply suggestion ───────────────────────────────────────────────────────
    function applySuggestion(s: Suggestion) {
        const cursor = textareaEl.selectionStart;
        sql = sql.slice(0, acWordStart) + s.insertText + sql.slice(cursor);
        syncSqlToTab();
        dropVisible = false;
        tick().then(() => {
            const p = acWordStart + s.insertText.length;
            textareaEl.selectionStart = textareaEl.selectionEnd = p;
            textareaEl.focus();
        });
    }

    function syncSqlToTab() {
        const cur = tabs.find((t) => t.id === activeTabId);
        if (cur) cur.sql = sql;
    }

    // ── Keyboard / input events ────────────────────────────────────────────────
    function handleKeydown(e: KeyboardEvent) {
        if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
            e.preventDefault();
            dropVisible = false;
            runQuery();
            return;
        }

        if (dropVisible) {
            const len = Math.min(suggestions.length, 12);
            if (e.key === "ArrowDown") {
                e.preventDefault();
                activeIdx = (activeIdx + 1) % len;
                return;
            }
            if (e.key === "ArrowUp") {
                e.preventDefault();
                activeIdx = (activeIdx - 1 + len) % len;
                return;
            }
            if (e.key === "Tab") {
                e.preventDefault();
                applySuggestion(suggestions[activeIdx]);
                return;
            }
            if (e.key === "Enter") {
                e.preventDefault();
                applySuggestion(suggestions[activeIdx]);
                return;
            }
            if (e.key === "Escape") {
                dropVisible = false;
                return;
            }
        }
    }

    function handleInput() {
        syncSqlToTab();
        updateAC();
    }
    function handleClick() {
        updateAC();
    }
    function handleBlur() {
        setTimeout(() => {
            dropVisible = false;
        }, 130);
    }

    // ── Copy functionality ─────────────────────────────────────────────────────
    let selectedRows = new Set<number>();
    let copyRangeEnd = 10;
    let copyFeedback = "";
    let copyFeedbackTimer: ReturnType<typeof setTimeout>;

    // Use a prev-value guard so the reset only fires when activeResult actually
    // changes to a new object, not on every cascading reactive invalidation
    // (e.g. `tabs = tabs` in runQuery.finally re-evaluates the whole chain).
    let _prevResult: typeof activeResult = undefined as any;
    $: if (activeResult !== _prevResult) {
        _prevResult = activeResult;
        selectedRows = new Set();
        if (activeResult && !activeResult.error) {
            copyRangeEnd = Math.min(activeResult.rows.length, 10) || 10;
        }
    }

    function rowsToTsv(rows: (string | null)[][], cols: string[]): string {
        const header = cols.join("\t");
        const body = rows
            .map((r) => r.map((c) => c ?? "NULL").join("\t"))
            .join("\n");
        return header + "\n" + body;
    }

    function flashCopied() {
        copyFeedback = "Copied!";
        clearTimeout(copyFeedbackTimer);
        copyFeedbackTimer = setTimeout(() => {
            copyFeedback = "";
        }, 1800);
    }

    async function copyAll() {
        if (!activeResult) return;
        await navigator.clipboard.writeText(
            rowsToTsv(activeResult.rows, activeResult.columns),
        );
        flashCopied();
    }

    async function copySelected() {
        if (!activeResult || selectedRows.size === 0) return;
        const rows = activeResult.rows.filter((_, i) => selectedRows.has(i));
        await navigator.clipboard.writeText(
            rowsToTsv(rows, activeResult.columns),
        );
        flashCopied();
    }

    async function copyRange() {
        if (!activeResult) return;
        const rows = activeResult.rows.slice(0, Math.max(1, copyRangeEnd));
        await navigator.clipboard.writeText(
            rowsToTsv(rows, activeResult.columns),
        );
        flashCopied();
    }

    function toggleRow(i: number) {
        if (selectedRows.has(i)) selectedRows.delete(i);
        else selectedRows.add(i);
        selectedRows = selectedRows;
    }

    function toggleAllRows() {
        if (!activeResult) return;
        if (selectedRows.size === activeResult.rows.length)
            selectedRows = new Set();
        else selectedRows = new Set(activeResult.rows.map((_, i) => i));
    }

    // ── Run query ──────────────────────────────────────────────────────────────
    async function runQuery() {
        if (!sql.trim()) return;
        const tabId = activeTabId;
        const cur = tabs.find((t) => t.id === tabId);
        if (!cur) return;

        cur.running = true;
        cur.result = null;
        tabs = tabs;

        try {
            const raw = await databaseApi.runQuery(sql);
            const tab = tabs.find((t) => t.id === tabId);
            if (tab) {
                tab.result = {
                    columns: raw.columns ?? [],
                    rows: raw.rows ?? [],
                    rowsAffected: raw.rowsAffected ?? 0,
                    error: raw.error ?? null,
                };
            }
        } catch (e: any) {
            const tab = tabs.find((t) => t.id === tabId);
            if (tab)
                tab.result = {
                    columns: [],
                    rows: [],
                    rowsAffected: 0,
                    error: e.message ?? "Unknown error",
                };
        } finally {
            const tab = tabs.find((t) => t.id === tabId);
            if (tab) tab.running = false;
            tabs = tabs;
        }
    }
</script>

<!-- ── Header ─────────────────────────────────────────────────────────────── -->
<PageHeader
    title="Database"
    subtitle="Execute raw SQL queries against the server databases"
>
    <svelte:fragment slot="actions">
        {#if schemaStatus === "ready"}
            <span class="schema-badge schema-badge--ok" title={schemaSummary}>
                <span class="schema-dot"></span> Schema ready
            </span>
            <button
                class="schema-btn"
                on:click={buildSchema}
                disabled={schemaStatus === "building"}
            >
                Rebuild Schema
            </button>
        {:else if schemaStatus === "missing"}
            <span class="schema-badge schema-badge--warn">Schema not built</span
            >
            <button
                class="schema-btn schema-btn--primary"
                on:click={buildSchema}>Build Schema</button
            >
        {:else if schemaStatus === "building"}
            <span class="schema-badge schema-badge--dim">
                <span class="schema-spinner"></span> Building…
            </span>
        {:else if schemaStatus === "loading"}
            <span class="schema-badge schema-badge--dim">Loading schema…</span>
        {:else if schemaStatus === "error"}
            <span class="schema-badge schema-badge--err" title={schemaMsg}
                >Schema error</span
            >
            <button class="schema-btn" on:click={buildSchema}>Retry</button>
        {/if}
    </svelte:fragment>
</PageHeader>

<div class="page">
    <!-- ── Schema summary strip ───────────────────────────────────────────── -->
    {#if schemaStatus === "ready"}
        <div class="schema-strip">
            {schemaSummary} · Intellisense active — Tab or Enter to accept, Esc to
            dismiss
        </div>
    {:else if schemaStatus === "missing"}
        <div class="schema-strip schema-strip--warn">
            Intellisense is disabled. Click <strong>Build Schema</strong> above to
            index all tables and columns.
        </div>
    {/if}

    <!-- ── Main layout (editor col + optional saved panel) ────────────────── -->
    <div class="editor-layout" class:editor-layout--with-panel={savedPanelOpen}>
        <!-- ── Editor column ──────────────────────────────────────────────── -->
        <div class="editor-col">
            <!-- ── Editor card ──────────────────────────────────────────── -->
            <div class="editor-card">
                <!-- Tab bar -->
                <div class="tab-bar">
                    {#each tabs as tab (tab.id)}
                        <button
                            class="tab"
                            class:tab--active={tab.id === activeTabId}
                            on:click={() => switchTab(tab.id)}
                        >
                            <span class="tab-name">{tab.name}</span>
                            {#if tab.id !== "main"}
                                <span
                                    class="tab-close"
                                    role="button"
                                    tabindex="-1"
                                    on:click|stopPropagation={() =>
                                        closeTab(tab.id)}
                                    on:keydown|stopPropagation={(e) =>
                                        e.key === "Enter" && closeTab(tab.id)}
                                    >×</span
                                >
                            {/if}
                        </button>
                    {/each}
                    <button class="tab-add" on:click={addTab} title="New tab"
                        >+</button
                    >
                </div>

                <!-- Editor header -->
                <div class="editor-header">
                    <span class="editor-label">SQL Query</span>
                    <div class="editor-actions">
                        {#if saving}
                            <input
                                class="save-name-input"
                                bind:this={saveInputEl}
                                bind:value={saveName}
                                placeholder="Query name…"
                                on:keydown={handleSaveKeydown}
                            />
                            {#if saveError}<span class="save-error"
                                    >{saveError}</span
                                >{/if}
                            <button
                                class="action-btn action-btn--primary"
                                on:click={confirmSave}>Save</button
                            >
                            <button
                                class="action-btn"
                                on:click={() => {
                                    saving = false;
                                    saveName = "";
                                    saveError = "";
                                }}>Cancel</button
                            >
                        {:else}
                            <button
                                class="action-btn"
                                on:click={startSave}
                                disabled={!sql.trim()}
                            >
                                Save Query
                            </button>
                            <button
                                class="action-btn"
                                class:action-btn--active={savedPanelOpen}
                                on:click={toggleSavedPanel}
                            >
                                Saved Queries
                            </button>
                            <span class="hint"
                                >Ctrl+Enter to run · Tab to autocomplete</span
                            >
                        {/if}
                    </div>
                </div>

                <!-- Editor body -->
                <div class="editor-body">
                    <textarea
                        class="sql-input"
                        bind:value={sql}
                        bind:this={textareaEl}
                        on:input={handleInput}
                        on:keydown={handleKeydown}
                        on:click={handleClick}
                        on:blur={handleBlur}
                        rows="10"
                        spellcheck="false"
                        autocorrect="off"
                        autocapitalize="off"
                        placeholder="SELECT TOP 10 * FROM SRO_VT_SHARD.dbo._RefSkill WHERE basic_code LIKE '%PET%'"
                    ></textarea>

                    <!-- Autocomplete dropdown -->
                    {#if dropVisible}
                        <div
                            class="ac-dropdown"
                            style="top:{dropTop}px; left:{dropLeft}px"
                            on:mousedown|preventDefault={() => {}}
                        >
                            {#each suggestions.slice(0, 12) as s, i (s.type + s.label)}
                                <button
                                    class="ac-item"
                                    class:ac-item--active={i === activeIdx}
                                    on:mousedown|preventDefault={() =>
                                        applySuggestion(s)}
                                    on:mouseover={() => {
                                        activeIdx = i;
                                    }}
                                >
                                    <span class="ac-badge ac-badge--{s.type}"
                                        >{s.type[0].toUpperCase()}</span
                                    >
                                    <span class="ac-label">{s.label}</span>
                                    {#if s.detail}<span class="ac-detail"
                                            >{s.detail}</span
                                        >{/if}
                                </button>
                            {/each}
                            {#if suggestions.length > 12}
                                <div class="ac-more">
                                    +{suggestions.length - 12} more — keep typing
                                    to narrow
                                </div>
                            {/if}
                        </div>
                    {/if}
                </div>

                <!-- Editor footer -->
                <div class="editor-footer">
                    <button
                        class="run-btn"
                        on:click={runQuery}
                        disabled={activeRunning || !sql.trim()}
                    >
                        {#if activeRunning}
                            <span class="spinner"></span> Running…
                        {:else}
                            ▶ Run Query
                        {/if}
                    </button>
                </div>
            </div>

            <!-- ── Results ──────────────────────────────────────────────── -->
            {#if activeResult}
                <div class="result-card">
                    {#if activeResult.error}
                        <div class="result-error">
                            <span class="result-error__icon">✕</span>
                            <pre
                                class="result-error__text">{activeResult.error}</pre>
                        </div>
                    {:else if activeResult.columns.length === 0}
                        <div class="result-info">
                            Query OK — rows affected: <strong
                                >{activeResult.rowsAffected}</strong
                            >
                        </div>
                    {:else}
                        <div class="result-meta">
                            <span
                                >{activeResult.rows.length} row{activeResult
                                    .rows.length !== 1
                                    ? "s"
                                    : ""}</span
                            >
                            <div class="copy-toolbar">
                                <button class="copy-btn" on:click={copyAll}
                                    >Copy All</button
                                >
                                <button
                                    class="copy-btn"
                                    on:click={toggleAllRows}
                                >
                                    {selectedRows.size ===
                                        activeResult.rows.length &&
                                    activeResult.rows.length > 0
                                        ? "Deselect All"
                                        : "Select All"}
                                </button>
                                <button
                                    class="copy-btn"
                                    on:click={copySelected}
                                    disabled={selectedRows.size === 0}
                                >
                                    Copy Selected{selectedRows.size > 0
                                        ? ` (${selectedRows.size})`
                                        : ""}
                                </button>
                                <span class="copy-range-group">
                                    <span class="copy-range-label">Rows 1–</span
                                    >
                                    <input
                                        class="copy-range-input"
                                        type="number"
                                        min="1"
                                        max={activeResult.rows.length}
                                        bind:value={copyRangeEnd}
                                    />
                                    <button
                                        class="copy-btn"
                                        on:click={copyRange}>Copy Range</button
                                    >
                                </span>
                                {#if copyFeedback}<span class="copy-feedback"
                                        >{copyFeedback}</span
                                    >{/if}
                            </div>
                        </div>
                        <div class="table-wrap">
                            <table class="result-table">
                                <thead>
                                    <tr>
                                        <th class="select-col"
                                            ><input
                                                type="checkbox"
                                                checked={selectedRows.size ===
                                                    activeResult.rows.length &&
                                                    activeResult.rows.length >
                                                        0}
                                                on:change={toggleAllRows}
                                            /></th
                                        >
                                        {#each activeResult.columns as col}<th
                                                >{col}</th
                                            >{/each}
                                    </tr>
                                </thead>
                                <tbody>
                                    {#each activeResult.rows as row, i}
                                        <tr
                                            class:row-selected={selectedRows.has(
                                                i,
                                            )}
                                            on:click={() => toggleRow(i)}
                                        >
                                            <td
                                                class="select-col"
                                                on:click|stopPropagation={() => {}}
                                            >
                                                <input
                                                    type="checkbox"
                                                    checked={selectedRows.has(
                                                        i,
                                                    )}
                                                    on:change={() =>
                                                        toggleRow(i)}
                                                />
                                            </td>
                                            {#each row as cell}<td
                                                    class:null-cell={cell ===
                                                        null}
                                                    >{cell ?? "NULL"}</td
                                                >{/each}
                                        </tr>
                                    {/each}
                                </tbody>
                            </table>
                        </div>
                    {/if}
                </div>
            {/if}
        </div>
        <!-- /editor-col -->

        <!-- ── Saved queries panel ────────────────────────────────────────── -->
        {#if savedPanelOpen}
            <div class="saved-panel">
                <div class="saved-panel-header">
                    <span class="saved-panel-title">Saved Queries</span>
                    <button
                        class="saved-panel-close"
                        on:click={toggleSavedPanel}>×</button
                    >
                </div>

                <div class="saved-panel-body">
                    {#if savedLoading}
                        <div class="saved-state saved-state--dim">Loading…</div>
                    {:else if savedError}
                        <div class="saved-state saved-state--err">
                            {savedError}
                        </div>
                    {:else if savedQueries.length === 0}
                        <div class="saved-state saved-state--dim">
                            No saved queries yet.<br />Run a query and click
                            <strong>Save Query</strong>.
                        </div>
                    {:else}
                        {#each savedQueries as q (q.id)}
                            <div class="saved-item">
                                <button
                                    class="saved-item-name"
                                    on:click={() => openSavedQuery(q)}
                                    title={q.sql}
                                >
                                    {q.name}
                                </button>
                                <button
                                    class="saved-item-del"
                                    on:click={() => deleteSavedQuery(q.id)}
                                    title="Delete">×</button
                                >
                            </div>
                        {/each}
                    {/if}
                </div>
            </div>
        {/if}
    </div>
    <!-- /editor-layout -->
</div>

<style>
    .page {
        padding: 1.4rem 1.5rem;
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    /* ── Layout ── */
    .editor-layout {
        display: grid;
        grid-template-columns: 1fr;
        gap: 1rem;
        align-items: start;
    }
    .editor-layout--with-panel {
        grid-template-columns: 1fr 260px;
    }

    .editor-col {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        min-width: 0;
    }

    /* ── Schema header widgets ── */
    .schema-badge {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        font-family: var(--font-heading);
        font-size: 0.65rem;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        padding: 0.25rem 0.6rem;
        border-radius: var(--radius);
        border: 1px solid transparent;
    }
    .schema-badge--ok {
        color: var(--green-bright);
        border-color: rgba(0, 200, 100, 0.25);
        background: rgba(0, 200, 100, 0.07);
    }
    .schema-badge--warn {
        color: var(--gold-light);
        border-color: var(--border-gold);
        background: rgba(139, 94, 28, 0.1);
    }
    .schema-badge--dim {
        color: var(--text-dim);
        border-color: var(--border-dark);
    }
    .schema-badge--err {
        color: var(--red-light);
        border-color: var(--red-dark);
        background: rgba(92, 16, 16, 0.15);
    }

    .schema-dot {
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: var(--green-bright);
        box-shadow: 0 0 5px var(--green-bright);
        flex-shrink: 0;
    }
    .schema-spinner {
        width: 10px;
        height: 10px;
        border: 2px solid var(--border-mid);
        border-top-color: var(--text-muted);
        border-radius: 50%;
        animation: spin 0.7s linear infinite;
        flex-shrink: 0;
    }
    .schema-btn {
        font-family: var(--font-heading);
        font-size: 0.65rem;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        padding: 0.25rem 0.7rem;
        border-radius: var(--radius);
        border: 1px solid var(--border-dark);
        background: var(--bg-raised);
        color: var(--text-muted);
        cursor: pointer;
        transition:
            background 0.15s,
            color 0.15s;
    }
    .schema-btn:hover:not(:disabled) {
        background: var(--bg-hover);
        color: var(--text-base);
    }
    .schema-btn:disabled {
        opacity: 0.4;
        cursor: not-allowed;
    }
    .schema-btn--primary {
        background: var(--gold-dim);
        border-color: var(--border-gold);
        color: var(--text-bright);
    }
    .schema-btn--primary:hover:not(:disabled) {
        background: var(--gold);
    }

    /* ── Schema strip ── */
    .schema-strip {
        font-size: 0.72rem;
        color: var(--text-dim);
        padding: 0.45rem 0.8rem;
        background: var(--bg-surface);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
    }
    .schema-strip--warn {
        color: var(--gold-light);
        border-color: rgba(139, 94, 28, 0.4);
        background: rgba(139, 94, 28, 0.07);
    }
    .schema-strip strong {
        color: inherit;
        font-weight: 600;
    }

    /* ── Tab bar ── */
    .tab-bar {
        display: flex;
        align-items: stretch;
        overflow-x: auto;
        background: var(--bg-deep);
        border-bottom: 1px solid var(--border-dark);
        border-radius: var(--radius) var(--radius) 0 0;
        scrollbar-width: none;
    }
    .tab-bar::-webkit-scrollbar {
        display: none;
    }

    .tab {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.42rem 0.85rem;
        background: transparent;
        border: none;
        border-right: 1px solid var(--border-dark);
        color: var(--text-dim);
        font-family: var(--font-heading);
        font-size: 0.65rem;
        letter-spacing: 0.07em;
        cursor: pointer;
        white-space: nowrap;
        transition:
            background 0.12s,
            color 0.12s;
        position: relative;
    }
    .tab:hover {
        background: var(--bg-raised);
        color: var(--text-muted);
    }
    .tab--active {
        background: var(--bg-surface);
        color: var(--gold-light);
        border-bottom: 1px solid var(--bg-surface);
        margin-bottom: -1px;
        z-index: 1;
    }

    .tab-name {
        max-width: 120px;
        overflow: hidden;
        text-overflow: ellipsis;
    }

    .tab-close {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 14px;
        height: 14px;
        border-radius: 3px;
        font-size: 0.75rem;
        line-height: 1;
        color: var(--text-dim);
        cursor: pointer;
        transition:
            background 0.1s,
            color 0.1s;
        flex-shrink: 0;
    }
    .tab-close:hover {
        background: rgba(200, 60, 60, 0.2);
        color: var(--red-light);
    }

    .tab-add {
        padding: 0 0.7rem;
        background: transparent;
        border: none;
        color: var(--text-dim);
        font-size: 1rem;
        line-height: 1;
        cursor: pointer;
        transition: color 0.12s;
        flex-shrink: 0;
    }
    .tab-add:hover {
        color: var(--text-base);
    }

    /* ── Editor card ── */
    .editor-card {
        background: var(--bg-surface);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
        overflow: visible;
    }

    .editor-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.5rem 0.9rem;
        border-bottom: 1px solid var(--border-dark);
        background: var(--bg-raised);
        gap: 0.75rem;
        flex-wrap: wrap;
    }

    .editor-label {
        font-family: var(--font-heading);
        font-size: 0.63rem;
        letter-spacing: 0.12em;
        text-transform: uppercase;
        color: var(--text-muted);
        flex-shrink: 0;
    }

    .editor-actions {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .action-btn {
        font-family: var(--font-heading);
        font-size: 0.63rem;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        padding: 0.22rem 0.6rem;
        border-radius: var(--radius);
        border: 1px solid var(--border-dark);
        background: var(--bg-deep);
        color: var(--text-muted);
        cursor: pointer;
        transition:
            background 0.12s,
            color 0.12s,
            border-color 0.12s;
        white-space: nowrap;
    }
    .action-btn:hover:not(:disabled) {
        background: var(--bg-hover);
        color: var(--text-base);
    }
    .action-btn:disabled {
        opacity: 0.35;
        cursor: not-allowed;
    }
    .action-btn--primary {
        background: var(--gold-dim);
        border-color: var(--border-gold);
        color: var(--text-bright);
    }
    .action-btn--primary:hover:not(:disabled) {
        background: var(--gold);
    }
    .action-btn--active {
        border-color: var(--border-gold);
        color: var(--gold-light);
        background: rgba(139, 94, 28, 0.12);
    }

    .save-name-input {
        padding: 0.2rem 0.5rem;
        background: var(--bg-deep);
        border: 1px solid var(--border-gold);
        border-radius: var(--radius);
        color: var(--text-base);
        font-family: var(--font-heading);
        font-size: 0.7rem;
        outline: none;
        width: 160px;
    }
    .save-name-input::placeholder {
        color: var(--text-dim);
    }

    .save-error {
        font-size: 0.62rem;
        color: var(--red-light);
        font-family: var(--font-heading);
    }

    .hint {
        font-size: 0.63rem;
        color: var(--text-dim);
        white-space: nowrap;
    }

    /* The body needs position:relative so the absolute dropdown works */
    .editor-body {
        position: relative;
    }

    .sql-input {
        display: block;
        width: 100%;
        box-sizing: border-box;
        padding: 0.9rem 1rem;
        background: var(--bg-deep);
        color: var(--text-base);
        font-family: "Consolas", "Cascadia Code", "Monaco", monospace;
        font-size: 0.83rem;
        line-height: 1.65;
        border: none;
        outline: none;
        resize: vertical;
        min-height: 160px;
    }
    .sql-input::placeholder {
        color: var(--text-dim);
    }

    .editor-footer {
        display: flex;
        justify-content: flex-end;
        padding: 0.5rem 0.9rem;
        border-top: 1px solid var(--border-dark);
        background: var(--bg-raised);
        border-radius: 0 0 var(--radius) var(--radius);
    }

    .run-btn {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.35rem 1.05rem;
        background: var(--gold-dim);
        border: 1px solid var(--border-gold);
        border-radius: var(--radius);
        color: var(--text-bright);
        font-family: var(--font-heading);
        font-size: 0.72rem;
        letter-spacing: 0.08em;
        cursor: pointer;
        transition: background 0.15s;
    }
    .run-btn:hover:not(:disabled) {
        background: var(--gold);
    }
    .run-btn:disabled {
        opacity: 0.4;
        cursor: not-allowed;
    }

    .spinner {
        width: 11px;
        height: 11px;
        border: 2px solid var(--gold-dim);
        border-top-color: var(--text-bright);
        border-radius: 50%;
        animation: spin 0.7s linear infinite;
    }
    @keyframes spin {
        to {
            transform: rotate(360deg);
        }
    }

    /* ── Autocomplete dropdown ── */
    .ac-dropdown {
        position: absolute;
        z-index: 200;
        min-width: 260px;
        max-width: 420px;
        background: var(--bg-raised);
        border: 1px solid var(--border-gold);
        border-radius: var(--radius);
        box-shadow: 0 6px 24px rgba(0, 0, 0, 0.55);
        overflow: hidden;
        display: flex;
        flex-direction: column;
    }
    .ac-item {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.32rem 0.65rem;
        background: transparent;
        border: none;
        color: var(--text-base);
        font-size: 0.8rem;
        font-family: "Consolas", "Cascadia Code", monospace;
        text-align: left;
        cursor: pointer;
        width: 100%;
        transition: background 0.08s;
    }
    .ac-item:hover,
    .ac-item--active {
        background: rgba(139, 94, 28, 0.18);
    }
    .ac-badge {
        flex-shrink: 0;
        width: 16px;
        height: 16px;
        border-radius: 3px;
        font-size: 0.6rem;
        font-family: var(--font-heading);
        font-weight: 700;
        display: flex;
        align-items: center;
        justify-content: center;
    }
    .ac-badge--keyword {
        background: rgba(139, 94, 28, 0.35);
        color: var(--gold-light);
    }
    .ac-badge--table {
        background: rgba(30, 130, 80, 0.35);
        color: #6ee7b7;
    }
    .ac-badge--column {
        background: rgba(60, 100, 200, 0.35);
        color: #93c5fd;
    }
    .ac-badge--database {
        background: rgba(140, 60, 180, 0.35);
        color: #d8b4fe;
    }
    .ac-label {
        flex: 1;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }
    .ac-detail {
        font-size: 0.68rem;
        color: var(--text-dim);
        flex-shrink: 0;
        max-width: 120px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        font-family: var(--font-heading);
    }
    .ac-more {
        padding: 0.28rem 0.65rem;
        font-size: 0.64rem;
        color: var(--text-dim);
        font-family: var(--font-heading);
        letter-spacing: 0.04em;
        border-top: 1px solid var(--border-dark);
        background: var(--bg-deep);
    }

    /* ── Result card ── */
    .result-card {
        background: var(--bg-surface);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
        overflow: hidden;
    }
    .result-error {
        display: flex;
        align-items: flex-start;
        gap: 0.6rem;
        padding: 0.85rem 1rem;
        background: rgba(92, 16, 16, 0.2);
        color: var(--red-light);
    }
    .result-error__icon {
        flex-shrink: 0;
        font-weight: 700;
        padding-top: 1px;
    }
    .result-error__text {
        margin: 0;
        font-family: "Consolas", monospace;
        font-size: 0.8rem;
        white-space: pre-wrap;
        word-break: break-word;
    }
    .result-info {
        padding: 0.85rem 1rem;
        font-size: 0.83rem;
        color: var(--text-muted);
    }
    .result-info strong {
        color: var(--gold-light);
    }
    .result-meta {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem;
        flex-wrap: wrap;
        padding: 0.4rem 0.9rem;
        font-size: 0.63rem;
        font-family: var(--font-heading);
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: var(--text-dim);
        background: var(--bg-raised);
        border-bottom: 1px solid var(--border-dark);
    }
    .table-wrap {
        overflow-x: auto;
        max-height: 440px;
        overflow-y: auto;
    }
    .result-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.8rem;
    }
    .result-table th {
        position: sticky;
        top: 0;
        padding: 0.42rem 0.75rem;
        background: var(--bg-raised);
        color: var(--gold-light);
        font-family: var(--font-heading);
        font-size: 0.63rem;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        text-align: left;
        border-bottom: 1px solid var(--border-dark);
        white-space: nowrap;
    }
    .result-table td {
        padding: 0.38rem 0.75rem;
        color: var(--text-base);
        border-bottom: 1px solid var(--border-dark);
        white-space: nowrap;
        font-family: "Consolas", monospace;
    }
    .result-table tr:last-child td {
        border-bottom: none;
    }
    .result-table tr:hover td {
        background: var(--bg-raised);
    }
    .result-table td.null-cell {
        color: var(--text-dim);
        font-style: italic;
    }
    .result-table tr {
        cursor: pointer;
    }
    .row-selected td {
        background: rgba(139, 94, 28, 0.1) !important;
    }

    /* ── Copy toolbar ── */
    .copy-toolbar {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        flex-wrap: wrap;
    }
    .copy-btn {
        font-family: var(--font-heading);
        font-size: 0.6rem;
        letter-spacing: 0.07em;
        text-transform: uppercase;
        padding: 0.2rem 0.55rem;
        border-radius: var(--radius);
        border: 1px solid var(--border-dark);
        background: var(--bg-deep);
        color: var(--text-muted);
        cursor: pointer;
        transition:
            background 0.12s,
            color 0.12s;
        white-space: nowrap;
    }
    .copy-btn:hover:not(:disabled) {
        background: var(--bg-hover);
        color: var(--text-base);
    }
    .copy-btn:disabled {
        opacity: 0.35;
        cursor: not-allowed;
    }
    .copy-range-group {
        display: flex;
        align-items: center;
        gap: 0.3rem;
    }
    .copy-range-label {
        font-size: 0.6rem;
        font-family: var(--font-heading);
        color: var(--text-dim);
        letter-spacing: 0.04em;
    }
    .copy-range-input {
        width: 52px;
        padding: 0.18rem 0.4rem;
        background: var(--bg-deep);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
        color: var(--text-base);
        font-family: var(--font-heading);
        font-size: 0.65rem;
        outline: none;
        text-align: center;
    }
    .copy-range-input:focus {
        border-color: var(--border-gold);
    }
    .copy-feedback {
        font-size: 0.6rem;
        font-family: var(--font-heading);
        color: var(--green-bright);
        letter-spacing: 0.06em;
        text-transform: uppercase;
    }
    .select-col {
        width: 32px;
        padding: 0.3rem 0.5rem !important;
        text-align: center !important;
    }
    .select-col input[type="checkbox"] {
        cursor: pointer;
        accent-color: var(--gold-light);
        width: 13px;
        height: 13px;
    }

    /* ── Saved queries panel ── */
    .saved-panel {
        background: var(--bg-surface);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
        display: flex;
        flex-direction: column;
        max-height: 70vh;
        overflow: hidden;
        position: sticky;
        top: 1rem;
    }

    .saved-panel-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.55rem 0.8rem;
        border-bottom: 1px solid var(--border-dark);
        background: var(--bg-raised);
        flex-shrink: 0;
    }

    .saved-panel-title {
        font-family: var(--font-heading);
        font-size: 0.63rem;
        letter-spacing: 0.1em;
        text-transform: uppercase;
        color: var(--text-muted);
    }

    .saved-panel-close {
        background: transparent;
        border: none;
        color: var(--text-dim);
        font-size: 1rem;
        cursor: pointer;
        line-height: 1;
        padding: 0 0.2rem;
        transition: color 0.1s;
    }
    .saved-panel-close:hover {
        color: var(--text-base);
    }

    .saved-panel-body {
        overflow-y: auto;
        flex: 1;
        padding: 0.4rem 0;
    }

    .saved-state {
        padding: 1rem 0.9rem;
        font-size: 0.75rem;
        line-height: 1.5;
        text-align: center;
    }
    .saved-state--dim {
        color: var(--text-dim);
    }
    .saved-state--err {
        color: var(--red-light);
    }
    .saved-state strong {
        color: var(--text-muted);
    }

    .saved-item {
        display: flex;
        align-items: center;
        gap: 0;
        border-bottom: 1px solid var(--border-dark);
    }
    .saved-item:last-child {
        border-bottom: none;
    }

    .saved-item-name {
        flex: 1;
        text-align: left;
        background: transparent;
        border: none;
        color: var(--text-base);
        font-size: 0.78rem;
        font-family: var(--font-heading);
        padding: 0.55rem 0.8rem;
        cursor: pointer;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        transition:
            background 0.1s,
            color 0.1s;
    }
    .saved-item-name:hover {
        background: var(--bg-raised);
        color: var(--gold-light);
    }

    .saved-item-del {
        flex-shrink: 0;
        background: transparent;
        border: none;
        color: var(--text-dim);
        font-size: 0.85rem;
        padding: 0.55rem 0.65rem;
        cursor: pointer;
        transition:
            color 0.1s,
            background 0.1s;
        line-height: 1;
    }
    .saved-item-del:hover {
        color: var(--red-light);
        background: rgba(92, 16, 16, 0.15);
    }
</style>
