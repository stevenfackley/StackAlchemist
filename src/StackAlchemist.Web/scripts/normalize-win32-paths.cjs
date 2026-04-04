/**
 * normalize-win32-paths.cjs
 *
 * Loaded via NODE_OPTIONS="--require ..." BEFORE next build starts.
 *
 * On Windows, pnpm resolves symlinks through its virtual store, which can
 * produce paths that start with an uppercase drive letter ("C:\...") while
 * other code paths produce lowercase ("c:\...").  Node.js uses the verbatim
 * resolved path as the key for its module cache, so the same physical file
 * can be loaded twice — once per casing — resulting in two distinct module
 * instances.  Next.js's built-in _document.js performs an `===` singleton
 * check on the Html component; if two instances exist the check fails with
 * "<Html> should not be imported outside of pages/_document."
 *
 * This hook normalises every resolved path on Windows so the drive letter is
 * always lowercase, ensuring the module cache is keyed consistently.
 */

'use strict';

if (process.platform === 'win32') {
  const Module = require('module');
  const _origResolve = Module._resolveFilename.bind(Module);

  Module._resolveFilename = function normalizeWin32Casing(
    request,
    parent,
    isMain,
    options
  ) {
    const resolved = _origResolve(request, parent, isMain, options);
    // Only touch absolute Windows paths whose drive letter is uppercase.
    if (typeof resolved === 'string' && /^[A-Z]:\\/.test(resolved)) {
      return resolved[0].toLowerCase() + resolved.slice(1);
    }
    return resolved;
  };
}
