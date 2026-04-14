<script lang="ts">
	import { goto } from '$app/navigation';
	import { accountApi } from '$lib/api/serverApi';
	import { authStore } from '$lib/stores/auth';
	import { onMount } from 'svelte';

	let username = '';
	let password = '';
	let error = '';
	let loading = false;

	onMount(() => {
		if (authStore.isAdmin()) goto('/dashboard');
	});

	async function handleLogin(e: SubmitEvent) {
		e.preventDefault();
		error = '';
		loading = true;

		try {
			const res = await accountApi.adminLogin(username, password);
			authStore.login(res.jwt, {
				jid: res.user.jid,
				username: res.user.username,
				nickname: res.user.nickname,
				email: res.user.email,
				sex: res.user.sex,
				authority: res.user.authority
			});
			goto('/dashboard');
		} catch (err: unknown) {
			error = err instanceof Error ? err.message : 'Login failed.';
		} finally {
			loading = false;
		}
	}
</script>

<div class="login-shell">
	<form class="login-card" on:submit={handleLogin}>
		<h1 class="title">VSRO Control</h1>
		<p class="subtitle">Administrator Access</p>

		{#if error}
			<div class="error-banner">{error}</div>
		{/if}

		<label class="field">
			<span>Username</span>
			<input
				type="text"
				bind:value={username}
				autocomplete="username"
				disabled={loading}
				required
			/>
		</label>

		<label class="field">
			<span>Password</span>
			<input
				type="password"
				bind:value={password}
				autocomplete="current-password"
				disabled={loading}
				required
			/>
		</label>

		<button type="submit" class="btn-login" disabled={loading}>
			{loading ? 'Signing in…' : 'Sign In'}
		</button>
	</form>
</div>

<style>
	.login-shell {
		display: flex;
		align-items: center;
		justify-content: center;
		min-height: 100vh;
		background: var(--bg-deep);
	}

	.login-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-gold);
		border-radius: 4px;
		padding: 2.5rem 2rem;
		width: 100%;
		max-width: 360px;
		display: flex;
		flex-direction: column;
		gap: 1.2rem;
	}

	.title {
		font-family: var(--font-heading);
		font-size: 1.6rem;
		color: var(--gold-light);
		text-align: center;
		margin: 0;
	}

	.subtitle {
		font-family: var(--font-body);
		font-size: 0.9rem;
		color: var(--text-muted);
		text-align: center;
		margin: -0.6rem 0 0.4rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
	}

	.error-banner {
		background: var(--red-dark);
		border: 1px solid var(--red);
		color: var(--text-bright);
		font-size: 0.85rem;
		padding: 0.6rem 0.8rem;
		border-radius: 2px;
	}

	.field {
		display: flex;
		flex-direction: column;
		gap: 0.35rem;
		font-family: var(--font-body);
		font-size: 0.9rem;
		color: var(--text-muted);
	}

	.field input {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: 2px;
		color: var(--text-bright);
		font-family: var(--font-body);
		font-size: 1rem;
		padding: 0.45rem 0.6rem;
		outline: none;
		transition: border-color 0.15s;
	}

	.field input:focus {
		border-color: var(--border-accent);
	}

	.field input:disabled {
		opacity: 0.5;
	}

	.btn-login {
		margin-top: 0.4rem;
		background: var(--gold-dim);
		border: 1px solid var(--border-gold);
		border-radius: 2px;
		color: var(--text-bright);
		cursor: pointer;
		font-family: var(--font-heading);
		font-size: 0.95rem;
		letter-spacing: 0.06em;
		padding: 0.6rem;
		transition: background 0.15s;
	}

	.btn-login:hover:not(:disabled) {
		background: var(--gold);
	}

	.btn-login:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}
</style>
