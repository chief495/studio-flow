import { state } from './state.js';
export async function api(path, method = 'GET', body) {
    const response = await fetch('/api' + path, {
        method,
        headers: {
            'Content-Type': 'application/json',
            ...(state.token ? { Authorization: 'Bearer ' + state.token } : {}),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await response.text();
    let data;
    try {
        data = JSON.parse(text);
    }
    catch {
        data = text;
    }
    if (!response.ok) {
        if (response.status === 401) {
            if (!path.startsWith('/auth/')) {
                window.dispatchEvent(new Event('session-expired'));
            }
            throw new Error('Неверный email или пароль, либо сессия истекла.');
        }
        if (response.status === 403)
            throw new Error('Недостаточно прав для этого действия.');
        throw new Error(data?.errors
            ? Object.values(data.errors).flat().join(' ')
            : typeof data === 'string' && data
                ? data
                : data?.detail || data?.title || 'Не удалось выполнить запрос.');
    }
    return data;
}
