import { getApiBase } from '$lib/config';
import { authStore } from '$lib/stores/auth';

// ── DTOs (mirror C# models) ──────────────────────────────────────────────────

export interface UserDTO {
	jid: number;
	username: string;
	nickname: string | null;
	email: string | null;
	sex: string | null;
	authority: number;
}

export interface LoginResponse {
	jwt: string;
	user: UserDTO;
}

export interface ModuleStatus {
	name: string;
	isRunning: boolean;
	processId: number | null;
	cpuUsage: number;
	memoryBytes: number;
	startTime: string | null;
	isResponsive: boolean;
}

export interface VSROServerStatus {
	isRunning: boolean;
	modulesOpened: number;
	moduleStatuses: ModuleStatus[];
	startupStage: string;
}

export interface StartupSettings {
	globalManagerPath:       string | null;
	downloadServerPath:      string | null;
	machineManagerPath:      string | null;
	gatewayServerPath:       string | null;
	farmManagerPath:         string | null;
	agentServerPath:         string | null;
	shardManagerPath:        string | null;
	gameServerPath:          string | null;
	proxyPath:               string | null;
	smcPath:                 string | null;
	nodeTypeIniPath:         string | null;
	smcUsername:             string | null;
	smcPassword:             string | null;
	smcMainWindowTitle:      string | null;
	shouldResolvePubIP:      boolean | null;
	questLuaRootPath:         string | null;
	questSctTempPath:         string | null;
	questSctDestinationPath:  string | null;
	questTextdataReferencePath:    string | null;
	questTextdataOutputPath:       string | null;
	questTextdataUpdateFolderPath: string | null;
}

// ── Players ──────────────────────────────────────────────────────────────────

// ── Live sessions ────────────────────────────────────────────────────────────

export interface LiveParty {
	partyId: number;
	message: string | null;
	leaderName: string | null;
	memberNames: string[];
}

export interface LiveStats {
    STR: number,
    INT: number
	level: number;
	currentHP: number;
	currentMP: number;
	zerkLevel: number;
	unusedStatPoints: number;
	gold: number;
	skillPoints: number;
}

export interface LiveSession {
	connectionId: number;
	characterName: string;
	jid: number;
	ip: string;
	loginTime: string;
	sessionSeconds: number;
	isAfk: boolean;
	inventoryReady: boolean;
	party: LiveParty | null;
	stats: LiveStats | null;
}

export interface LiveInventoryItem {
	slot: number;
	itemId: number;
	codeName: string;
	displayName: string;
	stack: number;
	maxStack: number;
	iconUrl: string | null;
}

export interface LiveInventory {
	connectionId: number;
	characterName: string;
	equipment: LiveInventoryItem[];
	inventory: LiveInventoryItem[];
	pets: Record<string, LiveInventoryItem[]>;
    petInfos: Record<string, LivePetInfo>;
}

export interface LivePetInfo {
    name: string;
    isAttackPet: boolean;
    codeName: string;
    readableName: string;
}

export interface CharacterPosition {
	characterName: string | null;
	latestRegion: number;
	posX: number;
	posY: number;
	posZ: number;
}

export interface ItemDurabilityEntry {
	codeName: string;
	durability: string;
	id: string;
	maxStack: string;
}

// Enum mirrors
export type RewardType    = 'Experience' | 'SkillExperience' | 'Gold' | 'SkillPoints';
export type TalismanGroup = 'ToguiVillage' | 'FlameMountain' | 'WreckA' | 'WreckB';
export type Town          = 'Jangan' | 'Donwhang' | 'Hotan' | 'Samarkand' | 'Constantinople' | 'AlexandriaNorth' | 'AlexandriaSouth';

export interface TextdataStatus {
	running: boolean;
	progress: number;
}

// ── Internal fetch wrapper ───────────────────────────────────────────────────

async function request<T>(path: string, options?: RequestInit, skipAuth = false): Promise<T> {
	const token = authStore.getToken();
	const authHeader: Record<string, string> =
		!skipAuth && token ? { Authorization: `Bearer ${token}` } : {};

	const res = await fetch(`${getApiBase()}${path}`, {
		...options,
		headers: {
			'Content-Type': 'application/json',
			...authHeader,
			...options?.headers
		}
	});

	if (!res.ok) {
		const text = await res.text().catch(() => res.statusText);
		throw new Error(`${res.status}: ${text}`);
	}
    
	return res.json() as Promise<T>;
}

// ── Public API surface ───────────────────────────────────────────────────────

export const serverApi = {
	getPublicStatus:    ()                 => request<{ isRunning: boolean }>('/server/public-status', undefined, true),
	getPublicPlayerCount: ()               => request<{ count: string }>('/players/public-player-count', undefined, true),
	getStatus:           ()                   => request<VSROServerStatus>('/server/status'),
	startServer:         ()                   => request<{ message: string }>('/server/start',            { method: 'POST' }),
	shutdownServer:      ()                   => request<{ message: string }>('/server/shutdown',         { method: 'POST' }),
	restartGateway:      ()                   => request<{ message: string }>('/server/restart-gateway',   { method: 'POST' }),
	restartShardGame:    ()                   => request<{ message: string }>('/server/restart-shard-game', { method: 'POST' }),
	getSettings:    ()                     => request<StartupSettings>('/server/settings'),
	updateSettings: (s: StartupSettings)   => request<{ message: string }>('/server/settings', {
		method: 'PUT',
		body: JSON.stringify(s)
	}),
	sendNotice: (msg: string) => request<{ message: string; playerCount: number }>('/server/notice', {
		method: 'POST',
		body: JSON.stringify({ message: msg })
	}),

	startProxy:   () => request<{ message: string }>('/server/proxy/start',   { method: 'POST' }),
	stopProxy:    () => request<{ message: string }>('/server/proxy/stop',    { method: 'POST' }),
	restartProxy: () => request<{ message: string }>('/server/proxy/restart', { method: 'POST' }),

	getLogOpcodes:   ()               => request<string[]>('/server/log-opcodes'),
	addLogOpcode:    (opcode: string) => request<{ message: string }>(`/server/add-log-opcode?opcode=${encodeURIComponent(opcode)}`),
	removeLogOpcode: (opcode: string) => request<{ message: string }>(`/server/remove-log-opcode?opcode=${encodeURIComponent(opcode)}`),
	clearLogOpcodes: ()               => request<{ message: string }>('/server/clear-log-opcodes')
};

export const playersApi = {
	getOnlineCount: () =>
		request<{ count: string }>('/players/online-count'),

	getCharacterPosition: (name: string) =>
		request<CharacterPosition>(`/players/character-position?name=${encodeURIComponent(name)}`),

	createAccount: (username: string, password: string, isAdmin: boolean) =>
		request<{ message: string }>('/players/account', {
			method: 'POST',
			body: JSON.stringify({ username, password, isAdmin })
		}),

	addSilk: (username: string, amount: number) =>
		request<{ message: string }>('/players/silk', {
			method: 'POST',
			body: JSON.stringify({ username, amount })
		}),

	truncateCharacters: (jid: number) =>
		request<{ message: string }>(`/players/characters/${jid}`, { method: 'DELETE' })
};

export const economyApi = {
	addItemToShop: (itemId: number, price: number, shopTabCodeName: string, data: number, currencyType: number) =>
		request<{ message: string; detail: string }>('/economy/shop/add-item', {
			method: 'POST',
			body: JSON.stringify({ itemId, price, shopTabCodeName, data, currencyType })
		}),

	editQuestRewards: (type: RewardType, multiplier: number, minLevel: number, maxLevel: number) =>
		request<{ message: string }>('/economy/quests/rewards', {
			method: 'POST',
			body: JSON.stringify({ type, multiplier, minLevel, maxLevel })
		}),

	setAlchemyRates: (param2: number, param3: number, param4: number) =>
		request<{ message: string }>('/economy/alchemy/rates', {
			method: 'POST',
			body: JSON.stringify({ param2, param3, param4 })
		}),

	setTalismanDropRates: (group: TalismanGroup, dropRatio: number, affectAll: boolean, dropAmountMin: number, dropAmountMax: number) =>
		request<{ message: string }>('/economy/talisman/drop-rates', {
			method: 'POST',
			body: JSON.stringify({ group, dropRatio, affectAll, dropAmountMin, dropAmountMax })
		}),

	addMonsterDrop: (monsterId: number, itemId: number, dropRatio: number) =>
		request<{ message: string }>('/economy/monster/add-drop', {
			method: 'POST',
			body: JSON.stringify({ monsterId, itemId, dropRatio })
		}),

	setItemMaxStack: (itemCodeName: string, newStackSize: number) =>
		request<{ message: string }>('/economy/items/max-stack', {
			method: 'PUT',
			body: JSON.stringify({ itemCodeName, newStackSize })
		}),

	getItemDurability: (code: string) =>
		request<ItemDurabilityEntry[]>(`/economy/items/durability?code=${encodeURIComponent(code)}`)
};

export interface RegionAssoc {
	areaName: string;
	enabled:  boolean;
}

export interface MonsterSpawnCount {
	mobCode:  string;
	maxCount: number;
}

export interface PrivilegedIp {
	nIdx:       number;
	szIPBegin:  string;
	szIPEnd:    string;
	isGm:       boolean;
	dIssueDate: string;
	szISP:      string | null;
	szDesc:     string | null;
}

export const worldApi = {
	addNpc: (codeName: string, characterName: string, storeGroups: number, tabGroup1: number, tabGroup2: number, tabGroup3: number, tabGroup4: number, lookingDir: number) =>
		request<{ message: string }>('/world/npc', {
			method: 'POST',
			body: JSON.stringify({ codeName, characterName, storeGroups, tabGroup1, tabGroup2, tabGroup3, tabGroup4, lookingDir })
		}),

	addReversePoint: (zoneName: string, posX: number, posY: number, posZ: number, regionId: number) =>
		request<{ message: string }>('/world/reverse-point', {
			method: 'POST',
			body: JSON.stringify({ zoneName, posX, posY, posZ, regionId })
		}),

	addTeleporter: (codeName: string, goldFee: number, townLink: Town, fromCharacter: string, toCharacter: string, requiredLevel: number) =>
		request<{ message: string }>('/world/teleporter', {
			method: 'POST',
			body: JSON.stringify({ codeName, goldFee, townLink, fromCharacter, toCharacter, requiredLevel })
		}),

	setMonsterSpawns: (monsterCodeName: string, maxCount: number, exact = false) =>
		request<{ message: string }>('/world/monster-spawns', {
			method: 'PUT',
			body: JSON.stringify({ monsterCodeName, maxCount, exact })
		}),

	setAllGroupMonsterSpawns: (maxCount: number) =>
		request<{ message: string }>('/world/monster-spawns/all-groups', {
			method: 'PUT',
			body: JSON.stringify({ maxCount })
		}),

	fixUniqueSpawns: () =>
		request<{ message: string }>('/world/fix-unique-spawns', { method: 'POST' }),

	getMonsterSpawnCounts: (prefix: string) =>
		request<MonsterSpawnCount[]>(`/world/monster-spawn-counts?prefix=${encodeURIComponent(prefix)}`),

	getRegions: () =>
		request<RegionAssoc[]>('/world/regions'),

	setRegion: (areaName: string, enabled: boolean) =>
		request<{ message: string }>('/world/regions', {
			method: 'PUT',
			body: JSON.stringify({ areaName, enabled })
		})
};

export const privilegedIpApi = {
	getAll: () =>
		request<PrivilegedIp[]>('/privileged-ip'),

	update: (nIdx: number, ip: string, isGm: boolean) =>
		request<{ message: string }>('/privileged-ip', {
			method: 'PUT',
			body: JSON.stringify({ nIdx, ip, isGm })
		})
};

export const accountApi = {
	/** Panel login — rejects non-admin authority levels */
	adminLogin: async (username: string, password: string): Promise<LoginResponse> => {
		const res = await request<LoginResponse>('/account/login', {
			method: 'POST',
			body: JSON.stringify({ username, password })
		}, true);

		if (res.user.authority === 3) {
			throw new Error('Access denied. Admin privileges required.');
		}

		return res;
	},

	/** Public login — accepts any valid account */
	userLogin: (username: string, password: string): Promise<LoginResponse> =>
		request<LoginResponse>('/account/login', {
			method: 'POST',
			body: JSON.stringify({ username, password })
		}, true),

	/** Public registration (invite code required) */
	signUp: (username: string, password: string, email: string, nickname: string, sex: string, inviteCode: string) =>
		request<{ message: string }>('/account/sign-up', {
			method: 'POST',
			body: JSON.stringify({ username, password, email, nickname, sex, inviteCode })
		}, true),

	/** Get current user's profile (requires auth) */
	getMe: (): Promise<UserDTO> =>
		request<UserDTO>('/account/me'),

	/** Update profile fields (requires auth) */
	updateProfile: (nickname: string | null, email: string | null, sex: string | null) =>
		request<{ message: string }>('/account/profile', {
			method: 'PUT',
			body: JSON.stringify({ nickname, email, sex })
		}),

	/** Change password (requires auth) */
	changePassword: (currentPassword: string, newPassword: string) =>
		request<{ message: string }>('/account/password', {
			method: 'PUT',
			body: JSON.stringify({ currentPassword, newPassword })
		}),

	/** Get current user's silk balance (requires auth) */
	getSilk: (): Promise<{ silk: number }> =>
		request<{ silk: number }>('/account/silk'),

	/** List all active invite codes (admin only) */
	listInviteCodes: (): Promise<InviteCode[]> =>
		request<InviteCode[]>('/account/invite-codes'),

	/** Generate a new invite code (admin only) */
	generateInviteCode: (note?: string): Promise<{ code: string; message: string }> =>
		request<{ code: string; message: string }>('/account/invite-codes', {
			method: 'POST',
			body: JSON.stringify({ note: note ?? '' })
		}),

	/** Revoke an invite code (admin only) */
	revokeInviteCode: (code: string): Promise<{ message: string }> =>
		request<{ message: string }>(`/account/invite-codes/${encodeURIComponent(code)}`, { method: 'DELETE' })
};

export interface InviteCode {
	code: string;
	createdAt: string;
	note: string;
}

export interface QueryResult {
	columns: string[];
	rows: (string | null)[][];
	rowsAffected: number;
	error: string | null;
}

export type DbSchema = Record<string, Record<string, string[]>>;

export const databaseApi = {
	runQuery: (sql: string) =>
		request<QueryResult>('/database/query', {
			method: 'POST',
			body: JSON.stringify({ sql })
		}),

	/** Returns cached schema from disk, throws if not yet built */
	getSchema: () => request<DbSchema>('/database/schema'),

	/** Triggers a fresh schema build from INFORMATION_SCHEMA and saves to disk */
	buildSchema: () =>
		request<{ message: string; databases: string[] }>('/database/schema/build', { method: 'POST' })
};

export interface SavedQuery {
	id: string;
	name: string;
	sql: string;
	createdAt: string;
}

export const savedQueriesApi = {
	list:   ()                          => request<SavedQuery[]>('/database/saved-queries'),
	save:   (name: string, sql: string) => request<SavedQuery>('/database/saved-queries', {
		method: 'POST',
		body: JSON.stringify({ name, sql })
	}),
	delete: (id: string)                => request<{ message: string }>(`/database/saved-queries/${id}`, { method: 'DELETE' })
};

export const textdataApi = {
	generate:  ()  => request<{ message: string }>('/textdata/generate', { method: 'POST' }),
	getStatus: ()  => request<TextdataStatus>('/textdata/status'),

	async download(): Promise<void> {
		const res = await fetch(`${getApiBase()}/textdata/download`);
		if (!res.ok) {
			const text = await res.text().catch(() => res.statusText);
			throw new Error(`${res.status}: ${text}`);
		}
		const blob = await res.blob();
		const url  = URL.createObjectURL(blob);
		const a    = document.createElement('a');
		a.href     = url;
		a.download = `textdata_${new Date().toISOString().replace(/[:.]/g, '-')}.zip`;
		a.click();
		URL.revokeObjectURL(url);
	}
};

// ── Client downloads ─────────────────────────────────────────────────────────

export interface ClientEntry {
	version:     string;
	fileName:    string;
	uploadedAt:  string;
	sizeBytes:   number;
	downloadUrl: string;
}

export interface LogResponse {
	total: number;
	entries: string[];
}

export const logsApi = {
	/** Fetch log entries from index `since` onwards. */
	get: (since = 0) => request<LogResponse>(`/logs?since=${since}`),

	/** Clear in-memory log history on the server. */
	clear: () => request<{ message: string }>('/logs', { method: 'DELETE' })
};

// ── Server config (server.cfg) ───────────────────────────────────────────────

export interface ServerRates {
	expRatio:           number;
	expRatioParty:      number;
	dropItemRatio:      number;
	dropGoldAmountCoef: number;
	winterEvent2009:    boolean;
	thanksgivingEvent:  boolean;
	christmasEvent2007: boolean;
}

export const serverCfgApi = {
	/** Public — no auth. Returns live rates parsed from server.cfg. */
	getRates: () =>
		request<ServerRates>('/server-cfg/rates', undefined, true),

	/** Admin — updates rate values in server.cfg on disk. */
	updateRates: (rates: ServerRates) =>
		request<{ message: string }>('/server-cfg/rates', {
			method: 'PUT',
			body: JSON.stringify(rates)
		}),

	/** Admin — updates every Certification IP across all blocks. */
	updateCertificationIp: (ip: string) =>
		request<{ message: string }>('/server-cfg/certification-ip', {
			method: 'PUT',
			body: JSON.stringify({ ip })
		}),
};

// ── Database backups ─────────────────────────────────────────────────────────

export interface BackupFileInfo {
	database:   string;
	fileName:   string;
	sizeBytes:  number;
	createdAt:  string;
}

export interface BackupStatus {
	isBusy:          boolean;
	lastRunAt:       string | null;
	lastRunMessage:  string;
	lastRunSuccess:  boolean;
	files:           BackupFileInfo[];
}

export const backupApi = {
	run:    () => request<{ message: string }>('/backup/run', { method: 'POST' }),
	status: () => request<BackupStatus>('/backup/status'),
};

export const clientApi = {
	/** Latest client info — public, no auth needed */
	getLatest: () => request<ClientEntry>('/client/latest', undefined, true),

	/** Full list — admin only */
	list: () => request<ClientEntry[]>('/client/list'),

	/** Download a client file — requires auth, triggers browser save-as dialog */
	async download(fileName: string): Promise<void> {
		const token = authStore.getToken();
		const res = await fetch(`${getApiBase()}/client/download/${encodeURIComponent(fileName)}`, {
			headers: token ? { Authorization: `Bearer ${token}` } : {}
		});
		if (!res.ok) {
			const text = await res.text().catch(() => res.statusText);
			if (res.status === 401) throw new Error('You must be logged in to download the client.');
			throw new Error(`${res.status}: ${text}`);
		}
		const blob = await res.blob();
		const url = URL.createObjectURL(blob);
		const a = document.createElement('a');
		a.href = url;
		a.download = fileName;
		a.click();
		URL.revokeObjectURL(url);
	}
};

// ── Quest editor ─────────────────────────────────────────────────────────────

export interface MobDrop {
	mobName:    string;
	dropChance: number;
}

export interface QuestMission {
	missionIndex: number;
	type:         'Kill' | 'Gather';
	snCode:       string;
	// Kill
	monsterName?:  string | null;
	monsterClass?: string | null;
	killCount?:    number | null;
	// Gather
	collectCount?: number | null;
	itemName?:     string | null;
	mobDrops?:     MobDrop[] | null;
}

export interface QuestFile {
	questName: string;
	fileName:  string;
	missions:  QuestMission[];
}

export interface QuestListResponse {
	quests:     QuestFile[];
	total:      number;
	page:       number;
	pageSize:   number;
	totalPages: number;
}

export interface MobDropUpdate {
	mobName:    string;
	dropChance: number;
}

export interface QuestMissionUpdate {
	missionIndex:  number;
	type:          'kill' | 'gather';
	killCount?:    number | null;
	collectCount?: number | null;
	mobDrops?:     MobDropUpdate[] | null;
}

export interface Notice {
	id:        number;
	contentID: number;
	subject:   string;
	article:   string;
	editDate:  string;
}

export const noticeApi = {
	getAll: (contentId = 22) =>
		request<Notice[]>(`/notice?contentId=${contentId}`),

	add: (subject: string, article: string, contentID = 22) =>
		request<{ message: string }>('/notice', {
			method: 'POST',
			body: JSON.stringify({ subject, article, contentID })
		}),

	delete: (id: number) =>
		request<{ message: string }>(`/notice/${id}`, { method: 'DELETE' })
};

// ── Binary Patching ──────────────────────────────────────────────────────────

export interface GameServerCurrentValues {
	maxLevel: number;
	masteryLimit: number;
	ratesFixed: boolean;
	dumpsDisabled: boolean;
	greenBookDisabled: boolean;
	spoofedIP: string;
	objectLimit: number;
}

export interface GameServerPatchRequest {
	maxLevel?: number | null;
	masteryLimit?: number | null;
	fixRates: boolean;
	disableDumpFiles: boolean;
	disableGreenBook: boolean;
	ipToSet?: string | null;
	objectLimitToSet?: number | null;
}

export interface NodeIniValues {
	wip:          string | null;
	nip:          string | null;
	shardName:    string | null;
	accountQuery: string | null;
	shardQuery:   string | null;
	logQuery:     string | null;
}

export interface NodeIniPatchRequest {
	ip?:           string | null;
	shardName?:    string | null;
	accountQuery?: string | null;
	shardQuery?:   string | null;
	logQuery?:     string | null;
}

export const patchApi = {
	getGameServerValues: () =>
		request<GameServerCurrentValues>('/patch/game-server'),

	patchGameServer: (req: GameServerPatchRequest) =>
		request<{ message: string; applied: string[] }>('/patch/game-server', {
			method: 'POST',
			body: JSON.stringify(req)
		}),

	patchMachineManagerIP: (ip: string) =>
		request<{ message: string }>('/patch/machine-manager/spoof-ip', {
			method: 'POST',
			body: JSON.stringify({ ipAddress: ip })
		}),

	patchAgentServerIP: (ip: string) =>
		request<{ message: string }>('/patch/agent-server/spoof-ip', {
			method: 'POST',
			body: JSON.stringify({ ipAddress: ip })
		}),

	getNodeIniValues: () =>
		request<NodeIniValues>('/patch/node-inis'),

	patchNodeInis: (req: NodeIniPatchRequest) =>
		request<{ message: string }>('/patch/node-inis', {
			method: 'POST',
			body: JSON.stringify(req)
		})
};

export const questApi = {
	list: (page = 1, search = '') =>
		request<QuestListResponse>(`/quest?page=${page}&search=${encodeURIComponent(search)}`),

	update: (questName: string, missions: QuestMissionUpdate[]) =>
		request<{ message: string }>(`/quest/${encodeURIComponent(questName)}`, {
			method: 'PUT',
			body: JSON.stringify({ missions })
		}),

	compile: () =>
		request<{ message: string }>('/quest/compile', { method: 'POST' }),

	async downloadTextdata(): Promise<void> {
		const token = authStore.getToken();
		const res = await fetch(`${getApiBase()}/quest/textdata/download`, {
			headers: token ? { Authorization: `Bearer ${token}` } : {}
		});
		if (!res.ok) {
			const text = await res.text().catch(() => res.statusText);
			throw new Error(`${res.status}: ${text}`);
		}
		const blob = await res.blob();
		const url  = URL.createObjectURL(blob);
		const a    = document.createElement('a');
		a.href     = url;
		a.download = 'textquest_speech&name.txt';
		a.click();
		URL.revokeObjectURL(url);
	}
};

export const liveApi = {
	getSessions: () =>
		request<LiveSession[]>('/live/sessions'),

	getInventory: (connectionId: number) =>
		request<LiveInventory>(`/live/sessions/${connectionId}/inventory`)
};
