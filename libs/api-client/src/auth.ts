/**
 * The client side of the identity provider (ARCHITECTURE §2). Each app implements it in
 * `apps/<app>/src/auth/firebase-*.ts` — the only files allowed to import the Firebase JS SDK (CI neutrality check).
 * Roles never come from here: the app asks `GET /v1/me`.
 */
export interface AuthUser {
  readonly uid: string;
  readonly email: string | null;
}

export interface AuthProvider {
  /** Current user, or null; called immediately with the known state and on every change. Returns an unsubscribe. */
  onChange(listener: (user: AuthUser | null) => void): () => void;
  /** A fresh bearer token (the adapter refreshes it silently), or null when signed out. */
  getToken(): Promise<string | null>;
  signOut(): Promise<void>;
}

/** Passwordless sign-in for the candidates and clients apps (SPEC §2.3): the link only opens the app that sent it. */
export interface EmailLinkAuthProvider extends AuthProvider {
  sendSignInLink(email: string): Promise<void>;
  /** True when the current URL is a sign-in link. */
  isSignInLink(url: string): boolean;
  /** Completes the sign-in; `email` is needed only when the link is opened on another device. */
  completeSignIn(url: string, email?: string): Promise<void>;
  /** The email remembered from {@link sendSignInLink} on this device, if any. */
  rememberedEmail(): string | null;
}

/** A sign-in failure with a user-facing message (es-MX). */
export class SignInError extends Error {}
