import { CoverMode } from './cover-mode';

describe('CoverMode (Ver como reclutador)', () => {
  beforeEach(() => sessionStorage.clear());

  it('survives a reload of the tab for the same Administrador', () => {
    const before = new CoverMode();
    before.bindUser(() => 'admin-1');
    before.enter('rec-1', 'Mónica Soto');

    const reloaded = new CoverMode();
    reloaded.bindUser(() => 'admin-1');
    expect(reloaded.active()).toEqual({ id: 'rec-1', name: 'Mónica Soto' });
    expect(reloaded.recruiterId()).toBe('rec-1');
  });

  it('never applies to another user signed in on the same tab', () => {
    const before = new CoverMode();
    before.bindUser(() => 'admin-1');
    before.enter('rec-1', 'Mónica Soto');

    const other = new CoverMode();
    other.bindUser(() => 'recruiter-9');
    expect(other.active()).toBeNull();
    expect(other.recruiterId()).toBeNull();
  });

  it('is cleared by «Salir» and does nothing before anyone is signed in', () => {
    const cover = new CoverMode();
    cover.enter('rec-1', 'Mónica Soto');
    expect(cover.active()).toBeNull();

    cover.bindUser(() => 'admin-1');
    cover.enter('rec-1', 'Mónica Soto');
    cover.exit();
    expect(new CoverMode().active()).toBeNull();
  });
});
