import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';
import { AuthResult, UserProfile } from './auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const user: UserProfile = {
    id: 'ada',
    email: 'ada@example.com',
    displayName: 'Ada Lovelace',
    createdAt: '2026-01-01T00:00:00Z',
  };

  const result: AuthResult = {
    accessToken: 'a-token',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user,
  };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts with no session', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('opens a session on login and keeps it across instances', () => {
    service.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/login')).flush(result);

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.currentUser()).toEqual(user);
    expect(service.accessToken()).toBe('a-token');

    expect(TestBed.inject(AuthService).accessToken()).toBe('a-token');
  });

  it('opens a session on register too', () => {
    service.register({ email: 'ada@example.com', displayName: 'Ada', password: 'Passw0rd!' }).subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/register')).flush(result);

    expect(service.isAuthenticated()).toBeTrue();
  });

  it('leaves no session behind on logout', () => {
    service.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/login')).flush(result);

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem('taskmanager.session')).toBeNull();
  });

  it('refreshes the stored profile without dropping the token', () => {
    service.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/login')).flush(result);

    service.loadProfile().subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/me')).flush({ ...user, displayName: 'Ada Byron' });

    expect(service.currentUser()?.displayName).toBe('Ada Byron');
    expect(service.accessToken()).toBe('a-token');
  });

  it('does not open a session from a profile refresh alone', () => {
    service.loadProfile().subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/me')).flush(user);

    expect(service.isAuthenticated()).toBeFalse();
  });
});
