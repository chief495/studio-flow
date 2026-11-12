export type Role = 'Admin' | 'Manager' | 'Client';
export interface User {
  id: string;
  email: string;
  role: Role;
  tenantId: string;
}
export interface AuthResponse extends User {
  token: string;
}
export interface Tariff {
  id: string;
  name: string;
  hourlyRate: number;
}
export interface Room {
  id: string;
  name: string;
  capacity: number;
  tariffId: string | null;
  tariff?: Tariff;
  isActive: boolean;
}
export interface Booking {
  id: string;
  userId: string;
  room?: Room;
  startsAt: string;
  endsAt: string;
  status: 'Active' | 'Cancelled';
}
export interface Slot {
  startsAt: string;
  endsAt: string;
  available: boolean;
}
export interface State {
  token: string | null;
  user: User | null;
  rooms: Room[];
  tariffs: Tariff[];
  bookings: Booking[];
  room: Room | null;
  editor: 'room' | 'tariff' | null;
}
