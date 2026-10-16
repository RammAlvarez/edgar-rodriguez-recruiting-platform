import { signal } from '@angular/core';

/**
 * "Ver como reclutador" (SPEC §4.4): the recruiter an Administrador is covering. Sent by the api-client as
 * X-Acting-For; the API checks it and audits every write. The banner and the recruiter-scoped views arrive in S4.
 */
export class CoverMode {
  private readonly recruiter = signal<{ id: string; name: string } | null>(null);

  readonly active = this.recruiter.asReadonly();
  readonly recruiterId = () => this.recruiter()?.id ?? null;

  enter(id: string, name: string): void {
    this.recruiter.set({ id, name });
  }

  exit(): void {
    this.recruiter.set(null);
  }
}
