<script lang="ts">
	import { onMount } from 'svelte';
	import { noticeApi } from '$lib/api/serverApi';

	// Tag colour mapping
	const tagColour: Record<string, string> = {
		Update: 'steel',
		Event:  'green',
		Patch:  'gold',
		Notice: 'red',
		Hotfix: 'red',
	};

	function detectTag(subject: string): string {
		const s = subject.toLowerCase();
		if (s.includes('hotfix')) return 'Hotfix';
		if (s.includes('patch'))  return 'Patch';
		if (s.includes('event'))  return 'Event';
		if (s.includes('update')) return 'Update';
		return 'Notice';
	}

	function formatDate(iso: string): string {
		return iso.slice(0, 10);
	}

	type Post = { id: number; date: string; tag: string; title: string; body: string };

	let posts: Post[] = [];
	let loading = true;
	let error = '';

	onMount(async () => {
		try {
			const notices = await noticeApi.getAll();
			posts = notices.map(n => ({
				id:    n.id,
				date:  formatDate(n.editDate),
				tag:   detectTag(n.subject),
				title: n.subject,
				body:  n.article,
			}));
		} catch {
			error = 'Could not load news. Please try again later.';
		} finally {
			loading = false;
		}
	});

	let selectedId: number | null = null;

	$: selected = selectedId !== null
		? posts.find(p => p.id === selectedId) ?? null
		: null;

	function open(id: number)  { selectedId = id; }
	function close()           { selectedId = null; }
</script>

<div class="page-shell">
	<!-- ── Page header ──────────────────────────────────────── -->
	<div class="page-hero">
		<div class="container">
			<h1 class="page-title">News &amp; Announcements</h1>
			<p class="page-sub">Stay up to date with server updates and events</p>
		</div>
	</div>

	<div class="container page-body">
		{#if selected}
			<!-- ── Full post view ─────────────────────────── -->
			<button class="back-btn" on:click={close}>← Back to all posts</button>

			<article class="post-full">
				<div class="post-full__meta">
					<span class="tag tag--{tagColour[selected.tag] ?? 'gold'}">{selected.tag}</span>
					<time class="post-date">{selected.date}</time>
				</div>
				<h2 class="post-full__title">{selected.title}</h2>
				<div class="post-full__divider" />
				<div class="post-body">
					{#each selected.body.split('\n') as line}
						{#if line.trim() === ''}
							<br />
						{:else}
							<p>{line}</p>
						{/if}
					{/each}
				</div>
			</article>

		{:else if loading}
			<p class="state-msg">Loading...</p>

		{:else if error}
			<p class="state-msg state-msg--error">{error}</p>

		{:else if posts.length === 0}
			<p class="state-msg">No announcements yet.</p>

		{:else}
			<!-- ── Post list ──────────────────────────────── -->
			<div class="post-list">
				{#each posts as post}
					<button class="post-card" on:click={() => open(post.id)}>
						<div class="post-card__meta">
							<span class="tag tag--{tagColour[post.tag] ?? 'gold'}">{post.tag}</span>
							<time class="post-date">{post.date}</time>
						</div>
						<h3 class="post-card__title">{post.title}</h3>
						<p class="post-card__excerpt">
							{post.body.split('\n').find(l => l.trim()) ?? ''}
						</p>
						<span class="post-card__read">Read more →</span>
					</button>
				{/each}
			</div>
		{/if}
	</div>
</div>

<style>
	.container {
		max-width: 860px;
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
	}

	/* ── Tags ────────────────────────────────────────────────── */
	.tag {
		display: inline-block;
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		padding: 0.15rem 0.45rem;
		border-radius: 2px;
		border: 1px solid;
	}

	.tag--gold  { color: var(--gold);         border-color: var(--gold-dim);    background: var(--bg-raised); }
	.tag--green { color: var(--green-bright);  border-color: var(--green);       background: var(--green-dark); }
	.tag--red   { color: var(--red-bright);    border-color: var(--red);         background: var(--red-dark); }
	.tag--steel { color: var(--steel-bright);  border-color: var(--steel-light); background: var(--steel-dark); }

	/* ── Post date ───────────────────────────────────────────── */
	.post-date {
		font-family: var(--font-mono);
		font-size: 0.75rem;
		color: var(--text-dim);
	}

	/* ── Post list ───────────────────────────────────────────── */
	.post-list {
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.post-card {
		width: 100%;
		text-align: left;
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.2rem 1.4rem;
		cursor: pointer;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
		transition: border-color 0.15s, background 0.15s;
	}

	.post-card:hover {
		border-color: var(--border-gold);
		background: var(--bg-raised);
	}

	.post-card__meta {
		display: flex;
		align-items: center;
		gap: 0.6rem;
	}

	.post-card__title {
		font-family: var(--font-heading);
		font-size: 1rem;
		color: var(--text-bright);
		letter-spacing: 0.04em;
	}

	.post-card__excerpt {
		font-size: 0.88rem;
		color: var(--text-muted);
		line-height: 1.5;
		/* clamp to 2 lines */
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}

	.post-card__read {
		font-size: 0.78rem;
		color: var(--gold-dim);
		align-self: flex-start;
	}

	/* ── Back button ─────────────────────────────────────────── */
	.back-btn {
		background: none;
		border: none;
		color: var(--text-muted);
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.06em;
		cursor: pointer;
		padding: 0;
		margin-bottom: 1.5rem;
		display: block;
		transition: color 0.15s;
	}

	.back-btn:hover { color: var(--gold); }

	/* ── Full post ───────────────────────────────────────────── */
	.post-full {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.75rem 2rem;
	}

	.post-full__meta {
		display: flex;
		align-items: center;
		gap: 0.7rem;
		margin-bottom: 0.8rem;
	}

	.post-full__title {
		font-family: var(--font-heading);
		font-size: 1.4rem;
		color: var(--gold-light);
		letter-spacing: 0.06em;
		margin-bottom: 0.6rem;
	}

	.post-full__divider {
		border-top: 1px solid var(--border-gold);
		margin-bottom: 1.25rem;
	}

	.post-body {
		font-size: 0.92rem;
		color: var(--text-base);
		line-height: 1.75;
	}

	.post-body p {
		margin: 0;
	}

	/* ── State messages ──────────────────────────────────────── */
	.state-msg {
		font-size: 0.88rem;
		color: var(--text-muted);
		text-align: center;
		padding: 3rem 0;
	}

	.state-msg--error {
		color: var(--red-bright);
	}
</style>
