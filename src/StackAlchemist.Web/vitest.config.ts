import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    environmentOptions: {
      jsdom: { url: 'http://localhost' },
    },
    setupFiles: ['./vitest.setup.ts'],
    globals: true,
    include: ['__tests__/**/*.{test,spec}.{ts,tsx}'],
    exclude: ['node_modules', '.next', 'e2e'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'text-summary', 'lcov', 'html'],
      reportsDirectory: './coverage',
      include: ['src/app/**/*.{ts,tsx}', 'src/components/**/*.{ts,tsx}', 'src/lib/**/*.{ts,tsx}', 'src/db/**/*.ts'],
      exclude: ['**/*.d.ts', '**/*.config.*', '**/types/**', '**/__tests__/**', '**/mocks/**'],
      // Ratchet floors: set just under measured coverage (2026-07-04 baseline:
      // 32.68 L / 28.41 B / 25.55 F / 32.3 S) so CI blocks regressions today.
      // Raise these as new suites land — never lower them.
      thresholds: { lines: 30, branches: 26, functions: 23, statements: 30 },
    },
    css: false,
    reporters: ['default', 'junit'],
    outputFile: { junit: './test-results/junit.xml' },
  },
  resolve: {
    // "@" mirrors the Next.js tsconfig path "@/*" -> "./src/*". "server-only" is not an
    // installed package (Next aliases it internally), so tests stub it.
    alias: {
      '@': path.resolve(__dirname, 'src'),
      'server-only': path.resolve(__dirname, '__tests__/mocks/server-only.ts'),
    },
  },
});
