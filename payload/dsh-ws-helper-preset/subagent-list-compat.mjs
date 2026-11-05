import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

export const name = 'subagent-list-compat';
export const inject = ['profileContext'];

export async function apply(ctx) {
  let entry;
  try {
    entry = createRequire(ctx.profileContext.installAnchor).resolve('@deepseek-ai/dsh-tool-subagent-control/list-agents');
  } catch (error) {
    if (['ERR_PACKAGE_PATH_NOT_EXPORTED', 'MODULE_NOT_FOUND'].includes(error.code)) return;
    throw error;
  }
  // Older runtimes expose discovery separately; newer ones register it in
  // the main control plugin. Never declare a removed module to the loader.
  await ctx.plugin(await import(pathToFileURL(entry).href));
}
