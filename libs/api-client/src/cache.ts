/**
 * Live queries and their cache tags (ARCHITECTURE §4, D39). A mutating response's `X-Invalidates` header re-fetches
 * every registered query that shares a tag; «Actualizar» re-fetches them all (changes made by other users).
 */
export class QueryCache {
  private readonly entries = new Set<{ tags: ReadonlySet<string>; refetch: () => void }>();

  register(tags: readonly string[], refetch: () => void): () => void {
    const entry = { tags: new Set(tags), refetch };
    this.entries.add(entry);
    return () => this.entries.delete(entry);
  }

  invalidate(tags: readonly string[]): void {
    if (tags.length === 0) return;
    for (const entry of [...this.entries]) {
      if (tags.some((t) => entry.tags.has(t))) entry.refetch();
    }
  }

  refreshAll(): void {
    for (const entry of [...this.entries]) entry.refetch();
  }

  get size(): number {
    return this.entries.size;
  }
}

export const INVALIDATES_HEADER = 'X-Invalidates';

export function parseInvalidates(header: string | null): string[] {
  return (header ?? '')
    .split(',')
    .map((t) => t.trim())
    .filter(Boolean);
}
