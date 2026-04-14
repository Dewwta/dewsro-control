<script lang="ts">
	import { onMount } from 'svelte';
	import { clientApi, type ClientEntry } from '$lib/api/serverApi';
	import { authStore } from '$lib/stores/auth';

	let client: ClientEntry | null = null;
	let clientError = '';
	let downloadBusy = false;
	let downloadError = '';

	$: isLoggedIn = !!$authStore.user;

	onMount(async () => {
		try {
			client = await clientApi.getLatest();
		} catch {
			clientError = 'no-client';
		}
	});

	async function handleDownload() {
		if (!client) return;
		downloadBusy = true;
		downloadError = '';
		try {
			await clientApi.download(client.fileName);
		} catch (e: unknown) {
			downloadError = e instanceof Error ? e.message : 'Download failed.';
		} finally {
			downloadBusy = false;
		}
	}

	function fmtSize(bytes: number): string {
		if (bytes >= 1_073_741_824) return (bytes / 1_073_741_824).toFixed(2) + ' GB';
		if (bytes >= 1_048_576)     return (bytes / 1_048_576).toFixed(1)     + ' MB';
		return (bytes / 1024).toFixed(0) + ' KB';
	}

	const minReqs = [
		{ label: 'OS',        value: 'Windows XP / Vista / 7 / 10 / 11' },
		{ label: 'CPU',       value: 'Intel Pentium 4 1.8 GHz or equivalent' },
		{ label: 'RAM',       value: '512 MB' },
		{ label: 'GPU',       value: 'DirectX 8.1 compatible, 64 MB VRAM' },
		{ label: 'Storage',   value: '4 GB free space' },
		{ label: 'Network',   value: 'Broadband Internet connection' },
	];

	const recReqs = [
		{ label: 'OS',        value: 'Windows 10 / 11 (64-bit)' },
		{ label: 'CPU',       value: 'Intel Core i3 2.0 GHz or equivalent' },
		{ label: 'RAM',       value: '2 GB' },
		{ label: 'GPU',       value: 'DirectX 9 compatible, 256 MB VRAM' },
		{ label: 'Storage',   value: '6 GB free space (SSD recommended)' },
		{ label: 'Network',   value: 'Broadband Internet connection' },
	];

	const installSteps = [
		'Download the full client installer below.',
		'Run the installer and follow the on-screen instructions.',
		'Launch the game via the desktop shortcut or Start menu.',
		'Log in with your VSRO account credentials.',
	];
</script>

<div class="page-shell">
	<!-- ── Page header ──────────────────────────────────────── -->
	<div class="page-hero">
		<div class="container">
			<h1 class="page-title">Download</h1>
			<p class="page-sub">Get the client and start playing</p>
		</div>
	</div>

	<div class="container page-body">

		<!-- ── Download card ────────────────────────────────── -->
		{#if clientError === 'no-client'}
			<section class="download-card download-card--unavailable">
				<div class="download-card__info">
					<h2 class="download-card__title">Client Unavailable</h2>
					<p class="download-card__desc">No client has been uploaded yet. Check back soon.</p>
				</div>
			</section>
		{:else if client}
			<section class="download-card">
				<div class="download-card__info">
					<h2 class="download-card__title">VSRO Full Client</h2>
					<p class="download-card__meta">
						Version {client.version}
						&nbsp;·&nbsp;
						{fmtSize(client.sizeBytes)}
						&nbsp;·&nbsp;
						Includes all patches
					</p>
					<p class="download-card__desc">
						Full standalone installation. No additional patches required after install.
						Compatible with Windows 7 through 11.
					</p>
					{#if downloadError}
						<p class="download-error">{downloadError}</p>
					{/if}
				</div>
				{#if isLoggedIn}
					<button class="btn-download" disabled={downloadBusy} on:click={handleDownload}>
						<span class="btn-download__icon">⬇</span>
						{downloadBusy ? 'Preparing…' : 'Download Client'}
					</button>
				{:else}
					<a href="/login" class="btn-download btn-download--login">
						<span class="btn-download__icon">🔒</span>
						Sign In to Download
					</a>
				{/if}
			</section>
		{:else}
			<section class="download-card">
				<div class="download-card__info">
					<h2 class="download-card__title">VSRO Full Client</h2>
					<p class="download-card__meta">Loading…</p>
				</div>
			</section>
		{/if}

		<!-- ── Installation steps ───────────────────────────── -->
		<section class="section">
			<h2 class="section-title">Installation</h2>
			<ol class="steps">
				{#each installSteps as step, i}
					<li class="step">
						<span class="step-num">{i + 1}</span>
						<span class="step-text">{step}</span>
					</li>
				{/each}
			</ol>
		</section>

		<!-- ── System requirements ──────────────────────────── -->
		<section class="section">
			<h2 class="section-title">System Requirements</h2>
			<div class="req-grid">
				<!-- Minimum -->
				<div class="req-panel">
					<h3 class="req-panel__heading">Minimum</h3>
					<table class="req-table">
						{#each minReqs as row}
							<tr>
								<td class="req-label">{row.label}</td>
								<td class="req-value">{row.value}</td>
							</tr>
						{/each}
					</table>
				</div>

				<!-- Recommended -->
				<div class="req-panel req-panel--accent">
					<h3 class="req-panel__heading">Recommended</h3>
					<table class="req-table">
						{#each recReqs as row}
							<tr>
								<td class="req-label">{row.label}</td>
								<td class="req-value">{row.value}</td>
							</tr>
						{/each}
					</table>
				</div>
			</div>
		</section>

		<!-- ── Troubleshooting note ──────────────────────────── -->
		<section class="section">
			<div class="notice">
				<span class="notice__icon">ℹ</span>
				<p>
					Having trouble connecting? Make sure your firewall allows the game client,
					and that you are using the correct server IP. Visit the
					<a href="/contact">contact page</a> if you need further assistance.
				</p>
			</div>
		</section>

	</div>
</div>

<style>
	.container {
		max-width: 900px;
		margin: 0 auto;
		padding: 0 2rem;
	}

	.download-card--unavailable {
		border-color: var(--border-mid);
		opacity: 0.7;
	}

	/* ── Page hero banner ────────────────────────────────────── */
	.page-hero {
		background: var(--bg-deep);
		border-bottom: 1px solid var(--border-gold);
		padding: 2.5rem 0 2rem;
	}

	.page-title {
		font-family: var(--font-heading);
		font-size: 2rem;
		color: var(--gold-light);
		letter-spacing: 0.1em;
		margin-bottom: 0.3rem;
	}

	.page-sub {
		font-size: 0.9rem;
		color: var(--text-muted);
		letter-spacing: 0.1em;
		text-transform: uppercase;
	}

	/* ── Page body ───────────────────────────────────────────── */
	.page-body {
		padding-top: 2.5rem;
		padding-bottom: 3rem;
		display: flex;
		flex-direction: column;
		gap: 2.5rem;
	}

	/* ── Download card ───────────────────────────────────────── */
	.download-card {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 1.5rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
		padding: 1.5rem 2rem;
		flex-wrap: wrap;
	}

	.download-card__title {
		font-family: var(--font-heading);
		font-size: 1.1rem;
		color: var(--gold-light);
		letter-spacing: 0.06em;
		margin-bottom: 0.3rem;
	}

	.download-card__meta {
		font-family: var(--font-mono);
		font-size: 0.78rem;
		color: var(--text-muted);
		margin-bottom: 0.5rem;
	}

	.download-card__desc {
		font-size: 0.88rem;
		color: var(--text-muted);
		line-height: 1.6;
		max-width: 480px;
	}

	.btn-download {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		background: var(--gold-dim);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
		color: var(--text-bright);
		font-family: var(--font-heading);
		font-size: 0.85rem;
		letter-spacing: 0.08em;
		padding: 0.7rem 1.4rem;
		text-decoration: none;
		white-space: nowrap;
		transition: background 0.15s;
		flex-shrink: 0;
	}

	.btn-download:hover:not(:disabled) {
		background: var(--gold);
		color: var(--text-bright);
	}

	.btn-download:disabled {
		opacity: 0.6;
		cursor: not-allowed;
	}

	.btn-download--login {
		background: var(--bg-raised);
		border-color: var(--border-mid);
		color: var(--text-muted);
	}

	.btn-download--login:hover {
		background: var(--bg-hover);
		color: var(--text-base);
	}

	.btn-download__icon {
		font-size: 1rem;
	}

	.download-error {
		font-size: 0.82rem;
		color: var(--red-light);
		margin-top: 0.4rem;
	}

	/* ── Sections ────────────────────────────────────────────── */
	.section-title {
		font-family: var(--font-heading);
		font-size: 0.95rem;
		color: var(--gold-light);
		letter-spacing: 0.1em;
		padding-bottom: 0.6rem;
		border-bottom: 1px solid var(--border-gold);
		margin-bottom: 1.1rem;
	}

	/* ── Installation steps ──────────────────────────────────── */
	.steps {
		list-style: none;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.step {
		display: flex;
		align-items: flex-start;
		gap: 0.9rem;
	}

	.step-num {
		flex-shrink: 0;
		width: 1.6rem;
		height: 1.6rem;
		display: flex;
		align-items: center;
		justify-content: center;
		background: var(--bg-raised);
		border: 1px solid var(--border-gold);
		border-radius: 2px;
		font-family: var(--font-heading);
		font-size: 0.75rem;
		color: var(--gold);
	}

	.step-text {
		font-size: 0.9rem;
		color: var(--text-base);
		padding-top: 0.2rem;
		line-height: 1.5;
	}

	/* ── Requirements grid ───────────────────────────────────── */
	.req-grid {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
	}

	.req-panel {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.1rem 1.2rem;
	}

	.req-panel--accent {
		border-color: var(--border-gold);
	}

	.req-panel__heading {
		font-family: var(--font-heading);
		font-size: 0.8rem;
		letter-spacing: 0.1em;
		color: var(--gold-light);
		margin-bottom: 0.8rem;
		text-transform: uppercase;
	}

	.req-table {
		width: 100%;
		border-collapse: collapse;
		font-size: 0.84rem;
	}

	.req-table tr + tr td {
		border-top: 1px solid var(--border-dark);
	}

	.req-label {
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		padding: 0.4rem 0.6rem 0.4rem 0;
		white-space: nowrap;
		width: 90px;
	}

	.req-value {
		color: var(--text-base);
		padding: 0.4rem 0;
		line-height: 1.4;
	}

	/* ── Notice ──────────────────────────────────────────────── */
	.notice {
		display: flex;
		gap: 0.8rem;
		align-items: flex-start;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-left: 3px solid var(--steel-light);
		border-radius: var(--radius);
		padding: 0.9rem 1.1rem;
		font-size: 0.88rem;
		color: var(--text-muted);
		line-height: 1.6;
	}

	.notice__icon {
		color: var(--steel-bright);
		font-size: 1rem;
		flex-shrink: 0;
		margin-top: 0.05rem;
	}

	/* ── Responsive ──────────────────────────────────────────── */
	@media (max-width: 600px) {
		.req-grid {
			grid-template-columns: 1fr;
		}

		.download-card {
			flex-direction: column;
			align-items: flex-start;
		}
	}
</style>
