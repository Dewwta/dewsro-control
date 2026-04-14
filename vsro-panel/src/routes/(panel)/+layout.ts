import { redirect } from '@sveltejs/kit';
import { browser } from '$app/environment';

export function load() {
	if (!browser) return {};

	const token = localStorage.getItem('panel_token');
	const userRaw = localStorage.getItem('panel_user');

	if (!token || !userRaw) {
		throw redirect(302, '/admin');
	}

	try {
		const user = JSON.parse(userRaw);
		// Authority != 3 means admin — mirrors server-side IsAuthoritive()
		if (user.authority === 3) {
			throw redirect(302, '/admin');
		}
	} catch (e) {
		if ((e as { status?: number }).status === 302) throw e;
		throw redirect(302, '/admin');
	}

	return {};
}
