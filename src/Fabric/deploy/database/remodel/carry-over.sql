-- Carries bit Fabric's data from the model before bitfoundation/bitfabric#73 into the one after it (bitfabric-internal/
-- specs/changes/2026-11-26-teams-licenses-projects.md, The data script). save.sh moved the old tables into the schema
-- "legacy" and the deployment's migrations built the new ones in "public"; this copies every row across, in one
-- transaction, and fails, changing nothing, when a count doesn't add up. carry-over.sh runs it and keeps its report.
--
-- Every table is copied as it is, column by column, except:
--   - "Projects" (Monitoring's) becomes "Projects" (one per old project, same id, name and team) and "Monitorings" (same
--     id, environment Production); its people (who connected it, whom an alert route names) become its members while they
--     are accepted members of the team.
--   - Every history table's "ProjectId" becomes "MonitoringId".
--   - Alert routes to someone who is no longer an accepted member of the team are dropped (Leaving a Team ends its alerts).
--   - "ProjectConnections"."ConnectedByUserId" becomes null when that account is gone.
--   - "LicensedProjects" becomes "ProjectTrials" (those that had a trial). A trial of a project nobody licensed is marked
--     "Ci" = 'unknown': trials didn't name their CI before, and every one of them came from bit platform's own workflows
--     and test runs, so the trial and conversion figures leave them out.
--   - "IssuedLicenses" go to a team of whoever got them: the active team they accepted whose title or name is the
--     project's name (letters and digits only, any case), else the one they accepted first, else a new team named after
--     them, with them as its admin, made as the Projects page makes one. The team's project of that id is made once, and
--     the person is its member.
--   - Hangfire's tables ("jobs") are not carried: its jobs are in Redis, and the recurring ones are registered at startup.
--   - The seeded prompts get bit Boilerplate 11.0.0-pre-25's new address of the AdminPanel demo's Windows installer, the
--     data change of the migration the new Initial folded in (AssistantWindowsInstallerUrl).

\set ON_ERROR_STOP on
SET client_min_messages = warning;

BEGIN;

-- The new schema's seed rows (the fallback team, its roles, the seeded accounts) come again from the old tables, as the
-- old database has them now.
DO $$
DECLARE t record;
BEGIN
    IF to_regclass('legacy."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'There is no "legacy" schema to carry: run save.sh first.';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__CarriedOver') THEN
        RAISE EXCEPTION 'The data was carried over already (public."__CarriedOver").';
    END IF;
    FOR t IN SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
    LOOP
        EXECUTE format('TRUNCATE public.%I CASCADE', t.table_name);
    END LOOP;
END $$;

-- Foreign keys are checked once, at the end (See the check below), rather than table by table in a dependency order.
SET LOCAL session_replication_role = replica;

-- 1. Every table whose columns are the same, by the columns both schemas have.
CREATE TEMP TABLE carried (table_name text PRIMARY KEY, before bigint, after bigint, note text) ON COMMIT DROP;

DO $$
DECLARE t record; cols text; n bigint;
BEGIN
    FOR t IN
        SELECT p.table_name FROM information_schema.tables p
        WHERE p.table_schema = 'public' AND p.table_type = 'BASE TABLE'
          AND p.table_name NOT IN ('__EFMigrationsHistory', 'Projects', 'Monitorings', 'ProjectMembers', 'ProjectTrials', 'IssuedLicenses', 'ProjectAlertRoutes', 'ProjectConnections')
          AND EXISTS (SELECT 1 FROM information_schema.tables l WHERE l.table_schema = 'legacy' AND l.table_name = p.table_name)
    LOOP
        SELECT string_agg(format('%I', c.column_name), ', ' ORDER BY c.ordinal_position) INTO cols
        FROM information_schema.columns c
        WHERE c.table_schema = 'public' AND c.table_name = t.table_name
          AND EXISTS (SELECT 1 FROM information_schema.columns o WHERE o.table_schema = 'legacy' AND o.table_name = t.table_name AND o.column_name = c.column_name);

        -- A history table's key to the old project is the new one's to its environment, the same id.
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = t.table_name AND column_name = 'MonitoringId')
           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'legacy' AND table_name = t.table_name AND column_name = 'ProjectId') THEN
            EXECUTE format('INSERT INTO public.%I (%s, "MonitoringId") SELECT %s, "ProjectId" FROM legacy.%I', t.table_name, cols, cols, t.table_name);
        ELSE
            EXECUTE format('INSERT INTO public.%I (%s) SELECT %s FROM legacy.%I', t.table_name, cols, cols, t.table_name);
        END IF;
        GET DIAGNOSTICS n = ROW_COUNT;
        INSERT INTO carried VALUES (t.table_name, NULL, n, NULL);
    END LOOP;
END $$;

-- Every sequence an id comes from goes on from the largest one carried, so a row made below, or by the app later, takes
-- a new one.
CREATE FUNCTION pg_temp.go_on_from_carried_ids() RETURNS void LANGUAGE plpgsql AS $$
DECLARE s record; maxid bigint;
BEGIN
    FOR s IN
        SELECT c.table_name, c.column_name, pg_get_serial_sequence(format('public.%I', c.table_name), c.column_name) AS seq
        FROM information_schema.columns c
        WHERE c.table_schema = 'public' AND pg_get_serial_sequence(format('public.%I', c.table_name), c.column_name) IS NOT NULL
    LOOP
        EXECUTE format('SELECT max(%I) FROM public.%I', s.column_name, s.table_name) INTO maxid;
        IF maxid IS NOT NULL THEN
            PERFORM setval(s.seq, maxid);
        END IF;
    END LOOP;
END $$;

SELECT pg_temp.go_on_from_carried_ids();

-- bit Boilerplate 11.0.0-pre-25's data change, folded into the new Initial with its migration (AssistantWindowsInstallerUrl):
-- the AdminPanel demo's Windows installer's new address, in every prompt that has the old one.
UPDATE public."SystemPrompts"
SET "Markdown" = replace("Markdown", 'https://windows-admin.bitplatform.dev/AdminPanel.Client.Windows-win-Setup.exe', 'https://windows-adminpanel.bitfabric.co/AdminPanel.Client.Windows-win-Setup.exe')
WHERE strpos("Markdown", 'https://windows-admin.bitplatform.dev/AdminPanel.Client.Windows-win-Setup.exe') > 0;

-- 2. Each monitored project: a project of its own, and its environment, Production.
INSERT INTO public."Projects" ("Id", "TenantId", "Name", "BitProjectId", "FirstCommit", "CreatedOn")
SELECT "Id", "TenantId", "Name", NULL, NULL, "CreatedOn" FROM legacy."Projects";

INSERT INTO public."Monitorings" ("Id", "TenantId", "ProjectId", "Environment", "ReportedProjectId", "Address", "ServerAddress", "Name", "TemplateVersion",
                                  "IconId", "IconSourceHash", "IconReadAt", "HasStaticFrontEnd", "Locations", "Health", "ConnectionStatus", "ConnectionProblem",
                                  "CreatedOn", "TimeZoneId", "LastCheckedAt")
SELECT "Id", "TenantId", "Id", 'Production', NULL, "Address", "ServerAddress", "Name", "TemplateVersion",
       "IconId", "IconSourceHash", "IconReadAt", "HasStaticFrontEnd", "Locations", "Health", "ConnectionStatus", "ConnectionProblem",
       "CreatedOn", "TimeZoneId", "LastCheckedAt"
FROM legacy."Projects";

INSERT INTO public."ProjectConnections" ("MonitoringId", "TenantId", "Issuer", "TokenEndpoint", "Resource", "Scopes", "ClientId", "ProtectedAccessToken",
                                         "ProtectedRefreshToken", "AccessTokenExpiresAt", "ConnectedByUserId", "ConnectedOn")
SELECT c."ProjectId", c."TenantId", c."Issuer", c."TokenEndpoint", c."Resource", c."Scopes", c."ClientId", c."ProtectedAccessToken",
       c."ProtectedRefreshToken", c."AccessTokenExpiresAt", CASE WHEN EXISTS (SELECT 1 FROM legacy."Users" u WHERE u."Id" = c."ConnectedByUserId") THEN c."ConnectedByUserId" END, c."ConnectedOn"
FROM legacy."ProjectConnections" c;

-- Someone who left the team keeps no route (Leaving a Team ends its alerts).
INSERT INTO public."ProjectAlertRoutes" ("Id", "MonitoringId", "TenantId", "Step", "DelayMinutes", "Channel", "UserId", "Address", "WebhookFormat", "Culture")
SELECT r."Id", r."ProjectId", r."TenantId", r."Step", r."DelayMinutes", r."Channel", r."UserId", r."Address", r."WebhookFormat", r."Culture"
FROM legacy."ProjectAlertRoutes" r
WHERE r."UserId" IS NULL
   OR EXISTS (SELECT 1 FROM legacy."TenantMemberships" m WHERE m."TenantId" = r."TenantId" AND m."UserId" = r."UserId" AND m."AcceptedOn" IS NOT NULL);

-- Its people: whoever connected it and whoever an alert reaches, while they are accepted members of the team.
INSERT INTO public."ProjectMembers" ("ProjectId", "UserId", "TenantId", "AddedOn")
SELECT DISTINCT ON (people."ProjectId", people."UserId") people."ProjectId", people."UserId", people."TenantId", people."AddedOn"
FROM (
    SELECT c."ProjectId", c."ConnectedByUserId" AS "UserId", c."TenantId", c."ConnectedOn" AS "AddedOn" FROM legacy."ProjectConnections" c
    UNION ALL
    SELECT r."ProjectId", r."UserId", r."TenantId", p."CreatedOn" FROM legacy."ProjectAlertRoutes" r JOIN legacy."Projects" p ON p."Id" = r."ProjectId" WHERE r."UserId" IS NOT NULL
) people
WHERE EXISTS (SELECT 1 FROM legacy."TenantMemberships" m WHERE m."TenantId" = people."TenantId" AND m."UserId" = people."UserId" AND m."AcceptedOn" IS NOT NULL)
ORDER BY people."ProjectId", people."UserId", people."AddedOn";

-- 3. Trials: the codebase's, as before. One nobody licensed is counted apart (See the top).
INSERT INTO public."ProjectTrials" ("Id", "Name", "FirstCommit", "IssuedOn", "ExpiresOn", "Document", "Ci")
SELECT lp."Id", lp."Name", lp."FirstCommit", lp."TrialIssuedOn", lp."TrialExpiresOn", lp."TrialLicense",
       CASE WHEN EXISTS (SELECT 1 FROM legacy."IssuedLicenses" l WHERE l."ProjectId" = lp."Id") THEN NULL ELSE 'unknown' END
FROM legacy."LicensedProjects" lp
WHERE lp."TrialLicense" IS NOT NULL AND lp."TrialIssuedOn" IS NOT NULL AND lp."TrialExpiresOn" IS NOT NULL;

-- 4. Licenses, to a team of whoever got them.
CREATE TEMP TABLE license_teams ("UserId" uuid, "BitProjectId" uuid, "TenantId" uuid, "IsNew" boolean, PRIMARY KEY ("UserId", "BitProjectId")) ON COMMIT DROP;

INSERT INTO license_teams
SELECT pairs."UserId", pairs."ProjectId",
       COALESCE(
           (SELECT m."TenantId" FROM legacy."TenantMemberships" m JOIN legacy."Tenants" t ON t."Id" = m."TenantId"
            WHERE m."UserId" = pairs."UserId" AND m."AcceptedOn" IS NOT NULL AND t."IsActive"
              AND (lower(regexp_replace(coalesce(t."Title", ''), '[^[:alnum:]]', '', 'g')) = lower(regexp_replace(lp."Name", '[^[:alnum:]]', '', 'g'))
                   OR lower(regexp_replace(t."Name", '[^[:alnum:]]', '', 'g')) = lower(regexp_replace(lp."Name", '[^[:alnum:]]', '', 'g')))
            ORDER BY m."AcceptedOn" LIMIT 1),
           (SELECT m."TenantId" FROM legacy."TenantMemberships" m JOIN legacy."Tenants" t ON t."Id" = m."TenantId"
            WHERE m."UserId" = pairs."UserId" AND m."AcceptedOn" IS NOT NULL AND t."IsActive"
            ORDER BY m."AcceptedOn", t."Name" LIMIT 1)),
       false
FROM (SELECT DISTINCT l."UserId", l."ProjectId" FROM legacy."IssuedLicenses" l) pairs
JOIN legacy."LicensedProjects" lp ON lp."Id" = pairs."ProjectId";

-- Someone in no team gets one named after them, with them as its admin, as TenantController.Create makes one: its admin
-- role, its demo role with the fallback team's demo features, and its support prompt.
CREATE TEMP TABLE new_teams ("UserId" uuid PRIMARY KEY, "TenantId" uuid, "AdminRoleId" uuid, "DemoRoleId" uuid) ON COMMIT DROP;

INSERT INTO new_teams
SELECT DISTINCT lt."UserId", gen_random_uuid(), gen_random_uuid(), gen_random_uuid() FROM license_teams lt WHERE lt."TenantId" IS NULL;

INSERT INTO public."Tenants" ("Id", "Name", "Title", "Domain", "IsActive")
SELECT nt."TenantId",
       left(coalesce(nullif(lower(regexp_replace(coalesce(u."FullName", split_part(u."Email", '@', 1), 'team'), '[^[:alnum:]]', '', 'g')), ''), 'team'), 40) || '-' || left(replace(nt."TenantId"::text, '-', ''), 8),
       left(coalesce(nullif(trim(u."FullName"), ''), split_part(u."Email", '@', 1), 'Team'), 64),
       NULL, true
FROM new_teams nt JOIN legacy."Users" u ON u."Id" = nt."UserId";

INSERT INTO public."TenantMemberships" ("Id", "TenantId", "UserId", "AcceptedOn")
SELECT gen_random_uuid(), nt."TenantId", nt."UserId", now() FROM new_teams nt;

INSERT INTO public."Roles" ("Id", "Name", "NormalizedName", "TenantId", "ConcurrencyStamp")
SELECT nt."AdminRoleId", 'TenantAdmin', 'TENANTADMIN', nt."TenantId", gen_random_uuid()::text FROM new_teams nt
UNION ALL
SELECT nt."DemoRoleId", 'Demo', 'DEMO', nt."TenantId", gen_random_uuid()::text FROM new_teams nt;

INSERT INTO public."UserRoles" ("UserId", "RoleId", "TenantId")
SELECT nt."UserId", nt."AdminRoleId", nt."TenantId" FROM new_teams nt;

INSERT INTO public."RoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT nt."DemoRoleId", rc."ClaimType", rc."ClaimValue"
FROM new_teams nt
CROSS JOIN legacy."RoleClaims" rc
JOIN legacy."Roles" r ON r."Id" = rc."RoleId" AND r."NormalizedName" = 'DEMO' AND r."TenantId" = 'b1f71671-a1d6-4f97-abb9-d87d7b47d6e1';

INSERT INTO public."SystemPrompts" ("Id", "PromptKind", "Markdown", "TenantId")
SELECT gen_random_uuid(), sp."PromptKind", sp."Markdown", nt."TenantId"
FROM new_teams nt
CROSS JOIN LATERAL (SELECT * FROM legacy."SystemPrompts" s WHERE s."TenantId" = 'b1f71671-a1d6-4f97-abb9-d87d7b47d6e1' ORDER BY s."PromptKind" LIMIT 1) sp;

UPDATE license_teams lt SET "TenantId" = nt."TenantId", "IsNew" = true FROM new_teams nt WHERE nt."UserId" = lt."UserId" AND lt."TenantId" IS NULL;

-- The team's project of each id, made once, named and committed as its newest license has it.
INSERT INTO public."Projects" ("Id", "TenantId", "Name", "BitProjectId", "FirstCommit", "CreatedOn")
SELECT gen_random_uuid(), teams."TenantId", lp."Name", lp."Id",
       (SELECT l."FirstCommit" FROM legacy."IssuedLicenses" l JOIN license_teams x ON x."UserId" = l."UserId" AND x."BitProjectId" = l."ProjectId"
        WHERE l."ProjectId" = lp."Id" AND x."TenantId" = teams."TenantId" ORDER BY l."IssuedOn" DESC LIMIT 1),
       lp."CreatedOn"
FROM (SELECT DISTINCT "TenantId", "BitProjectId" FROM license_teams) teams
JOIN legacy."LicensedProjects" lp ON lp."Id" = teams."BitProjectId";

INSERT INTO public."IssuedLicenses" ("Id", "TenantId", "ProjectId", "IssuedByUserId", "Plan", "FirstCommit", "IssuedOn", "ExpiresOn", "Document",
                                     "ClaimCodeHash", "ClaimExpiresOn", "ClaimedOn")
SELECT l."Id", lt."TenantId", p."Id", l."UserId", l."Plan", l."FirstCommit", l."IssuedOn", l."ExpiresOn", l."Document",
       l."ClaimCodeHash", l."ClaimExpiresOn", l."ClaimedOn"
FROM legacy."IssuedLicenses" l
JOIN license_teams lt ON lt."UserId" = l."UserId" AND lt."BitProjectId" = l."ProjectId"
JOIN public."Projects" p ON p."TenantId" = lt."TenantId" AND p."BitProjectId" = l."ProjectId";

INSERT INTO public."ProjectMembers" ("ProjectId", "UserId", "TenantId", "AddedOn")
SELECT DISTINCT ON (p."Id", lt."UserId") p."Id", lt."UserId", lt."TenantId", (SELECT min(l."IssuedOn") FROM legacy."IssuedLicenses" l WHERE l."UserId" = lt."UserId" AND l."ProjectId" = lt."BitProjectId")
FROM license_teams lt
JOIN public."Projects" p ON p."TenantId" = lt."TenantId" AND p."BitProjectId" = lt."BitProjectId"
ON CONFLICT DO NOTHING;

SELECT pg_temp.go_on_from_carried_ids();

SET LOCAL session_replication_role = origin;

-- 6. Every foreign key holds for every row carried, as if each had been checked on insert.
DO $$
DECLARE fk record; orphans bigint; cond text;
BEGIN
    FOR fk IN
        SELECT con.conname, cl.relname AS child, pcl.relname AS parent,
               (SELECT string_agg(format('c.%I = p.%I', a.attname, pa.attname), ' AND ')
                FROM unnest(con.conkey, con.confkey) AS k(child_col, parent_col)
                JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.child_col
                JOIN pg_attribute pa ON pa.attrelid = con.confrelid AND pa.attnum = k.parent_col) AS joins,
               (SELECT string_agg(format('c.%I IS NOT NULL', a.attname), ' AND ')
                FROM unnest(con.conkey) AS k(child_col)
                JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.child_col) AS present
        FROM pg_constraint con
        JOIN pg_class cl ON cl.oid = con.conrelid
        JOIN pg_class pcl ON pcl.oid = con.confrelid
        JOIN pg_namespace n ON n.oid = cl.relnamespace
        WHERE con.contype = 'f' AND n.nspname = 'public'
    LOOP
        EXECUTE format('SELECT count(*) FROM public.%I c WHERE %s AND NOT EXISTS (SELECT 1 FROM public.%I p WHERE %s)', fk.child, fk.present, fk.parent, fk.joins) INTO orphans;
        IF orphans > 0 THEN
            RAISE EXCEPTION 'Foreign key % of %: % rows point to no row of %.', fk.conname, fk.child, orphans, fk.parent;
        END IF;
    END LOOP;
END $$;

-- 7. Every table accounted for: what the old schema held and what the new one holds, and why a count differs.
DO $$
DECLARE t record; n bigint;
BEGIN
    FOR t IN SELECT table_schema, table_name FROM information_schema.tables WHERE table_schema IN ('legacy', 'legacy_jobs') AND table_type = 'BASE TABLE'
    LOOP
        EXECUTE format('SELECT count(*) FROM %I.%I', t.table_schema, t.table_name) INTO n;
        INSERT INTO carried (table_name, before) VALUES (CASE WHEN t.table_schema = 'legacy_jobs' THEN 'jobs.' ELSE '' END || t.table_name, n)
        ON CONFLICT (table_name) DO UPDATE SET before = excluded.before;
    END LOOP;
    FOR t IN SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
    LOOP
        EXECUTE format('SELECT count(*) FROM public.%I', t.table_name) INTO n;
        INSERT INTO carried (table_name, after) VALUES (t.table_name, n)
        ON CONFLICT (table_name) DO UPDATE SET after = excluded.after;
    END LOOP;
END $$;

UPDATE carried SET note = 'not carried: Hangfire keeps its jobs in Redis' WHERE table_name LIKE 'jobs.%';
UPDATE carried SET note = 'migrations history: the new Initial''s own' WHERE table_name = '__EFMigrationsHistory';
UPDATE carried SET note = format('each old project made a project of its own (%s); the licenses made %s more', (SELECT count(*) FROM legacy."Projects"), after - (SELECT count(*) FROM legacy."Projects")) WHERE table_name = 'Projects';
UPDATE carried SET before = (SELECT count(*) FROM legacy."Projects"), note = 'the old "Projects", one environment each' WHERE table_name = 'Monitorings';
UPDATE carried SET note = format('%s routes to someone no longer in the team dropped', before - after) WHERE table_name = 'ProjectAlertRoutes';
UPDATE carried SET note = 'new: who connected a project, whom its alerts reach, who got its license' WHERE table_name = 'ProjectMembers';
UPDATE carried SET before = (SELECT count(*) FROM legacy."LicensedProjects" WHERE "TrialLicense" IS NOT NULL),
                   note = format('from "LicensedProjects" with a trial; %s marked Ci = unknown', (SELECT count(*) FROM public."ProjectTrials" WHERE "Ci" = 'unknown')) WHERE table_name = 'ProjectTrials';
UPDATE carried SET note = 'into "ProjectTrials" and teams'' "Projects"' WHERE table_name = 'LicensedProjects';
UPDATE carried SET note = format('each to a team: %s new teams made', (SELECT count(*) FROM new_teams)) WHERE table_name = 'IssuedLicenses';
UPDATE carried SET note = format('%s made for people in no team', (SELECT count(*) FROM new_teams)) WHERE table_name IN ('Tenants', 'TenantMemberships', 'Roles', 'UserRoles', 'SystemPrompts', 'RoleClaims') AND (SELECT count(*) FROM new_teams) > 0;

-- A count that differs without a reason above stops everything.
DO $$
DECLARE bad text;
BEGIN
    SELECT string_agg(format('%s: %s before, %s after', table_name, before, after), '; ') INTO bad
    FROM carried
    WHERE coalesce(before, -1) <> coalesce(after, -1) AND note IS NULL;
    IF bad IS NOT NULL THEN
        RAISE EXCEPTION 'Rows not accounted for: %', bad;
    END IF;
    IF (SELECT count(*) FROM public."IssuedLicenses") <> (SELECT count(*) FROM legacy."IssuedLicenses") THEN
        RAISE EXCEPTION 'Every license is carried, to a team: % of %.', (SELECT count(*) FROM public."IssuedLicenses"), (SELECT count(*) FROM legacy."IssuedLicenses");
    END IF;
    IF (SELECT count(*) FROM public."Monitorings") <> (SELECT count(*) FROM legacy."Projects") THEN
        RAISE EXCEPTION 'Every monitored project is carried.';
    END IF;
END $$;

CREATE TABLE public."__CarriedOver" ("CarriedAt" timestamptz NOT NULL, "Report" text NOT NULL);
INSERT INTO public."__CarriedOver"
SELECT now(), string_agg(format('%s | %s | %s | %s', table_name, coalesce(before::text, '-'), coalesce(after::text, '-'), coalesce(note, '')), E'\n' ORDER BY table_name) FROM carried;

\pset format aligned
SELECT table_name AS "table", before, after, coalesce(note, '') AS note FROM carried ORDER BY table_name;

COMMIT;
