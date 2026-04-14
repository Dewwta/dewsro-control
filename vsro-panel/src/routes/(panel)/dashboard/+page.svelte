<script lang="ts">
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import StatCard from '$lib/components/ui/StatCard.svelte';
	import ModuleCard from '$lib/components/ui/ModuleCard.svelte';
	import StatusBadge from '$lib/components/ui/StatusBadge.svelte';
	import { statusStore } from '$lib/stores/serverStatus';

	$: state  = $statusStore;
	$: status = state.data;

	$: moduleCount  = status?.modulesOpened ?? 0;
	$: totalModules = status?.moduleStatuses?.length ?? 0;
	$: stage        = status?.startupStage ?? '';
	$: isStarting   = stage !== '' && stage !== 'Running' && stage !== 'Error';

	function fmtTime(d: Date | null) {
		if (!d) return '—';
		return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
	}

	$: pctOperational = totalModules > 0
		? Math.round((moduleCount / totalModules) * 100)
		: 0;
</script>

<PageHeader title="Dashboard" subtitle="Live server status overview">
	<svelte:fragment slot="actions">
		{#if status}
			<StatusBadge
				running={status.isRunning}
				label={isStarting ? 'Starting…' : status.isRunning ? 'Online' : 'Offline'}
				size="md"
			/>
		{/if}
		<span class="updated">
			{state.lastUpdated ? `Updated ${fmtTime(state.lastUpdated)}` : 'Connecting…'}
		</span>
	</svelte:fragment>
</PageHeader>

<div class="page">

	<!-- API error banner -->
	{#if state.error}
		<div class="alert alert--error">
			<strong>API Unreachable</strong> — {state.error}
		</div>
	{/if}

	<!-- Startup stage bar -->
	{#if isStarting}
		<div class="stage-bar">
			<span class="stage-bar__label">Stage</span>
			<span class="stage-bar__value">{stage}</span>
			<span class="stage-bar__spinner"></span>
		</div>
	{/if}

	<!-- ── Stat row ── -->
	<div class="stats-row">
		<StatCard
			label="Server Status"
			value={status?.isRunning ? 'Online' : 'Offline'}
			variant={status?.isRunning ? 'green' : 'red'}
		/>
		<StatCard
			label="Modules Running"
			value="{moduleCount} / {totalModules}"
			sublabel="{pctOperational}% operational"
			variant={moduleCount === totalModules && totalModules > 0 ? 'green' : moduleCount > 0 ? 'gold' : 'red'}
		/>
		<StatCard
			label="Startup Stage"
			value={stage || (status ? 'Idle' : '—')}
			variant={isStarting ? 'gold' : 'default'}
		/>
	</div>

	<!-- ── Module grid ── -->
	<section class="section">
		<h2 class="section__title">Module Status</h2>

		{#if status?.moduleStatuses?.length}
			<div class="module-grid">
				{#each status.moduleStatuses as mod (mod.name)}
					<ModuleCard module={mod} />
				{/each}
			</div>
		{:else if !state.error}
			<div class="empty">
				<span class="empty__text">Waiting for server data…</span>
			</div>
		{/if}
	</section>

</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1.4rem;
	}

	/* ── Alert ── */
	.alert {
		padding: 0.7rem 1rem;
		border-radius: var(--radius);
		border: 1px solid;
		font-size: 0.84rem;
	}
	.alert--error {
		background: rgba(92, 16, 16, 0.22);
		border-color: var(--red-dark);
		color: var(--red-light);
	}
	.alert--error strong { font-family: var(--font-heading); letter-spacing: 0.04em; }

	/* ── Stage bar ── */
	.stage-bar {
		display: flex;
		align-items: center;
		gap: 0.7rem;
		padding: 0.55rem 0.9rem;
		background: rgba(139, 94, 28, 0.1);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
		font-size: 0.82rem;
	}
	.stage-bar__label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: var(--text-muted);
	}
	.stage-bar__value {
		font-family: var(--font-heading);
		color: var(--gold-light);
		letter-spacing: 0.05em;
		flex: 1;
	}
	.stage-bar__spinner {
		width: 12px; height: 12px;
		border: 2px solid var(--gold-dim);
		border-top-color: var(--gold);
		border-radius: 50%;
		animation: spin 0.8s linear infinite;
	}
	@keyframes spin { to { transform: rotate(360deg); } }

	/* ── Stats ── */
	.stats-row {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
		gap: 1rem;
	}

	/* ── Modules ── */
	.section__title {
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.14em;
		color: var(--text-muted);
		margin-bottom: 0.7rem;
		padding-bottom: 0.45rem;
		border-bottom: 1px solid var(--border-dark);
		font-family: var(--font-heading);
	}

	.module-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(155px, 1fr));
		gap: 0.7rem;
	}

	/* ── Misc ── */
	.empty {
		padding: 2.5rem;
		text-align: center;
	}
	.empty__text {
		font-size: 0.84rem;
		color: var(--text-dim);
		font-style: italic;
	}

	.updated {
		font-size: 0.65rem;
		color: var(--text-dim);
		letter-spacing: 0.04em;
	}
</style>
