import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// Vitest for the TypeScript libs and scripts (the Angular apps run theirs through `ng test`).
export default defineConfig({
  resolve: {
    alias: {
      '@platform/shared': fileURLToPath(new URL('./libs/shared/src/index.ts', import.meta.url)),
      '@platform/api-client': fileURLToPath(new URL('./libs/api-client/src/index.ts', import.meta.url)),
    },
  },
  test: {
    include: ['libs/shared/**/*.spec.ts', 'libs/api-client/**/*.spec.ts', 'scripts/**/*.spec.ts'],
    environment: 'node',
  },
});
