"""Tests for the pure helpers in locate_issue."""

import json
import tempfile
import unittest
from pathlib import Path

import locate_issue


class LocateIssueTests(unittest.TestCase):
    def test_reproduced(self) -> None:
        self.assertTrue(locate_issue.reproduced("Error: MMCO bad", ["mmco"]))
        self.assertFalse(locate_issue.reproduced("fine", ["mmco"]))
        self.assertFalse(locate_issue.reproduced("anything", []))

    def test_verify_args(self) -> None:
        args = locate_issue.verify_args("/in.mkv", stats="-stats")
        self.assertIn("-stats", args)
        self.assertEqual(args[args.index("-i") + 1], "/in.mkv")
        self.assertEqual(args[-2:], ["null", "-"])

    def test_write_region_merges(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            rules = Path(d) / "rules.json"
            locate_issue.write_region(rules, "a", 1, 2, "n1")
            locate_issue.write_region(rules, "b", 3, 4, "n2")
            data = json.loads(rules.read_text())
        self.assertEqual(data["schema"], 1)
        self.assertEqual(data["regions"]["a"], {"start": 1, "end": 2, "note": "n1"})
        self.assertEqual(data["regions"]["b"]["end"], 4)


if __name__ == "__main__":
    unittest.main()
