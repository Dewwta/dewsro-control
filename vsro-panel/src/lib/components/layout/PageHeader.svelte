<script lang="ts">
	export let title: string;
	export let subtitle: string = '';
</script>

<header class="page-header">
	<div class="page-header__text">
		<h1 class="page-header__title">{title}</h1>
		{#if subtitle}
			<p class="page-header__subtitle">{subtitle}</p>
		{/if}
	</div>

	<div class="page-header__actions">
		<slot name="actions" />
	</div>
</header>

<style>
	.page-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 1rem;
		padding: 1.1rem 1.5rem;
		background: var(--bg-surface);
		border-bottom: 1px solid var(--border-gold);
		flex-shrink: 0;
	}

	.page-header__title {
		font-size: 1rem;
		font-weight: 600;
		letter-spacing: 0.12em;
		color: var(--gold-light);
		text-transform: uppercase;
		line-height: 1;
	}

	.page-header__subtitle {
		font-size: 0.78rem;
		color: var(--text-muted);
		margin-top: 4px;
		letter-spacing: 0.03em;
	}

	.page-header__actions {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		flex-shrink: 0;
	}
</style>
