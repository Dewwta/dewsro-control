<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { accountApi } from '$lib/api/serverApi';
	import { authStore } from '$lib/stores/auth';

	// ── State ─────────────────────────────────────────────────
	let loading    = true;
	let saveMsg    = '';
	let saveError  = '';
	let pwMsg      = '';
	let pwError    = '';
	let savingInfo = false;
	let savingPw   = false;

	// Profile fields
	let nickname = '';
	let email    = '';
	let sex: 'M' | 'F' = 'M';

	// TB_User.sex is nchar(1) — normalise whatever was stored previously
	function normalizeSex(val: string | null | undefined): 'M' | 'F' {
		if (!val) return 'M';
		return val.trim().toUpperCase().startsWith('F') ? 'F' : 'M';
	}

	// Password fields
	let currentPassword = '';
	let newPassword     = '';
	let confirmPassword = '';

	onMount(async () => {
		if (!$authStore.user) {
			goto('/login');
			return;
		}
		try {
			const me = await accountApi.getMe();
			nickname = me.nickname ?? '';
			email    = me.email    ?? '';
			sex      = normalizeSex(me.sex);
		} finally {
			loading = false;
		}
	});

	async function saveProfile(e: SubmitEvent) {
		e.preventDefault();
		saveMsg = ''; saveError = ''; savingInfo = true;
		try {
			await accountApi.updateProfile(
				nickname || null,
				email    || null,
				sex
			);
			// Update the local store so the navbar reflects the new nickname immediately
			if ($authStore.user) {
				authStore.login($authStore.token!, {
					...$authStore.user,
					nickname: nickname || null,
					email:    email    || null,
					sex
				});
			}
			saveMsg = 'Profile saved.';
		} catch (err: unknown) {
			saveError = err instanceof Error ? err.message : 'Save failed.';
		} finally {
			savingInfo = false;
		}
	}

	async function changePassword(e: SubmitEvent) {
		e.preventDefault();
		pwMsg = ''; pwError = '';

		if (newPassword !== confirmPassword) {
			pwError = 'New passwords do not match.';
			return;
		}

		savingPw = true;
		try {
			await accountApi.changePassword(currentPassword, newPassword);
			pwMsg           = 'Password changed successfully.';
			currentPassword = '';
			newPassword     = '';
			confirmPassword = '';
		} catch (err: unknown) {
			pwError = err instanceof Error ? err.message : 'Password change failed.';
		} finally {
			savingPw = false;
		}
	}
</script>

<div class="page-shell">
	<!-- Page header -->
	<div class="page-hero">
		<div class="container">
			<h1 class="page-title">My Account</h1>
			<p class="page-sub">Manage your profile and security settings</p>
		</div>
	</div>

	<div class="container page-body">
		{#if loading}
			<p class="loading-msg">Loading…</p>
		{:else}
			<!-- ── Profile info ──────────────────────────────── -->
			<section class="card">
				<h2 class="card-title">Profile Information</h2>

				{#if saveMsg}  <div class="banner banner--success">{saveMsg}</div>  {/if}
				{#if saveError}<div class="banner banner--error">{saveError}</div>{/if}

				<form class="form" on:submit={saveProfile}>
					<div class="field-row">
						<label class="field">
							<span>Nickname</span>
							<input type="text" bind:value={nickname} disabled={savingInfo} maxlength={32} />
						</label>
						<label class="field">
							<span>Email</span>
							<input type="email" bind:value={email} disabled={savingInfo} maxlength={100} />
						</label>
					</div>

					<label class="field field--sm">
						<span>Character Sex</span>
						<select bind:value={sex} disabled={savingInfo}>
							<option value="M">Male</option>
							<option value="F">Female</option>
						</select>
					</label>

					<div class="form-footer">
						<button type="submit" class="btn btn--primary" disabled={savingInfo}>
							{savingInfo ? 'Saving…' : 'Save Changes'}
						</button>
					</div>
				</form>
			</section>

			<!-- ── Change password ──────────────────────────── -->
			<section class="card">
				<h2 class="card-title">Change Password</h2>

				{#if pwMsg}  <div class="banner banner--success">{pwMsg}</div>  {/if}
				{#if pwError}<div class="banner banner--error">{pwError}</div>{/if}

				<form class="form" on:submit={changePassword}>
					<label class="field">
						<span>Current Password</span>
						<input
							type="password"
							bind:value={currentPassword}
							autocomplete="current-password"
							disabled={savingPw}
							required
						/>
					</label>

					<div class="field-row">
						<label class="field">
							<span>New Password</span>
							<input
								type="password"
								bind:value={newPassword}
								autocomplete="new-password"
								disabled={savingPw}
								required
								minlength={8}
							/>
						</label>
						<label class="field">
							<span>Confirm New Password</span>
							<input
								type="password"
								bind:value={confirmPassword}
								autocomplete="new-password"
								disabled={savingPw}
								required
								minlength={8}
							/>
						</label>
					</div>

					<div class="form-footer">
						<button type="submit" class="btn btn--primary" disabled={savingPw}>
							{savingPw ? 'Changing…' : 'Change Password'}
						</button>
					</div>
				</form>
			</section>

			<!-- ── Account info (read-only) ──────────────────── -->
			<section class="card card--flat">
				<h2 class="card-title">Account Details</h2>
				<div class="info-grid">
					<span class="info-label">Username</span>
					<span class="info-value">{$authStore.user?.username ?? '—'}</span>
					<span class="info-label">Account ID</span>
					<span class="info-value mono">{$authStore.user?.jid ?? '—'}</span>
				</div>
			</section>
		{/if}
	</div>
</div>

<style>
	.container {
		max-width: 780px;
		margin: 0 auto;
		padding: 0 2rem;
	}

	/* ── Page header ─────────────────────────────────────────── */
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

	.page-body {
		padding-top: 2rem;
		padding-bottom: 3rem;
		display: flex;
		flex-direction: column;
		gap: 1.5rem;
	}

	.loading-msg {
		color: var(--text-muted);
		font-style: italic;
		padding: 2rem 0;
	}

	/* ── Cards ───────────────────────────────────────────────── */
	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.5rem 1.75rem;
		display: flex;
		flex-direction: column;
		gap: 1.1rem;
	}

	.card--flat {
		background: var(--bg-raised);
	}

	.card-title {
		font-family: var(--font-heading);
		font-size: 0.9rem;
		color: var(--gold-light);
		letter-spacing: 0.1em;
		padding-bottom: 0.6rem;
		border-bottom: 1px solid var(--border-dark);
	}

	/* ── Banners ─────────────────────────────────────────────── */
	.banner {
		padding: 0.6rem 0.8rem;
		border-radius: 2px;
		font-size: 0.85rem;
	}

	.banner--success {
		background: var(--green-dark);
		border: 1px solid var(--green);
		color: var(--text-bright);
	}

	.banner--error {
		background: var(--red-dark);
		border: 1px solid var(--red);
		color: var(--text-bright);
	}

	/* ── Form ────────────────────────────────────────────────── */
	.form {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.field {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
		font-family: var(--font-body);
		font-size: 0.85rem;
		color: var(--text-muted);
	}

	.field--sm { max-width: 200px; }

	.field-row {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
	}

	.field input,
	.field select {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: 2px;
		color: var(--text-bright);
		font-family: var(--font-body);
		font-size: 0.95rem;
		padding: 0.45rem 0.6rem;
		outline: none;
		transition: border-color 0.15s;
	}

	.field input:focus,
	.field select:focus {
		border-color: var(--border-accent);
	}

	.field input:disabled,
	.field select:disabled {
		opacity: 0.5;
	}

	.form-footer {
		display: flex;
		justify-content: flex-end;
		padding-top: 0.3rem;
	}

	.btn {
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.08em;
		padding: 0.55rem 1.3rem;
		border-radius: var(--radius);
		border: 1px solid transparent;
		cursor: pointer;
		transition: background 0.15s;
	}

	.btn--primary {
		background: var(--gold-dim);
		border-color: var(--border-gold);
		color: var(--text-bright);
	}

	.btn--primary:hover:not(:disabled) { background: var(--gold); }
	.btn:disabled { opacity: 0.5; cursor: not-allowed; }

	/* ── Read-only info grid ─────────────────────────────────── */
	.info-grid {
		display: grid;
		grid-template-columns: 140px 1fr;
		gap: 0.4rem 1rem;
		font-size: 0.88rem;
	}

	.info-label {
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		align-self: center;
	}

	.info-value {
		color: var(--text-bright);
	}

	.mono { font-family: var(--font-mono); }

	@media (max-width: 580px) {
		.field-row { grid-template-columns: 1fr; }
		.field--sm { max-width: 100%; }
	}
</style>
