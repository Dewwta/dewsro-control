<script lang="ts">
	export let label: string;
	export let value: string | number;
	export let sublabel: string = '';
	export let variant: 'default' | 'gold' | 'green' | 'red' = 'default';
</script>

<div class="stat-card stat-card--{variant}">
	<div class="stat-card__label">{label}</div>
	<div class="stat-card__value">{value}</div>
	{#if sublabel}
		<div class="stat-card__sub">{sublabel}</div>
	{/if}
</div>

<style>
	.stat-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-top: 2px solid var(--border-gold);
		border-radius: var(--radius);
		padding: 1rem 1.2rem;
	}

	.stat-card--gold  { border-top-color: var(--gold); }
	.stat-card--green { border-top-color: var(--green-bright); }
	.stat-card--red   { border-top-color: var(--red-light); }

	.stat-card__label {
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.12em;
		color: var(--text-muted);
		font-family: var(--font-heading);
		margin-bottom: 0.45rem;
	}

	.stat-card__value {
		font-family: var(--font-heading);
		font-size: 1.65rem;
		font-weight: 600;
		color: var(--text-bright);
		line-height: 1;
	}

	.stat-card--green .stat-card__value { color: var(--green-bright); }
	.stat-card--red   .stat-card__value { color: var(--red-light); }
	.stat-card--gold  .stat-card__value { color: var(--gold-light); }

	.stat-card__sub {
		font-size: 0.7rem;
		color: var(--text-muted);
		margin-top: 0.35rem;
	}
</style>
