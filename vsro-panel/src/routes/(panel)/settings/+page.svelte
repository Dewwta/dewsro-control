<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { serverApi, type StartupSettings } from '$lib/api/serverApi';

	let settings: StartupSettings | null = null;
	let loadError = '';
	let loading   = true;
	let saving    = false;
	let saveMsg   = '';
	let saveError = '';

	onMount(async () => {
		try {
			settings = await serverApi.getSettings();
		} catch (e) {
			loadError = e instanceof Error ? e.message : String(e);
		} finally {
			loading = false;
		}
	});

	async function handleSave() {
		if (!settings) return;
		saving    = true;
		saveMsg   = '';
		saveError = '';
		try {
			await serverApi.updateSettings(settings);
			saveMsg = 'Settings saved successfully.';
		} catch (e) {
			saveError = e instanceof Error ? e.message : String(e);
		} finally {
			saving = false;
		}
	}

	// Grouped path fields for clean rendering
	const questPathFields: { key: keyof StartupSettings; label: string; hint: string }[] = [
		{ key: 'questLuaRootPath',        label: 'Quest Lua Root',              hint: 'Folder containing VSRO_LUA_1_188/ and make_quest.bat' },
		{ key: 'questSctTempPath',         label: 'Quest SCT Staging Path',      hint: 'Folder where compiled Quest.sct is held until server start' },
		{ key: 'questSctDestinationPath',  label: 'Quest SCT Destination',       hint: 'Folder on the game server where Quest.sct is deployed on startup' },
		{ key: 'questTextdataReferencePath', label: 'Quest Textdata Reference File', hint: 'Full path to textquest_speech&name.txt — this file is edited on each quest save and served by the download button' },
		{ key: 'questTextdataOutputPath',       label: 'Quest Textdata Output Path',        hint: 'Optional — folder where the updated textdata file is also copied after each save (e.g. game client folder)' },
		{ key: 'questTextdataUpdateFolderPath', label: 'Quest Textdata Auto-Update Folder', hint: 'Optional — folder where textquest_speech&name.txt is automatically synced after each quest save (e.g. SMC patcher update folder)' }
	];

	const exeFields: { key: keyof StartupSettings; label: string }[] = [
		{ key: 'globalManagerPath',  label: 'Global Manager' },
		{ key: 'downloadServerPath', label: 'Download Server' },
		{ key: 'machineManagerPath', label: 'Machine Manager' },
		{ key: 'gatewayServerPath',  label: 'Gateway Server' },
		{ key: 'farmManagerPath',    label: 'Farm Manager' },
		{ key: 'agentServerPath',    label: 'Agent Server' },
		{ key: 'shardManagerPath',   label: 'Shard Manager' },
		{ key: 'gameServerPath',     label: 'Game Server' },
		{ key: 'proxyPath',          label: 'VSRO Proxy' },
		{ key: 'smcPath',            label: 'SMC (smc_independent.exe)' },
		{ key: 'nodeTypeIniPath',    label: 'srNodeType.ini' }
	];
</script>

<PageHeader title="Settings" subtitle="Module paths and startup configuration" />

<div class="page">
	{#if loading}
		<p class="state-text">Loading settings…</p>

	{:else if loadError}
		<div class="msg msg--error">{loadError}</div>

	{:else if settings}
		<form on:submit|preventDefault={handleSave} class="form">

			<!-- Quest Paths -->
			<section class="form-section">
				<h2 class="form-section__title">Quest Editor Paths</h2>
				{#each questPathFields as f}
					<div class="field">
						<label class="field__label" for={f.key}>{f.label}</label>
						<input
							id={f.key}
							class="field__input"
							type="text"
							bind:value={settings[f.key]}
							placeholder={f.hint}
						/>
						<span class="field__hint">{f.hint}</span>
					</div>
				{/each}
			</section>

			<!-- Module Paths -->
			<section class="form-section">
				<h2 class="form-section__title">Module Paths</h2>
				{#each exeFields as f}
					<div class="field">
						<label class="field__label" for={f.key}>{f.label}</label>
						<input
							id={f.key}
							class="field__input"
							type="text"
							bind:value={settings[f.key]}
							placeholder="Full path to executable or file…"
						/>
					</div>
				{/each}
			</section>

			<!-- SMC Credentials -->
			<section class="form-section">
				<h2 class="form-section__title">SMC Credentials</h2>
				<div class="field field--half">
					<label class="field__label" for="smcUsername">Username</label>
					<input id="smcUsername" class="field__input" type="text" bind:value={settings.smcUsername} />
				</div>
				<div class="field field--half">
					<label class="field__label" for="smcPassword">Password</label>
					<input id="smcPassword" class="field__input" type="password" bind:value={settings.smcPassword} />
				</div>
				<div class="field field--half">
					<label class="field__label" for="smcTitle">SMC Window Title</label>
					<input id="smcTitle" class="field__input" type="text" bind:value={settings.smcMainWindowTitle} />
				</div>
			</section>

			<!-- Network -->
			<section class="form-section">
				<h2 class="form-section__title">Network</h2>
				<label class="checkbox-row">
					<input
						type="checkbox"
						checked={settings.shouldResolvePubIP ?? false}
						on:change={(e) => {
							if (settings) settings.shouldResolvePubIP = e.currentTarget.checked;
						}}
					/>
					<span class="checkbox-row__label">
						Resolve and patch public IP into <code>srNodeType.ini</code> on startup
					</span>
				</label>
			</section>

			<!-- Footer -->
			<div class="form-footer">
				<SrButton type="submit" variant="primary" loading={saving} disabled={saving}>
					Save Settings
				</SrButton>
				{#if saveMsg}
					<span class="inline-msg inline-msg--ok">{saveMsg}</span>
				{/if}
				{#if saveError}
					<span class="inline-msg inline-msg--err">{saveError}</span>
				{/if}
			</div>

		</form>
	{/if}
</div>

<style>
	.page {
		padding: 1.4rem 1.5rem;
	}

	.state-text {
		font-size: 0.85rem;
		color: var(--text-muted);
		font-style: italic;
	}

	.msg {
		padding: 0.65rem 0.95rem;
		border-radius: var(--radius);
		border: 1px solid;
		font-size: 0.84rem;
	}
	.msg--error { background: rgba(92,16,16,0.25); border-color: var(--red-dark); color: var(--red-light); }

	/* ── Form layout ── */
	.form {
		display: flex;
		flex-direction: column;
		gap: 1.4rem;
		max-width: 680px;
	}

	.form-section {
		background: var(--bg-surface);
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		padding: 1.25rem 1.35rem;
		display: flex;
		flex-direction: column;
		gap: 0.8rem;
	}

	.form-section__title {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.13em;
		color: var(--text-muted);
		padding-bottom: 0.5rem;
		border-bottom: 1px solid var(--border-dark);
	}

	/* ── Fields ── */
	.field {
		display: flex;
		flex-direction: column;
		gap: 0.28rem;
	}

	.field--half { max-width: 320px; }

	.field__hint {
		font-size: 0.68rem;
		color: var(--text-dim);
		font-style: italic;
	}

	.field__label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.09em;
		color: var(--text-muted);
	}

	.field__input {
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		color: var(--text-base);
		border-radius: var(--radius);
		padding: 0.42rem 0.65rem;
		font-family: var(--font-mono);
		font-size: 0.76rem;
		width: 100%;
		transition: border-color 0.15s, color 0.15s;
	}
	.field__input:focus {
		outline: none;
		border-color: var(--border-accent);
		color: var(--text-bright);
	}
	.field__input::placeholder { color: var(--text-dim); }

	/* ── Checkbox ── */
	.checkbox-row {
		display: flex;
		align-items: flex-start;
		gap: 0.55rem;
		cursor: pointer;
	}
	.checkbox-row input[type="checkbox"] {
		accent-color: var(--gold);
		margin-top: 3px;
		flex-shrink: 0;
		cursor: pointer;
	}
	.checkbox-row__label {
		font-size: 0.84rem;
		color: var(--text-base);
		line-height: 1.5;
	}
	.checkbox-row__label code {
		font-family: var(--font-mono);
		font-size: 0.78em;
		color: var(--gold);
		background: var(--bg-raised);
		padding: 1px 4px;
		border-radius: 2px;
	}

	/* ── Footer ── */
	.form-footer {
		display: flex;
		align-items: center;
		gap: 1rem;
		flex-wrap: wrap;
	}

	.inline-msg {
		font-family: var(--font-heading);
		font-size: 0.78rem;
		letter-spacing: 0.04em;
	}
	.inline-msg--ok  { color: var(--green-bright); }
	.inline-msg--err { color: var(--red-light); }
</style>
