import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './problem-details';

describe('toApiError', () => {
  const response = (status: number, error: unknown) =>
    new HttpErrorResponse({ status, error });

  it('reports an unreachable server distinctly from a rejected request', () => {
    expect(toApiError(response(0, null)).message).toBe('Cannot reach the server.');
  });

  it('uses the problem detail as the message', () => {
    const error = toApiError(response(409, { detail: 'An account with this email already exists.' }));

    expect(error.message).toBe('An account with this email already exists.');
    expect(error.fieldErrors).toEqual({});
  });

  it('surfaces validation failures as field errors', () => {
    const error = toApiError(
      response(400, { errors: { password: ['Password must contain a digit.'] } }),
    );

    expect(error.fieldErrors['password']).toEqual(['Password must contain a digit.']);
    expect(error.message).toBe('Please correct the highlighted fields.');
  });

  it('falls back to a generic message when the body carries nothing useful', () => {
    expect(toApiError(response(500, null)).message).toBe('Something went wrong. Please try again.');
  });
});
