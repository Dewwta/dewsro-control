<script lang="ts">
    import { onMount } from "svelte";
    import { getApiBase } from "$lib/config";
    import { authStore } from "$lib/stores/auth";
    import PageHeader from "$lib/components/layout/PageHeader.svelte";

    interface ClientEntry {
        version: string;
        fileName: string;
        uploadedAt: string;
        sizeBytes: number;
        downloadUrl: string;
    }

    // ── State ─────────────────────────────────────────────────
    let clients: ClientEntry[] = [];
    let loadError = "";

    // Upload form
    let version = "";
    let fileInput: HTMLInputElement;
    let selectedFile: File | null = null;

    // Upload progress
    let uploading = false;
    let uploadPct = 0;
    let uploadMsg = "";
    let uploadError = "";

    // Delete
    let deleting: string | null = null;

    // ── Load list ─────────────────────────────────────────────
    onMount(loadClients);

    async function loadClients() {
        loadError = "";
        try {
            const res = await fetch(`${getApiBase()}/client/list`, {
                headers: { Authorization: `Bearer ${authStore.getToken()}` },
            });
            if (!res.ok) throw new Error(await res.text());
            clients = await res.json();
        } catch (e: unknown) {
            loadError =
                e instanceof Error ? e.message : "Failed to load clients.";
        }
    }

    // ── Upload ────────────────────────────────────────────────
    function onFileChange(e: Event) {
        const input = e.target as HTMLInputElement;
        selectedFile = input.files?.[0] ?? null;
    }

    function upload() {
        if (!selectedFile) {
            uploadError = "Select a file first.";
            return;
        }
        if (!version.trim()) {
            uploadError = "Enter a version string.";
            return;
        }

        uploadError = "";
        uploadMsg = "";
        uploading = true;
        uploadPct = 0;

        const form = new FormData();
        form.append("file", selectedFile);
        form.append("version", version.trim());

        const xhr = new XMLHttpRequest();

        xhr.upload.onprogress = (e) => {
            if (e.lengthComputable)
                uploadPct = Math.round((e.loaded / e.total) * 100);
        };

        xhr.onload = async () => {
            uploading = false;

            console.log("STATUS:", xhr.status);
            console.log("RESPONSE:", xhr.responseText);

            if (xhr.status >= 200 && xhr.status < 300) {
                const body = JSON.parse(xhr.responseText);
                uploadMsg = body.message ?? "Upload complete.";
                version = "";
                selectedFile = null;
                if (fileInput) fileInput.value = "";
                await loadClients();
            } else {
                try {
                    uploadError =
                        JSON.parse(xhr.responseText)?.message ?? xhr.statusText;
                } catch (_) {
                    uploadError = xhr.responseText || xhr.statusText;
                }
            }
        };

        xhr.onerror = () => {
            uploading = false;
            uploadError = "Network error during upload.";
        };

        xhr.open("POST", `${getApiBase()}/client/upload`);
        xhr.setRequestHeader("Authorization", `Bearer ${authStore.getToken()}`);
        xhr.send(form);
    }

    // ── Delete ────────────────────────────────────────────────
    async function deleteClient(fileName: string) {
        deleting = fileName;
        try {
            const res = await fetch(
                `${getApiBase()}/client/${encodeURIComponent(fileName)}`,
                {
                    method: "DELETE",
                    headers: {
                        Authorization: `Bearer ${authStore.getToken()}`,
                    },
                },
            );
            if (!res.ok) throw new Error(await res.text());
            await loadClients();
        } catch (e: unknown) {
            loadError = e instanceof Error ? e.message : "Delete failed.";
        } finally {
            deleting = null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────
    function fmtSize(bytes: number): string {
        if (bytes >= 1_073_741_824)
            return (bytes / 1_073_741_824).toFixed(2) + " GB";
        if (bytes >= 1_048_576) return (bytes / 1_048_576).toFixed(1) + " MB";
        return (bytes / 1024).toFixed(0) + " KB";
    }

    function fmtDate(iso: string): string {
        return new Date(iso).toLocaleString();
    }

    function copyUrl(url: string) {
        navigator.clipboard.writeText(url);
    }
</script>

<PageHeader
    title="Client Management"
    subtitle="Upload and manage game client downloads"
/>

<div class="page-content">
    <!-- ── Upload card ──────────────────────────────────────── -->
    <section class="card">
        <h2 class="card-title">Upload New Client</h2>

        {#if uploadMsg}
            <div class="banner banner--success">{uploadMsg}</div>
        {/if}
        {#if uploadError}<div class="banner banner--error">
                {uploadError}
            </div>{/if}

        <div class="upload-form">
            <label class="field">
                <span
                    >Version string <span class="muted">(e.g. 1.188)</span
                    ></span
                >
                <input
                    type="text"
                    bind:value={version}
                    placeholder="1.188"
                    disabled={uploading}
                    maxlength={20}
                />
            </label>

            <label class="field">
                <span>Client zip file</span>
                <input
                    type="file"
                    accept=".zip"
                    bind:this={fileInput}
                    on:change={onFileChange}
                    disabled={uploading}
                    class="file-input"
                />
            </label>

            {#if selectedFile}
                <p class="file-info">
                    {selectedFile.name} — {fmtSize(selectedFile.size)}
                </p>
            {/if}

            {#if uploading}
                <div class="progress-wrap">
                    <div class="progress-bar" style="width: {uploadPct}%"></div>
                </div>
                <p class="progress-label">{uploadPct}% uploaded…</p>
            {/if}

            <button
                class="btn btn--primary"
                on:click={upload}
                disabled={uploading || !selectedFile}
            >
                {uploading ? `Uploading… ${uploadPct}%` : "Upload Client"}
            </button>
        </div>

        <p class="hint">
            Files are stored as <code>VSRO_Client_v&lt;version&gt;.zip</code>.
            Only the 5 most recent clients are kept — the oldest is deleted
            automatically when a 6th is uploaded.
        </p>
    </section>

    <!-- ── Stored clients ───────────────────────────────────── -->
    <section class="card">
        <h2 class="card-title">Stored Clients</h2>

        {#if loadError}
            <div class="banner banner--error">{loadError}</div>
        {/if}

        {#if clients.length === 0}
            <p class="empty">No clients uploaded yet.</p>
        {:else}
            <div class="client-list">
                {#each clients as c, i}
                    <div class="client-row" class:client-row--latest={i === 0}>
                        <div class="client-row__info">
                            <span class="client-ver">v{c.version}</span>
                            {#if i === 0}<span class="latest-badge">latest</span
                                >{/if}
                            <span class="client-meta"
                                >{fmtSize(c.sizeBytes)} · {fmtDate(
                                    c.uploadedAt,
                                )}</span
                            >
                        </div>
                        <div class="client-row__actions">
                            <button
                                class="btn btn--sm btn--ghost"
                                on:click={() => copyUrl(c.downloadUrl)}
                                title="Copy download URL"
                            >
                                Copy URL
                            </button>
                            <a
                                class="btn btn--sm btn--ghost"
                                href={c.downloadUrl}
                                target="_blank"
                                rel="noreferrer"
                            >
                                Download
                            </a>
                            <button
                                class="btn btn--sm btn--danger"
                                on:click={() => deleteClient(c.fileName)}
                                disabled={deleting === c.fileName}
                            >
                                {deleting === c.fileName ? "…" : "Delete"}
                            </button>
                        </div>
                    </div>
                {/each}
            </div>

            <p class="hint">{clients.length} / 5 slots used</p>
        {/if}
    </section>
</div>

<style>
    .page-content {
        padding: 1.5rem;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        max-width: 860px;
    }

    /* ── Cards ───────────────────────────────────────────────── */
    .card {
        background: var(--bg-surface);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
        padding: 1.4rem 1.6rem;
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .card-title {
        font-family: var(--font-heading);
        font-size: 0.88rem;
        color: var(--gold-light);
        letter-spacing: 0.1em;
        padding-bottom: 0.6rem;
        border-bottom: 1px solid var(--border-dark);
    }

    /* ── Banners ─────────────────────────────────────────────── */
    .banner {
        padding: 0.55rem 0.8rem;
        border-radius: 2px;
        font-size: 0.84rem;
    }
    .banner--success {
        background: var(--green-dark);
        border: 1px solid var(--green);
        color: var(--text-bright);
    }
    .banner--error {
        background: var(--red-dark);
        border: 1px solid var(--red);
        color: var(--text-bright);
    }

    /* ── Upload form ─────────────────────────────────────────── */
    .upload-form {
        display: flex;
        flex-direction: column;
        gap: 0.85rem;
    }

    .field {
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
        font-size: 0.83rem;
        color: var(--text-muted);
        font-family: var(--font-body);
    }

    .field input[type="text"] {
        background: var(--bg-raised);
        border: 1px solid var(--border-mid);
        border-radius: 2px;
        color: var(--text-bright);
        font-family: var(--font-mono);
        font-size: 0.9rem;
        padding: 0.4rem 0.6rem;
        outline: none;
        max-width: 200px;
        transition: border-color 0.15s;
    }

    .field input[type="text"]:focus {
        border-color: var(--border-accent);
    }

    .file-input {
        font-size: 0.83rem;
        color: var(--text-base);
    }

    .file-info {
        font-family: var(--font-mono);
        font-size: 0.8rem;
        color: var(--text-muted);
        margin-top: -0.3rem;
    }

    /* ── Progress ────────────────────────────────────────────── */
    .progress-wrap {
        height: 6px;
        background: var(--bg-raised);
        border-radius: 3px;
        overflow: hidden;
    }

    .progress-bar {
        height: 100%;
        background: var(--gold);
        border-radius: 3px;
        transition: width 0.2s;
    }

    .progress-label {
        font-family: var(--font-mono);
        font-size: 0.78rem;
        color: var(--text-muted);
        margin-top: -0.4rem;
    }

    /* ── Buttons ─────────────────────────────────────────────── */
    .btn {
        font-family: var(--font-heading);
        font-size: 0.78rem;
        letter-spacing: 0.07em;
        padding: 0.5rem 1.1rem;
        border-radius: var(--radius);
        border: 1px solid transparent;
        cursor: pointer;
        transition: background 0.15s;
        text-decoration: none;
        display: inline-flex;
        align-items: center;
    }

    .btn--primary {
        background: var(--gold-dim);
        border-color: var(--border-gold);
        color: var(--text-bright);
        align-self: flex-start;
    }
    .btn--primary:hover:not(:disabled) {
        background: var(--gold);
    }

    .btn--sm {
        padding: 0.3rem 0.7rem;
        font-size: 0.72rem;
    }

    .btn--ghost {
        background: transparent;
        border-color: var(--border-mid);
        color: var(--text-muted);
    }
    .btn--ghost:hover {
        background: var(--bg-raised);
        color: var(--text-base);
    }

    .btn--danger {
        background: var(--red-dark);
        border-color: var(--red);
        color: var(--text-bright);
    }
    .btn--danger:hover:not(:disabled) {
        background: var(--red);
    }

    .btn:disabled {
        opacity: 0.45;
        cursor: not-allowed;
    }

    /* ── Client list ─────────────────────────────────────────── */
    .client-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .client-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        padding: 0.75rem 0.9rem;
        background: var(--bg-raised);
        border: 1px solid var(--border-dark);
        border-radius: var(--radius);
        flex-wrap: wrap;
    }

    .client-row--latest {
        border-color: var(--border-gold);
    }

    .client-row__info {
        display: flex;
        align-items: center;
        gap: 0.6rem;
        flex-wrap: wrap;
    }

    .client-ver {
        font-family: var(--font-mono);
        font-size: 0.9rem;
        color: var(--text-bright);
    }

    .latest-badge {
        font-family: var(--font-heading);
        font-size: 0.6rem;
        letter-spacing: 0.1em;
        text-transform: uppercase;
        color: var(--gold);
        background: var(--bg-surface);
        border: 1px solid var(--border-gold);
        padding: 0.1rem 0.4rem;
        border-radius: 2px;
    }

    .client-meta {
        font-size: 0.78rem;
        color: var(--text-muted);
        font-family: var(--font-mono);
    }

    .client-row__actions {
        display: flex;
        gap: 0.4rem;
    }

    /* ── Misc ────────────────────────────────────────────────── */
    .empty {
        font-size: 0.88rem;
        color: var(--text-muted);
        font-style: italic;
    }
    .hint {
        font-size: 0.78rem;
        color: var(--text-dim);
    }
    .muted {
        color: var(--text-dim);
    }

    code {
        font-family: var(--font-mono);
        font-size: 0.82rem;
        background: var(--bg-raised);
        padding: 0.1em 0.3em;
        border-radius: 2px;
    }
</style>
