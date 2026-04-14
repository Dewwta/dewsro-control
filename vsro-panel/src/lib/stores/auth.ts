import { writable, get } from 'svelte/store';
import { browser } from '$app/environment';

export interface AuthUser {
	jid: number;
	username: string;
	nickname: string | null;
	email: string | null;
	sex: string | null;
	authority: number;
}

interface AuthState {
	token: string | null;
	user: AuthUser | null;
}

function createAuthStore() {
	const initial: AuthState = browser
		? {
				token: localStorage.getItem('panel_token'),
				user: (() => {
					try {
						const raw = localStorage.getItem('panel_user');
						return raw ? (JSON.parse(raw) as AuthUser) : null;
					} catch {
						return null;
					}
				})()
			}
		: { token: null, user: null };

	const { subscribe, set, update } = writable<AuthState>(initial);

	return {
		subscribe,

		login(token: string, user: AuthUser) {
			if (browser) {
				localStorage.setItem('panel_token', token);
				localStorage.setItem('panel_user', JSON.stringify(user));
			}
			set({ token, user });
		},

		logout() {
			if (browser) {
				localStorage.removeItem('panel_token');
				localStorage.removeItem('panel_user');
			}
			set({ token: null, user: null });
		},

		getToken(): string | null {
			return get({ subscribe }).token;
		},

		isAdmin(): boolean {
			const user = get({ subscribe }).user;
			// Authority != 3 means admin (mirrors IsAuthoritive() on the server)
			return user !== null && user.authority !== 3;
		}
	};
}

export const authStore = createAuthStore();
