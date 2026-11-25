import { missingRequired } from './recruiting-repositories';

type Item = Parameters<typeof missingRequired>[0]['items'][number];

const item = (isRequired: boolean, status: string | null, inTemplate = true) =>
  ({ inTemplate, isRequired, current: status ? { status } : null }) as unknown as Item;

describe('missingRequired (hire warning)', () => {
  it('counts required documents that are missing, rejected or expired, never optional or delivered ones', () => {
    const items = [
      item(true, 'validated'),
      item(true, 'delivered'),
      item(true, null),
      item(true, 'rejected'),
      item(true, 'expired'),
      item(false, null),
      item(true, null, false),
    ];
    expect(missingRequired({ items })).toBe(3);
  });

  it('is zero when everything required is in', () => {
    expect(missingRequired({ items: [item(true, 'validated'), item(false, null)] })).toBe(0);
  });
});
