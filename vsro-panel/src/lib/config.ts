export function getApiBase() {
    const { protocol, hostname } = window.location;

    const isLocal =
        hostname === 'localhost' ||
        hostname === '127.0.0.1';

    if (isLocal) {
        return protocol === 'https:'
            ? 'https://192.168.0.11:5086/api'
            : 'http://192.168.0.11:5085/api';
    }

    const port = protocol === 'https:' ? 5086 : 5085;

    return `${protocol}//${hostname}:${port}/api`;
}