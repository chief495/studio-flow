import { $ } from './dom.js';
import { state, admin } from './state.js';
import { escapeHtml, money, dateText } from './format.js';
import { api } from './api.js';
import { initializeBookingTime, editBookingTime, populateBookingTime } from './booking-time.js';
import { initializePasswordVisibility } from './password-visibility.js';
initializePasswordVisibility();
let noticeTimer;
function clearNotice() {
    clearTimeout(noticeTimer);
    $('notice').hidden = true;
    $('notice').textContent = '';
    $('notice').classList.remove('error');
}
document.addEventListener('input', () => {
    clearNotice();
    $('authError').textContent = '';
    $('editorError').textContent = '';
});
function notice(text, error = false) {
    if (!state.token)
        return;
    clearNotice();
    $('notice').hidden = false;
    $('notice').textContent = text;
    $('notice').classList.toggle('error', error);
    noticeTimer = setTimeout(clearNotice, error ? 8000 : 5000);
}
function logout() {
    state.token = null;
    state.user = null;
    sessionStorage.removeItem('studioflow_token');
    sessionStorage.removeItem('studioflow_user');
    $('editor').close();
    $('confirm').close();
    renderSession();
}
function renderSession() {
    $('notice').hidden = true;
    $('notice').textContent = '';
    $('notice').classList.remove('error');
    const signed = Boolean(state.token && state.user);
    $('auth').hidden = signed;
    $('workspace').hidden = !signed;
    $('logout').hidden = !signed;
    $('identity').textContent = signed
        ? state.user.email +
            ' · ' +
            ({ Admin: 'Администратор', Manager: 'Менеджер', Client: 'Клиент' }[state.user.role] || '')
        : '';
    $('adminTab').hidden = !admin();
    $('availability').hidden = true;
}
async function busy(button, action) {
    button.disabled = true;
    try {
        await action();
    }
    finally {
        button.disabled = false;
    }
}
$('logout').onclick = logout;
let registrationMode = false;
$('switchAuth').onclick = () => {
    registrationMode = !registrationMode;
    $('authForm').reset();
    $('authError').textContent = '';
    $('authTitle').textContent = registrationMode ? 'Создать аккаунт' : 'С возвращением';
    $('authHint').textContent = registrationMode
        ? 'Зарегистрируйтесь для бронирования в демо-студии'
        : 'Войдите, чтобы забронировать студию';
    $('confirmation').hidden = !registrationMode;
    $('confirmPassword').required = registrationMode;
    $('password').minLength = registrationMode ? 8 : 1;
    $('password').maxLength = 128;
    $('password').autocomplete = registrationMode ? 'new-password' : 'current-password';
    $('authSubmit').textContent = registrationMode ? 'Зарегистрироваться' : 'Войти';
    $('switchAuth').textContent = registrationMode
        ? 'Уже есть аккаунт? Войти'
        : 'Нет аккаунта? Зарегистрироваться';
};
$('authForm').onsubmit = async (event) => {
    event.preventDefault();
    $('authError').textContent = '';
    if (registrationMode && $('password').value !== $('confirmPassword').value) {
        $('authError').textContent = 'Пароли не совпадают.';
        return;
    }
    const endpoint = registrationMode ? '/auth/register' : '/auth/login';
    await busy($('authSubmit'), async () => {
        try {
            const body = {
                email: $('email').value.trim(),
                password: $('password').value,
            };
            const data = await api(endpoint, 'POST', body);
            state.token = data.token;
            state.user = data;
            sessionStorage.setItem('studioflow_token', data.token);
            sessionStorage.setItem('studioflow_user', JSON.stringify({
                id: data.id,
                email: data.email,
                role: data.role,
                tenantId: data.tenantId,
            }));
            $('authForm').reset();
            renderSession();
            tab('rooms');
            await reload();
        }
        catch (e) {
            $('authError').textContent = e.message;
        }
    });
};
function tab(name) {
    clearNotice();
    for (const key of ['rooms', 'bookings', 'admin'])
        document.getElementById(key + 'View').hidden = key !== name;
    document
        .querySelectorAll('[data-tab]')
        .forEach((b) => b.classList.toggle('active', b.dataset.tab === name));
}
document
    .querySelectorAll('[data-tab]')
    .forEach((b) => (b.onclick = () => tab(b.dataset.tab)));
async function reload() {
    try {
        const [rooms, tariffs, bookings] = await Promise.all([
            api('/rooms'),
            api('/tariffs'),
            api('/bookings'),
        ]);
        if (!state.token)
            return;
        Object.assign(state, { rooms, tariffs, bookings });
        renderRooms();
        renderBookings();
        renderAdmin();
    }
    catch (e) {
        notice(e.message, true);
    }
}
function renderRooms() {
    $('rooms').innerHTML = state.rooms.length
        ? state.rooms
            .map((r) => '<article class="card"><p class="eyebrow">СТУДИЙНОЕ ПРОСТРАНСТВО</p><h3>' +
            escapeHtml(r.name) +
            '</h3><p>До ' +
            r.capacity +
            ' человек</p><div class="price">' +
            (r.tariff ? money(r.tariff.hourlyRate) + ' / час' : 'Тариф уточняется') +
            '</div><button class="primary" data-room="' +
            r.id +
            '">Посмотреть свободное время →</button></article>')
            .join('')
        : '<p class="empty">Пока нет доступных комнат.</p>';
    $('rooms')
        .querySelectorAll('[data-room]')
        .forEach((b) => (b.onclick = async () => {
        state.room = state.rooms.find((r) => r.id === b.dataset.room) || null;
        if (!state.room)
            return;
        $('availability').hidden = false;
        editBookingTime(null);
        $('slotTitle').textContent = state.room.name;
        $('date').value ||= new Date().toISOString().slice(0, 10);
        await loadSlots();
        renderRoomBookings();
        $('availability').scrollIntoView({ behavior: 'smooth' });
    }));
}
let slotVersion = 0;
async function loadSlots() {
    $('date').dispatchEvent(new Event('calendar-sync'));
    const version = ++slotVersion;
    const room = state.room;
    if (!room || !$('date').value)
        return;
    $('slots').textContent = 'Загрузка слотов…';
    try {
        const slots = await api('/rooms/' + room.id + '/availability?date=' + $('date').value);
        if (version !== slotVersion || !state.token)
            return;
        $('slots').textContent =
            'Занятое время: ' +
                (slots
                    .map((slot) => slot.startsAt.slice(11, 16) + '–' + slot.endsAt.slice(11, 16))
                    .join(', ') || 'нет бронирований');
    }
    catch (e) {
        if (version === slotVersion)
            $('slots').textContent = e.message;
    }
}
$('date').min = new Date().toISOString().slice(0, 10);
$('date').onchange = loadSlots;
initializeBookingTime(async () => {
    await reload();
    await loadSlots();
}, (message) => notice(message, true));
function renderBookings() {
    renderRoomBookings();
    const filter = $('bookingFilter').value;
    const items = state.bookings.filter((b) => (admin() || b.userId === state.user.id) && (filter === 'all' || b.status === filter));
    $('bookings').innerHTML = items.length
        ? items
            .map((b) => '<article class="row booking-row"><div class="booking-details"><strong>' +
            escapeHtml(b.room?.name || 'Комната') +
            '</strong><p>' +
            dateText(b.startsAt) +
            ' — ' +
            dateText(b.endsAt) +
            '</p></div><span class="booking-status ' +
            (b.status === 'Active' ? 'booking-status-active' : 'booking-status-cancelled') +
            '">' +
            (b.status === 'Active' ? 'Активно' : 'Отменено') +
            '</span>' +
            (b.status === 'Active'
                ? '<div class="actions"><button data-booking-edit="' +
                    b.id +
                    '">Изменить</button><button data-cancel="' +
                    b.id +
                    '">Отменить бронирование</button></div>'
                : '<div class="actions"></div>') +
            '</article>')
            .join('')
        : '<p class="empty">Бронирований в этой категории пока нет.</p>';
    $('bookings')
        .querySelectorAll('[data-booking-edit]')
        .forEach((button) => {
        button.onclick = async () => {
            const booking = state.bookings.find((item) => item.id === button.dataset.bookingEdit);
            if (!booking)
                return;
            await openBookingEditor(booking);
        };
    });
    $('bookings')
        .querySelectorAll('[data-cancel]')
        .forEach((b) => (b.onclick = () => confirmAction('Отменить бронирование?', 'Комната снова станет доступна для других пользователей.', async () => {
        await api('/bookings/' + b.dataset.cancel + '/cancel', 'POST');
        notice('Бронирование отменено.');
        await reload();
        if (state.room)
            await loadSlots();
    })));
}
async function openBookingEditor(booking) {
    const room = state.rooms.find((item) => item.id === booking.room?.id);
    if (!room) {
        notice('Комната больше недоступна для бронирования.', true);
        return;
    }
    state.room = room;
    tab('rooms');
    $('availability').hidden = false;
    $('slotTitle').textContent = 'Изменить бронирование · ' + room.name;
    populateBookingTime(booking);
    await loadSlots();
    $('availability').scrollIntoView({ behavior: 'smooth' });
}
function renderRoomBookings() {
    let container = document.getElementById('roomBookings');
    if (!container) {
        container = document.createElement('section');
        container.id = 'roomBookings';
        $('availability').append(container);
    }
    container.replaceChildren();
    if (!state.room || !state.user)
        return;
    const items = state.bookings.filter((booking) => booking.room?.id === state.room.id &&
        booking.status === 'Active' &&
        (admin() || booking.userId === state.user.id));
    if (!items.length)
        return;
    const heading = document.createElement('h3');
    heading.textContent = 'Бронирования этой комнаты';
    container.append(heading);
    for (const booking of items) {
        const row = document.createElement('div');
        row.className = 'row';
        const description = document.createElement('span');
        description.textContent = dateText(booking.startsAt) + ' — ' + dateText(booking.endsAt);
        const actions = document.createElement('div');
        actions.className = 'actions';
        for (const [label, attribute] of [
            ['Изменить', 'data-booking-edit'],
            ['Отменить', 'data-cancel'],
        ]) {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = label;
            button.onclick = async () => {
                clearNotice();
                if (attribute === 'data-booking-edit') {
                    await openBookingEditor(booking);
                    return;
                }
                $('bookingFilter').value = 'all';
                renderBookings();
                $('bookings')
                    .querySelector('[' + attribute + '="' + booking.id + '"]')
                    ?.click();
            };
            actions.append(button);
        }
        row.append(description, actions);
        container.append(row);
    }
}
$('bookingFilter').onchange = renderBookings;
$('refresh').onclick = () => busy($('refresh'), reload);
function renderAdmin() {
    if (!admin())
        return;
    for (const [id, items, type] of [
        ['adminRooms', state.rooms, 'room'],
        ['tariffs', state.tariffs, 'tariff'],
    ]) {
        $(id).innerHTML =
            items
                .map((item) => '<div class="row"><div><strong>' +
                escapeHtml(item.name) +
                '</strong><p>' +
                (type === 'room'
                    ? item.capacity + ' чел. · ' + escapeHtml(item.tariff?.name || 'Без тарифа')
                    : money(item.hourlyRate) + ' / час') +
                '</p></div><button data-edit="' +
                item.id +
                '">Изменить</button></div>')
                .join('') || '<p class="empty">Список пока пуст.</p>';
        $(id)
            .querySelectorAll('[data-edit]')
            .forEach((b) => (b.onclick = () => openEditor(type, items.find((x) => x.id === b.dataset.edit))));
    }
}
function openEditor(type, item) {
    state.editor = type;
    $('editorForm').reset();
    $('editorError').textContent = '';
    $('editId').value = item?.id || '';
    $('editName').value = item?.name || '';
    $('editorTitle').textContent =
        (item ? 'Изменить ' : 'Добавить ') + (type === 'room' ? 'комнату' : 'тариф');
    $('roomFields').hidden = type !== 'room';
    $('tariffFields').hidden = type !== 'tariff';
    $('capacity').required = type === 'room';
    $('rate').required = type === 'tariff';
    $('capacity').value = String(item?.capacity || 6);
    $('rate').value = String(item?.hourlyRate ?? '');
    $('isActive').checked = item?.isActive ?? true;
    $('tariff').innerHTML =
        '<option value="">Без тарифа</option>' +
            state.tariffs
                .map((t) => '<option value="' +
                t.id +
                '">' +
                escapeHtml(t.name) +
                ' — ' +
                money(t.hourlyRate) +
                '</option>')
                .join('');
    $('tariff').value = item?.tariffId || '';
    $('editor').showModal();
}
$('newRoom').onclick = () => openEditor('room');
$('newTariff').onclick = () => openEditor('tariff');
$('closeEditor').onclick = () => $('editor').close();
$('editorForm').onsubmit = async (event) => {
    event.preventDefault();
    const button = event.submitter;
    await busy(button, async () => {
        try {
            const id = $('editId').value;
            const name = $('editName').value.trim();
            if (!name)
                throw new Error('Введите название.');
            if (state.editor === 'tariff')
                await api('/tariffs' + (id ? '/' + id : ''), id ? 'PUT' : 'POST', {
                    name,
                    hourlyRate: Number($('rate').value),
                });
            else {
                const body = {
                    name,
                    capacity: Number($('capacity').value),
                    tariffId: $('tariff').value || null,
                    isActive: $('isActive').checked,
                };
                if (id)
                    await api('/rooms/' + id, 'PUT', body);
                else {
                    const created = await api('/rooms', 'POST', { name, capacity: body.capacity });
                    if (body.tariffId || !body.isActive)
                        await api('/rooms/' + created.id, 'PUT', body);
                }
            }
            $('editor').close();
            notice('Изменения сохранены.');
            await reload();
        }
        catch (e) {
            $('editorError').textContent = e.message;
        }
    });
};
function confirmAction(title, text, action) {
    $('confirmTitle').textContent = title;
    $('confirmText').textContent = text;
    $('confirmYes').onclick = () => busy($('confirmYes'), async () => {
        try {
            await action();
        }
        catch (e) {
            notice(e.message, true);
        }
        finally {
            $('confirm').close();
        }
    });
    $('confirm').showModal();
}
$('confirmNo').onclick = () => $('confirm').close();
renderSession();
if (state.token)
    reload();
window.addEventListener('session-expired', () => {
    logout();
    $('authError').textContent = 'Сессия истекла. Войдите снова.';
});
