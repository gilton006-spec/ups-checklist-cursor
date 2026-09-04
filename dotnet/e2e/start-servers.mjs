import { spawn, execSync } from 'node:child_process';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '..');
const web = path.join(root, 'UpsChecklist.Web');
const flagFile = process.env.APP_MAINTENANCE_FLAG_FILE
  || path.join(__dirname, '.maintenance-flag');

if (fs.existsSync(flagFile)) fs.unlinkSync(flagFile);

const password = process.env.E2E_SITE_PASSWORD || 'e2e-test-only';

function baseEnv(extra = {}) {
  return {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Testing',
    DOTNET_ENVIRONMENT: 'Testing',
    HandoverEmail__Password: '',
    HandoverEmail__Host: 'smtp.e2e.invalid',
    HandoverEmail__Username: 'e2e@invalid.local',
    HandoverEmail__From: 'e2e@invalid.local',
    HandoverEmail__To: 'e2e@invalid.local',
    ...extra,
  };
}

const servers = [
  {
    url: process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:5088',
    env: baseEnv({ SiteAccess__Password: password }),
  },
  {
    url: process.env.PLAYWRIGHT_MAINTENANCE_URL || 'http://127.0.0.1:5089',
    env: baseEnv({
      APP_MAINTENANCE_MODE: 'true',
      SiteAccess__AllowOpenAccess: 'true',
      SiteAccess__Password: '',
    }),
  },
  {
    url: process.env.PLAYWRIGHT_FLAG_URL || 'http://127.0.0.1:5090',
    env: baseEnv({
      APP_MAINTENANCE_FLAG_FILE: flagFile,
      SiteAccess__AllowOpenAccess: 'true',
      SiteAccess__Password: '',
    }),
  },
];

function run(cmd, args, env) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { cwd: root, env, stdio: 'inherit', shell: true });
    child.on('exit', (code) => (code === 0 ? resolve() : reject(new Error(`${cmd} exited ${code}`))));
  });
}

async function waitHealth(url) {
  const health = `${url.replace(/\/$/, '')}/health`;
  for (let i = 0; i < 90; i += 1) {
    try {
      const res = await fetch(health);
      if (res.ok) return;
    } catch {
      /* retry */
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw new Error(`Timed out waiting for ${health}`);
}

/** Kill shell + nested `dotnet` children (Playwright may only signal the parent). */
function killProcessTree(pid) {
  if (!pid) return;
  try {
    if (process.platform === 'win32') {
      execSync(`taskkill /pid ${pid} /T /F`, { stdio: 'ignore' });
    } else {
      try {
        process.kill(-pid, 'SIGTERM');
      } catch {
        process.kill(pid, 'SIGTERM');
      }
    }
  } catch {
    /* already gone */
  }
}

async function main() {
  await run('dotnet', ['build', web, '-v', 'q', '--nologo'], process.env);
  const children = servers.map((server) =>
    spawn(
      'dotnet',
      ['run', '--project', web, '--no-build', '--no-launch-profile', '--urls', server.url],
      {
        cwd: root,
        env: server.env,
        stdio: 'inherit',
        shell: true,
        detached: process.platform !== 'win32',
      },
    ));

  let stopped = false;
  const stop = () => {
    if (stopped) return;
    stopped = true;
    for (const child of children) killProcessTree(child.pid);
  };
  process.on('exit', stop);
  process.on('SIGINT', () => { stop(); process.exit(130); });
  process.on('SIGTERM', () => { stop(); process.exit(143); });
  process.on('SIGHUP', () => { stop(); process.exit(129); });

  await Promise.all(servers.map((server) => waitHealth(server.url)));
  console.log('E2E servers ready:', servers.map((s) => s.url).join(', '));

  await new Promise(() => {
    /* keep alive until Playwright stops this process */
  });
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
