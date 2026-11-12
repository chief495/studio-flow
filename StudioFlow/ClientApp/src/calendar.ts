/** An inline calendar with explicit outside-click and Escape dismissal. */
export function initializeCalendar(input: HTMLInputElement): void {
  input.type = 'text';
  input.readOnly = true;
  input.setAttribute('aria-label', 'Дата бронирования');
  input.setAttribute('aria-haspopup', 'dialog');
  const wrapper = document.createElement('div');
  wrapper.className = 'calendar-anchor';
  input.before(wrapper);
  wrapper.append(input);
  input.hidden = true;
  const pickerButton = document.createElement('button');
  pickerButton.type = 'button';
  pickerButton.className = 'date-picker-button';
  pickerButton.textContent = 'Выбрать дату';
  pickerButton.setAttribute('aria-haspopup', 'dialog');
  pickerButton.setAttribute('aria-expanded', 'false');
  wrapper.append(pickerButton);

  const strip = document.createElement('div');
  strip.className = 'date-strip';
  strip.setAttribute('role', 'group');
  strip.setAttribute('aria-label', 'Дата бронирования');
  const control = wrapper.closest('.date-control');
  if (control) {
    control.classList.add('date-control-strip');
    control.querySelector('label')?.remove();
    control.prepend(strip);
  }
  const today = new Date().toISOString().slice(0, 10);
  for (let offset = 0; offset < 4; offset++) {
    const day = new Date(today + 'T12:00:00Z');
    day.setUTCDate(day.getUTCDate() + offset);
    const value = day.toISOString().slice(0, 10);
    const button = document.createElement('button');
    button.type = 'button';
    button.dataset.date = value;
    const title = document.createElement('strong');
    title.textContent =
      offset === 0
        ? 'Сегодня'
        : offset === 1
          ? 'Завтра'
          : day.toLocaleDateString('ru-RU', { weekday: 'long', timeZone: 'UTC' });
    const subtitle = document.createElement('span');
    subtitle.textContent = day.toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'long',
      timeZone: 'UTC',
    });
    button.append(title, subtitle);
    button.onclick = () => {
      input.value = value;
      input.dispatchEvent(new Event('change', { bubbles: true }));
      close();
    };
    strip.append(button);
  }
  function syncSelection(): void {
    let isQuickDate = false;
    strip.querySelectorAll<HTMLButtonElement>('button').forEach((button) => {
      const selected = button.dataset.date === input.value;
      button.classList.toggle('selected', selected);
      button.setAttribute('aria-pressed', String(selected));
      isQuickDate ||= selected;
    });
    pickerButton.classList.toggle('selected', Boolean(input.value) && !isQuickDate);
    pickerButton.textContent =
      !isQuickDate && input.value
        ? new Date(input.value + 'T12:00:00Z').toLocaleDateString('ru-RU', {
            day: 'numeric',
            month: 'long',
            timeZone: 'UTC',
          })
        : 'Выбрать дату';
  }
  input.addEventListener('change', syncSelection);
  input.addEventListener('calendar-sync', syncSelection);
  syncSelection();
  const popup = document.createElement('div');
  popup.className = 'calendar-popup';
  popup.hidden = true;
  popup.setAttribute('role', 'dialog');
  popup.setAttribute('aria-label', 'Выбор даты');
  popup.id = 'booking-calendar';
  pickerButton.setAttribute('aria-controls', popup.id);
  wrapper.append(popup);
  let month = new Date();

  function close(): void {
    popup.hidden = true;
    input.setAttribute('aria-expanded', 'false');
    pickerButton.setAttribute('aria-expanded', 'false');
  }

  function render(): void {
    popup.replaceChildren();
    const heading = document.createElement('div');
    heading.className = 'calendar-heading';
    const title = document.createElement('strong');
    title.textContent = month.toLocaleDateString('ru-RU', {
      month: 'long',
      year: 'numeric',
    });
    heading.append(title);
    for (const offset of [-1, 1]) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = offset < 0 ? '‹' : '›';
      button.setAttribute('aria-label', offset < 0 ? 'Предыдущий месяц' : 'Следующий месяц');
      button.onclick = () => {
        month = new Date(month.getFullYear(), month.getMonth() + offset, 1);
        render();
      };
      if (offset < 0 && input.min) {
        const firstAllowedMonth = input.min.slice(0, 7);
        const displayedMonth =
          month.getFullYear() + '-' + String(month.getMonth() + 1).padStart(2, '0');
        button.disabled = displayedMonth <= firstAllowedMonth;
      }
      heading.append(button);
    }
    popup.append(heading);
    const grid = document.createElement('div');
    grid.className = 'calendar-days';
    for (const name of ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс']) {
      const label = document.createElement('span');
      label.textContent = name;
      grid.append(label);
    }
    const year = month.getFullYear();
    const index = month.getMonth();
    const padding = (new Date(year, index, 1).getDay() + 6) % 7;
    for (let cell = 0; cell < 42; cell++) {
      const date = new Date(Date.UTC(year, index, cell - padding + 1, 12));
      const day = date.getUTCDate();
      const value = date.toISOString().slice(0, 10);
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = String(day);
      button.disabled = value < input.min;
      button.classList.toggle('selected', value === input.value);
      button.classList.toggle('outside-month', date.getUTCMonth() !== index);
      button.setAttribute(
        'aria-label',
        date.toLocaleDateString('ru-RU', {
          dateStyle: 'full',
          timeZone: 'UTC',
        }),
      );
      button.setAttribute('aria-pressed', String(value === input.value));
      if (value === today) button.setAttribute('aria-current', 'date');
      button.onclick = () => {
        input.value = value;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        close();
        pickerButton.focus();
      };
      grid.append(button);
    }
    popup.append(grid);
  }

  pickerButton.addEventListener('click', () => {
    if (!popup.hidden) {
      close();
      return;
    }
    month = input.value ? new Date(input.value + 'T12:00:00Z') : new Date();
    render();
    popup.hidden = false;
    input.setAttribute('aria-expanded', 'true');
    pickerButton.setAttribute('aria-expanded', 'true');
  });
  input.form?.addEventListener('reset', close);
  document.querySelectorAll('[data-tab]').forEach((button) => {
    button.addEventListener('click', close);
  });
  document.addEventListener('pointerdown', (event) => {
    if (event.target instanceof Node && !wrapper.contains(event.target)) close();
  });
  document.addEventListener('focusin', (event) => {
    if (event.target instanceof Node && !wrapper.contains(event.target)) close();
  });
  wrapper.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      close();
      pickerButton.focus();
    }
  });
}
