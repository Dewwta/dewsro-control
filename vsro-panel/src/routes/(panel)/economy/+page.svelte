<script lang="ts">
	import PageHeader from '$lib/components/layout/PageHeader.svelte';
	import SrButton from '$lib/components/ui/SrButton.svelte';
	import { economyApi, type ItemDurabilityEntry, type RewardType, type TalismanGroup } from '$lib/api/serverApi';

	// ── Shared helpers ────────────────────────────────────────────────────────
	function mkState() {
		return { loading: false, msg: '', ok: true };
	}

	// ── Item Durability Lookup ────────────────────────────────────────────────
	let durCode    = '';
	let durState   = mkState();
	let durResults: ItemDurabilityEntry[] = [];

	async function handleDurLookup() {
		if (!durCode.trim()) return;
		durState = { loading: true, msg: '', ok: true };
		durResults = [];
		try {
			durResults = await economyApi.getItemDurability(durCode.trim());
			durState = { loading: false, msg: durResults.length ? '' : 'No items found.', ok: true };
		} catch (e: any) {
			durState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Max Stack ─────────────────────────────────────────────────────────────
	let stackCode  = '';
	let stackSize  = 200;
	let stackState = mkState();

	async function handleSetStack() {
		if (!stackCode.trim()) return;
		stackState = { loading: true, msg: '', ok: true };
		try {
			const r = await economyApi.setItemMaxStack(stackCode.trim(), stackSize);
			stackState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			stackState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Add Item to Shop ──────────────────────────────────────────────────────
	let shopItemId   = 0;
	let shopPrice    = 0;
	let shopTab      = '';
	let shopData     = 0;
	let shopCurrency = 0;
	let shopState    = mkState();

	async function handleAddToShop() {
		if (!shopTab.trim() || shopItemId <= 0) return;
		shopState = { loading: true, msg: '', ok: true };
		try {
			const r = await economyApi.addItemToShop(shopItemId, shopPrice, shopTab.trim(), shopData, shopCurrency);
			shopState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			shopState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Add Item to Monster Drop ──────────────────────────────────────────────
	let dropMobId    = 0;
	let dropItemId   = 0;
	let dropRatio    = 0.1;
	let dropState    = mkState();

	async function handleAddMonsterDrop() {
		if (dropMobId <= 0 || dropItemId <= 0) return;
		dropState = { loading: true, msg: '', ok: true };
		try {
			const r = await economyApi.addMonsterDrop(dropMobId, dropItemId, dropRatio);
			dropState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			dropState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Quest Rewards ─────────────────────────────────────────────────────────
	const rewardTypes: { label: string; value: RewardType }[] = [
		{ label: 'Experience (EXP)',   value: 'Experience' },
		{ label: 'Skill EXP (SP-EXP)', value: 'SkillExperience' },
		{ label: 'Gold',               value: 'Gold' },
		{ label: 'Skill Points (SP)',  value: 'SkillPoints' }
	];

	let questType: RewardType = 'Experience';
	let questMult  = 1.5;
	let questMin   = 1;
	let questMax   = 100;
	let questState = mkState();

	async function handleQuestRewards() {
		questState = { loading: true, msg: '', ok: true };
		try {
			const r = await economyApi.editQuestRewards(questType, questMult, questMin, questMax);
			questState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			questState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Alchemy Rates ─────────────────────────────────────────────────────────
	// 12 boxes: +1–+4 → Param2, +5–+8 → Param3, +9–+12 → Param4
	// Each value is a % chance (0–100). Encoded the same way as the original
	// WinForms tool: 4 bytes packed into one signed 32-bit integer via hex concat.
	// Box[0] of each group is clamped to 127 (keeps the MSB clear → stays positive).
	let alcBoxes: number[] = Array(12).fill(100);
	let alcState = mkState();

	function encValues(boxes: number[]): number {
		let hex = '';
		for (let i = 0; i < 4; i++) {
			let v = Math.floor(boxes[i]) || 0;
			v = Math.max(0, i === 0 ? Math.min(127, v) : Math.min(255, v));
			hex += v.toString(16).padStart(2, '0');
		}
		return parseInt(hex, 16);
	}

	$: alcParam2 = encValues(alcBoxes.slice(0, 4));
	$: alcParam3 = encValues(alcBoxes.slice(4, 8));
	$: alcParam4 = encValues(alcBoxes.slice(8, 12));

	async function handleAlchemy() {
		alcState = { loading: true, msg: '', ok: true };
		try {
			const r = await economyApi.setAlchemyRates(alcParam2, alcParam3, alcParam4);
			alcState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			alcState = { loading: false, msg: e.message, ok: false };
		}
	}

	// ── Talisman / FW Drop Rates ──────────────────────────────────────────────
	const talismanGroups: { label: string; value: TalismanGroup }[] = [
		{ label: 'Togui Village',  value: 'ToguiVillage' },
		{ label: 'Flame Mountain', value: 'FlameMountain' },
		{ label: 'Wreck A',        value: 'WreckA' },
		{ label: 'Wreck B',        value: 'WreckB' }
	];

	let talisGroup: TalismanGroup = 'ToguiVillage';
	let talisRatio     = 0.12;
	let talisAmountMin = 1;
	let talisAmountMax = 1;
	let talisAll       = false;
	let talisState     = mkState();

	function clampRatio(e: Event) {
		const input = e.target as HTMLInputElement;
		let v = parseFloat(input.value);
		if (isNaN(v) || v < 0) v = 0;
		if (v > 1) v = 1;
		talisRatio = Math.round(v * 10000) / 10000;
	}

	async function handleTalisman() {
		talisState = { loading: true, msg: '', ok: true };
		try {
			const min = Math.max(0, Math.floor(talisAmountMin));
			const max = Math.max(min, Math.floor(talisAmountMax));
			const r = await economyApi.setTalismanDropRates(talisGroup, talisRatio, talisAll, min, max);
			talisState = { loading: false, msg: r.message, ok: true };
		} catch (e: any) {
			talisState = { loading: false, msg: e.message, ok: false };
		}
	}
</script>

<div class="page">
	<PageHeader title="Economy" subtitle="Shop, drop rates, quest rewards, and item configuration" />

	<!-- ── Row 1: Item lookup tools ─────────────────────────────────────── -->
	<div class="section-label">Item Tools</div>
	<div class="card-grid">

		<!-- Durability lookup -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Item Durability Lookup</h2>
				<span class="card__sub">Search items by code name prefix</span>
			</div>
			<div class="card__body">
				<div class="inline-form">
					<input class="sr-input" type="text" placeholder="e.g. ITEM_CH_SWORD%" bind:value={durCode}
						on:keydown={e => e.key === 'Enter' && handleDurLookup()} />
					<SrButton size="sm" loading={durState.loading} on:click={handleDurLookup}>Search</SrButton>
				</div>
				{#if durState.msg}
					<p class="field-msg" class:field-msg--error={!durState.ok}>{durState.msg}</p>
				{/if}
				{#if durResults.length}
					<div class="result-table">
						<div class="result-head">
							<span>Code Name</span><span>Durability</span><span>Max Stack</span><span>ID</span>
						</div>
						{#each durResults as row}
							<div class="result-row">
								<code class="result-code">{row.codeName}</code>
								<span class="result-val">{row.durability}</span>
								<span class="result-val">{row.maxStack}</span>
								<span class="result-dim">{row.id}</span>
							</div>
						{/each}
					</div>
				{/if}
			</div>
		</div>

		<!-- Max stack -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Item Max Stack</h2>
				<span class="card__sub">Change how many a player can carry in one slot</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Item Code Name</label>
					<input class="sr-input" type="text" placeholder="e.g. ITEM_ETC_RECOVERY_SMALL01" bind:value={stackCode} />
				</div>
				<div class="field-group">
					<label class="field-label">New Max Stack</label>
					<input class="sr-input" type="number" min="1" max="9999" bind:value={stackSize} />
				</div>
			</div>
			<div class="card__footer">
				{#if stackState.msg}
					<p class="inline-msg" class:inline-msg--ok={stackState.ok} class:inline-msg--err={!stackState.ok}>{stackState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={!stackCode.trim()} loading={stackState.loading} on:click={handleSetStack}>
					Set Stack Size
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Row 2: Shop + Monster drops ──────────────────────────────────── -->
	<div class="section-label">Drops &amp; Shop</div>
	<div class="card-grid">

		<!-- Add to shop -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Add Item to Shop</h2>
				<span class="card__sub">Creates package entries and adds to a shop tab</span>
			</div>
			<div class="card__body">
				<div class="field-row">
					<div class="field-group">
						<label class="field-label">Item ID</label>
						<input class="sr-input" type="number" min="1" bind:value={shopItemId} />
					</div>
					<div class="field-group">
						<label class="field-label">Price</label>
						<input class="sr-input" type="number" min="0" bind:value={shopPrice} />
					</div>
				</div>
				<div class="field-group">
					<label class="field-label">Shop Tab Code Name</label>
					<input class="sr-input" type="text" placeholder="e.g. STORE_TAB_POTION" bind:value={shopTab} />
				</div>
				<div class="field-row">
					<div class="field-group">
						<label class="field-label">Data (Durability)</label>
						<input class="sr-input" type="number" min="0" bind:value={shopData} />
					</div>
					<div class="field-group">
						<label class="field-label">Currency</label>
						<select class="sr-input" bind:value={shopCurrency}>
							<option value={0}>Gold</option>
							<option value={1}>Silk</option>
						</select>
					</div>
				</div>
			</div>
			<div class="card__footer">
				{#if shopState.msg}
					<p class="inline-msg" class:inline-msg--ok={shopState.ok} class:inline-msg--err={!shopState.ok}>{shopState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={!shopTab.trim() || shopItemId <= 0} loading={shopState.loading} on:click={handleAddToShop}>
					Add to Shop
				</SrButton>
			</div>
		</div>

		<!-- Monster drop -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Add Monster Drop</h2>
				<span class="card__sub">Assign a specific item to a monster's drop table</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Monster ID</label>
					<input class="sr-input" type="number" min="1" bind:value={dropMobId} />
				</div>
				<div class="field-group">
					<label class="field-label">Item ID</label>
					<input class="sr-input" type="number" min="1" bind:value={dropItemId} />
				</div>
				<div class="field-group">
					<label class="field-label">Drop Ratio <span class="field-hint">(e.g. 0.1 = 10%)</span></label>
					<input class="sr-input" type="number" step="0.01" min="0" max="1" bind:value={dropRatio} />
				</div>
			</div>
			<div class="card__footer">
				{#if dropState.msg}
					<p class="inline-msg" class:inline-msg--ok={dropState.ok} class:inline-msg--err={!dropState.ok}>{dropState.msg}</p>
				{/if}
				<SrButton variant="primary" disabled={dropMobId <= 0 || dropItemId <= 0} loading={dropState.loading} on:click={handleAddMonsterDrop}>
					Add Drop
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Row 3: Quest + Talisman ─────────────────────────────────────── -->
	<div class="section-label">Rates &amp; Rewards</div>
	<div class="card-grid">

		<!-- Quest rewards -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">Quest Rewards</h2>
				<span class="card__sub">Multiply a reward type across a level range</span>
			</div>
			<div class="card__body">
				<div class="field-group">
					<label class="field-label">Reward Type</label>
					<select class="sr-input" bind:value={questType}>
						{#each rewardTypes as rt}
							<option value={rt.value}>{rt.label}</option>
						{/each}
					</select>
				</div>
				<div class="field-group">
					<label class="field-label">Multiplier <span class="field-hint">(e.g. 1.5 = +50%)</span></label>
					<input class="sr-input" type="number" step="0.1" min="0.1" bind:value={questMult} />
				</div>
				<div class="field-row">
					<div class="field-group">
						<label class="field-label">Min Level</label>
						<input class="sr-input" type="number" min="1" max="130" bind:value={questMin} />
					</div>
					<div class="field-group">
						<label class="field-label">Max Level</label>
						<input class="sr-input" type="number" min="1" max="130" bind:value={questMax} />
					</div>
				</div>
			</div>
			<div class="card__footer">
				{#if questState.msg}
					<p class="inline-msg" class:inline-msg--ok={questState.ok} class:inline-msg--err={!questState.ok}>{questState.msg}</p>
				{/if}
				<SrButton variant="primary" loading={questState.loading} on:click={handleQuestRewards}>
					Apply Multiplier
				</SrButton>
			</div>
		</div>

		<!-- Talisman / FW drop rates -->
		<div class="card">
			<div class="card__header">
				<h2 class="card__title">FW Talisman Drops</h2>
				<span class="card__sub">Forgotten World talisman drop ratio and amounts</span>
			</div>
			<div class="card__body">
				<label class="checkbox-row">
					<input type="checkbox" bind:checked={talisAll} />
					<span class="checkbox-row__label">Affect all groups</span>
				</label>
				{#if !talisAll}
					<div class="field-group">
						<label class="field-label">Group</label>
						<select class="sr-input" bind:value={talisGroup}>
							{#each talismanGroups as g}
								<option value={g.value}>{g.label}</option>
							{/each}
						</select>
					</div>
				{/if}
				<div class="field-group">
					<label class="field-label">Drop Ratio <span class="field-hint">(0.01 – 1.0)</span></label>
					<input class="sr-input" type="number" step="0.01" min="0" max="1"
						bind:value={talisRatio} on:change={clampRatio} />
				</div>
				<div class="field-row">
					<div class="field-group">
						<label class="field-label">Drop Min</label>
						<input class="sr-input" type="number" min="0" step="1" bind:value={talisAmountMin} />
					</div>
					<div class="field-group">
						<label class="field-label">Drop Max</label>
						<input class="sr-input" type="number" min="0" step="1" bind:value={talisAmountMax} />
					</div>
				</div>
			</div>
			<div class="card__footer">
				{#if talisState.msg}
					<p class="inline-msg" class:inline-msg--ok={talisState.ok} class:inline-msg--err={!talisState.ok}>{talisState.msg}</p>
				{/if}
				<SrButton variant="primary" loading={talisState.loading} on:click={handleTalisman}>
					Set Drop Rate
				</SrButton>
			</div>
		</div>

	</div>

	<!-- ── Row 4: Alchemy Rates (full width) ────────────────────────────── -->
	<div class="section-label">Alchemy Rates</div>
	<div class="card">
		<div class="card__header">
			<h2 class="card__title">Stone Upgrade Success Rates</h2>
			<span class="card__sub">Enter a % chance (0–100) per enhancement level. Param values are computed automatically.</span>
		</div>
		<div class="card__body">
			<div class="alc-groups">
				{#each [0, 1, 2] as g}
					<div class="alc-group">
						<div class="alc-group__label">
							+{g * 4 + 1} – +{g * 4 + 4}
							<span class="field-hint">&nbsp;→ Param{g + 2}</span>
						</div>
						<div class="alc-boxes">
							{#each [0, 1, 2, 3] as b}
								<div class="field-group">
									<label class="field-label">+{g * 4 + b + 1}</label>
									<input
										class="sr-input"
										type="number"
										min="0"
										max={b === 0 ? 100 : 100}
										bind:value={alcBoxes[g * 4 + b]}
									/>
								</div>
							{/each}
						</div>
					</div>
				{/each}
			</div>
			<div class="alc-params">
				<div class="field-group">
					<label class="field-label">Param2 <span class="field-hint">(encoded, read-only)</span></label>
					<input class="sr-input sr-input--readonly" type="text" readonly value={alcParam2} />
				</div>
				<div class="field-group">
					<label class="field-label">Param3 <span class="field-hint">(encoded, read-only)</span></label>
					<input class="sr-input sr-input--readonly" type="text" readonly value={alcParam3} />
				</div>
				<div class="field-group">
					<label class="field-label">Param4 <span class="field-hint">(encoded, read-only)</span></label>
					<input class="sr-input sr-input--readonly" type="text" readonly value={alcParam4} />
				</div>
			</div>
		</div>
		<div class="card__footer">
			{#if alcState.msg}
				<p class="inline-msg" class:inline-msg--ok={alcState.ok} class:inline-msg--err={!alcState.ok}>{alcState.msg}</p>
			{/if}
			<SrButton variant="primary" loading={alcState.loading} on:click={handleAlchemy}>
				Set Rates
			</SrButton>
		</div>
	</div>

</div>

<style>
	.page {
		padding: 2rem;
		max-width: 1080px;
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.section-label {
		font-family: var(--font-heading);
		font-size: 0.7rem;
		letter-spacing: 0.12em;
		text-transform: uppercase;
		color: var(--text-dim);
		border-bottom: 1px solid var(--border-dark);
		padding-bottom: 0.35rem;
		margin-top: 0.5rem;
	}

	.card-grid {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
	}

	.card-grid--three {
		grid-template-columns: repeat(3, 1fr);
	}

	@media (max-width: 860px) {
		.card-grid--three { grid-template-columns: 1fr 1fr; }
	}
	@media (max-width: 580px) {
		.card-grid, .card-grid--three { grid-template-columns: 1fr; }
	}

	.card {
		background: var(--bg-surface);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		display: flex;
		flex-direction: column;
	}

	.card__header {
		padding: 0.8rem 1rem 0.55rem;
		border-bottom: 1px solid var(--border-dark);
	}

	.card__title {
		font-family: var(--font-heading);
		font-size: 0.85rem;
		color: var(--text-bright);
		letter-spacing: 0.07em;
		text-transform: uppercase;
		margin-bottom: 0.15rem;
	}

	.card__sub {
		font-size: 0.74rem;
		color: var(--text-muted);
	}

	.card__body {
		flex: 1;
		padding: 0.85rem 1rem;
		display: flex;
		flex-direction: column;
		gap: 0.65rem;
	}

	.card__footer {
		padding: 0.65rem 1rem 0.85rem;
		border-top: 1px solid var(--border-dark);
		display: flex;
		flex-direction: column;
		gap: 0.45rem;
	}

	/* ── Inputs ─── */
	.sr-input {
		width: 100%;
		background: var(--bg-raised);
		border: 1px solid var(--border-mid);
		border-radius: var(--radius);
		padding: 0.4rem 0.65rem;
		color: var(--text-base);
		font-family: var(--font-body);
		font-size: 0.88rem;
		outline: none;
		transition: border-color 0.15s;
	}
	.sr-input:focus { border-color: var(--border-accent); }

	select.sr-input { cursor: pointer; }

	.field-group {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
	}

	.field-row {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 0.5rem;
	}

	.field-label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.field-hint {
		font-family: var(--font-body);
		font-size: 0.72rem;
		color: var(--text-dim);
		text-transform: none;
		letter-spacing: 0;
	}

	.field-msg {
		font-size: 0.78rem;
		color: var(--text-muted);
	}

	.field-msg--error { color: var(--red-bright); }

	/* ── Checkbox ─── */
	.checkbox-row {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		cursor: pointer;
	}

	.checkbox-row input[type="checkbox"] {
		accent-color: var(--gold);
		width: 14px;
		height: 14px;
	}

	.checkbox-row__label {
		font-size: 0.84rem;
		color: var(--text-base);
	}

	/* ── Inline form ─── */
	.inline-form {
		display: flex;
		gap: 0.5rem;
		align-items: center;
	}

	.inline-form .sr-input { flex: 1; }

	/* ── Inline messages ─── */
	.inline-msg {
		font-size: 0.78rem;
		padding: 0.35rem 0.55rem;
		border-radius: var(--radius);
		border: 1px solid;
	}

	.inline-msg--ok {
		border-color: var(--green-light);
		background: rgba(39, 96, 24, 0.12);
		color: var(--green-bright);
	}

	.inline-msg--err {
		border-color: var(--red-light);
		background: rgba(92, 16, 16, 0.18);
		color: var(--red-bright);
	}

	/* ── Durability result table ─── */
	.result-table {
		border: 1px solid var(--border-dark);
		border-radius: var(--radius);
		overflow: hidden;
		font-size: 0.76rem;
		max-height: 180px;
		overflow-y: auto;
	}

	.result-head {
		display: grid;
		grid-template-columns: 1fr 80px 80px 60px;
		background: var(--bg-raised);
		padding: 0.35rem 0.6rem;
		font-family: var(--font-heading);
		font-size: 0.62rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		color: var(--text-muted);
		border-bottom: 1px solid var(--border-dark);
	}

	.result-row {
		display: grid;
		grid-template-columns: 1fr 80px 80px 60px;
		padding: 0.3rem 0.6rem;
		background: var(--bg-surface);
	}

	.result-row:nth-child(even) { background: var(--bg-raised); }

	.result-code {
		font-family: var(--font-mono);
		color: var(--steel-bright);
		font-size: 0.72rem;
	}

	.result-val {
		color: var(--gold);
		font-family: var(--font-mono);
		font-size: 0.72rem;
	}

	.result-dim {
		color: var(--text-dim);
		font-family: var(--font-mono);
		font-size: 0.72rem;
	}

	/* ── Alchemy ─── */
	.alc-groups {
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	.alc-group__label {
		font-family: var(--font-heading);
		font-size: 0.65rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		color: var(--text-muted);
		margin-bottom: 0.3rem;
	}

	.alc-boxes {
		display: grid;
		grid-template-columns: repeat(4, 1fr);
		gap: 0.5rem;
	}

	.alc-params {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 0.5rem;
		margin-top: 0.75rem;
		padding-top: 0.75rem;
		border-top: 1px solid var(--border-dark);
	}

	.sr-input--readonly {
		opacity: 0.6;
		cursor: default;
		font-family: var(--font-mono);
		font-size: 0.82rem;
		color: var(--gold);
	}
</style>
