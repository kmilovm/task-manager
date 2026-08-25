import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideRouter } from '@angular/router';
import { authGuard, guestGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { AuthResult } from './auth.models';

describe('route guards', () => {
  let auth: AuthService;
  let backend: HttpTestingController;

  const state = { url: '/tasks/42' } as RouterStateSnapshot;
  const route = {} as ActivatedRouteSnapshot;

  const result: AuthResult = {
    accessToken: 'a-token',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'ada', email: 'ada@example.com', displayName: 'Ada', createdAt: '2026-01-01T00:00:00Z' },
  };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  function signIn(): void {
    auth.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    backend.expectOne((request) => request.url.endsWith('/auth/login')).flush(result);
  }

  it('sends a signed-out visitor to login, remembering where they were going', () => {
    const result = TestBed.runInInjectionContext(() => authGuard(route, state));

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree))
      .toBe('/login?returnUrl=%2Ftasks%2F42');
  });

  it('lets a signed-in visitor through', () => {
    signIn();

    expect(TestBed.runInInjectionContext(() => authGuard(route, state))).toBeTrue();
  });

  it('keeps a signed-in visitor away from the login page', () => {
    signIn();

    const result = TestBed.runInInjectionContext(() => guestGuard(route, state));

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
  });

  it('lets a signed-out visitor reach the login page', () => {
    expect(TestBed.runInInjectionContext(() => guestGuard(route, state))).toBeTrue();
  });
});
