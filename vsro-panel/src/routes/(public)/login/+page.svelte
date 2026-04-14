<script lang="ts">
	import { goto } from '$app/navigation';
	import { accountApi } from '$lib/api/serverApi';
	import { authStore } from '$lib/stores/auth';
	import { onMount } from 'svelte';

	let tab: 'login' | 'register' = 'login';

	// Login fields
	let loginUsername = '';
	let loginPassword = '';

	// Register fields
	let regUsername   = '';
	let regPassword   = '';
	let regConfirm    = '';
	let regEmail      = '';
	let regNickname   = '';
	let regSex        = 'Male';
	let regInviteCode = '';

	let error   = '';
	let success = '';
	let loading = false;

	onMount(() => {
		if ($authStore.user) goto('/');
	});

	async function handleLogin(e: SubmitEvent) {
		e.preventDefault();
		error = ''; loading = true;

		try {
			const res = await accountApi.userLogin(loginUsername, loginPassword);
			authStore.login(res.jwt, {
				jid:       res.user.jid,
				username:  res.user.username,
				nickname:  res.user.nickname,
				email:     res.user.email,
				sex:       res.user.sex,
				authority: res.user.authority
			});
			goto('/');
		} catch (err: unknown) {
			error = err instanceof Error ? err.message : 'Login failed.';
		} finally {
			loading = false;
		}
	}

	async function handleRegister(e: SubmitEvent) {
		e.preventDefault();
		error = ''; success = '';

		if (regPassword !== regConfirm) {
			error = 'Passwords do not match.';
			return;
		}

		loading = true;
		try {
			await accountApi.signUp(regUsername, regPassword, regEmail, regNickname, regSex, regInviteCode);
			success = 'Account created! You can now sign in.';
			tab = 'login';
			loginUsername = regUsername;
		} catch (err: unknown) {
			error = err instanceof Error ? err.message : 'Registration failed.';
		} finally {
			loading = false;
		}
	}
</script>

<div class="login-shell">
	<div class="login-card">
		<!-- Card header -->
		<div class="card-header">
			<h1 class="card-title">
				{tab === 'login' ? 'Sign In' : 'Create Account'}
			</h1>
			<p class="card-sub">
				{tab === 'login' ? 'Welcome back, traveller.' : 'Begin your journey.'}
			</p>
		</div>

		<!-- Tab switcher -->
		<div class="tabs">
			<button
				class="tab"
				class:tab--active={tab === 'login'}
				on:click={() => { tab = 'login'; error = ''; success = ''; }}
			>Sign In</button>
			<button
				class="tab"
				class:tab--active={tab === 'register'}
				on:click={() => { tab = 'register'; error = ''; success = ''; }}
			>Register</button>
		</div>

		<!-- Feedback -->
		{#if error}
			<div class="banner banner--error">{error}</div>
		{/if}
		{#if success}
			<div class="banner banner--success">{success}</div>
		{/if}

		<!-- Login form -->
		{#if tab === 'login'}
			<form class="form" on:submit={handleLogin}>
				<label class="field">
					<span>Username</span>
					<input
						type="text"
						bind:value={loginUsername}
						autocomplete="username"
						disabled={loading}
						required
					/>
				</label>
				<label class="field">
					<span>Password</span>
					<input
						type="password"
						bind:value={loginPassword}
						autocomplete="current-password"
						disabled={loading}
						required
					/>
				</label>
				<button type="submit" class="btn-submit" disabled={loading}>
					{loading ? 'Signing in…' : 'Sign In'}
				</button>
			</form>

		<!-- Register form -->
		{:else}
			<form class="form" on:submit={handleRegister}>
				<label class="field">
					<span>Username</span>
					<input type="text"     bind:value={regUsername} autocomplete="username"     disabled={loading} required />
				</label>
				<label class="field">
					<span>Nickname (in-game name)</span>
					<input type="text"     bind:value={regNickname} autocomplete="off"          disabled={loading} />
				</label>
				<label class="field">
					<span>Email</span>
					<input type="email"    bind:value={regEmail}    autocomplete="email"        disabled={loading} />
				</label>
				<div class="field-row">
					<label class="field">
						<span>Password</span>
						<input type="password" bind:value={regPassword} autocomplete="new-password" disabled={loading} required minlength={8} />
					</label>
					<label class="field">
						<span>Confirm Password</span>
						<input type="password" bind:value={regConfirm}  autocomplete="new-password" disabled={loading} required minlength={8} />
					</label>
				</div>
				<label class="field">
					<span>Character Sex</span>
					<select bind:value={regSex} disabled={loading}>
						<option value="Male">Male</option>
						<option value="Female">Female</option>
					</select>
				</label>
				<label class="field">
					<span>Invite Code</span>
					<input type="text" bind:value={regInviteCode} autocomplete="off" disabled={loading} required placeholder="Enter your invite code" />
				</label>
				<button type="submit" class="btn-submit" disabled={loading}>
					{loading ? 'Creating account…' : 'Create Account'}
				</button>
			</form>
		{/if}
	</div>
</div>

<style>
	.login-shell {
		display: flex;
		justify-content: center;
		align-items: flex-start;
		padding: 3rem 1rem 4rem;
		min-height: calc(100vh - 56px - 60px); /* subtract topbar + footer */
		background:
			radial-gradient(ellipse 60% 50% at 50% 0%, rgba(200,148,60,0.05) 0%, transparent 70%);
	}

	.login-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
		padding: 2rem;
		width: 100%;
		max-width: 440px;
		display: flex;
		flex-direction: column;
		gap: 1.2rem;
	}

	.card-header {
		text-align: center;
	}

	.card-title {
		font-family: var(--font-heading);
		font-size: 1.4rem;
		color: var(--gold-light);
		letter-spacing: 0.1em;
		margin-bottom: 0.25rem;
	}

	.card-sub {
		font-size: 0.85rem;
		color: var(--text-muted);
		font-style: italic;
	}

	/* Tabs */
	.tabs {
		display: grid;
		grid-template-columns: 1fr 1fr;
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
	}

	.tab {
		padding: 0.5rem;
		background: transparent;
		border: none;
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.75rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		cursor: pointer;
		transition: background 0.15s, color 0.15s;
	}

	.tab--active {
		background: var(--bg-surface);
		color: var(--gold);
		border-bottom: 2px solid var(--gold-dim);
	}

	/* Feedback banners */
	.banner {
		padding: 0.6rem 0.8rem;
		border-radius: 2px;
		font-size: 0.85rem;
	}

	.banner--error {
		background: var(--red-dark);
		border: 1px solid var(--red);
		color: var(--text-bright);
	}

	.banner--success {
		background: var(--green-dark);
		border: 1px solid var(--green);
		color: var(--text-bright);
	}

	/* Form */
	.form {
		display: flex;
		flex-direction: column;
		gap: 0.9rem;
	}

	.field {
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
		font-family: var(--font-body);
		font-size: 0.85rem;
		color: var(--text-muted);
	}

	.field-row {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 0.75rem;
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
		width: 100%;
	}

	.field input:focus,
	.field select:focus {
		border-color: var(--border-accent);
	}

	.field input:disabled,
	.field select:disabled {
		opacity: 0.5;
	}

	.btn-submit {
		margin-top: 0.3rem;
		background: var(--gold-dim);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
		color: var(--text-bright);
		cursor: pointer;
		font-family: var(--font-heading);
		font-size: 0.85rem;
		letter-spacing: 0.08em;
		padding: 0.65rem;
		transition: background 0.15s;
	}

	.btn-submit:hover:not(:disabled) {
		background: var(--gold);
	}

	.btn-submit:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}
</style>
