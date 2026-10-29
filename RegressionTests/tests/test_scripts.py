"""Tests for the pure helpers in the corpus scripts."""

import json
import tempfile
import unittest
from pathlib import Path

import locate_issue
import reduce_corpus
from audit_physical import shapes_of


class ReduceCorpusTests(unittest.TestCase):
    def test_human(self) -> None:
        self.assertEqual(reduce_corpus.human(512), "512B")
        self.assertEqual(reduce_corpus.human(2048), "2K")
        self.assertEqual(reduce_corpus.human(5 * 1024**3), "5G")
        self.assertEqual(reduce_corpus.human(3 * 1024**5), "3P")

    def test_load_regions(self) -> None:
        with tempfile.TemporaryDirectory() as d:
            rules = Path(d) / "rules.json"
            self.assertEqual(reduce_corpus.load_regions(rules), {})
            rules.write_text(json.dumps({"regions": {"a": {"start": "5", "end": 9}}}))
            self.assertEqual(reduce_corpus.load_regions(rules), {"a": (5, 9)})

    def test_load_accept_validates(self) -> None:
        raw = {
            "accept": {
                "_comment": {},
                "good": {"state_missing": ["x"]},
                "bad": {"state_missing": "x"},
                "worse": "text",
            }
        }
        with tempfile.TemporaryDirectory() as d:
            rules = Path(d) / "rules.json"
            self.assertEqual(reduce_corpus.load_accept(rules), {})
            rules.write_text(json.dumps(raw))
            self.assertEqual(reduce_corpus.load_accept(rules), {"good": {"state_missing": ["x"]}})
            rules.write_text(json.dumps({"accept": []}))
            self.assertEqual(reduce_corpus.load_accept(rules), {})


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


class AuditPhysicalTests(unittest.TestCase):
    def test_shapes_of(self) -> None:
        errors = ["[h264 @ 0xADDR] mmco 3", "[h264 @ 0xADDR] mmco 8", "<silent-nonzero-exit>"]
        self.assertEqual(shapes_of(errors), ["[h264] mmco N"])


if __name__ == "__main__":
    unittest.main()
