// @ts-check
import eslint from '@eslint/js';
import angular from 'angular-eslint';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: ['**/dist/**', '**/node_modules/**', '**/.angular/**', 'services/**', 'design-reference/**', 'libs/shared/src/generated/**', '**/bin/**', '**/obj/**'],
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
  {
    // The Firebase JS SDK only in apps/*/src/auth/firebase-*.ts (also checked by scripts/check-neutrality.sh).
    files: ['apps/**/*.ts', 'libs/**/*.ts'],
    ignores: ['apps/*/src/auth/firebase-*.ts'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [{ group: ['firebase', 'firebase/*', '@firebase/*'], message: 'Only apps/*/src/auth/firebase-*.ts may import Firebase.' }] }],
    },
  },
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
