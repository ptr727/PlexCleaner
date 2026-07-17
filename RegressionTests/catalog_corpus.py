"""
catalog_corpus.py - derive a reproducible, machine-readable issue catalog for a media corpus
from a versioned PlexCleaner regression run.

Generated, never hand-maintained: re-run it and it re-derives every file's issue set from the
actual tool output of a chosen run. Output is `catalog.json` ONLY (automation; no human README);
it is the source of truth for what each file must reproduce and the target the reduced (quickscan)
corpus is validated against. Shared parsing/classification lives in corpus_common.py.

Usage: catalog_corpus.py [--run <version-dir>] [--out catalog.json]
"""

import argparse
import json
import os
from collections import Counter
from pathlib import Path

from corpus_common import (
    MEDIA_EXTS,
    SRC_DIR,
    buckets_for,
    classify_signature,
    find_run,
    parse_log,
    stem_of,
)


def build(run):
    res = json.loads((run / "Results_process.json").read_text())
    versions = res.get("Versions", {})
    log = parse_log(run / "PlexCleaner_process.log")
    err_files = {stem_of(x) for x in res["Results"]["Errors"]["Files"]}
    vf_files = {stem_of(x) for x in res["Results"]["VerifyFailed"]["Files"]}

    entries = []
    for r in res["Results"]["Results"]:
        name = os.path.basename(r["OriginalFileName"])
        if Path(name).suffix.lower() not in MEDIA_EXTS:
            continue
        stem = stem_of(name)
        state = set(s.strip() for s in (r.get("State") or "").split(",") if s.strip())
        lg = log.get(stem, {"detections": set(), "errors": set(), "tracks": []})
        sig = classify_signature(lg["errors"])
        buckets = buckets_for(state, lg["detections"], sig, stem in err_files)
        # FileDeleted / consumed samples yield no derivable output; fall back to a marker bucket
        if not buckets and not state:
            buckets = {"FileDeleted"}
        entries.append(
            {
                "file": name,
                "buckets": sorted(buckets),
                "state": sorted(state),
                "result": r.get("Result"),
                "modified": r.get("Modified"),
                "in_errors": stem in err_files,
                "in_verifyfailed": stem in vf_files,
                "detections": sorted(lg["detections"]),
                "verify_errors": sorted(lg["errors"]),
                "decode_subtypes": sorted(sig),
                "tracks": lg["tracks"],
            }
        )
    entries.sort(key=lambda e: e["file"])
    return {
        "schema": 1,
        "source_run": run.name,
        "application": versions.get("Application"),
        "tools": {
            t.get("ToolType", t.get("ToolFamily", "?")): t.get("Version")
            for t in versions.get("Tools", [])
        },
        "file_count": len(entries),
        "files": entries,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", help="version dir (default newest develop run)")
    ap.add_argument("--out", default=str(SRC_DIR / "catalog.json"))
    args = ap.parse_args()

    run = find_run(args.run)
    catalog = build(run)
    Path(args.out).write_text(json.dumps(catalog, indent=2, ensure_ascii=False))

    print(f"run={run.name} app={catalog['application']} files={catalog['file_count']}")
    print(f"catalog -> {args.out}")
    bc = Counter(b for e in catalog["files"] for b in e["buckets"])
    print("bucket distribution:")
    for b, n in bc.most_common():
        print(f"  {n:3}  {b}")


if __name__ == "__main__":
    main()
