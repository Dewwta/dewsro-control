<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { liveApi, type LiveSession, type LiveInventory } from '$lib/api/serverApi';

	// ── Session list ──────────────────────────────────────────────────────────
	let sessions: LiveSession[] = [];
	let sessionsBusy  = false;
	let sessionsError = '';
	let autoRefresh   = true;
	let refreshHandle: ReturnType<typeof setInterval> | null = null;

	async function fetchSessions(manual = false) {
		if (manual) sessionsBusy = true;
		sessionsError = '';
		try {
			sessions = await liveApi.getSessions();

			if (selectedId !== null && !sessions.find(s => s.connectionId === selectedId)) {
				selectedId = null;
				inventory  = null;
			}
			if (selectedId !== null && !inventoryLoading) {
				void refreshInventory(selectedId, true);
			}
		} catch (e: any) {
			sessionsError = e.message ?? 'Failed to load sessions.';
		} finally {
			if (manual) sessionsBusy = false;
		}
	}

	function startAutoRefresh() {
		stopAutoRefresh();
		refreshHandle = setInterval(() => fetchSessions(false), 1000);
	}
	function stopAutoRefresh() {
		if (refreshHandle) { clearInterval(refreshHandle); refreshHandle = null; }
	}
	function toggleAutoRefresh() {
		autoRefresh = !autoRefresh;
		autoRefresh ? startAutoRefresh() : stopAutoRefresh();
	}

	// ── Inventory panel ───────────────────────────────────────────────────────
	let selectedId: number | null = null;
	let inventory: LiveInventory | null = null;
	let inventoryLoading = false;
	let inventoryError   = '';
	let activeTab = 'inventory';
	const PAGE_SIZE = 24;
	let currentPage = 1;

	$: bagItems     = inventory?.inventory ?? [];
	$: bagPageCount = Math.max(1, Math.ceil(bagItems.length / PAGE_SIZE));
	$: pagedBag     = bagItems.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
    $: petKeys      = inventory?.pets ? Object.keys(inventory.pets) : [];
	$: selectedSession = sessions.find(s => s.connectionId === selectedId) ?? null;

	async function refreshInventory(id: number, silent = false) {
		if (!silent) { inventoryLoading = true; inventoryError = ''; }
		try {
			const fresh = await liveApi.getInventory(id);
			// Only apply if the player is still selected (avoid stale updates after switching)
			if (selectedId === id) inventory = fresh;
		} catch (e: any) {
			if (!silent) inventoryError = e.message ?? 'Failed to load inventory.';
		} finally {
			if (!silent) inventoryLoading = false;
		}
	}

	async function selectPlayer(s: LiveSession) {
		if (selectedId === s.connectionId) { selectedId = null; inventory = null; return; }
		selectedId  = s.connectionId;
		inventory   = null;
		activeTab   = 'inventory';
		currentPage = 1;
		await refreshInventory(s.connectionId, false);
	}

	function fmtDuration(sec: number): string {
		const h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = Math.floor(sec % 60);
		if (h > 0) return `${h}h ${m}m`;
		if (m > 0) return `${m}m ${s}s`;
		return `${s}s`;
	}
	function fmtGold(n: number): string { return n.toLocaleString(); }

	onMount(() => { fetchSessions(false); startAutoRefresh(); });
	onDestroy(stopAutoRefresh);
</script>

<div class="page">
	<PageHeader title="Live" subtitle="Real-time proxy session viewer">
		<svelte:fragment slot="actions">
			<SrButton size="sm" variant="secondary" loading={sessionsBusy}
				on:click={() => fetchSessions(true)}>Refresh</SrButton>
			<SrButton size="sm" variant={autoRefresh ? 'primary' : 'secondary'}
				on:click={toggleAutoRefresh}>{autoRefresh ? 'Auto ✓' : 'Auto ✗'}</SrButton>
		</svelte:fragment>
	</PageHeader>

	{#if sessionsError}
		<p class="top-error">{sessionsError}</p>
	{/if}

	<div class="content-layout">

		<!-- ── Left: session cards ──────────────────────────────────────────── -->
		<div class="sessions-col">
			<div class="col-label">{sessions.length} player{sessions.length !== 1 ? 's' : ''} online</div>

			{#if sessions.length === 0}
				<div class="empty-state">No active sessions.</div>
			{:else}
				{#each sessions as s (s.connectionId)}
					<button
						class="session-card"
						class:session-card--active={selectedId === s.connectionId}
						on:click={() => selectPlayer(s)}
					>
						<div class="sc-top">
							<span class="sc-name">{s.characterName}</span>
							{#if s.stats}<span class="sc-level">Lv {s.stats.level}</span>{/if}
						</div>
						<div class="sc-meta">
							<span class="sc-time">{fmtDuration(s.sessionSeconds)}</span>
							{#if s.isAfk}<span class="badge badge--afk">AFK</span>{/if}
							{#if s.party}<span class="badge badge--party">party</span>{/if}
							{#if !s.inventoryReady}<span class="badge badge--dim">inv?</span>{/if}
						</div>
						<div class="sc-ip">{s.ip}</div>
					</button>
				{/each}
			{/if}
		</div>

		<!-- ── Right: detail panel ──────────────────────────────────────────── -->
		<div class="detail-col">
			{#if selectedId === null}
				<div class="inv-placeholder">
					<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none"
						stroke="currentColor" stroke-width="1.5" class="placeholder-icon">
						<rect x="2" y="7" width="20" height="14" rx="2"/>
						<path d="M16 7V5a2 2 0 0 0-4 0v2"/>
						<path d="M8 7V5a2 2 0 0 0-4 0v2"/>
					</svg>
					<span>Select a player to view details</span>
				</div>

			{:else}

				<!-- ── Stats bar ─────────────────────────────────────────────── -->
				{#if selectedSession && selectedSession.stats}
					<div class="stats-bar">
						<div class="stat-pill">
							<span class="stat-pill__label">HP</span>
							<span class="stat-pill__val stat-pill__val--hp">{selectedSession.stats.currentHP.toLocaleString()}</span>
						</div>
						<div class="stat-pill">
							<span class="stat-pill__label">MP</span>
							<span class="stat-pill__val stat-pill__val--mp">{selectedSession.stats.currentMP.toLocaleString()}</span>
						</div>
						{#if selectedSession.stats.zerkLevel > 0}
							<div class="stat-pill">
								<span class="stat-pill__label">Zerk</span>
								<span class="stat-pill__val">{selectedSession.stats.zerkLevel}</span>
							</div>
						{/if}
						{#if selectedSession.stats.unusedStatPoints > 0}
							<div class="stat-pill stat-pill--warn">
								<span class="stat-pill__label">Stat pts</span>
								<span class="stat-pill__val">{selectedSession.stats.unusedStatPoints}</span>
							</div>
						{/if}
						<div class="stat-pill">
							<span class="stat-pill__label">Gold</span>
							<span class="stat-pill__val">{fmtGold(selectedSession.stats.gold)}</span>
						</div>
						<div class="stat-pill">
							<span class="stat-pill__label">Skill pts</span>
							<span class="stat-pill__val">{selectedSession.stats.skillPoints.toLocaleString()}</span>
						</div>
                        <div class="stat-pill">
							<span class="stat-pill__label">STR</span>
							<span class="stat-pill__val">
                                {selectedSession.stats.str?.toLocaleString?.() ?? 0}
                            </span>
						</div>
                        <div class="stat-pill">
							<span class="stat-pill__label">INT</span>
							<span class="stat-pill__val">
                                {selectedSession.stats.int?.toLocaleString?.() ?? 0}
                            </span>
						</div>
					</div>
				{/if}

				<!-- ── Party bar ──────────────────────────────────────────────── -->
				{#if selectedSession && selectedSession.party}
					<div class="party-bar">
						<span class="party-bar__label">Party</span>
						{#if selectedSession.party.message}
							<span class="party-bar__msg">"{selectedSession.party.message}"</span>
						{/if}
						<div class="party-members">
							{#each selectedSession.party.memberNames as name}
								<span
									class="party-member"
									class:party-member--leader={name === selectedSession.party.leaderName}
									title={name === selectedSession.party.leaderName ? 'Leader' : 'Member'}
								>{name}</span>
							{/each}
						</div>
					</div>
				{/if}

				<!-- ── Inventory box ─────────────────────────────────────────── -->
				<div class="inventory-box">
					{#if inventoryLoading}
						<div class="inv-placeholder">Loading…</div>

					{:else if inventoryError}
						<div class="inv-placeholder inv-placeholder--error">{inventoryError}</div>

					{:else if inventory}
						<div class="tab-bar">
							<button class="tab" class:tab--active={activeTab === 'inventory'}
								on:click={() => { activeTab = 'inventory'; currentPage = 1; }}>
								Inventory <span class="tab__count">{inventory.inventory.length}</span>
							</button>
							<button class="tab" class:tab--active={activeTab === 'equipment'}
								on:click={() => activeTab = 'equipment'}>
								Equipment <span class="tab__count">{inventory.equipment.length}</span>
							</button>
							{#each petKeys as uid}
                                {#if inventory.petInfos[uid].isAttackPet == false}
                                    <button class="tab" class:tab--active={activeTab === `pet_${uid}`}
    									on:click={() => activeTab = `pet_${uid}`}>
    									{inventory.petInfos[uid].name}, {inventory.petInfos[uid].readableName} <span class="tab__count">{inventory.pets[uid]?.length ?? 0}</span>
    								</button>
                                {/if}
							{/each}
						</div>

						<!-- Inventory tab -->
						{#if activeTab === 'inventory'}
							{#if pagedBag.length === 0}
								<div class="empty-state" style="padding: 1.5rem;">Inventory is empty.</div>
							{:else}
								<div class="item-grid">
									{#each pagedBag as item (item.slot)}
										<div class="item-cell" title="{item.displayName}&#10;{item.codeName}&#10;Slot {item.slot}">
											{#if item.iconUrl}
												<img class="item-icon" src={item.iconUrl} alt={item.displayName} loading="lazy" />
											{:else}
												<div class="item-icon item-icon--missing">?</div>
											{/if}
											<span class="item-name">{item.displayName}</span>
											{#if item.maxStack > 1}
												<span class="item-stack">{item.stack}</span>
											{/if}
										</div>
									{/each}
								</div>
							{/if}
							{#if bagPageCount > 1}
								<div class="pagination">
									<button class="pg-btn" disabled={currentPage === 1}
										on:click={() => currentPage--}>‹</button>
									<span class="pg-label">{currentPage} / {bagPageCount}</span>
									<button class="pg-btn" disabled={currentPage === bagPageCount}
										on:click={() => currentPage++}>›</button>
								</div>
							{/if}

						<!-- Equipment tab -->
						{:else if activeTab === 'equipment'}
							{#if inventory.equipment.length === 0}
								<div class="empty-state" style="padding: 1.5rem;">No equipment data.</div>
							{:else}
								<div class="item-grid">
									{#each inventory.equipment as item (item.slot)}
										<div class="item-cell" title="{item.displayName}&#10;{item.codeName}&#10;Slot {item.slot}">
											{#if item.iconUrl}
												<img class="item-icon" src={item.iconUrl} alt={item.displayName} loading="lazy" />
											{:else}
												<div class="item-icon item-icon--missing">?</div>
											{/if}
											<span class="item-name">{item.displayName}</span>
										</div>
									{/each}
								</div>
							{/if}

						<!-- Pet tabs -->
						{:else}
							{#each petKeys as uid}
								{#if activeTab === `pet_${uid}`}
									{#if !inventory.pets[uid] || inventory.pets[uid].length === 0}
										<div class="empty-state" style="padding: 1.5rem;">Pet inventory is empty.</div>
									{:else}
										<div class="item-grid">
											{#each inventory.pets[uid] as item (item.slot)}
												<div class="item-cell" title="{item.displayName}&#10;{item.codeName}&#10;Slot {item.slot}">
													{#if item.iconUrl}
														<img class="item-icon" src={item.iconUrl} alt={item.displayName} loading="lazy" />
													{:else}
														<div class="item-icon item-icon--missing">?</div>
													{/if}
													<span class="item-name">{item.displayName}</span>
													{#if item.maxStack > 1}
														<span class="item-stack">{item.stack}</span>
													{/if}
												</div>
											{/each}
										</div>
									{/if}
								{/if}
							{/each}
						{/if}

					{/if}
				</div>
			{/if}
		</div>

	</div>
</div>

<style>
	.page {
		padding: 2rem;
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}
	.top-error {
		font-size: 0.8rem;
		color: var(--red-bright);
		padding: 0.4rem 0.7rem;
		border: 1px solid var(--red-light);
		border-radius: var(--radius);
		background: rgba(92,16,16,0.15);
	}

	/* ── Layout ─── */
	.content-layout {
		display: grid;
		grid-template-columns: 220px 1fr;
		gap: 1rem;
		align-items: start;
	}
	@media (max-width: 700px) { .content-layout { grid-template-columns: 1fr; } }

	/* ── Session list ─── */
	.sessions-col { display: flex; flex-direction: column; gap: 0.35rem; }
	.col-label {
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--text-muted);
		padding-bottom: 0.3rem;
		border-bottom: 1px solid var(--border-dark);
	}
	.empty-state { font-size: 0.78rem; color: var(--text-dim); padding: 0.8rem 0; text-align: center; }
	.session-card {
		width: 100%;
		text-align: left;
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-left: 2px solid transparent;
		border-radius: var(--radius);
		padding: 0.55rem 0.7rem;
		cursor: pointer;
		transition: background 0.12s, border-color 0.12s;
		display: flex;
		flex-direction: column;
		gap: 0.18rem;
	}
	.session-card:hover { background: var(--bg-raised); border-left-color: var(--border-gold); }
	.session-card--active { background: var(--bg-raised); border-left-color: var(--gold); }
	.sc-top { display: flex; align-items: baseline; justify-content: space-between; gap: 0.4rem; }
	.sc-name { font-family: var(--font-heading); font-size: 0.83rem; color: var(--text-bright); }
	.sc-level { font-family: var(--font-mono); font-size: 0.65rem; color: var(--gold-light); flex-shrink: 0; }
	.sc-meta { display: flex; align-items: center; gap: 0.3rem; flex-wrap: wrap; }
	.sc-time { font-size: 0.68rem; color: var(--text-muted); font-family: var(--font-mono); }
	.sc-ip   { font-size: 0.62rem; color: var(--text-dim);   font-family: var(--font-mono); }
	.badge {
		font-size: 0.52rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		padding: 1px 4px;
		border-radius: 2px;
		font-family: var(--font-heading);
	}
	.badge--afk   { background: rgba(180,120,20,0.2); color: var(--gold-light); border: 1px solid var(--gold); }
	.badge--party { background: rgba(40,80,180,0.2);  color: #7aadff;           border: 1px solid #3a5faa; }
	.badge--dim   { background: rgba(80,80,80,0.2);   color: var(--text-muted); border: 1px solid var(--border-mid); }

	/* ── Detail column ─── */
	.detail-col { display: flex; flex-direction: column; gap: 0.6rem; }

	/* ── Placeholder ─── */
	.inv-placeholder {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 0.6rem;
		color: var(--text-dim);
		font-size: 0.8rem;
		font-family: var(--font-heading);
		letter-spacing: 0.05em;
		padding: 3rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
	}
	.inv-placeholder--error { color: var(--red-bright); }
	.placeholder-icon { width: 36px; height: 36px; color: var(--border-gold); }

	/* ── Stats bar ─── */
	.stats-bar {
		display: flex;
		flex-wrap: wrap;
		gap: 0.4rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.55rem 0.75rem;
	}
	.stat-pill {
		display: flex;
		align-items: center;
		gap: 0.3rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: 3px;
		padding: 0.2rem 0.55rem;
	}
	.stat-pill--warn { border-color: var(--gold); }
	.stat-pill__label {
    	font-family: var(--font-heading);
    	font-size: 0.62rem;
    	letter-spacing: 0.1em;
    	text-transform: uppercase;

    	color: var(--text-dim);
    	font-weight: 600;
    	opacity: 0.9;
    }
	.stat-pill__val { font-family: var(--font-mono); font-size: 0.75rem; color: var(--text-bright); }
	.stat-pill__val--hp { color: #e06060; }
	.stat-pill__val--mp { color: #6090e0; }

	/* ── Party bar ─── */
	.party-bar {
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: 0.5rem;
		background: var(--bg-surface);
		border: 1px solid #3a5faa;
		border-left: 3px solid #7aadff;
		border-radius: var(--radius);
		padding: 0.5rem 0.75rem;
	}
	.party-bar__label {
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: #7aadff;
	}
	.party-bar__msg { font-size: 0.72rem; color: var(--text-muted); font-style: italic; }
	.party-members { display: flex; flex-wrap: wrap; gap: 0.3rem; margin-left: auto; }
	.party-member {
		font-family: var(--font-heading);
		font-size: 0.68rem;
		padding: 1px 7px;
		border-radius: 2px;
		background: rgba(40,80,180,0.15);
		border: 1px solid #3a5faa;
		color: var(--text-base);
	}
	.party-member--leader { border-color: #7aadff; color: #7aadff; }

	/* ── Inventory box ─── */
	.inventory-box {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		display: flex;
		flex-direction: column;
		min-height: 120px;
	}

	/* ── Tab bar ─── */
	.tab-bar {
		display: flex;
		border-bottom: 1px solid var(--border-dark);
		background: var(--bg-raised);
		border-radius: var(--radius) var(--radius) 0 0;
		overflow-x: auto;
		flex-shrink: 0;
	}
	.tab {
		padding: 0.48rem 0.8rem;
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.07em;
		text-transform: uppercase;
		color: var(--text-muted);
		background: none;
		border: none;
		border-bottom: 2px solid transparent;
		cursor: pointer;
		white-space: nowrap;
		transition: color 0.12s, border-color 0.12s;
		display: flex;
		align-items: center;
		gap: 0.3rem;
	}
	.tab:hover { color: var(--text-base); }
	.tab--active { color: var(--gold-light); border-bottom-color: var(--gold); }
	.tab__count { font-size: 0.58rem; background: var(--bg-hover); padding: 1px 4px; border-radius: 2px; color: var(--text-dim); }

	/* ── Item grid ─── */
	.item-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, 72px);
		gap: 6px;
		padding: 0.75rem;
	}
	.item-cell {
		width: 72px;
		height: 72px;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 2px;
		padding: 4px;
		border-radius: var(--radius);
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		position: relative;
		transition: border-color 0.12s;
		overflow: hidden;
		box-sizing: border-box;
	}
	.item-cell:hover { border-color: var(--border-gold); background: var(--bg-hover); }
	.item-icon {
		width: 48px;
		height: 48px;
		object-fit: contain;
		image-rendering: pixelated;
		flex-shrink: 0;
	}
	.item-icon--missing {
		width: 48px;
		height: 48px;
		display: flex;
		align-items: center;
		justify-content: center;
		font-size: 1rem;
		color: var(--text-dim);
		background: var(--bg-surface);
		border: 1px dashed var(--border-dark);
		border-radius: 2px;
		flex-shrink: 0;
	}
	.item-name {
		font-size: 0.57rem;
		color: var(--text-muted);
		text-align: center;
		line-height: 1.15;
		max-width: 64px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.item-stack {
		position: absolute;
		bottom: 14px;
		right: 4px;
		font-size: 0.55rem;
		font-family: var(--font-mono);
		color: var(--text-bright);
		background: rgba(0,0,0,0.7);
		padding: 0 3px;
		border-radius: 2px;
		line-height: 1.4;
	}

	/* ── Pagination ─── */
	.pagination {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 0.5rem;
		padding: 0.45rem;
		border-top: 1px solid var(--border-dark);
	}
	.pg-btn {
		width: 26px; height: 26px;
		display: flex; align-items: center; justify-content: center;
		font-size: 0.9rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		color: var(--text-base);
		cursor: pointer;
		transition: background 0.12s, border-color 0.12s;
	}
	.pg-btn:hover:not(:disabled) { background: var(--bg-hover); border-color: var(--border-gold); }
	.pg-btn:disabled { opacity: 0.35; cursor: default; }
	.pg-label {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		color: var(--text-muted);
		min-width: 46px;
		text-align: center;
	}
</style>
