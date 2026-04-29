import next from 'eslint-config-next/core-web-vitals';
import reactHooks from 'eslint-plugin-react-hooks';

const config = [
  {
    ignores: [
      '.next/**',
      'node_modules/**',
      'coverage/**',
      'test-results/**',
      'playwright-report/**',
      'dist/**',
      'build/**',
    ],
  },
  ...next,
  {
    plugins: { 'react-hooks': reactHooks },
    rules: {
      'no-console': ['warn', { allow: ['warn', 'error'] }],
      'no-unused-vars': 'off',
      'react-hooks/exhaustive-deps': 'warn',
    },
  },
];

export default config;
