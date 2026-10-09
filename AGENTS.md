# Instructions for Coding Agents

## Scope

These instructions apply to the entire repository. Also follow any `AGENTS.md` files in nested directories. Respond to the user in Ukrainian unless they request another language.

## Project Context

This is the backend of a Threads-style social application built with .NET 10, ASP.NET Core, EF Core, and PostgreSQL. It also uses Redis/HybridCache, JWT, S3, and external integrations.

Before making changes, read the relevant sections of `README.md` and inspect the current implementation. Code and configuration are the source of truth when documentation differs. Do not invent entities, fields, routes, or capabilities that do not exist in the code.

## Architecture

- `Threads.Domain`: entities, enums, and domain models. Do not add dependencies on API or Infrastructure.
- `Threads.Application`: business logic, DTOs, mapping, and service and repository contracts.
- `Threads.Infrastructure`: EF Core, repository implementations, security, and external integrations.
- `Threads.Api`: HTTP controllers, authorization, middleware, error handling, and dependency registration.
- `tests/Threads.Application.UnitTests`: unit tests using xUnit and NSubstitute.
- `tests/Threads.Infrastructure.IntegrationTests`: integration tests using PostgreSQL through Testcontainers.

Keep controllers short: HTTP binding and application service calls. Business rules and ownership checks belong in the application layer; database queries belong in Infrastructure. Register new dependencies through the existing DI configuration and interfaces.

## Change Guidelines

- Keep changes within the current task; avoid unrelated refactoring.
- Preserve the user's existing uncommitted changes.
- Follow the existing C# style, nullable annotations, async patterns, and naming conventions in the relevant module.
- Pass `CancellationToken` through asynchronous calls that support it.
- Use the existing DTOs, cursor pagination, and `ProblemDetails` error format.
- Do not change public API contracts unless required by the current task. Document contract changes.
- Do not add packages or services without a concrete need.
- Do not store or log passwords, tokens, API keys, or actual connection strings.
- Do not commit, push, or deploy unless explicitly requested by the user.

## PostgreSQL and EF Core

The schema is defined in `Threads.Infrastructure/Data/ThreadsDbContext.cs`, table configurations, and migrations. Check the actual keys, relationships, and indexes before making changes.

- For reads, use `AsNoTracking` where tracking is unnecessary and project only the required fields.
- Perform filtering, aggregation, and result limiting in the database. Avoid N+1 queries and loading entire tables into memory.
- Preserve visibility, soft deletion, and account activity rules. Using `IgnoreQueryFilters` requires a justified need and explicit access checks.
- Parameterize SQL; do not concatenate user values into queries.
- Represent schema changes through EF Core migrations and update the model snapshot. Do not rewrite migration history unless explicitly requested.
- Do not apply migrations to external or production databases without explicit permission. Integration tests may apply them to their own test containers.

## Recommendations Based on the Interaction Graph

This section applies when the task concerns recommendations; it is not an instruction to implement them for every task.

- Represent the graph through existing PostgreSQL tables: users and posts are nodes; follows and interactions are edges.
- Inspect the existing `Follow`, likes, comments, reposts, bookmarks, and views. Do not add Neo4j or ML infrastructure without a justified task requirement.
- For posts, consider interactions with authors, shared preferences, freshness, and author diversity.
- For follow recommendations, consider shared connections and interactions; exclude the current user and accounts they already follow.
- Follow the actual access and moderation rules; do not return inaccessible objects or duplicates.
- Limit the number of candidates and the influence of repeated interactions. A view should be a weaker signal than an explicit interaction.
- Provide fallback recommendations for users without interaction history.
- Document the ranking formula and configurable weights. Initial weights are heuristic until validated against data.
- Ensure deterministic ordering for equal scores and explain pagination behavior when scores change.

## Validation

Run commands from the repository root:

```bash
dotnet build BackEndForFinalProject.sln
dotnet test tests/Threads.Application.UnitTests
dotnet test tests/Threads.Infrastructure.IntegrationTests
```

Integration tests require Docker to be running: they create their own PostgreSQL container. The application's `docker-compose.yml` does not start PostgreSQL.

Start with checks for the changed module. For behavior changes, add meaningful tests to the existing test projects; validate PostgreSQL-specific queries with integration tests. Documentation-only changes do not require a build or tests.

Update documentation when changing behavior, APIs, or configuration. In the final response, briefly state what changed, which checks were performed, and any remaining limitations. Explicitly report checks that were skipped or failed.
