<script lang="ts">
	import { authStore } from '$lib/stores/auth';
	import { accountApi } from '$lib/api/serverApi';
	import { goto } from '$app/navigation';
	import { page } from '$app/stores';
	import { onMount } from 'svelte';

	const navLinks = [
		{ href: '/',         label: 'Home'     },
		{ href: '/news',     label: 'News'     },
		{ href: '/download', label: 'Download' },
		{ href: '/about',    label: 'About'    },
		{ href: '/guide',    label: 'Guide'    },
	];

	let silk: number | null = null;

	async function loadSilk() {
		if (!$authStore.user) { silk = null; return; }
		try {
			const res = await accountApi.getSilk();
			silk = res.silk;
		} catch {
			silk = null;
		}
	}

	// Reload silk when the user changes (login/logout)
	$: if ($authStore.user) {
		loadSilk();
	} else {
		silk = null;
	}

	onMount(loadSilk);

	function logout() {
		authStore.logout();
		goto('/');
	}
</script>

<div class="site-shell">
	<!-- ── Top navigation bar ─────────────────────────────────── -->
	<header class="topbar">
		<a href="/" class="topbar__brand">
			<span class="brand-icon">⚔</span>
			<span class="brand-name">VSRO</span>
		</a>

		<nav class="topbar__nav">
			{#each navLinks as link}
				<a
					href={link.href}
					class="nav-link"
					class:nav-link--active={$page.url.pathname === link.href}
				>
					{link.label}
				</a>
			{/each}
		</nav>

		<div class="topbar__auth">
			{#if $authStore.user}
				{#if $authStore.user.authority !== 3}
					<a href="/dashboard" class="auth-btn auth-btn--panel">Panel</a>
				{/if}
				{#if silk !== null}
					<div class="silk-badge" title="Silk balance">
						<img src="/silk.png" alt="silk" class="silk-icon" />
						<span class="silk-value">{silk.toLocaleString()}</span>
					</div>
				{/if}
				<a href="/account" class="auth-greeting">
					<span class="auth-icon">◈</span>
					{$authStore.user.nickname ?? $authStore.user.username}
				</a>
				<button class="auth-btn auth-btn--ghost" on:click={logout}>Sign Out</button>
			{:else}
				<a href="/login" class="auth-btn">Sign In</a>
			{/if}
		</div>
	</header>

	<!-- ── Page content ───────────────────────────────────────── -->
	<main class="site-content">
		<slot />
	</main>

	<!-- ── Footer ─────────────────────────────────────────────── -->
	<footer class="site-footer">
		<div class="footer-inner">
			<span class="footer-copy">© {new Date().getFullYear()} VSRO. All rights reserved.</span>
			<div class="footer-links">
				<a href="/about">About</a>
				<a href="/admin" class="footer-admin-link">Admin</a>
			</div>
		</div>
	</footer>
</div>

<style>
	/* ── Shell ───────────────────────────────────────────────── */
	.site-shell {
		display: flex;
		flex-direction: column;
		min-height: 100vh;
	}

	.site-content {
		flex: 1;
	}

	/* ── Topbar ──────────────────────────────────────────────── */
	.topbar {
		display: flex;
		align-items: center;
		gap: 1rem;
		padding: 0 2rem;
		height: 56px;
		background: var(--bg-deep);
		border-bottom: 1px solid var(--border-gold);
		position: sticky;
		top: 0;
		z-index: 100;
	}

	.topbar__brand {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		text-decoration: none;
		flex-shrink: 0;
	}

	.brand-icon {
		font-size: 1.1rem;
		color: var(--gold);
	}

	.brand-name {
		font-family: var(--font-heading);
		font-size: 1.15rem;
		letter-spacing: 0.15em;
		color: var(--gold-light);
	}

	/* ── Nav links ───────────────────────────────────────────── */
	.topbar__nav {
		display: flex;
		align-items: center;
		gap: 0.25rem;
		flex: 1;
		padding-left: 1.5rem;
	}

	.nav-link {
		font-family: var(--font-heading);
		font-size: 0.75rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--text-muted);
		padding: 0.35rem 0.7rem;
		border-radius: var(--radius);
		text-decoration: none;
		transition: color 0.15s, background 0.15s;
	}

	.nav-link:hover {
		color: var(--gold-light);
		background: var(--bg-surface);
	}

	.nav-link--active {
		color: var(--gold);
	}

	/* ── Auth area ───────────────────────────────────────────── */
	.topbar__auth {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		flex-shrink: 0;
	}

	.auth-greeting {
		font-family: var(--font-body);
		font-size: 1rem;
		color: var(--text-base);
		display: flex;
		align-items: center;
		gap: 0.35rem;
		text-decoration: none;
	}

	.auth-icon {
		color: var(--gold-dim);
		font-size: 0.7rem;
	}

	.auth-btn {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		padding: 0.3rem 0.9rem;
		border-radius: var(--radius);
		border: 1px solid var(--border-gold);
		background: var(--gold-dim);
		color: var(--text-bright);
		cursor: pointer;
		text-decoration: none;
		transition: background 0.15s;
	}

	.auth-btn:hover {
		background: var(--gold);
		color: var(--text-bright);
	}

	.auth-btn--ghost {
		background: transparent;
		border-color: var(--border-mid);
		color: var(--text-muted);
	}

	.auth-btn--ghost:hover {
		background: var(--bg-surface);
		color: var(--text-base);
	}

	.auth-btn--panel {
		background: var(--red-dark);
		border-color: var(--red);
		color: var(--text-bright);
	}

	.auth-btn--panel:hover {
		background: var(--red);
		color: var(--text-bright);
	}

	/* ── Silk badge ───────────────────────────────────────────── */
	.silk-badge {
		display: flex;
		align-items: center;
		gap: 0.3rem;
		padding: 0.2rem 0.55rem;
		background: rgba(139, 94, 28, 0.15);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
	}

	.silk-icon {
		width: 12px;
		height: 14px;
		flex-shrink: 0;
		image-rendering: pixelated;
	}

	.silk-value {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.06em;
		color: var(--gold-light);
	}

	/* ── Footer ──────────────────────────────────────────────── */
	.site-footer {
		background: var(--bg-deep);
		border-top: 1px solid var(--border-dark);
		padding: 1.25rem 2rem;
	}

	.footer-inner {
		max-width: 1200px;
		margin: 0 auto;
		display: flex;
		justify-content: space-between;
		align-items: center;
		gap: 1rem;
	}

	.footer-copy {
		font-size: 0.8rem;
		color: var(--text-dim);
		font-family: var(--font-body);
	}

	.footer-links {
		display: flex;
		gap: 1.25rem;
	}

	.footer-links a {
		font-size: 0.8rem;
		color: var(--text-muted);
	}

	.footer-links a:hover {
		color: var(--gold);
	}

	.footer-admin-link {
		opacity: 0.4;
	}

	.footer-admin-link:hover {
		opacity: 1;
	}

	@media (max-width: 768px) {
		.topbar__nav {
			display: none;
		}
	}
</style>
