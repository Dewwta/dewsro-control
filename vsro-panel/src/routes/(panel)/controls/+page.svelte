<script lang="ts">
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import StatusBadge from '$lib/components/ui/StatusBadge.svelte';
	import { statusStore } from '$lib/stores/serverStatus';
	import { serverApi } from '$lib/api/serverApi';

	$: status     = $statusStore.data;
	$: isRunning  = status?.isRunning ?? false;
	$: stage      = status?.startupStage ?? '';
	$: isStarting = stage !== '' && stage !== 'Running' && stage !== 'Error';

	type MsgType = 'success' | 'error' | 'info';
	let message    = '';
	let msgType: MsgType = 'info';
	let startBusy  = false;
	let shutBusy   = false;
	let needConfirmStart = false;
	let needConfirm = false;
	let confirmTimer: ReturnType<typeof setTimeout> | null = null;
	let confirmStartTimer: ReturnType<typeof setTimeout> | null = null;

	// Restart state
	let gatewayBusy          = false;
	let shardGameBusy        = false;
	let needConfirmGateway   = false;
	let needConfirmShardGame = false;
	let confirmGatewayTimer:   ReturnType<typeof setTimeout> | null = null;
	let confirmShardGameTimer: ReturnType<typeof setTimeout> | null = null;

	async function handleStart() {
		if (!needConfirmStart) {
			needConfirmStart = true;
			message          = 'Click Start again to confirm server startup.';
			msgType          = 'info';
			if (confirmStartTimer) clearTimeout(confirmStartTimer);
			confirmStartTimer = setTimeout(() => {
				needConfirmStart = false;
				if (message === 'Click Start again to confirm server startup.') message = '';
			}, 5000);
			return;
		}

		needConfirmStart = false;
		if (confirmStartTimer) clearTimeout(confirmStartTimer);
		startBusy = true;
		message   = '';
		try {
			const res = await serverApi.startServer();
			message = res.message;
			msgType = 'success';
			statusStore.refresh();
		} catch (e) {
			message = e instanceof Error ? e.message : String(e);
			msgType = 'error';
		} finally {
			startBusy = false;
		}
	}

	async function handleRestartGateway() {
		if (!needConfirmGateway) {
			needConfirmGateway = true;
			message = 'Click again to confirm Gateway Server restart.';
			msgType = 'info';
			if (confirmGatewayTimer) clearTimeout(confirmGatewayTimer);
			confirmGatewayTimer = setTimeout(() => {
				needConfirmGateway = false;
				if (message === 'Click again to confirm Gateway Server restart.') message = '';
			}, 5000);
			return;
		}
		needConfirmGateway = false;
		if (confirmGatewayTimer) clearTimeout(confirmGatewayTimer);
		gatewayBusy = true;
		message = '';
		try {
			const res = await serverApi.restartGateway();
			message = res.message;
			msgType = 'success';
			statusStore.refresh();
		} catch (e) {
			message = e instanceof Error ? e.message : String(e);
			msgType = 'error';
		} finally {
			gatewayBusy = false;
		}
	}

	async function handleRestartShardGame() {
		if (!needConfirmShardGame) {
			needConfirmShardGame = true;
			message = 'Click again to confirm Shard Manager + Game Server restart.';
			msgType = 'info';
			if (confirmShardGameTimer) clearTimeout(confirmShardGameTimer);
			confirmShardGameTimer = setTimeout(() => {
				needConfirmShardGame = false;
				if (message === 'Click again to confirm Shard Manager + Game Server restart.') message = '';
			}, 5000);
			return;
		}
		needConfirmShardGame = false;
		if (confirmShardGameTimer) clearTimeout(confirmShardGameTimer);
		shardGameBusy = true;
		message = '';
		try {
			const res = await serverApi.restartShardGame();
			message = res.message;
			msgType = 'success';
			statusStore.refresh();
		} catch (e) {
			message = e instanceof Error ? e.message : String(e);
			msgType = 'error';
		} finally {
			shardGameBusy = false;
		}
	}

	// ── Proxy Controls ───────────────────────────────────────────────────────
	let proxyBusy = false;
	let proxyMessage = '';
	let proxyMsgType: MsgType = 'info';
	let needConfirmProxyStop = false;
	let confirmProxyStopTimer: ReturnType<typeof setTimeout> | null = null;

	$: isProxyRunning = status?.moduleStatuses?.find(m => m.name === 'Integrated Proxy')?.isRunning ?? false;

	async function handleProxyStart() {
		proxyBusy = true;
		proxyMessage = '';
		try {
			const res = await serverApi.startProxy();
			proxyMessage = res.message;
			proxyMsgType = 'success';
			statusStore.refresh();
		} catch (e) {
			proxyMessage = e instanceof Error ? e.message : String(e);
			proxyMsgType = 'error';
		} finally {
			proxyBusy = false;
		}
	}

	async function handleProxyStop() {
		if (!needConfirmProxyStop) {
			needConfirmProxyStop = true;
			proxyMessage = 'Click Stop again to confirm stopping the proxy.';
			proxyMsgType = 'info';
			if (confirmProxyStopTimer) clearTimeout(confirmProxyStopTimer);
			confirmProxyStopTimer = setTimeout(() => {
				needConfirmProxyStop = false;
				if (proxyMessage === 'Click Stop again to confirm stopping the proxy.') proxyMessage = '';
			}, 5000);
			return;
		}
		needConfirmProxyStop = false;
		if (confirmProxyStopTimer) clearTimeout(confirmProxyStopTimer);
		proxyBusy = true;
		proxyMessage = '';
		try {
			const res = await serverApi.stopProxy();
			proxyMessage = res.message;
			proxyMsgType = 'success';
			statusStore.refresh();
		} catch (e) {
			proxyMessage = e instanceof Error ? e.message : String(e);
			proxyMsgType = 'error';
		} finally {
			proxyBusy = false;
		}
	}

	async function handleProxyRestart() {
		proxyBusy = true;
		proxyMessage = '';
		try {
			const res = await serverApi.restartProxy();
			proxyMessage = res.message;
			proxyMsgType = 'success';
			statusStore.refresh();
		} catch (e) {
			proxyMessage = e instanceof Error ? e.message : String(e);
			proxyMsgType = 'error';
		} finally {
			proxyBusy = false;
		}
	}

	// ── Send Notice ──────────────────────────────────────────────────────────
	let noticeText    = '';
	let noticeBusy    = false;
	let noticeMessage = '';
	let noticeMsgType: MsgType = 'info';

	async function handleSendNotice() {
		if (!noticeText.trim()) return;
		noticeBusy = true;
		noticeMessage = '';
		try {
			const res = await serverApi.sendNotice(noticeText.trim());
			noticeMessage = res.message;
			noticeMsgType = 'success';
			noticeText = '';
		} catch (e) {
			noticeMessage = e instanceof Error ? e.message : String(e);
			noticeMsgType = 'error';
		} finally {
			noticeBusy = false;
		}
	}

	async function handleShutdown() {
		if (!needConfirm) {
			needConfirm = true;
			message     = 'Click Shutdown again to confirm.';
			msgType     = 'info';
			if (confirmTimer) clearTimeout(confirmTimer);
			confirmTimer = setTimeout(() => {
				needConfirm = false;
				if (message === 'Click Shutdown again to confirm.') message = '';
			}, 5000);
			return;
		}

		needConfirm = false;
		if (confirmTimer) clearTimeout(confirmTimer);
		shutBusy = true;
		message  = '';

		try {
			const res = await serverApi.shutdownServer();
			message = res.message;
			msgType = 'success';
			statusStore.refresh();
		} catch (e) {
			message = e instanceof Error ? e.message : String(e);
			msgType = 'error';
		} finally {
			shutBusy = false;
		}
	}
</script>

<PageHeader title="Server Controls" subtitle="Start and stop the VSRO module stack">
	<svelte:fragment slot="actions">
		{#if status}
			<StatusBadge
				running={isRunning}
				label={isStarting ? stage : isRunning ? 'Online' : 'Offline'}
				size="md"
			/>
		{/if}
	</svelte:fragment>
</PageHeader>

<div class="page">

	<!-- Startup progress -->
	{#if isStarting}
		<div class="stage-bar">
			<span class="stage-bar__label">Stage</span>
			<span class="stage-bar__value">{stage}</span>
			<span class="stage-bar__spinner"></span>
		</div>
	{/if}

	<!-- Main control card -->
	<div class="ctrl-card">
		<div class="ctrl-card__title">Server Power</div>
		<p class="ctrl-card__desc">
			Starting the server launches all modules in sequence and automates the SMC node activation.
			The process takes approximately 2 minutes — do not interact with the server machine during startup.
			Shutdown kills all tracked processes immediately.
		</p>

		<div class="ctrl-card__actions">
			<SrButton
				variant="primary"
				size="lg"
				disabled={isRunning || isStarting}
				loading={startBusy}
				on:click={handleStart}
			>
				{needConfirmStart ? '⚠ Confirm Start' : '▶ Start Server'}
			</SrButton>

			<SrButton
				variant="danger"
				size="lg"
				disabled={!isRunning && !isStarting}
				loading={shutBusy}
				on:click={handleShutdown}
			>
				{needConfirm ? '⚠ Confirm Shutdown' : '■ Shutdown'}
			</SrButton>
		</div>
	</div>

	<!-- Partial restart card -->
	{#if isRunning}
		<div class="ctrl-card">
			<div class="ctrl-card__title">Partial Restart</div>
			<p class="ctrl-card__desc">
				Restart individual server components without a full cycle.
				<strong>Gateway</strong> restarts instantly — use after notice/launcher changes.
				<strong>Shard + Game</strong> kills both, relaunches them, waits ~35s for full init, then re-activates all nodes via SMC automatically.
			</p>

			<div class="ctrl-card__actions">
				<SrButton
					variant="secondary"
					size="md"
					disabled={isStarting}
					loading={gatewayBusy}
					on:click={handleRestartGateway}
				>
					{needConfirmGateway ? '⚠ Confirm' : '↺ Restart Gateway'}
				</SrButton>

				<SrButton
					variant="secondary"
					size="md"
					disabled={isStarting}
					loading={shardGameBusy}
					on:click={handleRestartShardGame}
				>
					{needConfirmShardGame ? '⚠ Confirm' : '↺ Restart Shard + Game'}
				</SrButton>
			</div>
		</div>
	{/if}

	<!-- Proxy control card -->
	<div class="ctrl-card">
		<div class="ctrl-card__title">Integrated Proxy</div>
		<p class="ctrl-card__desc">
			Controls the built-in gateway, download, and agent proxies.
			Use <strong>Start</strong> to bring the proxy up independently of the full server stack,
			<strong>Stop</strong> to tear it down, or <strong>Restart</strong> to cycle it without touching any other modules.
		</p>

		<div class="proxy-status">
			<span class="module-row__dot" class:dot--on={isProxyRunning} class:dot--off={!isProxyRunning}></span>
			<span class="proxy-status__label">{isProxyRunning ? 'Proxy Running' : 'Proxy Stopped'}</span>
		</div>

		<div class="ctrl-card__actions">
			<SrButton
				variant="primary"
				size="md"
				disabled={isProxyRunning || proxyBusy}
				loading={proxyBusy}
				on:click={handleProxyStart}
			>
				▶ Start Proxy
			</SrButton>

			<SrButton
				variant="danger"
				size="md"
				disabled={!isProxyRunning || proxyBusy}
				loading={proxyBusy}
				on:click={handleProxyStop}
			>
				{needConfirmProxyStop ? '⚠ Confirm Stop' : '■ Stop Proxy'}
			</SrButton>

			<SrButton
				variant="secondary"
				size="md"
				disabled={proxyBusy}
				loading={proxyBusy}
				on:click={handleProxyRestart}
			>
				↺ Restart Proxy
			</SrButton>
		</div>

		{#if proxyMessage}
			<div class="msg msg--{proxyMsgType}" style="margin-top:0.6rem">{proxyMessage}</div>
		{/if}
	</div>

	<!-- Send Notice card -->
	<div class="ctrl-card">
		<div class="ctrl-card__title">Send In-Game Notice</div>
		<p class="ctrl-card__desc">
			Broadcasts a notice message to all currently connected players.
		</p>
		<div class="notice-form">
			<textarea
				class="notice-input"
				placeholder="Type your notice message..."
				bind:value={noticeText}
				rows="3"
				disabled={noticeBusy}
				on:keydown={e => e.key === 'Enter' && !e.shiftKey && (e.preventDefault(), handleSendNotice())}
			></textarea>
			<div class="notice-form__footer">
				{#if noticeMessage}
					<span class="notice-msg notice-msg--{noticeMsgType}">{noticeMessage}</span>
				{:else}
					<span></span>
				{/if}
				<SrButton
					variant="primary"
					size="sm"
					disabled={!noticeText.trim()}
					loading={noticeBusy}
					on:click={handleSendNotice}
				>
					Send Notice
				</SrButton>
			</div>
		</div>
	</div>

	<!-- Response message -->
	{#if message}
		<div class="msg msg--{msgType}">{message}</div>
	{/if}

	<!-- Compact module list -->
	{#if status?.moduleStatuses?.length}
		<div class="module-list-card">
			<div class="module-list-card__title">Module Summary</div>
			<div class="module-list">
				{#each status.moduleStatuses as mod (mod.name)}
					<div
						class="module-row"
						class:module-row--on={mod.isRunning}
						class:module-row--off={!mod.isRunning}
					>
						<span class="module-row__dot"></span>
						<span class="module-row__name">{mod.name}</span>
						{#if mod.isRunning && mod.processId}
							<span class="module-row__pid">PID {mod.processId}</span>
						{:else}
							<span class="module-row__status">Stopped</span>
						{/if}
					</div>
				{/each}
			</div>
		</div>
	{/if}

</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
		display: flex;
		flex-direction: column;
		gap: 1.25rem;
		max-width: 780px;
	}

	/* ── Stage bar ── */
	.stage-bar {
		display: flex;
		align-items: center;
		gap: 0.7rem;
		padding: 0.55rem 0.9rem;
		background: rgba(139, 94, 28, 0.1);
		border: 1px solid var(--border-gold);
		border-radius: var(--radius);
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
		font-size: 0.82rem;
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

	/* ── Control card ── */
	.ctrl-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-top: 2px solid var(--border-gold);
		border-radius: var(--radius);
		padding: 1.4rem 1.5rem;
	}

	.ctrl-card__title {
		font-family: var(--font-heading);
		font-size: 0.88rem;
		letter-spacing: 0.1em;
		text-transform: uppercase;
		color: var(--gold-light);
		margin-bottom: 0.6rem;
	}

	.ctrl-card__desc {
		font-size: 0.86rem;
		color: var(--text-muted);
		line-height: 1.7;
		margin-bottom: 1.3rem;
		max-width: 540px;
	}

	.ctrl-card__actions {
		display: flex;
		gap: 0.75rem;
		flex-wrap: wrap;
	}

	/* ── Notice form ── */
	.notice-form {
		display: flex;
		flex-direction: column;
		gap: 0.6rem;
	}

	.notice-input {
		width: 100%;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.5rem 0.75rem;
		color: var(--text-base);
		font-family: var(--font-body);
		font-size: 0.88rem;
		resize: vertical;
		outline: none;
		transition: border-color 0.15s;
		box-sizing: border-box;
	}
	.notice-input:focus { border-color: var(--border-accent); }
	.notice-input:disabled { opacity: 0.5; }

	.notice-form__footer {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 0.75rem;
	}

	.notice-msg {
		font-size: 0.78rem;
		font-family: var(--font-heading);
		letter-spacing: 0.03em;
	}
	.notice-msg--success { color: var(--green-bright); }
	.notice-msg--error   { color: var(--red-light); }
	.notice-msg--info    { color: var(--gold); }

	/* ── Message ── */
	.msg {
		padding: 0.65rem 0.95rem;
		border-radius: var(--radius);
		border: 1px solid;
		font-family: var(--font-heading);
		font-size: 0.8rem;
		letter-spacing: 0.04em;
	}
	.msg--success { background: rgba(21,45,12,0.4); border-color: var(--green); color: var(--green-bright); }
	.msg--error   { background: rgba(92,16,16,0.3); border-color: var(--red-dark); color: var(--red-light); }
	.msg--info    { background: rgba(122,90,37,0.15); border-color: var(--border-gold); color: var(--gold); }

	/* ── Module list ── */
	.module-list-card {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.1rem 1.25rem;
	}

	.module-list-card__title {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.12em;
		color: var(--text-muted);
		padding-bottom: 0.5rem;
		border-bottom: 1px solid var(--border-dark);
		margin-bottom: 0.7rem;
	}

	.module-list {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
		gap: 0.35rem;
	}

	.module-row {
		display: flex;
		align-items: center;
		gap: 0.4rem;
		font-size: 0.76rem;
		padding: 0.25rem 0;
	}

	.module-row__dot {
		width: 6px; height: 6px;
		border-radius: 50%;
		flex-shrink: 0;
	}
	.module-row--on .module-row__dot  { background: var(--green-bright); box-shadow: 0 0 4px var(--green-bright); }
	.module-row--off .module-row__dot { background: var(--red-light); }

	.module-row__name {
		color: var(--text-muted);
	}
	.module-row--on .module-row__name { color: var(--text-base); }

	.module-row__pid {
		font-family: var(--font-mono);
		font-size: 0.65rem;
		color: var(--text-dim);
	}

	.module-row__status {
		font-size: 0.65rem;
		color: var(--text-dim);
		font-style: italic;
	}

	/* ── Proxy status ── */
	.proxy-status {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		margin-bottom: 0.75rem;
		font-size: 0.78rem;
		color: var(--text-muted);
	}

	.proxy-status__label {
		font-family: var(--font-heading);
		font-size: 0.72rem;
		letter-spacing: 0.06em;
	}

	.dot--on  { background: var(--green-bright); box-shadow: 0 0 4px var(--green-bright); width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
	.dot--off { background: var(--red-light); width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
</style>
