import { HttpErrorResponse } from '@angular/common/http';

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export interface ApiError {
  message: string;
  fieldErrors: Record<string, string[]>;
}

const FALLBACK_MESSAGE = 'Something went wrong. Please try again.';

export function toApiError(error: HttpErrorResponse): ApiError {
  if (error.status === 0) {
    return { message: 'Cannot reach the server.', fieldErrors: {} };
  }

  const problem = error.error as ProblemDetails | null;

  if (!problem) {
    return { message: FALLBACK_MESSAGE, fieldErrors: {} };
  }

  return {
    message: problem.errors ? 'Please correct the highlighted fields.' : problem.detail ?? FALLBACK_MESSAGE,
    fieldErrors: problem.errors ?? {},
  };
}
