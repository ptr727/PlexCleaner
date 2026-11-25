"""Tests that every corpus tool answers --help with no REGRESSION_* variable set."""

import os
import subprocess
import sys
import unittest
from pathlib import Path

TOOLS = ["audit_physical.py", "catalog_corpus.py", "locate_issue.py", "reduce_corpus.py"]
ROOT = Path(__file__).resolve().parent.parent


class HelpTests(unittest.TestCase):
    def test_help_needs_no_environment(self) -> None:
        env = {k: v for k, v in os.environ.items() if not k.startswith("REGRESSION_")}
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
        env = {k: v for k, v in os.environ.items() if not k.startswith("REGRESSION_")}
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


if __name__ == "__main__":
    unittest.main()
