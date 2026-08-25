import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/problem-details';
import { EMPTY_FILTER, Task, TaskFilter } from './tasks.models';
import { TasksService } from './tasks.service';

@Injectable({ providedIn: 'root' })
export class TasksStore {
  private readonly tasks = inject(TasksService);
  private readonly auth = inject(AuthService);

  private readonly items = signal<Task[]>([]);
  private readonly loading = signal(false);
  private readonly loaded = signal(false);
  private readonly error = signal<string | null>(null);
  private readonly deleting = signal<string | null>(null);
  private readonly filter = signal<TaskFilter>(EMPTY_FILTER);

  readonly tasksList = this.items.asReadonly();
  readonly isLoading = this.loading.asReadonly();
  readonly errorMessage = this.error.asReadonly();
  readonly deletingId = this.deleting.asReadonly();
  readonly currentFilter = this.filter.asReadonly();

  readonly isFiltered = computed(() => {
    const filter = this.filter();

    return filter.status !== null || filter.search.trim().length > 0;
  });

  readonly isEmpty = computed(() => this.loaded() && !this.loading() && this.items().length === 0);

  constructor() {
    // Signing out must not leave one account's rows visible to the next one. The store watches
    // the session itself so no other component has to remember to clear it.
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        this.reset();
      }
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.tasks.list(this.filter()).subscribe({
      next: (tasks) => {
        this.items.set(tasks);
        this.loaded.set(true);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(toApiError(error).message);
        this.loaded.set(true);
        this.loading.set(false);
      },
    });
  }

  applyFilter(filter: TaskFilter): void {
    this.filter.set(filter);
    this.load();
  }

  clearFilter(): void {
    this.applyFilter(EMPTY_FILTER);
  }

  remove(id: string): void {
    this.deleting.set(id);
    this.error.set(null);

    this.tasks.remove(id).subscribe({
      next: () => {
        this.items.update((tasks) => tasks.filter((task) => task.id !== id));
        this.deleting.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(toApiError(error).message);
        this.deleting.set(null);
      },
    });
  }

  /** Drops everything held for the signed-in account, so the next reader starts from scratch. */
  reset(): void {
    this.items.set([]);
    this.filter.set(EMPTY_FILTER);
    this.error.set(null);
    this.deleting.set(null);
    this.loaded.set(false);
  }
}
