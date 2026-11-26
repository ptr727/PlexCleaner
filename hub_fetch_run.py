# pyright: strict
"""Fetch a ptr727/ProjectTemplate script fresh from `main` and run it through uv.

Never pinned or vendored: a pin nothing keeps current goes stale by construction.
CI (this repo's own, and the hub's) is the backstop for a change that lands broken on `main`.
A fetch failure fails the caller loudly rather than silently skipping the gate it guards.
The script runs as `uv run --no-project --script`.
The packages its PEP 723 block declares therefore resolve, beside the data files it reads, which are fetched from the same ref.
Usage: hub_fetch_run.py <hub-relative-path> [script-args...]
Example: hub_fetch_run.py .github/actions/prose-gate/prose_lint.py --diff-commit -- <paths>
On SIGINT or SIGTERM it stops the script, removes the fetched tree, and re-raises the signal.
"""

import contextlib
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path

HUB_REPO = "ptr727/ProjectTemplate"
HUB_RAW_BASE = f"https://raw.githubusercontent.com/{HUB_REPO}"
HUB_MAIN_SHA_URL = f"https://api.github.com/repos/{HUB_REPO}/commits/main"
HUB_GIT_URL = f"https://github.com/{HUB_REPO}"
PROVENANCE_VAR = "PROSE_GATE_PROVENANCE"
PROSE_GATE_PATH = ".github/actions/prose-gate/prose_lint.py"
# The files a script reads or imports from the hub tree, fetched before it runs.
COMPANIONS = {
    PROSE_GATE_PATH: (".github/actions/prose-gate/prose-gate.json",),
    "scripts/docker_lint.py": (),
}
LOOKUP_TIMEOUT = 5


def lookup_via_git() -> tuple[str | None, str]:
    """`main`'s commit from `git ls-remote`, which spends no API quota, else None and the cause.

    Stderr is discarded rather than captured, so a remote helper git leaves behind on a timeout
    holds no pipe this process waits on.
    """
    try:
        probe = subprocess.run(
            ["git", "ls-remote", HUB_GIT_URL, "refs/heads/main"],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            check=False,
            encoding="utf-8",
            errors="replace",
            env={**os.environ, "GIT_TERMINAL_PROMPT": "0"},
            timeout=LOOKUP_TIMEOUT,
        )
    except subprocess.TimeoutExpired:
        return None, "timed out"
    except OSError as exc:
        return None, type(exc).__name__
    if probe.returncode != 0:
        return None, f"exit {probe.returncode}"
    if m := re.fullmatch(r"([0-9a-f]{40})\trefs/heads/main\n?", probe.stdout):
        return m.group(1), ""
    return None, "unexpected output"


def resolve_main_commit() -> str | None:
    """Return the full commit hash `main` points at now, from the API or else `git ls-remote`.

    Every file a run fetches comes from that one commit, so a script and its data always pair.
    Where neither resolves, one line names both causes and the gate runs against `main` instead.
    That run fetches twice and refuses to run, if the two reads differ, a push having landed mid-run.
    A hook with no network cannot fetch the gate at all, and the CI gate is the enforcement.
    """
    token = (os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN") or "").strip()
    request = urllib.request.Request(
        HUB_MAIN_SHA_URL, headers={"Accept": "application/vnd.github.sha"}
    )
    if token:
        request.add_unredirected_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(request, timeout=LOOKUP_TIMEOUT) as response:
            body: bytes = response.read()
        sha = body.decode(errors="replace").strip()
    except urllib.error.HTTPError as exc:
        reason = f"HTTP {exc.code}"
    except Exception as exc:  # noqa: BLE001  # any lookup failure falls back to git ls-remote
        reason = type(exc).__name__
    else:
        if re.fullmatch(r"[0-9a-f]{40}", sha):
            return sha
        reason = "unexpected response body"
    git_sha, git_reason = lookup_via_git()
    if git_sha:
        return git_sha
    print(
        f"hub_fetch_run: could not resolve the main commit (API {reason}, git ls-remote "
        f"{git_reason}), using main.",
        file=sys.stderr,
    )
    return None


class FetchError(Exception):
    def __init__(self, url: str, cause: Exception) -> None:
        super().__init__(url)
        self.url = url
        self.cause = cause


def fetch_all(ref: str, paths: tuple[str, ...]) -> dict[str, bytes]:
    """Fetch each hub path at `ref` in order, raising FetchError naming the first that fails."""
    fetched: dict[str, bytes] = {}
    for path in paths:
        url = f"{HUB_RAW_BASE}/{ref}/{path}"
        try:
            with urllib.request.urlopen(url, timeout=30) as response:
                fetched[path] = response.read()
        except (urllib.error.URLError, OSError) as exc:
            raise FetchError(url, exc) from exc
    return fetched


USAGE = "usage: hub_fetch_run.py <hub-relative-path> [script-args...]"


def main(argv: list[str]) -> int:
    if argv[:1] in (["-h"], ["--help"]):
        print(USAGE)
        return 0
    if not argv or argv[0].startswith("-"):
        problem = f"unknown option {argv[0]!r}, " if argv else ""
        print(f"hub_fetch_run: {problem}{USAGE}", file=sys.stderr)
        return 2
    hub_path, script_args = argv[0], argv[1:]
    is_prose_gate = hub_path == PROSE_GATE_PATH
    commit = resolve_main_commit() if hub_path in COMPANIONS else None
    ref = commit or "main"
    if hub_path in COMPANIONS and not is_prose_gate:
        print(f"hub_fetch_run: running {hub_path} from {HUB_REPO}@{ref}.", file=sys.stderr)
    uv = shutil.which("uv")
    if uv is None:
        print("hub_fetch_run: uv is not on PATH, so the gate did not run.", file=sys.stderr)
        return 1
    env = dict(os.environ)
    if is_prose_gate and not env.get(PROVENANCE_VAR, "").strip():
        if commit:
            env[PROVENANCE_VAR] = f"{HUB_REPO}@{commit} (hub_fetch_run.py main)"
        else:
            env[PROVENANCE_VAR] = f"{HUB_REPO}@main (hub_fetch_run.py, commit unresolved)"
    with tempfile.TemporaryDirectory() as scratch:
        paths = (*COMPANIONS.get(hub_path, ()), hub_path)
        try:
            fetched = fetch_all(ref, paths)
            if commit is None and len(paths) > 1 and fetch_all(ref, paths) != fetched:
                print(
                    "hub_fetch_run: main moved while the files were fetched, "
                    "so a mixed pair was refused, re-run to fetch a consistent pair.",
                    file=sys.stderr,
                )
                print("hub_fetch_run: the gate did not run.", file=sys.stderr)
                return 1
        except FetchError as exc:
            print(f"hub_fetch_run: could not fetch {exc.url}: {exc.cause}", file=sys.stderr)
            print("hub_fetch_run: the gate did not run.", file=sys.stderr)
            return 1
        # A file keeps its hub-relative path, so a sys.path entry derived from it stays in here.
        for path, content in fetched.items():
            target = Path(scratch) / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(content)
        script = Path(scratch) / hub_path
        cmd = [uv, "run", "--no-project", "--script", str(script), *script_args]
        child = subprocess.Popen(cmd, env=env)
        try:
            return child.wait()
        finally:
            child.terminate()
            child.wait()


# The first signal the handler took, where one interrupted the run.
received: list[int] = []


def on_signal(signum: int, _frame: object) -> None:
    """Record the signal and raise KeyboardInterrupt, so both signals unwind one path.

    A later signal is passed over, since it would cut the unwinding short. One is the SIGTERM that
    `uv run` forwards to a process group the terminal's SIGINT already reached.
    """
    if received:
        return
    received.append(signum)
    raise KeyboardInterrupt


def run(argv: list[str]) -> int | signal.Signals:
    """Run main, returning its status, or the signal that interrupted it once its cleanup ran."""
    signal.signal(signal.SIGTERM, on_signal)
    # A SIGINT inherited as ignored, as a background child's is, stays ignored.
    if signal.getsignal(signal.SIGINT) is signal.default_int_handler:
        signal.signal(signal.SIGINT, on_signal)
    try:
        return main(argv)
    except KeyboardInterrupt:
        signum = signal.Signals(received[-1] if received else signal.SIGINT)
        print(f"hub_fetch_run: interrupted by {signum.name}.", file=sys.stderr)
        return signum


def reraise(signum: signal.Signals) -> None:
    """End this process by `signum`, so a caller sees the signal rather than an exit status."""
    with contextlib.suppress(OSError):
        sys.stdout.flush()
        sys.stderr.flush()
    if sys.platform == "win32":
        sys.exit(130)
    signal.signal(signum, signal.SIG_DFL)
    os.kill(os.getpid(), signum)
    sys.exit(128 + signum)


if __name__ == "__main__":
    status = run(sys.argv[1:])
    if isinstance(status, signal.Signals):
        reraise(status)
    sys.exit(status)
