<script lang="ts">
	import type { ModuleStatus } from '$lib/api/serverApi';

	export let module: ModuleStatus;

	function formatBytes(bytes: number): string {
		if (bytes <= 0) return '—';
		const mb = bytes / 1_048_576;
		if (mb >= 1) return `${mb.toFixed(1)} MB`;
		return `${(bytes / 1024).toFixed(1)} KB`;
	}

	function formatUptime(startTime: string | null): string {
		if (!startTime) return '—';
		const diff = Date.now() - new Date(startTime).getTime();
		const h = Math.floor(diff / 3_600_000);
		const m = Math.floor((diff % 3_600_000) / 60_000);
		if (h > 0) return `${h}h ${m}m`;
		return `${m}m`;
	}
</script>

<div
	class="card"
	class:card--running={module.isRunning}
	class:card--stopped={!module.isRunning}
>
	<div class="card__header">
		<span class="card__dot"></span>
		<span class="card__name">{module.name}</span>
	</div>

	{#if module.isRunning}
		<dl class="card__stats">
			<div class="card__row">
				<dt class="card__key">PID</dt>
				<dd class="card__val card__val--mono">{module.processId ?? '—'}</dd>
			</div>
			<div class="card__row">
				<dt class="card__key">Memory</dt>
				<dd class="card__val">{formatBytes(module.memoryBytes)}</dd>
			</div>
			<div class="card__row">
				<dt class="card__key">Uptime</dt>
				<dd class="card__val">{formatUptime(module.startTime)}</dd>
			</div>
		</dl>
	{:else}
		<p class="card__stopped">Stopped</p>
	{/if}
</div>

<style>
	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-left: 3px solid transparent;
		border-radius: var(--radius);
		padding: 0.8rem 0.9rem;
	}

	.card--running {
		border-left-color: var(--green-bright);
	}

	.card--stopped {
		border-left-color: var(--red-dark);
		opacity: 0.6;
	}

	/* ── Header ─── */
	.card__header {
		display: flex;
		align-items: center;
		gap: 0.45rem;
		margin-bottom: 0.55rem;
	}

	.card__dot {
		width: 7px;
		height: 7px;
		border-radius: 50%;
		flex-shrink: 0;
	}

	.card--running .card__dot {
		background: var(--green-bright);
		box-shadow: 0 0 5px var(--green-bright);
	}

	.card--stopped .card__dot {
		background: var(--red-light);
	}

	.card__name {
		font-family: var(--font-heading);
		font-size: 0.74rem;
		letter-spacing: 0.05em;
		color: var(--text-bright);
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	.card--stopped .card__name {
		color: var(--text-muted);
	}

	/* ── Stats ─── */
	.card__stats {
		display: flex;
		flex-direction: column;
		gap: 0.22rem;
	}

	.card__row {
		display: flex;
		justify-content: space-between;
		align-items: baseline;
	}

	.card__key {
		font-size: 0.62rem;
		text-transform: uppercase;
		letter-spacing: 0.07em;
		color: var(--text-muted);
	}

	.card__val {
		font-size: 0.72rem;
		color: var(--text-base);
	}

	.card__val--mono {
		font-family: var(--font-mono);
		font-size: 0.67rem;
	}

	.card__stopped {
		font-size: 0.7rem;
		color: var(--text-dim);
		font-style: italic;
		letter-spacing: 0.04em;
	}
</style>
