# Putting Venue Helper on GitHub

This folder is already a git repository with one commit. You just need to create
an empty repo on GitHub and push to it.

## 1. Create the repo on GitHub
- Go to https://github.com/new
- Repository name: `VenueHelper` (or whatever you like)
- Description: "All-in-one FFXIV Dalamud plugin for venue hosts: counter, raffle, auction, giveaway."
- Public
- **Do NOT** check "Add a README", "Add .gitignore", or "Add a license" —
  this project already has all three, and adding them on GitHub will cause a
  conflict on your first push.
- Click "Create repository".

## 2. Push from your machine
GitHub will show you a URL like `https://github.com/YOURNAME/VenueHelper.git`.
Open a terminal in this folder and run:

```bash
git branch -M main
git remote add origin https://github.com/YOURNAME/VenueHelper.git
git push -u origin main
```

(Replace YOURNAME with your GitHub username.) You'll be asked to authenticate —
use a Personal Access Token as the password, or the GitHub CLI / Git Credential
Manager if you have it set up.

## 3. Future changes
After editing code:

```bash
git add -A
git commit -m "Describe what you changed"
git push
```

## Optional: automated builds + release zips
If you want GitHub to build the plugin and attach a zip to each release
automatically, you can add a GitHub Actions workflow later. Ask and it can be
set up — it builds on every tag and publishes `VenueHelper/bin/Release` as a
release artifact, which is also the first step toward listing on a custom
Dalamud plugin repository.

## A note on the identity in the first commit
The initial commit was made with a placeholder name/email. To set your own:

```bash
git config user.name "Your Name"
git config user.email "your@email.com"
```

If you want the first commit to carry your identity too, you can redo it before
pushing:

```bash
git -c user.name="Your Name" -c user.email="your@email.com" commit --amend --reset-author -m "Initial commit: Venue Helper Dalamud plugin"
```
