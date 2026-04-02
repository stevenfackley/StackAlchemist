import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    globals: true,
    include: ['__tests__/**/*.{test,spec}.{ts,tsx}'],
    exclude: ['node_modules', '.next', 'e2e'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'text-summary', 'lcov', 'html'],
      reportsDirectory: './coverage',
      include: ['app/**/*.{ts,tsx}', 'components/**/*.{ts,tsx}', 'lib/**/*.{ts,tsx}'],
      exclude: ['**/*.d.ts', '**/*.config.*', '**/types/**', '**/__tests__/**', '**/mocks/**'],
      thresholds: { lines: 80, branches: 75, functions: 80, statements: 80 },
    },
    css: false,
    reporters: ['default', 'junit'],
    outputFile: { junit: './test-results/junit.xml' },
  },
  resolve: {
    alias: { '@': path.resolve(__dirname, '.') },
  },
});
