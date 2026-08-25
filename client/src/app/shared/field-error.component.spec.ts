import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { FieldErrorComponent } from './field-error.component';

@Component({
  selector: 'app-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FieldErrorComponent],
  template: `<app-field-error [control]="control" [serverErrors]="serverErrors()" label="Email" />`,
})
class HostComponent {
  readonly control = new FormControl('', [Validators.required, Validators.email]);
  readonly serverErrors = signal<string[]>([]);
}

describe('FieldErrorComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  const rendered = () =>
    ((fixture.nativeElement as HTMLElement).querySelector('.field-error')?.textContent ?? '').trim();

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('says nothing about a control the user has not reached yet', () => {
    expect(rendered()).toBe('');
  });

  it('renders the message when the control is touched, even under OnPush', () => {
    fixture.componentInstance.control.markAsTouched();
    fixture.detectChanges();

    expect(rendered()).toBe('Email is required.');
  });

  it('renders the message when the control becomes dirty', () => {
    fixture.componentInstance.control.setValue('not-an-email');
    fixture.componentInstance.control.markAsDirty();
    fixture.detectChanges();

    expect(rendered()).toBe('Enter a valid email address.');
  });

  it('stops showing the message once the value is corrected', () => {
    fixture.componentInstance.control.markAsTouched();
    fixture.detectChanges();
    expect(rendered()).not.toBe('');

    fixture.componentInstance.control.setValue('ada@example.com');
    fixture.detectChanges();

    expect(rendered()).toBe('');
  });

  it('shows a server error the client rules did not catch', () => {
    fixture.componentInstance.serverErrors.set(['That address is already registered.']);
    fixture.detectChanges();

    expect(rendered()).toBe('That address is already registered.');
  });

  it('announces the message so a screen reader reads it', () => {
    fixture.componentInstance.control.markAsTouched();
    fixture.detectChanges();

    const element = (fixture.nativeElement as HTMLElement).querySelector('.field-error');

    expect(element?.getAttribute('role')).toBe('alert');
  });
});
