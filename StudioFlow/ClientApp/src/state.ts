import type { State } from './models.js';
export const state: State = {
  token: sessionStorage.getItem('studioflow_token'),
  user: null,
  rooms: [],
  tariffs: [],
  bookings: [],
  room: null,
  editor: null,
};
try {
  state.user = JSON.parse(sessionStorage.getItem('studioflow_user') || 'null');
} catch {
  /* Invalid stored session is discarded below. */
}
if (!state.user) state.token = null;
export const admin = () => ['Admin', 'Manager'].includes(state.user?.role || '');
