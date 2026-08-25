import { TokenStorage } from './token-storage';
import { Session } from './auth.models';

describe('TokenStorage', () => {
  const storage = new TokenStorage();

  const session = (expiresAt: string): Session => ({
    accessToken: 'token',
    expiresAt,
    user: { id: 'id', email: 'ada@example.com', displayName: 'Ada Lovelace', createdAt: '2026-01-01T00:00:00Z' },
  });

  beforeEach(() => localStorage.clear());

  it('returns null when nothing was stored', () => {
    expect(storage.read()).toBeNull();
  });

  it('round-trips a session that has not expired', () => {
    const stored = session(new Date(Date.now() + 60_000).toISOString());

    storage.write(stored);

    expect(storage.read()).toEqual(stored);
  });

  it('discards a session whose token has already expired', () => {
    storage.write(session(new Date(Date.now() - 1_000).toISOString()));

    expect(storage.read()).toBeNull();
  });
});
