# Security

Only the latest release is supported.

Please report vulnerabilities privately through **Security → Report a vulnerability** on GitHub, not in a public issue. This is a one-person project, so replies can take a few days.

## What the app connects to

- IP geolocation services, only in auto-detect mode: `ipwho.is`, then `ipapi.co`, then `ip-api.com` (unencrypted HTTP).
- `api.github.com`, only when you click **Check for updates**. The app never downloads or installs anything itself.

The exe isn't code-signed. Each release lists its SHA-256, which you can compare with `Get-FileHash PrayerTimes.exe`.
