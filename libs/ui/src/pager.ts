import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { MatPaginator, MatPaginatorIntl, PageEvent } from '@angular/material/paginator';
import type { Sort } from '@angular/material/sort';

export const PAGE_SIZES = [15, 25, 50] as const;

export type SortValue = string | number | boolean | Date | null | undefined;
/** How a column reads its value for sorting: one function per column key, or one function for all of them. */
export type SortAccessors<T> = Record<string, (row: T) => SortValue> | ((key: string, row: T) => SortValue);

const collator = new Intl.Collator('es', { numeric: true, sensitivity: 'base' });

function plain(v: Exclude<SortValue, null | undefined>): string | number {
  return typeof v === 'boolean' ? Number(v) : v instanceof Date ? v.getTime() : v;
}

function compare(a: Exclude<SortValue, null | undefined>, b: Exclude<SortValue, null | undefined>): number {
  const x = plain(a);
  const y = plain(b);
  if (typeof x === 'number' && typeof y === 'number') return x - y;
  return collator.compare(String(x), String(y));
}

/** Sort and page state for one table: 15 rows by default, 25 or 50 on request. */
export class Pager {
  readonly index = signal(0);
  readonly size = signal<number>(PAGE_SIZES[0]);
  readonly sort = signal<Sort>({ active: '', direction: '' });

  /** The rows in the chosen order (empty values last, whichever the direction), then the current page of them. */
  slice<T>(rows: readonly T[], accessors?: SortAccessors<T>): T[] {
    const ordered = this.order(rows, accessors);
    const size = this.size();
    const last = Math.max(0, Math.ceil(ordered.length / size) - 1);
    const start = Math.min(this.index(), last) * size;
    return ordered.slice(start, start + size);
  }

  /** Index clamped to the available pages, as the paginator should show it. */
  indexFor(total: number): number {
    return Math.min(this.index(), Math.max(0, Math.ceil(total / this.size()) - 1));
  }

  set(e: PageEvent): void {
    this.size.set(e.pageSize);
    this.index.set(e.pageIndex);
  }

  sortChange(e: Sort): void {
    this.sort.set(e);
    this.index.set(0);
  }

  private order<T>(rows: readonly T[], accessors?: SortAccessors<T>): readonly T[] {
    const { active, direction } = this.sort();
    if (!active || !direction) return rows;
    const read =
      typeof accessors === 'function'
        ? (r: T) => accessors(active, r)
        : accessors?.[active] ?? ((r: T) => (r as Record<string, SortValue>)[active]);
    const sign = direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => {
      const x = read(a);
      const y = read(b);
      const nx = x === null || x === undefined || x === '';
      const ny = y === null || y === undefined || y === '';
      if (nx || ny) return nx === ny ? 0 : nx ? 1 : -1;
      return sign * compare(x, y);
    });
  }
}

function spanishLabels(): MatPaginatorIntl {
  const intl = new MatPaginatorIntl();
  intl.itemsPerPageLabel = 'Filas por página';
  intl.nextPageLabel = 'Página siguiente';
  intl.previousPageLabel = 'Página anterior';
  intl.firstPageLabel = 'Primera página';
  intl.lastPageLabel = 'Última página';
  intl.getRangeLabel = (page, size, length) => {
    if (length === 0) return '0 de 0';
    const start = page * size;
    return `${start + 1}–${Math.min(start + size, length)} de ${length}`;
  };
  return intl;
}

/** Paginator under a table; it stays out of the way while everything fits in the smallest page. */
@Component({
  selector: 'ui-pager',
  imports: [MatPaginator],
  providers: [{ provide: MatPaginatorIntl, useFactory: spanishLabels }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (total() > sizes[0]) {
      <mat-paginator
        [length]="total()"
        [pageSize]="pager().size()"
        [pageIndex]="pager().indexFor(total())"
        [pageSizeOptions]="sizes"
        showFirstLastButtons
        (page)="pager().set($event)"
      />
    }
  `,
})
export class UiPager {
  readonly pager = input.required<Pager>();
  readonly total = input.required<number>();
  protected readonly sizes = [...PAGE_SIZES];
}
