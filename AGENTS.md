# WT Translator Local: project instructions

This is the standalone, offline, open-source edition. Keep S3 and cloud synchronization out of this project.

## Required update and publication workflow

The user explicitly requires downloadable ZIP applications **inside the Git repository**, not only in GitHub Releases or CI artifacts.

- Keep `binaries/WtTranslator-win-x64.zip`, `binaries/WtTranslator-osx-arm64.zip`, and `binaries/WtTranslator-osx-x64.zip` tracked in ordinary Git. Do not use Git LFS pointers in place of these downloadable archives.
- After application changes, run the relevant tests and rebuild all three applications with `./scripts/update-binaries.sh` on macOS. Update the application version and documentation when appropriate.
- Commit source changes, rebuilt ZIPs, `binaries/SHA256SUMS.txt`, and the binaries README together. Documentation-only changes do not require a binary rebuild.
- Push each completed update to **both** remotes: `origin` (GitHub) and `gitlab` (GitLab). Use `./scripts/push-all.sh`. Push release tags to both as well.
- Verify both remotes point to the published commit. If either push fails, resolve it or report that destination as unfinished. Never claim both are updated when only one is.
- GitHub Releases are optional additional distribution; they do not replace `binaries/`.

The original sibling `WT_translator` is a separate legacy project. Do not overwrite its history or its GitLab repository when updating this edition.
