import { InjectionToken } from '@angular/core';
import type { PasswordAuthProvider } from '../auth/firebase-auth';

export const PASSWORD_AUTH = new InjectionToken<PasswordAuthProvider>('PASSWORD_AUTH');
