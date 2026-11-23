import { locations } from './locations';
import type { Env } from './network-location';

export { NetworkLocation } from './network-location';

/**
 * The bitplatform Network (README.md): every minute, the cron trigger pokes one object per location, created by the
 * location's name with its hint, so the first call places it in that area for good. Each object keeps its own alarm a
 * minute armed and does the timing there; the poke only makes sure the alarm is set. Nothing else reaches this Worker.
 */
export default {
    async scheduled(_controller, env) {
        const pokes = await Promise.allSettled(locations.map(location =>
            env.LOCATIONS.get(env.LOCATIONS.idFromName(location), { locationHint: location }).poke(location)));

        pokes.forEach((poke, index) => {
            if (poke.status === 'rejected') {
                console.error({ message: 'A location didn\'t answer its poke.', location: locations[index], error: String(poke.reason) });
            }
        });
    },

    async fetch() {
        return new Response(null, { status: 404 });
    }
} satisfies ExportedHandler<Env>;
