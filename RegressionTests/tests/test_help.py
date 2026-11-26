"""Tests that the Python tools answer --help and refuse unknown arguments."""

import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = ["audit_physical.py", "catalog_corpus.py", "locate_issue.py", "reduce_corpus.py"]
INJECTORS = ["synthetic/inject_cc_sei.py", "synthetic/inject_hdr10plus_sei.py"]
ROOT = Path(__file__).resolve().parent.parent


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

    def test_abbreviated_option_is_refused(self) -> None:
        for tool in [*TOOLS, "check_results.py", "synthetic/synthesize.py"]:
            with self.subTest(tool=tool):
                proc = subprocess.run(
                    [sys.executable, str(ROOT / tool), "--hel"],
                    env=clean_env(),
                    capture_output=True,
                    text=True,
                    check=False,
                    timeout=60,
                )
                self.assertEqual(proc.returncode, 2)
                self.assertNotIn("usage:", proc.stdout)

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


class InjectorTests(unittest.TestCase):
    @staticmethod
    def run_injector(
        tool: str, *args: str, cwd: Path | None = None
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(ROOT / tool), *args],
            cwd=cwd,
            capture_output=True,
            text=True,
            check=False,
            timeout=60,
        )

    def test_help_on_stdout(self) -> None:
        for tool in INJECTORS:
            for flag in ("--help", "-h"):
                with self.subTest(tool=tool, flag=flag):
                    proc = self.run_injector(tool, flag)
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertTrue(proc.stdout.startswith(f"usage: {Path(tool).name}"))

    def test_help_anywhere_writes_nothing(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                src, out = Path(d) / "in.hevc", Path(d) / "out.hevc"
                src.write_bytes(b"\x00\x00\x00\x01\x40\x01")
                long_form = self.run_injector(tool, "--help", str(out))
                short_form = self.run_injector(tool, str(src), "-h")
                for proc in (long_form, short_form):
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertTrue(proc.stdout.startswith(f"usage: {Path(tool).name}"))
                self.assertEqual(long_form.stdout, short_form.stdout)
                self.assertEqual(sorted(p.name for p in Path(d).iterdir()), ["in.hevc"])

    def test_abbreviated_help_is_refused(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool):
                proc = self.run_injector(tool, "--hel")
                self.assertEqual(proc.returncode, 2)
                self.assertIn("unrecognized arguments: --hel", proc.stderr)

    def test_help_wins_over_an_unknown_option(self) -> None:
        for tool in INJECTORS:
            for args in (["--help", "--bogus"], ["--bogus", "--help"]):
                with self.subTest(tool=tool, args=args):
                    proc = self.run_injector(tool, *args)
                    self.assertEqual(proc.returncode, 0, proc.stderr)
                    self.assertTrue(proc.stdout.startswith(f"usage: {Path(tool).name}"))

    def test_dash_named_input_after_end_of_options(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                (Path(d) / "-in.hevc").write_bytes(
                    b"\x00\x00\x00\x01\x40\x01\x00\x00\x00\x01\x02\x01"
                )
                proc = self.run_injector(tool, "--", "-in.hevc", "out.hevc", cwd=Path(d))
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertTrue((Path(d) / "out.hevc").is_file())

    def test_missing_input_is_named_without_a_traceback(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                missing = Path(d) / "missing.hevc"
                proc = self.run_injector(tool, str(missing), str(Path(d) / "out.hevc"))
                self.assertEqual(proc.returncode, 1)
                self.assertIn(missing.name, proc.stderr)
                self.assertNotIn("Traceback", proc.stderr)
                self.assertEqual(list(Path(d).iterdir()), [])

    def test_unknown_argument_is_refused_before_any_write(self) -> None:
        for tool in INJECTORS:
            for extra in (["--bogus"], ["out.hevc", "-x"], ["-1"], ["-1", "out.hevc"]):
                with self.subTest(tool=tool, extra=extra), tempfile.TemporaryDirectory() as d:
                    src = Path(d) / "in.hevc"
                    src.write_bytes(b"\x00\x00\x00\x01\x40\x01")
                    proc = self.run_injector(
                        tool,
                        str(src),
                        *[str(Path(d) / e) if e.endswith(".hevc") else e for e in extra],
                    )
                    self.assertEqual(proc.returncode, 2)
                    self.assertIn("unrecognized arguments", proc.stderr)
                    self.assertEqual(sorted(p.name for p in Path(d).iterdir()), ["in.hevc"])

    def test_injects_into_named_output(self) -> None:
        for tool in INJECTORS:
            with self.subTest(tool=tool), tempfile.TemporaryDirectory() as d:
                src, out = Path(d) / "in.hevc", Path(d) / "out.hevc"
                src.write_bytes(b"\x00\x00\x00\x01\x40\x01\x00\x00\x00\x01\x02\x01")
                proc = self.run_injector(tool, str(src), str(out))
                self.assertEqual(proc.returncode, 0, proc.stderr)
                self.assertGreater(out.stat().st_size, src.stat().st_size)


if __name__ == "__main__":
    unittest.main()
