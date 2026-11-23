# FarmPlatform

Connects fruit pickers, drivers and orchard owners in Georgia for the harvest season. One account can hold several roles. Users can also add **linked pickers** (people without smartphones) and answer for them once they agree by SMS. Georgian by default, English available.

**Stack:** React + TypeScript (Vite, Tailwind) · ASP.NET Core .NET 10 · PostgreSQL

## Setup

Requires .NET 10 SDK, Node.js 22, Docker Desktop (running).

```powershell
.\scripts\setup-dev.ps1        # once (macOS/Linux: ./scripts/setup-dev.sh)
```

Run in two terminals:

```bash
cd backend;  dotnet run --project src/FarmPlatform.API    # http://localhost:5080
cd frontend; npm run dev                                   # http://localhost:5173
```

Make an account admin (after registering and verifying it):

```bash
cd backend; dotnet run --project src/FarmPlatform.API -- make-admin you@example.com
```

Optional: put `VITE_GOOGLE_MAPS_API_KEY=...` in `frontend/.env.local` for maps.

## Future plans

- Picking events: owners post harvest dates, pickers and drivers respond
- Vehicle and orchard management, profile photos, account settings
- Real SMS sending, Excel export
- Deployment: Cloudflare Pages, Render, Neon

Technical details: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
