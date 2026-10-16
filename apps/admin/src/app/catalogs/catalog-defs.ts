import { formatDateKey } from '@platform/shared';
import type { CatalogsDto } from '../data/api-types';

export type FieldType = 'text' | 'number' | 'toggle' | 'select' | 'date';

export interface CatalogField {
  readonly key: string;
  readonly label: string;
  readonly type: FieldType;
  readonly required?: boolean;
  readonly options?: readonly { readonly value: string | null; readonly label: string }[];
  /** Only editable when creating (a stable key other modules refer to). */
  readonly createOnly?: boolean;
  readonly hint?: string;
  readonly default?: unknown;
}

export interface CatalogColumn {
  readonly key: string;
  readonly label: string;
  readonly format?: (row: Row) => string;
  readonly numeric?: boolean;
}

export type Row = Record<string, unknown> & { readonly id: string };

/** One tab of Catálogos: where its rows are in `GET /v1/catalogs`, its columns, and the fields of its side panel. */
export interface CatalogDef {
  readonly key: string;
  readonly label: string;
  /** Route segment under /v1/catalogs. */
  readonly path: string;
  readonly singular: string;
  readonly rows: (c: CatalogsDto) => readonly Row[];
  readonly columns: readonly CatalogColumn[];
  readonly fields: readonly CatalogField[];
  readonly hasActive: boolean;
}

const yesNo = (key: string) => (r: Row) => (r[key] ? 'Sí' : 'No');
const active: CatalogField = { key: 'isActive', label: 'Activo', type: 'toggle', default: true };

export const CATALOGS: readonly CatalogDef[] = [
  {
    key: 'zones',
    label: 'Zonas',
    path: 'zones',
    singular: 'zona',
    rows: (c) => c.zones as unknown as Row[],
    columns: [
      { key: 'nameEs', label: 'Municipio / zona' },
      { key: 'state', label: 'Estado' },
    ],
    fields: [
      { key: 'nameEs', label: 'Nombre', type: 'text', required: true },
      { key: 'state', label: 'Estado', type: 'text', required: true, default: 'Nuevo León' },
      active,
    ],
    hasActive: true,
  },
  {
    key: 'positions',
    label: 'Puestos',
    path: 'positions',
    singular: 'puesto',
    rows: (c) => c.positions as unknown as Row[],
    columns: [{ key: 'nameEs', label: 'Puesto' }],
    fields: [{ key: 'nameEs', label: 'Nombre', type: 'text', required: true }, active],
    hasActive: true,
  },
  {
    key: 'educationLevels',
    label: 'Niveles de estudio',
    path: 'education-levels',
    singular: 'nivel de estudios',
    rows: (c) => c.educationLevels as unknown as Row[],
    columns: [
      { key: 'nameEs', label: 'Nivel' },
      { key: 'sortOrder', label: 'Orden', numeric: true },
    ],
    fields: [
      { key: 'nameEs', label: 'Nombre', type: 'text', required: true },
      { key: 'sortOrder', label: 'Orden', type: 'number', required: true, default: 0 },
      active,
    ],
    hasActive: true,
  },
  {
    key: 'benefits',
    label: 'Prestaciones',
    path: 'benefits',
    singular: 'prestación',
    rows: (c) => c.benefits as unknown as Row[],
    columns: [
      { key: 'nameEs', label: 'Prestación' },
      { key: 'isDefault', label: 'Por omisión', format: yesNo('isDefault') },
    ],
    fields: [
      { key: 'nameEs', label: 'Nombre', type: 'text', required: true },
      { key: 'isDefault', label: 'Marcada por omisión en una vacante nueva', type: 'toggle', default: false },
      active,
    ],
    hasActive: true,
  },
  {
    key: 'banks',
    label: 'Bancos',
    path: 'banks',
    singular: 'banco',
    rows: (c) => c.banks as unknown as Row[],
    columns: [
      { key: 'speiCode', label: 'Clave SPEI' },
      { key: 'name', label: 'Banco' },
      {
        key: 'supportsRegistration',
        label: 'Alta masiva',
        format: (r) => ({ banorte: 'Banorte', banregio: 'Banregio' })[r['supportsRegistration'] as string] ?? '—',
      },
      { key: 'supportsMassivePayment', label: 'Pagos masivos', format: yesNo('supportsMassivePayment') },
    ],
    fields: [
      { key: 'name', label: 'Nombre', type: 'text', required: true },
      { key: 'speiCode', label: 'Clave SPEI', type: 'text', required: true, hint: '3 dígitos de Banxico' },
      {
        key: 'supportsRegistration',
        label: 'Layout de alta masiva',
        type: 'select',
        default: null,
        options: [
          { value: null, label: 'Ninguno' },
          { value: 'banorte', label: 'Banorte' },
          { value: 'banregio', label: 'Banregio' },
        ],
      },
      { key: 'supportsMassivePayment', label: 'Pagos masivos (Banco Azteca)', type: 'toggle', default: false },
      active,
    ],
    hasActive: true,
  },
  {
    key: 'documentTypes',
    label: 'Tipos de documento',
    path: 'document-types',
    singular: 'tipo de documento',
    rows: (c) => c.documentTypes as unknown as Row[],
    columns: [
      { key: 'code', label: 'Clave' },
      { key: 'nameEs', label: 'Documento' },
      {
        key: 'hasExpiry',
        label: 'Vigencia',
        format: (r) => (r['hasExpiry'] ? (r['defaultValidityDays'] ? `${r['defaultValidityDays']} días` : 'Fecha de vencimiento') : '—'),
      },
      { key: 'sortOrder', label: 'Orden', numeric: true },
    ],
    fields: [
      { key: 'code', label: 'Clave', type: 'text', required: true, createOnly: true, hint: 'Mayúsculas, sin espacios: ACTA_NAC' },
      { key: 'nameEs', label: 'Nombre', type: 'text', required: true },
      { key: 'hasExpiry', label: 'Vence', type: 'toggle', default: false },
      { key: 'defaultValidityDays', label: 'Vigencia por omisión (días)', type: 'number', hint: 'Solo si vence; vacío = se captura la fecha' },
      { key: 'sortOrder', label: 'Orden', type: 'number', required: true, default: 0 },
      active,
    ],
    hasActive: true,
  },
  {
    key: 'holidays',
    label: 'Días festivos',
    path: 'holidays',
    singular: 'día festivo',
    rows: (c) => c.holidays as unknown as Row[],
    columns: [
      { key: 'date', label: 'Fecha', format: (r) => formatDateKey(r['date'] as string) },
      { key: 'nameEs', label: 'Festivo' },
      { key: 'isMandatory', label: 'Descanso obligatorio', format: yesNo('isMandatory') },
    ],
    fields: [
      { key: 'date', label: 'Fecha', type: 'date', required: true },
      { key: 'nameEs', label: 'Nombre', type: 'text', required: true },
      { key: 'isMandatory', label: 'Descanso obligatorio (LFT art. 74)', type: 'toggle', default: true },
    ],
    hasActive: false,
  },
];

/** The request body of a catalog panel: every field, empty text/number → null, numbers as numbers. */
export function toRequest(def: CatalogDef, value: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(
    def.fields.map((f) => {
      const v = value[f.key];
      if (f.type === 'number') return [f.key, v === '' || v === null || v === undefined ? null : Number(v)];
      if (f.type === 'text' || f.type === 'date') return [f.key, typeof v === 'string' && v.trim() !== '' ? v.trim() : null];
      return [f.key, v ?? null];
    }),
  );
}
