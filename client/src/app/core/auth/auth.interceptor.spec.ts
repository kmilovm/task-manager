import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { AuthResult } from './auth.models';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;
  let router: jasmine.SpyObj<Router>;

  const result: AuthResult = {
    accessToken: 'a-token',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'ada', email: 'ada@example.com', displayName: 'Ada', createdAt: '2026-01-01T00:00:00Z' },
  };

  beforeEach(() => {
    localStorage.clear();
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: router },
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => backend.verify());

  function signIn(): void {
    auth.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    backend.expectOne((request) => request.url.endsWith('/auth/login')).flush(result);
  }

  it('sends no Authorization header when there is no session', () => {
    http.get('/api/tasks').subscribe();

    expect(backend.expectOne('/api/tasks').request.headers.has('Authorization')).toBeFalse();
  });

  it('attaches the bearer token once a session exists', () => {
    signIn();

    http.get('/api/tasks').subscribe();

    expect(backend.expectOne('/api/tasks').request.headers.get('Authorization')).toBe('Bearer a-token');
  });

  it('ends the session and returns to login when the server rejects the token', () => {
    signIn();

    http.get('/api/tasks').subscribe({ error: () => undefined });
    backend.expectOne('/api/tasks').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('leaves a failed sign-in alone instead of redirecting', () => {
    auth.login({ email: 'ada@example.com', password: 'wrong' }).subscribe({ error: () => undefined });
    backend
      .expectOne((request) => request.url.endsWith('/auth/login'))
      .flush({ detail: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('lets other failures through untouched', () => {
    signIn();

    http.get('/api/tasks').subscribe({ error: () => undefined });
    backend.expectOne('/api/tasks').flush(null, { status: 500, statusText: 'Server Error' });

    expect(auth.isAuthenticated()).toBeTrue();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
