import { writable } from 'svelte/store';
import { browser } from '$app/environment';
import { serverApi, type VSROServerStatus } from '$lib/api/serverApi';

export interface StatusState {
	data: VSROServerStatus | null;
	loading: boolean;
	error: string | null;
	lastUpdated: Date | null;
}

function createStatusStore() {
	const { subscribe, set, update } = writable<StatusState>({
		data: null,
		loading: false,
		error: null,
		lastUpdated: null
	});

	let timer: ReturnType<typeof setInterval> | null = null;

	async function refresh() {
		try {
			const data = await serverApi.getStatus();
			set({ data, loading: false, error: null, lastUpdated: new Date() });
		} catch (e) {
			update((s) => ({
				...s,
				loading: false,
				error: e instanceof Error ? e.message : String(e),
				lastUpdated: new Date()
			}));
		}
	}

	function startPolling(intervalMs = 3000) {
		if (!browser) return;
		refresh();
		if (timer) clearInterval(timer);
		timer = setInterval(refresh, intervalMs);
	}

	function stopPolling() {
		if (timer) {
			clearInterval(timer);
			timer = null;
		}
	}

	return { subscribe, refresh, startPolling, stopPolling };
}

export const statusStore = createStatusStore();
