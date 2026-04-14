<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import {
		questApi,
		type QuestFile,
		type QuestMission,
		type QuestMissionUpdate,
		type MobDropUpdate
	} from '$lib/api/serverApi';
	import { QUEST_NAMES } from '$lib/data/questNames';

	// ── State ────────────────────────────────────────────────────────────────

	let quests:     QuestFile[] = [];
	let total       = 0;
	let totalPages  = 1;
	let page        = 1;
	let search      = '';
	let searchInput = '';

	let loading     = true;
	let loadError   = '';

	let compiling     = false;
	let compileMsg    = '';
	let compileErr    = '';

	let downloading   = false;
	let downloadErr   = '';

	// Per-quest edit state: questName → { dirty missions, saving, msg, err }
	type EditState = {
		missions: QuestMission[];
		saving:   boolean;
		msg:      string;
		err:      string;
	};
	let editMap: Record<string, EditState> = {};

	// Which quest rows are expanded
	let expanded: Record<string, boolean> = {};

	// ── Load ─────────────────────────────────────────────────────────────────

	async function load() {
		loading   = true;
		loadError = '';
		editMap   = {};
		try {
			const res = await questApi.list(page, search);
			quests     = res.quests;
			total      = res.total;
			totalPages = res.totalPages;
			// Init edit state for each quest (deep clone missions)
			for (const q of quests) {
				editMap[q.questName] = {
					missions: deepCloneMissions(q.missions),
					saving:   false,
					msg:      '',
					err:      ''
				};
			}
		} catch (e) {
			loadError = e instanceof Error ? e.message : String(e);
		} finally {
			loading = false;
		}
	}

	onMount(load);

	// ── Search ───────────────────────────────────────────────────────────────

	function handleSearch() {
		search = searchInput.trim();
		page   = 1;
		expanded = {};
		load();
	}

	function handleSearchKey(e: KeyboardEvent) {
		if (e.key === 'Enter') handleSearch();
	}

	function clearSearch() {
		searchInput = '';
		search      = '';
		page        = 1;
		expanded    = {};
		load();
	}

	// ── Pagination ───────────────────────────────────────────────────────────

	function goPage(p: number) {
		page     = p;
		expanded = {};
		load();
	}

	// ── Compile ──────────────────────────────────────────────────────────────

	async function handleDownloadTextdata() {
		downloading  = true;
		downloadErr  = '';
		try {
			await questApi.downloadTextdata();
		} catch (e) {
			downloadErr = e instanceof Error ? e.message : String(e);
		} finally {
			downloading = false;
		}
	}

	async function handleCompile() {
		compiling   = true;
		compileMsg  = '';
		compileErr  = '';
		try {
			const res   = await questApi.compile();
			compileMsg  = res.message;
		} catch (e) {
			compileErr = e instanceof Error ? e.message : String(e);
		} finally {
			compiling = false;
		}
	}

	// ── Per-quest save ───────────────────────────────────────────────────────

	async function handleSave(questName: string) {
		const state = editMap[questName];
		if (!state) return;
		state.saving = true;
		state.msg    = '';
		state.err    = '';
		editMap = editMap; // trigger reactivity

		try {
			const updates: QuestMissionUpdate[] = state.missions.map(m => {
				if (m.type === 'Kill') {
					return {
						missionIndex: m.missionIndex,
						type:         'kill',
						killCount:    m.killCount ?? undefined
					};
				} else {
					return {
						missionIndex: m.missionIndex,
						type:         'gather',
						collectCount: m.collectCount ?? undefined,
						mobDrops:     m.mobDrops?.map(d => ({
							mobName:    d.mobName,
							dropChance: d.dropChance
						})) as MobDropUpdate[]
					};
				}
			});

			const res    = await questApi.update(questName, updates);
			state.msg    = res.message;
		} catch (e) {
			state.err = e instanceof Error ? e.message : String(e);
		} finally {
			state.saving = false;
			editMap = editMap;
		}
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	function deepCloneMissions(missions: QuestMission[]): QuestMission[] {
		return missions.map(m => ({
			...m,
			mobDrops: m.mobDrops ? m.mobDrops.map(d => ({ ...d })) : null
		}));
	}

	function missionLabel(m: QuestMission): string {
		if (m.type === 'Kill') return `[${m.missionIndex}] Kill — ${m.monsterName} (${m.monsterClass})`;
		return `[${m.missionIndex}] Gather — ${m.itemName}`;
	}

	function toggleExpand(questName: string) {
		expanded[questName] = !expanded[questName];
		expanded = expanded;
	}

	function killCountChange(questName: string, mIdx: number, val: string) {
		const state = editMap[questName];
		if (!state) return;
		const m = state.missions.find(x => x.missionIndex === mIdx);
		if (m) m.killCount = parseInt(val) || 0;
		editMap = editMap;
	}

	function collectCountChange(questName: string, mIdx: number, val: string) {
		const state = editMap[questName];
		if (!state) return;
		const m = state.missions.find(x => x.missionIndex === mIdx);
		if (m) m.collectCount = parseInt(val) || 0;
		editMap = editMap;
	}

	function dropChanceChange(questName: string, mIdx: number, mobName: string, val: string) {
		const state = editMap[questName];
		if (!state) return;
		const m = state.missions.find(x => x.missionIndex === mIdx);
		if (!m?.mobDrops) return;
		const drop = m.mobDrops.find(d => d.mobName === mobName);
		if (drop) drop.dropChance = parseFloat(val) || 0;
		editMap = editMap;
	}
</script>

<PageHeader title="Quests" subtitle="Edit kill counts and gather rates across all quest lua files" />

<div class="page">

	<!-- ── Toolbar ── -->
	<div class="toolbar">
		<div class="search-row">
			<input
				class="search-input"
				type="text"
				placeholder="Search quest name…"
				bind:value={searchInput}
				on:keydown={handleSearchKey}
			/>
			<SrButton variant="secondary" on:click={handleSearch}>Search</SrButton>
			{#if search}
				<SrButton variant="ghost" on:click={clearSearch}>Clear</SrButton>
			{/if}
			<span class="result-count">{total} quest{total !== 1 ? 's' : ''}{search ? ` matching "${search}"` : ''}</span>
		</div>

		<div class="compile-row">
			<SrButton variant="primary" loading={compiling} disabled={compiling} on:click={handleCompile}>
				Compile &amp; Stage
			</SrButton>
			<SrButton variant="secondary" loading={downloading} disabled={downloading} on:click={handleDownloadTextdata}>
				Download Textdata
			</SrButton>
			{#if compileMsg}
				<span class="inline-msg inline-msg--ok">{compileMsg}</span>
			{/if}
			{#if compileErr}
				<span class="inline-msg inline-msg--err">{compileErr}</span>
			{/if}
			{#if downloadErr}
				<span class="inline-msg inline-msg--err">{downloadErr}</span>
			{/if}
		</div>
	</div>

	<!-- ── Body ── -->
	{#if loading}
		<p class="state-text">Loading quests…</p>

	{:else if loadError}
		<div class="msg msg--error">{loadError}</div>

	{:else if quests.length === 0}
		<p class="state-text">No quests found{search ? ` for "${search}"` : ''}.</p>

	{:else}
		<div class="quest-list">
			{#each quests as q (q.questName)}
				{@const state = editMap[q.questName]}
				{@const isOpen = !!expanded[q.questName]}

				<div class="quest-card" class:quest-card--open={isOpen}>

					<!-- Header row -->
					<button class="quest-header" on:click={() => toggleExpand(q.questName)}>
						<span class="quest-name">
							{#if QUEST_NAMES[q.questName]}
								<span class="quest-display-name">{QUEST_NAMES[q.questName]}</span>
								<span class="quest-code">{q.questName}</span>
							{:else}
								{q.questName}
							{/if}
						</span>
						<span class="quest-meta">{q.missions.length} mission{q.missions.length !== 1 ? 's' : ''}</span>
						<span class="quest-caret" class:quest-caret--open={isOpen}>▾</span>
					</button>

					<!-- Expanded missions -->
					{#if isOpen && state}
						<div class="quest-body">
							{#each state.missions as m (m.missionIndex)}
								<div class="mission">
									<div class="mission-label">{missionLabel(m)}</div>

									{#if m.type === 'Kill'}
										<div class="mission-fields">
											<div class="mfield">
												<label class="mfield__label">Kill Count</label>
												<input
													class="mfield__input"
													type="number"
													min="1"
													value={m.killCount ?? ''}
													on:input={e => killCountChange(q.questName, m.missionIndex, e.currentTarget.value)}
												/>
											</div>
										</div>

									{:else if m.type === 'Gather'}
										<div class="mission-fields">
											<div class="mfield">
												<label class="mfield__label">Collect Count</label>
												<input
													class="mfield__input"
													type="number"
													min="1"
													value={m.collectCount ?? ''}
													on:input={e => collectCountChange(q.questName, m.missionIndex, e.currentTarget.value)}
												/>
											</div>
										</div>

										{#if m.mobDrops && m.mobDrops.length > 0}
											<div class="mob-table">
												<div class="mob-table__head">
													<span>Monster</span>
													<span>Drop Chance (%)</span>
												</div>
												{#each m.mobDrops as drop (drop.mobName)}
													<div class="mob-row">
														<span class="mob-name">{drop.mobName}</span>
														<input
															class="mfield__input mfield__input--sm"
															type="number"
															min="0"
															max="100"
															step="0.01"
															value={drop.dropChance}
															on:input={e => dropChanceChange(q.questName, m.missionIndex, drop.mobName, e.currentTarget.value)}
														/>
													</div>
												{/each}
											</div>
										{/if}
									{/if}
								</div>
							{/each}

							<!-- Save row -->
							<div class="quest-footer">
								<SrButton
									variant="primary"
									loading={state.saving}
									disabled={state.saving}
									on:click={() => handleSave(q.questName)}
								>
									Save
								</SrButton>
								{#if state.msg}
									<span class="inline-msg inline-msg--ok">{state.msg}</span>
								{/if}
								{#if state.err}
									<span class="inline-msg inline-msg--err">{state.err}</span>
								{/if}
							</div>
						</div>
					{/if}
				</div>
			{/each}
		</div>

		<!-- ── Pagination ── -->
		{#if totalPages > 1}
			<div class="pagination">
				<button
					class="page-btn"
					disabled={page <= 1}
					on:click={() => goPage(page - 1)}
				>‹ Prev</button>

				{#each Array.from({ length: totalPages }, (_, i) => i + 1) as p}
					{#if p === 1 || p === totalPages || Math.abs(p - page) <= 2}
						<button
							class="page-btn"
							class:page-btn--active={p === page}
							on:click={() => goPage(p)}
						>{p}</button>
					{:else if Math.abs(p - page) === 3}
						<span class="page-ellipsis">…</span>
					{/if}
				{/each}

				<button
					class="page-btn"
					disabled={page >= totalPages}
					on:click={() => goPage(page + 1)}
				>Next ›</button>
			</div>
		{/if}
	{/if}
</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1.2rem;
	}

	/* ── Toolbar ── */
	.toolbar {
		display: flex;
		flex-direction: column;
		gap: 0.7rem;
	}

	.search-row,
	.compile-row {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		flex-wrap: wrap;
	}

	.search-input {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		color: var(--text-base);
		border-radius: var(--radius);
		padding: 0.42rem 0.65rem;
		font-family: var(--font-mono);
		font-size: 0.76rem;
		width: 280px;
		transition: border-color 0.15s;
	}
	.search-input:focus {
		outline: none;
		border-color: var(--border-accent);
	}
	.search-input::placeholder { color: var(--text-dim); }

	.result-count {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		color: var(--text-dim);
		letter-spacing: 0.05em;
	}

	.inline-msg {
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.04em;
	}
	.inline-msg--ok  { color: var(--green-bright); }
	.inline-msg--err { color: var(--red-light); }

	/* ── Quest list ── */
	.quest-list {
		display: flex;
		flex-direction: column;
		gap: 4px;
	}

	.quest-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
		transition: border-color 0.15s;
	}
	.quest-card--open {
		border-color: var(--border-gold);
	}

	.quest-header {
		width: 100%;
		display: flex;
		align-items: center;
		gap: 0.7rem;
		padding: 0.55rem 0.9rem;
		background: none;
		border: none;
		cursor: pointer;
		text-align: left;
		color: var(--text-base);
		transition: background 0.12s;
	}
	.quest-header:hover {
		background: var(--bg-raised);
	}

	.quest-name {
		font-family: var(--font-mono);
		font-size: 0.78rem;
		color: var(--text-bright);
		flex: 1;
		display: flex;
		flex-direction: column;
		gap: 0.05rem;
	}

	.quest-display-name {
		font-family: var(--font-body);
		font-size: 0.84rem;
		color: var(--text-bright);
	}

	.quest-code {
		font-family: var(--font-mono);
		font-size: 0.65rem;
		color: var(--text-dim);
	}

	.quest-meta {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		color: var(--text-dim);
		letter-spacing: 0.07em;
	}

	.quest-caret {
		font-size: 0.85rem;
		color: var(--text-muted);
		transition: transform 0.15s;
	}
	.quest-caret--open {
		transform: rotate(180deg);
	}

	/* ── Quest body ── */
	.quest-body {
		border-top: 1px solid var(--border-dark);
		padding: 0.9rem 1rem 0.8rem;
		display: flex;
		flex-direction: column;
		gap: 0.9rem;
	}

	.mission {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.mission-label {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		color: var(--gold-light);
		letter-spacing: 0.03em;
	}

	.mission-fields {
		display: flex;
		flex-wrap: wrap;
		gap: 0.6rem;
	}

	.mfield {
		display: flex;
		flex-direction: column;
		gap: 0.2rem;
	}

	.mfield__label {
		font-family: var(--font-heading);
		font-size: 0.6rem;
		text-transform: uppercase;
		letter-spacing: 0.09em;
		color: var(--text-muted);
	}

	.mfield__input {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		color: var(--text-base);
		border-radius: var(--radius);
		padding: 0.35rem 0.5rem;
		font-family: var(--font-mono);
		font-size: 0.8rem;
		width: 120px;
		transition: border-color 0.15s;
	}
	.mfield__input--sm { width: 90px; }
	.mfield__input:focus {
		outline: none;
		border-color: var(--border-accent);
		color: var(--text-bright);
	}

	/* ── Mob drop table ── */
	.mob-table {
		display: flex;
		flex-direction: column;
		gap: 3px;
	}

	.mob-table__head {
		display: grid;
		grid-template-columns: 1fr 160px;
		font-family: var(--font-heading);
		font-size: 0.6rem;
		text-transform: uppercase;
		letter-spacing: 0.09em;
		color: var(--text-muted);
		padding: 0 0.1rem;
	}

	.mob-row {
		display: grid;
		grid-template-columns: 1fr 160px;
		align-items: center;
		gap: 0.4rem;
		background: var(--bg-raised);
		border-radius: var(--radius);
		padding: 0.3rem 0.5rem;
	}

	.mob-name {
		font-family: var(--font-mono);
		font-size: 0.74rem;
		color: var(--text-base);
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	/* ── Quest footer ── */
	.quest-footer {
		display: flex;
		align-items: center;
		gap: 0.8rem;
		padding-top: 0.3rem;
		border-top: 1px solid var(--border-dark);
		flex-wrap: wrap;
	}

	/* ── State ── */
	.state-text {
		font-size: 0.85rem;
		color: var(--text-muted);
		font-style: italic;
	}

	.msg {
		padding: 0.65rem 0.95rem;
		border-radius: var(--radius);
		border: 1px solid;
		font-size: 0.84rem;
	}
	.msg--error {
		background: rgba(92,16,16,0.25);
		border-color: var(--red-dark);
		color: var(--red-light);
	}

	/* ── Pagination ── */
	.pagination {
		display: flex;
		align-items: center;
		gap: 4px;
		flex-wrap: wrap;
	}

	.page-btn {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		color: var(--text-muted);
		border-radius: var(--radius);
		padding: 0.3rem 0.6rem;
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.05em;
		cursor: pointer;
		transition: background 0.12s, color 0.12s, border-color 0.12s;
	}
	.page-btn:hover:not(:disabled) {
		background: var(--bg-hover);
		color: var(--text-base);
		border-color: var(--border-gold);
	}
	.page-btn--active {
		background: var(--bg-hover);
		border-color: var(--gold);
		color: var(--gold-light);
	}
	.page-btn:disabled {
		opacity: 0.35;
		cursor: default;
	}

	.page-ellipsis {
		font-size: 0.75rem;
		color: var(--text-dim);
		padding: 0 2px;
	}
</style>
