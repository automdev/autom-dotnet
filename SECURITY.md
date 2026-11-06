# Security Policy

## Supported versions

Security fixes are released for the latest published version of the Autom .NET SDK.

## Reporting a vulnerability

Please do not report security vulnerabilities through public GitHub issues.

Email **contact@autom.dev** with a description of the issue, the affected version, and steps to reproduce. We acknowledge reports within three business days and keep you informed until the issue is resolved.

## Handling API keys

- Keep your Autom API key out of source control. Load it from the `AUTOM_API_KEY` environment variable or a secret manager.
- Rotate the key from the [Autom dashboard](https://app.autom.dev) if you believe it was exposed.
