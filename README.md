# FarmPlatform

Platform connecting fruit pickers, drivers and orchard owners.

- **Phase 1:** foundation. Solution structure, database model, EF Core + PostgreSQL, ASP.NET Core Identity,
  security plumbing (cookies, CSRF, CORS, rate limiting), and the React shell (Georgian by default).
- **Phase 2:** accounts. Registration, login/logout, email verification, forgot/reset password, session
  handling in the SPA, and protected routes.
- **Phase 3:** registration wizard (categories → personal details / vehicles / orchards → review → submit),
  Google Maps location picker, dashboard category cards, and the profile, vehicle and orchard APIs with
  ownership checks.
- **Phase 4 (next):** profile management pages, profile photo (Cloudflare R2), and account settings.

## Repository layout

```
FarmPlatform/
├─ docker-compose.yml                 local PostgreSQL 17
├─ scripts/setup-dev.sh | .ps1        one-shot setup + verification
├─ backend/
│  ├─ FarmPlatform.slnx
│  ├─ Directory.Build.props           net10.0, nullable, analyzers
│  ├─ Directory.Packages.props        all NuGet versions in one place
│  ├─ .config/dotnet-tools.json       dotnet-ef 10.0.15 (local tool)
│  ├─ Dockerfile                      Render image
│  ├─ src/
│  │  ├─ FarmPlatform.Domain          entities, value objects, business rules (no dependencies)
│  │  ├─ FarmPlatform.Application     use-case contracts: IAppDbContext, ICurrentUser, ISensitiveDataProtector, IFileStorage
│  │  ├─ FarmPlatform.Infrastructure  EF Core DbContext + configurations, Identity, encryption, (later) R2
│  │  └─ FarmPlatform.API             Program.cs, controllers, auth/CSRF/CORS/rate-limit setup, error handling
│  └─ tests/FarmPlatform.Tests        unit, model and HTTP pipeline tests (xUnit v3)
└─ frontend/                          React 19 + TypeScript + Vite + Tailwind v4 + React Router + i18next
```

Project references point inward only: `API → Application + Infrastructure`, `Infrastructure → Application → Domain`.

## Run it locally

Prerequisites: **.NET 10 SDK**, **Node.js 22 LTS** (20.19+ works), **Docker**.

```bash
./scripts/setup-dev.sh        # Windows: pwsh scripts/setup-dev.ps1
```

The script starts PostgreSQL, generates development encryption keys into **user-secrets** (outside the repo),
builds, **creates the `InitialCreate` migration** on first run, applies it, runs the tests and builds the frontend.

Then, in two terminals:

```bash
cd backend && dotnet run --project src/FarmPlatform.API   # http://localhost:5080
cd frontend && npm run dev                                 # http://localhost:5173
```

How to check it works:
- `curl http://localhost:5080/health/ready`: `Healthy` means the API reached PostgreSQL.
- `curl http://localhost:5080/api/v1/lookups/legal-entity-statuses`: returns the 4 seeded legal forms.
- The site footer (dev only) shows **სერვერი: მუშაობს** (Server: online).
- `http://localhost:5080/openapi/v1.json` returns the OpenAPI document (development only).

Commit the generated `backend/src/FarmPlatform.Infrastructure/Persistence/Migrations/` folder.

<details><summary>Manual commands (what the script runs)</summary>

```bash
docker compose up -d --wait postgres
cd backend
dotnet tool restore && dotnet restore
dotnet user-secrets set "Encryption:ActiveKeyId" "k1" --project src/FarmPlatform.API
dotnet user-secrets set "Encryption:Keys:k1" "$(openssl rand -base64 32)" --project src/FarmPlatform.API
dotnet user-secrets set "Encryption:BlindIndexKey" "$(openssl rand -base64 32)" --project src/FarmPlatform.API
dotnet build
dotnet ef migrations add InitialCreate --project src/FarmPlatform.Infrastructure --startup-project src/FarmPlatform.API --output-dir Persistence/Migrations
dotnet ef database update --project src/FarmPlatform.Infrastructure --startup-project src/FarmPlatform.API
dotnet test
cd ../frontend && npm ci && npm run dev
```
</details>

## Database model

```
AspNetUsers (ApplicationUser: Identity + ProfilePhotoKey, PreferredLanguage, IsActive, CreatedAt, UpdatedAt)
 ├─1:0..1─ PersonalDetails        name, encrypted personal ID + bank account, addresses, Latitude/Longitude
 ├─1:0..1─ PickerProfiles         category row (IsActive, DeactivatedAt)
 ├─1:0..1─ DriverProfiles ─1:N─ Vehicles        plate, model, passenger capacity (CHECK 1..60)
 └─1:0..1─ OrchardOwnerProfiles ─1:N─ Orchards  legal status, name, ID number, address, lat/lng, area (ha)
                                                └─N:1─ LegalEntityStatuses (seeded, configurable)
DataProtectionKeys                (ASP.NET Core key ring, see decision 8)
```

Coordinates are `numeric(9,6)` (about 11 cm precision). Orchard area is `numeric(12,4)` hectares. All ids are UUIDv7.

## Architecture decisions

The spec asks for alternatives and a recommendation wherever a decision wasn't clear-cut. These are the choices I made, so you can push back now, while changing them is still cheap.

1. **One shared `PersonalDetails` row per user instead of duplicate fields on `PickerProfile` and `DriverProfile`.**
   The spec's tables repeat first name, ID number, bank account and addresses on both profiles. With duplicates, a Picker+Driver
   would enter the same data twice, and the two copies could drift apart.
   *Alternatives:* duplicate fields (simplest, but data drifts apart), or one table with a "type" column.
   *Chosen:* personal data lives once in `PersonalDetails`. The category tables are thin rows that record the category
   and will later hold role-specific data (skills, availability, licences).

2. **Categories are deactivated, not deleted.** `RoleProfile.Deactivate()` sets `IsActive=false`. The account, its personal
   data and its history are kept, and the category can be re-enabled without retyping anything. A separate
   `OrchardOwnerProfile` gives Orchard the same add/remove behaviour as the other two categories.

3. **Application-level encryption for personal ID and bank account numbers.** AES-256-GCM with a random nonce and
   key-id prefix, which allows key rotation. A keyed HMAC "blind index" lets the database enforce *one person per account*
   (unique index) without storing the ID number in readable form. A `Last4` column supports masked display.
   *Alternatives:* PostgreSQL `pgcrypto` (puts keys in SQL and logs), ASP.NET Data Protection (fine for tokens, but no
   deterministic lookup), or nothing (the spec says protect). Keys come only from environment variables or user-secrets.

4. **Authentication: Identity cookie, same-site subdomains.** Recommended production layout: frontend `https://example.ge`
   (Cloudflare Pages) and API `https://api.example.ge` (Render custom domain). These are the same *site*, so the
   `__Host-` session cookie (HttpOnly, Secure, SameSite=Lax) works with `credentials: "include"`. A `*.pages.dev` plus
   `*.onrender.com` setup would make the cookie third-party, and browsers increasingly block those.
   *Alternatives:* JWT in localStorage (readable by XSS, and the spec asks for HttpOnly cookies), or proxying `/api` through
   Cloudflare (workable, adds a hop). In development, Vite proxies `/api`, so everything is same-origin.

5. **CSRF: synchronizer token on every unsafe request.** The SPA gets a token from `GET /api/v1/auth/csrf`, keeps it in
   memory and sends `X-XSRF-TOKEN`. A global MVC filter validates it, so new endpoints are protected by default.
   Together with SameSite cookies and an explicit CORS origin list, this covers cross-site request forgery.

6. **No generic repositories.** Use cases go through `IAppDbContext` (EF Core is already a unit of work). Repositories get
   added only where a query is complex enough to deserve a name.

7. **Uniqueness rules ("consider legitimate shared information"):**
   - Personal ID number: unique, through the blind index. One person, one account.
   - Licence plate: unique *per driver*, not globally, because a family car can have two registered drivers.
   - Orchard identification number: indexed, **not unique**, because one company can run several orchards.
   - Phone number: indexed, not unique, because family members sometimes share a phone.

8. **Data Protection keys are stored in PostgreSQL.** They encrypt the session cookie and the email-confirmation and
   password-reset tokens. Without persistence, every Render redeploy would sign everyone out and break pending reset links.

9. **Migrations do not run on production startup.** Locally, the app migrates on start (`Database:MigrateOnStartup`). For
   production, a migration bundle runs as a deploy step (set up in the deployment phase), so a failed migration never
   leaves the app half-started.

10. **.NET 10 (LTS) rather than .NET 11.** .NET 11 shipped this month, but it is a short-term release. .NET 10 is supported
    until November 2028. All packages are pinned to the 10.0.x line in `Directory.Packages.props` (EF Core 10.0.15,
    Npgsql 10.0.4). **Do not accept 11.x package updates while targeting net10.0.**

11. **Legal entity statuses are a lookup table**, not an enum, so options can be added or renamed without a deployment.
    Codes: `individual_entrepreneur`, `llc`, `natural_person`, `other`.

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings__Default` | env / user-secrets | Neon: `Host=…neon.tech;Database=…;Username=…;Password=…;SSL Mode=Require;Channel Binding=Require`. Use the **direct** (non-pooler) host for migrations. |
| `Encryption__ActiveKeyId`, `Encryption__Keys__k1`, `Encryption__BlindIndexKey` | env / user-secrets | 32 random bytes, base64. **Back up the blind-index key**: losing it breaks duplicate detection, and losing data keys makes stored data unreadable. |
| `Cors__AllowedOrigins__0` | env | e.g. `https://example.ge` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` | set in Dockerfile | Render terminates TLS. Required for Secure cookies, HSTS and per-IP rate limits. |
| `Frontend__BaseUrl` | env | Used for links in emails, e.g. `https://example.ge` |
| `Email__Provider` | env | `Log` (dev, prints emails) or `Smtp` |
| `Email__FromAddress`, `Email__FromName` | env | Sender, e.g. `no-reply@example.ge` |
| `Email__Smtp__Host`, `__Port`, `__Username`, `__Password`, `__EnableSsl` | env | SMTP relay credentials |
| `RateLimiting__AuthPermitLimit` | env | Auth requests per minute per IP (default 10) |
| `VITE_API_BASE_URL` | Cloudflare Pages env | empty in dev; `https://api.example.ge` in prod |

## Phase 2: accounts and authentication

### API

| Endpoint | Auth | Notes |
|---|---|---|
| `GET  /api/v1/auth/csrf` | – | CSRF token for the `X-XSRF-TOKEN` header. Fetch again after login/register/logout. |
| `POST /api/v1/auth/register` | – | email, password, confirmPassword, phoneNumber, acceptTerms, preferredLanguage. Signs in and emails a verification link. |
| `POST /api/v1/auth/login` | – | email, password, rememberMe. Locks for 15 min after 5 failures. |
| `POST /api/v1/auth/logout` | – | Ends the session. |
| `POST /api/v1/auth/verify-email` | – | userId, token (from the email link). |
| `POST /api/v1/auth/resend-verification` | ✔ | Sends the verification email again. |
| `POST /api/v1/auth/forgot-password` | – | Always 202, so it doesn't reveal whether the account exists. |
| `POST /api/v1/auth/reset-password` | – | email, token, newPassword, confirmPassword. Also confirms the email. |
| `GET  /api/v1/auth/me` | ✔ | Current user, or 401. |

Errors are RFC 9457 problems with a stable `code` (e.g. `auth.email_taken`) and `errors: { field: [codes] }`.
The frontend translates codes in `frontend/src/i18n/locales/*.json` under `errors`.
Auth endpoints are rate limited to 10 requests per minute per IP (`RateLimiting:AuthPermitLimit`).

### Emails in development

With `Email:Provider = Log` (the default), emails are **not sent**. They're printed in the backend terminal,
including the verification and reset links. Open the link from there to complete the flow.

For production, set `Email__Provider=Smtp` and the `Email__Smtp__*` variables below. Any SMTP relay works
(Brevo, Postmark, Amazon SES, Mailgun).

### Phase 2 decisions
- **Users are signed in right after registering.** An unverified email doesn't block the multi-step
  registration. The dashboard reminds them and offers "send again". Features that need a verified email use
  the `EmailVerified` authorization policy (cookie claim `email_verified`).
- **Duplicate emails get a clear "already registered" message** (409). Rate limiting makes probing for
  registered emails slow. Login and forgot-password don't reveal whether an account exists.
- **Phones** are normalized to E.164: `555 12 34 56` → `+995555123456`. Foreign numbers need `+`.
- **Terms acceptance** is stored with a timestamp and version (`AspNetUsers.TermsAcceptedAt/TermsVersion`).
  Bump `LegalDocuments.CurrentTermsVersion` when the terms change.
- **Passwords** need at least 10 characters, with no forced symbols. A password reset signs other sessions out
  within 10 minutes (security-stamp check).

## Phase 3: registration categories and profiles

### API (all require sign-in; data is always the signed-in user's own)

| Endpoint | Notes |
|---|---|
| `GET  /api/v1/profiles` | Dashboard summary: personal details (masked), each category's status, vehicles, orchards |
| `POST /api/v1/profiles/registration` | Wizard submit: `{ picker, driver, orchardOwner, personalDetails?, vehicles?, orchards? }`, saved atomically |
| `POST /api/v1/profiles/picker` \| `/driver` \| `/orchard` | Add one category (same rules) |
| `GET/PUT /api/v1/personal-details/me` | Shared Picker/Driver details. On PUT, an empty ID or IBAN keeps the stored value |
| `GET /api/v1/pickers/me`, `/drivers/me`, `/orchard-owners/me` | One category (404 if not registered). `PUT /pickers/me` and `/drivers/me` update personal details |
| `DELETE /api/v1/pickers/me`, `/drivers/me`, `/orchard-owners/me` | **Deactivates** the category. The account and its data are kept |
| `GET/POST /api/v1/vehicles`, `PUT/DELETE /api/v1/vehicles/{id}` | Driver's vehicles (max 10, plate unique per driver) |
| `GET/POST /api/v1/orchards`, `GET/PUT/DELETE /api/v1/orchards/{id}` | Owner's orchards (max 50) |

Another user's vehicle or orchard id answers **404**, never 403, so ids can't be probed.
Personal ID numbers and IBANs are only ever returned as `…Last4`.

### Validation (Georgia)
- **Personal ID:** 11 digits.
- **IBAN:** ISO mod-97 checksum. Georgian IBANs must be `GE` + 2 digits + 2 letters + 16 digits.
- **Orchard identification number:** LLC → 9-digit company code; individual entrepreneur / natural person →
  11-digit personal number; other → either.
- **Vehicles:** passenger seats 1–60; plate 4–10 letters/digits, stored normalized (`aa-123-bb` → `AA123BB`).
- **Orchards:** area > 0 and ≤ 100,000 ha, up to 4 decimals.
- **Names:** letters in any script. Addresses are required, max 300 characters.

### Google Maps setup
1. In Google Cloud, create a project with billing enabled, then enable **Maps JavaScript API**,
   **Places API (New)** and **Geocoding API**.
2. Create an API key restricted to **HTTP referrers** (`http://localhost:5173/*`, `https://your-domain/*`) and to
   those three APIs. Set a daily quota or budget alert.
3. Optionally create a Map ID (Map Management). `DEMO_MAP_ID` is used in development.
4. Put them in `frontend/.env.local` (not committed):
   ```
   VITE_GOOGLE_MAPS_API_KEY=...
   VITE_GOOGLE_MAPS_MAP_ID=...
   ```
Without a key, location fields fall back to "use my current location" plus manual coordinates, so the forms
still work.

### Bank account holder and alternative phone (Picker / Driver personal details)
- **"This bank account does not belong to me."** When ticked, the account holder's first name, last name and
  personal ID number are required. The holder's ID is encrypted like the user's own and only returned as
  `accountHolderPersonalIdLast4`. Unlike the user's own ID it is *not* unique, because one person (e.g. a parent)
  may hold the account for several workers. It can't be the user's own ID number (`validation.holder_is_self`).
  Unticking clears the stored holder details.
- **Alternative phone number:** optional, normalized like the main phone (`599 11 22 33` → `+995599112233`). The
  form rejects it if it's the same as the account's main number.
- **Editing:** the Profile page (`/profile`) shows these fields and lets users edit them. Leaving the ID number,
  IBAN or holder's ID empty keeps the stored value.
- **API:** `PersonalDetailsInput` gained `alternativePhoneNumber`, `bankAccountOwnedByOther`,
  `accountHolderFirstName`, `accountHolderLastName` and `accountHolderPersonalIdNumber`. The migration is
  `AddBankAccountHolderAndAlternativePhone`.

### Phase 3 decisions
- **The wizard keeps its data in memory only**, never in localStorage, because it contains the ID number and
  IBAN. Moving between steps keeps everything; reloading the page starts the wizard again, after a browser
  warning.
- **Personal details are entered once and reused.** When adding a category later, the steps whose data
  already exists are skipped.
- **Registration is all-or-nothing:** one request and one database transaction.
- **Integration tests run on real PostgreSQL.** Each test class gets a throwaway database created from the
  migrations and dropped afterwards. They use the docker compose database (`127.0.0.1:15432`); override with
  `FARMPLATFORM_TEST_DB`.

## Represented people (linked persons)

Some pickers do not use smartphones. Any signed-in user (a "representative") can add them at `/linked`,
answer for them about picking work, and optionally receive their wages, **but only after the person agrees
by SMS**. The person never needs an account.

How consent works:
1. The representative adds the person: name, personal ID number, **the person's own mobile number**,
   addresses, location, and payment (the person's own IBAN, or "to me", which uses the representative's IBAN).
   The representative must have saved their own personal details first, so the SMS can name them.
2. The person gets an SMS with a 6-digit code and a link (`/c/{token}`) that states the scope in plain Georgian.
3. Consent is given either by telling the representative the code (they type it in) or by tapping
   "I agree" on the link page. The person can decline, or withdraw later, from the same link.
4. Changing the phone number, the payment method or the person's IBAN puts consent back to "waiting" and sends
   a new SMS. Name and address edits keep the consent. Removing a person deletes their data and sends an SMS.

Rules: one person, one profile (a personal ID number can't be both a user and a linked person, or linked twice);
you can't link yourself or use your own phone numbers; Georgian numbers must be mobile; max 30 people per
representative; codes expire after 7 days or 5 wrong tries; SMS: 1 per minute (except right after a change)
and 5 per day per person. Only hashes of codes and link tokens are stored; each new SMS replaces the old link.

### API
| Method | Path | |
|---|---|---|
| GET / POST | `/api/v1/linked-persons` | list / add (sends the consent SMS) |
| GET / PUT / DELETE | `/api/v1/linked-persons/{id}` | own records only (others get 404) |
| POST | `/api/v1/linked-persons/{id}/resend-consent` | new code + link |
| POST | `/api/v1/linked-persons/{id}/confirm` | `{ "code": "123456" }`, rate limited |
| GET | `/api/v1/consent/{token}` | public, rate limited |
| POST | `/api/v1/consent/{token}/accept` · `/decline` · `/withdraw` | public, rate limited |

In development, SMS messages are printed to the API console (`Sms:Provider = Log`), like emails.

### Before launch
- **A real SMS provider is needed** (a Georgian SMS gateway, or an international one such as Twilio). Only
  `Log` is accepted today; the app refuses other values so it can't silently "send" nothing in production.
- **SMS consent proves access to the phone, not identity.** That's the usual standard, but for paying wages
  to someone else it's worth a legal review against Georgia's Law on Personal Data Protection (2023),
  including how long consent records are kept. The consent text version is stored with every consent.
- **Answering picking requests for a person** will be wired in with the picking events feature; the
  consent (`CanBeOfferedWork`) is already in place for it.

## Admin panel

Admins see every account at `/admin` (an "ადმინი" / "Admin" link appears in the header): counts per category,
lists filtered by **pickers, drivers, orchard owners, linked pickers, admins, or no category**, search by name,
email, phone or an exact 11-digit personal ID number, and a detail page per account (personal info, vehicles,
orchards, linked pickers). Personal ID numbers and IBANs are only ever shown as their last 4 digits. The panel is
read-only.

### Creating an admin
There is no sign-up or API for this on purpose; it's done on the server:
1. Register the account on the website as usual and **confirm its email** (open the verification link).
2. From the `backend` folder, with the database running:
   ```
   dotnet run --project src/FarmPlatform.API -- make-admin you@example.ge
   ```
   It prints `you@example.ge is now an admin.` Reload the website and the Admin link appears.
3. To remove: `dotnet run --project src/FarmPlatform.API -- remove-admin you@example.ge`

Granting and removing take effect immediately: every admin request checks the role in the database, not the
login cookie. On Render, run the same command from the service's Shell (`dotnet FarmPlatform.API.dll make-admin …`).

### API (admin only; others get 403)
| Method | Path | |
|---|---|---|
| GET | `/api/v1/admin/stats` | counts |
| GET | `/api/v1/admin/users?role=&search=&page=&pageSize=` | role: `all`, `picker`, `driver`, `orchard_owner`, `admin`, `none` |
| GET | `/api/v1/admin/users/{id}` | one account (each view is logged with the admin's id) |
| GET | `/api/v1/admin/linked-pickers?status=&search=&page=` | status: `all`, `pending`, `confirmed`, `declined`, `revoked` |

## Phase 4 (next)

Rest of profile management (add/edit/remove vehicles and orchards, deactivate a category with confirmation;
personal details editing already exists at `/profile`), profile photo upload to Cloudflare R2, and account
settings (change password, language, phone).
