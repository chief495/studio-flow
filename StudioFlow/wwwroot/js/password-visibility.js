/**
 * Adds an accessible visibility toggle without changing form submission.
 * Each field keeps its own state; resets always restore password masking.
 */
export function initializePasswordVisibility() {
    const fields = document.querySelectorAll('input[autocomplete="current-password"], input[autocomplete="new-password"]');
    fields.forEach((input) => {
        const wrapper = document.createElement('div');
        wrapper.className = 'password-field';
        input.before(wrapper);
        wrapper.append(input);
        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'password-toggle';
        toggle.setAttribute('aria-controls', input.id);
        const setVisible = (visible) => {
            input.type = visible ? 'text' : 'password';
            toggle.textContent = visible ? 'Скрыть' : 'Показать';
            toggle.setAttribute('aria-label', visible ? 'Скрыть пароль' : 'Показать пароль');
            toggle.setAttribute('aria-pressed', String(visible));
        };
        toggle.addEventListener('click', () => {
            const start = input.selectionStart;
            const end = input.selectionEnd;
            setVisible(input.type === 'password');
            input.focus();
            input.setSelectionRange(start, end);
        });
        input.form?.addEventListener('reset', () => setVisible(false));
        setVisible(false);
        wrapper.append(toggle);
    });
}
