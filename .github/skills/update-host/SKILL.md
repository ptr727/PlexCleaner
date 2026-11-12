---
name: update-host
description: >-
  Brings this machine's coding agents fully current with the ptr727/ProjectTemplate hub in one
  step, from any repo or session, and reports whether they are. It upgrades the installed agent
  tools, installs the fleet skills and the Claude Code safety kit, and writes the host-wide rules
  for Codex and opencode. Use this whenever asked to update, refresh, or set up this machine, host,
  or agents with the fleet skills, rules, protections, safety kit, or hooks, to check whether this
  machine is current, or after any promotion that changed them. Also use it on a fresh host.
  Triggers even when only the skills seem stale. check-this-repo reads one repo and one report,
  resync-a-repo converges a repo, and this skill changes the machine.
---

# Update Host

## Why This Exists

A host holds four things the fleet installs for coding agents. They are the agent tools themselves, the fleet skills, the Claude Code safety kit, and the host-wide rules. Each used to be installed and checked by its own command, so a host was routinely current on some and stale on the rest. One action now brings all four current, and one reports each. The bootstrap fetches the hub's `main` itself, so the session needs no checkout.

## Run It

Fetch the loader fresh and run it as the user whose agents are being updated, never under sudo. Download it rather than piping it, as the hub's `host-setup/README.md` states. Each command below is one self-contained line that fetches and runs, and carries no shell variable from one command to the next.

On Linux or WSL, the setup and then the status:

```sh
d=$(mktemp -d) && curl -fsSLo "$d/bootstrap.sh" https://raw.githubusercontent.com/ptr727/ProjectTemplate/main/host-setup/bootstrap.sh && bash "$d/bootstrap.sh" --agent-setup --yes
d=$(mktemp -d) && curl -fsSLo "$d/bootstrap.sh" https://raw.githubusercontent.com/ptr727/ProjectTemplate/main/host-setup/bootstrap.sh && bash "$d/bootstrap.sh" --agent-status
```

On native Windows, in a PowerShell console:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command '[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; $f = Join-Path $env:TEMP bootstrap.ps1; try { Invoke-WebRequest -UseBasicParsing -Uri https://raw.githubusercontent.com/ptr727/ProjectTemplate/main/host-setup/bootstrap.ps1 -OutFile $f -ErrorAction Stop } catch { Write-Host $_; exit 1 }; powershell -ExecutionPolicy Bypass -File $f -AgentSetup -Yes; exit $LASTEXITCODE'
powershell -NoProfile -ExecutionPolicy Bypass -Command '[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; $f = Join-Path $env:TEMP bootstrap.ps1; try { Invoke-WebRequest -UseBasicParsing -Uri https://raw.githubusercontent.com/ptr727/ProjectTemplate/main/host-setup/bootstrap.ps1 -OutFile $f -ErrorAction Stop } catch { Write-Host $_; exit 1 }; powershell -ExecutionPolicy Bypass -File $f -AgentStatus; exit $LASTEXITCODE'
```

Each Windows command stops on a failed download, so a stale earlier copy of the loader never runs. Run the status after the setup, and run it alone to answer "is this machine current". The status changes nothing in the agent setup. On Windows, any bootstrap action, the status included, first installs PowerShell 7 where it is missing. Add `--dry-run` or `-DryRun` to the setup to see what it would run. A dry run never touches the status, which changes nothing in the agent setup and always reads the real host.

## What the Setup Does

1. Upgrades the coding-agent tools already installed, and adds none.
2. Installs the fleet skills for Codex, opencode, and Claude Code, from a tree the bootstrap keeps.
3. Installs the Claude Code safety kit where Claude Code is present, and skips it with a line saying so where it is not.
4. Writes the host-wide rules to the global instruction file of each of Codex and opencode that is installed. A Codex `AGENTS.override.md` that is not blank takes precedence over that file, so the status reports it as SHADOWED and the setup warns. Neither touches the override.

## Report It

Report each step's status as the output states it, in the order above, and name every step the status run lists as not current. Then say plainly:

- Restart agent sessions to load the new hooks and rules. A running session keeps what it started with.
- Codex and opencode get the rules but no hooks yet. No hook guards their sessions yet, so those sessions are not mechanically guarded.

## When the Run Is Refused

An executor may refuse the run as a self-modification of the agent's own configuration, because the safety kit rewrites files under the user's agent directories. Do not retry it, and do not edit those directories by hand. Hand the maintainer the setup line above for their platform, unchanged, with the `!` prefix. It fetches and runs in one line, so it needs nothing from an earlier command. Then run the status once they confirm it ran.

## What It Does Not Do

- It does not touch the current repo. Drift in a repo's carried files is `check-this-repo` for a report and `resync-a-repo` for a converge.
- It does not add an agent tool that is not installed. Installing a tool is `bootstrap.sh --agents NAME`, chosen by the maintainer.
- It does not merge or release anything. Refreshing a hub checkout's own skills after a promotion stays `merge-and-release`'s step.
