<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import { privilegedIpApi, serverCfgApi, noticeApi, backupApi, type PrivilegedIp, type ServerRates, type Notice, type BackupStatus, type BackupFileInfo } from '$lib/api/serverApi';

	// ── Privileged IPs ────────────────────────────────────────────────────────

	interface IpRow extends PrivilegedIp {
		editIp:  string;
		editIsGm: boolean;
		saving:  boolean;
		msg:     string;
		ok:      boolean;
	}

	let rows: IpRow[] = [];
	let loadError = '';
	let loading = true;

	async function loadIps() {
		loading = true;
		loadError = '';
		try {
			const data = await privilegedIpApi.getAll();
			rows = data.map(ip => ({
				...ip,
				editIp:   ip.szIPBegin,
				editIsGm: ip.isGm,
				saving:   false,
				msg:      '',
				ok:       true
			}));
		} catch (e: any) {
			loadError = e.message ?? 'Failed to load privileged IPs.';
		} finally {
			loading = false;
		}
	}

	async function saveRow(row: IpRow) {
		if (!row.editIp.trim()) return;
		row.saving = true;
		row.msg    = '';
		rows = rows;
		try {
			const res = await privilegedIpApi.update(row.nIdx, row.editIp.trim(), row.editIsGm);
			row.szIPBegin = row.editIp.trim();
			row.szIPEnd   = row.editIp.trim();
			row.isGm      = row.editIsGm;
			row.msg       = res.message;
			row.ok        = true;
		} catch (e: any) {
			row.msg = e.message ?? 'Save failed.';
			row.ok  = false;
		} finally {
			row.saving = false;
			rows = rows;
		}
	}

	$: isDirty = (row: IpRow) =>
		row.editIp.trim() !== row.szIPBegin || row.editIsGm !== row.isGm;

	// ── Server Rates ──────────────────────────────────────────────────────────

	let rates: ServerRates | null = null;
	let ratesLoading = true;
	let ratesError = '';
	let ratesSaving = false;
	let ratesMsg = '';
	let ratesMsgOk = true;

	// editable copies
	let editExpRatio = 0;
	let editExpRatioParty = 0;
	let editDropItemRatio = 0;
	let editDropGoldAmountCoef = 0;
	let editWinterEvent = false;
	let editThanksgivingEvent = false;
	let editChristmasEvent2007 = false;

	$: ratesDirty = rates !== null && (
		editExpRatio             !== rates.expRatio             ||
		editExpRatioParty        !== rates.expRatioParty        ||
		editDropItemRatio        !== rates.dropItemRatio        ||
		editDropGoldAmountCoef   !== rates.dropGoldAmountCoef   ||
		editWinterEvent          !== rates.winterEvent2009      ||
		editThanksgivingEvent    !== rates.thanksgivingEvent    ||
		editChristmasEvent2007   !== rates.christmasEvent2007
	);

	async function loadRates() {
		ratesLoading = true;
		ratesError = '';
		try {
			rates = await serverCfgApi.getRates();
			editExpRatio             = rates.expRatio;
			editExpRatioParty        = rates.expRatioParty;
			editDropItemRatio        = rates.dropItemRatio;
			editDropGoldAmountCoef   = rates.dropGoldAmountCoef;
			editWinterEvent          = rates.winterEvent2009;
			editThanksgivingEvent    = rates.thanksgivingEvent;
			editChristmasEvent2007   = rates.christmasEvent2007;
		} catch (e: any) {
			ratesError = e.message ?? 'Failed to load server rates.';
		} finally {
			ratesLoading = false;
		}
	}

	async function saveRates() {
		ratesSaving = true;
		ratesMsg = '';
		try {
			const res = await serverCfgApi.updateRates({
				expRatio:           editExpRatio,
				expRatioParty:      editExpRatioParty,
				dropItemRatio:      editDropItemRatio,
				dropGoldAmountCoef: editDropGoldAmountCoef,
				winterEvent2009:    editWinterEvent,
				thanksgivingEvent:  editThanksgivingEvent,
				christmasEvent2007: editChristmasEvent2007
			});
			rates = {
				expRatio:           editExpRatio,
				expRatioParty:      editExpRatioParty,
				dropItemRatio:      editDropItemRatio,
				dropGoldAmountCoef: editDropGoldAmountCoef,
				winterEvent2009:    editWinterEvent,
				thanksgivingEvent:  editThanksgivingEvent,
				christmasEvent2007: editChristmasEvent2007
			};
			ratesMsg = res.message;
			ratesMsgOk = true;
		} catch (e: any) {
			ratesMsg = e.message ?? 'Save failed.';
			ratesMsgOk = false;
		} finally {
			ratesSaving = false;
		}
	}

	// ── Certification IP ──────────────────────────────────────────────────────

	let certIp = '';
	let certSaving = false;
	let certMsg = '';
	let certMsgOk = true;

	async function saveCertIp() {
		if (!certIp.trim()) return;
		certSaving = true;
		certMsg = '';
		try {
			const res = await serverCfgApi.updateCertificationIp(certIp.trim());
			certMsg = res.message;
			certMsgOk = true;
		} catch (e: any) {
			certMsg = e.message ?? 'Save failed.';
			certMsgOk = false;
		} finally {
			certSaving = false;
		}
	}

	// ── Notice Board ──────────────────────────────────────────────────────────

	let notices: Notice[] = [];
	let noticesLoading = true;
	let noticesError = '';
	let noticeContentId = 22;

	// add form
	let newSubject = '';
	let newArticle = '';
	let addBusy = false;
	let addMsg = '';
	let addMsgOk = true;

	// per-row delete state
	let deletingId: number | null = null;
	let deleteMsg = '';
	let deleteMsgOk = true;

	async function loadNotices() {
		noticesLoading = true;
		noticesError = '';
		try {
			notices = await noticeApi.getAll(noticeContentId);
		} catch (e: any) {
			noticesError = e.message ?? 'Failed to load notices.';
		} finally {
			noticesLoading = false;
		}
	}

	async function handleAddNotice() {
		if (!newSubject.trim() || !newArticle.trim()) return;
		addBusy = true;
		addMsg = '';
		try {
			const res = await noticeApi.add(newSubject.trim(), newArticle.trim(), noticeContentId);
			addMsg = res.message;
			addMsgOk = true;
			newSubject = '';
			newArticle = '';
			await loadNotices();
		} catch (e: any) {
			addMsg = e.message ?? 'Failed to add notice.';
			addMsgOk = false;
		} finally {
			addBusy = false;
		}
	}

	async function handleDeleteNotice(id: number) {
		deletingId = id;
		deleteMsg = '';
		try {
			const res = await noticeApi.delete(id);
			deleteMsg = res.message;
			deleteMsgOk = true;
			await loadNotices();
		} catch (e: any) {
			deleteMsg = e.message ?? 'Failed to delete notice.';
			deleteMsgOk = false;
		} finally {
			deletingId = null;
		}
	}

	// ── Database Backups ──────────────────────────────────────────────────────

	let backupStatus: BackupStatus | null = null;
	let backupStatusLoading = true;
	let backupStatusError = '';
	let backupRunning = false;
	let backupMsg = '';
	let backupMsgOk = true;

	// Group files by database for display
	$: backupsByDb = backupStatus
		? backupStatus.files.reduce<Record<string, BackupFileInfo[]>>((acc, f) => {
			(acc[f.database] ??= []).push(f);
			return acc;
		  }, {})
		: {};

	function formatBytes(bytes: number): string {
		if (bytes < 1024)          return `${bytes} B`;
		if (bytes < 1024 * 1024)   return `${(bytes / 1024).toFixed(1)} KB`;
		return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
	}

	function formatDate(iso: string): string {
		return new Date(iso).toLocaleString();
	}

	async function loadBackupStatus() {
		backupStatusLoading = true;
		backupStatusError = '';
		try {
			backupStatus = await backupApi.status();
		} catch (e: any) {
			backupStatusError = e.message ?? 'Failed to load backup status.';
		} finally {
			backupStatusLoading = false;
		}
	}

	async function handleBackupNow() {
		backupRunning = true;
		backupMsg = '';
		try {
			const res = await backupApi.run();
			backupMsg = res.message;
			backupMsgOk = true;
			await loadBackupStatus();
		} catch (e: any) {
			backupMsg = e.message ?? 'Backup failed.';
			backupMsgOk = false;
		} finally {
			backupRunning = false;
		}
	}

	onMount(() => {
		loadIps();
		loadRates();
		loadNotices();
		loadBackupStatus();
	});
</script>

<div class="page">
	<PageHeader title="More Admin Controls" subtitle="Privileged IPs, server rates, and global Certification IP — all sourced from server.cfg" />

	<!-- ── Privileged IPs ─────────────────────────────────────────────────── -->
	<section class="control-box">
		<h2 class="box-title">Privileged IP Addresses</h2>
		<p class="box-desc">
			Controls which IPs receive privileged (GM) access on the gateway.
			Saving an entry updates both <strong>szIPBegin</strong> and <strong>szIPEnd</strong> to the same value.
		</p>

		{#if loading}
			<div class="state-msg">Loading…</div>
		{:else if loadError}
			<div class="state-msg state-msg--err">{loadError}</div>
		{:else if rows.length === 0}
			<div class="state-msg">No privileged IP entries found.</div>
		{:else}
			<div class="ip-table-wrap">
				<table class="ip-table">
					<thead>
						<tr>
							<th>nIdx</th>
							<th>IP Address</th>
							<th class="th-center">GM</th>
							<th>Issue Date</th>
							<th>ISP</th>
							<th>Description</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						{#each rows as row (row.nIdx)}
							<tr class:row--dirty={isDirty(row)}>
								<td class="td-idx">{row.nIdx}</td>
								<td class="td-ip">
									<input
										class="cfg-input"
										type="text"
										bind:value={row.editIp}
										on:input={() => { rows = rows; }}
										placeholder="0.0.0.0"
										spellcheck="false"
									/>
								</td>
								<td class="td-center">
									<label class="toggle" title={row.editIsGm ? 'GM — click to remove' : 'Not GM — click to grant'}>
										<input
											type="checkbox"
											bind:checked={row.editIsGm}
											on:change={() => { rows = rows; }}
										/>
										<span class="toggle__track"></span>
									</label>
								</td>
								<td class="td-meta">{new Date(row.dIssueDate).toLocaleDateString()}</td>
								<td class="td-meta">{row.szISP ?? '—'}</td>
								<td class="td-meta td-desc">{row.szDesc ?? '—'}</td>
								<td class="td-actions">
									<div class="td-actions__row">
										{#if row.msg}
											<span class="row-msg" class:row-msg--ok={row.ok} class:row-msg--err={!row.ok}>{row.msg}</span>
										{/if}
										<button
											class="save-btn"
											class:save-btn--active={isDirty(row)}
											disabled={row.saving || !isDirty(row)}
											on:click={() => saveRow(row)}
										>
											{row.saving ? 'Saving…' : 'Save'}
										</button>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	</section>

	<!-- ── Server Rates ───────────────────────────────────────────────────── -->
	<section class="control-box">
		<h2 class="box-title">Server Rates &amp; Events</h2>
		<p class="box-desc">
			Edits the <strong>SR_GameServer</strong> and <strong>SR_ShardManager</strong> blocks in <code>server.cfg</code>.
			Rates use the raw scale (e.g. 500 = 5×, 100 = 1×). Christmas Event 2007 is in SR_ShardManager (0/1).
			Restart the server for changes to take effect.
		</p>

		{#if ratesLoading}
			<div class="state-msg">Loading…</div>
		{:else if ratesError}
			<div class="state-msg state-msg--err">{ratesError}</div>
		{:else}
			<div class="fields-grid">
				<div class="field">
					<label class="field-label" for="expRatio">Exp Ratio</label>
					<input id="expRatio" class="cfg-input" type="number" bind:value={editExpRatio} min="0" />
				</div>
				<div class="field">
					<label class="field-label" for="expRatioParty">Exp Ratio (Party)</label>
					<input id="expRatioParty" class="cfg-input" type="number" bind:value={editExpRatioParty} min="0" />
				</div>
				<div class="field">
					<label class="field-label" for="dropItemRatio">Drop Item Ratio</label>
					<input id="dropItemRatio" class="cfg-input" type="number" bind:value={editDropItemRatio} min="0" />
				</div>
				<div class="field">
					<label class="field-label" for="dropGold">Drop Gold Amount Coef</label>
					<input id="dropGold" class="cfg-input" type="number" bind:value={editDropGoldAmountCoef} min="0" />
				</div>
				<div class="field field--full">
					<span class="field-label">Winter Event 2009</span>
					<label class="event-toggle">
						<input type="checkbox" bind:checked={editWinterEvent} />
						<span class="toggle__track"></span>
						<span class="event-toggle__label" class:event-toggle__label--on={editWinterEvent}>
							{editWinterEvent ? 'EVENT_ON' : 'EVENT_OFF'}
						</span>
					</label>
				</div>
				<div class="field field--full">
					<span class="field-label">Thanksgiving Event</span>
					<label class="event-toggle">
						<input type="checkbox" bind:checked={editThanksgivingEvent} />
						<span class="toggle__track"></span>
						<span class="event-toggle__label" class:event-toggle__label--on={editThanksgivingEvent}>
							{editThanksgivingEvent ? 'EVENT_ON' : 'EVENT_OFF'}
						</span>
					</label>
				</div>
				<div class="field field--full">
					<span class="field-label">Christmas Event 2007</span>
					<label class="event-toggle">
						<input type="checkbox" bind:checked={editChristmasEvent2007} />
						<span class="toggle__track"></span>
						<span class="event-toggle__label" class:event-toggle__label--on={editChristmasEvent2007}>
							{editChristmasEvent2007 ? 'ON' : 'OFF'}
						</span>
					</label>
				</div>
			</div>

			<div class="box-footer">
				{#if ratesMsg}
					<span class="row-msg" class:row-msg--ok={ratesMsgOk} class:row-msg--err={!ratesMsgOk}>{ratesMsg}</span>
				{/if}
				<button
					class="save-btn"
					class:save-btn--active={ratesDirty}
					disabled={ratesSaving || !ratesDirty}
					on:click={saveRates}
				>
					{ratesSaving ? 'Saving…' : 'Save Rates'}
				</button>
			</div>
		{/if}
	</section>

	<!-- ── Notice Board ───────────────────────────────────────────────────── -->
	<section class="control-box">
		<h2 class="box-title">Notice Board</h2>
		<p class="box-desc">
			Manages entries in the <strong>_Notice</strong> table used for in-game announcements.
			Filter by <strong>ContentID</strong> (default 22). Subject max 80 chars, Article max 1024 chars.
		</p>

		<div class="warn-banner">
			<span class="warn-banner__icon">⚠</span>
			<span>
				<strong>Do NOT change ContentID</strong> unless you are absolutely certain of what you are doing.
				The game client expects specific ContentID values — changing them will break notice visibility in-game.
			</span>
		</div>

		<!-- ContentID filter -->
		<div class="notice-filter">
			<label class="field-label" for="noticeContentId">ContentID Filter</label>
			<div class="notice-filter__row">
				<input
					id="noticeContentId"
					class="cfg-input cfg-input--narrow"
					type="number"
					bind:value={noticeContentId}
					min="1"
				/>
				<button class="save-btn save-btn--active" on:click={loadNotices}>Refresh</button>
			</div>
		</div>

		<!-- Table -->
		{#if noticesLoading}
			<div class="state-msg">Loading…</div>
		{:else if noticesError}
			<div class="state-msg state-msg--err">{noticesError}</div>
		{:else if notices.length === 0}
			<div class="state-msg">No notices found for ContentID {noticeContentId}.</div>
		{:else}
			<div class="ip-table-wrap">
				<table class="ip-table">
					<thead>
						<tr>
							<th>ID</th>
							<th>ContentID</th>
							<th>Subject</th>
							<th>Article</th>
							<th>Edit Date</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						{#each notices as n (n.id)}
							<tr>
								<td class="td-idx">{n.id}</td>
								<td class="td-idx">{n.contentID}</td>
								<td class="td-meta">{n.subject}</td>
								<td class="td-article">{n.article}</td>
								<td class="td-meta">{new Date(n.editDate).toLocaleString()}</td>
								<td class="td-actions">
									<div class="td-actions__row">
										<button
											class="save-btn save-btn--danger"
											disabled={deletingId === n.id}
											on:click={() => handleDeleteNotice(n.id)}
										>
											{deletingId === n.id ? 'Deleting…' : 'Delete'}
										</button>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}

		{#if deleteMsg}
			<span class="row-msg" class:row-msg--ok={deleteMsgOk} class:row-msg--err={!deleteMsgOk}>{deleteMsg}</span>
		{/if}

		<!-- Add notice form -->
		<div class="notice-add-form">
			<h3 class="notice-add-form__title">Add Notice</h3>
			<div class="fields-grid">
				<div class="field">
					<label class="field-label" for="newSubject">Subject <span class="char-hint">(max 80)</span></label>
					<input
						id="newSubject"
						class="cfg-input"
						type="text"
						bind:value={newSubject}
						maxlength="80"
						placeholder="Notice title…"
					/>
				</div>
				<div class="field field--full">
					<label class="field-label" for="newArticle">Article <span class="char-hint">(max 1024)</span></label>
					<textarea
						id="newArticle"
						class="cfg-textarea"
						bind:value={newArticle}
						maxlength="1024"
						rows="4"
						placeholder="Notice body text…"
					></textarea>
				</div>
			</div>
			<div class="box-footer">
				{#if addMsg}
					<span class="row-msg" class:row-msg--ok={addMsgOk} class:row-msg--err={!addMsgOk}>{addMsg}</span>
				{/if}
				<button
					class="save-btn"
					class:save-btn--active={newSubject.trim().length > 0 && newArticle.trim().length > 0}
					disabled={addBusy || !newSubject.trim() || !newArticle.trim()}
					on:click={handleAddNotice}
				>
					{addBusy ? 'Adding…' : 'Add Notice'}
				</button>
			</div>
		</div>
	</section>

	<!-- ── Database Backups ─────────────────────────────────────────────── -->
	<section class="control-box">
		<h2 class="box-title">Database Backups</h2>
		<p class="box-desc">
			Backs up <strong>SRO_VT_ACCOUNT</strong>, <strong>SRO_VT_SHARD</strong>, and <strong>SRO_VT_LOG</strong>
			to the path configured in <code>settings.xml</code> (<code>BackupPath</code>).
			Auto-backup runs every <strong>2 hours</strong>. A maximum of <strong>10</strong> files per database are kept —
			the oldest are deleted automatically. Files are written by the SQL Server service account.
		</p>

		{#if backupStatusLoading}
			<div class="state-msg">Loading…</div>
		{:else if backupStatusError}
			<div class="state-msg state-msg--err">{backupStatusError}</div>
		{:else if backupStatus}
			<div class="backup-meta">
				<div class="backup-meta__item">
					<span class="field-label">Status</span>
					<span class="backup-badge" class:backup-badge--busy={backupStatus.isBusy}>
						{backupStatus.isBusy ? '⟳ Running…' : '● Idle'}
					</span>
				</div>
				{#if backupStatus.lastRunAt}
					<div class="backup-meta__item">
						<span class="field-label">Last Run</span>
						<span class="backup-meta__val">{formatDate(backupStatus.lastRunAt)}</span>
					</div>
					<div class="backup-meta__item">
						<span class="field-label">Result</span>
						<span class="row-msg" class:row-msg--ok={backupStatus.lastRunSuccess} class:row-msg--err={!backupStatus.lastRunSuccess}>
							{backupStatus.lastRunMessage}
						</span>
					</div>
				{/if}
			</div>

			{#if Object.keys(backupsByDb).length > 0}
				<div class="backup-files">
					{#each Object.entries(backupsByDb) as [db, files]}
						<div class="backup-db-group">
							<div class="backup-db-group__title">{db}</div>
							<div class="ip-table-wrap">
								<table class="ip-table">
									<thead>
										<tr>
											<th>File</th>
											<th>Size</th>
											<th>Created</th>
										</tr>
									</thead>
									<tbody>
										{#each files as f (f.fileName)}
											<tr>
												<td class="td-meta" style="font-family: var(--font-mono, monospace); font-size: 0.74rem;">{f.fileName}</td>
												<td class="td-meta">{formatBytes(f.sizeBytes)}</td>
												<td class="td-meta">{formatDate(f.createdAt)}</td>
											</tr>
										{/each}
									</tbody>
								</table>
							</div>
						</div>
					{/each}
				</div>
			{:else}
				<div class="state-msg">No backup files found in the configured path.</div>
			{/if}
		{/if}

		<div class="box-footer">
			{#if backupMsg}
				<span class="row-msg" class:row-msg--ok={backupMsgOk} class:row-msg--err={!backupMsgOk}>{backupMsg}</span>
			{/if}
			<button
				class="save-btn save-btn--active"
				disabled={backupRunning || backupStatus?.isBusy}
				on:click={handleBackupNow}
			>
				{backupRunning ? 'Backing up…' : '▶ Backup Now'}
			</button>
		</div>
	</section>

	<!-- ── Certification IP ───────────────────────────────────────────────── -->
	<section class="control-box">
		<h2 class="box-title">Global Certification IP</h2>
		<p class="box-desc">
			Rewrites the IP address inside every <strong>Certification "ip", port</strong> line across
			all blocks (GlobalManager, GatewayServer, AgentServer, etc.) in <code>server.cfg</code>.
			All modules must share the same IP, so one edit updates them all.
		</p>

		<div class="fields-grid">
			<div class="field">
				<label class="field-label" for="certIp">New IP Address</label>
				<input
					id="certIp"
					class="cfg-input"
					type="text"
					bind:value={certIp}
					placeholder="192.168.0.11"
					spellcheck="false"
				/>
			</div>
		</div>

		<div class="box-footer">
			{#if certMsg}
				<span class="row-msg" class:row-msg--ok={certMsgOk} class:row-msg--err={!certMsgOk}>{certMsg}</span>
			{/if}
			<button
				class="save-btn"
				class:save-btn--active={certIp.trim().length > 0}
				disabled={certSaving || !certIp.trim()}
				on:click={saveCertIp}
			>
				{certSaving ? 'Saving…' : 'Update All IPs'}
			</button>
		</div>
	</section>
</div>

<style>
	.page {
		padding: 2rem;
		max-width: 1100px;
		display: flex;
		flex-direction: column;
		gap: 1.5rem;
	}

	/* ── Control box ─── */
	.control-box {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 1.25rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.box-title {
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--gold-light);
		margin: 0;
	}

	.box-desc {
		font-size: 0.82rem;
		color: var(--text-muted);
		line-height: 1.55;
		margin: 0;
	}
	.box-desc strong { color: var(--gold); }
	.box-desc code {
		font-family: var(--font-mono, monospace);
		font-size: 0.8em;
		color: var(--text-base);
		background: var(--bg-raised);
		padding: 1px 4px;
		border-radius: 3px;
	}

	/* ── State messages ─── */
	.state-msg {
		font-size: 0.85rem;
		color: var(--text-dim);
		padding: 1rem 0;
		text-align: center;
	}
	.state-msg--err { color: var(--red-bright); }

	/* ── Table ─── */
	.ip-table-wrap {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
		overflow-x: auto;
	}

	.ip-table {
		width: 100%;
		border-collapse: collapse;
		font-size: 0.82rem;
	}

	.ip-table thead tr {
		background: var(--bg-raised);
		border-bottom: 1px solid var(--border-dark);
	}

	.ip-table th {
		padding: 0.5rem 0.85rem;
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.09em;
		text-transform: uppercase;
		color: var(--gold-light);
		text-align: left;
		white-space: nowrap;
	}
	.th-center { text-align: center; }

	.ip-table td {
		padding: 0.5rem 0.85rem;
		border-bottom: 1px solid var(--border-dark);
		color: var(--text-base);
		vertical-align: middle;
	}
	.ip-table tbody tr:last-child td { border-bottom: none; }
	.ip-table tbody tr:hover td { background: var(--bg-raised); }
	.row--dirty td { background: rgba(139, 94, 28, 0.07) !important; }

	.td-idx {
		font-family: var(--font-heading);
		font-size: 0.7rem;
		color: var(--text-dim);
		width: 48px;
	}
	.td-ip { min-width: 160px; }
	.td-center { text-align: center; }
	.td-meta {
		font-family: var(--font-mono, monospace);
		font-size: 0.77rem;
		color: var(--text-muted);
		white-space: nowrap;
	}
	.td-desc {
		max-width: 200px;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}
	.td-actions { min-width: 120px; }
	.td-actions__row {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		justify-content: flex-end;
	}

	/* ── Fields grid ─── */
	.fields-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
		gap: 0.85rem;
	}

	.field {
		display: flex;
		flex-direction: column;
		gap: 0.35rem;
	}

	.field--full {
		grid-column: 1 / -1;
		flex-direction: row;
		align-items: center;
		gap: 1rem;
	}

	.field-label {
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.09em;
		text-transform: uppercase;
		color: var(--gold-light);
	}

	/* ── Generic config input ─── */
	.cfg-input {
		width: 100%;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.3rem 0.55rem;
		color: var(--text-base);
		font-family: var(--font-mono, monospace);
		font-size: 0.82rem;
		outline: none;
		transition: border-color 0.15s;
		box-sizing: border-box;
	}
	.cfg-input:focus { border-color: var(--border-accent); }
	input[type="number"].cfg-input { -moz-appearance: textfield; }
	input[type="number"].cfg-input::-webkit-outer-spin-button,
	input[type="number"].cfg-input::-webkit-inner-spin-button { -webkit-appearance: none; }

	/* ── Event toggle ─── */
	.event-toggle {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		cursor: pointer;
	}
	.event-toggle input { position: absolute; opacity: 0; width: 0; height: 0; }

	.event-toggle__label {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.07em;
		color: var(--text-dim);
		transition: color 0.15s;
	}
	.event-toggle__label--on { color: var(--gold-light); }

	/* ── Toggle (shared) ─── */
	.toggle {
		position: relative;
		display: inline-flex;
		align-items: center;
		cursor: pointer;
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
	.toggle input:checked + .toggle__track,
	.event-toggle input:checked + .toggle__track {
		background: rgba(139, 94, 28, 0.2);
		border-color: var(--gold-light);
	}
	.toggle input:checked + .toggle__track::after,
	.event-toggle input:checked + .toggle__track::after {
		transform: translateX(15px);
		background: var(--gold-light);
	}

	/* ── Box footer ─── */
	.box-footer {
		display: flex;
		align-items: center;
		justify-content: flex-end;
		gap: 0.75rem;
		padding-top: 0.25rem;
		border-top: 1px solid var(--border-dark);
	}

	/* ── Save button ─── */
	.save-btn {
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.07em;
		text-transform: uppercase;
		padding: 0.3rem 0.85rem;
		border-radius: var(--radius);
		border: 1px solid var(--border-dark);
		background: var(--bg-deep);
		color: var(--text-dim);
		cursor: pointer;
		transition: background 0.12s, color 0.12s, border-color 0.12s;
		white-space: nowrap;
		flex-shrink: 0;
	}
	.save-btn:disabled { opacity: 0.35; cursor: not-allowed; }
	.save-btn--active {
		border-color: var(--border-gold);
		background: var(--gold-dim);
		color: var(--text-bright);
	}
	.save-btn--active:hover:not(:disabled) { background: var(--gold); }

	/* ── Row / field feedback ─── */
	.row-msg {
		font-size: 0.7rem;
		font-family: var(--font-heading);
		white-space: nowrap;
	}
	.row-msg--ok  { color: var(--green-bright); }
	.row-msg--err { color: var(--red-bright); }

	/* ── Warning banner ─── */
	.warn-banner {
		display: flex;
		align-items: flex-start;
		gap: 0.65rem;
		background: rgba(160, 40, 40, 0.12);
		border: 1px solid var(--red-dark, #7a2020);
		border-radius: var(--radius);
		padding: 0.65rem 0.9rem;
		font-size: 0.8rem;
		color: var(--red-light, #e07070);
		line-height: 1.55;
	}
	.warn-banner__icon {
		font-size: 1rem;
		flex-shrink: 0;
		margin-top: 1px;
	}
	.warn-banner strong { color: var(--red-bright, #f08080); }

	/* ── Notice filter ─── */
	.notice-filter {
		display: flex;
		flex-direction: column;
		gap: 0.35rem;
		max-width: 260px;
	}
	.notice-filter__row {
		display: flex;
		gap: 0.5rem;
		align-items: center;
	}
	.cfg-input--narrow { max-width: 90px; }

	/* ── Notice article cell ─── */
	.td-article {
		font-family: var(--font-mono, monospace);
		font-size: 0.77rem;
		color: var(--text-muted);
		max-width: 320px;
		white-space: pre-wrap;
		word-break: break-word;
	}

	/* ── Delete button variant ─── */
	.save-btn--danger {
		border-color: var(--red-dark, #7a2020);
		color: var(--red-light, #e07070);
	}
	.save-btn--danger:hover:not(:disabled) {
		background: rgba(160, 40, 40, 0.2);
		border-color: var(--red-bright, #f08080);
		color: var(--red-bright, #f08080);
	}

	/* ── Add notice form ─── */
	.notice-add-form {
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1rem 1.1rem;
		display: flex;
		flex-direction: column;
		gap: 0.85rem;
	}
	.notice-add-form__title {
		font-family: var(--font-heading);
		font-size: 0.62rem;
		text-transform: uppercase;
		letter-spacing: 0.09em;
		color: var(--text-muted);
		margin: 0;
	}
	.cfg-textarea {
		width: 100%;
		background: var(--bg-deep);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.4rem 0.6rem;
		color: var(--text-base);
		font-family: var(--font-mono, monospace);
		font-size: 0.82rem;
		outline: none;
		resize: vertical;
		box-sizing: border-box;
		transition: border-color 0.15s;
	}
	.cfg-textarea:focus { border-color: var(--border-accent); }

	/* ── Backup section ─── */
	.backup-meta {
		display: flex;
		flex-wrap: wrap;
		gap: 1.25rem 2rem;
		align-items: flex-start;
	}
	.backup-meta__item {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
	}
	.backup-meta__val {
		font-family: var(--font-mono, monospace);
		font-size: 0.8rem;
		color: var(--text-base);
	}
	.backup-badge {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.06em;
		color: var(--text-dim);
	}
	.backup-badge--busy {
		color: var(--gold-light);
	}
	.backup-files {
		display: flex;
		flex-direction: column;
		gap: 0.85rem;
	}
	.backup-db-group__title {
		font-family: var(--font-heading);
		font-size: 0.62rem;
		text-transform: uppercase;
		letter-spacing: 0.09em;
		color: var(--gold-light);
		margin-bottom: 0.35rem;
	}

	.char-hint {
		font-size: 0.58rem;
		color: var(--text-dim);
		font-style: italic;
		text-transform: none;
		letter-spacing: 0;
	}
</style>
