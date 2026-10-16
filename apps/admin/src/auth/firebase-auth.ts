import { type AuthProvider, type AuthUser, type RuntimeConfig, SignInError } from '@platform/api-client';
import { initializeApp } from 'firebase/app';
import {
  type Auth,
  browserLocalPersistence,
  connectAuthEmulator,
  getAuth,
  onIdTokenChanged,
  sendPasswordResetEmail,
  setPersistence,
  signInWithEmailAndPassword,
  signOut,
} from 'firebase/auth';

/** Admin app sign-in: email + password (TOTP MFA arrives in slice S12). The only admin file that imports Firebase. */
export interface PasswordAuthProvider extends AuthProvider {
  signInWithPassword(email: string, password: string): Promise<void>;
  sendPasswordReset(email: string): Promise<void>;
}

const MESSAGES: Record<string, string> = {
  'auth/invalid-credential': 'El correo o la contraseña no son correctos.',
  'auth/invalid-email': 'Introduce un correo válido.',
  'auth/user-disabled': 'Tu cuenta está desactivada. Contacta al administrador.',
  'auth/too-many-requests': 'Demasiados intentos. Espera unos minutos e inténtalo de nuevo.',
  'auth/network-request-failed': 'Sin conexión. Revisa tu internet e inténtalo de nuevo.',
};

const toSignInError = (e: unknown) =>
  new SignInError(MESSAGES[(e as { code?: string }).code ?? ''] ?? 'No se pudo iniciar sesión. Inténtalo de nuevo.');

export function createFirebaseAuth(config: RuntimeConfig): PasswordAuthProvider {
  const app = initializeApp({
    apiKey: config.auth.apiKey,
    authDomain: config.auth.authDomain,
    projectId: config.auth.projectId,
  });
  const auth: Auth = getAuth(app);
  auth.languageCode = 'es';
  if (config.auth.emulatorUrl) connectAuthEmulator(auth, config.auth.emulatorUrl, { disableWarnings: true });
  void setPersistence(auth, browserLocalPersistence);

  return {
    onChange(listener) {
      return onIdTokenChanged(auth, (user) =>
        listener(user ? ({ uid: user.uid, email: user.email } satisfies AuthUser) : null),
      );
    },
    async getToken() {
      await auth.authStateReady();
      return auth.currentUser ? auth.currentUser.getIdToken() : null;
    },
    signOut: () => signOut(auth),
    async signInWithPassword(email, password) {
      try {
        await signInWithEmailAndPassword(auth, email, password);
      } catch (e) {
        throw toSignInError(e);
      }
    },
    async sendPasswordReset(email) {
      try {
        await sendPasswordResetEmail(auth, email);
      } catch (e) {
        throw toSignInError(e);
      }
    },
  };
}
