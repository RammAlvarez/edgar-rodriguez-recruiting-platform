import { computed, signal } from '@angular/core';

interface Covering {
  readonly id: string;
  readonly name: string;
  /** The Administrador who entered cover mode; nobody else ever sends it. */
  readonly by: string;
}

const KEY = 'platform.cover-mode';

/**
 * "Ver como reclutador" (SPEC §4.4): the recruiter an Administrador is covering. Sent by the api-client as
 * X-Acting-For; the API checks it and audits every write.
 *
 * Kept for the browser tab (sessionStorage), so a reload or a typed URL stays in cover mode (demo run, 2026-11-25: a
 * reload used to drop it silently and land on Inicio). It only applies while the same user is signed in.
 */
export class CoverMode {
  private readonly stored = signal<Covering | null>(read());
  private readonly user = signal<() => string | null>(() => null);

  private readonly current = computed(() => {
    const c = this.stored();
    return c && c.by === this.user()() ? c : null;
  });

  readonly active = computed(() => {
    const c = this.current();
    return c ? { id: c.id, name: c.name } : null;
  });
  readonly recruiterId = () => this.current()?.id ?? null;

  /** Who is signed in now (the session's «me»); called once when the app starts. */
  bindUser(userId: () => string | null): void {
    this.user.set(userId);
  }

  enter(id: string, name: string): void {
    const by = this.user()();
    if (!by) return;
    this.save({ id, name, by });
  }

  exit(): void {
    this.save(null);
  }

  private save(value: Covering | null): void {
    this.stored.set(value);
    try {
      if (value) sessionStorage.setItem(KEY, JSON.stringify(value));
      else sessionStorage.removeItem(KEY);
    } catch {
      // Storage blocked (private mode): cover mode still works until the next reload.
    }
  }
}

function read(): Covering | null {
  try {
    const raw = sessionStorage.getItem(KEY);
    const value = raw ? (JSON.parse(raw) as Partial<Covering>) : null;
    return value?.id && value.name && value.by ? { id: value.id, name: value.name, by: value.by } : null;
  } catch {
    return null;
  }
}
