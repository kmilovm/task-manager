import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { toApiError } from '../../core/http/problem-details';
import { FieldErrorComponent } from '../../shared/field-error.component';
import { TASK_STATUSES, Task, TaskStatus, statusLabel } from './tasks.models';
import { TasksService } from './tasks.service';

@Component({
  selector: 'app-task-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, FieldErrorComponent],
  templateUrl: './task-form.component.html',
})
export class TaskFormComponent implements OnInit {
  private readonly tasks = inject(TasksService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
    dueDate: [''],
    status: ['Pending' as TaskStatus],
  });

  readonly statuses = TASK_STATUSES;

  readonly taskId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly notFound = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});

  readonly isEdit = computed(() => this.taskId() !== null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id === null) {
      return;
    }

    this.taskId.set(id);
    this.loading.set(true);

    this.tasks.get(id).subscribe({
      next: (task) => {
        this.fill(task);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) {
          this.notFound.set(true);
        } else {
          this.errorMessage.set(toApiError(error).message);
        }

        this.loading.set(false);
      },
    });
  }

  label(status: TaskStatus): string {
    return statusLabel(status);
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.fieldErrors.set({});

    this.save().subscribe({
      next: () => void this.router.navigateByUrl('/tasks'),
      error: (error: HttpErrorResponse) => {
        const apiError = toApiError(error);

        this.errorMessage.set(apiError.message);
        this.fieldErrors.set(apiError.fieldErrors);
        this.submitting.set(false);
      },
    });
  }

  private save(): Observable<Task> {
    const value = this.form.getRawValue();
    const description = value.description.trim().length === 0 ? null : value.description.trim();
    const dueDate = value.dueDate.length === 0 ? null : value.dueDate;
    const id = this.taskId();

    // PUT replaces the whole task, so every field goes on every save: a cleared description or
    // due date has to be sent as null rather than left out.
    return id === null
      ? this.tasks.create({ title: value.title, description, dueDate })
      : this.tasks.update(id, { title: value.title, description, status: value.status, dueDate });
  }

  private fill(task: Task): void {
    this.form.setValue({
      title: task.title,
      description: task.description ?? '',
      dueDate: task.dueDate ?? '',
      status: task.status,
    });
  }
}
