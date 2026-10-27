<!-- Copy of CommunitiesForever-Web docs/UPLOADER-API.md at 0d1a8de8791cfadf98f8cb40ab6c36dfe5669ced. Refresh with scripts/refresh-api-contract.ps1; do not edit. -->
# Uploader API

The contract between this site and the Windows uploader ([CommunitiesForever-Upload](https://github.com/nicoputten/CommunitiesForever-Upload)). This repo owns it: WEB-33 built it, the uploader follows it.

Status: **v1, final** (WEB-33, 2026-10-27; connecting and disconnecting moved to the OAuth 2.0 standards in WEB-33b before any release, decided with Nico). From now on changes are **additive only** (new optional fields, new endpoints, new error or rejection codes the app treats as unknown); a breaking change gets `/v2/` and `v1` keeps working until every friend has updated (CLAUDE.md compatibility spirit). The examples below marked `<!-- example: … -->` are checked against the real routes by `tests/server/uploader.test.ts`: same keys, same value types.

## Standards
The app is a **public OAuth 2.0 client** without a secret, `client_id=communitiesforever-uploader`, and uses a standard OAuth client library (on .NET: `Duende.IdentityModel`, `RequestDeviceAuthorizationAsync`, `RequestDeviceTokenAsync`, `RevokeTokenAsync`) rather than its own HTTP code for connecting:
- [RFC 8628](https://www.rfc-editor.org/rfc/rfc8628) Device Authorization Grant: connecting through the browser.
- [RFC 6750](https://www.rfc-editor.org/rfc/rfc6750) Bearer tokens: `Authorization: Bearer <access_token>` on every call.
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) Token revocation: "Disconnect".

The site implements these endpoints itself (small and tested; no OAuth server package fits Next.js route handlers, CLAUDE.md rule 9). There is no discovery document: the app knows the endpoint paths below.

## Principles
- The uploader never parses the addon file. Everything it shows about the content comes from these responses.
- One access token per installed app ("device"), upload-only: it can upload, read its own identity and revoke itself (and, WEB-35, read the update feed). It cannot read the site's pages or data: the site accepts session cookies everywhere else and tokens only under `/api/uploader/v1/`.
- Uploads with a token go through exactly the same checks and storage as manual uploads (WEB-06: may upload, own file, same community). They show in the same history and get the same session report. The upload records which device sent it.
- **Several uploaders at once.** Friends' files arrive out of sync: each holds its owner's newest own data and older copies of everyone else's. The site merges per character GUID and the newest `updatedAt` wins (locations: newest `seenAt`), independent of upload order, so older data never overwrites newer. An upload's response describes only what *that* upload changed; one with nothing newer is `nothing_new`.
- Access tokens are long random secrets, stored only as their SHA-256 on the site, handed out once. They don't expire; they are revoked (decided with Nico: no refresh tokens).
- All endpoints are under `/api/uploader/v1/`, HTTPS, answers in JSON with `Cache-Control: no-store` on errors and tokens. Requests to `device_authorization`, `token` and `revoke` are forms (`application/x-www-form-urlencoded`), as OAuth prescribes. Times are Unix seconds.
- Errors are OAuth style (RFC 6749 §5.2): `{ "error": "<code>", "error_description": "<text for logs>" }`; the app acts on `error` only.

<!-- example: error -->
```json
{ "error": "invalid_token", "error_description": "Unknown or revoked token." }
```

## Connecting (RFC 8628)
1. **Device authorization request.** `POST /api/uploader/v1/device_authorization` (no auth), form `client_id=communitiesforever-uploader&device_name=DESKTOP-NICO&app_version=1.0.0` (`device_name` required, at most 64 characters; `app_version` optional; both are our extensions) → `200`:

   <!-- example: device-authorization -->
   ```json
   {
     "device_code": "kq3V0dXyPZ8m1nR2c9WbTg",
     "user_code": "KXRT-7QMN",
     "verification_uri": "https://wow.vestras.nl/uploader",
     "verification_uri_complete": "https://wow.vestras.nl/uploader?code=KXRT-7QMN",
     "expires_in": 600,
     "interval": 5
   }
   ```
   - `device_code` is random and unguessable: whoever holds it collects the token. Keep it in memory only.
   - `user_code` has 8 characters from `ACDEFGHJKMNPQRTWXY34679` (no look-alikes), shown as `XXXX-XXXX`; the site also accepts it in lower case or without the dash.
   - Errors: `400 invalid_request` (not a form, no device name), `401 invalid_client` (wrong `client_id`), `429 rate_limited` with `Retry-After` (more than 10 per 10 minutes per IP, or 50 connections waiting).
2. **User approval.** The app opens `verification_uri_complete`. The signed-in user sees "Connect an uploader?" with the device name and the code, and Allow / Deny (WEB-34). Only someone who may upload can allow; at most 5 active uploaders per person.
3. **Device access token request.** `POST /api/uploader/v1/token` (no auth), form `grant_type=urn:ietf:params:oauth:grant-type:device_code&device_code=…&client_id=communitiesforever-uploader`, every `interval` seconds:
   - `200`, once: the token is made at this moment and never shown again.

     <!-- example: token -->
     ```json
     { "access_token": "cfu_3q2-7wEXAMPLEexampleEXAMPLEexampleEXAMPLExy", "token_type": "Bearer" }
     ```
   - `400` with `error`:
     - `authorization_pending`: not allowed yet, keep polling;
     - `slow_down`: polled faster than `interval`, add 5 seconds to it (RFC 8628 §3.5);
     - `access_denied`: denied, or the user lost upload rights before the app collected the token;
     - `expired_token`: expired, already collected, or an unknown `device_code`;
     - `unsupported_grant_type`, `invalid_request`.
   - `401 invalid_client`: wrong `client_id`.

   After `200` the app calls `GET /me` for the BattleTag and site name it shows.

A token can also be created by hand on the uploader page ("Create a token by hand", WEB-34) and pasted into the app.

### Access tokens
- Format: `cfu_` and 43 base64url characters (32 random bytes). Send as `Authorization: Bearer <access_token>` (RFC 6750 §2.1).
- No expiry, no refresh token. One per device: connecting the same device name again replaces the user's older token for it.
- Revoked by the user (uploader page), by the app (`/revoke`), or when the admin blocks the user (unblocking needs connecting again). A user who loses "may upload" keeps the token but gets `403 may_not_upload` until the right comes back.
- Bearer errors (RFC 6750 §3), on every Bearer endpoint:
  - no `Authorization: Bearer` header: `401 invalid_request` with `WWW-Authenticate: Bearer realm="uploader"`;
  - an unknown or revoked token: `401 invalid_token` with `WWW-Authenticate: Bearer realm="uploader", error="invalid_token"`. The app shows "Reconnect";
  - the user may not upload: `403 may_not_upload`.

## Uploading
`POST /api/uploader/v1/uploads` with `Authorization: Bearer <access_token>`, `Content-Type: application/octet-stream`, body = the raw `CommunitiesForever.lua` bytes (max 25 MiB). Optional headers: `X-Uploader-Version` (stored as the device's app version), `X-File-Saved-At` (Unix seconds of the file's write time; accepted and ignored in v1).

`200` for every stored upload, accepted or not:

<!-- example: upload-ok -->
```json
{
  "uploadId": 42,
  "uploadedAt": 1793000000,
  "status": "ok",
  "rejection": null,
  "schemaVersion": null,
  "summaryLine": "5 levels gained · 4 skill gains · 1 new character",
  "highlights": ["Ves Doombringer 37 → 39", "Ves Doombringer: Tailoring 150 → 187", "Zul Kazrek is new"],
  "counts": { "characters": 10, "friends": 5 },
  "reportUrl": "https://wow.vestras.nl/upload/42"
}
```

<!-- example: upload-rejected -->
```json
{
  "uploadId": 43,
  "uploadedAt": 1793000100,
  "status": "rejected",
  "rejection": "newer_format",
  "schemaVersion": 3,
  "summaryLine": "Newer addon: format 3",
  "highlights": [],
  "counts": null,
  "reportUrl": "https://wow.vestras.nl/upload/43"
}
```

- `status`: `ok` (something in it was newer), `nothing_new` (the same file again, or nothing in it newer than what the site has), `rejected`.
- `rejection` (only with `rejected`): `wrong_file` (another addon's file), `damaged`, `newer_format` (the site doesn't read this addon version yet; `schemaVersion` names it; retry after a site update), `not_your_file`, `other_community`, `no_community_yet` (before the admin's first upload). Treat an unknown code as "not uploaded".
- `summaryLine`: the line the upload history shows: up to three counts, "First upload · N characters", "Nothing new", or the short reason of a rejection.
- `highlights`: at most 3 lines about what changed (level ups, then skill gains, then new characters), full character names; `[]` for the first upload, `nothing_new` and rejections.
- `counts`: characters with a record in the uploaded file, and the friends who own them; `null` when the file can't be read.
- `reportUrl`: the session report (it also explains a rejection); opening it needs a sign-in.

Errors: the Bearer errors above, `400 empty_file`, `413 too_large`, `429 rate_limited` with `Retry-After` (more than 60 uploads per hour per token).

## Identity
`GET /api/uploader/v1/me` (Bearer): "Test connection" in the app's settings, and the name and site it shows after connecting. Bearer errors as above.

<!-- example: me -->
```json
{ "battleTag": "Nico#2201", "siteName": "Doombringers", "deviceName": "DESKTOP-NICO", "appVersion": "1.0.0", "createdAt": 1793000000, "lastUploadAt": null }
```
`appVersion` and `lastUploadAt` may be `null`.

## Disconnecting (RFC 7009)
`POST /api/uploader/v1/revoke` (no auth: holding the token is the proof), form `token=<access_token>&token_type_hint=access_token&client_id=communitiesforever-uploader` → `200` with an empty body, also for an unknown or already revoked token. Errors: `400 invalid_request` (no token), `401 invalid_client`.

## Installer and updates (WEB-35)
- `GET /uploader/download`: the current installer, for signed-in users (the uploader page's "Download for Windows").
- `GET /api/uploader/v1/updates/<velopack feed files>` (Bearer): the Velopack update feed for the app.
- `POST /api/uploader/v1/releases` (deploy secret, used only by the uploader repo's release workflow): publishes a new installer and its update packages.

## On the site (WEB-34)
- Page `/uploader` (board `site-uploader-page`): what the app does, download, the three steps, the approval when `?code=` is present, "Your uploaders" (device, connected date, last upload, active, Revoke), "Create a token by hand". Built on `src/server/uploader.ts` (`findPairing`, `decidePairing`, `createToken`, `activeTokens`, `revokeToken`); uploads store the device in `uploads.device_id` for the history's "via DESKTOP-NICO".
- The top bar's "Upload" becomes "Uploader"; the manual upload page stays and is reached by a small "Upload manually" link on the uploader page (decided with Nico).
