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

	// ── Helpers ───────────────────────────────────────────────────────────────
	function fmtDuration(sec: number): string {
		const h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = Math.floor(sec % 60);
		if (h > 0) return `${h}h ${m}m`;
		if (m > 0) return `${m}m ${s}s`;
		return `${s}s`;
	}
	function fmtGold(n: number): string {
		if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
		if (n >= 1_000_000)     return `${(n / 1_000_000).toFixed(1)}M`;
		if (n >= 1_000)         return `${(n / 1_000).toFixed(1)}K`;
		return n.toLocaleString();
	}
	function fmtExp(n: number): string {
		if (!n) return '0';
		if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(2)}B`;
		if (n >= 1_000_000)     return `${(n / 1_000_000).toFixed(1)}M`;
		if (n >= 1_000)         return `${(n / 1_000).toFixed(1)}K`;
		return n.toString();
	}
	function raceShort(race: string | null | undefined): string {
		if (!race) return '?';
		const r = race.toLowerCase();
		if (r.includes('chinese'))  return 'CH';
		if (r.includes('european')) return 'EU';
		return race.slice(0, 2).toUpperCase();
	}
	function raceClass(race: string | null | undefined): string {
		if (!race) return 'race--unk';
		const r = race.toLowerCase();
		if (r.includes('chinese'))  return 'race--ch';
		if (r.includes('european')) return 'race--eu';
		return 'race--unk';
	}
	function isBotActive(state: string | null | undefined): boolean {
		return !!state && state !== 'Idle' && state !== 'None' && state !== '';
	}

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
						class:session-card--bot={isBotActive(s.stats?.botState) && selectedId !== s.connectionId}
						on:click={() => selectPlayer(s)}
					>
						<div class="sc-top">
							{#if s.stats?.race}
								<span class="race-tag {raceClass(s.stats.race)}">{raceShort(s.stats.race)}</span>
							{/if}
							<span class="sc-name">{s.characterName}</span>
							{#if s.stats}<span class="sc-level">Lv {s.stats.level}</span>{/if}
						</div>
						<div class="sc-meta">
							<span class="sc-time">{fmtDuration(s.sessionSeconds)}</span>
							{#if isBotActive(s.stats?.botState)}
								<span class="badge badge--bot"><span class="mini-dot mini-dot--bot"></span>BOT</span>
							{/if}
							{#if s.isGM}<span class="badge badge--gm">GM</span>{/if}
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
				<div class="sect-placeholder">
					<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none"
						stroke="currentColor" stroke-width="1.5" class="placeholder-icon">
						<rect x="2" y="7" width="20" height="14" rx="2"/>
						<path d="M16 7V5a2 2 0 0 0-4 0v2"/>
						<path d="M8 7V5a2 2 0 0 0-4 0v2"/>
					</svg>
					<span>Select a player to view details</span>
				</div>

			{:else if selectedSession}

				<!-- ── Player header ─────────────────────────────────────────── -->
				<div class="player-header">
					<div class="ph-top">
						<div class="ph-identity">
							{#if selectedSession.stats?.race}
								<span class="race-tag race-tag--lg {raceClass(selectedSession.stats.race)}">{raceShort(selectedSession.stats.race)}</span>
							{/if}
							<span class="ph-name">{selectedSession.characterName}</span>
							{#if selectedSession.stats}
								<span class="ph-level">Lv {selectedSession.stats.level}</span>
							{/if}
						</div>
						<div class="ph-badges">
							{#if selectedSession.isGM}
								<span class="badge badge--gm">GM</span>
							{/if}
							{#if selectedSession.isAfk}
								<span class="badge badge--afk">AFK</span>
							{/if}
							{#if isBotActive(selectedSession.stats?.botState)}
								<span class="badge badge--bot-lg">
									<span class="mini-dot mini-dot--bot"></span>BOT — {selectedSession.stats?.botState}
								</span>
							{/if}
						</div>
					</div>
					<div class="ph-meta">
						<span>JID {selectedSession.jid}</span>
						{#if selectedSession.stats?.serverName}
							<span class="ph-sep">·</span><span>{selectedSession.stats.serverName}</span>
						{/if}
						<span class="ph-sep">·</span>
						<span class="ph-mono">{selectedSession.ip}</span>
						<span class="ph-sep">·</span>
						<span>Online {fmtDuration(selectedSession.sessionSeconds)}</span>
						{#if selectedSession.stats?.characterUID}
							<span class="ph-sep">·</span>
							<span class="ph-mono">UID #{selectedSession.stats.characterUID}</span>
						{/if}
					</div>
				</div>

				{#if selectedSession.stats}
					{@const st = selectedSession.stats}

					<!-- ── Three-column info cards ──────────────────────────── -->
					<div class="info-grid">

						<div class="info-card">
							<div class="card-title">Vitals</div>
							<div class="card-rows">
								<div class="cr">
									<span class="cr-k">HP</span>
									<span class="cr-v cr-v--hp">{st.currentHP.toLocaleString()}{#if st.maxHP} / {st.maxHP.toLocaleString()}{/if}</span>
								</div>
								<div class="cr">
									<span class="cr-k">MP</span>
									<span class="cr-v cr-v--mp">{st.currentMP.toLocaleString()}{#if st.maxMP} / {st.maxMP.toLocaleString()}{/if}</span>
								</div>
								<div class="cr">
									<span class="cr-k">Gold</span>
									<span class="cr-v">{fmtGold(st.gold)}</span>
								</div>
								{#if st.zerkLevel > 0}
									<div class="cr">
										<span class="cr-k">Berserk</span>
										<span class="cr-v cr-v--zerk">{st.zerkLevel}</span>
									</div>
								{/if}
								{#if (st.unusedStatPoints ?? 0) > 0}
									<div class="cr cr--warn">
										<span class="cr-k">Stat Pts</span>
										<span class="cr-v">{st.unusedStatPoints} unspent</span>
									</div>
								{/if}
							</div>
						</div>

						<div class="info-card">
							<div class="card-title">Attributes</div>
							<div class="card-rows">
								<div class="cr">
									<span class="cr-k">STR</span>
									<span class="cr-v">{st.str?.toLocaleString() ?? 0}</span>
								</div>
								<div class="cr">
									<span class="cr-k">INT</span>
									<span class="cr-v">{st.int?.toLocaleString() ?? 0}</span>
								</div>
								<div class="cr">
									<span class="cr-k">Skill Pts</span>
									<span class="cr-v">{st.skillPoints.toLocaleString()}</span>
								</div>
								{#if st.runSpeed}
									<div class="cr">
										<span class="cr-k">Run Speed</span>
										<span class="cr-v">{st.runSpeed.toFixed(1)}</span>
									</div>
								{/if}
							</div>
						</div>

						<div class="info-card">
							<div class="card-title">Session Stats</div>
							<div class="card-rows">
								<div class="cr">
									<span class="cr-k">Kills</span>
									<span class="cr-v">{st.sessionKills ?? 0}</span>
								</div>
								<div class="cr">
									<span class="cr-k">Unique Kills</span>
									<span class="cr-v">{st.sessionUniqueKills ?? 0}</span>
								</div>
								<div class="cr">
									<span class="cr-k">Exp Gained</span>
									<span class="cr-v">{fmtExp(st.cumulativeExp ?? 0)}</span>
								</div>
								{#if (st.lastTargetUID ?? 0) > 0}
									<div class="cr">
										<span class="cr-k">Last Target</span>
										<span class="cr-v cr-v--dim">#{st.lastTargetUID}</span>
									</div>
								{/if}
							</div>
						</div>

					</div>

					<!-- ── Position ─────────────────────────────────────────── -->
					<div class="info-card info-card--wide">
						<div class="card-title">
							Position
							{#if st.isMoving}
								<span class="status-chip status-chip--moving"><span class="mini-dot mini-dot--green"></span>Moving</span>
							{:else}
								<span class="status-chip status-chip--still">Stationary</span>
							{/if}
						</div>
						<div class="pos-grid">
							<div class="cr">
								<span class="cr-k">Region</span>
								<span class="cr-v">{st.regionReadableName ?? st.currentRegion ?? '—'}</span>
							</div>
							{#if st.regionName}
								<div class="cr">
									<span class="cr-k">Region Code</span>
									<span class="cr-v cr-v--dim">{st.regionName}</span>
								</div>
							{/if}
							<div class="cr">
								<span class="cr-k">Region ID</span>
								<span class="cr-v cr-v--dim">{st.regionId ?? '—'}</span>
							</div>
							<div class="cr">
								<span class="cr-k">World X / Y</span>
								<span class="cr-v">{st.worldX} / {st.worldY}</span>
							</div>
							<div class="cr">
								<span class="cr-k">Z</span>
								<span class="cr-v">{st.worldZ}</span>
							</div>
							{#if st.sectorX !== undefined && st.sectorY !== undefined}
								<div class="cr">
									<span class="cr-k">Sector</span>
									<span class="cr-v cr-v--dim">{st.sectorX},{st.sectorY}</span>
								</div>
							{/if}
						</div>
					</div>

					<!-- ── Bot Inspector (only when active) ─────────────────── -->
					{#if isBotActive(st.botState)}
						<div class="info-card info-card--wide info-card--bot">
							<div class="card-title">
								Bot Inspector
								<span class="bot-state-chip">{st.botState}</span>
							</div>
							<div class="pos-grid">
								{#if st.trainingDestX !== null && st.trainingDestX !== undefined}
									<div class="cr">
										<span class="cr-k">Train Dest</span>
										<span class="cr-v">X:{st.trainingDestX} Y:{st.trainingDestY ?? '?'} Z:{st.trainingDestZ ?? 0}</span>
									</div>
								{/if}
								{#if st.trainingDestRegionId !== null && st.trainingDestRegionId !== undefined}
									<div class="cr">
										<span class="cr-k">Dest Region</span>
										<span class="cr-v cr-v--dim">{st.trainingDestRegionId}</span>
									</div>
								{/if}
								<div class="cr">
									<span class="cr-k">Mobs Tracked</span>
									<span class="cr-v">{st.mobCount ?? 0}</span>
								</div>
								<div class="cr">
									<span class="cr-k">Drops Visible</span>
									<span class="cr-v">{st.droppedItemCount ?? 0}</span>
								</div>
								<div class="cr">
									<span class="cr-k">Spawned Objs</span>
									<span class="cr-v cr-v--dim">{st.spawnedObjectCount ?? 0}</span>
								</div>
							</div>
						</div>
					{/if}

				{/if}

				<!-- ── Party bar ──────────────────────────────────────────────── -->
				{#if selectedSession.party}
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
						<div class="sect-placeholder sect-placeholder--sm">Loading inventory…</div>
					{:else if inventoryError}
						<div class="sect-placeholder sect-placeholder--error">{inventoryError}</div>
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
										{inventory.petInfos[uid].name}, {inventory.petInfos[uid].readableName}
										<span class="tab__count">{inventory.pets[uid]?.length ?? 0}</span>
									</button>
								{/if}
							{/each}
						</div>

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

					{:else}
						<div class="sect-placeholder sect-placeholder--sm">No inventory data yet.</div>
					{/if}
				</div>

			{/if}
		</div>

	</div>
</div>

<style>
	@keyframes pulse-dot {
		0%, 100% { opacity: 1;   transform: scale(1);   }
		50%       { opacity: 0.4; transform: scale(0.65); }
	}

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

	/* ── Sessions column ─── */
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

	/* ── Session card ─── */
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
	.session-card:hover                { background: var(--bg-raised); border-left-color: var(--border-gold); }
	.session-card--active              { background: var(--bg-raised); border-left-color: var(--gold); }
	.session-card--bot                 { border-left-color: #c97c30; }
	.sc-top  { display: flex; align-items: center; gap: 0.35rem; }
	.sc-name { font-family: var(--font-heading); font-size: 0.83rem; color: var(--text-bright); flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
	.sc-level { font-family: var(--font-mono); font-size: 0.65rem; color: var(--gold-light); flex-shrink: 0; }
	.sc-meta { display: flex; align-items: center; gap: 0.3rem; flex-wrap: wrap; }
	.sc-time { font-size: 0.68rem; color: var(--text-muted); font-family: var(--font-mono); }
	.sc-ip   { font-size: 0.62rem; color: var(--text-dim);   font-family: var(--font-mono); }

	/* ── Race tag ─── */
	.race-tag {
		font-family: var(--font-heading);
		font-size: 0.5rem;
		letter-spacing: 0.06em;
		padding: 1px 4px;
		border-radius: 2px;
		font-weight: 700;
		flex-shrink: 0;
		line-height: 1.5;
	}
	.race-tag--lg { font-size: 0.65rem; padding: 2px 6px; border-radius: 3px; }
	.race--ch  { background: rgba(30,120,60,0.25);  color: #5ac87a; border: 1px solid #2d7a46; }
	.race--eu  { background: rgba(40,80,200,0.25);  color: #7aadff; border: 1px solid #3a5faa; }
	.race--unk { background: rgba(80,80,80,0.2);    color: var(--text-muted); border: 1px solid var(--border-mid); }

	/* ── Badges ─── */
	.badge {
		display: inline-flex;
		align-items: center;
		gap: 3px;
		font-size: 0.52rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		padding: 1px 4px;
		border-radius: 2px;
		font-family: var(--font-heading);
		white-space: nowrap;
	}
	.badge--afk   { background: rgba(180,120,20,0.2);  color: var(--gold-light); border: 1px solid var(--gold); }
	.badge--party { background: rgba(40,80,180,0.2);   color: #7aadff;           border: 1px solid #3a5faa; }
	.badge--dim   { background: rgba(80,80,80,0.2);    color: var(--text-muted); border: 1px solid var(--border-mid); }
	.badge--gm    { background: rgba(180,140,20,0.25); color: #e8c040;           border: 1px solid #9a7a10; }
	.badge--bot   { background: rgba(200,80,30,0.2);   color: #e87a50;           border: 1px solid #a04020; }
	.badge--bot-lg {
		display: inline-flex;
		align-items: center;
		gap: 4px;
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		padding: 2px 7px;
		border-radius: 3px;
		background: rgba(200,80,30,0.15);
		color: #e87a50;
		border: 1px solid #a04020;
	}

	/* ── Animated dots ─── */
	.mini-dot {
		display: inline-block;
		width: 5px;
		height: 5px;
		border-radius: 50%;
		flex-shrink: 0;
		animation: pulse-dot 1.4s ease-in-out infinite;
	}
	.mini-dot--bot   { background: #e87a50; }
	.mini-dot--green { background: #5ac87a; }

	/* ── Detail column ─── */
	.detail-col { display: flex; flex-direction: column; gap: 0.6rem; min-width: 0; }

	/* ── Section placeholder ─── */
	.sect-placeholder {
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
	.sect-placeholder--sm    { padding: 1.2rem; font-size: 0.75rem; flex-direction: row; }
	.sect-placeholder--error { color: var(--red-bright); }
	.placeholder-icon { width: 36px; height: 36px; color: var(--border-gold); }

	/* ── Player header ─── */
	.player-header {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.75rem 1rem;
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
	}
	.ph-top {
		display: flex;
		align-items: center;
		gap: 0.7rem;
		flex-wrap: wrap;
	}
	.ph-identity {
		display: flex;
		align-items: center;
		gap: 0.45rem;
		flex: 1;
		min-width: 0;
	}
	.ph-name  { font-family: var(--font-heading); font-size: 1.1rem; color: var(--text-bright); letter-spacing: 0.02em; }
	.ph-level { font-family: var(--font-mono); font-size: 0.72rem; color: var(--gold-light); flex-shrink: 0; }
	.ph-badges { display: flex; align-items: center; gap: 0.35rem; flex-wrap: wrap; }
	.ph-meta {
		display: flex;
		align-items: center;
		gap: 0.4rem;
		flex-wrap: wrap;
		font-size: 0.64rem;
		color: var(--text-dim);
		font-family: var(--font-mono);
	}
	.ph-sep  { color: var(--border-mid); }
	.ph-mono { color: var(--text-muted); }

	/* ── Info grid (3-up) ─── */
	.info-grid {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 0.5rem;
	}
	@media (max-width: 960px) { .info-grid { grid-template-columns: repeat(2, 1fr); } }
	@media (max-width: 600px) { .info-grid { grid-template-columns: 1fr; } }

	/* ── Info card ─── */
	.info-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.6rem 0.75rem;
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
	}
	.info-card--wide { grid-column: 1 / -1; }
	.info-card--bot  { border-color: #7a4020; background: rgba(200,80,30,0.03); }

	.card-title {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		font-family: var(--font-heading);
		font-size: 0.6rem;
		letter-spacing: 0.12em;
		text-transform: uppercase;
		color: var(--text-dim);
		border-bottom: 1px solid var(--border-dark);
		padding-bottom: 0.3rem;
	}

	/* ── Key-value rows ─── */
	.card-rows {
		display: flex;
		flex-direction: column;
		gap: 0;
	}
	.pos-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
		gap: 0 2rem;
	}
	.cr {
		display: flex;
		align-items: baseline;
		justify-content: space-between;
		gap: 0.5rem;
		padding: 0.32rem 0;
		border-bottom: 1px solid var(--border-dark);
	}
	.cr:last-child { border-bottom: none; }
	.cr--warn .cr-k { color: var(--gold-light); }
	.cr-k {
		font-family: var(--font-heading);
		font-size: 0.59rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		color: var(--text-muted);
		white-space: nowrap;
		flex-shrink: 0;
	}
	.cr-v {
		font-family: var(--font-mono);
		font-size: 0.73rem;
		color: var(--text-bright);
		text-align: right;
	}
	.cr-v--hp   { color: #e06060; }
	.cr-v--mp   { color: #6090e0; }
	.cr-v--dim  { color: var(--text-muted); }
	.cr-v--zerk { color: #e0a030; }

	/* ── Status chips (inside card titles) ─── */
	.status-chip {
		display: inline-flex;
		align-items: center;
		gap: 3px;
		font-family: var(--font-heading);
		font-size: 0.55rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		padding: 1px 5px;
		border-radius: 2px;
	}
	.status-chip--moving { background: rgba(30,120,60,0.2);  color: #5ac87a; border: 1px solid #2d7a46; }
	.status-chip--still  { background: rgba(80,80,80,0.15);  color: var(--text-dim); border: 1px solid var(--border-dark); }
	.bot-state-chip {
		font-family: var(--font-heading);
		font-size: 0.55rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		padding: 1px 5px;
		border-radius: 2px;
		background: rgba(200,80,30,0.2);
		color: #e87a50;
		border: 1px solid #a04020;
	}

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
