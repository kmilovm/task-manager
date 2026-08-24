import { AbstractControl, ValidationErrors } from '@angular/forms';

export function passwordStrength(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string | null;

  if (!value) {
    return null;
  }

  return /[A-Za-z]/.test(value) && /[0-9]/.test(value) ? null : { passwordStrength: true };
}
