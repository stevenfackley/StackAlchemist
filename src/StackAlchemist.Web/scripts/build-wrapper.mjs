/**
 * build-wrapper.mjs
 *
 * Thin wrapper around `next build` that injects the Windows path-casing patch
 * via NODE_OPTIONS so that every child process spawned by Next.js (including
 * the static-generation worker that pre-renders /404 and /500) inherits the
 * Module._resolveFilename hook before any modules are loaded.
 *
 * On non-Windows systems the hook is a no-op, so this wrapper is safe to use
 * on Linux CI / inside Docker images.
 */

import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import nextPackageJson from '../node_modules/next/package.json' with { type: 'json' };

const __dirname = dirname(fileURLToPath(import.meta.url));
const patchPath = join(__dirname, 'normalize-win32-paths.cjs').replace(/\\/g, '/');

// Build the final NODE_OPTIONS value, preserving anything already set.
const existingOpts = process.env.NODE_OPTIONS ?? '';
const patchFlag = `--require "${patchPath}"`;
const finalOpts = existingOpts.includes(patchFlag)
  ? existingOpts
  : `${patchFlag} ${existingOpts}`.trim();

const env = { ...process.env, NODE_OPTIONS: finalOpts };

// Locate the Next.js CLI entry point from the symlink that pnpm creates.
// Using process.execPath (current node binary) + the next CLI module avoids
// shell=true cross-platform differences.
const nextCli = join(dirname(__dirname), 'node_modules', '.bin', 'next');
const nextMajorVersion = Number.parseInt(nextPackageJson.version.split('.')[0] ?? '0', 10);
const buildArgs = ['build'];

// Next 16+ defaults to Turbopack during production builds. This repo still
// relies on a custom webpack hook for path normalization, so force webpack to
// keep local builds, Docker, and CI consistent across dependency bumps.
if (nextMajorVersion >= 16) {
  buildArgs.push('--webpack');
}

const result = spawnSync(nextCli, buildArgs, {
  stdio: 'inherit',
  env,
  // shell: true is required on Windows to execute .cmd wrappers in .bin/
  shell: process.platform === 'win32',
});

process.exit(result.status ?? 1);
