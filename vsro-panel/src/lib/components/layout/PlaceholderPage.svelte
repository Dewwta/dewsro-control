<script lang="ts">
	export let title: string;
	export let description: string = 'This section is reserved for future content.';
</script>

<div class="placeholder">
	<div class="placeholder__emblem">⚔</div>
	<h2 class="placeholder__title">{title}</h2>
	<p class="placeholder__desc">{description}</p>
	<span class="placeholder__tag">Coming Soon</span>
</div>

<style>
	.placeholder {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		flex: 1;
		padding: 5rem 2rem;
		text-align: center;
		min-height: 50vh;
	}

	.placeholder__emblem {
		font-size: 2.8rem;
		color: var(--gold-dim);
		opacity: 0.5;
		margin-bottom: 1.1rem;
		line-height: 1;
	}

	.placeholder__title {
		font-size: 1.15rem;
		color: var(--text-muted);
		letter-spacing: 0.12em;
		margin-bottom: 0.65rem;
	}

	.placeholder__desc {
		font-size: 0.88rem;
		color: var(--text-dim);
		max-width: 380px;
		line-height: 1.7;
		margin-bottom: 1.25rem;
	}

	.placeholder__tag {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.18em;
		color: var(--text-dim);
		border: 1px solid var(--border-dark);
		padding: 0.3rem 0.85rem;
		border-radius: 999px;
	}
</style>
