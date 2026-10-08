# Security Policy

## Table of Contents
- [Supported Versions](#supported-versions)
- [Security Principles](#security-principles)
- [Permissions Policy](#permissions-policy)
- [Reporting a Vulnerability](#reporting-a-vulnerability)

## Supported Versions

Security updates and patches are only provided for the latest version and the `main` branch.

| Version | Supported          |
| ------- | ------------------ |
| 0.8.x   | :white_check_mark: |
| < 0.8.0 | :x:                |

---

## Security Principles

Any change, contribution, or patch to this project **must not introduce a greater security risk to the user than the Unity engine itself already carries**. Contributions that add new attack surface, unnecessary network activity, unnecessary data collection, or unnecessary system access will be rejected.

---

## Permissions Policy

This game is required to work **without requesting any permissions or system rights from the user**, with a single exception: on Linux, the OS-level permission to access the audio device (needed for sound/music playback) may be requested, as this is a standard requirement of the audio stack rather than a project-specific request.

The application does **not** need, and must never request, access to:
- Camera
- Gyroscope / motion sensors
- Microphone
- Bluetooth
- Wi-Fi (or any other network/radio access)
- Any other system permission not explicitly listed above

### Google Play build
The Google Play build uses **Google Play Games Services** solely to handle achievements. This build is uploaded to Google Play exclusively by the project maintainer (`GGalya1`). All Google Play–related code is inactive and does not execute on platforms where it is not needed (i.e. outside of the Google Play build).

---

## Reporting a Vulnerability

If you discover a potential security vulnerability in this project, please report it responsibly:

1. **Do not** open a public issue.
2. Submit a report via [GitHub Private Vulnerability Reporting](https://github.com/GGalya1/riscv-architecture-educational-game/security/advisories/new) or send an email to `garturmail@gmail.com`.
3. Please include a brief description of the issue and steps to reproduce it.

You can expect an initial response within 48-72 hours. Thank you for helping keep this project safe!
