<script lang="ts">
	import Sidebar from '$lib/components/layout/Sidebar.svelte';
	import { statusStore } from '$lib/stores/serverStatus';
	import { onMount } from 'svelte';

	onMount(() => {
		// Apply overflow:hidden to html/body via JS so it is guaranteed to be
		// removed when this layout is destroyed (navigating to the public site).
		// Using :global(html,body){overflow:hidden} in CSS causes it to persist
		// across the panel→public transition, locking scroll on the public site.
		document.documentElement.style.overflow = 'hidden';
		document.body.style.overflow = 'hidden';

		statusStore.startPolling(3000);

		return () => {
			document.documentElement.style.overflow = '';
			document.body.style.overflow = '';
			statusStore.stopPolling();
		};
	});
</script>

<div class="shell">
	<Sidebar />
	<div class="content-wrap">
		<slot />
	</div>
</div>

<style>
	.shell {
		display: flex;
		height: 100vh;
		overflow: hidden;
		background: var(--bg-base);
	}

	.content-wrap {
		flex: 1;
		display: flex;
		flex-direction: column;
		overflow-y: auto;
		min-width: 0;
	}
</style>
