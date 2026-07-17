#!/usr/bin/env python3
"""
audit_physical.py - physical-error-shape audit of the reduced corpus.

The reduce gate compares States, detections and broad signature CLASSES; a class can lump several
distinct physical ffmpeg messages, so a clip could carry a different physical defect that maps to
the same class - and a future, more precise PlexCleaner classification would then invalidate the
sample. This audit closes that gap: it extracts every physical error SHAPE (exact message
template, run- and site-varying content normalized) from the ground run and from each reduced
clip's processing log, and reports any source shape the clip does not reproduce.

Augments the reduced collection's catalog.json in place with per-file:
  source_error_shapes / clip_error_shapes / missing_error_shapes / shape_coverage
Kept-full and verbatim entries are equal by construction (the shipped file IS the source).
"""

import argparse
import json
from collections.abc import Iterable
from pathlib import Path

from corpus_common import error_shape, find_run, parse_log, stem_of

# Default paths for the reference server; override on the command line for another environment.
SCRATCH = Path("/data/media/PlexCleaner/scratch-trim")


def shapes_of(errors: Iterable[str]) -> list[str]:
    return sorted({error_shape(e) for e in errors if e != "<silent-nonzero-exit>"})


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--run", help="ground-truth run dir (default: newest develop run)")
    ap.add_argument(
        "--reduced",
        default="/data/media/troublesome/reduced",
        help="reduced collection dir holding catalog.json",
    )
    ap.add_argument(
        "--work",
        default=str(SCRATCH / "work"),
        help="root of the per-clip processing logs from the reduce run",
    )
    args = ap.parse_args()

    reduced = Path(args.reduced)
    work = Path(args.work)
    run = find_run(args.run)
    ground = parse_log(run / "PlexCleaner_process.log")
    collection = json.loads((reduced / "catalog.json").read_text())
    manifest = collection["files"]

    gaps = 0
    audited = 0
    for e in manifest:
        stem = stem_of(e["file"])
        src_shapes = shapes_of(ground.get(stem, {"errors": set()})["errors"])
        e["source_error_shapes"] = src_shapes

        if e["decision"] != "reduced" or e.get("method") == "verbatim":
            # the shipped file IS the source; physically identical by construction
            e["clip_error_shapes"] = src_shapes
            e["missing_error_shapes"] = []
            continue

        clip_log = work / stem / "out" / "clip_process.log"
        if not clip_log.exists():
            e["clip_error_shapes"] = None
            e["missing_error_shapes"] = ["<no clip log found - rerun needed>"]
            gaps += 1
            continue
        audited += 1
        cl_shapes = shapes_of(parse_log(clip_log).get(stem, {"errors": set()})["errors"])
        missing = sorted(set(src_shapes) - set(cl_shapes))
        e["clip_error_shapes"] = cl_shapes
        e["missing_error_shapes"] = missing
        if missing:
            gaps += 1
            print(f"GAP  {e['file'][:66]}")
            for s in missing:
                print(f"     - {s[:130]}")

    # Quantified completeness: the corpus does not need to be perfect, it needs to be MEASURED.
    # Per-file coverage + a corpus-level figure make the residual gap explicit, so it is always
    # known when a change touches an under-covered area and a full-corpus run is warranted.
    total_src = total_hit = 0
    for e in manifest:
        src = set(e["source_error_shapes"])
        clip = set(e["clip_error_shapes"] or [])
        e["shape_coverage"] = round(len(src & clip) / len(src), 3) if src else 1.0
        total_src += len(src)
        total_hit += len(src & clip)

    (reduced / "catalog.json").write_text(json.dumps(collection, indent=2, ensure_ascii=False))
    with_src = sum(1 for e in manifest if e["source_error_shapes"])
    print(f"\naudited {audited} cut clips ({with_src} files have source error shapes at all)")
    print(f"files with physical-shape gaps: {gaps}")
    print(
        f"CORPUS PHYSICAL-SHAPE COVERAGE: {total_hit}/{total_src} shapes = "
        f"{100 * total_hit / total_src:.1f}%"
        if total_src
        else "no source shapes"
    )
    inc = [(e["file"], e["shape_coverage"]) for e in manifest if e["shape_coverage"] < 1.0]
    if inc:
        print("incomplete files (full-corpus run needed for changes touching these):")
        for f, c in sorted(inc, key=lambda x: x[1]):
            print(f"  {c * 100:5.1f}%  {f[:66]}")
    print(f"catalog augmented: {reduced / 'catalog.json'}")


if __name__ == "__main__":
    main()
