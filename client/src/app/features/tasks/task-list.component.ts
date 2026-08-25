import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { TASK_STATUSES, Task, TaskStatus, statusLabel } from './tasks.models';
import { TasksStore } from './tasks.store';

@Component({
  selector: 'app-task-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './task-list.component.html',
})
export class TaskListComponent implements OnInit {
  private readonly store = inject(TasksStore);

  readonly tasks = this.store.tasksList;
  readonly loading = this.store.isLoading;
  readonly errorMessage = this.store.errorMessage;
  readonly deletingId = this.store.deletingId;
  readonly isFiltered = this.store.isFiltered;
  readonly isEmpty = this.store.isEmpty;

  readonly statuses = TASK_STATUSES;
  readonly pendingDelete = signal<string | null>(null);

  readonly filters = inject(FormBuilder).nonNullable.group({
    search: [''],
    status: ['' as TaskStatus | ''],
  });

  constructor() {
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe((value) => {
      this.store.applyFilter({
        search: value.search ?? '',
        status: value.status === '' || value.status === undefined ? null : value.status,
      });
    });
  }

  ngOnInit(): void {
    this.filters.setValue(
      {
        search: this.store.currentFilter().search,
        status: this.store.currentFilter().status ?? '',
      },
      { emitEvent: false },
    );

    this.store.load();
  }

  label(status: TaskStatus): string {
    return statusLabel(status);
  }

  /** Renders a "yyyy-MM-dd" due date in the reader's own timezone rather than shifting it by one. */
  asLocalDate(value: string): Date {
    return new Date(`${value}T00:00:00`);
  }

  askDelete(task: Task): void {
    this.pendingDelete.set(task.id);
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  confirmDelete(task: Task): void {
    this.pendingDelete.set(null);
    this.store.remove(task.id);
  }

  clearFilters(): void {
    this.filters.reset({ search: '', status: '' });
  }

  retry(): void {
    this.store.load();
  }
}
