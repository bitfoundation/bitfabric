# The bitplatform Network

bit Fabric's own server in Germany reads each project's `/healthz` every minute with the token the customer approved. The bitplatform Network adds the other places a project is checked from: every minute, from each location the customer picks, it times what a visitor there waits for, anonymously, so no project's token ever leaves bit Fabric's server (`bitfabric-internal/specs/monitoring.md`, "Germany, plus the locations the customer picks"). The pages never say Durable Object, Worker or location hint: to a customer it is "the bitplatform Network".

This folder is a Cloudflare Worker on the Workers free plan. It is not part of the .NET solution and is deployed on its own, by `.github/workflows/cd-network.yml`.

## How it works

```
cron, every minute ──poke──▶ one Durable Object per location (wnam, enam, weur, eeur, apac, apac-ne, apac-se, oc)
                                  │ its alarm, every minute
                                  ├─ GET  {FABRIC_URL}/api/v1/MonitoringNetwork/Targets?location=enam   (X-Network-Secret)
                                  ├─ GET  https://www.cloudflare.com/cdn-cgi/trace                      (colo=IAD: where it really runs)
                                  ├─ GET  each target, timed                                            (no token, https only)
                                  └─ POST {FABRIC_URL}/api/v1/MonitoringNetwork/Results                 (X-Network-Secret)
```

- **A location is an object.** `src/index.ts` gets each location's object with `idFromName(location)` and `get(id, { locationHint: location })`. Cloudflare places an object by its hint the first time it is created and never moves it, and a hint is best effort, so every reading names the data center it really ran in, from Cloudflare's trace. `sam`, `afr` and `me` are left out: Cloudflare places those in a nearby region instead. The list is in `src/locations.ts`, which mirrors `src/Shared/Features/Monitoring/NetworkLocations.cs`.
- **The cron only pokes.** Every minute it calls `poke(location)` on each object, which remembers its location and arms the alarm if none is set. The alarm then runs on its own at the start of every minute.
- **The alarm times.** It reads its targets from bit Fabric: a project's home page, which it asks for with `?bf=<a new GUID>` so any cache that honors the query string has to miss, or, for a project whose front end is hosted statically, its server's `/health` as is. It follows redirects within the same origin, three at most (the template sends `/` to `/en-US/`), stops at the response headers, waits 10 seconds at most, and notes whether an `Age`, `cf-cache-status: HIT` or `X-Cache: HIT` header came back. Then it posts the readings, each with the data center it ran in. bit Fabric stores each one as a round of the project from that location's region and adds it to the home page's 15 minute rollups, as it does Germany's (`MonitoringNetworkController`).
- **bit Fabric decides what is timed.** The targets are the projects that picked the location, first added first, up to `Monitoring:NetworkTargetsPerLocation` (50), and only https addresses, which bit Fabric checked when the project was added. A reading of a project the location doesn't time is ignored.

## Limits

The Workers free plan sets them, and the code is written to them:

| Limit (free plan) | What uses it | With all 8 locations, 50 apps each |
| --- | --- | --- |
| 50 outside requests per invocation | Every target, every redirect, bit Fabric's two calls and the trace | An alarm takes as many targets as fit, holding 4 requests for each one in flight (the request and three redirects) and giving back what it didn't use; the rest of the minute goes on in another alarm right away. A template app costs 2 requests (`/` and `/en-US/`), so an alarm times about 20 of them and 50 take 3 alarms. |
| 6 connections waiting for headers at once | The timing | 5 targets at a time, so no request queues behind another and times the wait. |
| 100,000 Durable Object requests a day | Every poke and every alarm | One alarm a minute per location is about 1,440 a day, and the pokes another 1,440. 50 apps per location: about (1 + 3) × 1,440 × 8 ≈ 46,000. |
| 100,000 rows written a day | Every `setAlarm` | One per alarm: about 35,000. The minute's progress stays in memory, not in storage. |
| 13,000 GB-s of duration a day | The time an object waits on its requests | About 8 seconds of waiting per location per minute. 50 apps answering in 300 ms, 5 at a time, take about 3. Apps that time out cost the most, which is why the timeout is short. |

An object that is evicted mid-minute starts that minute over, and what is left of a minute when the next one starts is dropped rather than timed late. Beyond 50 apps a location, or for more than 8 locations, move the Worker to the Workers paid plan and raise `Monitoring:NetworkTargetsPerLocation`.

## Settings

| Where | Name | What |
| --- | --- | --- |
| Worker variable | `FABRIC_URL` | bit Fabric's api, such as `https://api.bitfabric.co`. Set with `--var` by the deployment. |
| Worker secret | `NETWORK_SECRET` | The same value as the api's `Monitoring:NetworkSecret`. Set with `wrangler secret put` by the deployment. |
| bit Fabric's api | `Monitoring:NetworkSecret` | Empty turns the Network's endpoints off. |
| bit Fabric's api | `Monitoring:NetworkTargetsPerLocation` | 50 on the free plan. |

Without `FABRIC_URL` or `NETWORK_SECRET` the alarms do nothing but log a warning.

## Deploying

`.github/workflows/cd-network.yml` deploys it by hand to Test or Production (`gh workflow run cd-network.yml -f environment=Test`), as `bit-fabric-network-test` or `bit-fabric-network-production`. Each GitHub environment needs:

- `CLOUDFLARE_API_TOKEN` (secret): a Cloudflare API token on the account with **Workers: Admin**: the first deployment creates the Worker, which **Workers: Edit** can't do, as it only updates a Worker that exists. The `CLOUDFLARE_TOKEN` the server's deployment purges the cache with can't deploy a Worker.
- `CLOUDFLARE_ACCOUNT_ID` (secret): the Cloudflare account the Worker runs in.
- `SERVERAPI__MONITORING__NETWORKSECRET` (secret): a long random value, such as `openssl rand -hex 32`. The server's deployment writes it into the api's `Monitoring:NetworkSecret`, and this one gives it to the Worker, so the two always match. Change it, then deploy both.
- `SERVER_API_ADDRESS` (variable), which the server's deployment already uses: the Worker's `FABRIC_URL`.

## Working on it

```
npm ci
npm run typecheck
npm run dev           # wrangler dev --test-scheduled, then open http://localhost:8787/__scheduled to run the cron once
```

For `npm run dev`, put `NETWORK_SECRET=...` in a `.dev.vars` file here (it is git-ignored) and pass `--var FABRIC_URL:http://localhost:5023` to reach a local bit Fabric whose `Monitoring:NetworkSecret` is the same. Locally every location runs on this machine, and the trace names the data center nearest to it. bit Fabric only gives a location https addresses, so a project on localhost isn't timed.

`src/Tests/Features/Monitoring/ProjectMonitoringTests.Network.cs` covers bit Fabric's side: the secret, the targets, the readings and their rollups.
