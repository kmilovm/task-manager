import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { TaskListComponent } from './task-list.component';
import { Task } from './tasks.models';

describe('TaskListComponent', () => {
  let fixture: ComponentFixture<TaskListComponent>;
  let http: HttpTestingController;

  const task = (id: string, title: string, overrides: Partial<Task> = {}): Task => ({
    id,
    title,
    description: null,
    status: 'Pending',
    dueDate: null,
    createdAt: '2026-01-01T00:00:00Z',
    completedAt: null,
    ...overrides,
  });

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      imports: [TaskListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TaskListComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function respondWith(tasks: Task[]): void {
    http.expectOne((request) => request.url.endsWith('/tasks')).flush(tasks);
    fixture.detectChanges();
  }

  const rows = () => fixture.debugElement.queryAll(By.css('tbody tr'));
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('renders one row per task, in the order the server returned', () => {
    respondWith([task('1', 'Review the deck'), task('2', 'Write the report')]);

    expect(rows().length).toBe(2);
    expect(rows()[0]?.nativeElement.textContent).toContain('Review the deck');
    expect(rows()[1]?.nativeElement.textContent).toContain('Write the report');
  });

  it('shows the status as words rather than the wire value', () => {
    respondWith([task('1', 'Review the deck', { status: 'InProgress' })]);

    expect(text()).toContain('In progress');
  });

  it('says a task has no due date instead of leaving the cell blank', () => {
    respondWith([task('1', 'Review the deck')]);

    expect(text()).toContain('No due date');
  });

  it('tells an empty account apart from an empty filter', fakeAsync(() => {
    respondWith([]);

    const emptyMessage = text();

    fixture.componentInstance.filters.controls.status.setValue('Done');
    tick(250);
    respondWith([]);

    expect(text()).not.toBe(emptyMessage);
    expect(fixture.componentInstance.isFiltered()).toBeTrue();
  }));

  it('asks before deleting and does nothing until it is confirmed', () => {
    respondWith([task('1', 'Review the deck')]);

    fixture.componentInstance.askDelete(task('1', 'Review the deck'));
    fixture.detectChanges();

    expect(text()).toContain('Delete this task?');
    http.expectNone((request) => request.url.endsWith('/tasks/1'));
  });

  it('leaves the task alone when the confirmation is dismissed', () => {
    respondWith([task('1', 'Review the deck')]);

    fixture.componentInstance.askDelete(task('1', 'Review the deck'));
    fixture.componentInstance.cancelDelete();
    fixture.detectChanges();

    expect(text()).not.toContain('Delete this task?');
    expect(rows().length).toBe(1);
  });

  it('removes the row once the deletion is confirmed', () => {
    respondWith([task('1', 'Review the deck'), task('2', 'Write the report')]);

    fixture.componentInstance.confirmDelete(task('1', 'Review the deck'));
    http.expectOne((request) => request.url.endsWith('/tasks/1')).flush(null);
    fixture.detectChanges();

    expect(rows().length).toBe(1);
    expect(text()).not.toContain('Review the deck');
  });

  it('keeps the table in place while refreshing, so filtering does not shift the layout', fakeAsync(() => {
    respondWith([task('1', 'Review the deck')]);

    const tableBefore = fixture.debugElement.query(By.css('.task-table')).nativeElement as HTMLElement;
    const topBefore = tableBefore.offsetTop;

    fixture.componentInstance.filters.controls.status.setValue('Done');
    tick(250);
    fixture.detectChanges();

    const table = fixture.debugElement.query(By.css('.task-table'));

    expect(table).not.toBeNull();
    expect(table.nativeElement).toBe(tableBefore);
    expect((table.nativeElement as HTMLElement).offsetTop).toBe(topBefore);
    expect(table.nativeElement.classList).toContain('is-refreshing');

    http.expectOne((request) => request.url.endsWith('/tasks')).flush([]);
    fixture.detectChanges();
  }));

  it('announces the refresh to assistive technology without occupying space', fakeAsync(() => {
    respondWith([task('1', 'Review the deck')]);

    const status = fixture.debugElement.query(By.css('[role="status"]'));

    expect(status.nativeElement.classList).toContain('visually-hidden');

    fixture.componentInstance.filters.controls.status.setValue('Done');
    tick(250);
    fixture.detectChanges();

    expect(status.nativeElement.textContent.trim()).toBe('Refreshing your tasks');

    http.expectOne((request) => request.url.endsWith('/tasks')).flush([]);
    fixture.detectChanges();
  }));

  it('debounces the filter into a single request', fakeAsync(() => {
    respondWith([]);

    fixture.componentInstance.filters.controls.search.setValue('re');
    fixture.componentInstance.filters.controls.search.setValue('rep');
    fixture.componentInstance.filters.controls.search.setValue('report');
    tick(250);

    const request = http.expectOne((candidate) => candidate.params.get('search') === 'report');
    request.flush([]);
    fixture.detectChanges();
  }));

  it('renders a due date on the reader s own day rather than shifting it back', () => {
    const rendered = fixture.componentInstance.asLocalDate('2030-01-10');

    expect(rendered.getFullYear()).toBe(2030);
    expect(rendered.getMonth()).toBe(0);
    expect(rendered.getDate()).toBe(10);

    respondWith([]);
  });
});
