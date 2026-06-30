<script lang="ts">
	import { onMount, onDestroy, tick } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import { logsApi, serverApi, type ModuleDebugStatus } from '$lib/api/serverApi';


	// ── Types ──────────────────────────────────────────────────────────────────
	type Level = 'INFO' | 'WARN' | 'ERROR' | 'DEBUG' | 'TRACE' | 'UNKNOWN';

	interface LogEntry {
		id: number;      // monotonic — used as the {#each} key to prevent duplicates
		raw: string;
		timestamp: string;
		source: string;
		level: Level;
		message: string;
	}

	// Matches: [2026-03-24 12:00:00.123] [SourceName] Level: message
	// Supports Info, Warn, Error, Debug, Trace in any case (Info, INFO, info, etc.)
	// The `s` flag makes `.` match newlines so multiline exception bodies are captured.
	const LOG_RE = /^\[([^\]]+)\] \[([^\]]+)\] (Info|Warn|Error|Debug|Trace|INFO|WARN|ERROR|DEBUG|TRACE):\s*([\s\S]*)$/;

	let _seq = 0;
	function parse(raw: string): LogEntry {
		const m = LOG_RE.exec(raw);
		if (!m) return { id: ++_seq, raw, timestamp: '', source: '', level: 'UNKNOWN', message: raw };
		const level = m[3].toUpperCase() as Level;
		return { id: ++_seq, raw, timestamp: m[1], source: m[2], level, message: m[4] };
	}

	// ── State ──────────────────────────────────────────────────────────────────
	const MAX_ENTRIES = 1000;

	let entries: LogEntry[] = [];
	let since       = 0;
	let connected   = false;
	let lastUpdated = '';
	let pollError   = '';

	// Filters
	let showInfo  = true;
	let showWarn  = true;
	let showError = true;
	let showDebug = true;
	let showTrace = true;
	let search    = '';

	// Auto-scroll
	let listEl: HTMLDivElement;
	let autoScroll = true;

	// ── Filtering ──────────────────────────────────────────────────────────────
	$: filtered = entries.filter(e => {
        if (e.level === 'INFO' && !showInfo) return false;
        if (e.level === 'WARN' && !showWarn) return false;
        if (e.level === 'ERROR' && !showError) return false;
        if (e.level === 'DEBUG' && !showDebug) return false;
        if (e.level === 'TRACE' && !showTrace) return false;

        if (search && search.trim().length > 0) {
            const raw = e.raw.toLowerCase();

            // split into multiple targets: supports "||", "," and spaces
            const targets = search
                .toLowerCase()
                .split(/(\|\||,|\s+)/)   // split on || , or spaces
                .map(s => s.trim())
                .filter(Boolean);

            // OR match: any target must exist
            if (!targets.some(t => raw.includes(t))) {
                return false;
            }
        }

        return true;
    });

	// ── Polling ────────────────────────────────────────────────────────────────
	let _pollInFlight = false;

	async function poll() {
		if (_pollInFlight) return;   // drop tick if previous request is still in-flight
		_pollInFlight = true;
		try {
			const data = await logsApi.get(since);
			pollError = '';
			connected = true;
			lastUpdated = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });

			if (data.entries.length === 0) return;

			// If total < since, history was flushed to disk — start over
			if (data.total < since) {
				since   = 0;
				entries = data.entries.map(parse);
			} else {
				const newEntries = data.entries.map(parse);
				entries = [...entries, ...newEntries].slice(-MAX_ENTRIES);
				since   = data.total;
			}

			if (autoScroll) {
				await tick();
				listEl?.scrollTo({ top: listEl.scrollHeight, behavior: 'smooth' });
			}
		} catch (e: any) {
			connected = false;
			pollError = e.message;
		} finally {
			_pollInFlight = false;
		}
	}

	function handleScroll() {
		if (!listEl) return;
		const atBottom = listEl.scrollHeight - listEl.scrollTop - listEl.clientHeight < 40;
		autoScroll = atBottom;
	}

	function scrollToBottom() {
		autoScroll = true;
		listEl?.scrollTo({ top: listEl.scrollHeight, behavior: 'smooth' });
	}

	let clearBusy = false;

	async function clearView() {
		clearBusy = true;
		try {
			await logsApi.clear();
		} catch {
			// best effort — still reset local view even if API fails
		} finally {
			clearBusy = false;
		}
		entries = [];
		since   = 0;
	}

	let interval: ReturnType<typeof setInterval>;

	// ── Opcode logging ─────────────────────────────────────────────────────────
	let trackedOpcodes: string[] = [];
	let opcodeInput   = '';
	let opcodeError   = '';
	let opcodeAdding  = false;

	function parseOpcode(input: string): number | null {
		const t = input.trim();
		if (!t) return null;
		const val = t.startsWith('0x') || t.startsWith('0X')
			? parseInt(t, 16)
			: parseInt(t, 10);
		if (isNaN(val) || val < 0 || val > 65535) return null;
		return val;
	}

	function toHex(n: number): string {
		return '0x' + n.toString(16).toUpperCase().padStart(4, '0');
	}

	async function addOpcode() {
		const val = parseOpcode(opcodeInput);
		if (val === null) { opcodeError = 'Invalid opcode — use decimal or 0x hex.'; return; }
		const hex = toHex(val);
		if (trackedOpcodes.includes(hex)) { opcodeError = `${hex} is already tracked.`; return; }
		opcodeAdding = true;
		opcodeError  = '';
		try {
			await serverApi.addLogOpcode(opcodeInput.trim());
			trackedOpcodes = [...trackedOpcodes, hex];
			opcodeInput    = '';
		} catch (e: any) {
			opcodeError = e.message;
		} finally {
			opcodeAdding = false;
		}
	}

	async function removeOpcode(hex: string) {
		opcodeError = '';
		try {
			await serverApi.removeLogOpcode(hex);
			trackedOpcodes = trackedOpcodes.filter(op => op !== hex);
		} catch (e: any) {
			opcodeError = e.message;
		}
	}

	async function clearAllOpcodes() {
		opcodeError = '';
		try {
			await serverApi.clearLogOpcodes();
			trackedOpcodes = [];
		} catch (e: any) {
			opcodeError = e.message;
		}
	}

	function opcodeKeydown(e: KeyboardEvent) {
		if (e.key === 'Enter') addOpcode();
	}

	// ── Module debug mode ──────────────────────────────────────────────────────
	let allModules: ModuleDebugStatus[] = [];
	let moduleSearch = '';
	let moduleError = '';
	let moduleUpdating: Set<string> = new Set();

	$: filteredModules = allModules.filter(m => {
		if (!moduleSearch.trim()) return true;
		return m.name.toLowerCase().includes(moduleSearch.toLowerCase());
	});

	async function toggleModuleDebug(moduleName: string) {
		const module = allModules.find(m => m.name === moduleName);
		if (!module) return;

		const newValue = !module.debugEnabled;
		moduleUpdating.add(moduleName);
		moduleUpdating = moduleUpdating;
		moduleError = '';

		try {
			await serverApi.setModuleDebug(moduleName, newValue);
			module.debugEnabled = newValue;
			allModules = allModules;
		} catch (e: any) {
			moduleError = e.message;
		} finally {
			moduleUpdating.delete(moduleName);
			moduleUpdating = moduleUpdating;
		}
	}

	async function disableAllModuleDebug() {
		moduleError = '';
		try {
			await serverApi.disableAllModuleDebug();
			allModules = allModules.map(m => ({ ...m, debugEnabled: false }));
		} catch (e: any) {
			moduleError = e.message;
		}
	}

	onMount(async () => {
		try {
			trackedOpcodes = await serverApi.getLogOpcodes();
		} catch {
			// not critical — start empty if fetch fails
		}
		try {
			allModules = await serverApi.getModuleDebug();
		} catch {
			// not critical — start empty if fetch fails
		}
		poll();
		interval = setInterval(poll, 2000);
	});
	onDestroy(() => clearInterval(interval));
</script>

<!-- ── Header ─────────────────────────────────────────────────────────────── -->
<PageHeader title="Logs" subtitle="Live API and server log stream">
	<svelte:fragment slot="actions">
		<span class="live-badge" class:live-badge--on={connected} class:live-badge--off={!connected}>
			<span class="live-dot"></span>
			{connected ? 'Live' : 'Disconnected'}
		</span>
		{#if lastUpdated}
			<span class="updated-ts">{lastUpdated}</span>
		{/if}
	</svelte:fragment>
</PageHeader>

<div class="page">

	<!-- ── Toolbar ────────────────────────────────────────────────────────── -->
	<div class="toolbar">
		<div class="toolbar__filters">
			<label class="filter-chip filter-chip--info" class:filter-chip--off={!showInfo}>
				<input type="checkbox" bind:checked={showInfo} hidden />
				INFO
				<span class="filter-count">{entries.filter(e => e.level === 'INFO').length}</span>
			</label>
			<label class="filter-chip filter-chip--warn" class:filter-chip--off={!showWarn}>
				<input type="checkbox" bind:checked={showWarn} hidden />
				WARN
				<span class="filter-count">{entries.filter(e => e.level === 'WARN').length}</span>
			</label>
			<label class="filter-chip filter-chip--error" class:filter-chip--off={!showError}>
				<input type="checkbox" bind:checked={showError} hidden />
				ERROR
				<span class="filter-count">{entries.filter(e => e.level === 'ERROR').length}</span>
			</label>
			<label class="filter-chip filter-chip--debug" class:filter-chip--off={!showDebug}>
				<input type="checkbox" bind:checked={showDebug} hidden />
				DEBUG
				<span class="filter-count">{entries.filter(e => e.level === 'DEBUG').length}</span>
			</label>
			<label class="filter-chip filter-chip--trace" class:filter-chip--off={!showTrace}>
				<input type="checkbox" bind:checked={showTrace} hidden />
				TRACE
				<span class="filter-count">{entries.filter(e => e.level === 'TRACE').length}</span>
			</label>
		</div>

		<input
			class="search-input"
			type="text"
			placeholder="Filter logs…"
			bind:value={search}
		/>

		<div class="toolbar__actions">
			{#if !autoScroll}
				<button class="tool-btn tool-btn--gold" on:click={scrollToBottom}>↓ Follow</button>
			{/if}
			<button class="tool-btn" disabled={clearBusy} on:click={clearView}>{clearBusy ? 'Clearing…' : 'Clear Logs'}</button>
		</div>
	</div>

	<!-- ── Error banner ───────────────────────────────────────────────────── -->
	{#if pollError}
		<div class="error-banner">{pollError}</div>
	{/if}

	<!-- ── Log list ───────────────────────────────────────────────────────── -->
	<div class="log-wrap" bind:this={listEl} on:scroll={handleScroll}>
		{#if filtered.length === 0}
			<div class="empty">
				{#if entries.length === 0}
					<span>Waiting for log entries…</span>
				{:else}
					<span>No entries match the current filter.</span>
				{/if}
			</div>
		{:else}
			{#each filtered as entry (entry.id)}
				<div class="log-row log-row--{entry.level.toLowerCase()}">
					{#if entry.timestamp}
						<span class="log-ts">{entry.timestamp}</span>
						<span class="log-src">[{entry.source}]</span>
						<span class="log-lvl log-lvl--{entry.level.toLowerCase()}">{entry.level}</span>
						<span class="log-msg">{entry.message}</span>
					{:else}
						<span class="log-msg log-msg--raw">{entry.raw}</span>
					{/if}
				</div>
			{/each}
		{/if}
	</div>

	<!-- ── Opcode logging panel ───────────────────────────────────────────── -->
	<div class="opcode-panel">
		<div class="opcode-panel__header">
			<span class="opcode-panel__title">Opcode Logging</span>
			{#if trackedOpcodes.length > 0}
				<button class="tool-btn tool-btn--danger" on:click={clearAllOpcodes}>Clear All</button>
			{/if}
		</div>
		<div class="opcode-panel__body">
			<div class="opcode-chips">
				{#if trackedOpcodes.length === 0}
					<span class="opcode-empty">No opcodes tracked — add one below</span>
				{:else}
					{#each trackedOpcodes as hex}
						<span class="opcode-chip">
							{hex}
							<button class="opcode-chip__remove" on:click={() => removeOpcode(hex)}>×</button>
						</span>
					{/each}
				{/if}
			</div>
			<div class="opcode-add">
				<input
					class="opcode-input"
					class:opcode-input--error={!!opcodeError}
					placeholder="0x1234 or 4660"
					bind:value={opcodeInput}
					on:keydown={opcodeKeydown}
					disabled={opcodeAdding}
				/>
				<button class="tool-btn tool-btn--gold" on:click={addOpcode} disabled={opcodeAdding}>
					{opcodeAdding ? '…' : 'Add'}
				</button>
			</div>
			{#if opcodeError}
				<span class="opcode-err">{opcodeError}</span>
			{/if}
		</div>
	</div>

	<!-- ── Module debug mode panel ──────────────────────────────────────────── -->
	<div class="module-debug-panel">
		<div class="module-debug-panel__header">
			<span class="module-debug-panel__title">Module Debug Mode</span>
			{#if allModules.some(m => m.debugEnabled)}
				<button class="tool-btn tool-btn--danger" on:click={disableAllModuleDebug}>Disable All</button>
			{/if}
		</div>
		<div class="module-debug-panel__body">
			<input
				class="module-search"
				type="text"
				placeholder="Search modules…"
				bind:value={moduleSearch}
			/>
			<div class="module-list">
				{#if allModules.length === 0}
					<span class="module-empty">No modules available</span>
				{:else if filteredModules.length === 0}
					<span class="module-empty">No modules match search</span>
				{:else}
					{#each filteredModules as module (module.name)}
						<div class="module-item">
							<button
								class="module-item__toggle"
								class:module-item__toggle--active={module.debugEnabled}
								disabled={moduleUpdating.has(module.name)}
								on:click={() => toggleModuleDebug(module.name)}
								title={module.debugEnabled ? 'Debug enabled' : 'Debug disabled'}
							>
								{#if moduleUpdating.has(module.name)}
									<span class="module-item__spinner"></span>
								{:else}
									<span class="module-item__indicator" class:module-item__indicator--on={module.debugEnabled}></span>
								{/if}
							</button>
							<span class="module-item__name">{module.name}</span>
						</div>
					{/each}
				{/if}
			</div>
			{#if moduleError}
				<span class="module-err">{moduleError}</span>
			{/if}
		</div>
	</div>

	<div class="status-bar">
		<span>{filtered.length} / {entries.length} entries shown</span>
		{#if entries.length >= MAX_ENTRIES}
			<span class="status-bar__cap">Capped at {MAX_ENTRIES} — oldest entries dropped</span>
		{/if}
	</div>

</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	/* ── Live badge ── */
	.live-badge {
		display: flex;
		align-items: center;
		gap: 0.4rem;
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		padding: 0.22rem 0.6rem;
		border-radius: var(--radius);
		border: 1px solid transparent;
	}
	.live-badge--on  { color: var(--green-bright); border-color: rgba(0,200,100,0.25); background: rgba(0,200,100,0.07); }
	.live-badge--off { color: var(--red-light);    border-color: var(--red-dark);      background: rgba(92,16,16,0.12); }

	.live-dot {
		width: 6px; height: 6px;
		border-radius: 50%;
		flex-shrink: 0;
	}
	.live-badge--on  .live-dot { background: var(--green-bright); box-shadow: 0 0 5px var(--green-bright); animation: pulse 2s ease-in-out infinite; }
	.live-badge--off .live-dot { background: var(--red-light); }

	@keyframes pulse {
		0%, 100% { opacity: 1; }
		50%       { opacity: 0.4; }
	}

	.updated-ts {
		font-size: 0.63rem;
		color: var(--text-dim);
		letter-spacing: 0.04em;
	}

	/* ── Toolbar ── */
	.toolbar {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		flex-wrap: wrap;
	}

	.toolbar__filters {
		display: flex;
		gap: 0.35rem;
	}

	.filter-chip {
		display: flex;
		align-items: center;
		gap: 0.35rem;
		padding: 0.22rem 0.6rem;
		border-radius: var(--radius);
		border: 1px solid var(--border-dark);
		font-family: var(--font-heading);
		font-size: 0.63rem;
		letter-spacing: 0.08em;
		cursor: pointer;
		user-select: none;
		transition: opacity 0.15s, background 0.15s;
	}
	.filter-chip--info  { background: rgba(100,160,255,0.08); border-color: rgba(100,160,255,0.25); color: #93c5fd; }
	.filter-chip--warn  { background: rgba(220,160, 20,0.08); border-color: rgba(220,160, 20,0.3);  color: #fcd34d; }
	.filter-chip--error { background: rgba(220, 50, 50,0.08); border-color: rgba(220, 50, 50,0.3);  color: var(--red-light); }
	.filter-chip--debug { background: rgba(150,130,200,0.08); border-color: rgba(150,130,200,0.25); color: #d8b4fe; }
	.filter-chip--trace { background: rgba(100,200,180,0.08); border-color: rgba(100,200,180,0.25); color: #7ee8c1; }
	.filter-chip--off   { opacity: 0.35; }

	.filter-count {
		font-size: 0.6rem;
		opacity: 0.7;
	}

	.search-input {
		flex: 1;
		min-width: 140px;
		max-width: 280px;
		padding: 0.3rem 0.65rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		color: var(--text-base);
		font-size: 0.78rem;
		font-family: var(--font-body);
		outline: none;
	}
	.search-input:focus { border-color: var(--border-gold); }
	.search-input::placeholder { color: var(--text-dim); }

	.toolbar__actions { display: flex; gap: 0.4rem; margin-left: auto; }

	.tool-btn {
		padding: 0.28rem 0.75rem;
		font-family: var(--font-heading);
		font-size: 0.63rem;
		letter-spacing: 0.07em;
		text-transform: uppercase;
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		background: var(--bg-raised);
		color: var(--text-muted);
		cursor: pointer;
		transition: background 0.15s, color 0.15s;
	}
	.tool-btn:hover { background: var(--bg-hover); color: var(--text-base); }
	.tool-btn--gold { border-color: var(--border-gold); background: var(--gold-dim); color: var(--text-bright); }
	.tool-btn--gold:hover { background: var(--gold); }

	/* ── Error banner ── */
	.error-banner {
		padding: 0.5rem 0.8rem;
		background: rgba(92,16,16,0.2);
		border: 1px solid var(--red-dark);
		border-radius: var(--radius);
		color: var(--red-light);
		font-size: 0.78rem;
	}

	/* ── Log list ── */
	.log-wrap {
		height: 650px;
		overflow-y: auto;
		background: var(--bg-deep);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 0.4rem 0;
		font-family: 'Consolas', 'Cascadia Code', 'Monaco', monospace;
		font-size: 0.78rem;
		line-height: 1.55;
	}

	.empty {
		padding: 2rem;
		text-align: center;
		color: var(--text-dim);
		font-style: italic;
		font-family: var(--font-body);
		font-size: 0.84rem;
	}

	.log-row {
		display: flex;
		align-items: baseline;
		gap: 0.4rem;
		padding: 0.12rem 0.75rem;
		border-bottom: 1px solid transparent;
		transition: background 0.08s;
	}
	.log-row:hover { background: rgba(255,255,255,0.03); }
	.log-row--error { background: rgba(92,16,16,0.12); }
	.log-row--warn  { background: rgba(95,75,130,0.08); }
	.log-row--debug { background: rgba(60,40,80,0.08); }
	.log-row--trace { background: rgba(40,60,60,0.06); }

	.log-ts {
		flex-shrink: 0;
		color: var(--text-dim);
		font-size: 0.72rem;
		min-width: 175px;
	}

	.log-src {
		flex-shrink: 0;
		color: var(--gold-light);
		font-size: 0.72rem;
		min-width: 130px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.log-lvl {
		flex-shrink: 0;
		font-size: 0.67rem;
		font-family: var(--font-heading);
		letter-spacing: 0.06em;
		padding: 0 4px;
		border-radius: 2px;
		min-width: 42px;
		text-align: center;
	}
	.log-lvl--info  { background: rgba(100,160,255,0.15); color: #93c5fd; }
	.log-lvl--warn  { background: rgba(220,160, 20,0.15); color: #fcd34d; }
	.log-lvl--error { background: rgba(220, 50, 50,0.2);  color: var(--red-light); }
	.log-lvl--debug { background: rgba(150,130,200,0.15); color: #d8b4fe; }
	.log-lvl--trace { background: rgba(100,200,180,0.15); color: #7ee8c1; }

	.log-msg {
		flex: 1;
		color: var(--text-base);
		white-space: pre-wrap;
		word-break: break-word;
	}
	.log-msg--raw { color: var(--text-dim); }

	/* ── Status bar ── */
	.status-bar {
		display: flex;
		align-items: center;
		justify-content: space-between;
		font-size: 0.63rem;
		color: var(--text-dim);
		font-family: var(--font-heading);
		letter-spacing: 0.05em;
		padding: 0 0.1rem;
	}

	.status-bar__cap { color: var(--gold-dim); }

	/* ── Opcode panel ── */
	.opcode-panel {
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 0.6rem 0.75rem;
		display: flex;
		flex-direction: column;
		gap: 0.45rem;
	}

	.opcode-panel__header {
		display: flex;
		align-items: center;
		justify-content: space-between;
	}

	.opcode-panel__title {
		font-family: var(--font-heading);
		font-size: 0.63rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.opcode-panel__body {
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
	}

	.opcode-chips {
		display: flex;
		flex-wrap: wrap;
		gap: 0.35rem;
		min-height: 1.6rem;
		align-items: center;
	}

	.opcode-empty {
		font-size: 0.72rem;
		color: var(--text-dim);
		font-style: italic;
	}

	.opcode-chip {
		display: inline-flex;
		align-items: center;
		gap: 0.25rem;
		padding: 0.18rem 0.5rem 0.18rem 0.6rem;
		background: rgba(100,160,255,0.1);
		border: 1px solid rgba(100,160,255,0.25);
		border-radius: var(--radius);
		font-family: 'Consolas', 'Cascadia Code', monospace;
		font-size: 0.75rem;
		color: #93c5fd;
	}

	.opcode-chip__remove {
		background: none;
		border: none;
		color: var(--text-dim);
		cursor: pointer;
		font-size: 0.9rem;
		line-height: 1;
		padding: 0 0.1rem;
		transition: color 0.1s;
	}
	.opcode-chip__remove:hover { color: var(--red-light); }

	.opcode-add {
		display: flex;
		gap: 0.4rem;
		align-items: center;
	}

	.opcode-input {
		width: 160px;
		padding: 0.28rem 0.6rem;
		background: var(--bg-deep);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		color: var(--text-base);
		font-family: 'Consolas', 'Cascadia Code', monospace;
		font-size: 0.78rem;
		outline: none;
		transition: border-color 0.15s;
	}
	.opcode-input:focus          { border-color: var(--border-gold); }
	.opcode-input--error         { border-color: var(--red-dark); }
	.opcode-input::placeholder   { color: var(--text-dim); }

	.opcode-err {
		font-size: 0.68rem;
		color: var(--red-light);
	}

	.tool-btn--danger {
		border-color: var(--red-dark);
		color: var(--red-light);
	}
	.tool-btn--danger:hover { background: rgba(92,16,16,0.2); }

	/* ── Module debug panel ── */
	.module-debug-panel {
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 0.6rem 0.75rem;
		display: flex;
		flex-direction: column;
		gap: 0.45rem;
	}

	.module-debug-panel__header {
		display: flex;
		align-items: center;
		justify-content: space-between;
	}

	.module-debug-panel__title {
		font-family: var(--font-heading);
		font-size: 0.63rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.module-debug-panel__body {
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
	}

	.module-search {
		width: 100%;
		padding: 0.28rem 0.6rem;
		background: var(--bg-deep);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		color: var(--text-base);
		font-family: var(--font-body);
		font-size: 0.78rem;
		outline: none;
		transition: border-color 0.15s;
	}
	.module-search:focus { border-color: var(--border-gold); }
	.module-search::placeholder { color: var(--text-dim); }

	.module-list {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		max-height: 200px;
		overflow-y: auto;
		padding: 0.35rem 0;
	}

	.module-empty {
		font-size: 0.72rem;
		color: var(--text-dim);
		font-style: italic;
		padding: 0.5rem 0.6rem;
	}

	.module-item {
		display: flex;
		align-items: center;
		gap: 0.4rem;
		padding: 0.28rem 0.5rem;
		border-radius: var(--radius);
		transition: background 0.1s;
	}
	.module-item:hover {
		background: rgba(255,255,255,0.03);
	}

	.module-item__toggle {
		background: none;
		border: none;
		padding: 0;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
		width: 18px;
		height: 18px;
		flex-shrink: 0;
		transition: opacity 0.15s;
	}
	.module-item__toggle:disabled {
		opacity: 0.5;
		cursor: default;
	}
	.module-item__toggle:hover:not(:disabled) {
		opacity: 0.8;
	}

	.module-item__indicator {
		width: 10px;
		height: 10px;
		border-radius: 50%;
		background: var(--border-mid);
		box-shadow: inset 0 1px 2px rgba(0,0,0,0.3);
		transition: background 0.15s;
	}
	.module-item__indicator--on {
		background: var(--green-bright);
		box-shadow: 0 0 6px var(--green-bright), inset 0 1px 2px rgba(0,0,0,0.3);
	}

	.module-item__spinner {
		width: 10px;
		height: 10px;
		border: 1.5px solid rgba(255,255,255,0.2);
		border-top-color: var(--gold);
		border-radius: 50%;
		animation: spin 0.8s linear infinite;
	}

	.module-item__name {
		font-family: 'Consolas', 'Cascadia Code', monospace;
		font-size: 0.76rem;
		color: var(--text-base);
		flex: 1;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	.module-err {
		font-size: 0.68rem;
		color: var(--red-light);
		padding: 0.3rem 0.6rem;
	}

	@keyframes spin {
		to { transform: rotate(360deg); }
	}
</style>
