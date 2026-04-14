<script lang="ts">
	export let variant: 'primary' | 'danger' | 'secondary' = 'primary';
	export let disabled = false;
	export let loading = false;
	export let type: 'button' | 'submit' | 'reset' = 'button';
	export let size: 'sm' | 'md' | 'lg' = 'md';
</script>

<button
	{type}
	disabled={disabled || loading}
	class="sr-btn sr-btn--{variant} sr-btn--{size}"
	class:sr-btn--loading={loading}
	on:click
>
	{#if loading}
		<span class="sr-btn__spinner" aria-hidden="true"></span>
	{/if}
	<slot />
</button>

<style>
	.sr-btn {
		display: inline-flex;
		align-items: center;
		justify-content: center;
		gap: 0.45rem;
		font-family: var(--font-heading);
		letter-spacing: 0.1em;
		text-transform: uppercase;
		border: 1px solid;
		border-radius: var(--radius);
		cursor: pointer;
		transition: background 0.15s, color 0.15s, border-color 0.15s;
		white-space: nowrap;
		flex-shrink: 0;
	}

	/* Sizes */
	.sr-btn--sm { font-size: 0.68rem; padding: 0.4rem 0.9rem; }
	.sr-btn--md { font-size: 0.78rem; padding: 0.55rem 1.3rem; }
	.sr-btn--lg { font-size: 0.88rem; padding: 0.7rem 1.8rem; }

	.sr-btn:disabled {
		opacity: 0.32;
		cursor: not-allowed;
	}

	/* ── Primary (gold) ─── */
	.sr-btn--primary {
		background: rgba(139, 94, 28, 0.22);
		border-color: var(--gold-dim);
		color: var(--gold-light);
	}
	.sr-btn--primary:hover:not(:disabled) {
		background: rgba(139, 94, 28, 0.42);
		border-color: var(--gold);
		color: var(--gold-bright);
	}
	.sr-btn--primary:active:not(:disabled) {
		background: rgba(139, 94, 28, 0.58);
	}

	/* ── Danger (red) ─── */
	.sr-btn--danger {
		background: rgba(92, 16, 16, 0.22);
		border-color: var(--red-dark);
		color: var(--red-light);
	}
	.sr-btn--danger:hover:not(:disabled) {
		background: rgba(140, 32, 32, 0.42);
		border-color: var(--red);
		color: var(--red-bright);
	}
	.sr-btn--danger:active:not(:disabled) {
		background: rgba(140, 32, 32, 0.58);
	}

	/* ── Secondary ─── */
	.sr-btn--secondary {
		background: transparent;
		border-color: var(--border-mid);
		color: var(--text-muted);
	}
	.sr-btn--secondary:hover:not(:disabled) {
		background: var(--bg-raised);
		border-color: var(--border-gold);
		color: var(--text-base);
	}

	/* ── Spinner ─── */
	.sr-btn__spinner {
		width: 11px;
		height: 11px;
		border: 2px solid currentColor;
		border-top-color: transparent;
		border-radius: 50%;
		animation: spin 0.7s linear infinite;
	}

	@keyframes spin {
		to { transform: rotate(360deg); }
	}
</style>
