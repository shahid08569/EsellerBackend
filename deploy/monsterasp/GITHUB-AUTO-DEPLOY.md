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

## If site shows “Updating Eseller API…” / CORS errors

IIS still has `app_offline.htm` (deploy mid-flight or cancelled).

1. Open https://github.com/shahid08569/EsellerBackend/actions  
2. Left: **Clear API Offline Banner** → **Run workflow** → `master`  
3. Wait ~30s → open https://api.esellerglobal.com/health (must NOT say Updating…)

Or MonsterASP File Manager → delete `/wwwroot/app_offline.htm`.

## If Deploy shows red X (FTP 550 GetHandle)

DLL locked by IIS. Workflow now retries 3×. If still red:

1. MonsterASP panel → **Recycle App Pool** (or Stop/Start site)  
2. Actions → **Deploy API → MonsterASP** → **Run workflow**  
3. Wait for green check + health OK  

White/grey circles = cancelled/skipped (not real failures). Yellow = queued behind another deploy.
