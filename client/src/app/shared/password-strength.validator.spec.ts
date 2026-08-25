import { FormControl } from '@angular/forms';
import { passwordStrength } from './password-strength.validator';

describe('passwordStrength', () => {
  const validate = (value: string | null) => passwordStrength(new FormControl(value));

  it('ignores an empty value so the required rule owns that message', () => {
    expect(validate(null)).toBeNull();
    expect(validate('')).toBeNull();
  });

  it('accepts a password with a letter and a digit', () => {
    expect(validate('Passw0rd!')).toBeNull();
  });

  it('rejects a password with no digit', () => {
    expect(validate('password')).toEqual({ passwordStrength: true });
  });

  it('rejects a password with no letter', () => {
    expect(validate('12345678')).toEqual({ passwordStrength: true });
  });
});
