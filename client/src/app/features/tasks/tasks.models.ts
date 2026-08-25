export type TaskStatus = 'Pending' | 'InProgress' | 'Done';

export const TASK_STATUSES: readonly TaskStatus[] = ['Pending', 'InProgress', 'Done'];

const STATUS_LABELS: Record<TaskStatus, string> = {
  Pending: 'Pending',
  InProgress: 'In progress',
  Done: 'Done',
};

export function statusLabel(status: TaskStatus): string {
  return STATUS_LABELS[status];
}

export interface Task {
  id: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  dueDate: string | null;
  createdAt: string;
  completedAt: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  dueDate: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  status: TaskStatus;
  dueDate: string | null;
}

export interface TaskFilter {
  status: TaskStatus | null;
  search: string;
}

export const EMPTY_FILTER: TaskFilter = { status: null, search: '' };
