# FarmPlatform

A platform for Georgia's fruit-picking season that brings together the three groups who make a harvest happen:

- **Pickers** looking for seasonal work in orchards
- **Drivers** with vehicles who take workers to and from the orchards
- **Orchard owners** who need workers and transport during the harvest

One account can hold several roles; someone can be both a picker and a driver. The site is in **Georgian** by
default, with English available.

### What works today
- Sign-up, login, email verification and password reset
- A registration wizard per role: personal details, vehicles, orchards, with locations picked on a map
- A dashboard and a profile page to view and edit your information
- **Linked pickers:** many pickers don't use smartphones. Any user can add them, and after the picker agrees by
  SMS, that user can answer picking requests for them and, if the picker chooses, receive their pay
- **Admin panel:** administrators can see everyone registered, by role, with search
- Personal ID numbers and bank accounts are stored encrypted

**Built with:** React + TypeScript (Vite, Tailwind) for the website, ASP.NET Core (.NET 10) with PostgreSQL for
the backend.

## Getting started

### 1. Install
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22 LTS](https://nodejs.org) (22.22 or newer)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), and make sure it's running

### 2. One-time setup
From the project folder:

```bash
./scripts/setup-dev.sh          # macOS / Linux
```
```powershell
.\scripts\setup-dev.ps1       # Windows (if blocked, first run: Unblock-File .\scripts\setup-dev.ps1)
```

This starts the database in Docker, creates local encryption keys (kept outside the project), sets up the
database, runs the tests and builds the website.

### 3. Run it
Use two terminals:

```bash
cd backend
dotnet run --project src/FarmPlatform.API      # backend: http://localhost:5080
```
```bash
cd frontend
npm run dev                                     # website: http://localhost:5173
```

Open **http://localhost:5173**. The footer shows **სერვერი: მუშაობს** (Server: online) when everything is
connected. In development, emails and SMS aren't really sent: they're printed in the backend terminal, including
the email verification link.

If the backend says it can't connect to the database, start Docker Desktop and run `docker compose up -d`.

### 4. Make yourself an admin (optional)
Register and verify an account on the website, then stop the backend and run:

```bash
cd backend
dotnet run --project src/FarmPlatform.API -- make-admin you@example.com
```

Start the backend again and reload the site; an **ადმინი** (Admin) link appears in the header.

### 5. Google Maps (optional)
Without a key, location fields fall back to "use my current location". To get the map, create
`frontend/.env.local` with `VITE_GOOGLE_MAPS_API_KEY=...`. See [developer notes](docs/DEVELOPMENT.md#google-maps-setup)
for which Google APIs to enable.

### Looking at the database
Connect pgAdmin or DBeaver to `127.0.0.1`, port `15432`, database `farmplatform_dev`, user `farmplatform`,
password `farmplatform_dev_pw` (local development only).

## Future plans

- **Picking events:** orchard owners post harvest dates and how many pickers and drivers they need; pickers and
  drivers (or the people answering for linked pickers) reply whether they can come
- **Profile management:** add, edit and remove vehicles and orchards; switch roles on or off; profile photos
- **Account settings:** change password, phone number and language
- **Real SMS sending** through an SMS provider, needed before launch
- **Admin tools:** export lists to Excel
- **Going live:** website on Cloudflare Pages, backend on Render, database on Neon, files on Cloudflare R2

---

Technical details (architecture, API endpoints, validation rules, configuration) are in
[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).
