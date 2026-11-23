// [mirror] the bitplatform Network's locations - keep in sync with:
// - src/Shared/Features/Monitoring/NetworkLocations.cs

/**
 * The location hints that really place a Durable Object in their area. South America, Africa and the Middle East are left
 * out: Cloudflare places an object asked for there in a nearby area instead. bit Fabric stores a project's picks by these
 * codes and names each by its area.
 */
export const locations = ['wnam', 'enam', 'weur', 'eeur', 'apac', 'apac-ne', 'apac-se', 'oc'] as const satisfies readonly DurableObjectLocationHint[];

export type Location = (typeof locations)[number];

export function isLocation(value: unknown): value is Location {
    return typeof value === 'string' && (locations as readonly string[]).includes(value);
}
