<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { worldApi, type Town, type RegionAssoc, type MonsterSpawnCount } from '$lib/api/serverApi';

	function mkState() {
		return { loading: false, msg: '', ok: true };
	}

	// ── Region display name map ───────────────────────────────────────────────
	// Keys must match AreaName exactly (case-sensitive) as stored in
	// _RefRegionBindAssocServer. Add or edit entries freely — unrecognised
	// codes fall back to showing the raw code name.
	const REGION_NAMES: Record<string, string> = {
		// Main cities
		'CHINA':          'Jangan',
		'West_China':     'Donwhang',
		'Oasis_Kingdom':  'Hotan',
		'SD':             'Abundance Grounds (Alex Desert)',
		'Eu':             'Constantinople',
		'Am':             'Asia Minor (Left of Samarkand)',
		'Ca':             'Samarkand (Central Asia)',

		// Wilderness & travel zones
		'TQ':             'Qin-Shi Tomb',
		'Roc':            'Roc Mountain',
		'Thief Village':  'Thief Town',

		// Egypt region
		'KingsValley':    "King's Valley",
		'Pharaoh':        'Holy Water Temple',
		'DELTA':          'Alexandria',
		'TEMPLE':         'Alexandria Job Cave (Black/Red Eggre)',

		// Instanced & special zones
		'GOD_TOGUI':      'Togui Village',
		'GOD_FLAME':      'Flame Mountain',
		'GOD_WRECK_IN':   'Shipwreck (Lvl 1)',
		'GOD_WRECK_OUT':  'Shipwreck (Lvl 2)',
		'EVENT_GHOST':    'Ghost Event Zone',
		'JUPITER':        'Jupiter',
		'PRISON':         'Prison',
		'GM_EVENT':       'GM Event Zone',
		'NULL':           'None / Unbound',

		// Battle arenas
		'ARENA_OCCUPY':   'Arena — Occupation',
		'ARENA_FLAG':     'Arena — Flag',
		'ARENA_SCORE':    'Arena — Score',
		'ARENA_GNGWC':    'Arena — World Championship',
		'SIEGE_DUNGEON':  'Siege Dungeon',

		// Fortress war zones
		'FORT_JA_AREA':   'Jangan Fortress Zone',
		'FORT_DW_AREA':   'Donwhang Fortress Zone',
		'FORT_HT_AREA':   'Hotan Fortress Zone',
		'FORT_CT_AREA':   'Constantinople Fortress Zone',
		'FORT_SK_AREA':   'Samarkand Fortress Zone',
		'FORT_BJ_AREA':   'Fort BJ Zone',
		'FORT_HM_AREA':   'Fort HM Zone',
		'FORT_ER_AREA':   'Fort ER Zone',
        
        // Secret
        'CHINA_SYSTEM':   'Secret'
	};

	function getDisplayName(areaName: string): string {
		return REGION_NAMES[areaName] ?? areaName;
	}

	// ── Region Server Bind ────────────────────────────────────────────────────
	let regions: RegionAssoc[] = [];
	let dirtyRegions = new Set<string>();
	let regionLoadState  = mkState();
	let regionSaveState  = mkState();

	async function loadRegions() {
		regionLoadState = { loading: true, msg: '', ok: true };
		try {
			regions = await worldApi.getRegions();
			regionLoadState = { loading: false, msg: '', ok: true };
		} catch (e: any) {
			regionLoadState = { loading: false, msg: e.message, ok: false };
		}
	}

	function markDirty(areaName: string) {
		dirtyRegions = new Set([...dirtyRegions, areaName]);
	}

	async function handleSaveRegions() {
		const dirty = regions.filter(r => dirtyRegions.has(r.areaName));
		if (!dirty.length) return;
		regionSaveState = { loading: true, msg: '', ok: true };
		try {
			await Promise.all(dirty.map(r => worldApi.setRegion(r.areaName, r.enabled)));
			dirtyRegions = new Set();
			regionSaveState = { loading: false, msg: `Saved ${dirty.length} region${dirty.length !== 1 ? 's' : ''}.`, ok: true };
		} catch (e: any) {
			regionSaveState = { loading: false, msg: e.message, ok: false };
		}
	}

	onMount(loadRegions);

	// ── Add NPC ───────────────────────────────────────────────────────────────
	let npcCode    = '';
	let npcChar    = '';
	let npcStore   = 1;
	let npcTab1    = 0;
	let npcTab2    = 0;
	let npcTab3    = 0;
	let npcTab4    = 0;
	let npcDir     = 0;
	let npcState   = mkState();

	async function handleAddNpc() {
		if (!npcCode.trim() || !npcChar.trim()) return;
		npcState = { loading: true, msg: '', ok: true };
		try {
			const r = await worldApi.addNpc(npcCode.trim(), npcChar.trim(), npcStore, npcTab1, npcTab2, npcTab3, npcTab4, npcDir);
			npcState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			npcState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Add Reverse Point ─────────────────────────────────────────────────────
	let rpZone     = '';
	let rpPosX     = 0;
	let rpPosY     = 0;
	let rpPosZ     = 0;
	let rpRegion   = 0;
	let rpState    = mkState();

	async function handleReversePoint() {
		if (!rpZone.trim()) return;
		rpState = { loading: true, msg: '', ok: true };
		try {
			const r = await worldApi.addReversePoint(rpZone.trim(), rpPosX, rpPosY, rpPosZ, rpRegion);
			rpState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			rpState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Add Teleporter ────────────────────────────────────────────────────────
	const towns: { label: string; value: Town }[] = [
		{ label: 'Jangan',             value: 'Jangan' },
		{ label: 'Donwhang',           value: 'Donwhang' },
		{ label: 'Hotan',              value: 'Hotan' },
		{ label: 'Samarkand',          value: 'Samarkand' },
		{ label: 'Constantinople',     value: 'Constantinople' },
		{ label: 'Alexandria (North)', value: 'AlexandriaNorth' },
		{ label: 'Alexandria (South)', value: 'AlexandriaSouth' }
	];

	let telCode    = '';
	let telFee     = 0;
	let telTown: Town = 'Jangan';
	let telFromChar = '';
	let telToChar   = '';
	let telReqLvl  = 0;
	let telState   = mkState();

	async function handleAddTeleporter() {
		if (!telCode.trim() || !telFromChar.trim() || !telToChar.trim()) return;
		telState = { loading: true, msg: '', ok: true };
		try {
			const r = await worldApi.addTeleporter(telCode.trim(), telFee, telTown, telFromChar.trim(), telToChar.trim(), telReqLvl);
			telState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			telState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Monster Spawns — By Group ─────────────────────────────────────────────
	const MOB_GROUPS: { label: string; prefix: string }[] = [
		{ label: 'Jangan',                         prefix: 'MOB_CH_'  },
		{ label: 'Donwhang',                        prefix: 'MOB_WC_'  },
		{ label: 'Hotan',                           prefix: 'MOB_KT_'  },
		{ label: 'Karakoram',                       prefix: 'MOB_KK_'  },
		{ label: 'Qin-Shi Tomb',                    prefix: 'MOB_TQ_'  },
		{ label: 'Roc Mountain',                    prefix: 'MOB_RM_'  },
		{ label: 'Oasis',                           prefix: 'MOB_OA_'  },
		{ label: "Abundance Grounds / King's Valley", prefix: 'MOB_SD_'  },
		{ label: 'Europe / Constantinople',           prefix: 'MOB_EU_'  },
		{ label: 'Asia Minor',                        prefix: 'MOB_AM_'  },
		{ label: 'Central Asia (Samarkand)',          prefix: 'MOB_CA_'  },
		{ label: 'Taklaman',                          prefix: 'MOB_TK_'  },
		{ label: 'Special / God Zones',               prefix: 'MOB_GOD_' },
	];

	let spawnGroupPrefix  = MOB_GROUPS[0].prefix;
	let spawnGroupMax     = 5;
	let spawnGroupAll     = false;
	let spawnGroupConfirm = false;
	let spawnGroupState   = mkState();

	function onGroupFormChange() { spawnGroupConfirm = false; }

	async function handleGroupSpawnCap() {
		if (spawnGroupAll && !spawnGroupConfirm) { spawnGroupConfirm = true; return; }
		spawnGroupConfirm = false;
		spawnGroupState = { loading: true, msg: '', ok: true };
		try {
			const r = spawnGroupAll
				? await worldApi.setAllGroupMonsterSpawns(spawnGroupMax)
				: await worldApi.setMonsterSpawns(spawnGroupPrefix, spawnGroupMax, false);
			spawnGroupState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			spawnGroupState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Monster Spawns — By Exact Codename ───────────────────────────────────
	let spawnExactCode  = '';
	let spawnExactMax   = 1;
	let spawnExactState = mkState();

	async function handleExactSpawnCap() {
		if (!spawnExactCode.trim()) return;
		spawnExactState = { loading: true, msg: '', ok: true };
		try {
			const r = await worldApi.setMonsterSpawns(spawnExactCode.trim(), spawnExactMax, true);
			spawnExactState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			spawnExactState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Spawn Count Query ─────────────────────────────────────────────────────
	let spawnQueryPrefix = MOB_GROUPS[0].prefix;
	let spawnQueryRows: MonsterSpawnCount[] = [];
	let spawnQueryState = mkState();

	async function handleSpawnCountQuery() {
		spawnQueryState = { loading: true, msg: '', ok: true };
		spawnQueryRows = [];
		try {
			spawnQueryRows = await worldApi.getMonsterSpawnCounts(spawnQueryPrefix);
			spawnQueryState = { loading: false, msg: `${spawnQueryRows.length} monster${spawnQueryRows.length !== 1 ? 's' : ''} found.`, ok: true };
		} catch (e: any) {
			spawnQueryState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Fix Unique Spawns ─────────────────────────────────────────────────────
	let fixState   = mkState();
	let fixConfirm = false;

	async function handleFixUniqueSpawns() {
		if (!fixConfirm) { fixConfirm = true; return; }
		fixState   = { loading: true, msg: '', ok: true };
		fixConfirm = false;
		try {
			const r = await worldApi.fixUniqueSpawns();
			fixState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			fixState = { loading: false, msg: e.message, ok: false };
		}
	}
</script>

<div class="page">
	<PageHeader title="World" subtitle="NPC placement, teleporters, and monster spawn management" />

	<!-- ── Info strip ───────────────────────────────────────────────────── -->
	<div class="info-strip">
		<span class="info-strip__icon">ℹ</span>
		<p class="info-strip__text">
			NPC and teleporter operations require an <strong>online GM character</strong> whose position is used as the placement anchor.
			After adding NPCs or teleporters, a new <strong>textdata export</strong> may be required.
		</p>
	</div>

	<!-- ── Section: Region Server Bind ──────────────────────────────────── -->
	<div class="section-label">Region Server Bind</div>
	<div class="card-grid">
		<div class="card card--wide">
			<div class="card__header">
				<h2 class="card__title">Region Associations</h2>
				<span class="card__sub">Enable or disable game areas on the server — controls which regions players can enter</span>
			</div>
			<div class="card__body">
				{#if regionLoadState.loading}
					<p class="region-loading">Loading regions…</p>
				{:else if regionLoadState.msg && !regionLoadState.ok}
					<p class="inline-msg inline-msg--err">{regionLoadState.msg}</p>
				{:else if regions.length === 0}
					<p class="region-loading">No regions found.</p>
				{:else}
					<div class="region-list">
						{#each regions as region (region.areaName)}
							<div class="region-row" class:region-row--dirty={dirtyRegions.has(region.areaName)}>
								<span class="region-name">
									{getDisplayName(region.areaName)}
									<span class="region-code">{region.areaName}</span>
								</span>
								<label class="toggle" title={region.enabled ? 'Click to disable' : 'Click to enable'}>
									<input
										type="checkbox"
										bind:checked={region.enabled}
										on:change={() => markDirty(region.areaName)}
									/>
									<span class="toggle__track"></span>
								</label>
								<span class="region-status" class:region-status--on={region.enabled}>{region.enabled ? 'Enabled' : 'Disabled'}</span>
							</div>
						{/each}
					</div>
				{/if}
			</div>
			<div class="card__footer">
				{#if regionSaveState.msg}
					<p class="inline-msg" class:inline-msg--ok={regionSaveState.ok} class:inline-msg--err={!regionSaveState.ok}>{regionSaveState.msg}</p>
				{/if}
				<SrButton
					variant="primary"
					disabled={dirtyRegions.size === 0}
					loading={regionSaveState.loading}
					on:click={handleSaveRegions}
				>
					Save Changes{dirtyRegions.size > 0 ? ` (${dirtyRegions.size})` : ''}
				</SrButton>
			</div>
		</div>
	</div>

	<!-- ── Section: NPC Placement ───────────────────────────────────────── -->
	<div class="section-label">NPC Placement</div>
	<div class="card-grid">

		<div class="card card--wide">
			<div class="card__header">
				<h2 class="card__title">Place NPC at Character</h2>
				<span class="card__sub">Spawns an NPC at the given GM character's current world position</span>
			</div>
			<div class="card__body card__body--cols">
				<div class="col">
					<div class="field-group">
						<label class="field-label">NPC Code Name</label>
						<input class="sr-input" type="text" placeholder="e.g. NPC_CH_JANGAN_GROCERY" bind:value={npcCode} />
					</div>
					<div class="field-group">
						<label class="field-label">GM Character Name</label>
						<input class="sr-input" type="text" placeholder="Character at target location" bind:value={npcChar} />
					</div>
					<div class="field-group">
						<label class="field-label">Store Groups <span class="field-hint">(tab count)</span></label>
						<input class="sr-input" type="number" min="0" max="4" bind:value={npcStore} />
					</div>
					<div class="field-group">
						<label class="field-label">Looking Direction <span class="field-hint">(0–360)</span></label>
						<input class="sr-input" type="number" min="0" max="360" bind:value={npcDir} />
					</div>
				</div>
				<div class="col">
					<div class="field-group">
						<label class="field-label">Tab Group 1 ID</label>
						<input class="sr-input" type="number" min="0" bind:value={npcTab1} />
					</div>
					<div class="field-group">
						<label class="field-label">Tab Group 2 ID</label>
						<input class="sr-input" type="number" min="0" bind:value={npcTab2} />
					</div>
					<div class="field-group">
						<label class="field-label">Tab Group 3 ID</label>
						<input class="sr-input" type="number" min="0" bind:value={npcTab3} />
					</div>
					<div class="field-group">
						<label class="field-label">Tab Group 4 ID</label>
						<input class="sr-input" type="number" min="0" bind:value={npcTab4} />
					</div>
				</div>
			</div>
			<div class="card__footer">
				{#if npcState.msg}
					<p class="inline-msg" class:inline-msg--ok={npcState.ok} class:inline-msg--err={!npcState.ok}>{npcState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={!npcCode.trim() || !npcChar.trim()} loading={npcState.loading} on:click={handleAddNpc}>
					Place NPC
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Section: Teleport Management ─────────────────────────────────── -->
	<div class="section-label">Teleport Management</div>
	<div class="card-grid">

		<!-- Add teleporter -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Add Teleporter</h2>
				<span class="card__sub">Links from a city to a character's position</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Teleporter Code Name</label>
					<input class="sr-input" type="text" placeholder="e.g. Jangan_Cave_B1" bind:value={telCode} />
				</div>
				<div class="field-row">
					<div class="field-group">
						<label class="field-label">Gold Fee</label>
						<input class="sr-input" type="number" min="0" bind:value={telFee} />
					</div>
					<div class="field-group">
						<label class="field-label">Link to Town</label>
						<select class="sr-input" bind:value={telTown}>
							{#each towns as t}
								<option value={t.value}>{t.label}</option>
							{/each}
						</select>
					</div>
				</div>
				<div class="field-group">
					<label class="field-label">From Character <span class="field-hint">(NPC placed here)</span></label>
					<input class="sr-input" type="text" placeholder="Character at NPC spawn point" bind:value={telFromChar} />
				</div>
				<div class="field-group">
					<label class="field-label">To Character <span class="field-hint">(destination)</span></label>
					<input class="sr-input" type="text" placeholder="Character at destination point" bind:value={telToChar} />
				</div>
				<div class="field-group">
					<label class="field-label">Required Level <span class="field-hint">(0 = no requirement)</span></label>
					<input class="sr-input" type="number" min="0" max="130" bind:value={telReqLvl} />
				</div>
			</div>
			<div class="card__footer">
				{#if telState.msg}
					<p class="inline-msg" class:inline-msg--ok={telState.ok} class:inline-msg--err={!telState.ok}>{telState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={!telCode.trim() || !telFromChar.trim() || !telToChar.trim()} loading={telState.loading} on:click={handleAddTeleporter}>
					Add Teleporter
				</SrButton>
			</div>
		</div>

		<!-- Add reverse point -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Add Reverse Point</h2>
				<span class="card__sub">Creates an optional teleport destination in <code>_RefOptionalTeleport</code></span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Zone Name <span class="field-hint">(SN_ prefix added automatically)</span></label>
					<input class="sr-input" type="text" placeholder="e.g. CaveEntrance" bind:value={rpZone} />
				</div>
				<div class="field-group">
					<label class="field-label">Region ID</label>
					<input class="sr-input" type="number" bind:value={rpRegion} />
				</div>
				<div class="field-row">
					<div class="field-group">
						<label class="field-label">Pos X</label>
						<input class="sr-input" type="number" bind:value={rpPosX} />
					</div>
					<div class="field-group">
						<label class="field-label">Pos Y</label>
						<input class="sr-input" type="number" bind:value={rpPosY} />
					</div>
				</div>
				<div class="field-group">
					<label class="field-label">Pos Z</label>
					<input class="sr-input" type="number" bind:value={rpPosZ} />
				</div>
				<div class="hint-box">
					After adding, append the entry to <code>textdata_object.txt</code> and run DB2PK2.
				</div>
			</div>
			<div class="card__footer">
				{#if rpState.msg}
					<p class="inline-msg" class:inline-msg--ok={rpState.ok} class:inline-msg--err={!rpState.ok}>{rpState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={!rpZone.trim()} loading={rpState.loading} on:click={handleReversePoint}>
					Add Reverse Point
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Section: Monster Spawns ──────────────────────────────────────── -->
	<div class="section-label">Monster Spawns</div>
	<div class="card-grid">

		<!-- By Monster Group — full width -->
		<div class="card card--wide">
			<div class="card__header">
				<h2 class="card__title">Spawn Cap — By Monster Group</h2>
				<span class="card__sub">Apply a spawn cap to all monsters in a specific area group, or to every group at once</span>
			</div>
			<div class="card__body card__body--cols">
				<div class="col">
					<div class="field-group">
						<label class="field-label">Monster Group</label>
						<select
							class="sr-input"
							bind:value={spawnGroupPrefix}
							disabled={spawnGroupAll}
							on:change={onGroupFormChange}
						>
							{#each MOB_GROUPS as g}
								<option value={g.prefix}>{g.label}</option>
							{/each}
						</select>
					</div>
					<div class="field-group">
						<label class="field-label">Max Count</label>
						<input class="sr-input" type="number" min="0" bind:value={spawnGroupMax} on:input={onGroupFormChange} />
					</div>
				</div>
				<div class="col">
					<label class="all-groups-toggle">
						<input type="checkbox" bind:checked={spawnGroupAll} on:change={onGroupFormChange} />
						<span class="all-groups-label">All Groups</span>
						<span class="field-hint">— runs each area group individually in sequence</span>
					</label>
					{#if spawnGroupAll}
						<div class="hint-box hint-box--warn">
							This will update spawners across <strong>all {MOB_GROUPS.length} monster groups</strong>.
							Each group is processed one at a time. Run <strong>Fix Unique Spawns</strong> afterwards if targeting all groups.
						</div>
					{/if}
					{#if spawnGroupConfirm}
						<div class="confirm-strip">
							Click again to confirm — all {MOB_GROUPS.length} groups will be updated.
						</div>
					{/if}
				</div>
			</div>
			<div class="card__footer">
				{#if spawnGroupState.msg}
					<p class="inline-msg" class:inline-msg--ok={spawnGroupState.ok} class:inline-msg--err={!spawnGroupState.ok}>{spawnGroupState.msg}</p>
				{/if}
				<SrButton
					variant={spawnGroupAll && spawnGroupConfirm ? 'danger' : 'primary'}
					loading={spawnGroupState.loading}
					on:click={handleGroupSpawnCap}
				>
					{spawnGroupAll && spawnGroupConfirm ? '⚠ Confirm — Apply to All Groups' : 'Set Spawn Cap'}
				</SrButton>
			</div>
		</div>

		<!-- By Exact Codename -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Spawn Cap — Exact Codename</h2>
				<span class="card__sub">Target all spawners for one specific monster without touching anything else</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Monster Code Name <span class="field-hint">(exact match)</span></label>
					<input class="sr-input" type="text" placeholder="e.g. MOB_CH_TIGERWOMAN" bind:value={spawnExactCode} />
				</div>
				<div class="field-group">
					<label class="field-label">Max Count</label>
					<input class="sr-input" type="number" min="0" bind:value={spawnExactMax} />
				</div>
			</div>
			<div class="card__footer">
				{#if spawnExactState.msg}
					<p class="inline-msg" class:inline-msg--ok={spawnExactState.ok} class:inline-msg--err={!spawnExactState.ok}>{spawnExactState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={!spawnExactCode.trim()} loading={spawnExactState.loading} on:click={handleExactSpawnCap}>
					Set Spawn Cap
				</SrButton>
			</div>
		</div>

		<!-- Fix unique spawns -->
		<div class="card card--utility">
			<div class="card__header">
				<h2 class="card__title">Fix Unique Spawns</h2>
				<span class="card__sub">Resets all unique bosses to a max of 1 simultaneous instance</span>
			</div>
			<div class="card__body">
				<p class="utility-desc">
					Applies to all known unique bosses — Tiger Woman, Uruchi, Bonelord, Kerberos, Ivy, Lord Treasure,
					Isyutaru, Blacksnake, Apis, Tomb Generals, Snake Generals, Tomb Guardians, and Tahomet variants.
				</p>
				{#if fixConfirm}
					<div class="confirm-strip">
						Click again to confirm — this affects all unique spawners in the database.
					</div>
				{/if}
			</div>
			<div class="card__footer">
				{#if fixState.msg}
					<p class="inline-msg" class:inline-msg--ok={fixState.ok} class:inline-msg--err={!fixState.ok}>{fixState.msg}</p>
				{/if}
				<SrButton
					variant={fixConfirm ? 'danger' : 'secondary'}
					loading={fixState.loading}
					on:click={handleFixUniqueSpawns}
				>
					{fixConfirm ? '⚠ Confirm Fix' : 'Fix Unique Spawns'}
				</SrButton>
			</div>
		</div>

		<!-- Spawn Count Query — full width -->
		<div class="card card--wide">
			<div class="card__header">
				<h2 class="card__title">Query Spawn Counts</h2>
				<span class="card__sub">View the current max spawn count for every monster in a group</span>
			</div>
			<div class="card__body card__body--cols">
				<div class="col spawn-query-controls">
					<div class="field-group">
						<label class="field-label" for="spawn-query-group">Monster Group</label>
						<select id="spawn-query-group" class="sr-input" bind:value={spawnQueryPrefix}>
							{#each MOB_GROUPS as g}
								<option value={g.prefix}>{g.label}</option>
							{/each}
						</select>
					</div>
					{#if spawnQueryState.msg}
						<p class="inline-msg" class:inline-msg--ok={spawnQueryState.ok} class:inline-msg--err={!spawnQueryState.ok}>{spawnQueryState.msg}</p>
					{/if}
					<SrButton variant="primary" loading={spawnQueryState.loading} on:click={handleSpawnCountQuery}>
						Query
					</SrButton>
				</div>
				<div class="col">
					{#if spawnQueryRows.length > 0}
						<div class="spawn-table-wrap">
							<table class="spawn-table">
								<thead>
									<tr>
										<th>Code Name</th>
										<th class="spawn-count-col">Max Count</th>
									</tr>
								</thead>
								<tbody>
									{#each spawnQueryRows as row}
										<tr>
											<td class="spawn-code">{row.mobCode}</td>
											<td class="spawn-count">{row.maxCount}</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</div>
					{:else if !spawnQueryState.loading}
						<p class="spawn-empty">Select a group and click Query to view current spawn counts.</p>
					{/if}
				</div>
			</div>
		</div>

	</div>
</div>

<style>
	.page {
		padding: 2rem;
		max-width: 1000px;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	/* ── Info strip ─── */
	.info-strip {
		display: flex;
		align-items: flex-start;
		gap: 0.75rem;
		background: var(--steel-dark);
		border: 1px solid var(--steel);
		border-left: 3px solid var(--steel-bright);
		border-radius: var(--radius);
		padding: 0.75rem 1rem;
	}

	.info-strip__icon {
		color: var(--steel-bright);
		flex-shrink: 0;
		margin-top: 1px;
	}

	.info-strip__text {
		font-size: 0.85rem;
		color: var(--text-muted);
		line-height: 1.5;
	}

	.info-strip__text strong { color: var(--gold); }

	.section-label {
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.12em;
		text-transform: uppercase;
		color: var(--text-dim);
		border-bottom: 1px solid var(--border-dark);
		padding-bottom: 0.35rem;
		margin-top: 0.5rem;
	}

	/* ── Grids ─── */
	.card-grid {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
	}

	@media (max-width: 640px) { .card-grid { grid-template-columns: 1fr; } }

	/* ── Cards ─── */
	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		display: flex;
		flex-direction: column;
	}

	.card--wide {
		grid-column: 1 / -1;
	}

	.card--utility {
		border-color: var(--border-dark);
	}

	.card__header {
		padding: 0.8rem 1rem 0.55rem;
		border-bottom: 1px solid var(--border-dark);
	}

	.card__title {
		font-family: var(--font-heading);
		font-size: 0.88rem;
		color: var(--text-bright);
		letter-spacing: 0.07em;
		text-transform: uppercase;
		margin-bottom: 0.15rem;
	}

	.card__sub {
		font-size: 0.74rem;
		color: var(--text-muted);
	}

	.card__sub code {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		color: var(--steel-bright);
	}

	.card__body {
		flex: 1;
		padding: 0.85rem 1rem;
		display: flex;
		flex-direction: column;
		gap: 0.65rem;
	}

	.card__body--cols {
		flex-direction: row;
		gap: 1.5rem;
	}

	@media (max-width: 640px) { .card__body--cols { flex-direction: column; gap: 0.65rem; } }

	.col {
		flex: 1;
		display: flex;
		flex-direction: column;
		gap: 0.65rem;
	}

	.card__footer {
		padding: 0.65rem 1rem 0.85rem;
		border-top: 1px solid var(--border-dark);
		display: flex;
		flex-direction: column;
		gap: 0.45rem;
	}

	/* ── Inputs ─── */
	.sr-input {
		width: 100%;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.4rem 0.65rem;
		color: var(--text-base);
		font-family: var(--font-body);
		font-size: 0.88rem;
		outline: none;
		transition: border-color 0.15s;
	}
	.sr-input:focus { border-color: var(--border-accent); }
	select.sr-input { cursor: pointer; }

	.field-group {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
	}

	.field-row {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 0.5rem;
	}

	.field-label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.field-hint {
		font-family: var(--font-body);
		font-size: 0.72rem;
		color: var(--text-dim);
		text-transform: none;
		letter-spacing: 0;
	}

	/* ── Hint box ─── */
	/* ── All-groups toggle ─── */
	.all-groups-toggle {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		cursor: pointer;
		padding: 0.3rem 0;
	}

	.all-groups-toggle input { accent-color: var(--gold); cursor: pointer; }

	.all-groups-label {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.07em;
		text-transform: uppercase;
		color: var(--text-base);
	}

	.hint-box--warn {
		border-left-color: var(--red-dark);
	}

	.hint-box--warn strong { color: var(--red-bright); }

	.hint-box {
		font-size: 0.76rem;
		color: var(--text-dim);
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-left: 2px solid var(--gold-dim);
		border-radius: var(--radius);
		padding: 0.4rem 0.6rem;
		line-height: 1.45;
	}

	.hint-box code {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		color: var(--steel-bright);
	}

	/* ── Utility card extras ─── */
	.utility-desc {
		font-size: 0.83rem;
		color: var(--text-muted);
		line-height: 1.55;
	}

	.confirm-strip {
		font-size: 0.8rem;
		color: var(--red-bright);
		background: rgba(92, 16, 16, 0.15);
		border: 1px solid var(--red-dark);
		border-radius: var(--radius);
		padding: 0.4rem 0.65rem;
	}

	/* ── Region list ─── */
	.region-loading {
		font-size: 0.83rem;
		color: var(--text-dim);
		padding: 0.25rem 0;
	}

	.region-list {
		display: flex;
		flex-direction: column;
		gap: 0;
		max-height: 380px;
		overflow-y: auto;
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
	}

	.region-row {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		padding: 0.45rem 0.75rem;
		border-bottom: 1px solid var(--border-dark);
		transition: background 0.12s;
	}
	.region-row:last-child { border-bottom: none; }
	.region-row:hover { background: var(--bg-raised); }
	.region-row--dirty { background: rgba(139, 94, 28, 0.07); }

	.region-name {
		flex: 1;
		display: flex;
		flex-direction: column;
		gap: 0.05rem;
		overflow: hidden;
	}

	.region-name > :first-child {
		font-size: 0.82rem;
		color: var(--text-base);
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	.region-code {
		display: block;
		font-family: var(--font-mono, monospace);
		font-size: 0.65rem;
		color: var(--text-dim);
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	.region-status {
		font-family: var(--font-heading);
		font-size: 0.6rem;
		letter-spacing: 0.07em;
		text-transform: uppercase;
		color: var(--text-dim);
		width: 52px;
		text-align: right;
		flex-shrink: 0;
	}
	.region-status--on { color: var(--green-bright); }

	/* ── Toggle switch ─── */
	.toggle {
		position: relative;
		display: inline-flex;
		align-items: center;
		cursor: pointer;
		flex-shrink: 0;
	}
	.toggle input { position: absolute; opacity: 0; width: 0; height: 0; }
	.toggle__track {
		width: 32px;
		height: 17px;
		background: var(--bg-deep);
		border: 1px solid var(--border-mid);
		border-radius: 9px;
		transition: background 0.2s, border-color 0.2s;
		position: relative;
	}
	.toggle__track::after {
		content: '';
		position: absolute;
		top: 2px;
		left: 2px;
		width: 11px;
		height: 11px;
		border-radius: 50%;
		background: var(--text-dim);
		transition: transform 0.2s, background 0.2s;
	}
	.toggle input:checked + .toggle__track {
		background: rgba(0, 180, 80, 0.18);
		border-color: var(--green-bright);
	}
	.toggle input:checked + .toggle__track::after {
		transform: translateX(15px);
		background: var(--green-bright);
	}

	/* ── Spawn count query ─── */
	.spawn-query-controls {
		max-width: 260px;
		flex-shrink: 0;
		display: flex;
		flex-direction: column;
		gap: 0.65rem;
	}

	.spawn-table-wrap {
		max-height: 360px;
		overflow-y: auto;
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
	}

	.spawn-table {
		width: 100%;
		border-collapse: collapse;
		font-size: 0.8rem;
	}

	.spawn-table th {
		position: sticky;
		top: 0;
		background: var(--bg-raised);
		color: var(--gold-light);
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		text-align: left;
		padding: 0.4rem 0.7rem;
		border-bottom: 1px solid var(--border-dark);
		white-space: nowrap;
	}

	.spawn-table td {
		padding: 0.3rem 0.7rem;
		border-bottom: 1px solid var(--border-dark);
		font-family: var(--font-mono, monospace);
		color: var(--text-base);
		white-space: nowrap;
	}

	.spawn-table tr:last-child td { border-bottom: none; }
	.spawn-table tr:hover td      { background: var(--bg-raised); }

	.spawn-count-col { width: 90px; text-align: right !important; }
	.spawn-count     { text-align: right; color: var(--gold); }

	.spawn-empty {
		font-size: 0.82rem;
		color: var(--text-dim);
		padding: 0.5rem 0;
	}

	/* ── Inline messages ─── */
	.inline-msg {
		font-size: 0.78rem;
		padding: 0.35rem 0.55rem;
		border-radius: var(--radius);
		border: 1px solid;
	}

	.inline-msg--ok {
		border-color: var(--green-light);
		background: rgba(39, 96, 24, 0.12);
		color: var(--green-bright);
	}

	.inline-msg--err {
		border-color: var(--red-light);
		background: rgba(92, 16, 16, 0.18);
		color: var(--red-bright);
	}
</style>
