"""Tests that the harness and the corpus tools answer --help with no REGRESSION_* variable set."""

import os
import shutil
import subprocess
import sys
import unittest
from pathlib import Path

TOOLS = ["audit_physical.py", "catalog_corpus.py", "locate_issue.py", "reduce_corpus.py"]
ROOT = Path(__file__).resolve().parent.parent
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
]


def clean_env() -> dict[str, str]:
    return {k: v for k, v in os.environ.items() if not k.startswith("REGRESSION_")}


class HelpTests(unittest.TestCase):
    def test_help_needs_no_environment(self) -> None:
        env = clean_env()
        for tool in TOOLS:
            with self.subTest(tool=tool):
                proc = subprocess.run(
                    [sys.executable, str(ROOT / tool), "--help"],
                    env=env,
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=60,
                )
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertIn("usage:", proc.stdout)

    def test_missing_variable_is_named(self) -> None:
        env = clean_env()
        proc = subprocess.run(
            [sys.executable, str(ROOT / "catalog_corpus.py")],
            env=env,
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )
        self.assertNotEqual(proc.returncode, 0)
        self.assertRegex(proc.stderr, r"REGRESSION_[A-Z_]+ is not set")


@unittest.skipIf(sys.platform == "win32" or shutil.which("bash") is None, "needs Linux bash")
class HarnessTests(unittest.TestCase):
    def run_harness(self, *args: str, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["bash", str(HARNESS), *args],
            env=env,
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )

    def test_help_lists_every_variable(self) -> None:
        proc = self.run_harness("--help", env=clean_env())
        self.assertEqual(proc.returncode, 0, proc.stderr)
        for name in HARNESS_VARIABLES:
            self.assertIn(name, proc.stdout)

    def test_missing_variable_is_named_before_any_host_lookup(self) -> None:
        proc = self.run_harness("quick", env=clean_env())
        self.assertEqual(proc.returncode, 2)
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
        self.assertEqual(proc.returncode, 2)
        self.assertIn("REGRESSION_THREAD_COUNT", proc.stderr)


if __name__ == "__main__":
    unittest.main()
