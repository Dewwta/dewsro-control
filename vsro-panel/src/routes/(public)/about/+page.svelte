<script lang="ts">
	import { onMount } from 'svelte';
	import { authStore } from '$lib/stores/auth';
	import { serverCfgApi, type ServerRates } from '$lib/api/serverApi';

	let rates: ServerRates | null = null;
	let ratesError = false;

	onMount(async () => {
		try {
			rates = await serverCfgApi.getRates();
		} catch {
			ratesError = true;
		}
	});

	/** Converts raw cfg value to a display multiplier string. 100 = 1x, 500 = 5x */
	function toMultiplier(raw: number): string {
		return `${raw / 100}x`;
	}
</script>

<!-- ── About page ─────────────────────────────────────────────────── -->

<!-- Page header -->
<div class="page-header">
	<div class="container">
		<h1 class="page-title">About the Server</h1>
		<p class="page-subtitle">Everything you need to know about who we are and how to get in touch.</p>
	</div>
</div>

<div class="page-body">
	<div class="container">
		<div class="about-grid">

			<!-- ── Main content ── -->
			<main class="main-col">

				<!-- Story section -->
				<section class="content-section">
					<h2 class="section-title">Our Story</h2>
					<div class="section-body">
						<p>
							<!-- TODO: Replace with your server's story -->
							Founded in 2015, DSRO is a recreation of the original Silkroad Online experience. After the
                            company joymax buried the game to allow botters to take hold of the entire game, this server
                            is the last true 2008 experience of Silkroad Online. Please report any issues or bugs through
                            the 'about' page. DSRO the name, has no special meaning, it's just the first letter of my alias.
						</p>
						<p>
							This server is ran by Dellta, with no other moderators. So please be understanding and patient.
                            
						</p>
					</div>
				</section>

				<!-- What we offer -->
				<section class="content-section">
					<h2 class="section-title">What We Offer</h2>
					<div class="feature-grid">
						<div class="feature-card">
							<span class="feature-icon">⚔</span>
							<h3 class="feature-title">Classic Gameplay</h3>
							<p class="feature-desc">
								Cap 120, Degree 15 — faithful to the original, with quality-of-life improvements
								where they actually matter.
							</p>
						</div>
						<div class="feature-card">
							<span class="feature-icon">◈</span>
							<h3 class="feature-title">No Pay-to-Win</h3>
							<p class="feature-desc">
								Silk is not payed for. You earn silk by playing, and participating in
                                in-game events.
							</p>
						</div>
						<div class="feature-card">
							<span class="feature-icon">⚡</span>
							<h3 class="feature-title">Active Events</h3>
							<p class="feature-desc">
								Regular in-game events, seasonal content, and GM-hosted competitions keep the
								world alive week after week.
							</p>
						</div>
						<div class="feature-card">
							<span class="feature-icon">🛡</span>
							<h3 class="feature-title">Anti-Cheat</h3>
							<p class="feature-desc">
								Custom from-scratch anti-cheat to detect botters, and cheaters. As well as a reporting system.
                                Hundreds of hours of research and code went into making the server safe,
                                and reliable.
							</p>
						</div>
					</div>
				</section>

				<!-- Server rules / values -->
				<section class="content-section">
					<h2 class="section-title">Our Rules</h2>
					<div class="rules-list">
						<div class="rule-item">
							<span class="rule-num">01</span>
							<div>
								<strong>Respect all players</strong>
								<p>Harassment, hate speech, and targeted toxicity will not be tolerated in any channel.</p>
							</div>
						</div>
						<div class="rule-item">
							<span class="rule-num">02</span>
							<div>
								<strong>No cheating or exploiting</strong>
								<p>Use of third-party programs, bots, or exploiting known bugs is grounds for a permanent ban.</p>
							</div>
						</div>
						<div class="rule-item">
							<span class="rule-num">03</span>
							<div>
								<strong>No account sharing or selling</strong>
								<p>Accounts are personal. Sharing or selling your account voids all support for it.</p>
							</div>
						</div>
						<div class="rule-item">
							<span class="rule-num">04</span>
							<div>
								<strong>Follow staff instructions</strong>
								<p>GMs and moderators have final say in-game. Disputes can be escalated through proper channels.</p>
							</div>
						</div>
					</div>
				</section>

			</main>

			<!-- ── Sidebar ── -->
			<aside class="side-col">

				<!-- Server info -->
				<div class="side-panel">
					<h3 class="side-panel__title">Server Info</h3>
					<ul class="info-list">
						<li class="info-row">
							<span class="info-label">Cap</span>
							<span class="info-value">120</span>
						</li>
						<li class="info-row">
							<span class="info-label">Degree</span>
							<span class="info-value">15th</span>
						</li>
						<li class="info-row">
							<span class="info-label">Races</span>
							<span class="info-value">CH / EU</span>
						</li>
						<li class="info-row">
							<span class="info-label">XP Rate</span>
							<span class="info-value">
								{#if rates}
									{toMultiplier(rates.expRatio)}
								{:else if ratesError}
									<span class="rate-unavailable">—</span>
								{:else}
									<span class="rate-loading">…</span>
								{/if}
							</span>
						</li>
						<li class="info-row">
							<span class="info-label">Party XP</span>
							<span class="info-value">
								{#if rates}
									{toMultiplier(rates.expRatioParty)}
								{:else if ratesError}
									<span class="rate-unavailable">—</span>
								{:else}
									<span class="rate-loading">…</span>
								{/if}
							</span>
						</li>
						<li class="info-row">
							<span class="info-label">Drop Rate</span>
							<span class="info-value">
								{#if rates}
									{toMultiplier(rates.dropItemRatio)}
								{:else if ratesError}
									<span class="rate-unavailable">—</span>
								{:else}
									<span class="rate-loading">…</span>
								{/if}
							</span>
						</li>
						<li class="info-row">
							<span class="info-label">Gold Drop</span>
							<span class="info-value">
								{#if rates}
									{toMultiplier(rates.dropGoldAmountCoef)}
								{:else if ratesError}
									<span class="rate-unavailable">—</span>
								{:else}
									<span class="rate-loading">…</span>
								{/if}
							</span>
						</li>
						<li class="info-row">
							<span class="info-label">Since</span>
							<span class="info-value">2015</span>
						</li>
					</ul>
				</div>

				<!-- Contact / Support -->
				<div class="side-panel">
					<h3 class="side-panel__title">Contact &amp; Support</h3>
					<div class="contact-list">
						<a href="#" class="contact-row">
							<span class="contact-icon">💬</span>
							<div>
								<span class="contact-label">Discord</span>
								<span class="contact-sub">Join our community server</span>
							</div>
						</a>
						<a href="#" class="contact-row">
							<span class="contact-icon">✉</span>
							<div>
								<span class="contact-label">Email</span>
								<span class="contact-sub">support@[yourserver].com</span>
							</div>
						</a>
						<a href="#" class="contact-row">
							<span class="contact-icon">📋</span>
							<div>
								<span class="contact-label">Forum</span>
								<span class="contact-sub">Bug reports &amp; suggestions</span>
							</div>
						</a>
					</div>
				</div>

				<!-- Staff -->
				<div class="side-panel">
					<h3 class="side-panel__title">Staff</h3>
					<ul class="staff-list">
						<!-- TODO: Replace with real staff names/roles -->
						<li class="staff-row">
							<span class="staff-role staff-role--owner">Owner</span>
							<span class="staff-name">Dellta (or stevie)</span>
						</li>
						<li class="staff-row">
							<span class="staff-role staff-role--gm">GM</span>
							<span class="staff-name">N/A</span>
						</li>
						<li class="staff-row">
							<span class="staff-role staff-role--mod">Mod</span>
							<span class="staff-name">N/A</span>
						</li>
					</ul>
				</div>

				<!-- Download CTA -->
				<div class="side-panel side-panel--accent">
					<h3 class="side-panel__title">⬇ Ready to Play?</h3>
					<p class="side-panel__body">Download the client and create your account to get started.</p>
					<div class="cta-row">
						<a href="/download" class="cta cta--primary cta--sm">Download</a>
						{#if !$authStore.user}
							<a href="/login" class="cta cta--ghost cta--sm">Sign In</a>
						{/if}
					</div>
				</div>

			</aside>
		</div>
	</div>
</div>

<style>
	/* ── Layout ──────────────────────────────────────────────── */
	.container {
		max-width: 1200px;
		margin: 0 auto;
		padding: 0 2rem;
	}

	/* ── Page header ─────────────────────────────────────────── */
	.page-header {
		background:
			linear-gradient(160deg, #0a0703 0%, #1a1005 50%, #0d0a03 100%);
		border-bottom: 1px solid var(--border-gold);
		padding: 3rem 0 2.5rem;
		position: relative;
		overflow: hidden;
	}

	.page-header::before {
		content: '';
		position: absolute;
		inset: 0;
		background:
			radial-gradient(ellipse 60% 80% at 10% 50%, rgba(200,148,60,0.06) 0%, transparent 70%),
			radial-gradient(ellipse 30% 50% at 90% 20%, rgba(140,32,32,0.06) 0%, transparent 60%);
		pointer-events: none;
	}

	.page-header::after {
		content: '';
		position: absolute;
		inset: 0;
		background-image:
			linear-gradient(rgba(200,148,60,0.025) 1px, transparent 1px),
			linear-gradient(90deg, rgba(200,148,60,0.025) 1px, transparent 1px);
		background-size: 40px 40px;
		pointer-events: none;
	}

	.page-title {
		font-family: var(--font-heading);
		font-size: clamp(1.6rem, 4vw, 2.4rem);
		color: var(--gold-light);
		letter-spacing: 0.12em;
		text-shadow: 0 2px 16px rgba(200,148,60,0.25);
		margin-bottom: 0.4rem;
		position: relative;
	}

	.page-subtitle {
		font-size: 0.9rem;
		color: var(--text-muted);
		letter-spacing: 0.05em;
		position: relative;
	}

	/* ── Page body ───────────────────────────────────────────── */
	.page-body {
		padding: 2.5rem 0 3.5rem;
	}

	.about-grid {
		display: grid;
		grid-template-columns: 1fr 300px;
		gap: 2rem;
		align-items: start;
	}

	/* ── Content sections ────────────────────────────────────── */
	.content-section {
		margin-bottom: 2.5rem;
	}

	.section-title {
		font-family: var(--font-heading);
		font-size: 0.9rem;
		letter-spacing: 0.12em;
		text-transform: uppercase;
		color: var(--gold-light);
		padding-bottom: 0.6rem;
		border-bottom: 1px solid var(--border-gold);
		margin-bottom: 1.2rem;
	}

	.section-body p {
		font-size: 0.9rem;
		color: var(--text-muted);
		line-height: 1.75;
		margin-bottom: 0.8rem;
	}

	.section-body p:last-child {
		margin-bottom: 0;
	}

	/* ── Feature grid ────────────────────────────────────────── */
	.feature-grid {
		display: grid;
		grid-template-columns: repeat(2, 1fr);
		gap: 1px;
		background: var(--border-dark);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
	}

	.feature-card {
		background: var(--bg-surface);
		padding: 1.25rem 1.2rem;
		display: flex;
		flex-direction: column;
		gap: 0.4rem;
		transition: background 0.15s;
	}

	.feature-card:hover {
		background: var(--bg-raised);
	}

	.feature-icon {
		font-size: 1.3rem;
		line-height: 1;
		margin-bottom: 0.2rem;
	}

	.feature-title {
		font-family: var(--font-heading);
		font-size: 0.82rem;
		letter-spacing: 0.06em;
		color: var(--gold);
	}

	.feature-desc {
		font-size: 0.82rem;
		color: var(--text-muted);
		line-height: 1.55;
	}

	/* ── Rules list ──────────────────────────────────────────── */
	.rules-list {
		display: flex;
		flex-direction: column;
		gap: 1px;
	}

	.rule-item {
		display: flex;
		gap: 1.2rem;
		align-items: flex-start;
		padding: 1rem 1.2rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		margin-bottom: 0.5rem;
		transition: border-color 0.15s;
	}

	.rule-item:hover {
		border-color: var(--border-gold);
	}

	.rule-num {
		font-family: var(--font-mono);
		font-size: 0.7rem;
		color: var(--gold-dim);
		padding-top: 0.1rem;
		flex-shrink: 0;
		letter-spacing: 0.05em;
	}

	.rule-item strong {
		display: block;
		font-family: var(--font-heading);
		font-size: 0.82rem;
		letter-spacing: 0.05em;
		color: var(--text-bright);
		margin-bottom: 0.25rem;
	}

	.rule-item p {
		font-size: 0.82rem;
		color: var(--text-muted);
		line-height: 1.55;
		margin: 0;
	}

	/* ── Sidebar ─────────────────────────────────────────────── */
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
		gap: 0.75rem;
	}

	.side-panel--accent {
		border-color: var(--border-gold);
		background: var(--bg-raised);
	}

	.side-panel__title {
		font-family: var(--font-heading);
		font-size: 0.82rem;
		letter-spacing: 0.08em;
		color: var(--gold-light);
		padding-bottom: 0.5rem;
		border-bottom: 1px solid var(--border-dark);
	}

	.side-panel__body {
		font-size: 0.83rem;
		color: var(--text-muted);
		line-height: 1.6;
	}

	/* ── Server info list ────────────────────────────────────── */
	.info-list {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.info-row {
		display: flex;
		justify-content: space-between;
		align-items: center;
		font-size: 0.82rem;
	}

	.info-label {
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.06em;
		text-transform: uppercase;
	}

	.info-value {
		color: var(--text-bright);
		font-family: var(--font-mono);
		font-size: 0.82rem;
	}

	.rate-loading {
		color: var(--text-dim);
		font-style: italic;
	}

	.rate-unavailable {
		color: var(--text-dim);
	}

	/* ── Contact list ────────────────────────────────────────── */
	.contact-list {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.contact-row {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		padding: 0.6rem 0.75rem;
		background: var(--bg-raised);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		text-decoration: none;
		transition: border-color 0.15s, background 0.15s;
	}

	.contact-row:hover {
		border-color: var(--border-gold);
		background: var(--bg-deep);
	}

	.contact-icon {
		font-size: 1.1rem;
		flex-shrink: 0;
	}

	.contact-label {
		display: block;
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.05em;
		color: var(--gold);
	}

	.contact-sub {
		display: block;
		font-size: 0.74rem;
		color: var(--text-dim);
	}

	/* ── Staff list ──────────────────────────────────────────── */
	.staff-list {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.staff-row {
		display: flex;
		align-items: center;
		gap: 0.6rem;
		font-size: 0.83rem;
	}

	.staff-role {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		padding: 0.1rem 0.4rem;
		border-radius: 2px;
		border: 1px solid;
		flex-shrink: 0;
	}

	.staff-role--owner {
		color: var(--gold-light);
		border-color: var(--border-gold);
		background: rgba(200,148,60,0.08);
	}

	.staff-role--gm {
		color: #7ecfff;
		border-color: rgba(126,207,255,0.3);
		background: rgba(126,207,255,0.05);
	}

	.staff-role--mod {
		color: #a8e6a0;
		border-color: rgba(168,230,160,0.3);
		background: rgba(168,230,160,0.05);
	}

	.staff-name {
		color: var(--text-base);
		font-family: var(--font-body);
	}

	/* ── CTA buttons ─────────────────────────────────────────── */
	.cta-row {
		display: flex;
		gap: 0.5rem;
		flex-wrap: wrap;
	}

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

	/* ── Responsive ──────────────────────────────────────────── */
	@media (max-width: 768px) {
		.about-grid {
			grid-template-columns: 1fr;
		}

		.feature-grid {
			grid-template-columns: 1fr;
		}
	}
</style>
