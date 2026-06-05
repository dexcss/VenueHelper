# Publishing Venue Helper on GitHub

This guide takes you from the folder on your disk to a public repo that other
people can add to Dalamud and install in-game. There are two parts:

1. **Get the code on GitHub** (required).
2. **Make it installable in-game** via a custom Dalamud repo (the part that lets
   people "add the repo and use it").

Throughout, replace **YOURNAME** with your GitHub username everywhere it appears.

---

## Part 1 — Put the code on GitHub

This folder is already a git repository with commits, and `bin`/`obj` build
junk is excluded by `.gitignore`. You only need to create an empty GitHub repo
and push.

### 1.1 Create the repo
- Go to https://github.com/new
- **Repository name:** `VenueHelper`
- **Visibility:** Public
- **Do NOT** tick "Add a README", ".gitignore", or "license" — this project
  already has all three; adding them on GitHub causes a push conflict.
- Click **Create repository**.

### 1.2 Push
Open a terminal **in this folder** and run (replace YOURNAME):

```bash
git branch -M main
git remote add origin https://github.com/YOURNAME/VenueHelper.git
git push -u origin main
```

When prompted to log in, use a **Personal Access Token** as the password
(GitHub no longer accepts your account password here). Create one at
https://github.com/settings/tokens — a "classic" token with the `repo` scope is
enough. Or install GitHub Desktop / the `gh` CLI, which handle login for you.

### 1.3 Set your identity (optional)
The existing commits use a placeholder name. To use your own going forward:

```bash
git config user.name "Your Name"
git config user.email "your@email.com"
```

### 1.4 Future changes
After editing code:

```bash
git add -A
git commit -m "Describe what changed"
git push
```

---

## Part 2 — Make it installable in-game

Dalamud installs plugins from a **release zip** containing the built `.dll` and
the `VenueHelper.json` manifest. People then add a **custom repo URL** (your
`repo.json`) in Dalamud settings, and it appears in their plugin installer.

The repo already includes everything needed:
- `.github/workflows/release.yml` — builds the plugin and attaches a
  `VenueHelper.zip` to a GitHub Release automatically whenever you push a
  version tag.
- `repo.json` — the "plugin master" list that points Dalamud at that zip.
- `VenueHelper/VenueHelper.json` — the plugin manifest (already updated).

### 2.1 Fix the placeholder URLs
Before your first release, edit these files and replace **YOURNAME** with your
GitHub username:
- `repo.json` (the `RepoUrl`, `IconUrl`, and three `DownloadLink*` fields)
- `VenueHelper/VenueHelper.json` (`RepoUrl` and `IconUrl`)

Commit and push the change:

```bash
git add -A
git commit -m "Set GitHub URLs"
git push
```

### 2.2 Add an icon (optional but recommended)
Put a square PNG (512x512 works) at `images/icon.png`, then commit/push it.
Without it the IconUrl just 404s harmlessly.

### 2.3 Cut your first release
Tag a version and push the tag. The version **must** match the
`AssemblyVersion` in the manifests (currently `1.0.0.0`):

```bash
git tag v1.0.0.0
git push origin v1.0.0.0
```

Pushing the tag triggers the GitHub Action. Watch it under the **Actions** tab
of your repo. When it finishes (a few minutes), a **Release** appears with
`VenueHelper.zip` attached. The download links in `repo.json` use
`releases/latest/download/VenueHelper.zip`, so they always point at your newest
release automatically.

### 2.4 How other people install it
Tell users to:
1. In-game, open Dalamud settings: type `/xlsettings`.
2. Go to the **Experimental** tab.
3. Under **Custom Plugin Repositories**, paste:
   ```
   https://raw.githubusercontent.com/YOURNAME/VenueHelper/main/repo.json
   ```
4. Click the **+**, then **Save** (the floppy-disk icon).
5. Open the plugin installer (`/xlplugins`), search **Venue Helper**, install.

That's it — they can now use it and will get updates whenever you cut a new
release.

---

## Releasing updates later

1. Bump the version in **both** `VenueHelper/VenueHelper.json`
   (`AssemblyVersion`) and `VenueHelper/VenueHelper.csproj` (`<Version>`), and in
   `repo.json` (`AssemblyVersion` + `TestingAssemblyVersion`). Use a 4-part
   version like `1.1.0.0`.
2. Commit and push.
3. Tag and push the tag:
   ```bash
   git tag v1.1.0.0
   git push origin v1.1.0.0
   ```
4. The Action builds and publishes the new release; users get the update
   automatically.

---

## Notes & gotchas

- **Build/Dalamud API level:** the workflow downloads the latest Dalamud to
  build against. If a game patch bumps Dalamud's API level, you may need to
  update `DalamudApiLevel` in the manifests (currently `12`) to match, or
  Dalamud will mark the plugin outdated. Check the current level if installs
  start failing after a patch.
- **Licensing:** this project is AGPL-3.0 (it adapts code from Venue Manager,
  which is AGPL). Keep the `LICENSE` file in the repo. See the Credits section
  in `README.md`; reaching out to the other referenced plugin authors before
  wide distribution is the courteous and safest move.
- **First release is the slow one** — later ones reuse cached dependencies.
- If the Action fails on `dotnet build`, open the failed run under **Actions**,
  read the log, and check the NuGet package versions in the csproj resolve.
