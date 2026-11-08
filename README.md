# StudioFlow

Multi-tenant booking platform for recording and rehearsal studios.

MVP includes JWT auth, Admin/Manager/Client roles, tenant-isolated rooms and bookings, cancellation, overlap protection, PostgreSQL, EF Core, Swagger, Serilog and Docker Compose.

```bash
docker compose up -d postgres
dotnet run --project StudioFlow
```

Open `/swagger`. Register a user with an arbitrary non-empty `tenantId` GUID and use the returned token as `Bearer` authorization. For production, replace the development JWT key with a secret from the environment or a secret manager.
