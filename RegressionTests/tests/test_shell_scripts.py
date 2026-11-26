"""Tests the exit statuses, the --help text, and the interrupt handling of the shell scripts."""

import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

from test_help import ROOT, clean_env
from test_regression_harness import write_stub

REPO = ROOT.parent
SCRIPTS = [
    REPO / "Docker" / "build.sh",
    REPO / "Docker" / "install-debug-tools.sh",
    REPO / "Docker" / "test.sh",
    REPO / "Docker" / "version.sh",
    ROOT / "integration-test.sh",
    ROOT / "regression-test.sh",
]
NEEDS_BASH = unittest.skipUnless(
    sys.platform.startswith("linux") and shutil.which("bash") is not None, "needs Linux with bash"
)
PIDFILE_STUB = (
    'echo $$ > "$STUB_PIDFILE.tmp"\n'
    'mv "$STUB_PIDFILE.tmp" "$STUB_PIDFILE"\n'
    "exec sleep 120 </dev/null >/dev/null 2>&1\n"
)


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


def signals() -> list[tuple[signal.Signals, str]]:
    cases = [(signal.SIGTERM, "SIGTERM")]
    if signal.getsignal(signal.SIGINT) is not signal.SIG_IGN:
        cases.append((signal.SIGINT, "SIGINT"))
    return cases


@NEEDS_BASH
class RefusalTests(unittest.TestCase):
    def test_unknown_argument_exits_64_naming_it(self) -> None:
        for script in SCRIPTS:
            with self.subTest(script=script.name):
                proc = run_script(script, "--bogus", env=clean_env())
                self.assertEqual(proc.returncode, 64, proc.stderr)
                self.assertEqual(proc.stdout, "")
                self.assertIn("--bogus", proc.stderr)

    def test_help_exits_0_and_names_the_statuses(self) -> None:
        for script in SCRIPTS:
            with self.subTest(script=script.name):
                proc = run_script(script, "--help", env=clean_env())
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertIn("64", proc.stdout)

    def test_help_with_an_extra_argument_exits_64(self) -> None:
        for script in SCRIPTS:
            with self.subTest(script=script.name):
                proc = run_script(script, "-h", "extra", env=clean_env())
                self.assertEqual(proc.returncode, 64, proc.stderr)
                self.assertEqual(proc.stdout, "")
                self.assertIn("extra", proc.stderr)

    @unittest.skipUnless(Path("/etc/os-release").is_file(), "needs /etc/os-release")
    def test_failing_command_with_status_64_exits_1(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            stubs = Path(d)
            write_stub(stubs, "dotnet", "exit 64\n")
            env = clean_env() | {"PATH": f"{stubs}{os.pathsep}{os.environ.get('PATH', '')}"}
            proc = run_script(REPO / "Docker" / "version.sh", env=env)
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertNotIn("unknown argument", proc.stderr)


@NEEDS_BASH
class InterruptTests(unittest.TestCase):
    def interrupt(
        self,
        script: Path,
        env: dict[str, str],
        stubs: Path,
        pidfile: Path,
        sig: signal.Signals,
        name: str,
        *args: str,
    ) -> None:
        env = env | {
            "PATH": f"{stubs}{os.pathsep}{os.environ.get('PATH', '')}",
            "STUB_PIDFILE": str(pidfile),
        }
        proc = subprocess.Popen(
            ["bash", str(script), *args],
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
            self.assertTrue(pidfile.exists(), "the script never started its child")
            child = int(pidfile.read_text())
            proc.send_signal(sig)
            _, err = proc.communicate(timeout=30)
            with self.assertRaises(ProcessLookupError):
                os.kill(child, 0)
        finally:
            try:
                os.killpg(proc.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            proc.wait()
        self.assertEqual(proc.returncode, -sig, err)
        self.assertIn(f"interrupted by {name}", err)

    @unittest.skipIf(Path("/Test/Media").exists(), "the host has /Test/Media")
    def test_test_script_stops_its_download_and_removes_it(self) -> None:
        wget = (
            'while [[ $# -gt 0 ]]; do\n    if [[ "$1" == -O ]]; then out="$2"; break; fi\n'
            "    shift\ndone\n"
            'printf data > "$out"\n' + PIDFILE_STUB
        )
        for sig, name in signals():
            with self.subTest(signal=name), tempfile.TemporaryDirectory() as d:
                stubs = Path(d) / "bin"
                stubs.mkdir()
                write_stub(stubs, "wget", wget)
                tmp = Path(d) / "tmp"
                tmp.mkdir()
                env = clean_env() | {"TMPDIR": str(tmp)}
                self.interrupt(
                    REPO / "Docker" / "test.sh", env, stubs, Path(d) / "child.pid", sig, name
                )
                zip_file = tmp / "PlexCleaner-Test" / "matroska-test-files.zip"
                self.assertFalse(zip_file.exists())

    def test_integration_script_stops_its_container(self) -> None:
        tools = json.loads((REPO / ".config" / "dotnet-tools.json").read_text())
        version = tools["tools"]["dotnet-coverage"]["version"]
        for sig, name in signals():
            with self.subTest(signal=name), tempfile.TemporaryDirectory() as d:
                stubs = Path(d) / "bin"
                stubs.mkdir()
                write_stub(stubs, "docker", PIDFILE_STUB)
                dll = Path(d) / "nuget" / "dotnet-coverage" / version / "x" / "dotnet-coverage.dll"
                dll.parent.mkdir(parents=True)
                dll.write_bytes(b"")
                env = clean_env() | {"NUGET_PACKAGES": str(Path(d) / "nuget")}
                work = Path(d) / "work"
                self.interrupt(
                    ROOT / "integration-test.sh",
                    env,
                    stubs,
                    Path(d) / "child.pid",
                    sig,
                    name,
                    "image:tag",
                    str(work),
                )


if __name__ == "__main__":
    unittest.main()
