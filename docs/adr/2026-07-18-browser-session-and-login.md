# ADR: Browser session, login and account access

## Status

Active decision, extracted from `ENGINEERING_NOTES.md` on 2026-07-18.

## Browser lifecycle

- Treat `DOMContentLoaded` as sufficient only when the required page marker is checked afterward.
- Session pacing sleep saves StorageState and closes the browser without Travian logout. Manual logout and
  account switching remain explicit logout flows.
- Sleep, proxy handover, reset, and account/browser changes may retain filtered authentication state.
  Real desktop-process startup and user-triggered application exit delete every account's saved Playwright
  authentication state. Startup cleanup covers crashes where exit cleanup could not run.
- Browser shutdown, popup handling, and session replacement must account for isolated browser contexts.
- Detect browser crash/closed-page errors and surface a specific diagnostic message.
- Portable builds resolve Playwright from the bundled `.playwright` directory.
- The anti-detection setup is intentional: keep `--disable-blink-features=AutomationControlled`, clear
  `navigator.webdriver` through an init script, launch headed Chrome maximized, and use
  `ViewportSize.NoViewport`. Do not restore hard-coded viewport dimensions.

## Lobby login and SSO

- Full login always starts at `lobby.legends.travian.com/account`; never probe or submit credentials on the
  configured game server first, and do not use direct game-server login as fallback.
- `DOMContentLoaded` does not prove that the lobby React application has rendered. Wait for either the owned-world
  cards or both credential fields, and retry a missing/transient lobby state at most three times. A repeated submit
  is allowed only after the freshly loaded lobby explicitly shows the complete login form again.
- Select the owned world by cached lobby wuid when available. Accept any authenticated path on the configured
  game origin and store a newly learned wuid in the per-account analysis snapshot.
- Automatic matching tolerates a speed label omitted from the lobby world name, while an explicit conflicting
  speed is never accepted. The interactive chooser is available only while the account is still configured as
  `Choose in lobby`. After its first verified selection saves the concrete server, missing/stale cached data or a
  failed landing must keep that server unchanged and fail into normal login retry instead of reopening the chooser.
  The chosen wuid is saved only after `Play now` reaches the configured origin, so a wrong selection is not remembered.
- After credentials are submitted, execution-context destruction is an expected navigation transition. Wait
  for the rendered owned-world card before continuing. Both login submit and `Play now` use normal click pacing.
- After lobby SSO commits navigation to the game origin, do not wait for the old context to prove the game
  shell. Suppress the known CMP overlay, save filtered auth state without slow live-origin cleanup, create a
  clean game context in the same Chromium process, close the lobby context, and verify login in the replacement.
- Saved state may retain lobby/auth hosts required for SSO, but must remove sibling game-server state and
  consent storage.
- Read account language only on the configured game origin; lobby and browser-error documents are not account-language
  evidence. Accept Travian's equivalent `en` and `en-US` English codes. A verified language-gate action resumes the
  automation mode that was running before the pause; an idle account remains idle.

## Account access and holds

- Classify access as `LoggedIn`, `LoggedOut`, `Unavailable`, `Restricted`, `Challenge`, or `Unknown`.
- Verify `Unknown` once on canonical `/dorf1.php`; network failures are `Unavailable` and do not count toward
  restriction.
- `Restricted`, `Challenge`, or three consecutive verified `Unknown` results create a persistent,
  account-specific automation hold. Stop only that account, preserve queue/settings, and require manual
  re-enable after review.

## Consequences

Login and browser lifecycle are shared operational behavior. Changes require focused verification of SSO,
state filtering, cancellation, browser replacement, and process startup/exit cleanup.
