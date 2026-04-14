<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { browser } from '$app/environment';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { textdataApi } from '$lib/api/serverApi';

	// ── State ────────────────────────────────────────────────────────────────

	let running        = false;
	let progress       = 0;
	let hasFiles       = false;
	let lastGenerated: Date | null = null;

	let generating     = false;   // local lock while POST is in-flight
	let downloading    = false;
	let message        = '';
	let messageType: 'success' | 'error' | 'info' = 'info';

	let pollTimer: ReturnType<typeof setInterval> | null = null;

	// Stage labels keyed by progress milestone
	const stageLabels: Record<number, string> = {
		 0:  'Initialising…',
		14:  'Quest data…',
		28:  'Teleport data…',
		42:  'Package data…',
		57:  'Optional teleports…',
		71:  'Skill data…',
		85:  'Item data…',
		100: 'Character data…'
	};

	$: stageLabel = (() => {
		const milestones = Object.keys(stageLabels).map(Number).sort((a, b) => b - a);
		const hit = milestones.find(m => progress >= m);
		return hit !== undefined ? stageLabels[hit] : 'Preparing…';
	})();

	// ── Polling ──────────────────────────────────────────────────────────────

	function startPolling() {
		if (pollTimer) return;
		pollTimer = setInterval(async () => {
			try {
				const s = await textdataApi.getStatus();
				running  = s.running;
				progress = s.progress;
				if (!s.running) stopPolling();
			} catch { /* silently ignore mid-generation network blips */ }
		}, 1500);
	}

	function stopPolling() {
		if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
	}

	// ── Initial status check ─────────────────────────────────────────────────

	onMount(async () => {
		if (!browser) return;
		try {
			const s = await textdataApi.getStatus();
			running  = s.running;
			progress = s.progress;
			if (s.running) startPolling();
			// If progress is 100 and not running, files are ready
			if (!s.running && s.progress === 100) hasFiles = true;
		} catch {
			// API unreachable — leave defaults
		}
	});

	onDestroy(stopPolling);

	// ── Actions ───────────────────────────────────────────────────────────────

	async function handleGenerate() {
		if (generating || running) return;
		generating   = true;
		message      = '';
		hasFiles     = false;
		progress     = 0;

		try {
			await textdataApi.generate();
			running        = true;
			lastGenerated  = null;
			messageType    = 'info';
			message        = 'Generation started — this may take a minute or two.';
			startPolling();
		} catch (err: any) {
			messageType = 'error';
			message     = err.message ?? 'Failed to start generation.';
		} finally {
			generating = false;
		}
	}

	async function handleDownload() {
		if (downloading || running) return;
		downloading = true;
		message     = '';

		try {
			await textdataApi.download();
			messageType   = 'success';
			message       = 'Download started.';
		} catch (err: any) {
			messageType = 'error';
			message     = err.message ?? 'Download failed.';
		} finally {
			downloading = false;
		}
	}

	// When generation finishes, mark files as ready
	$: if (!running && progress === 100) {
		hasFiles      = true;
		lastGenerated = new Date();
	}
</script>

<div class="page">
	<PageHeader title="Client Data Export" subtitle="Generate and download encrypted textdata files for the game server client">
		<svelte:fragment slot="actions">
			<span class="status-chip" class:status-chip--running={running} class:status-chip--ready={!running && hasFiles}>
				<span class="status-chip__dot"></span>
				{#if running}
					Generating
				{:else if hasFiles}
					Ready
				{:else}
					Idle
				{/if}
			</span>
		</svelte:fragment>
	</PageHeader>

	<!-- ── Info banner ───────────────────────────────────────────────────── -->
	<div class="info-banner">
		<span class="info-banner__icon">ℹ</span>
		<p class="info-banner__text">
			This tool queries the game database and produces the encrypted <code>.txt</code> files
			expected by the client application. Once generated, download the zip and place the
			contents in your client's <code>textdata</code> folder.
		</p>
	</div>

	<!-- ── Main card ─────────────────────────────────────────────────────── -->
	<div class="card-row">

		<!-- Generate card -->
		<div class="card card--generate" class:card--active={running}>
			<div class="card__header">
				<h2 class="card__title">Generate</h2>
				<span class="card__sub">Queries the live database and writes all textdata files</span>
			</div>

			<div class="card__body">
				{#if running}
					<!-- Progress state -->
					<div class="progress-wrap">
						<div class="progress-bar">
							<div class="progress-bar__fill" style="width: {progress}%"></div>
							<div class="progress-bar__shimmer"></div>
						</div>
						<div class="progress-meta">
							<span class="progress-meta__stage">{stageLabel}</span>
							<span class="progress-meta__pct">{progress}%</span>
						</div>
					</div>
				{:else}
					<p class="card__desc">
						Existing files will be wiped before each run. Only one generation can run at a time.
					</p>
					{#if lastGenerated}
						<p class="card__last">Last generated: {lastGenerated.toLocaleTimeString()}</p>
					{/if}
				{/if}
			</div>

			<div class="card__footer">
				<SrButton
					variant="primary"
					disabled={running || generating}
					loading={generating}
					on:click={handleGenerate}
				>
					{running ? 'Generating…' : 'Generate Now'}
				</SrButton>
			</div>
		</div>

		<!-- Download card -->
		<div class="card card--download" class:card--ready={hasFiles && !running}>
			<div class="card__header">
				<h2 class="card__title">Download</h2>
				<span class="card__sub">Packages all generated files into a zip archive</span>
			</div>

			<div class="card__body">
				{#if hasFiles && !running}
					<div class="file-list-hint">
						<span class="file-list-hint__icon">📦</span>
						<p class="file-list-hint__text">
							Files are ready. Click below to download the zip.
						</p>
					</div>
				{:else if running}
					<p class="card__desc card__desc--muted">
						Waiting for generation to finish…
					</p>
				{:else}
					<p class="card__desc card__desc--muted">
						No files yet. Run generation first.
					</p>
				{/if}
			</div>

			<div class="card__footer">
				<SrButton
					variant={hasFiles && !running ? 'primary' : 'secondary'}
					disabled={!hasFiles || running || downloading}
					loading={downloading}
					on:click={handleDownload}
				>
					Download ZIP
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Message bar ───────────────────────────────────────────────────── -->
	{#if message}
		<div class="msg-bar msg-bar--{messageType}">
			<span class="msg-bar__text">{message}</span>
			<button class="msg-bar__dismiss" on:click={() => (message = '')}>✕</button>
		</div>
	{/if}

	<!-- ── What's inside ─────────────────────────────────────────────────── -->
	<div class="section-title">Contents of the generated archive</div>
	<div class="file-table">
		{#each [
			['itemdata.txt', 'Index of item data chunk files'],
			['itemdata_N.txt', 'Item definitions (paginated, 5000 items/file)'],
			['skilldata.txt / skilldataenc.txt', 'Skill file indices (plain + encrypted)'],
			['skilldata_NENC.txt', 'Encrypted skill chunk files'],
			['characterdata.txt', 'Character/mob index'],
			['characterdata_N.txt', 'Character data chunks'],
			['questdata.txt', 'Quest definitions'],
			['teleportbuilding / data / link', 'Teleport NPC & link tables'],
			['refpackageitem / refshopgoods / …', 'Shop & package item tables'],
			['magicoption / assign / group / …', 'Magic option tables'],
			['leveldata.txt', 'Level / EXP curves'],
			['+ ~25 more tables', 'Region, fortress, gacha, trade, event, etc.'],
		] as [file, desc]}
			<div class="file-row">
				<code class="file-row__name">{file}</code>
				<span class="file-row__desc">{desc}</span>
			</div>
		{/each}
	</div>
</div>

<style>
	.page {
		padding: 2rem;
		max-width: 960px;
		display: flex;
		flex-direction: column;
		gap: 1.5rem;
	}

	/* ── Info banner ─── */
	.info-banner {
		display: flex;
		align-items: flex-start;
		gap: 0.8rem;
		background: var(--steel-dark);
		border: 1px solid var(--steel);
		border-left: 3px solid var(--steel-bright);
		border-radius: var(--radius);
		padding: 0.8rem 1rem;
	}

	.info-banner__icon {
		color: var(--steel-bright);
		font-size: 1rem;
		flex-shrink: 0;
		margin-top: 1px;
	}

	.info-banner__text {
		font-size: 0.88rem;
		color: var(--text-muted);
		line-height: 1.55;
	}

	.info-banner__text code {
		font-family: var(--font-mono);
		font-size: 0.82rem;
		color: var(--steel-bright);
		background: rgba(74, 154, 192, 0.1);
		padding: 1px 4px;
		border-radius: 2px;
	}

	/* ── Status chip (header actions) ─── */
	.status-chip {
		display: inline-flex;
		align-items: center;
		gap: 0.4rem;
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		padding: 0.25rem 0.65rem;
		border-radius: 2px;
		border: 1px solid var(--border-mid);
		color: var(--text-muted);
		background: var(--bg-surface);
	}

	.status-chip--running {
		border-color: var(--gold);
		color: var(--gold-light);
		background: rgba(200, 148, 60, 0.08);
	}

	.status-chip--ready {
		border-color: var(--green-light);
		color: var(--green-bright);
		background: rgba(61, 138, 40, 0.1);
	}

	.status-chip__dot {
		width: 6px;
		height: 6px;
		border-radius: 50%;
		background: currentColor;
		flex-shrink: 0;
	}

	.status-chip--running .status-chip__dot {
		animation: pulse 1.1s ease-in-out infinite;
	}

	@keyframes pulse {
		0%, 100% { opacity: 1; }
		50%       { opacity: 0.3; }
	}

	/* ── Card row ─── */
	.card-row {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
	}

	@media (max-width: 640px) {
		.card-row { grid-template-columns: 1fr; }
	}

	/* ── Cards ─── */
	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		display: flex;
		flex-direction: column;
		transition: border-color 0.2s;
	}

	.card--generate.card--active {
		border-color: var(--gold-dim);
	}

	.card--download.card--ready {
		border-color: var(--steel-light);
		box-shadow: 0 0 18px rgba(30, 77, 107, 0.35);
	}

	.card__header {
		padding: 1rem 1.1rem 0.6rem;
		border-bottom: 1px solid var(--border-dark);
	}

	.card__title {
		font-family: var(--font-heading);
		font-size: 0.95rem;
		color: var(--text-bright);
		letter-spacing: 0.07em;
		text-transform: uppercase;
		margin-bottom: 0.2rem;
	}

	.card--download.card--ready .card__title {
		color: var(--steel-bright);
	}

	.card__sub {
		font-size: 0.78rem;
		color: var(--text-muted);
	}

	.card__body {
		flex: 1;
		padding: 1rem 1.1rem;
	}

	.card__desc {
		font-size: 0.85rem;
		color: var(--text-muted);
		line-height: 1.5;
	}

	.card__desc--muted {
		opacity: 0.5;
		font-style: italic;
	}

	.card__last {
		margin-top: 0.5rem;
		font-size: 0.75rem;
		color: var(--text-dim);
		font-family: var(--font-mono);
	}

	.card__footer {
		padding: 0.75rem 1.1rem 1rem;
		border-top: 1px solid var(--border-dark);
	}

	/* ── Progress bar ─── */
	.progress-wrap {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.progress-bar {
		position: relative;
		height: 8px;
		background: var(--bg-raised);
		border-radius: 4px;
		overflow: hidden;
	}

	.progress-bar__fill {
		position: absolute;
		inset: 0 auto 0 0;
		background: linear-gradient(90deg, var(--gold-dim), var(--gold-light));
		border-radius: 4px;
		transition: width 0.6s ease;
	}

	.progress-bar__shimmer {
		position: absolute;
		inset: 0;
		background: linear-gradient(
			90deg,
			transparent 0%,
			rgba(255,255,255,0.08) 50%,
			transparent 100%
		);
		background-size: 200% 100%;
		animation: shimmer 1.6s linear infinite;
	}

	@keyframes shimmer {
		from { background-position: -200% 0; }
		to   { background-position:  200% 0; }
	}

	.progress-meta {
		display: flex;
		justify-content: space-between;
		align-items: center;
	}

	.progress-meta__stage {
		font-size: 0.78rem;
		color: var(--gold);
		font-family: var(--font-heading);
		letter-spacing: 0.05em;
	}

	.progress-meta__pct {
		font-size: 0.78rem;
		color: var(--text-muted);
		font-family: var(--font-mono);
	}

	/* ── Download ready hint ─── */
	.file-list-hint {
		display: flex;
		align-items: center;
		gap: 0.7rem;
	}

	.file-list-hint__icon {
		font-size: 1.6rem;
		line-height: 1;
		flex-shrink: 0;
	}

	.file-list-hint__text {
		font-size: 0.85rem;
		color: var(--text-base);
		line-height: 1.45;
	}

	/* ── Message bar ─── */
	.msg-bar {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 0.8rem;
		padding: 0.65rem 1rem;
		border-radius: var(--radius);
		border: 1px solid;
		font-size: 0.85rem;
	}

	.msg-bar--info {
		border-color: var(--steel);
		background: rgba(30, 77, 107, 0.2);
		color: var(--steel-bright);
	}

	.msg-bar--success {
		border-color: var(--green-light);
		background: rgba(39, 96, 24, 0.2);
		color: var(--green-bright);
	}

	.msg-bar--error {
		border-color: var(--red-light);
		background: rgba(92, 16, 16, 0.3);
		color: var(--red-bright);
	}

	.msg-bar__text { flex: 1; }

	.msg-bar__dismiss {
		background: none;
		border: none;
		cursor: pointer;
		color: currentColor;
		font-size: 0.75rem;
		opacity: 0.6;
		padding: 0;
		flex-shrink: 0;
	}

	.msg-bar__dismiss:hover { opacity: 1; }

	/* ── File table ─── */
	.section-title {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.12em;
		text-transform: uppercase;
		color: var(--text-dim);
		border-bottom: 1px solid var(--border-dark);
		padding-bottom: 0.4rem;
	}

	.file-table {
		display: flex;
		flex-direction: column;
		gap: 1px;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
	}

	.file-row {
		display: flex;
		align-items: baseline;
		gap: 1rem;
		padding: 0.45rem 0.8rem;
		background: var(--bg-surface);
		transition: background 0.12s;
	}

	.file-row:nth-child(even) { background: var(--bg-raised); }
	.file-row:hover           { background: var(--bg-hover); }

	.file-row__name {
		font-family: var(--font-mono);
		font-size: 0.75rem;
		color: var(--steel-bright);
		min-width: 220px;
		flex-shrink: 0;
	}

	.file-row__desc {
		font-size: 0.78rem;
		color: var(--text-muted);
	}
</style>
