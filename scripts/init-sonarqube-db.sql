-- Runs automatically the first time the Postgres container initialises an empty
-- data directory (docker-entrypoint-initdb.d). SonarQube requires its own empty
-- database — sharing the application's `codereview` database makes SonarQube drop
-- ~200 of its own tables alongside the app's `ReviewReports` table.
--
-- NOTE: this file does NOT run against an already-initialised volume. If your
-- postgres volume already exists, create the database once by hand instead:
--   docker exec codereview-postgres psql -U codereview -d codereview -c "CREATE DATABASE sonarqube OWNER codereview;"
CREATE DATABASE sonarqube OWNER codereview;
