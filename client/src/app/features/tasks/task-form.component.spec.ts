import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { TaskFormComponent } from './task-form.component';
import { Task } from './tasks.models';

describe('TaskFormComponent', () => {
  let http: HttpTestingController;

  const existing: Task = {
    id: '42',
    title: 'Write the report',
    description: 'Quarterly numbers',
    status: 'InProgress',
    dueDate: '2030-01-10',
    createdAt: '2026-01-01T00:00:00Z',
    completedAt: null,
  };

  function create(id: string | null): ComponentFixture<TaskFormComponent> {
    TestBed.configureTestingModule({
      imports: [TaskFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', id]]) } } },
      ],
    });

    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(TaskFormComponent);
    fixture.detectChanges();

    return fixture;
  }

  afterEach(() => http.verify());

  it('starts empty in create mode and asks for nothing from the server', () => {
    const component = create(null).componentInstance;

    expect(component.isEdit()).toBeFalse();
    expect(component.form.getRawValue()).toEqual({
      title: '',
      description: '',
      dueDate: '',
      status: 'Pending',
    });
  });

  it('refuses to submit without a title and marks the control as touched', () => {
    const component = create(null).componentInstance;

    component.submit();

    expect(component.form.controls.title.touched).toBeTrue();
    http.expectNone((request) => request.url.endsWith('/tasks'));
  });

  it('rejects a title longer than the server would accept', () => {
    const component = create(null).componentInstance;

    component.form.controls.title.setValue('a'.repeat(201));

    expect(component.form.controls.title.hasError('maxlength')).toBeTrue();
  });

  it('sends a blank description and due date as null rather than empty strings', () => {
    const component = create(null).componentInstance;

    component.form.controls.title.setValue('Book the room');
    component.form.controls.description.setValue('   ');
    component.submit();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/tasks'));

    expect(request.request.body).toEqual({ title: 'Book the room', description: null, dueDate: null });
    request.flush(existing);
  });

  it('never sends a status when creating, because a new task is always pending', () => {
    const component = create(null).componentInstance;

    component.form.controls.title.setValue('Book the room');
    component.submit();

    const request = http.expectOne((candidate) => candidate.url.endsWith('/tasks'));

    expect(Object.keys(request.request.body as object)).not.toContain('status');
    request.flush(existing);
  });

  it('loads the task into the form in edit mode', () => {
    const fixture = create('42');
    http.expectOne((request) => request.url.endsWith('/tasks/42')).flush(existing);
    fixture.detectChanges();

    expect(fixture.componentInstance.isEdit()).toBeTrue();
    expect(fixture.componentInstance.form.getRawValue()).toEqual({
      title: 'Write the report',
      description: 'Quarterly numbers',
      dueDate: '2030-01-10',
      status: 'InProgress',
    });
  });

  it('sends every field on save, so clearing a due date actually clears it', () => {
    const component = create('42').componentInstance;
    http.expectOne((request) => request.url.endsWith('/tasks/42')).flush(existing);

    component.form.controls.dueDate.setValue('');
    component.submit();

    const request = http.expectOne((candidate) => candidate.method === 'PUT');

    expect(request.request.body).toEqual({
      title: 'Write the report',
      description: 'Quarterly numbers',
      status: 'InProgress',
      dueDate: null,
    });
    request.flush(existing);
  });

  it('shows the server message when a rule it does not enforce is broken', () => {
    const component = create(null).componentInstance;

    component.form.controls.title.setValue('Late task');
    component.form.controls.dueDate.setValue('2020-01-01');
    component.submit();

    http
      .expectOne((candidate) => candidate.url.endsWith('/tasks'))
      .flush({ detail: 'Due date cannot be in the past.' }, { status: 400, statusText: 'Bad Request' });

    expect(component.errorMessage()).toBe('Due date cannot be in the past.');
    expect(component.submitting()).toBeFalse();
  });

  it('goes to the list once the save succeeds', () => {
    const component = create(null).componentInstance;
    const router = spyOn(TestBed.inject(Router), 'navigateByUrl');

    component.form.controls.title.setValue('Book the room');
    component.submit();
    http.expectOne((candidate) => candidate.url.endsWith('/tasks')).flush(existing);

    expect(router).toHaveBeenCalledWith('/tasks');
  });

  it('reports a task that is gone rather than showing an empty form', () => {
    const component = create('42').componentInstance;

    http
      .expectOne((request) => request.url.endsWith('/tasks/42'))
      .flush({ detail: 'Task not found.' }, { status: 404, statusText: 'Not Found' });

    expect(component.notFound()).toBeTrue();
  });
});
