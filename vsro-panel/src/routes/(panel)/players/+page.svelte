<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { playersApi, type CharacterPosition } from '$lib/api/serverApi';

	// ── Online count ──────────────────────────────────────────────────────────
	let onlineCount    = '—';
	let countLoading   = false;

	async function refreshOnlineCount() {
		countLoading = true;
		try {
			const r = await playersApi.getOnlineCount();
			onlineCount = r.count;
		} catch { onlineCount = 'Error'; }
		finally { countLoading = false; }
	}

	// ── Character lookup ──────────────────────────────────────────────────────
	let lookupName     = '';
	let lookupLoading  = false;
	let charPosition: CharacterPosition | null = null;
	let lookupError    = '';

	async function handleLookup() {
		if (!lookupName.trim()) return;
		lookupLoading = true;
		charPosition  = null;
		lookupError   = '';
		try {
			charPosition = await playersApi.getCharacterPosition(lookupName.trim());
		} catch (e: any) {
			lookupError = e.message ?? 'Character not found.';
		} finally { lookupLoading = false; }
	}

	// ── Create account ────────────────────────────────────────────────────────
	let newUsername    = '';
	let newPassword    = '';
	let isAdmin        = false;
	let createLoading  = false;
	let createMsg      = '';
	let createMsgType: 'success' | 'error' = 'success';

	async function handleCreateAccount() {
		if (!newUsername.trim() || !newPassword.trim()) return;
		createLoading = true;
		createMsg     = '';
		try {
			const r = await playersApi.createAccount(newUsername.trim(), newPassword.trim(), isAdmin);
			createMsg     = r.message;
			createMsgType = 'success';
			newUsername   = '';
			newPassword   = '';
			isAdmin       = false;
		} catch (e: any) {
			createMsg     = e.message ?? 'Failed to create account.';
			createMsgType = 'error';
		} finally { createLoading = false; }
	}

	// ── Add silk ──────────────────────────────────────────────────────────────
	let silkUser       = '';
	let silkAmount     = 1000;
	let silkLoading    = false;
	let silkMsg        = '';
	let silkMsgType: 'success' | 'error' = 'success';

	async function handleAddSilk() {
		if (!silkUser.trim() || silkAmount <= 0) return;
		silkLoading = true;
		silkMsg     = '';
		try {
			const r = await playersApi.addSilk(silkUser.trim(), silkAmount);
			silkMsg     = r.message;
			silkMsgType = 'success';
			silkUser    = '';
		} catch (e: any) {
			silkMsg     = e.message ?? 'Failed to add silk.';
			silkMsgType = 'error';
		} finally { silkLoading = false; }
	}

	// ── Delete characters ─────────────────────────────────────────────────────
	let deleteJID: number | null = null;
	let deleteLoading     = false;
	let deleteMsg         = '';
	let deleteMsgType: 'success' | 'error' = 'success';
	let confirmVisible    = false;
	let confirmedJID      = 0;

	function promptDelete() {
        if (deleteJID === null || isNaN(deleteJID) || deleteJID <= 0) return;
        confirmedJID   = deleteJID;
        confirmVisible = true;
    }

	function cancelDelete() {
		confirmVisible = false;
	}

	async function confirmDelete() {
		confirmVisible = false;
		deleteLoading  = true;
		deleteMsg      = '';
		try {
			const r   = await playersApi.truncateCharacters(confirmedJID);
			deleteMsg     = r.message;
			deleteMsgType = 'success';
			deleteJID     = null;
		} catch (e: any) {
			deleteMsg     = e.message ?? 'Failed to delete characters.';
			deleteMsgType = 'error';
		} finally { deleteLoading = false; }
	}

	onMount(refreshOnlineCount);
</script>

<div class="page">
	<PageHeader title="Players" subtitle="Account management, silk, and character tools">
		<svelte:fragment slot="actions">
			<SrButton size="sm" variant="secondary" loading={countLoading} on:click={refreshOnlineCount}>
				Refresh
			</SrButton>
		</svelte:fragment>
	</PageHeader>

	<!-- ── Top row: online count + character lookup ─────────────────────── -->
	<div class="top-row">

		<!-- Online count card -->
		<div class="stat-card">
			<span class="stat-card__label">Players Online</span>
			<span class="stat-card__value">{onlineCount}</span>
			<span class="stat-card__sub">from IP logs table</span>
		</div>

		<!-- Character lookup -->
		<div class="card card--lookup">
			<div class="card__header">
				<h2 class="card__title">Character Lookup</h2>
				<span class="card__sub">Find a character's current position</span>
			</div>
			<div class="card__body">
				<div class="inline-form">
					<input
						class="sr-input"
						type="text"
						placeholder="Character name…"
						bind:value={lookupName}
						on:keydown={e => e.key === 'Enter' && handleLookup()}
					/>
					<SrButton size="sm" loading={lookupLoading} on:click={handleLookup}>
						Search
					</SrButton>
				</div>

				{#if lookupError}
					<p class="field-error">{lookupError}</p>
				{/if}

				{#if charPosition}
					<div class="position-result">
						<div class="pos-row">
							<span class="pos-label">Character</span>
							<span class="pos-value">{charPosition.characterName ?? '—'}</span>
						</div>
						<div class="pos-row">
							<span class="pos-label">Region</span>
							<code class="pos-value">{charPosition.latestRegion}</code>
						</div>
						<div class="pos-row">
							<span class="pos-label">Position</span>
							<code class="pos-value">{charPosition.posX}, {charPosition.posY}, {charPosition.posZ}</code>
						</div>
					</div>
				{/if}
			</div>
		</div>

	</div>

	<!-- ── Bottom row: account creation + silk ──────────────────────────── -->
	<div class="action-row">

		<!-- Create account -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Create Account</h2>
				<span class="card__sub">Add a new player or GM account</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Username</label>
					<input class="sr-input" type="text" placeholder="e.g. silkroad_admin" bind:value={newUsername} />
				</div>
				<div class="field-group">
					<label class="field-label">Password</label>
					<input class="sr-input" type="password" placeholder="Plain text — hashed before storage" bind:value={newPassword} />
				</div>
				<label class="checkbox-row">
					<input type="checkbox" bind:checked={isAdmin} />
					<span class="checkbox-row__label">GM / Admin account <span class="field-hint">(sec_primary = 1)</span></span>
				</label>
			</div>
			<div class="card__footer">
				{#if createMsg}
					<p class="inline-msg inline-msg--{createMsgType}">{createMsg}</p>
				{/if}
				<SrButton
					variant="primary"
					disabled={!newUsername.trim() || !newPassword.trim()}
					loading={createLoading}
					on:click={handleCreateAccount}
				>
					Create Account
				</SrButton>
			</div>
		</div>

		<!-- Add silk -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Add Silk</h2>
				<span class="card__sub">Grant premium currency to a player account</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Account Username</label>
					<input class="sr-input" type="text" placeholder="Target account name" bind:value={silkUser} />
				</div>
				<div class="field-group">
					<label class="field-label">Silk Amount</label>
					<input class="sr-input" type="number" min="1" bind:value={silkAmount} />
				</div>
			</div>
			<div class="card__footer">
				{#if silkMsg}
					<p class="inline-msg inline-msg--{silkMsgType}">{silkMsg}</p>
				{/if}
				<SrButton
					variant="primary"
					disabled={!silkUser.trim() || silkAmount <= 0}
					loading={silkLoading}
					on:click={handleAddSilk}
				>
					Add Silk
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Danger zone: delete characters ───────────────────────────────── -->
	<div class="danger-row">
		<div class="card card--danger">
			<div class="card__header">
				<h2 class="card__title">Delete Characters</h2>
				<span class="card__sub">Permanently removes all characters tied to a JID — this cannot be undone</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Account JID</label>
					<input
						class="sr-input"
						type="number"
						min="1"
						placeholder="e.g. 1042"
						bind:value={deleteJID}
						on:keydown={e => e.key === 'Enter' && promptDelete()}
					/>
				</div>
			</div>
			<div class="card__footer">
				{#if deleteMsg}
					<p class="inline-msg inline-msg--{deleteMsgType}">{deleteMsg}</p>
				{/if}
				<SrButton
					variant="danger"
					disabled={deleteJID === null || deleteJID <= 0 || isNaN(deleteJID)}
					loading={deleteLoading}
					on:click={promptDelete}
				>
					Delete Characters
				</SrButton>
			</div>
		</div>
	</div>

	<!-- ── Confirmation overlay ──────────────────────────────────────────── -->
	{#if confirmVisible}
		<div class="confirm-backdrop" role="dialog" aria-modal="true">
			<div class="confirm-box">
				<h3 class="confirm-title">Are you sure?</h3>
				<p class="confirm-body">
					This will <strong>permanently delete all characters</strong> for JID <code>{confirmedJID}</code>.
					The operation cannot be undone.
				</p>
				<div class="confirm-actions">
					<SrButton variant="secondary" on:click={cancelDelete}>Cancel</SrButton>
					<SrButton variant="danger"    on:click={confirmDelete}>Yes, Delete</SrButton>
				</div>
			</div>
		</div>
	{/if}

</div>

<style>
	.page {
		padding: 2rem;
		max-width: 960px;
		display: flex;
		flex-direction: column;
		gap: 1.25rem;
	}

	/* ── Top row ─── */
	.top-row {
		display: grid;
		grid-template-columns: 180px 1fr;
		gap: 1rem;
		align-items: start;
	}

	@media (max-width: 600px) { .top-row { grid-template-columns: 1fr; } }

	/* ── Stat card ─── */
	.stat-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-top: 3px solid var(--gold);
		border-radius: var(--radius);
		padding: 1rem;
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		min-height: 100px;
		justify-content: center;
	}

	.stat-card__label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.stat-card__value {
		font-family: var(--font-heading);
		font-size: 2.2rem;
		color: var(--text-bright);
		line-height: 1;
	}

	.stat-card__sub {
		font-size: 0.7rem;
		color: var(--text-dim);
		margin-top: 0.1rem;
	}

	/* ── Action row ─── */
	.action-row {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
	}

	@media (max-width: 640px) { .action-row { grid-template-columns: 1fr; } }

	/* ── Cards ─── */
	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		display: flex;
		flex-direction: column;
	}

	.card--lookup { flex: 1; }

	.card__header {
		padding: 0.85rem 1rem 0.6rem;
		border-bottom: 1px solid var(--border-dark);
	}

	.card__title {
		font-family: var(--font-heading);
		font-size: 0.9rem;
		color: var(--text-bright);
		letter-spacing: 0.07em;
		text-transform: uppercase;
		margin-bottom: 0.15rem;
	}

	.card__sub {
		font-size: 0.76rem;
		color: var(--text-muted);
	}

	.card__body {
		flex: 1;
		padding: 0.9rem 1rem;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.card__footer {
		padding: 0.7rem 1rem 0.9rem;
		border-top: 1px solid var(--border-dark);
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	/* ── Inputs ─── */
	.sr-input {
		width: 100%;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.45rem 0.7rem;
		color: var(--text-base);
		font-family: var(--font-body);
		font-size: 0.9rem;
		outline: none;
		transition: border-color 0.15s;
	}

	.sr-input:focus { border-color: var(--border-accent); }

	.field-group {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
	}

	.field-label {
		font-family: var(--font-heading);
		font-size: 0.68rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.field-hint {
		font-family: var(--font-body);
		font-size: 0.75rem;
		color: var(--text-dim);
		text-transform: none;
		letter-spacing: 0;
	}

	.field-error {
		font-size: 0.78rem;
		color: var(--red-bright);
		margin-top: -0.25rem;
	}

	/* ── Checkbox ─── */
	.checkbox-row {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		cursor: pointer;
	}

	.checkbox-row input[type="checkbox"] {
		accent-color: var(--gold);
		width: 14px;
		height: 14px;
		flex-shrink: 0;
	}

	.checkbox-row__label {
		font-size: 0.85rem;
		color: var(--text-base);
	}

	/* ── Inline form (lookup) ─── */
	.inline-form {
		display: flex;
		gap: 0.5rem;
		align-items: center;
	}

	.inline-form .sr-input { flex: 1; }

	/* ── Position result ─── */
	.position-result {
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-left: 3px solid var(--steel-light);
		border-radius: var(--radius);
		padding: 0.65rem 0.8rem;
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
	}

	.pos-row {
		display: flex;
		justify-content: space-between;
		align-items: baseline;
		gap: 0.5rem;
	}

	.pos-label {
		font-size: 0.72rem;
		color: var(--text-muted);
		font-family: var(--font-heading);
		letter-spacing: 0.06em;
		text-transform: uppercase;
		flex-shrink: 0;
	}

	.pos-value {
		font-family: var(--font-mono);
		font-size: 0.8rem;
		color: var(--steel-bright);
		text-align: right;
	}

	/* ── Danger row ─── */
	.danger-row {
		display: grid;
		grid-template-columns: 1fr;
		gap: 1rem;
	}

	.card--danger {
		border-color: var(--red-light);
		border-top: 3px solid var(--red-bright);
	}

	/* ── Confirmation overlay ─── */
	.confirm-backdrop {
		position: fixed;
		inset: 0;
		background: rgba(0, 0, 0, 0.65);
		display: flex;
		align-items: center;
		justify-content: center;
		z-index: 9999;
	}

	.confirm-box {
		background: var(--bg-surface);
		border: 1px solid var(--red-light);
		border-radius: var(--radius);
		padding: 1.5rem;
		max-width: 420px;
		width: 90%;
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.confirm-title {
		font-family: var(--font-heading);
		font-size: 1rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		color: var(--red-bright);
	}

	.confirm-body {
		font-size: 0.88rem;
		color: var(--text-base);
		line-height: 1.5;
	}

	.confirm-body code {
		font-family: var(--font-mono);
		color: var(--text-bright);
	}

	.confirm-actions {
		display: flex;
		justify-content: flex-end;
		gap: 0.6rem;
	}

	/* ── Inline message ─── */
	.inline-msg {
		font-size: 0.8rem;
		padding: 0.4rem 0.6rem;
		border-radius: var(--radius);
		border: 1px solid;
	}

	.inline-msg--success {
		border-color: var(--green-light);
		background: rgba(39, 96, 24, 0.15);
		color: var(--green-bright);
	}

	.inline-msg--error {
		border-color: var(--red-light);
		background: rgba(92, 16, 16, 0.2);
		color: var(--red-bright);
	}
</style>
