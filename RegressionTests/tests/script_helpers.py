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
PIDFILE_STUB = (
    'echo $$ > "$STUB_PIDFILE.tmp"\n'
    'mv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\n'
    "exec sleep 120 </dev/null >/dev/null 2>&1\n"
)


def write_stub(directory: Path, name: str, body: str) -> None:
    stub = directory / name
    stub.write_text("#!/bin/bash\n" + body)
    stub.chmod(0o755)


def signals() -> list[signal.Signals]:
    cases = [signal.SIGTERM]
    if signal.getsignal(signal.SIGINT) is not signal.SIG_IGN:
        cases.append(signal.SIGINT)
    return cases


def interrupt(
    case: unittest.TestCase,
    argv: list[str],
    env: dict[str, str],
    pidfile: Path,
    sig: signal.Signals,
) -> str:
    """Run argv, signal it once the stub child records its pid, and return its stderr.

    Asserts that the script died by the signal, that the stub child is gone,
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
        with case.assertRaises(ProcessLookupError):
            os.kill(child, 0)
    finally:
        try:
            os.killpg(proc.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        proc.wait()
    case.assertEqual(proc.returncode, -sig, err)
    case.assertIn(f"interrupted by {sig.name}", err)
    return err
