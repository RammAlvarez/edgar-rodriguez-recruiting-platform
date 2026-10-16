import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';

describe('routes', () => {
  it('has the email-link sign-in, its landing page and a guarded shell', () => {
    const paths = routes.map((r) => r.path);
    expect(paths).toContain('entrar');
    expect(paths).toContain('entrar/enlace');
    const shell = routes.find((r) => r.path === '');
    expect(shell?.canActivate?.length).toBeGreaterThan(0);
  });
});
