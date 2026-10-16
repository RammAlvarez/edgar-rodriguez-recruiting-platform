import { type EmailLinkAuthProvider, type RuntimeConfig, SignInError } from '@platform/api-client';
import { initializeApp } from 'firebase/app';
import {
  browserLocalPersistence,
  connectAuthEmulator,
  getAuth,
  isSignInWithEmailLink,
  onIdTokenChanged,
  sendSignInLinkToEmail,
  setPersistence,
  signInWithEmailLink,
  signOut,
} from 'firebase/auth';

// The only file of this app that imports the Firebase SDK (CI neutrality check).
const EMAIL_KEY = 'platform.signInEmail';

const MESSAGES: Record<string, string> = {
  'auth/invalid-email': 'Introduce un correo válido.',
  'auth/invalid-action-code': 'El enlace ya se usó o caducó. Pide uno nuevo.',
  'auth/expired-action-code': 'El enlace caducó. Pide uno nuevo.',
  'auth/too-many-requests': 'Demasiados intentos. Espera unos minutos e inténtalo de nuevo.',
  'auth/network-request-failed': 'Sin conexión. Revisa tu internet e inténtalo de nuevo.',
};

const toSignInError = (e: unknown) =>
  new SignInError(MESSAGES[(e as { code?: string }).code ?? ''] ?? 'No se pudo iniciar sesión. Inténtalo de nuevo.');

function storage(): Storage | null {
  try {
    return globalThis.localStorage;
  } catch {
    return null;
  }
}

export function createFirebaseAuth(config: RuntimeConfig): EmailLinkAuthProvider {
  const app = initializeApp({ apiKey: config.auth.apiKey, authDomain: config.auth.authDomain, projectId: config.auth.projectId });
  const auth = getAuth(app);
  auth.languageCode = 'es';
  if (config.auth.emulatorUrl) connectAuthEmulator(auth, config.auth.emulatorUrl, { disableWarnings: true });
  void setPersistence(auth, browserLocalPersistence);

  return {
    onChange: (listener) => onIdTokenChanged(auth, (u) => listener(u ? { uid: u.uid, email: u.email } : null)),
    async getToken() {
      await auth.authStateReady();
      return auth.currentUser ? auth.currentUser.getIdToken() : null;
    },
    signOut: () => signOut(auth),
    async sendSignInLink(email) {
      try {
        // The link points back to this app's own domain, so it cannot open another app (SPEC §2.3).
        await sendSignInLinkToEmail(auth, email, { url: `${location.origin}/entrar/enlace`, handleCodeInApp: true });
        storage()?.setItem(EMAIL_KEY, email);
      } catch (e) {
        throw toSignInError(e);
      }
    },
    isSignInLink: (url) => isSignInWithEmailLink(auth, url),
    async completeSignIn(url, email) {
      const address = email ?? storage()?.getItem(EMAIL_KEY);
      if (!address) throw new SignInError('Escribe el correo al que llegó el enlace.');
      try {
        await signInWithEmailLink(auth, address, url);
        storage()?.removeItem(EMAIL_KEY);
      } catch (e) {
        throw toSignInError(e);
      }
    },
    rememberedEmail: () => storage()?.getItem(EMAIL_KEY) ?? null,
  };
}
