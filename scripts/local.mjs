// Cross-platform Cursor entry point. Existing hosted/Linux scripts are unchanged.
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";

const project = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const command = process.argv[2];
if (!["dev", "build", "start"].includes(command)) {
  process.stderr.write("Usage: node scripts/local.mjs dev|build|start\n");
  process.exit(1);
}
const packageName = command === "dev" ? "vite" : "vinext";
const packageDir = resolve(project, "node_modules", packageName);
let manifest;
try { manifest = JSON.parse(await readFile(resolve(packageDir, "package.json"), "utf8")); }
catch { process.stderr.write("Dependencies are missing. Run npm ci first.\n"); process.exit(1); }
const binary = typeof manifest.bin === "string" ? manifest.bin : manifest.bin[packageName];
const args = command === "dev" ? [] : [command];
const child = spawn(process.execPath, [resolve(packageDir, binary), ...args, ...process.argv.slice(3)], {
  cwd: project,
  stdio: "inherit",
  env: { ...process.env, WRANGLER_WRITE_LOGS: "false", WRANGLER_LOG_PATH: resolve(project, ".wrangler", "logs") },
});
child.on("error", error => { process.stderr.write(error.message + "\n"); process.exitCode = 1; });
child.on("exit", code => { process.exitCode = code ?? 1; });
