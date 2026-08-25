import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TasksService } from './tasks.service';
import { EMPTY_FILTER } from './tasks.models';

describe('TasksService', () => {
  let service: TasksService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TasksService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('omits both query parameters when nothing is filtered', () => {
    service.list(EMPTY_FILTER).subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/tasks'));

    expect(request.request.params.keys()).toEqual([]);
    request.flush([]);
  });

  it('sends the status verbatim so the enum matches the server vocabulary', () => {
    service.list({ status: 'InProgress', search: '' }).subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/tasks'));

    expect(request.request.params.get('status')).toBe('InProgress');
    expect(request.request.params.has('search')).toBeFalse();
    request.flush([]);
  });

  it('trims the search term and drops it when it is only whitespace', () => {
    service.list({ status: null, search: '   ' }).subscribe();
    http.expectOne((candidate) => candidate.url.endsWith('/tasks') && !candidate.params.has('search')).flush([]);

    service.list({ status: null, search: '  report  ' }).subscribe();
    const request = http.expectOne((candidate) => candidate.params.get('search') === 'report');

    request.flush([]);
  });

  it('combines status and search', () => {
    service.list({ status: 'Done', search: 'report' }).subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/tasks'));

    expect(request.request.params.get('status')).toBe('Done');
    expect(request.request.params.get('search')).toBe('report');
    request.flush([]);
  });

  it('uses the verb and url each operation expects', () => {
    service.get('42').subscribe();
    const read = http.expectOne((r) => r.url.endsWith('/tasks/42'));
    expect(read.request.method).toBe('GET');
    read.flush({});

    service.create({ title: 'New', description: null, dueDate: null }).subscribe();
    const created = http.expectOne((r) => r.url.endsWith('/tasks'));
    expect(created.request.method).toBe('POST');
    created.flush({});

    service.update('42', { title: 'New', description: null, status: 'Done', dueDate: null }).subscribe();
    const replaced = http.expectOne((r) => r.url.endsWith('/tasks/42'));
    expect(replaced.request.method).toBe('PUT');
    replaced.flush({});

    service.remove('42').subscribe();
    const removed = http.expectOne((r) => r.url.endsWith('/tasks/42'));
    expect(removed.request.method).toBe('DELETE');
    removed.flush(null);
  });

  it('sends every field on update, because PUT replaces the whole task', () => {
    service.update('42', { title: 'Title', description: null, status: 'Pending', dueDate: null }).subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/tasks/42'));

    expect(Object.keys(request.request.body as object).sort())
      .toEqual(['description', 'dueDate', 'status', 'title']);
    request.flush({});
  });
});
