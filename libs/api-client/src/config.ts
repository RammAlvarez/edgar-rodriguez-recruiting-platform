/**
 * Runtime configuration read from `/config.json` next to the app's static files, so one build deploys to any static
 * host and environment (the deploy step writes the file; ARCHITECTURE §5). Nothing here is secret.
 */
export interface RuntimeConfig {
  readonly apiUrl: string;
  readonly auth: {
    readonly apiKey: string;
    readonly authDomain: string;
    readonly projectId: string;
    /** Local only: e.g. "http://127.0.0.1:9199". */
    readonly emulatorUrl?: string;
  };
}

export async function loadRuntimeConfig(url = '/config.json'): Promise<RuntimeConfig> {
  const response = await globalThis.fetch(url, { cache: 'no-store' });
  if (!response.ok) throw new Error(`Missing ${url} (${response.status})`);
  return (await response.json()) as RuntimeConfig;
}
