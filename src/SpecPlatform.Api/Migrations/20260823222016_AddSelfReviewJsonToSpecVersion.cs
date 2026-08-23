using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfReviewJsonToSpecVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Create all core tables (IF NOT EXISTS) ─────────────────────
            // Safe for both a brand-new database AND the existing prod DB where these
            // tables were created outside of EF migrations via EnsureCreated().

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Projects"" (
                    ""Id""          SERIAL PRIMARY KEY,
                    ""Name""        TEXT NOT NULL DEFAULT '',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""CreatedAt""   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS ""Specs"" (
                    ""Id""          SERIAL PRIMARY KEY,
                    ""ProjectId""   INTEGER NOT NULL REFERENCES ""Projects""(""Id"") ON DELETE CASCADE,
                    ""Title""       TEXT NOT NULL DEFAULT '',
                    ""Description"" TEXT NOT NULL DEFAULT '',
                    ""Status""      TEXT NOT NULL DEFAULT 'Draft',
                    ""CreatedAt""   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ""IX_Specs_ProjectId"" ON ""Specs"" (""ProjectId"");

                CREATE TABLE IF NOT EXISTS ""SpecVersions"" (
                    ""Id""             SERIAL PRIMARY KEY,
                    ""SpecId""         INTEGER NOT NULL REFERENCES ""Specs""(""Id"") ON DELETE CASCADE,
                    ""VersionNumber""  INTEGER NOT NULL DEFAULT 1,
                    ""Content""        TEXT NOT NULL DEFAULT '',
                    ""PublishedAt""    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""SelfReviewJson"" TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_SpecVersions_SpecId"" ON ""SpecVersions"" (""SpecId"");

                CREATE TABLE IF NOT EXISTS ""AcceptanceCriteria"" (
                    ""Id""               SERIAL PRIMARY KEY,
                    ""SpecVersionId""    INTEGER NOT NULL REFERENCES ""SpecVersions""(""Id"") ON DELETE CASCADE,
                    ""Text""             TEXT NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS ""IX_AcceptanceCriteria_SpecVersionId"" ON ""AcceptanceCriteria"" (""SpecVersionId"");

                CREATE TABLE IF NOT EXISTS ""ScopeTags"" (
                    ""Id""               SERIAL PRIMARY KEY,
                    ""SpecVersionId""    INTEGER NOT NULL REFERENCES ""SpecVersions""(""Id"") ON DELETE CASCADE,
                    ""TagName""          TEXT NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS ""IX_ScopeTags_SpecVersionId"" ON ""ScopeTags"" (""SpecVersionId"");

                CREATE TABLE IF NOT EXISTS ""ChatSessions"" (
                    ""Id""           SERIAL PRIMARY KEY,
                    ""ProjectId""    INTEGER NOT NULL REFERENCES ""Projects""(""Id"") ON DELETE CASCADE,
                    ""PersonaMode""  TEXT NOT NULL DEFAULT 'po_brainstorming',
                    ""CreatedAt""    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""UpdatedAt""    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ""IX_ChatSessions_ProjectId"" ON ""ChatSessions"" (""ProjectId"");

                CREATE TABLE IF NOT EXISTS ""ChatMessages"" (
                    ""Id""              SERIAL PRIMARY KEY,
                    ""ChatSessionId""   INTEGER NOT NULL REFERENCES ""ChatSessions""(""Id"") ON DELETE CASCADE,
                    ""Role""            TEXT NOT NULL DEFAULT 'user',
                    ""Content""         TEXT NOT NULL DEFAULT '',
                    ""Timestamp""       TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ""IX_ChatMessages_ChatSessionId"" ON ""ChatMessages"" (""ChatSessionId"");

                CREATE TABLE IF NOT EXISTS ""Notifications"" (
                    ""Id""              SERIAL PRIMARY KEY,
                    ""SpecId""          INTEGER NOT NULL,
                    ""SpecTitle""       TEXT NOT NULL DEFAULT '',
                    ""ProjectName""     TEXT NOT NULL DEFAULT '',
                    ""VersionNumber""   INTEGER NOT NULL DEFAULT 1,
                    ""SummaryText""     TEXT NOT NULL DEFAULT '',
                    ""CreatedAt""       TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS ""Users"" (
                    ""Id""           SERIAL PRIMARY KEY,
                    ""GitHubId""     TEXT NOT NULL DEFAULT '',
                    ""Username""     TEXT NOT NULL DEFAULT '',
                    ""DisplayName""  TEXT NOT NULL DEFAULT '',
                    ""Email""        TEXT NOT NULL DEFAULT '',
                    ""AvatarUrl""    TEXT NOT NULL DEFAULT '',
                    ""CreatedAt""    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""LastLoginAt""  TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // ── Step 2: Add the SelfReviewJson column (IF NOT EXISTS) ───────────────
            // Safe on existing prod DB (column may already exist from a manual ALTER),
            // and works on a fresh DB where SpecVersions was just created above with
            // SelfReviewJson already included — Postgres silently skips it if present.
            migrationBuilder.Sql(@"
                ALTER TABLE ""SpecVersions""
                ADD COLUMN IF NOT EXISTS ""SelfReviewJson"" TEXT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelfReviewJson",
                table: "SpecVersions");
        }
    }
}
