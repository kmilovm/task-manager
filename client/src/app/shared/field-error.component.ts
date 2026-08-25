import { ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';

@Component({
  selector: 'app-field-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (message(); as text) {
      <span class="field-error" role="alert">{{ text }}</span>
    }
  `,
})
export class FieldErrorComponent {
  private readonly changeDetector = inject(ChangeDetectorRef);

  readonly control = input.required<AbstractControl>();
  readonly serverErrors = input<string[]>([]);
  readonly label = input('This field');

  constructor() {
    // A FormControl is not a signal: marking it touched or invalid changes no input, so an OnPush
    // view would never be re-rendered and the message would stay invisible.
    effect((onCleanup) => {
      const subscription = this.control().events.subscribe(() => this.changeDetector.markForCheck());

      onCleanup(() => subscription.unsubscribe());
    });
  }

  message(): string | null {
    const control = this.control();

    if (!control.touched && !control.dirty) {
      return this.serverErrors()[0] ?? null;
    }

    const errors = control.errors;

    if (!errors) {
      return this.serverErrors()[0] ?? null;
    }

    if (errors['required']) {
      return `${this.label()} is required.`;
    }

    if (errors['email']) {
      return 'Enter a valid email address.';
    }

    if (errors['maxlength']) {
      return `${this.label()} cannot exceed ${errors['maxlength'].requiredLength} characters.`;
    }

    if (errors['minlength']) {
      return `${this.label()} must be at least ${errors['minlength'].requiredLength} characters.`;
    }

    if (errors['passwordStrength']) {
      return 'Password must contain a letter and a digit.';
    }

    return this.serverErrors()[0] ?? null;
  }
}
