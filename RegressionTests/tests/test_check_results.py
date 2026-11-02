"""Tests for comparing a process results file with the expected outcomes in check_results."""

import json
import tempfile
import unittest
from pathlib import Path
from typing import Any

import check_results as cr


def result(name: str, state: str, new_name: str = "", result_ok: bool = True) -> dict[str, Any]:
    return {
        "Result": result_ok,
        "OriginalFileName": f"/Test/Media/{name}",
        "NewFileName": f"/Test/Media/{new_name or name}",
        "Modified": bool(state),
        "State": state,
    }


def results(*items: dict[str, Any]) -> dict[str, Any]:
    return {"SchemaVersion": 1, "Results": {"Results": list(items)}}


class OutcomesTests(unittest.TestCase):
    def test_keys_by_file_name_and_sorts_state(self) -> None:
        found = cr.outcomes(results(result("a.MKV", "Verified, ReMuxed", "a.mkv")))
        self.assertEqual(
            found,
            {
                "a.MKV": {
                    "NewFileName": "a.mkv",
                    "Result": True,
                    "Modified": True,
                    "State": ["ReMuxed", "Verified"],
                    "FailedOperation": None,
                }
            },
        )

    def test_empty_state(self) -> None:
        self.assertEqual(cr.outcomes(results(result("a.mkv", "")))["a.mkv"]["State"], [])

    def test_failed_operation(self) -> None:
        item = result("a.mkv", "FileDeleted", result_ok=False) | {"FailedOperation": "Verify"}
        self.assertEqual(cr.outcomes(results(item))["a.mkv"]["FailedOperation"], "Verify")

    def test_duplicate_name_refused(self) -> None:
        dup = results(result("a.mkv", "Verified"), result("a.mkv", "Verified"))
        with self.assertRaises(ValueError):
            cr.outcomes(dup)


class DifferencesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.expected = cr.outcomes(
            results(result("a.mkv", "Verified"), result("b.mkv", "ReMuxed"))
        )

    def test_match(self) -> None:
        self.assertEqual(cr.differences(self.expected, dict(self.expected)), [])

    def test_missing_and_unexpected(self) -> None:
        actual = cr.outcomes(results(result("a.mkv", "Verified"), result("c.mkv", "Verified")))
        found = cr.differences(self.expected, actual)
        self.assertEqual(len(found), 2)
        self.assertTrue(found[0].startswith("b.mkv: expected a result"))
        self.assertTrue(found[1].startswith("c.mkv: unexpected result"))

    def test_changed_state(self) -> None:
        actual = cr.outcomes(
            results(result("a.mkv", "Verified"), result("b.mkv", "ReMuxed, SetFlags"))
        )
        self.assertEqual(
            cr.differences(self.expected, actual),
            ["b.mkv: State expected ['ReMuxed'], found ['ReMuxed', 'SetFlags']"],
        )


class MainTests(unittest.TestCase):
    def setUp(self) -> None:
        self._dir = tempfile.TemporaryDirectory()
        self.addCleanup(self._dir.cleanup)
        self.root = Path(self._dir.name)
        self.results = self.root / "Results.json"
        self.expected = self.root / "expected.json"
        self.results.write_text(json.dumps(results(result("a.mkv", "Verified"))), encoding="utf-8")

    def run_main(self, *extra: str) -> int:
        args = ["--expected", str(self.expected), "--results", str(self.results), *extra]
        return cr.main(args)

    def test_write_then_check_passes(self) -> None:
        self.assertEqual(self.run_main("--write"), 0)
        self.assertEqual(self.run_main(), 0)

    def test_check_fails_on_difference(self) -> None:
        self.assertEqual(self.run_main("--write"), 0)
        self.results.write_text(json.dumps(results(result("a.mkv", "ReMuxed"))), encoding="utf-8")
        self.assertEqual(self.run_main(), 1)

    def test_unknown_argument_refused(self) -> None:
        with self.assertRaises(SystemExit) as raised:
            self.run_main("--bogus")
        self.assertNotEqual(raised.exception.code, 0)


if __name__ == "__main__":
    unittest.main()
