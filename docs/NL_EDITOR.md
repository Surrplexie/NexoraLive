# Web Rule Authoring (Phase I)

Browser-based `.nle` editor for the public demo stack — no Windows WinForms install required.

## Pages & APIs

| URL | Purpose |
|-----|---------|
| `/editor.html` | Visual rule editor + live evaluate panel |
| `GET /api/v1/editor/config` | Load editor model + generated NLE text |
| `PUT /api/v1/editor/config` | Save to sandbox (operator auth) |
| `POST /api/v1/editor/evaluate` | Dry-run mock event → Allow/Block/Warn |
| `POST /api/v1/editor/apply` | Point live session at sandbox + restart (operator) |
| `POST /api/v1/editor/reset` | Restore demo template into sandbox (operator) |
| `GET /api/v1/editor/vocabulary` | Known events, properties, comparators |

## Sandbox file

Edits are stored at:

```
%NL_DATA_ROOT%/web-editor-sandbox.nle
```

(local default: `%LOCALAPPDATA%\NL\web-editor-sandbox.nle`)

- **Visitors** can load rules and use **Evaluate** without authentication.
- **Operators** save/apply/reset with `X-NL-Operator-Key` (same as `/operator.html`).
- **Apply to session** updates the session profile `configPath` to the sandbox and restarts the session so the bridge picks up new rules.
- **Demo loop reset** (Phase G) also resets the sandbox from `NL_DEMO_CONFIG` (default `demo.nle`).

## Supported editor subset

Matches v0.1 NLE grammar (same as `NL.ConfigEditor`):

- Event blocks with `allow`, `block`, `deny`, `warn`
- `if` / `else` with simple comparisons joined by `and` / `or`
- Optional hotkey declarations (model supports them; UI focuses on events)

Shared library: `src/NL.NleEditor.Core` (`NleLoader`, `NleWriter`, `NleEditorEvaluate`).

## Local try

```bash
dotnet run --project src/NL.SessionHost.Web
# open http://127.0.0.1:27020/editor.html
```

In public/demo Docker, the editor is linked from the spectator nav bar.

## Rate limits

When Phase K hardening is on:

- `GET /api/v1/editor/*` counts toward public read limits
- `POST /api/v1/editor/evaluate` has its own bucket (`NL_EDITOR_EVALUATE_RATE_PER_MIN`, default 120/min)
