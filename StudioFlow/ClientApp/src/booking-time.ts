import { api } from './api.js';
import { state } from './state.js';
import { initializeCalendar } from './calendar.js';
import type { Booking } from './models.js';

/** Replaces the entire draft when the user selects a saved booking. */
export function populateBookingTime(booking: Booking): void {
  const date = document.getElementById('date') as HTMLInputElement;
  const start = document.getElementById('bookingStart') as HTMLInputElement;
  const end = document.getElementById('bookingEnd') as HTMLInputElement;
  const form = document.getElementById('bookingTimeForm') as HTMLFormElement;

  // Form fields and availability use the studio's UTC time.
  const startsAt = new Date(booking.startsAt).toISOString();
  const endsAt = new Date(booking.endsAt).toISOString();
  date.value = startsAt.slice(0, 10);
  start.value = startsAt.slice(11, 16);
  end.value = endsAt.slice(11, 16);
  editBookingTime(booking.id);
  date.dispatchEvent(new Event('calendar-sync'));
  form.dispatchEvent(new Event('input', { bubbles: true }));
}

let editingId: string | null = null;
export function editBookingTime(id: string | null): void {
  editingId = id;
  const button = document.querySelector<HTMLButtonElement>('#bookingTimeForm button');
  if (button) button.textContent = id ? 'Сохранить изменения' : 'Забронировать';
}

interface TimeSelection {
  roomId: string;
  startsAt: string;
  endsAt: string;
}

export function initializeBookingTime(
  onSaved: () => Promise<void>,
  onError: (message: string) => void,
): void {
  const form = document.getElementById('bookingTimeForm') as HTMLFormElement;
  const date = document.getElementById('date') as HTMLInputElement;
  const start = document.getElementById('bookingStart') as HTMLInputElement;
  const end = document.getElementById('bookingEnd') as HTMLInputElement;
  const preview = document.getElementById('bookingPreview') as HTMLElement;

  function selection(): TimeSelection {
    if (!state.room || !date.value || !start.value || !end.value) {
      throw new Error('Выберите комнату, дату, начало и окончание.');
    }

    const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/;
    if (!timePattern.test(start.value) || !timePattern.test(end.value)) {
      throw new Error('Введите начало и окончание в формате ЧЧ:ММ, например 10:30.');
    }

    const startsAt = new Date(date.value + 'T' + start.value + ':00Z');
    const endsAt = new Date(date.value + 'T' + end.value + ':00Z');
    const minutes = (endsAt.getTime() - startsAt.getTime()) / 60_000;
    if (!Number.isInteger(minutes) || minutes < 30 || minutes > 480) {
      throw new Error('Выберите длительность от 30 до 480 минут.');
    }
    if (startsAt.getTime() <= Date.now()) {
      throw new Error('Выберите время в будущем.');
    }
    if (
      start.value < '09:00' ||
      endsAt.toISOString().slice(0, 10) !== date.value ||
      endsAt.getUTCHours() * 60 + endsAt.getUTCMinutes() > 22 * 60
    ) {
      throw new Error('Выбранное время выходит за часы работы: 09:00–22:00 UTC.');
    }
    return {
      roomId: state.room.id,
      startsAt: startsAt.toISOString(),
      endsAt: endsAt.toISOString(),
    };
  }

  function updatePreview(): void {
    try {
      const value = selection();
      const minutes = (Date.parse(value.endsAt) - Date.parse(value.startsAt)) / 60_000;
      preview.textContent = 'Продолжительность: ' + minutes + ' мин. Время студии: UTC.';
    } catch (error) {
      preview.textContent = error instanceof Error ? error.message : 'Проверьте время.';
    }
  }

  start.value = '10:00';
  end.value = '11:00';
  form.addEventListener('input', updatePreview);
  form.addEventListener('change', updatePreview);
  initializeCalendar(date);
  date.addEventListener('change', updatePreview);
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = form.querySelector('button')!;
    button.disabled = true;
    try {
      await api(
        editingId ? '/bookings/' + editingId : '/bookings',
        editingId ? 'PUT' : 'POST',
        selection(),
      );
      editBookingTime(null);
      await onSaved();
      preview.textContent = 'Бронирование создано.';
    } catch (error) {
      onError(error instanceof Error ? error.message : 'Не удалось создать бронирование.');
      await onSaved();
    } finally {
      button.disabled = false;
    }
  });
}
