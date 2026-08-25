import { TestBed } from '@angular/core/testing';
import { ApplicationRef } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TasksStore } from './tasks.store';
import { AuthService } from '../../core/auth/auth.service';
import { AuthResult } from '../../core/auth/auth.models';
import { Task } from './tasks.models';

describe('TasksStore', () => {
  let store: TasksStore;
  let http: HttpTestingController;
  let auth: AuthService;

  const task = (id: string, title: string): Task => ({
    id,
    title,
    description: null,
    status: 'Pending',
    dueDate: null,
    createdAt: '2026-01-01T00:00:00Z',
    completedAt: null,
  });

  const credentials: AuthResult = {
    accessToken: 'a-token',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'ada', email: 'ada@example.com', displayName: 'Ada', createdAt: '2026-01-01T00:00:00Z' },
  };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    auth = TestBed.inject(AuthService);
    store = TestBed.inject(TasksStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settleEffects = () => TestBed.inject(ApplicationRef).tick();

  function listReturns(tasks: Task[]): void {
    http.expectOne((request) => request.url.endsWith('/tasks')).flush(tasks);
  }

  it('holds nothing before the first load', () => {
    expect(store.tasksList()).toEqual([]);
    expect(store.isLoading()).toBeFalse();
    expect(store.isEmpty()).toBeFalse();
  });

  it('reports loading while the request is in flight', () => {
    store.load();

    expect(store.isLoading()).toBeTrue();

    listReturns([task('1', 'Write the report')]);

    expect(store.isLoading()).toBeFalse();
    expect(store.tasksList().length).toBe(1);
  });

  it('keeps the order the server returned', () => {
    store.load();
    listReturns([task('1', 'Review the deck'), task('2', 'Write the report')]);

    expect(store.tasksList().map((item) => item.title)).toEqual(['Review the deck', 'Write the report']);
  });

  it('distinguishes an empty account from an empty filter', () => {
    store.load();
    listReturns([]);

    expect(store.isEmpty()).toBeTrue();
    expect(store.isFiltered()).toBeFalse();

    store.applyFilter({ status: 'Done', search: '' });
    listReturns([]);

    expect(store.isFiltered()).toBeTrue();
  });

  it('treats a whitespace-only search as no filter at all', () => {
    store.applyFilter({ status: null, search: '   ' });
    listReturns([]);

    expect(store.isFiltered()).toBeFalse();
  });

  it('surfaces the server message when loading fails', () => {
    store.load();
    http
      .expectOne((request) => request.url.endsWith('/tasks'))
      .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Server Error' });

    expect(store.errorMessage()).toBe('Something broke.');
    expect(store.isLoading()).toBeFalse();
  });

  it('drops a deleted task without refetching the list', () => {
    store.load();
    listReturns([task('1', 'Review the deck'), task('2', 'Write the report')]);

    store.remove('1');
    expect(store.deletingId()).toBe('1');

    http.expectOne((request) => request.url.endsWith('/tasks/1')).flush(null);

    expect(store.tasksList().map((item) => item.id)).toEqual(['2']);
    expect(store.deletingId()).toBeNull();
  });

  it('keeps the task and reports the reason when deleting fails', () => {
    store.load();
    listReturns([task('1', 'Review the deck')]);

    store.remove('1');
    http
      .expectOne((request) => request.url.endsWith('/tasks/1'))
      .flush({ detail: 'Task not found.' }, { status: 404, statusText: 'Not Found' });

    expect(store.tasksList().length).toBe(1);
    expect(store.errorMessage()).toBe('Task not found.');
    expect(store.deletingId()).toBeNull();
  });

  it('clears itself when the session ends, so the next account starts empty', () => {
    auth.login({ email: 'ada@example.com', password: 'Passw0rd!' }).subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/login')).flush(credentials);
    settleEffects();

    store.applyFilter({ status: 'Done', search: 'report' });
    listReturns([task('1', 'Archive last sprint')]);

    auth.logout();
    settleEffects();

    expect(store.tasksList()).toEqual([]);
    expect(store.currentFilter()).toEqual({ status: null, search: '' });
  });
});
