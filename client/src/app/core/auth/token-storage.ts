import { Injectable } from '@angular/core';
import { Session } from './auth.models';

const STORAGE_KEY = 'taskmanager.session';

@Injectable({ providedIn: 'root' })
export class TokenStorage {
  read(): Session | null {
    const raw = localStorage.getItem(STORAGE_KEY);

    if (!raw) {
      return null;
    }

    try {
      const session = JSON.parse(raw) as Session;

      return this.hasExpired(session) ? null : session;
    } catch {
      this.clear();
      return null;
    }
  }

  write(session: Session): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }

  clear(): void {
    localStorage.removeItem(STORAGE_KEY);
  }

  private hasExpired(session: Session): boolean {
    return new Date(session.expiresAt).getTime() <= Date.now();
  }
}
