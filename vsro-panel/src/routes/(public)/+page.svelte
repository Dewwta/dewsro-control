<script lang="ts">
	import { onMount } from 'svelte';
	import { serverApi, noticeApi, playersApi } from '$lib/api/serverApi';

	let serverOnline: boolean | null = null;
	let playerCount: string | null = null;

	type NewsItem = { date: string; tag: string; title: string; excerpt: string };
	let news: NewsItem[] = [];
	let newsLoading = true;

	function detectTag(subject: string): string {
		const s = subject.toLowerCase();
		if (s.includes('hotfix')) return 'Hotfix';
		if (s.includes('patch'))  return 'Patch';
		if (s.includes('event'))  return 'Event';
		if (s.includes('update')) return 'Update';
		return 'Notice';
	}

	onMount(async () => {
		const [statusResult, countResult] = await Promise.allSettled([
			serverApi.getPublicStatus(),
			playersApi.getOnlineCount()
		]);
		serverOnline = statusResult.status === 'fulfilled' ? statusResult.value.isRunning : false;
		playerCount  = countResult.status  === 'fulfilled' ? countResult.value.count     : '—';

		try {
			const notices = await noticeApi.getAll();
			news = notices.slice(0, 3).map(n => ({
				date:    n.editDate.slice(0, 10),
				tag:     detectTag(n.subject),
				title:   n.subject,
				excerpt: n.article.split('\n').find((l: string) => l.trim()) ?? '',
			}));
		} catch {
			// fail silently — news section stays empty
		} finally {
			newsLoading = false;
		}
	});

	const serverStats = [
		{ label: 'Cap',    value: '120'  },
		{ label: 'Degree', value: '15th' },
		{ label: 'Races',  value: 'CH/EU' },
	];
</script>

<!-- ── Hero ──────────────────────────────────────────────────────── -->
<section class="hero">
	<div class="hero__overlay" />
	<div class="hero__content">
		<h1 class="hero__title">DSRO: Silkroad Online</h1>
		<p class="hero__sub">Private Server — Est. 2015</p>
		<div class="hero__stats">
			{#each serverStats as stat}
				<div class="stat-pill">
					<span class="stat-label">{stat.label}</span>
					<span class="stat-value">{stat.value}</span>
				</div>
			{/each}
			<div
				class="stat-pill"
				class:stat-pill--green={serverOnline === true}
				class:stat-pill--red={serverOnline === false}
			>
				<span class="stat-label">Status</span>
				<span class="stat-value">
					{#if serverOnline === null}Checking…{:else if serverOnline}Online{:else}Offline{/if}
				</span>
			</div>
			<div class="stat-pill">
				<span class="stat-label">Players</span>
				<span class="stat-value">{playerCount ?? '…'}</span>
			</div>
		</div>
		<div class="hero__actions">
			<a href="/about" class="cta cta--ghost">Learn More</a>
		</div>
	</div>
</section>

<!-- ── Main content grid ─────────────────────────────────────────── -->
<section class="main-grid-section">
	<div class="container">
		<div class="main-grid">

			<!-- News column -->
			<div class="news-col">
				<div class="section-header">
					<h2 class="section-title">News &amp; Announcements</h2>
					<a href="/news" class="section-more">View All →</a>
				</div>
				<div class="news-list">
					{#if newsLoading}
						<p class="news-state">Loading…</p>
					{:else if news.length === 0}
						<p class="news-state">No announcements yet.</p>
					{:else}
						{#each news as item}
							<article class="news-card">
								<div class="news-card__meta">
									<span class="news-tag">{item.tag}</span>
									<time class="news-date">{item.date}</time>
								</div>
								<h3 class="news-card__title">{item.title}</h3>
								<p class="news-card__excerpt">{item.excerpt}</p>
								<a href="/news" class="news-card__read">Read more →</a>
							</article>
						{/each}
					{/if}
				</div>
			</div>

			<!-- Side panels -->
			<aside class="side-col">
				<!-- About box -->
				<div class="side-panel">
					<h3 class="side-panel__title">About the Server</h3>
					<p class="side-panel__body">
						A faithful recreation of the classic Silkroad Online experience. No pay-to-win,
						community-driven development, and active events every week.
					</p>
					<a href="/about" class="side-link">More about us →</a>
				</div>

				<!-- Download box -->
				<div class="side-panel side-panel--accent">
					<h3 class="side-panel__title">⬇ Download</h3>
					<p class="side-panel__body">
						Client v1.188 — Full installation with all patches included.
					</p>
					<a href="/download" class="cta cta--primary cta--sm">Download Client</a>
				</div>

			</aside>

		</div>
	</div>
</section>

<style>
	/* ── Layout helpers ──────────────────────────────────────── */
	.container {
		max-width: 1200px;
		margin: 0 auto;
		padding: 0 2rem;
	}

	/* ── Hero ────────────────────────────────────────────────── */
	.hero {
		position: relative;
		min-height: 420px;
		display: flex;
		align-items: center;
		background:
			linear-gradient(160deg, #0a0703 0%, #1a1005 50%, #0d0a03 100%);
		overflow: hidden;
	}

	/* Decorative radial glow */
	.hero::before {
		content: '';
		position: absolute;
		inset: 0;
		background:
			radial-gradient(ellipse 70% 60% at 30% 50%, rgba(200,148,60,0.07) 0%, transparent 70%),
			radial-gradient(ellipse 40% 40% at 80% 30%, rgba(140,32,32,0.08) 0%, transparent 60%);
		pointer-events: none;
	}

	/* Subtle grid texture */
	.hero__overlay {
		position: absolute;
		inset: 0;
		background-image:
			linear-gradient(rgba(200,148,60,0.03) 1px, transparent 1px),
			linear-gradient(90deg, rgba(200,148,60,0.03) 1px, transparent 1px);
		background-size: 40px 40px;
		pointer-events: none;
	}

	.hero__content {
		position: relative;
		padding: 4rem 2rem;
		max-width: 1200px;
		margin: 0 auto;
		width: 100%;
	}

	.hero__title {
		font-family: var(--font-heading);
		font-size: clamp(2rem, 5vw, 3.5rem);
		color: var(--gold-light);
		letter-spacing: 0.12em;
		text-shadow: 0 2px 20px rgba(200,148,60,0.3);
		margin-bottom: 0.4rem;
	}

	.hero__sub {
		font-family: var(--font-body);
		font-size: 1rem;
		color: var(--text-muted);
		letter-spacing: 0.2em;
		text-transform: uppercase;
		margin-bottom: 1.75rem;
	}

	.hero__stats {
		display: flex;
		flex-wrap: wrap;
		gap: 0.6rem;
		margin-bottom: 2rem;
	}

	.stat-pill {
		display: flex;
		gap: 0.4rem;
		align-items: center;
		padding: 0.3rem 0.8rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: 2px;
		font-size: 0.82rem;
	}

	.stat-label {
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
	}

	.stat-value {
		color: var(--text-bright);
		font-family: var(--font-mono);
	}

	.stat-pill--green .stat-value {
		color: var(--green-bright);
	}

	.stat-pill--red .stat-value {
		color: var(--red, #c0392b);
	}

	.hero__actions {
		display: flex;
		gap: 0.75rem;
		flex-wrap: wrap;
	}

	/* ── CTA buttons ─────────────────────────────────────────── */
	.cta {
		display: inline-block;
		font-family: var(--font-heading);
		font-size: 0.8rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		padding: 0.6rem 1.4rem;
		border-radius: var(--radius);
		border: 1px solid var(--border-gold);
		text-decoration: none;
		transition: background 0.15s, color 0.15s;
	}

	.cta--primary {
		background: var(--gold-dim);
		color: var(--text-bright);
	}

	.cta--primary:hover {
		background: var(--gold);
		color: var(--text-bright);
	}

	.cta--ghost {
		background: transparent;
		border-color: var(--border-mid);
		color: var(--text-muted);
	}

	.cta--ghost:hover {
		background: var(--bg-surface);
		color: var(--text-base);
	}

	.cta--sm {
		padding: 0.45rem 1rem;
		font-size: 0.75rem;
	}

	/* ── Main content grid ───────────────────────────────────── */
	.main-grid-section {
		padding: 2.5rem 0 3rem;
	}

	.main-grid {
		display: grid;
		grid-template-columns: 1fr 320px;
		gap: 2rem;
		align-items: start;
	}

	/* ── Section header ──────────────────────────────────────── */
	.section-header {
		display: flex;
		justify-content: space-between;
		align-items: baseline;
		margin-bottom: 1rem;
		padding-bottom: 0.6rem;
		border-bottom: 1px solid var(--border-gold);
	}

	.section-title {
		font-size: 1rem;
		letter-spacing: 0.1em;
		color: var(--gold-light);
	}

	.section-more {
		font-size: 0.78rem;
		color: var(--text-muted);
	}

	.section-more:hover {
		color: var(--gold);
	}

	/* ── News cards ──────────────────────────────────────────── */
	.news-state {
		font-size: 0.85rem;
		color: var(--text-dim);
		padding: 1rem 0;
	}

	.news-list {
		display: flex;
		flex-direction: column;
		gap: 1px;
	}

	.news-card {
		padding: 1.1rem 1.2rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		margin-bottom: 0.6rem;
		transition: border-color 0.15s;
	}

	.news-card:hover {
		border-color: var(--border-gold);
	}

	.news-card__meta {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		margin-bottom: 0.4rem;
	}

	.news-tag {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--gold-dim);
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		padding: 0.1rem 0.4rem;
		border-radius: 2px;
	}

	.news-date {
		font-size: 0.75rem;
		color: var(--text-dim);
		font-family: var(--font-mono);
	}

	.news-card__title {
		font-family: var(--font-heading);
		font-size: 0.95rem;
		color: var(--text-bright);
		letter-spacing: 0.04em;
		margin-bottom: 0.4rem;
	}

	.news-card__excerpt {
		font-size: 0.88rem;
		color: var(--text-muted);
		line-height: 1.6;
		margin-bottom: 0.6rem;
	}

	.news-card__read {
		font-size: 0.78rem;
		color: var(--gold-dim);
	}

	.news-card__read:hover {
		color: var(--gold);
	}

	/* ── Side panels ─────────────────────────────────────────── */
	.side-col {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.side-panel {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.1rem 1.2rem;
		display: flex;
		flex-direction: column;
		gap: 0.6rem;
	}

	.side-panel--accent {
		border-color: var(--border-gold);
		background: var(--bg-raised);
	}

	.side-panel__title {
		font-family: var(--font-heading);
		font-size: 0.85rem;
		letter-spacing: 0.08em;
		color: var(--gold-light);
		padding-bottom: 0.5rem;
		border-bottom: 1px solid var(--border-dark);
	}

	.side-panel__body {
		font-size: 0.85rem;
		color: var(--text-muted);
		line-height: 1.6;
	}

	.side-link {
		font-size: 0.8rem;
		color: var(--gold-dim);
		align-self: flex-start;
	}

	.side-link:hover {
		color: var(--gold);
	}

	/* ── Responsive ──────────────────────────────────────────── */
	@media (max-width: 768px) {
		.main-grid {
			grid-template-columns: 1fr;
		}
	}
</style>
