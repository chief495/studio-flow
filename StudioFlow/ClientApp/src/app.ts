import type { AuthResponse, Room, Tariff, Booking, Slot } from './models.js';
import { $ } from './dom.js';
import { state, admin } from './state.js';
import { escapeHtml, money, dateText } from './format.js';
import { api } from './api.js';
function notice(text, error = false) {
  $('notice').hidden = false;
  $('notice').textContent = text;
  $('notice').classList.toggle('error', error);
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
  const signed = Boolean(state.token && state.user);
  $('auth').hidden = signed;
  $('workspace').hidden = !signed;
  $('logout').hidden = !signed;
  $('identity').textContent = signed
    ? state.user!.email +
      ' · ' +
      ({ Admin: 'Администратор', Manager: 'Менеджер', Client: 'Клиент' }[state.user!.role] || '')
    : '';
  $('adminTab').hidden = !admin();
  $('availability').hidden = true;
}
async function busy(button: HTMLButtonElement, action: () => Promise<void>) {
  button.disabled = true;
  try {
    await action();
  } finally {
    button.disabled = false;
  }
}
function switchAuth(register) {
  state.register = register;
  $('authTitle').textContent = register ? 'Создать аккаунт' : 'С возвращением';
  $('authHint').textContent = register
    ? 'Начните бронировать студию онлайн'
    : 'Войдите, чтобы забронировать студию';
  $('confirmation').hidden = !register;
  $('confirmPassword').required = register;
  $('password').minLength = register ? 8 : 1;
  $('password').autocomplete = register ? 'new-password' : 'current-password';
  $('authSubmit').textContent = register ? 'Зарегистрироваться' : 'Войти';
  $('switchAuth').textContent = register
    ? 'Уже есть аккаунт? Войти'
    : 'Нет аккаунта? Зарегистрироваться';
  $('authError').textContent = '';
}
$('switchAuth').onclick = () => switchAuth(!state.register);
$('logout').onclick = logout;
$('authForm').onsubmit = async (event) => {
  event.preventDefault();
  $('authError').textContent = '';
  if (state.register && $('password').value !== $('confirmPassword').value) {
    $('authError').textContent = 'Пароли не совпадают.';
    return;
  }
  await busy($('authSubmit'), async () => {
    try {
      const body: { email: string; password: string; tenantId?: string } = {
        email: $('email').value.trim(),
        password: $('password').value,
      };
      if (state.register) body.tenantId = (await api<{ id: string }>('/studio')).id;
      const data = await api<AuthResponse>(
        state.register ? '/auth/register' : '/auth/login',
        'POST',
        body,
      );
      state.token = data.token;
      state.user = data;
      sessionStorage.setItem('studioflow_token', data.token);
      sessionStorage.setItem(
        'studioflow_user',
        JSON.stringify({
          id: data.id,
          email: data.email,
          role: data.role,
          tenantId: data.tenantId,
        }),
      );
      $('authForm').reset();
      renderSession();
      tab('rooms');
      await reload();
    } catch (e) {
      $('authError').textContent = e.message;
    }
  });
};
function tab(name: string | undefined) {
  for (const key of ['rooms', 'bookings', 'admin'])
    document.getElementById(key + 'View')!.hidden = key !== name;
  document
    .querySelectorAll<HTMLButtonElement>('[data-tab]')
    .forEach((b) => b.classList.toggle('active', b.dataset.tab === name));
}
document
  .querySelectorAll<HTMLButtonElement>('[data-tab]')
  .forEach((b) => (b.onclick = () => tab(b.dataset.tab)));
async function reload() {
  try {
    const [rooms, tariffs, bookings] = await Promise.all([
      api<Room[]>('/rooms'),
      api<Tariff[]>('/tariffs'),
      api<Booking[]>('/bookings'),
    ]);
    if (!state.token) return;
    Object.assign(state, { rooms, tariffs, bookings });
    renderRooms();
    renderBookings();
    renderAdmin();
  } catch (e) {
    notice(e.message, true);
  }
}
function renderRooms() {
  $('rooms').innerHTML = state.rooms.length
    ? state.rooms
        .map(
          (r) =>
            '<article class="card"><p class="eyebrow">СТУДИЙНОЕ ПРОСТРАНСТВО</p><h3>' +
            escapeHtml(r.name) +
            '</h3><p>До ' +
            r.capacity +
            ' человек</p><div class="price">' +
            (r.tariff ? money(r.tariff.hourlyRate) + ' / час' : 'Тариф уточняется') +
            '</div><button class="primary" data-room="' +
            r.id +
            '">Посмотреть свободное время →</button></article>',
        )
        .join('')
    : '<p class="empty">Пока нет доступных комнат.</p>';
  $('rooms')
    .querySelectorAll<HTMLButtonElement>('[data-room]')
    .forEach(
      (b) =>
        (b.onclick = async () => {
          state.room = state.rooms.find((r) => r.id === b.dataset.room) || null;
          if (!state.room) return;
          $('availability').hidden = false;
          $('slotTitle').textContent = state.room.name;
          $('date').value ||= new Date().toISOString().slice(0, 10);
          await loadSlots();
          $('availability').scrollIntoView({ behavior: 'smooth' });
        }),
    );
}
let slotVersion = 0;
async function loadSlots() {
  const version = ++slotVersion;
  const room = state.room;
  if (!room || !$('date').value) return;
  $('slots').textContent = 'Загрузка слотов…';
  try {
    const slots = await api<Slot[]>('/rooms/' + room.id + '/slots?date=' + $('date').value);
    if (version !== slotVersion || !state.token) return;
    $('slots').replaceChildren();
    for (const slot of slots) {
      const b = document.createElement('button');
      b.disabled = !slot.available;
      b.textContent =
        slot.startsAt.slice(11, 16) +
        '–' +
        slot.endsAt.slice(11, 16) +
        (slot.available ? '' : ' · занято');
      b.onclick = () =>
        confirmAction(
          'Подтвердить бронирование?',
          room.name +
            ' · ' +
            slot.startsAt.slice(0, 10) +
            ' · ' +
            slot.startsAt.slice(11, 16) +
            ' UTC' +
            (room.tariff ? ' · ' + money(room.tariff.hourlyRate) : ''),
          async () => {
            try {
              await api('/bookings', 'POST', {
                roomId: room.id,
                startsAt: slot.startsAt,
                endsAt: slot.endsAt,
              });
              notice('Бронирование создано.');
              await reload();
            } finally {
              await loadSlots();
            }
          },
        );
      $('slots').append(b);
    }
  } catch (e) {
    if (version === slotVersion) $('slots').textContent = e.message;
  }
}
$('date').min = new Date().toISOString().slice(0, 10);
$('date').onchange = loadSlots;
function renderBookings() {
  const filter = $('bookingFilter').value;
  const items = state.bookings.filter(
    (b) => (admin() || b.userId === state.user!.id) && (filter === 'all' || b.status === filter),
  );
  $('bookings').innerHTML = items.length
    ? items
        .map(
          (b) =>
            '<article class="row"><div><strong>' +
            escapeHtml(b.room?.name || 'Комната') +
            '</strong><p>' +
            dateText(b.startsAt) +
            ' — ' +
            dateText(b.endsAt) +
            '</p><span class="badge">' +
            (b.status === 'Active' ? 'Активно' : 'Отменено') +
            '</span></div>' +
            (b.status === 'Active'
              ? '<button data-cancel="' + b.id + '">Отменить бронирование</button>'
              : '') +
            '</article>',
        )
        .join('')
    : '<p class="empty">Бронирований в этой категории пока нет.</p>';
  $('bookings')
    .querySelectorAll<HTMLButtonElement>('[data-cancel]')
    .forEach(
      (b) =>
        (b.onclick = () =>
          confirmAction(
            'Отменить бронирование?',
            'Комната снова станет доступна для других пользователей.',
            async () => {
              await api('/bookings/' + b.dataset.cancel + '/cancel', 'POST');
              notice('Бронирование отменено.');
              await reload();
              if (state.room) await loadSlots();
            },
          )),
    );
}
$('bookingFilter').onchange = renderBookings;
$('refresh').onclick = () => busy($('refresh'), reload);
function renderAdmin() {
  if (!admin()) return;
  for (const [id, items, type] of [
    ['adminRooms', state.rooms, 'room'],
    ['tariffs', state.tariffs, 'tariff'],
  ] as const) {
    $(id).innerHTML =
      items
        .map(
          (item) =>
            '<div class="row"><div><strong>' +
            escapeHtml(item.name) +
            '</strong><p>' +
            (type === 'room'
              ? item.capacity + ' чел. · ' + escapeHtml(item.tariff?.name || 'Без тарифа')
              : money(item.hourlyRate) + ' / час') +
            '</p></div><button data-edit="' +
            item.id +
            '">Изменить</button></div>',
        )
        .join('') || '<p class="empty">Список пока пуст.</p>';
    $(id)
      .querySelectorAll<HTMLButtonElement>('[data-edit]')
      .forEach(
        (b) =>
          (b.onclick = () =>
            openEditor(
              type,
              items.find((x) => x.id === b.dataset.edit),
            )),
      );
  }
}
function openEditor(type: 'room' | 'tariff', item?: Partial<Room & Tariff>) {
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
      .map(
        (t) =>
          '<option value="' +
          t.id +
          '">' +
          escapeHtml(t.name) +
          ' — ' +
          money(t.hourlyRate) +
          '</option>',
      )
      .join('');
  $('tariff').value = item?.tariffId || '';
  $('editor').showModal();
}
$('newRoom').onclick = () => openEditor('room');
$('newTariff').onclick = () => openEditor('tariff');
$('closeEditor').onclick = () => $('editor').close();
$('editorForm').onsubmit = async (event) => {
  event.preventDefault();
  const button = (event as SubmitEvent).submitter as HTMLButtonElement;
  await busy(button, async () => {
    try {
      const id = $('editId').value;
      const name = $('editName').value.trim();
      if (!name) throw new Error('Введите название.');
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
        if (id) await api('/rooms/' + id, 'PUT', body);
        else {
          const created = await api<Room>('/rooms', 'POST', { name, capacity: body.capacity });
          if (body.tariffId || !body.isActive) await api('/rooms/' + created.id, 'PUT', body);
        }
      }
      $('editor').close();
      notice('Изменения сохранены.');
      await reload();
    } catch (e) {
      $('editorError').textContent = e.message;
    }
  });
};
function confirmAction(title, text, action) {
  $('confirmTitle').textContent = title;
  $('confirmText').textContent = text;
  $('confirmYes').onclick = () =>
    busy($('confirmYes'), async () => {
      try {
        await action();
      } catch (e) {
        notice(e.message, true);
      } finally {
        $('confirm').close();
      }
    });
  $('confirm').showModal();
}
$('confirmNo').onclick = () => $('confirm').close();
switchAuth(false);
renderSession();
if (state.token) reload();

window.addEventListener('session-expired', () => {
  logout();
  $('authError').textContent = 'Сессия истекла. Войдите снова.';
});
