import { setupServer } from 'msw/node';
import { handlers } from './handlers';

/**
 * MSW Server for Node.js (used in Vitest tests).
 * Override handlers in individual tests with server.use(...)
 */
export const server = setupServer(...handlers);
