"""Tests for the pure helpers in audit_physical."""

import unittest

from audit_physical import shapes_of


class AuditPhysicalTests(unittest.TestCase):
    def test_shapes_of(self) -> None:
        errors = ["[h264 @ 0xADDR] mmco 3", "[h264 @ 0xADDR] mmco 8", "<silent-nonzero-exit>"]
        self.assertEqual(shapes_of(errors), ["[h264] mmco N"])


if __name__ == "__main__":
    unittest.main()
