<script lang="ts">
	export let running: boolean;
	export let label: string = '';
	export let size: 'sm' | 'md' | 'lg' = 'md';
</script>

<span class="badge badge--{size}" class:badge--on={running} class:badge--off={!running}>
	<span class="badge__dot"></span>
	<span class="badge__label">{label || (running ? 'Online' : 'Offline')}</span>
</span>

<style>
	.badge {
		display: inline-flex;
		align-items: center;
		gap: 0.4em;
		font-family: var(--font-heading);
		letter-spacing: 0.08em;
		border-radius: 999px;
		padding: 0.22em 0.65em;
		border: 1px solid;
		white-space: nowrap;
	}

	.badge--sm { font-size: 0.62rem; }
	.badge--md { font-size: 0.74rem; }
	.badge--lg { font-size: 0.9rem; }

	.badge__dot {
		width: 6px;
		height: 6px;
		border-radius: 50%;
		flex-shrink: 0;
	}

	/* Online */
	.badge--on {
		color: var(--green-bright);
		border-color: var(--green);
		background: rgba(21, 45, 12, 0.55);
	}
	.badge--on .badge__dot {
		background: var(--green-bright);
		box-shadow: 0 0 5px var(--green-bright);
	}

	/* Offline */
	.badge--off {
		color: var(--red-light);
		border-color: var(--red-dark);
		background: rgba(92, 16, 16, 0.3);
	}
	.badge--off .badge__dot {
		background: var(--red-light);
	}
</style>
