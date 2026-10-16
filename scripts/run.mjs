#!/usr/bin/env node
// `node scripts/run.mjs <command…>`: runs a command with the local environment (scripts/lib/env.mjs).
import { spawn } from 'node:child_process';
import { loadEnv, root } from './lib/env.mjs';

const [command, ...args] = process.argv.slice(2);
if (!command) {
  console.error('usage: node scripts/run.mjs <command> [args…]');
  process.exit(2);
}
const child = spawn(command, args, { cwd: root, env: loadEnv(), stdio: 'inherit' });
child.on('exit', (code, signal) => process.exit(signal ? 1 : (code ?? 1)));
