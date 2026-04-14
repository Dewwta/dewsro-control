<script lang="ts">
	let selected = 0;
	let lightbox = false;

	const regions = [
		{
			label:    'Jangan',
			sublabel: 'China — Central Hub',
			file:     'JanganMonsterMap.jpg',
			desc:     'The starting city of the Silk Road. Home to low- and mid-level Chinese monsters across the surrounding plains and caves.'
		},
		{
			label:    'Donwhang',
			sublabel: 'West China',
			file:     'DonwhangMonsterMap.jpg',
			desc:     'A desert trade city west of Jangan. Mid-range monsters populate the surrounding dunes and rocky passes.'
		},
		{
			label:    'Hotan',
			sublabel: 'Oasis Kingdom',
			file:     'OasisKingdomMonsterMap.jpg',
			desc:     'An oasis city deep in the desert. Higher-level Chinese monsters guard the surrounding region and its cave systems.'
		},
		{
			label:    'Taklamakan',
			sublabel: 'Desert Wilderness',
			file:     'TaklamanMonsterMap.jpg',
			desc:     'The vast Taklamakan desert stretching between the Chinese cities. Dangerous travel routes filled with elite-tier monsters.'
		},
		{
			label:    'Roc Mountain',
			sublabel: 'Elite Zone',
			file:     'RocMountainMonsterMap.jpg',
			desc:     'A treacherous mountain range reserved for high-level players. Unique bosses and dense elite spawns throughout.'
		},
		{
			label:    'Constantinople',
			sublabel: 'Europe — Capital',
			file:     'EuropeMonsterMap.jpg',
			desc:     'The capital of the European region. A hub for EU-race characters with mid-to-high level monsters in the surrounding areas.'
		},
        {
			label:    'Samarakand',
			sublabel: 'Asia',
			file:     'SamarakandMonsterMap.jpg',
			desc:     'Samarakand, the captial of Asia, is a ruthless region within the heart of Asia. Home to mid level monsters.'
		},
		{
			label:    'Asia Minor',
			sublabel: 'Samarkand Region',
			file:     'AsiaMinorMonsterMap.jpg',
			desc:     'The wild lands connecting Europe and Central Asia. Features a range of European and mixed monsters across open plains.'
		},
        {
			label:    'Qin-Shi Tomb B1',
			sublabel: 'Jangan Tomb',
			file:     'QinShiB1MonsterMap.jpg',
			desc:     'The first level of the Jangan Qin-Shi Tomb, also known as B1'
		},
        {
			label:    'Qin-Shi Tomb B2',
			sublabel: 'Jangan Tomb',
			file:     'QinShiB2MonsterMap.jpg',
			desc:     'The second level of the Jangan Qin-Shi Tomb, also known as B2'
		},
        {
			label:    'Qin-Shi Tomb B3',
			sublabel: 'Jangan Tomb',
			file:     'QinShiB3MonsterMap.jpg',
			desc:     'The third level of the Jangan Qin-Shi Tomb, also known as B3'
		},
        {
			label:    'Qin-Shi Tomb B4',
			sublabel: 'Jangan Tomb',
			file:     'QinShiB4MonsterMap.jpg',
			desc:     'The fourth level of the Jangan Qin-Shi Tomb, also known as B4'
		},
		{
			label:    'Alexandria',
			sublabel: 'Egypt Region',
			file:     'AlexandriaMonsterMap.jpg',
			desc:     'The Egyptian city of Alexandria and its surroundings. Egypt-specific monsters, tombs, and the infamous King\'s Valley lie nearby.'
		},
	];

	$: region = regions[selected];

	function openLightbox() { lightbox = true; }
	function closeLightbox() { lightbox = false; }

	function onKeydown(e: KeyboardEvent) {
		if (!lightbox) return;
		if (e.key === 'Escape') closeLightbox();
		if (e.key === 'ArrowRight') selected = (selected + 1) % regions.length;
		if (e.key === 'ArrowLeft')  selected = (selected - 1 + regions.length) % regions.length;
	}
</script>

<svelte:window on:keydown={onKeydown} />

<!-- ── Page header ──────────────────────────────────────────────── -->
<section class="guide-hero">
	<div class="guide-hero__overlay"></div>
	<div class="guide-hero__content">
		<h1 class="guide-hero__title">Monster Maps</h1>
		<p class="guide-hero__sub">Regional spawn guides for all zones</p>
	</div>
</section>

<!-- ── Main layout ──────────────────────────────────────────────── -->
<div class="guide-shell">

	<!-- Region picker sidebar -->
	<aside class="region-list">
		<div class="region-list__label">Regions</div>
		{#each regions as r, i}
			<button
				class="region-btn"
				class:region-btn--active={selected === i}
				on:click={() => selected = i}
			>
				<span class="region-btn__name">{r.label}</span>
				<span class="region-btn__sub">{r.sublabel}</span>
			</button>
		{/each}
	</aside>

	<!-- Map panel -->
	<div class="map-panel">
		<div class="map-panel__header">
			<div>
				<h2 class="map-panel__title">{region.label}</h2>
				<span class="map-panel__sub">{region.sublabel}</span>
			</div>
			<span class="map-panel__hint">Click image to enlarge</span>
		</div>

		<button class="map-panel__img-wrap" on:click={openLightbox} aria-label="Enlarge map">
			<img
				src="/{region.file}"
				alt="{region.label} Monster Map"
				class="map-panel__img"
			/>
			<div class="map-panel__zoom-badge">
				<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="11" y1="8" x2="11" y2="14"/><line x1="8" y1="11" x2="14" y2="11"/></svg>
			</div>
		</button>

		<p class="map-panel__desc">{region.desc}</p>

		<!-- Prev / next nav -->
		<div class="map-nav">
			<button
				class="map-nav__btn"
				on:click={() => selected = (selected - 1 + regions.length) % regions.length}
			>
				← {regions[(selected - 1 + regions.length) % regions.length].label}
			</button>
			<span class="map-nav__count">{selected + 1} / {regions.length}</span>
			<button
				class="map-nav__btn map-nav__btn--right"
				on:click={() => selected = (selected + 1) % regions.length}
			>
				{regions[(selected + 1) % regions.length].label} →
			</button>
		</div>
	</div>

</div>

<!-- ── Lightbox ──────────────────────────────────────────────────── -->
{#if lightbox}
	<!-- svelte-ignore a11y-click-events-have-key-events a11y-no-static-element-interactions -->
	<div class="lightbox" on:click={closeLightbox}>
		<!-- svelte-ignore a11y-click-events-have-key-events a11y-no-static-element-interactions -->
		<div class="lightbox__inner" on:click|stopPropagation>
			<div class="lightbox__toolbar">
				<span class="lightbox__label">{region.label} — {region.sublabel}</span>
				<button class="lightbox__close" on:click={closeLightbox} aria-label="Close">✕</button>
			</div>
			<img
				src="/{region.file}"
				alt="{region.label} Monster Map"
				class="lightbox__img"
			/>
			<div class="lightbox__nav">
				<button class="lightbox__nav-btn" on:click={() => selected = (selected - 1 + regions.length) % regions.length}>
					‹ Prev
				</button>
				<span class="lightbox__nav-count">{selected + 1} / {regions.length}</span>
				<button class="lightbox__nav-btn" on:click={() => selected = (selected + 1) % regions.length}>
					Next ›
				</button>
			</div>
		</div>
	</div>
{/if}

<style>
	/* ── Hero ─────────────────────────────────────────────────── */
	.guide-hero {
		position: relative;
		height: 160px;
		display: flex;
		align-items: center;
		background: linear-gradient(160deg, #0a0703 0%, #1a1005 60%, #0d0a03 100%);
		overflow: hidden;
	}

	.guide-hero::before {
		content: '';
		position: absolute;
		inset: 0;
		background:
			radial-gradient(ellipse 60% 80% at 25% 50%, rgba(200,148,60,0.07) 0%, transparent 70%),
			radial-gradient(ellipse 30% 60% at 80% 30%, rgba(140,32,32,0.07) 0%, transparent 60%);
		pointer-events: none;
	}

	.guide-hero__overlay {
		position: absolute;
		inset: 0;
		background-image:
			linear-gradient(rgba(200,148,60,0.03) 1px, transparent 1px),
			linear-gradient(90deg, rgba(200,148,60,0.03) 1px, transparent 1px);
		background-size: 40px 40px;
		pointer-events: none;
	}

	.guide-hero__content {
		position: relative;
		padding: 0 2rem;
		max-width: 1200px;
		margin: 0 auto;
		width: 100%;
	}

	.guide-hero__title {
		font-family: var(--font-heading);
		font-size: clamp(1.6rem, 4vw, 2.4rem);
		color: var(--gold-light);
		letter-spacing: 0.12em;
		text-shadow: 0 2px 16px rgba(200,148,60,0.25);
		margin-bottom: 0.25rem;
	}

	.guide-hero__sub {
		font-size: 0.82rem;
		color: var(--text-muted);
		letter-spacing: 0.18em;
		text-transform: uppercase;
	}

	/* ── Shell ────────────────────────────────────────────────── */
	.guide-shell {
		display: grid;
		grid-template-columns: 200px 1fr;
		gap: 0;
		max-width: 1200px;
		margin: 0 auto;
		padding: 2rem;
		align-items: start;
	}

	@media (max-width: 700px) {
		.guide-shell {
			grid-template-columns: 1fr;
			padding: 1rem;
		}
	}

	/* ── Region list ──────────────────────────────────────────── */
	.region-list {
		display: flex;
		flex-direction: column;
		gap: 2px;
		padding-right: 1.25rem;
		border-right: 1px solid var(--border-dark);
		position: sticky;
		top: 72px;
	}

	@media (max-width: 700px) {
		.region-list {
			flex-direction: row;
			flex-wrap: wrap;
			padding-right: 0;
			border-right: none;
			border-bottom: 1px solid var(--border-dark);
			padding-bottom: 1rem;
			margin-bottom: 1.25rem;
			position: static;
		}
	}

	.region-list__label {
		font-family: var(--font-heading);
		font-size: 0.6rem;
		letter-spacing: 0.14em;
		text-transform: uppercase;
		color: var(--text-dim);
		padding: 0 0.5rem 0.5rem;
		margin-bottom: 0.25rem;
		border-bottom: 1px solid var(--border-dark);
	}

	@media (max-width: 700px) {
		.region-list__label { display: none; }
	}

	.region-btn {
		display: flex;
		flex-direction: column;
		gap: 0.1rem;
		padding: 0.5rem 0.7rem;
		border-radius: var(--radius);
		border: 1px solid transparent;
		border-left: 2px solid transparent;
		background: transparent;
		cursor: pointer;
		text-align: left;
		transition: background 0.13s, color 0.13s, border-color 0.13s;
	}

	.region-btn:hover {
		background: var(--bg-raised);
		border-left-color: var(--border-gold);
	}

	.region-btn--active {
		background: var(--bg-raised);
		border-left-color: var(--gold);
	}

	.region-btn__name {
		font-family: var(--font-heading);
		font-size: 0.76rem;
		letter-spacing: 0.06em;
		color: var(--text-base);
	}

	.region-btn--active .region-btn__name {
		color: var(--gold-light);
	}

	.region-btn__sub {
		font-size: 0.65rem;
		color: var(--text-dim);
	}

	@media (max-width: 700px) {
		.region-btn {
			flex-direction: row;
			align-items: center;
			gap: 0.4rem;
			border-left-width: 1px;
			border-bottom-width: 2px;
		}

		.region-btn--active {
			border-bottom-color: var(--gold);
			border-left-color: transparent;
		}

		.region-btn__sub { display: none; }
	}

	/* ── Map panel ────────────────────────────────────────────── */
	.map-panel {
		padding-left: 1.75rem;
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	@media (max-width: 700px) {
		.map-panel { padding-left: 0; }
	}

	.map-panel__header {
		display: flex;
		align-items: flex-end;
		justify-content: space-between;
		gap: 1rem;
		padding-bottom: 0.6rem;
		border-bottom: 1px solid var(--border-gold);
	}

	.map-panel__title {
		font-family: var(--font-heading);
		font-size: 1.15rem;
		letter-spacing: 0.1em;
		color: var(--gold-light);
		margin-bottom: 0.15rem;
	}

	.map-panel__sub {
		font-size: 0.73rem;
		color: var(--text-muted);
		letter-spacing: 0.1em;
		text-transform: uppercase;
	}

	.map-panel__hint {
		font-size: 0.68rem;
		color: var(--text-dim);
		letter-spacing: 0.05em;
		flex-shrink: 0;
	}

	/* Map image wrapper — clickable */
	.map-panel__img-wrap {
		position: relative;
		display: block;
		width: 100%;
		background: var(--bg-deep);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		overflow: hidden;
		cursor: zoom-in;
		padding: 0;
		transition: border-color 0.15s;
	}

	.map-panel__img-wrap:hover {
		border-color: var(--border-gold);
	}

	.map-panel__img {
		display: block;
		width: 100%;
		height: auto;
		max-height: 520px;
		object-fit: contain;
		transition: transform 0.2s;
	}

	.map-panel__img-wrap:hover .map-panel__img {
		transform: scale(1.01);
	}

	.map-panel__zoom-badge {
		position: absolute;
		bottom: 0.6rem;
		right: 0.6rem;
		width: 28px;
		height: 28px;
		background: rgba(0,0,0,0.55);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		display: flex;
		align-items: center;
		justify-content: center;
		opacity: 0;
		transition: opacity 0.15s;
		pointer-events: none;
	}

	.map-panel__zoom-badge svg {
		width: 14px;
		height: 14px;
		color: var(--gold-light);
	}

	.map-panel__img-wrap:hover .map-panel__zoom-badge {
		opacity: 1;
	}

	.map-panel__desc {
		font-size: 0.85rem;
		color: var(--text-muted);
		line-height: 1.65;
		padding: 0.65rem 0.85rem;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-left: 2px solid var(--gold-dim);
		border-radius: var(--radius);
	}

	/* Prev / next navigation */
	.map-nav {
		display: flex;
		align-items: center;
		gap: 0.5rem;
	}

	.map-nav__btn {
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.07em;
		color: var(--text-dim);
		background: transparent;
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 0.3rem 0.75rem;
		cursor: pointer;
		transition: color 0.13s, border-color 0.13s, background 0.13s;
		white-space: nowrap;
	}

	.map-nav__btn:hover {
		color: var(--gold-light);
		border-color: var(--border-gold);
		background: var(--bg-raised);
	}

	.map-nav__btn--right { margin-left: auto; }

	.map-nav__count {
		font-family: var(--font-mono);
		font-size: 0.7rem;
		color: var(--text-dim);
		flex: 1;
		text-align: center;
	}

	/* ── Lightbox ─────────────────────────────────────────────── */
	.lightbox {
		position: fixed;
		inset: 0;
		z-index: 500;
		background: rgba(0,0,0,0.88);
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 1rem;
		backdrop-filter: blur(4px);
	}

	.lightbox__inner {
		display: flex;
		flex-direction: column;
		gap: 0.6rem;
		max-width: min(95vw, 1100px);
		width: 100%;
	}

	.lightbox__toolbar {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 1rem;
	}

	.lightbox__label {
		font-family: var(--font-heading);
		font-size: 0.8rem;
		letter-spacing: 0.08em;
		color: var(--gold-light);
		text-transform: uppercase;
	}

	.lightbox__close {
		font-size: 1rem;
		color: var(--text-muted);
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		width: 28px;
		height: 28px;
		cursor: pointer;
		display: flex;
		align-items: center;
		justify-content: center;
		flex-shrink: 0;
		transition: color 0.13s, border-color 0.13s;
	}

	.lightbox__close:hover {
		color: var(--text-bright);
		border-color: var(--border-gold);
	}

	.lightbox__img {
		display: block;
		width: 100%;
		height: auto;
		max-height: 80vh;
		object-fit: contain;
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
	}

	.lightbox__nav {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		justify-content: center;
	}

	.lightbox__nav-btn {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.07em;
		color: var(--text-muted);
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.35rem 1rem;
		cursor: pointer;
		transition: color 0.13s, border-color 0.13s;
	}

	.lightbox__nav-btn:hover {
		color: var(--gold-light);
		border-color: var(--border-gold);
	}

	.lightbox__nav-count {
		font-family: var(--font-mono);
		font-size: 0.72rem;
		color: var(--text-dim);
		min-width: 48px;
		text-align: center;
	}
</style>
