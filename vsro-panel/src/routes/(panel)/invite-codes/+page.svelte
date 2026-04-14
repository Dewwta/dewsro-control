<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { accountApi, type InviteCode } from '$lib/api/serverApi';

	// ── State ─────────────────────────────────────────────────────────────────
	let codes: InviteCode[] = [];
	let loadError = '';
	let loading = true;

	// Generate form
	let noteInput = '';
	let genBusy = false;
	let genMsg = '';
	let genMsgType: 'success' | 'error' = 'success';
	let lastGenerated = '';

	// Revoke
	let revoking: string | null = null;
	let revokeMsg = '';
	let revokeMsgType: 'success' | 'error' = 'success';

	// ── Load ──────────────────────────────────────────────────────────────────
	onMount(loadCodes);

	async function loadCodes() {
		loading = true;
		loadError = '';
		try {
			codes = await accountApi.listInviteCodes();
		} catch (e: unknown) {
			loadError = e instanceof Error ? e.message : 'Failed to load invite codes.';
		} finally {
			loading = false;
		}
	}

	// ── Generate ──────────────────────────────────────────────────────────────
	async function handleGenerate() {
		genBusy = true;
		genMsg = '';
		lastGenerated = '';
		try {
			const res = await accountApi.generateInviteCode(noteInput.trim() || undefined);
			lastGenerated = res.code;
			genMsg = 'Code generated — copy it and send it to the player.';
			genMsgType = 'success';
			noteInput = '';
			await loadCodes();
		} catch (e: unknown) {
			genMsg = e instanceof Error ? e.message : 'Failed to generate code.';
			genMsgType = 'error';
		} finally {
			genBusy = false;
		}
	}

	// ── Revoke ────────────────────────────────────────────────────────────────
	async function handleRevoke(code: string) {
		revoking = code;
		revokeMsg = '';
		try {
			await accountApi.revokeInviteCode(code);
			revokeMsg = `Code ${code} revoked.`;
			revokeMsgType = 'success';
			await loadCodes();
		} catch (e: unknown) {
			revokeMsg = e instanceof Error ? e.message : 'Failed to revoke code.';
			revokeMsgType = 'error';
		} finally {
			revoking = null;
		}
	}

	// ── Helpers ───────────────────────────────────────────────────────────────
	function fmtDate(iso: string): string {
		return new Date(iso).toLocaleString([], {
			year: 'numeric', month: 'short', day: 'numeric',
			hour: '2-digit', minute: '2-digit'
		});
	}

	let copiedCode = '';
	function copyCode(code: string) {
		navigator.clipboard.writeText(code).then(() => {
			copiedCode = code;
			setTimeout(() => { copiedCode = ''; }, 2000);
		});
	}
</script>

<PageHeader title="Invite Codes" subtitle="Generate and manage one-time signup invite codes" />

<div class="page">

	<!-- ── Generate card ── -->
	<div class="card">
		<div class="card__title">Generate New Code</div>
		<p class="card__desc">
			Each code can only be used once. Share it directly with the person you want to invite.
			Codes are persisted to disk and survive API restarts.
		</p>

		<div class="gen-form">
			<div class="field">
				<label class="field__label" for="note-input">Note <span class="field__hint">(optional — who is this for?)</span></label>
				<input
					id="note-input"
					class="field__input"
					type="text"
					placeholder="e.g. Discord user: Dellta#1234"
					bind:value={noteInput}
					disabled={genBusy}
					on:keydown={e => e.key === 'Enter' && handleGenerate()}
				/>
			</div>
			<SrButton variant="primary" size="md" loading={genBusy} on:click={handleGenerate}>
				Generate Code
			</SrButton>
		</div>

		{#if lastGenerated}
			<div class="generated-box">
				<span class="generated-box__label">New code</span>
				<span class="generated-box__code">{lastGenerated}</span>
				<button
					class="copy-btn"
					class:copy-btn--done={copiedCode === lastGenerated}
					on:click={() => copyCode(lastGenerated)}
				>
					{copiedCode === lastGenerated ? 'Copied!' : 'Copy'}
				</button>
			</div>
		{/if}

		{#if genMsg}
			<p class="inline-msg inline-msg--{genMsgType}">{genMsg}</p>
		{/if}
	</div>

	<!-- ── Active codes card ── -->
	<div class="card">
		<div class="card__title">Active Codes <span class="count-badge">{codes.length}</span></div>

		{#if revokeMsg}
			<p class="inline-msg inline-msg--{revokeMsgType}" style="margin-bottom:0.6rem">{revokeMsg}</p>
		{/if}

		{#if loading}
			<p class="state-text">Loading…</p>
		{:else if loadError}
			<div class="msg msg--error">{loadError}</div>
		{:else if codes.length === 0}
			<p class="state-text">No active invite codes. Generate one above.</p>
		{:else}
			<div class="code-list">
				<div class="code-list__header">
					<span>Code</span>
					<span>Note</span>
					<span>Created</span>
					<span></span>
				</div>
				{#each codes as entry (entry.code)}
					<div class="code-row">
						<div class="code-row__code">
							<span class="code-mono">{entry.code}</span>
							<button
								class="copy-btn copy-btn--sm"
								class:copy-btn--done={copiedCode === entry.code}
								on:click={() => copyCode(entry.code)}
							>
								{copiedCode === entry.code ? '✓' : 'Copy'}
							</button>
						</div>
						<span class="code-row__note">{entry.note || '—'}</span>
						<span class="code-row__date">{fmtDate(entry.createdAt)}</span>
						<div class="code-row__actions">
							<SrButton
								variant="danger"
								size="sm"
								loading={revoking === entry.code}
								disabled={revoking !== null}
								on:click={() => handleRevoke(entry.code)}
							>
								Revoke
							</SrButton>
						</div>
					</div>
				{/each}
			</div>
		{/if}
	</div>

</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1.25rem;
		max-width: 780px;
	}

	/* ── Card ── */
	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-top: 2px solid var(--border-gold);
		border-radius: var(--radius);
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.card__title {
		font-family: var(--font-heading);
		font-size: 0.88rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--gold-light);
		display: flex;
		align-items: center;
		gap: 0.6rem;
	}

	.card__desc {
		font-size: 0.85rem;
		color: var(--text-muted);
		line-height: 1.7;
		margin: 0;
	}

	.count-badge {
		font-family: var(--font-mono);
		font-size: 0.7rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		color: var(--text-dim);
		padding: 1px 7px;
		border-radius: 10px;
		letter-spacing: 0;
	}

	/* ── Generate form ── */
	.gen-form {
		display: flex;
		gap: 0.75rem;
		align-items: flex-end;
		flex-wrap: wrap;
	}

	.field {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
		flex: 1;
		min-width: 220px;
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

	.field__input:focus   { border-color: var(--border-accent); }
	.field__input:disabled { opacity: 0.45; cursor: not-allowed; }

	/* ── Generated code display ── */
	.generated-box {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		padding: 0.65rem 1rem;
		background: rgba(21, 45, 12, 0.25);
		border: 1px solid var(--green);
		border-radius: var(--radius);
		flex-wrap: wrap;
	}

	.generated-box__label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: var(--green-bright);
	}

	.generated-box__code {
		font-family: var(--font-mono);
		font-size: 1rem;
		color: var(--text-bright);
		letter-spacing: 0.15em;
		flex: 1;
	}

	/* ── Copy button ── */
	.copy-btn {
		padding: 0.22rem 0.7rem;
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.07em;
		text-transform: uppercase;
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		background: var(--bg-raised);
		color: var(--text-muted);
		cursor: pointer;
		transition: background 0.12s, color 0.12s, border-color 0.12s;
		white-space: nowrap;
	}

	.copy-btn:hover { background: var(--bg-hover); color: var(--text-base); }
	.copy-btn--done { border-color: var(--green); color: var(--green-bright); background: rgba(21,45,12,0.3); }
	.copy-btn--sm   { font-size: 0.58rem; padding: 0.16rem 0.5rem; }

	/* ── Code list ── */
	.code-list {
		display: flex;
		flex-direction: column;
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
	}

	.code-list__header {
		display: grid;
		grid-template-columns: 160px 1fr 180px 80px;
		gap: 0.5rem;
		padding: 0.4rem 0.85rem;
		background: var(--bg-raised);
		border-bottom: 1px solid var(--border-dark);
		font-family: var(--font-heading);
		font-size: 0.6rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: var(--text-dim);
	}

	.code-row {
		display: grid;
		grid-template-columns: 160px 1fr 180px 80px;
		gap: 0.5rem;
		align-items: center;
		padding: 0.55rem 0.85rem;
		border-bottom: 1px solid var(--border-dark);
		transition: background 0.1s;
	}

	.code-row:last-child { border-bottom: none; }
	.code-row:hover { background: var(--bg-raised); }

	.code-row__code {
		display: flex;
		align-items: center;
		gap: 0.45rem;
	}

	.code-mono {
		font-family: var(--font-mono);
		font-size: 0.82rem;
		color: var(--text-bright);
		letter-spacing: 0.1em;
	}

	.code-row__note {
		font-size: 0.8rem;
		color: var(--text-muted);
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.code-row__date {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		color: var(--text-dim);
	}

	.code-row__actions {
		display: flex;
		justify-content: flex-end;
	}

	/* ── Messages ── */
	.inline-msg {
		font-size: 0.8rem;
		font-family: var(--font-heading);
		letter-spacing: 0.03em;
	}
	.inline-msg--success { color: var(--green-bright); }
	.inline-msg--error   { color: var(--red-light); }

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
