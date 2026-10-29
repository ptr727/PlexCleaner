"""Tests for the pure helpers in audit_physical."""

import unittest

from audit_physical import shapes_of


class AuditPhysicalTests(unittest.TestCase):
    def test_shapes_of(self) -> None:
        errors = [
            "[hevc @ 0xADDR] Could not find ref with POC 12",
            "[h264 @ 0xADDR] mmco 3",
            "[h264 @ 0xADDR] mmco 8",
            "<silent-nonzero-exit>",
        ]
        self.assertEqual(
            shapes_of(errors), ["[h264] mmco N", "[hevc] Could not find ref with POC N"]
        )


if __name__ == "__main__":
    unittest.main()
