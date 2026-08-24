import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResult, LoginRequest, RegisterRequest, Session, UserProfile } from './auth.models';
import { TokenStorage } from './token-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorage);
  private readonly session = signal<Session | null>(this.storage.read());

  readonly currentUser = computed<UserProfile | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  register(request: RegisterRequest): Observable<AuthResult> {
    return this.http
      .post<AuthResult>(`${environment.apiBaseUrl}/auth/register`, request)
      .pipe(tap((result) => this.startSession(result)));
  }

  login(request: LoginRequest): Observable<AuthResult> {
    return this.http
      .post<AuthResult>(`${environment.apiBaseUrl}/auth/login`, request)
      .pipe(tap((result) => this.startSession(result)));
  }

  loadProfile(): Observable<UserProfile> {
    return this.http
      .get<UserProfile>(`${environment.apiBaseUrl}/auth/me`)
      .pipe(tap((user) => this.refreshUser(user)));
  }

  logout(): void {
    this.storage.clear();
    this.session.set(null);
  }

  private refreshUser(user: UserProfile): void {
    const session = this.session();

    if (!session) {
      return;
    }

    const refreshed: Session = { ...session, user };

    this.storage.write(refreshed);
    this.session.set(refreshed);
  }

  private startSession(result: AuthResult): void {
    const session: Session = {
      accessToken: result.accessToken,
      expiresAt: result.expiresAt,
      user: result.user,
    };

    this.storage.write(session);
    this.session.set(session);
  }
}
