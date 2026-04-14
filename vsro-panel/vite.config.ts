import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, type Plugin } from 'vite';
import fs from 'fs';
import { spawnSync } from 'child_process';

// Disables Windows Console Quick Edit mode for the terminal running this process.
// Child processes on Windows share the parent's conhost handle, so SetConsoleMode
// called from the spawned PowerShell affects the entire console window.
// This prevents the terminal from pausing Vite when a user clicks on it.
// Just fixing another stupid bullshit problem.
function disableWindowsQuickEdit(): Plugin {
	return {
		name: 'disable-windows-quick-edit',
		configureServer() {
			if (process.platform !== 'win32') return;
			const ps = [
				'Add-Type -TypeDefinition @\'',
				'using System; using System.Runtime.InteropServices;',
				'public class QE {',
				'  [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int h);',
				'  [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr h, out uint m);',
				'  [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr h, uint m);',
				'  public static void Off() { var h = GetStdHandle(-10); uint m; GetConsoleMode(h, out m); SetConsoleMode(h, m & ~0x0040u); }',
				'}',
				'\'@',
				'[QE]::Off()'
			].join('\n');
			spawnSync('powershell', ['-NonInteractive', '-NoProfile', '-Command', ps], { stdio: 'inherit' });
		}
	};
}

export default defineConfig({
	plugins: [sveltekit(), disableWindowsQuickEdit()],
	server: {
		host: '0.0.0.0',
		port: 5007,
		https: {
			key: fs.readFileSync('C:\\Certs\\server.key'),
			cert: fs.readFileSync('C:\\Certs\\server.crt')
		}
	}
});