<script lang="ts">
    import PageHeader from "$lib/components/layout/PageHeader.svelte";
    import StatCard from "$lib/components/ui/StatCard.svelte";
    import ModuleCard from "$lib/components/ui/ModuleCard.svelte";
    import StatusBadge from "$lib/components/ui/StatusBadge.svelte";
    import { statusStore } from "$lib/stores/serverStatus";
    import { serverApi } from "$lib/api/serverApi";
    import { onMount } from "svelte";

    $: state = $statusStore;
    $: status = state.data;

    $: moduleCount = status?.modulesOpened ?? 0;
    $: totalModules = status?.moduleStatuses?.length ?? 0;
    $: stage = status?.startupStage ?? "";
    $: isStarting = stage !== "" && stage !== "Running" && stage !== "Error";

    type MsgType = "success" | "error" | "info";

    let runningBots: string[] = [];
    let selectedBot = "";
    let tpX = 0,
        tpY = 0,
        tpZ = 0,
        tpR = 45;
    let tpBusy = false;
    let tpLoading = false;
    let tpMessage = "";
    let tpMsgType: MsgType = "info";

    // Extract short name from "[botPartyOng] SBotP v1.0.38..."
    function extractBotName(title: string): string {
        const match = title.match(/^\[(.+?)\]/);
        return match ? match[1] : title;
    }
    function handleBotSelect(e: Event) {
        selectBot((e.target as HTMLSelectElement).value);
    }
    async function fetchBotStatus() {
        try {
            const res = await serverApi.botStatus();
            runningBots = res.runningBots ?? [];
            if (runningBots.length > 0 && !selectedBot)
                await selectBot(runningBots[0]);
        } catch {
            runningBots = [];
        }
    }

    async function selectBot(title: string) {
        selectedBot = title;
        const name = extractBotName(title);
        tpLoading = true;
        tpMessage = "";
        try {
            const res = await serverApi.getBotTrainplace(name);
            tpX = res.x;
            tpY = res.y;
            tpZ = res.z;
            tpR = res.r;
        } catch (e) {
            tpMessage = e instanceof Error ? e.message : String(e);
            tpMsgType = "error";
        } finally {
            tpLoading = false;
        }
    }

    async function handleSetTrainplace() {
        if (!selectedBot) return;
        const name = extractBotName(selectedBot);
        tpBusy = true;
        tpMessage = "";
        try {
            const res = await serverApi.setBotTrainplace(
                name,
                tpX,
                tpY,
                tpZ,
                tpR,
            );
            tpMessage = res.message;
            tpMsgType = "success";
        } catch (e) {
            tpMessage = e instanceof Error ? e.message : String(e);
            tpMsgType = "error";
        } finally {
            tpBusy = false;
        }
    }

    onMount(() => {
        fetchBotStatus();
    });

    function fmtTime(d: Date | null) {
        if (!d) return "—";
        return d.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
    }

    $: pctOperational =
        totalModules > 0 ? Math.round((moduleCount / totalModules) * 100) : 0;
</script>

<PageHeader title="Dashboard" subtitle="Live server status overview">
    <svelte:fragment slot="actions">
        {#if status}
            <StatusBadge
                running={status.isRunning}
                label={isStarting
                    ? "Starting…"
                    : status.isRunning
                      ? "Online"
                      : "Offline"}
                size="md"
            />
        {/if}
        <span class="updated">
            {state.lastUpdated
                ? `Updated ${fmtTime(state.lastUpdated)}`
                : "Connecting…"}
        </span>
    </svelte:fragment>
</PageHeader>

<div class="page">
    <!-- API error banner -->
    {#if state.error}
        <div class="alert alert--error">
            <strong>API Unreachable</strong> — {state.error}
        </div>
    {/if}

    <!-- Startup stage bar -->
    {#if isStarting}
        <div class="stage-bar">
            <span class="stage-bar__label">Stage</span>
            <span class="stage-bar__value">{stage}</span>
            <span class="stage-bar__spinner"></span>
        </div>
    {/if}

    <!-- ── Stat row ── -->
    <div class="stats-row">
        <StatCard
            label="Server Status"
            value={status?.isRunning ? "Online" : "Offline"}
            variant={status?.isRunning ? "green" : "red"}
        />
        <StatCard
            label="Modules Running"
            value="{moduleCount} / {totalModules}"
            sublabel="{pctOperational}% operational"
            variant={moduleCount === totalModules && totalModules > 0
                ? "green"
                : moduleCount > 0
                  ? "gold"
                  : "red"}
        />
        <StatCard
            label="Startup Stage"
            value={stage || (status ? "Idle" : "—")}
            variant={isStarting ? "gold" : "default"}
        />
    </div>

    <!-- ── Module grid ── -->
    <section class="section">
        <h2 class="section__title">Module Status</h2>

        {#if status?.moduleStatuses?.length}
            <div class="module-grid">
                {#each status.moduleStatuses as mod (mod.name)}
                    <ModuleCard module={mod} />
                {/each}
            </div>
        {:else if !state.error}
            <div class="empty">
                <span class="empty__text">Waiting for server data…</span>
            </div>
        {/if}
    </section>

    <!-- ── Bot Controls ── -->
    <section class="section">
        <h2 class="section__title">Bot Controls</h2>

        {#if runningBots.length === 0}
            <div class="empty">
                <span class="empty__text">No bots currently running.</span>
            </div>
        {:else}
            <div class="bot-ctrl-card">
                <div class="bot-ctrl-card__title">Trainplace 1</div>
                <p class="bot-ctrl-card__desc">
                    Select a bot to view and update its training coordinates.
                    Changes are saved immediately to the bot.
                </p>

                <div class="bot-ctrl-form">
                    <!-- Bot selector -->
                    <div class="bot-ctrl-field">
                        <label class="bot-ctrl-label">Bot</label>
                        <select
                            class="bot-ctrl-input bot-ctrl-select"
                            disabled={tpBusy || tpLoading}
                            on:change={handleBotSelect}
                        >
                            {#each runningBots as bot}
                                <option
                                    value={bot}
                                    selected={bot === selectedBot}
                                >
                                    {extractBotName(bot)}
                                </option>
                            {/each}
                        </select>
                    </div>

                    <!-- Coord fields -->
                    {#if tpLoading}
                        <div class="bot-loading">Loading coordinates...</div>
                    {:else}
                        <div class="bot-ctrl-coords">
                            <div class="bot-ctrl-field">
                                <label class="bot-ctrl-label">X</label>
                                <input
                                    class="bot-ctrl-input"
                                    type="number"
                                    bind:value={tpX}
                                    disabled={tpBusy}
                                />
                            </div>
                            <div class="bot-ctrl-field">
                                <label class="bot-ctrl-label">Y</label>
                                <input
                                    class="bot-ctrl-input"
                                    type="number"
                                    bind:value={tpY}
                                    disabled={tpBusy}
                                />
                            </div>
                            <div class="bot-ctrl-field">
                                <label class="bot-ctrl-label">Z</label>
                                <input
                                    class="bot-ctrl-input"
                                    type="number"
                                    bind:value={tpZ}
                                    disabled={tpBusy}
                                />
                            </div>
                            <div class="bot-ctrl-field">
                                <label class="bot-ctrl-label">R</label>
                                <input
                                    class="bot-ctrl-input"
                                    type="number"
                                    bind:value={tpR}
                                    disabled={tpBusy}
                                />
                            </div>
                        </div>

                        <div class="bot-ctrl-actions">
                            <button
                                class="bot-ctrl-btn"
                                disabled={tpBusy || !selectedBot}
                                on:click={handleSetTrainplace}
                            >
                                {tpBusy ? "Saving..." : "▶ Set Trainplace"}
                            </button>
                        </div>
                    {/if}

                    {#if tpMessage}
                        <div class="bot-msg bot-msg--{tpMsgType}">
                            {tpMessage}
                        </div>
                    {/if}
                </div>
            </div>
        {/if}
    </section>
</div>

<style>
    .page {
        padding: 1.4rem 1.5rem;
        display: flex;
        flex-direction: column;
        gap: 1.4rem;
    }

    /* ── Alert ── */
    .alert {
        padding: 0.7rem 1rem;
        border-radius: var(--radius);
        border: 1px solid;
        font-size: 0.84rem;
    }
    .alert--error {
        background: rgba(92, 16, 16, 0.22);
        border-color: var(--red-dark);
        color: var(--red-light);
    }
    .alert--error strong {
        font-family: var(--font-heading);
        letter-spacing: 0.04em;
    }

    /* ── Stage bar ── */
    .stage-bar {
        display: flex;
        align-items: center;
        gap: 0.7rem;
        padding: 0.55rem 0.9rem;
        background: rgba(106, 90, 140, 0.1);
        border: 1px solid var(--border-gold);
        border-radius: var(--radius);
        font-size: 0.82rem;
    }
    .stage-bar__label {
        font-family: var(--font-heading);
        font-size: 0.65rem;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: var(--text-muted);
    }
    .stage-bar__value {
        font-family: var(--font-heading);
        color: var(--gold-light);
        letter-spacing: 0.05em;
        flex: 1;
    }
    .stage-bar__spinner {
        width: 12px;
        height: 12px;
        border: 2px solid var(--gold-dim);
        border-top-color: var(--gold);
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
        to {
            transform: rotate(360deg);
        }
    }

    /* ── Stats ── */
    .stats-row {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
        gap: 1rem;
    }

    /* ── Modules ── */
    .section__title {
        font-size: 0.65rem;
        text-transform: uppercase;
        letter-spacing: 0.14em;
        color: var(--text-muted);
        margin-bottom: 0.7rem;
        padding-bottom: 0.45rem;
        border-bottom: 1px solid var(--border-dark);
        font-family: var(--font-heading);
    }

    .module-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(155px, 1fr));
        gap: 0.7rem;
    }

    /* ── Misc ── */
    .empty {
        padding: 2.5rem;
        text-align: center;
    }
    .empty__text {
        font-size: 0.84rem;
        color: var(--text-dim);
        font-style: italic;
    }

    .updated {
        font-size: 0.65rem;
        color: var(--text-dim);
        letter-spacing: 0.04em;
    }
    /* ── Bot Controls ── */
    .bot-ctrl-card {
        background: var(--bg-surface);
        border: 1px solid var(--border-dark);
        border-top: 2px solid var(--border-gold);
        border-radius: var(--radius);
        padding: 1.2rem 1.4rem;
    }

    .bot-ctrl-card__title {
        font-family: var(--font-heading);
        font-size: 0.82rem;
        letter-spacing: 0.1em;
        text-transform: uppercase;
        color: var(--gold-light);
        margin-bottom: 0.4rem;
    }

    .bot-ctrl-card__desc {
        font-size: 0.82rem;
        color: var(--text-muted);
        line-height: 1.6;
        margin-bottom: 1rem;
    }

    .bot-ctrl-form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .bot-ctrl-row {
        display: flex;
        align-items: center;
        gap: 0.6rem;
    }

    .bot-ctrl-coords {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 0.6rem;
    }

    .bot-ctrl-field {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .bot-ctrl-label {
        font-family: var(--font-heading);
        font-size: 0.62rem;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: var(--text-muted);
    }

    .bot-ctrl-input {
        background: var(--bg-raised);
        border: 1px solid var(--border-mid);
        border-radius: var(--radius);
        padding: 0.35rem 0.55rem;
        color: var(--text-base);
        font-family: var(--font-mono);
        font-size: 0.84rem;
        outline: none;
        width: 100%;
        box-sizing: border-box;
        transition: border-color 0.15s;
    }
    .bot-ctrl-input:focus {
        border-color: var(--border-accent);
    }
    .bot-ctrl-input:disabled {
        opacity: 0.5;
    }

    .bot-ctrl-actions {
        display: flex;
        gap: 0.6rem;
    }

    .bot-ctrl-btn {
        background: var(--bg-raised);
        border: 1px solid var(--border-gold);
        color: var(--gold-light);
        border-radius: var(--radius);
        padding: 0.4rem 1rem;
        font-family: var(--font-heading);
        font-size: 0.78rem;
        letter-spacing: 0.06em;
        cursor: pointer;
        transition: background 0.15s;
    }
    .bot-ctrl-btn:hover {
        background: rgba(106, 90, 140, 0.2);
    }
    .bot-ctrl-btn:disabled {
        opacity: 0.4;
        cursor: default;
    }

    .bot-msg {
        padding: 0.5rem 0.8rem;
        border-radius: var(--radius);
        border: 1px solid;
        font-family: var(--font-heading);
        font-size: 0.76rem;
        letter-spacing: 0.04em;
    }
    .bot-msg--success {
        background: rgba(21, 45, 12, 0.4);
        border-color: var(--green);
        color: var(--green-bright);
    }
    .bot-msg--error {
        background: rgba(92, 16, 16, 0.3);
        border-color: var(--red-dark);
        color: var(--red-light);
    }
    .bot-msg--info {
        background: rgba(95, 75, 130, 0.15);
        border-color: var(--border-gold);
        color: var(--gold);
    }

    .bot-ctrl-select {
        appearance: none;
        cursor: pointer;
    }

    .bot-loading {
        font-family: var(--font-heading);
        font-size: 0.75rem;
        color: var(--text-muted);
        letter-spacing: 0.06em;
        padding: 0.4rem 0;
    }
</style>
