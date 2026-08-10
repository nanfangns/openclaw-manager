# Windows clean-machine smoke test

Target: Windows 10/11 x64, a standard user account with network access, and no preinstalled OpenClaw.

## Install

1. Run `packaging/output/OpenClawManagerSetup.exe`.
2. Confirm the Start Menu and optional desktop shortcut target `OpenClawManager.exe`, not a temporary BAT file.
3. Open the manager and verify the Overview page handles missing Node.js/OpenClaw without crashing.
4. Run the online install with and without model configuration.
5. Confirm UAC is shown only for the Node.js MSI operation and canceling UAC reports failure.
6. Confirm a compatible existing Node.js is reused and is not recorded as manager-owned.
7. Confirm a failed download or failed npm command leaves the install incomplete and visible in Logs.
8. After Node.js or OpenClaw installation, confirm the current manager process detects the newly installed commands without restarting Windows or manually editing PATH.
9. After an installation with model configuration, confirm the final verification includes a model probe and clearly reports provider/model failures.

## Gateway and repair

1. Confirm Gateway is installed and started only when selected.
2. Confirm the status page reports running, stopped, and non-zero command results accurately.
3. Stop and restart Gateway, then refresh status.
4. Occupy port 18789 before installation and verify the workflow does not report a healthy Gateway.
5. Use Repair after stopping Gateway and verify it starts and health-checks the service.
6. Feed the manager a Gateway status response with a running runtime and failed connectivity probe, and verify it reports the Gateway as unhealthy.

## Diagnostics

1. Open **诊断中心** without triggering a probe automatically.
2. Run the complete diagnosis and verify the list contains environment, Node.js, npm, OpenClaw, config, Gateway, and model checks.
3. Export the diagnostic ZIP and verify it contains `diagnostics.json` and `recent-logs.txt`, but no `.openclaw` configuration file or API key.
4. Confirm exported paths replace the current user profile with `%USERPROFILE%` and logs remain redacted.

## Configuration and uninstall

1. Create a model configuration and verify API keys are never displayed in the UI or written to logs.
2. Create a backup, modify the config, and restore the selected backup.
3. Restart the manager and verify state, backups, and logs are retained below `%LOCALAPPDATA%\\OpenClawManager`.
4. In Safe Uninstall, preview ownership before selecting cleanup.
5. Uninstall with configuration unchecked and verify `%USERPROFILE%\\.openclaw` remains.
6. Uninstall with configuration checked and verify a backup is created before removal.
7. Confirm arbitrary user environment variables are unchanged.
