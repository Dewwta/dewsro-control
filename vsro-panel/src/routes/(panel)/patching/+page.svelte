<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import StatusBadge from '$lib/components/ui/StatusBadge.svelte';
	import { statusStore } from '$lib/stores/serverStatus';
	import { patchApi, type GameServerCurrentValues, type GameServerPatchRequest, type NodeIniValues, type NodeIniPatchRequest } from '$lib/api/serverApi';

	// ── Server state ─────────────────────────────────────────────────────────
	$: serverStatus   = $statusStore.data;
	$: isRunning      = serverStatus?.isRunning ?? false;
	$: modulesOpen    = serverStatus?.modulesOpened ?? 0;

	// ── Current Game Server values ───────────────────────────────────────────
	let current: GameServerCurrentValues | null = null;
	let loadError = '';
	let loading   = true;

	// ── Game Server patch form ───────────────────────────────────────────────
	let gs_maxLevel: string       = '';
	let gs_masteryLimit: string   = '';
	let gs_ip: string             = '';
	let gs_objectLimit: string    = '';
	let gs_fixRates               = false;
	let gs_disableDumps           = false;
	let gs_disableGreenBook       = false;

	// ── IP patch forms ───────────────────────────────────────────────────────
	let mm_ip = '';
	let as_ip = '';

	// ── Message state ────────────────────────────────────────────────────────
	type MsgType = 'success' | 'error' | 'info';

	let gsMsg     = ''; let gsMsgType: MsgType = 'info'; let gsBusy = false; let gsConfirm = false;
	let mmMsg     = ''; let mmMsgType: MsgType = 'info'; let mmBusy = false; let mmConfirm = false;
	let asMsg     = ''; let asMsgType: MsgType = 'info'; let asBusy = false; let asConfirm = false;
	let gsApplied: string[] = [];

	let gsConfirmTimer: ReturnType<typeof setTimeout> | null = null;
	let mmConfirmTimer: ReturnType<typeof setTimeout> | null = null;
	let asConfirmTimer: ReturnType<typeof setTimeout> | null = null;

	// ── Node INI state ───────────────────────────────────────────────────────
	let iniValues: NodeIniValues | null = null;
	let iniLoadError = '';
	let iniLoading = true;

	let ini_ip          = '';
	let ini_shardName   = '';
	let ini_accountQuery = '';
	let ini_shardQuery  = '';
	let ini_logQuery    = '';

	let iniMsg = ''; let iniMsgType: MsgType = 'info'; let iniBusy = false; let iniConfirm = false;
	let iniConfirmTimer: ReturnType<typeof setTimeout> | null = null;

	onMount(async () => {
		// Load GameServer values
		try {
			current = await patchApi.getGameServerValues();
			gs_maxLevel     = String(current.maxLevel);
			gs_masteryLimit = String(current.masteryLimit);
			gs_ip           = current.spoofedIP ?? '';
			gs_objectLimit  = String(current.objectLimit);
			gs_fixRates          = current.ratesFixed;
			gs_disableDumps      = current.dumpsDisabled;
			gs_disableGreenBook  = current.greenBookDisabled;
		} catch (e) {
			loadError = e instanceof Error ? e.message : String(e);
		} finally {
			loading = false;
		}

		// Load Node INI values
		try {
			iniValues = await patchApi.getNodeIniValues();
			ini_ip           = iniValues.wip ?? '';
			ini_shardName    = iniValues.shardName ?? '';
			ini_accountQuery = iniValues.accountQuery ?? '';
			ini_shardQuery   = iniValues.shardQuery ?? '';
			ini_logQuery     = iniValues.logQuery ?? '';
		} catch (e) {
			iniLoadError = e instanceof Error ? e.message : String(e);
		} finally {
			iniLoading = false;
		}
	});

	// ── Helpers ──────────────────────────────────────────────────────────────

	function parseOptInt(val: string): number | null {
		const n = parseInt(val, 10);
		return isNaN(n) ? null : n;
	}

	function armConfirm(
		setFlag: (v: boolean) => void,
		timer: ReturnType<typeof setTimeout> | null,
		setTimer: (t: ReturnType<typeof setTimeout>) => void
	) {
		setFlag(true);
		if (timer) clearTimeout(timer);
		setTimer(setTimeout(() => setFlag(false), 5000));
	}

	// ── Game Server apply ────────────────────────────────────────────────────

	async function handleApplyGameServer() {
		if (!gsConfirm) {
			armConfirm(
				v => (gsConfirm = v),
				gsConfirmTimer,
				t => (gsConfirmTimer = t)
			);
			gsMsg = 'Click Apply again to confirm patching SR_GameServer.exe.';
			gsMsgType = 'info';
			return;
		}
		gsConfirm = false;
		if (gsConfirmTimer) clearTimeout(gsConfirmTimer);

		gsBusy    = true;
		gsMsg     = '';
		gsApplied = [];

		const req: GameServerPatchRequest = {
			maxLevel:         parseOptInt(gs_maxLevel),
			masteryLimit:     parseOptInt(gs_masteryLimit),
			fixRates:         gs_fixRates,
			disableDumpFiles: gs_disableDumps,
			disableGreenBook: gs_disableGreenBook,
			ipToSet:          gs_ip.trim() || null,
			objectLimitToSet: parseOptInt(gs_objectLimit)
		};

		try {
			const res = await patchApi.patchGameServer(req);
			gsMsg     = res.message;
			gsMsgType = 'success';
			gsApplied = res.applied ?? [];
			// Refresh current values
			current = await patchApi.getGameServerValues();
		} catch (e) {
			gsMsg     = e instanceof Error ? e.message : String(e);
			gsMsgType = 'error';
		} finally {
			gsBusy = false;
		}
	}

	// ── Machine Manager apply ────────────────────────────────────────────────

	async function handleApplyMachineManager() {
		if (!mmConfirm) {
			armConfirm(v => (mmConfirm = v), mmConfirmTimer, t => (mmConfirmTimer = t));
			mmMsg = 'Click Apply again to confirm patching MachineManager.exe.';
			mmMsgType = 'info';
			return;
		}
		mmConfirm = false;
		if (mmConfirmTimer) clearTimeout(mmConfirmTimer);

		mmBusy = true;
		mmMsg  = '';
		try {
			const res = await patchApi.patchMachineManagerIP(mm_ip.trim());
			mmMsg     = res.message;
			mmMsgType = 'success';
		} catch (e) {
			mmMsg     = e instanceof Error ? e.message : String(e);
			mmMsgType = 'error';
		} finally {
			mmBusy = false;
		}
	}

	// ── Node INI apply ───────────────────────────────────────────────────────

	async function handleApplyNodeInis() {
		if (!iniConfirm) {
			armConfirm(v => (iniConfirm = v), iniConfirmTimer, t => (iniConfirmTimer = t));
			iniMsg = 'Click Apply again to confirm patching the cert INI files.';
			iniMsgType = 'info';
			return;
		}
		iniConfirm = false;
		if (iniConfirmTimer) clearTimeout(iniConfirmTimer);

		iniBusy = true;
		iniMsg  = '';

		const req: NodeIniPatchRequest = {
			ip:           ini_ip.trim()           || null,
			shardName:    ini_shardName.trim()    || null,
			accountQuery: ini_accountQuery.trim() || null,
			shardQuery:   ini_shardQuery.trim()   || null,
			logQuery:     ini_logQuery.trim()     || null,
		};

		try {
			const res = await patchApi.patchNodeInis(req);
			iniMsg     = res.message;
			iniMsgType = 'success';
			// Refresh INI display values
			iniValues = await patchApi.getNodeIniValues();
		} catch (e) {
			iniMsg     = e instanceof Error ? e.message : String(e);
			iniMsgType = 'error';
		} finally {
			iniBusy = false;
		}
	}

	// ── Agent Server apply ───────────────────────────────────────────────────

	async function handleApplyAgentServer() {
		if (!asConfirm) {
			armConfirm(v => (asConfirm = v), asConfirmTimer, t => (asConfirmTimer = t));
			asMsg = 'Click Apply again to confirm patching AgentServer.exe.';
			asMsgType = 'info';
			return;
		}
		asConfirm = false;
		if (asConfirmTimer) clearTimeout(asConfirmTimer);

		asBusy = true;
		asMsg  = '';
		try {
			const res = await patchApi.patchAgentServerIP(as_ip.trim());
			asMsg     = res.message;
			asMsgType = 'success';
		} catch (e) {
			asMsg     = e instanceof Error ? e.message : String(e);
			asMsgType = 'error';
		} finally {
			asBusy = false;
		}
	}
</script>

<PageHeader title="Binary Patching" subtitle="Patch vSRO 1.188 server executables — server must be fully offline">
	<svelte:fragment slot="actions">
		{#if serverStatus}
			<StatusBadge running={!isRunning} label={isRunning ? 'Server Online — Locked' : 'Server Offline — Safe to Patch'} size="md" />
		{/if}
	</svelte:fragment>
</PageHeader>

<div class="page">

	<!-- ── Server Running Warning ── -->
	{#if isRunning}
		<div class="warn-banner">
			<svg class="warn-banner__icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
			<div>
				<div class="warn-banner__title">Server Is Running — Patching Blocked</div>
				<div class="warn-banner__body">
					{modulesOpen} module{modulesOpen !== 1 ? 's are' : ' is'} currently active.
					All modules must be completely shut down before any patch can be applied.
					Use the Controls page to shut down the server, then return here.
				</div>
			</div>
		</div>
	{/if}

	<!-- ── Game Server Card ── -->
	<div class="patch-card" class:patch-card--locked={isRunning}>
		<div class="patch-card__header">
			<div class="patch-card__title">SR_GameServer.exe</div>
			<span class="patch-card__tag">Game Server</span>
		</div>
		<p class="patch-card__desc">
			Modifies level caps, mastery limits, rate fixes, dump suppression, green book, bind IP, and object spawn limit.
			A timestamped backup is created in <code>patch_backups/</code> beside the executable before every apply.
		</p>

		{#if loading}
			<p class="state-text">Reading current values…</p>
		{:else if loadError}
			<div class="msg msg--error">{loadError}</div>
		{:else if current}
			<!-- Current values read-out -->
			<div class="current-grid">
				<div class="current-item">
					<span class="current-item__label">Max Level</span>
					<span class="current-item__value">{current.maxLevel}</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Mastery Limit</span>
					<span class="current-item__value">{current.masteryLimit}</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Object Limit</span>
					<span class="current-item__value">{current.objectLimit.toLocaleString()}</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Spoofed IP</span>
					<span class="current-item__value current-item__value--mono">{current.spoofedIP || '—'}</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Rates Fixed</span>
					<span class="current-item__value" class:val--on={current.ratesFixed} class:val--off={!current.ratesFixed}>
						{current.ratesFixed ? 'Yes' : 'No'}
					</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Dumps Disabled</span>
					<span class="current-item__value" class:val--on={current.dumpsDisabled} class:val--off={!current.dumpsDisabled}>
						{current.dumpsDisabled ? 'Yes' : 'No'}
					</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Green Book Off</span>
					<span class="current-item__value" class:val--on={current.greenBookDisabled} class:val--off={!current.greenBookDisabled}>
						{current.greenBookDisabled ? 'Yes' : 'No'}
					</span>
				</div>
			</div>
		{/if}

		<!-- Patch form -->
		<div class="form-grid">
			<div class="field">
				<label class="field__label" for="gs-maxlevel">Max Level <span class="field__hint">(1–254)</span></label>
				<input id="gs-maxlevel" class="field__input" type="number" min="1" max="254" bind:value={gs_maxLevel} disabled={isRunning || gsBusy} />
			</div>
			<div class="field">
				<label class="field__label" for="gs-mastery">Mastery Limit <span class="field__hint">(1–900)</span></label>
				<input id="gs-mastery" class="field__input" type="number" min="1" max="900" bind:value={gs_masteryLimit} disabled={isRunning || gsBusy} />
			</div>
			<div class="field">
				<label class="field__label" for="gs-objlimit">Object Spawn Limit <span class="field__hint">(50000–100000)</span></label>
				<input id="gs-objlimit" class="field__input" type="number" min="50000" max="100000" step="1000" bind:value={gs_objectLimit} disabled={isRunning || gsBusy} />
			</div>
			<div class="field">
				<label class="field__label" for="gs-ip">Bind IP <span class="field__hint">(leave blank to skip)</span></label>
				<input id="gs-ip" class="field__input field__input--mono" type="text" placeholder="e.g. 192.168.0.11" bind:value={gs_ip} disabled={isRunning || gsBusy} />
			</div>
		</div>

		<div class="toggle-row">
			<label class="toggle" class:toggle--disabled={isRunning || gsBusy}>
				<input type="checkbox" bind:checked={gs_fixRates} disabled={isRunning || gsBusy} />
				<span class="toggle__box"></span>
				<span class="toggle__label">Fix Rate Overflow</span>
				<span class="toggle__hint">Patches 5 offsets to prevent rate calculation overflow</span>
			</label>
			<label class="toggle" class:toggle--disabled={isRunning || gsBusy}>
				<input type="checkbox" bind:checked={gs_disableDumps} disabled={isRunning || gsBusy} />
				<span class="toggle__box"></span>
				<span class="toggle__label">Disable Crash Dumps</span>
				<span class="toggle__hint">NOP-chains the dump file generation call</span>
			</label>
			<label class="toggle" class:toggle--disabled={isRunning || gsBusy}>
				<input type="checkbox" bind:checked={gs_disableGreenBook} disabled={isRunning || gsBusy} />
				<span class="toggle__box"></span>
				<span class="toggle__label">Disable Green Book System</span>
				<span class="toggle__hint">NOPs out the 12-byte book eligibility check</span>
			</label>
		</div>

		<div class="card-footer">
			{#if gsMsg}
				<span class="inline-msg inline-msg--{gsMsgType}">{gsMsg}</span>
			{:else}
				<span></span>
			{/if}
			<SrButton
				variant="primary"
				size="md"
				disabled={isRunning}
				loading={gsBusy}
				on:click={handleApplyGameServer}
			>
				{gsConfirm ? '⚠ Confirm Apply' : '▶ Apply Patches'}
			</SrButton>
		</div>

		{#if gsApplied.length}
			<div class="applied-list">
				<span class="applied-list__label">Applied:</span>
				{#each gsApplied as p}
					<span class="applied-list__item">{p}</span>
				{/each}
			</div>
		{/if}
	</div>

	<!-- ── Machine Manager Card ── -->
	<div class="patch-card" class:patch-card--locked={isRunning}>
		<div class="patch-card__header">
			<div class="patch-card__title">MachineManager.exe</div>
			<span class="patch-card__tag">Machine Manager</span>
		</div>
		<p class="patch-card__desc">
			Spoofs the IP address the Machine Manager binds to. Patches two ASM redirect bytes and writes the IP string into a 32-byte buffer.
			A backup is created before every apply.
		</p>

		<div class="form-grid form-grid--single">
			<div class="field">
				<label class="field__label" for="mm-ip">Bind IP Address</label>
				<input id="mm-ip" class="field__input field__input--mono" type="text" placeholder="e.g. 192.168.0.11" bind:value={mm_ip} disabled={isRunning || mmBusy} />
			</div>
		</div>

		<div class="card-footer">
			{#if mmMsg}
				<span class="inline-msg inline-msg--{mmMsgType}">{mmMsg}</span>
			{:else}
				<span></span>
			{/if}
			<SrButton
				variant="primary"
				size="md"
				disabled={isRunning || !mm_ip.trim()}
				loading={mmBusy}
				on:click={handleApplyMachineManager}
			>
				{mmConfirm ? '⚠ Confirm Apply' : '▶ Apply IP Patch'}
			</SrButton>
		</div>
	</div>

	<!-- ── Node INI (Cert Server) Card ── -->
	<div class="patch-card" class:patch-card--locked={isRunning}>
		<div class="patch-card__header">
			<div class="patch-card__title">Cert Server INIs</div>
			<span class="patch-card__tag">srNodeType / srGlobalService / srShard</span>
		</div>
		<p class="patch-card__desc">
			Patches the three certification server INI files — sets the world/node IP, shard name, and database connection strings.
			The cert is recompiled automatically after patching if a compile path is configured.
		</p>

		{#if iniLoading}
			<p class="state-text">Reading current INI values…</p>
		{:else if iniLoadError}
			<div class="msg msg--error">{iniLoadError}</div>
		{:else if iniValues}
			<div class="current-grid">
				<div class="current-item">
					<span class="current-item__label">World IP (wip)</span>
					<span class="current-item__value current-item__value--mono">{iniValues.wip || '—'}</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Node IP (nip)</span>
					<span class="current-item__value current-item__value--mono">{iniValues.nip || '—'}</span>
				</div>
				<div class="current-item">
					<span class="current-item__label">Shard Name</span>
					<span class="current-item__value">{iniValues.shardName || '—'}</span>
				</div>
				<div class="current-item" style="min-width: 260px;">
					<span class="current-item__label">Account Query</span>
					<span class="current-item__value current-item__value--mono" style="font-size:0.7rem;word-break:break-all;">{iniValues.accountQuery || '—'}</span>
				</div>
				<div class="current-item" style="min-width: 260px;">
					<span class="current-item__label">Shard Query</span>
					<span class="current-item__value current-item__value--mono" style="font-size:0.7rem;word-break:break-all;">{iniValues.shardQuery || '—'}</span>
				</div>
				<div class="current-item" style="min-width: 260px;">
					<span class="current-item__label">Log Query</span>
					<span class="current-item__value current-item__value--mono" style="font-size:0.7rem;word-break:break-all;">{iniValues.logQuery || '—'}</span>
				</div>
			</div>
		{/if}

		<div class="form-grid">
			<div class="field">
				<label class="field__label" for="ini-ip">IP Address <span class="field__hint">(sets wip + nip)</span></label>
				<input id="ini-ip" class="field__input field__input--mono" type="text" placeholder="e.g. 192.168.0.11" bind:value={ini_ip} disabled={isRunning || iniBusy} />
			</div>
			<div class="field">
				<label class="field__label" for="ini-shard">Shard Name</label>
				<input id="ini-shard" class="field__input" type="text" placeholder="e.g. VSRO" bind:value={ini_shardName} disabled={isRunning || iniBusy} />
			</div>
		</div>
		<div class="form-grid">
			<div class="field">
				<label class="field__label" for="ini-acctq">Account Query <span class="field__hint">(SRO_VT_ACCOUNT DSN)</span></label>
				<input id="ini-acctq" class="field__input field__input--mono" type="text" placeholder="leave blank to skip" bind:value={ini_accountQuery} disabled={isRunning || iniBusy} />
			</div>
			<div class="field">
				<label class="field__label" for="ini-shardq">Shard Query <span class="field__hint">(SRO_VT_SHARD DSN)</span></label>
				<input id="ini-shardq" class="field__input field__input--mono" type="text" placeholder="leave blank to skip" bind:value={ini_shardQuery} disabled={isRunning || iniBusy} />
			</div>
			<div class="field">
				<label class="field__label" for="ini-logq">Log Query <span class="field__hint">(SRO_VT_LOG DSN)</span></label>
				<input id="ini-logq" class="field__input field__input--mono" type="text" placeholder="leave blank to skip" bind:value={ini_logQuery} disabled={isRunning || iniBusy} />
			</div>
		</div>

		<div class="card-footer">
			{#if iniMsg}
				<span class="inline-msg inline-msg--{iniMsgType}">{iniMsg}</span>
			{:else}
				<span></span>
			{/if}
			<SrButton
				variant="primary"
				size="md"
				disabled={isRunning}
				loading={iniBusy}
				on:click={handleApplyNodeInis}
			>
				{iniConfirm ? '⚠ Confirm Apply' : '▶ Apply INI Patch'}
			</SrButton>
		</div>
	</div>

	<!-- ── Agent Server Card ── -->
	<div class="patch-card" class:patch-card--locked={isRunning}>
		<div class="patch-card__header">
			<div class="patch-card__title">AgentServer.exe</div>
			<span class="patch-card__tag">Agent Server</span>
		</div>
		<p class="patch-card__desc">
			Spoofs the IP address the Agent Server binds to. Patches two ASM redirect bytes and writes the IP string into a 35-byte buffer.
			A backup is created before every apply.
		</p>

		<div class="form-grid form-grid--single">
			<div class="field">
				<label class="field__label" for="as-ip">Bind IP Address</label>
				<input id="as-ip" class="field__input field__input--mono" type="text" placeholder="e.g. 192.168.0.11" bind:value={as_ip} disabled={isRunning || asBusy} />
			</div>
		</div>

		<div class="card-footer">
			{#if asMsg}
				<span class="inline-msg inline-msg--{asMsgType}">{asMsg}</span>
			{:else}
				<span></span>
			{/if}
			<SrButton
				variant="primary"
				size="md"
				disabled={isRunning || !as_ip.trim()}
				loading={asBusy}
				on:click={handleApplyAgentServer}
			>
				{asConfirm ? '⚠ Confirm Apply' : '▶ Apply IP Patch'}
			</SrButton>
		</div>
	</div>

</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1.25rem;
		max-width: 820px;
	}

	/* ── Warning Banner ── */
	.warn-banner {
		display: flex;
		gap: 0.9rem;
		align-items: flex-start;
		padding: 1rem 1.2rem;
		background: rgba(92, 16, 16, 0.25);
		border: 1px solid var(--red-dark);
		border-left: 3px solid var(--red-light);
		border-radius: var(--radius);
	}

	.warn-banner__icon {
		width: 20px;
		height: 20px;
		color: var(--red-light);
		flex-shrink: 0;
		margin-top: 1px;
	}

	.warn-banner__title {
		font-family: var(--font-heading);
		font-size: 0.85rem;
		letter-spacing: 0.07em;
		color: var(--red-light);
		margin-bottom: 0.3rem;
	}

	.warn-banner__body {
		font-size: 0.84rem;
		color: var(--text-muted);
		line-height: 1.6;
	}

	/* ── Patch Card ── */
	.patch-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-top: 2px solid var(--border-gold);
		border-radius: var(--radius);
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1rem;
		transition: opacity 0.2s;
	}

	.patch-card--locked {
		opacity: 0.55;
		pointer-events: none;
	}

	.patch-card__header {
		display: flex;
		align-items: center;
		gap: 0.7rem;
	}

	.patch-card__title {
		font-family: var(--font-heading);
		font-size: 0.9rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--gold-light);
	}

	.patch-card__tag {
		font-family: var(--font-heading);
		font-size: 0.6rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--text-dim);
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		padding: 2px 7px;
		border-radius: 3px;
	}

	.patch-card__desc {
		font-size: 0.85rem;
		color: var(--text-muted);
		line-height: 1.7;
		margin: 0;
	}

	.patch-card__desc code {
		font-family: var(--font-mono);
		font-size: 0.8rem;
		background: var(--bg-raised);
		padding: 1px 5px;
		border-radius: 3px;
		color: var(--gold);
	}

	/* ── Current values grid ── */
	.current-grid {
		display: flex;
		flex-wrap: wrap;
		gap: 0.5rem 1.2rem;
		padding: 0.75rem 1rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
	}

	.current-item {
		display: flex;
		flex-direction: column;
		gap: 2px;
		min-width: 90px;
	}

	.current-item__label {
		font-family: var(--font-heading);
		font-size: 0.58rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: var(--text-dim);
	}

	.current-item__value {
		font-size: 0.82rem;
		color: var(--text-base);
	}

	.current-item__value--mono {
		font-family: var(--font-mono);
		font-size: 0.78rem;
	}

	.val--on  { color: var(--green-bright); }
	.val--off { color: var(--text-dim); }

	/* ── Form grid ── */
	.form-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
		gap: 0.8rem;
	}

	.form-grid--single {
		grid-template-columns: minmax(200px, 360px);
	}

	.field {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
	}

	.field__label {
		font-family: var(--font-heading);
		font-size: 0.7rem;
		text-transform: uppercase;
		letter-spacing: 0.08em;
		color: var(--text-muted);
	}

	.field__hint {
		font-size: 0.62rem;
		color: var(--text-dim);
		text-transform: none;
		letter-spacing: 0;
	}

	.field__input {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.42rem 0.65rem;
		color: var(--text-base);
		font-family: var(--font-body);
		font-size: 0.87rem;
		outline: none;
		transition: border-color 0.15s;
		width: 100%;
		box-sizing: border-box;
	}

	.field__input--mono {
		font-family: var(--font-mono);
		font-size: 0.82rem;
	}

	.field__input:focus  { border-color: var(--border-accent); }
	.field__input:disabled { opacity: 0.45; cursor: not-allowed; }

	/* ── Toggles ── */
	.toggle-row {
		display: flex;
		flex-direction: column;
		gap: 0.6rem;
	}

	.toggle {
		display: flex;
		align-items: center;
		gap: 0.65rem;
		cursor: pointer;
		user-select: none;
	}

	.toggle--disabled {
		opacity: 0.45;
		cursor: not-allowed;
	}

	.toggle input[type='checkbox'] {
		display: none;
	}

	.toggle__box {
		width: 32px;
		height: 18px;
		border-radius: 9px;
		background: var(--bg-hover);
		border: 1px solid var(--border-mid);
		flex-shrink: 0;
		position: relative;
		transition: background 0.15s, border-color 0.15s;
	}

	.toggle__box::after {
		content: '';
		position: absolute;
		top: 2px;
		left: 2px;
		width: 12px;
		height: 12px;
		border-radius: 50%;
		background: var(--text-dim);
		transition: transform 0.15s, background 0.15s;
	}

	.toggle input:checked ~ .toggle__box {
		background: rgba(21, 45, 12, 0.6);
		border-color: var(--green);
	}

	.toggle input:checked ~ .toggle__box::after {
		transform: translateX(14px);
		background: var(--green-bright);
	}

	.toggle__label {
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.05em;
		color: var(--text-muted);
	}

	.toggle__hint {
		font-size: 0.72rem;
		color: var(--text-dim);
	}

	/* ── Card footer ── */
	.card-footer {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 0.75rem;
		padding-top: 0.25rem;
		border-top: 1px solid var(--border-dark);
	}

	/* ── Applied list ── */
	.applied-list {
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: 0.35rem;
		padding: 0.55rem 0.75rem;
		background: rgba(21, 45, 12, 0.25);
		border: 1px solid var(--green);
		border-radius: var(--radius);
	}

	.applied-list__label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: var(--green-bright);
	}

	.applied-list__item {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		padding: 1px 6px;
		border-radius: 3px;
		color: var(--text-muted);
	}

	/* ── Inline messages ── */
	.inline-msg {
		font-size: 0.78rem;
		font-family: var(--font-heading);
		letter-spacing: 0.03em;
		flex: 1;
	}
	.inline-msg--success { color: var(--green-bright); }
	.inline-msg--error   { color: var(--red-light); }
	.inline-msg--info    { color: var(--gold); }

	/* ── Misc ── */
	.state-text {
		font-size: 0.84rem;
		color: var(--text-dim);
		font-style: italic;
	}

	.msg {
		padding: 0.65rem 0.95rem;
		border-radius: var(--radius);
		border: 1px solid;
		font-family: var(--font-heading);
		font-size: 0.8rem;
		letter-spacing: 0.04em;
	}
	.msg--error { background: rgba(92,16,16,0.3); border-color: var(--red-dark); color: var(--red-light); }
</style>
