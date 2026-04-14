<script lang="ts">
	import { page } from '$app/stores';
	import { statusStore } from '$lib/stores/serverStatus';

	interface NavItem {
		label: string;
		href: string;
		icon: string;
		wip?: boolean;
	}

	const navItems: NavItem[] = [
		{
			label: 'Dashboard',
			href: '/dashboard',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>`
		},
		{
			label: 'Controls',
			href: '/controls',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18.36 6.64a9 9 0 1 1-12.73 0"/><line x1="12" y1="2" x2="12" y2="12"/></svg>`
		},
		{
			label: 'Settings',
			href: '/settings',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>`
		},
		{
			label: 'World',
			href: '/world',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>`
		},
		{
			label: 'Textdata',
			href: '/textdata',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>`
		},
		{
			label: 'Players',
			href: '/players',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>`
		},
		{
			label: 'Live',
			href: '/live',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M6.3 6.3a8 8 0 0 0 0 11.4"/><path d="M17.7 6.3a8 8 0 0 1 0 11.4"/><path d="M3.5 3.5a14 14 0 0 0 0 17"/><path d="M20.5 3.5a14 14 0 0 1 0 17"/></svg>`
		},
		{
			label: 'Economy',
			href: '/economy',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>`
		},
		{
			label: 'Logs',
			href: '/logs',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></svg>`
		},
		{
			label: 'Clients',
			href: '/clients',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>`
		},
		{
			label: 'Database',
			href: '/database',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg>`
		},
		{
			label: 'Quests',
			href: '/quests',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>`
		},
		{
			label: 'Admin Controls',
			href: '/privileged-ips',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>`
		},
		{
			label: 'Invite Codes',
			href: '/invite-codes',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>`
		},
		{
			label: 'Patching',
			href: '/patching',
			icon: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>`
		}
	];

	$: currentPath = $page.url.pathname;
	$: isServerRunning = $statusStore.data?.isRunning ?? false;
	$: apiConnected = $statusStore.error === null && $statusStore.lastUpdated !== null;
</script>

<aside class="sidebar">
	<!-- ── Header / Branding ── -->
	<div class="sidebar__header">
		<div class="sidebar__logo">
			<span class="sidebar__emblem">⚔</span>
			<div class="sidebar__brand">
				<span class="sidebar__brand-name">DSRO</span>
				<span class="sidebar__brand-sub">Control Panel</span>
			</div>
		</div>
	</div>

	<div class="sidebar__rule"></div>

	<!-- ── Navigation ── -->
	<nav class="sidebar__nav">
		{#each navItems as item (item.href)}
			<a
				href={item.href}
				class="nav-item"
				class:nav-item--active={currentPath.startsWith(item.href)}
				class:nav-item--wip={item.wip}
				title={item.wip ? `${item.label} — Coming Soon` : item.label}
			>
				<span class="nav-item__icon">{@html item.icon}</span>
				<span class="nav-item__label">{item.label}</span>
				{#if item.wip}
					<span class="nav-item__pip">soon</span>
				{/if}
			</a>
		{/each}
	</nav>

	<!-- ── Back to site ── -->
	<div class="sidebar__back">
		<a href="/" class="back-link">
			<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg>
			Back to Site
		</a>
	</div>

	<!-- ── Footer Status ── -->
	<div class="sidebar__footer">
		<div class="sidebar__rule sidebar__rule--faint"></div>
		<div class="status-row">
			<span
				class="dot"
				class:dot--on={isServerRunning}
				class:dot--off={!isServerRunning}
			></span>
			<span class="status-row__label">
				{isServerRunning ? 'Server Online' : 'Server Offline'}
			</span>
		</div>
		<div class="status-row status-row--sm">
			<span
				class="dot dot--xs"
				class:dot--on={apiConnected}
				class:dot--off={!apiConnected}
			></span>
			<span class="status-row__label status-row__label--dim">
				API {apiConnected ? 'Connected' : 'Unreachable'}
			</span>
		</div>
	</div>
</aside>

<style>
	.sidebar {
		width: var(--sidebar-width);
		min-height: 100vh;
		height: 100vh;
		background: var(--bg-surface);
		border-right: 1px solid var(--border-gold);
		display: flex;
		flex-direction: column;
		flex-shrink: 0;
		overflow-y: auto;
	}

	/* ── Header ─── */
	.sidebar__header {
		padding: 1.2rem 1rem 1rem;
	}

	.sidebar__logo {
		display: flex;
		align-items: center;
		gap: 0.6rem;
	}

	.sidebar__emblem {
		font-size: 1.55rem;
		color: var(--gold);
		line-height: 1;
		flex-shrink: 0;
	}

	.sidebar__brand {
		display: flex;
		flex-direction: column;
	}

	.sidebar__brand-name {
		font-family: var(--font-heading);
		font-size: 1.05rem;
		font-weight: 700;
		color: var(--gold-light);
		letter-spacing: 0.14em;
		line-height: 1;
	}

	.sidebar__brand-sub {
		font-size: 0.6rem;
		color: var(--text-muted);
		letter-spacing: 0.12em;
		text-transform: uppercase;
		margin-top: 3px;
	}

	/* ── Decorative rule ─── */
	.sidebar__rule {
		height: 1px;
		background: linear-gradient(to right, transparent, var(--border-gold), transparent);
		margin: 0 0.5rem;
	}

	.sidebar__rule--faint {
		background: linear-gradient(to right, transparent, var(--border-dark), transparent);
	}

	/* ── Nav ─── */
	.sidebar__nav {
		flex: 1;
		display: flex;
		flex-direction: column;
		gap: 2px;
		padding: 0.6rem 0.5rem;
	}

	.nav-item {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		padding: 0.5rem 0.7rem;
		border-radius: var(--radius);
		border-left: 2px solid transparent;
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.76rem;
		letter-spacing: 0.07em;
		text-decoration: none;
		transition: background 0.15s, color 0.15s, border-color 0.15s;
	}

	.nav-item:hover {
		background: var(--bg-raised);
		color: var(--text-base);
		border-left-color: var(--border-gold);
	}

	.nav-item--active {
		background: var(--bg-raised);
		color: var(--gold-light);
		border-left-color: var(--gold);
	}

	.nav-item--wip {
		opacity: 0.45;
	}

	.nav-item__icon {
		width: 15px;
		height: 15px;
		display: flex;
		align-items: center;
		justify-content: center;
		flex-shrink: 0;
	}

	.nav-item__icon :global(svg) {
		width: 15px;
		height: 15px;
	}

	.nav-item__label {
		flex: 1;
	}

	.nav-item__pip {
		font-size: 0.55rem;
		text-transform: uppercase;
		letter-spacing: 0.06em;
		color: var(--text-dim);
		background: var(--bg-hover);
		padding: 1px 5px;
		border-radius: 2px;
	}

	/* ── Back to site ─── */
	.sidebar__back {
		padding: 0 0.5rem 0.5rem;
	}

	.back-link {
		display: flex;
		align-items: center;
		gap: 0.45rem;
		padding: 0.45rem 0.7rem;
		border-radius: var(--radius);
		border: 1px solid var(--border-dark);
		color: var(--text-dim);
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.07em;
		text-decoration: none;
		transition: background 0.15s, color 0.15s, border-color 0.15s;
	}

	.back-link:hover {
		background: var(--bg-raised);
		color: var(--text-muted);
		border-color: var(--border-mid);
	}

	.back-link :global(svg) {
		width: 13px;
		height: 13px;
		flex-shrink: 0;
	}

	/* ── Footer ─── */
	.sidebar__footer {
		padding: 0.5rem 0.8rem 1rem;
		display: flex;
		flex-direction: column;
		gap: 0.3rem;
	}

	.sidebar__rule {
		margin-bottom: 0.6rem;
	}

	.status-row {
		display: flex;
		align-items: center;
		gap: 0.45rem;
	}

	.status-row--sm {
		margin-top: 2px;
	}

	.status-row__label {
		font-size: 0.75rem;
		color: var(--text-muted);
		font-family: var(--font-heading);
		letter-spacing: 0.05em;
	}

	.status-row__label--dim {
		font-size: 0.65rem;
		color: var(--text-dim);
	}

	/* ── Dots ─── */
	.dot {
		width: 8px;
		height: 8px;
		border-radius: 50%;
		flex-shrink: 0;
		background: var(--text-dim);
	}

	.dot--xs {
		width: 6px;
		height: 6px;
	}

	.dot--on {
		background: var(--green-bright);
		box-shadow: 0 0 6px var(--green-bright);
	}

	.dot--off {
		background: var(--red-light);
	}
</style>
