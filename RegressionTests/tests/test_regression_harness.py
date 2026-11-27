"""Tests that the regression harness script answers --help, validates its inputs, and stops on a signal."""

import os
import signal
import subprocess
import tempfile
import unittest
from pathlib import Path

from script_helpers import (
    NEEDS_BASH,
    NEEDS_JQ,
    PIDFILE_PUBLISH,
    PIDFILE_STUB,
    interrupt,
    run_script,
    signals,
    stubbed_env,
    with_stubs,
    write_stub,
)
from test_help import ROOT, clean_env

HARNESS = ROOT / "regression-test.sh"
HARNESS_VARIABLES = [
    "REGRESSION_HOST_CONFIG",
    "REGRESSION_TEST_MEDIA",
    "REGRESSION_PLUGIN_BUILD_DIR",
    "REGRESSION_CORPUS_DATASET",
    "REGRESSION_TEST_DATASET",
    "REGRESSION_THREAD_COUNT",
    "REGRESSION_CORPUS_DIR",
    "REGRESSION_SCRATCH_DIR",
    "REGRESSION_CRASH_DUMP",
]

HARNESS_ENV = {
    "REGRESSION_HOST_CONFIG": "/config",
    "REGRESSION_TEST_MEDIA": "/m/test",
    "REGRESSION_PLUGIN_BUILD_DIR": "/build",
    "REGRESSION_CORPUS_DATASET": "pool/corpus",
    "REGRESSION_TEST_DATASET": "pool/test",
    "REGRESSION_THREAD_COUNT": "4",
}


def refusing_host(directory: str, env: dict[str, str]) -> dict[str, str]:
    stubs = Path(directory)
    write_stub(stubs, "id", "exit 1\n")
    write_stub(stubs, "getent", "exit 2\n")
    return with_stubs(env, stubs)


@NEEDS_BASH
class HarnessTests(unittest.TestCase):
    def run_harness(self, *args: str, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
        return run_script(HARNESS, *args, env=env)

    def test_help_lists_every_variable(self) -> None:
        proc = self.run_harness("--help", env=clean_env())
        self.assertEqual(proc.returncode, 0, proc.stderr)
        for name in HARNESS_VARIABLES:
            self.assertIn(name, proc.stdout)

    def test_missing_variable_is_named_before_any_host_lookup(self) -> None:
        proc = self.run_harness("quick", env=clean_env())
        self.assertEqual(proc.returncode, 64)
        self.assertIn("REGRESSION_HOST_CONFIG is not set", proc.stderr)

    def test_invalid_thread_count_is_named(self) -> None:
        env = clean_env() | {
            "REGRESSION_HOST_CONFIG": "/config",
            "REGRESSION_TEST_MEDIA": "/media",
            "REGRESSION_PLUGIN_BUILD_DIR": "/build",
            "REGRESSION_CORPUS_DATASET": "pool/corpus",
            "REGRESSION_TEST_DATASET": "pool/test",
            "REGRESSION_THREAD_COUNT": "0",
        }
        proc = self.run_harness("quick", env=env)
        self.assertEqual(proc.returncode, 64)
        self.assertIn("REGRESSION_THREAD_COUNT", proc.stderr)

    def test_unknown_plugin_is_refused_before_any_host_lookup(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            env = refusing_host(d, clean_env() | HARNESS_ENV)
            proc = self.run_harness("quick", "develop", "full", "bogus", env=env)
        self.assertEqual(proc.returncode, 64, proc.stderr)
        self.assertIn("'bogus'", proc.stderr)

    def test_crash_dump_other_than_one_is_refused_before_any_host_lookup(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            env = refusing_host(d, clean_env() | HARNESS_ENV | {"REGRESSION_CRASH_DUMP": "yes"})
            proc = self.run_harness("quick", env=env)
        self.assertEqual(proc.returncode, 64, proc.stderr)
        self.assertIn("REGRESSION_CRASH_DUMP is not 1: 'yes'", proc.stderr)


class StubbedHarnessTestCase(unittest.TestCase):
    def stub_host(self, d: str) -> tuple[Path, dict[str, str]]:
        stubs = Path(d) / "bin"
        stubs.mkdir()
        write_stub(
            stubs,
            "id",
            'case "$*" in "-u") echo 0 ;; "-u nonroot") echo 1001 ;; *) exit 1 ;; esac\n',
        )
        write_stub(stubs, "getent", "echo users:x:100:\n")
        return stubs, stubbed_env(clean_env() | HARNESS_ENV, stubs, Path(d) / "child.pid")

    def stub_full_run(
        self, d: Path, docker_before: str, docker_after: str
    ) -> tuple[dict[str, str], Path]:
        stubs, env = self.stub_host(str(d))
        log = d / "zfs.log"
        log.write_text("")
        config = d / "config"
        config.mkdir()
        (config / "PlexCleaner.json").write_text("{}")
        env |= {
            "REGRESSION_HOST_CONFIG": str(config),
            "REGRESSION_PLUGIN_BUILD_DIR": str(d / "build"),
            "STUB_LOG": str(log),
        }
        write_stub(
            stubs,
            "docker",
            '[[ "$1 $2" == "image inspect" ]] && echo 1.2.3\n'
            + docker_before
            + '[[ " $* " == *" process "* ]] || exit 0\n'
            'echo "docker $*" >> "$STUB_LOG"\n' + docker_after,
        )
        write_stub(stubs, "git", "exit 1\n")
        write_stub(stubs, "chown", "exit 0\n")
        write_stub(
            stubs,
            "zfs",
            'echo "$*" >> "$STUB_LOG"\ncase "$*" in\n'
            '"list -H -t snapshot -o name -s creation pool/corpus") echo pool/corpus@s2 ;;\n'
            '"get -H -o value origin pool/test") echo pool/corpus@s2 ;;\n'
            '"get -H -o value mountpoint pool/test") echo /m/test ;;\n'
            "esac\n",
        )
        return env, log


@NEEDS_BASH
class HarnessSignalTests(StubbedHarnessTestCase):
    def test_signal_stops_the_running_child(self) -> None:
        for sig in signals():
            with self.subTest(signal=sig.name), tempfile.TemporaryDirectory() as d:
                stubs, env = self.stub_host(d)
                write_stub(stubs, "docker", '[[ "$1" == pull ]] || exit 0\n' + PIDFILE_STUB)
                interrupt(
                    self,
                    ["bash", str(HARNESS), "quick", "develop"],
                    env,
                    Path(d) / "child.pid",
                    sig,
                )

    @NEEDS_JQ
    def test_signal_restores_a_provisioned_dataset(self) -> None:
        for tty, kill_works in ((False, True), (True, True), (False, False)):
            for sig in signals():
                with (
                    self.subTest(tty=tty, kill_works=kill_works, signal=sig.name),
                    tempfile.TemporaryDirectory() as d,
                ):
                    self.restore_after_signal(Path(d), sig, tty, kill_works)

    def restore_after_signal(
        self, d: Path, sig: signal.Signals, tty: bool, kill_works: bool
    ) -> None:
        stop = 'touch "$STUB_LOG.stopped"' if kill_works else "exit 1"
        process_run = (
            "trap '' TERM\n" + PIDFILE_PUBLISH + "for ((i = 0; i < 1200; i++)); do\n"
            '    [[ -e "$STUB_LOG.stopped" ]] && exit 0\n'
            "    sleep 0.1\n"
            "done\n"
            if kill_works
            else PIDFILE_STUB
        )
        env, log = self.stub_full_run(
            d,
            f'if [[ "$1" == kill ]]; then echo "docker $*" >> "$STUB_LOG"; {stop}; fi\n',
            'cp "$STUB_LOG" "$STUB_LOG.atstart"\n' + process_run,
        )
        controller, terminal = os.openpty() if tty else (-1, subprocess.DEVNULL)
        try:
            interrupt(
                self,
                ["bash", str(HARNESS), "quick", "develop"],
                env,
                Path(d) / "child.pid",
                sig,
                stdin=terminal,
            )
        finally:
            if tty:
                os.close(controller)
                os.close(terminal)
        lines = log.read_text().splitlines()
        run = len((log.parent / "zfs.log.atstart").read_text().splitlines()) - 1
        self.assertEqual(" -it " in lines[run], tty, lines[run])
        after = lines[run + 1 :]
        self.assertEqual(after[0], "docker kill --signal TERM PlexCleaner-RegressionTest")
        self.assertIn("rollback pool/test@backup", after[1:])


CRASH_DUMP_SETTINGS = (
    "DOTNET_DbgEnableMiniDump=1",
    "DOTNET_DbgMiniDumpType=2",
    "DOTNET_DbgMiniDumpName=/Test/Config/1.2.3/%e.%p.%t.dmp",
    "DOTNET_EnableCrashReport=1",
)


@NEEDS_BASH
@NEEDS_JQ
class HarnessCrashDumpTests(StubbedHarnessTestCase):
    def process_run(self, crash_dump: dict[str, str]) -> str:
        with tempfile.TemporaryDirectory() as d:
            env, log = self.stub_full_run(Path(d), "", "")
            proc = run_script(HARNESS, "quick", "develop", env=env | crash_dump)
            runs = [line for line in log.read_text().splitlines() if line.startswith("docker ")]
        self.assertEqual(len(runs), 1, proc.stderr)
        return runs[0] + " "

    def test_crash_dump_reaches_the_process_run(self) -> None:
        run = self.process_run({"REGRESSION_CRASH_DUMP": "1"})
        for setting in CRASH_DUMP_SETTINGS:
            self.assertIn(f" --env {setting} ", run)

    def test_unset_crash_dump_adds_no_dump_settings(self) -> None:
        run = self.process_run({})
        for setting in CRASH_DUMP_SETTINGS:
            self.assertNotIn(setting, run)


@NEEDS_BASH
class ProvisionDatasetTests(unittest.TestCase):
    ZFS = """echo "$*" >> "$STUB_LOG"
case "$*" in
"get -H -o value origin pool/test") echo "$STUB_ORIGIN" ;;
"get -H -o value origin pool/test-retired-1") echo pool/corpus@s1 ;;
"get -H -o value origin "*) echo - ;;
"get -H -o value mountpoint "*) echo "${STUB_MOUNTPOINT:-/m/test}" ;;
"list -H -o name") printf '%s\\n' pool/test pool/test-retired-1 pool/test-retired-backup ;;
"list -H -t snapshot pool/test@backup") [[ -n "$STUB_HAS_BACKUP" ]] ;;
"list pool/test") [[ -n "$STUB_ORIGIN" ]] ;;
esac
"""

    def provision(
        self, origin: str, has_backup: bool, mountpoint: str = ""
    ) -> tuple[subprocess.CompletedProcess[str], list[str]]:
        with tempfile.TemporaryDirectory() as d:
            stubs = Path(d) / "bin"
            stubs.mkdir()
            write_stub(stubs, "zfs", self.ZFS)
            log = Path(d) / "zfs.log"
            log.write_text("")
            script = (
                'source <(sed -n "/^CheckMountpoint()/,/^}/p;/^ProvisionDataset()/,/^}/p" "$1")\n'
                "TestDataset=pool/test Snapshot=pool/test@backup CorpusDataset=pool/corpus\n"
                "CorpusSnap=pool/corpus@s2 HostMedia=/m/test\n"
                "ProvisionDataset\n"
            )
            env = with_stubs(clean_env(), stubs) | {
                "STUB_LOG": str(log),
                "STUB_ORIGIN": origin,
                "STUB_HAS_BACKUP": "1" if has_backup else "",
                "STUB_MOUNTPOINT": mountpoint,
            }
            proc = subprocess.run(
                ["bash", "-Eeuo", "pipefail", "-c", script, "provision", str(HARNESS)],
                env=env,
                capture_output=True,
                text=True,
                check=False,
                timeout=60,
            )
            return proc, log.read_text().splitlines()

    def test_destroys_only_retired_corpus_clones(self) -> None:
        proc, calls = self.provision("pool/corpus@s1", has_backup=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        destroyed = [c for c in calls if c.startswith("destroy")]
        self.assertEqual(destroyed, ["destroy -r pool/test-retired-1"])
        self.assertIn("Skipping pool/test-retired-backup", proc.stderr)

    def test_current_clone_without_backup_is_replaced(self) -> None:
        proc, calls = self.provision("pool/corpus@s2", has_backup=False)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertTrue(any(c.startswith("rename pool/test pool/test-retired-") for c in calls))
        self.assertIn("clone pool/corpus@s2 pool/test", calls)
        self.assertIn("snapshot pool/test@backup", calls)
        self.assertFalse(any(c.startswith("rollback") for c in calls))

    def test_current_clone_with_backup_rolls_back(self) -> None:
        proc, calls = self.provision("pool/corpus@s2", has_backup=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn("rollback pool/test@backup", calls)

    def test_wrong_mountpoint_is_refused_before_any_change(self) -> None:
        proc, calls = self.provision("pool/corpus@s2", has_backup=True, mountpoint="/elsewhere")
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertIn("is not the mountpoint of pool/test", proc.stderr)
        changes = ("rollback", "rename", "clone", "snapshot", "destroy")
        self.assertEqual([c for c in calls if c.startswith(changes)], [])

    def test_dataset_that_is_not_a_corpus_clone_is_refused(self) -> None:
        proc, calls = self.provision("-", has_backup=False)
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertIn("is not a clone of pool/corpus", proc.stderr)
        changes = ("rollback", "rename", "clone", "snapshot", "destroy")
        self.assertEqual([c for c in calls if c.startswith(changes)], [])


if __name__ == "__main__":
    unittest.main()
