# GitHub → MonsterASP auto-deploy

Push to `master` / `main` / `minor-fixes*` runs **Deploy API → MonsterASP**.

## One-time: GitHub Secrets

https://github.com/shahid08569/EsellerBackend/settings/secrets/actions

| Secret | Value |
|--------|--------|
| `FTP_SERVER` | `site96408.siteasp.net` |
| `FTP_USERNAME` | `site96408` |
| `FTP_PASSWORD` | MonsterASP → Deploy → FTP Show |
| `FTP_SERVER_DIR` | `/wwwroot/` |
| `FTP_PROTOCOL` | `ftps` (or `ftp` if FTPS fails) |

Server `appsettings.Production.json` is never overwritten by the workflow.
