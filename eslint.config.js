// @ts-check
import { readdirSync } from 'node:fs';
import eslint from '@eslint/js';
import angular from 'angular-eslint';
import tseslint from 'typescript-eslint';


const APPS = readdirSync('apps', { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name);

const firebasePattern = {
  group: ['firebase', 'firebase/*', '@firebase/*'],
  message: 'Only apps/*/src/auth/firebase-*.ts may import Firebase.',
};

/**
 * Import boundaries (ARCHITECTURE "Front end"): an app never imports another app, libs never import apps, and the libs layer
 * upward only as shared ← api-client ← ui. The Firebase JS SDK stays in apps/*\/src/auth/firebase-*.ts (also checked by
 * scripts/check-neutrality.sh). One `no-restricted-imports` block per scope, because a later block replaces an earlier one's patterns.
 */
function importBoundaries() {
  const rule = (...patterns) => ({ 'no-restricted-imports': ['error', { patterns }] });
  const noApps = { group: ['**/apps/**'], message: 'Libs must not import from apps.' };
  const noUi = { group: ['@platform/ui', '@platform/ui/*', '**/libs/ui/**', '**/ui/src/**'], message: 'Only apps and libs/ui may import @platform/ui (layering: shared ← api-client ← ui).' };
  const noApiClient = { group: ['@platform/api-client', '@platform/api-client/*', '**/libs/api-client/**', '**/api-client/src/**'], message: 'libs/shared must not import @platform/api-client (layering: shared ← api-client ← ui).' };
  const blocks = [];
  for (const app of APPS) {
    const others = APPS.filter((a) => a !== app);
    const crossApp = { group: others.flatMap((o) => [`**/apps/${o}/**`, `**/${o}/src/**`]), message: `apps/${app} must not import another app; share through libs/*.` };
    blocks.push(
      { files: [`apps/${app}/**/*.ts`], ignores: ['apps/*/src/auth/firebase-*.ts'], rules: rule(crossApp, firebasePattern) },
      { files: [`apps/${app}/src/auth/firebase-*.ts`], rules: rule(crossApp) },
    );
  }
  blocks.push(
    { files: ['libs/ui/**/*.ts'], rules: rule(noApps, firebasePattern) },
    { files: ['libs/api-client/**/*.ts'], rules: rule(noApps, noUi, firebasePattern) },
    { files: ['libs/shared/**/*.ts'], rules: rule(noApps, noUi, noApiClient, firebasePattern) },
  );
  return blocks;
}

export default tseslint.config(
  {
    ignores: ['**/dist/**', '**/node_modules/**', '**/.angular/**', 'services/**', 'design-reference/**', 'libs/shared/src/generated/**', '**/bin/**', '**/obj/**', '.claude/**'],
  },
  {
    files: ['**/*.ts'],
    extends: [eslint.configs.recommended, ...tseslint.configs.recommended, ...tseslint.configs.stylistic, ...angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': ['error', { type: 'attribute', prefix: ['app', 'ui'], style: 'camelCase' }],
      '@angular-eslint/component-selector': ['error', { type: 'element', prefix: ['app', 'ui'], style: 'kebab-case' }],
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
    },
  },
  {
    // Components and repositories reach the API only through libs/api-client (kickoff "Front end").
    files: ['apps/**/*.ts', 'libs/ui/**/*.ts'],
    rules: {
      'no-restricted-globals': ['error', { name: 'fetch', message: 'Use the ApiClient from @platform/api-client.' }],
      'no-restricted-syntax': [
        'error',
        { selector: "CallExpression[callee.property.name='fetch']", message: 'Use the ApiClient from @platform/api-client.' },
      ],
    },
  },
  ...importBoundaries(),
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
  },
  {
    files: ['scripts/**/*.mjs', 'libs/shared/scripts/**/*.mjs', 'eslint.config.js'],
    languageOptions: { globals: { process: 'readonly', console: 'readonly', fetch: 'readonly', URL: 'readonly' } },
    extends: [eslint.configs.recommended],
  },
);
