"""Shared helpers for the tests that run the shell scripts under stubs and signals."""

import os
import shutil
import signal
import subprocess
import sys
import time
import unittest
from pathlib import Path

NEEDS_BASH = unittest.skipUnless(
    sys.platform.startswith("linux") and shutil.which("bash") is not None, "needs Linux with bash"
)
PIDFILE_PUBLISH = 'echo $$ > "$STUB_PIDFILE.tmp"\nmv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\n'
PIDFILE_STUB = PIDFILE_PUBLISH + "exec sleep 120 </dev/null >/dev/null 2>&1\n"


def write_stub(directory: Path, name: str, body: str) -> None:
    stub = directory / name
    stub.write_text("#!/bin/bash\n" + body)
    stub.chmod(0o755)


def run_script(script: Path, *args: str, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["bash", str(script), *args],
        env=env,
        stdin=subprocess.DEVNULL,
        capture_output=True,
        text=True,
        check=False,
        timeout=60,
    )


def with_stubs(env: dict[str, str], stubs: Path) -> dict[str, str]:
    return env | {"PATH": f"{stubs}{os.pathsep}{os.environ.get('PATH', '')}"}


def stubbed_env(env: dict[str, str], stubs: Path, pidfile: Path) -> dict[str, str]:
    return with_stubs(env, stubs) | {"STUB_PIDFILE": str(pidfile)}


def signals() -> list[signal.Signals]:
    cases = [signal.SIGTERM]
    if signal.getsignal(signal.SIGINT) is not signal.SIG_IGN:
        cases.append(signal.SIGINT)
    return cases


def gone(pid: int, grace: float) -> bool:
    deadline = time.monotonic() + grace
    while True:
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return True
        if time.monotonic() >= deadline:
            return False
        time.sleep(0.05)


def interrupt(
    case: unittest.TestCase,
    argv: list[str],
    env: dict[str, str],
    pidfile: Path,
    sig: signal.Signals,
    grace: float = 0,
) -> str:
    """Run argv, signal it once the stub child records its pid, and return its stderr.

    Asserts that the script died by the signal, that the stub child is gone within grace seconds,
    and that stderr names the signal.
    """
    proc = subprocess.Popen(
        argv,
        env=env,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        start_new_session=True,
    )
    try:
        deadline = time.monotonic() + 30
        while not pidfile.exists() and time.monotonic() < deadline:
            time.sleep(0.05)
        case.assertTrue(pidfile.exists(), "the script never started its child")
        child = int(pidfile.read_text())
        proc.send_signal(sig)
        _, err = proc.communicate(timeout=30)
        case.assertTrue(gone(child, grace), f"the child {child} outlived the script")
    finally:
        try:
            os.killpg(proc.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        proc.wait()
        if pidfile.exists():
            try:
                os.kill(int(pidfile.read_text()), signal.SIGKILL)
            except ProcessLookupError:
                pass
    case.assertEqual(proc.returncode, -sig, err)
    case.assertIn(f"interrupted by {sig.name}", err)
    return err
