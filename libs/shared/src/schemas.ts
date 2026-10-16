import { z } from 'zod';

/**
 * Zod schemas for client-side form validation only. The server's FluentValidation rules are authoritative; these
 * give the same message earlier. Messages are es-MX and follow the design system's error style.
 */
export const decimalString = (message = 'Introduce un importe válido, por ejemplo 1234.56.') =>
  z.string().trim().regex(/^-?\d{1,10}(\.\d{1,2})?$/, message);

export const email = z.string().trim().email('Introduce un correo válido.');

export const mobilePhone = z
  .string()
  .trim()
  .regex(/^\d{10}$/, 'El celular debe tener 10 dígitos.');

export const curp = z
  .string()
  .trim()
  .toUpperCase()
  .regex(/^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$/, 'La CURP debe tener 18 caracteres con el formato oficial.');

export const rfcPerson = z
  .string()
  .trim()
  .toUpperCase()
  .regex(/^[A-ZÑ&]{4}\d{6}[A-Z0-9]{3}$/, 'El RFC de persona física debe tener 13 caracteres.');
