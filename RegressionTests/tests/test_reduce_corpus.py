"""Tests for the pure helpers in reduce_corpus."""

import json
import tempfile
import unittest
from pathlib import Path

import reduce_corpus


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


if __name__ == "__main__":
    unittest.main()
