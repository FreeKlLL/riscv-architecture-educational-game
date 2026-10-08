## Table of Contents
- [Branching Naming Conventions](#branching-naming-conventions)
- [Continuous Integration & Tests](#continuous-integration--tests)
- [Use of AI Tools](#use-of-ai-tools)
- [Tags & Releases](#tags--releases)


---

## Branching Naming Conventions
We follow the **Conventional Commits** naming pattern. All branches should be named in `kebab-case` using the following format:

`type/short-description` or `type/#issue-number-description`

**Allowed prefixes:**
* `feat/` - New features or gameplay mechanics
* `fix/` - Bug fixes
* `docs/` - Documentation changes
* `refactor/` - Code refactoring without logic changes
* `perf/` - Performance optimizations
* `test/` - Adding or updating tests

Branch names are validated _automatically_ against this pattern:
 
```
^(feat|fix|docs|refactor|perf|test)\/[a-z0-9-]+$
```

### General Rules for Branch Names
* Use **lowercase** letters only.
* Use **(`-`)** to separate words (kebab-case). Do not use spaces or underscores.
* Keep names concise but descriptive (2–4 words max).
* Always branch off the `main` (or `develop`) branch.

---

## Continuous Integration & Tests
We use **GitHub Actions** to automatically run our Unity Test Runner suite on every pull request.
 
* All required tests must pass before a PR can be merged into `main`. **If the tests fail, the merge will be blocked**.
* Please run the tests locally in Unity (`Window > General > Test Runner`) before pushing, to catch issues early and save CI time.

---

## Use of AI Tools
AI-assisted development (code assistants, LLMs, etc.) is allowed on this project, but under the following rules:
 
**Allowed:**
* Using AI to help write or refactor code, scripts, tests, or documentation.
* This is only permitted **before the `1.0` release**. The project is currently on `v0.8`, so AI-assisted contributions are welcome under the conditions below.

**Not allowed / requires human review:**
* **Graphics and visual assets** (sprites, textures, UI art, icons, animations, 3D models, etc.) must always be created by a human. AI-generated graphics are **never** accepted, at any version.
* Any code, text, or other content created or modified with AI assistance **must be reviewed and reworked by a human** before it is submitted. Please mention in your PR description if AI was used.
**After `1.0`:**
* Once the project reaches version `1.0`, AI tools may only be used **locally**, for personal drafting or exploration.
* AI-assisted changes may no longer be merged into the public repository from that point on - all contributions merged after `1.0` must be fully human-made.

---

## Tags & Releases
Pushing a new tag automatically triggers a deployment to itch.io and creates a GitHub Release:
 
```yaml
name: Deploy to itch.io and GitHub Releases
on:
  push:
    tags: [ "v*" ]
```
 
Because of this, **please do not create tags** casually or for testing. Tags should only be created as part of an intentional release!
