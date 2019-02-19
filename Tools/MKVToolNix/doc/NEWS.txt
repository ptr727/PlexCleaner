# Version 31.0.0 "Dolores In A Shoestand" 2019-02-09

## New features and enhancements

* all programs: added a new option `--abort-on-warnings` that will cause the
  program to abort after it has emitted the first warning, similar to how it
  aborts after the first error. Implements #2493.
* mkvmerge, mkvextract: when closing files that were opened for writing,
  cached data will not be flushed to storage automatically anymore. This
  reverts the workaround implemented for #2469. A new option was added to both
  programs (`--flush-on-close`) that re-enables flushing for people who are
  affected by data loss such as described in #2469.

  The reason is that automatic flushing causes long delays in processing
  queues when the output by mkvmerge/mkvextract isn't the final product but
  just an intermediate result to be processed further.

  Implements #2480.
* MKVToolNix GUI: multiplexer: the dialog previewing different character sets
  for text subtitles will now keep the position of the displayed text when
  switching between character sets. Implements #2489.

## Bug fixes

* mkvmerge: AVI reader: using DV type 1 AVIs will now result in an unsupported
  file type being reported (as the underlying AVI library doesn't support
  them) instead of crashing mkvmerge. Fixes #2491.
* mkvmerge: HEVC: the height of interlaced streams will now be set correctly
  to the height of the full frame instead of the height of a single interlaced
  field. Fixes #2446.
* mkvmerge: MP4 reader: edit lists consisting solely of elements that mkvmerge
  doesn't support (such as dwells) are simply ignored. Before no data was read
  for such tracks at all. Fixes #2487.
* mkvmerge: text subtitles: entries with an explicit duration of 0ms will now
  be handled correctly: the 0ms duration will be stored in Matroska instead of
  the difference between the current and the following entry. Fixes #2490.
* MKVToolNix GUI: multiplexer, chapter editor: fixed drag & drop handling with
  Qt 5.12.0 and newer. Fixes #2472.
* MKVToolNix GUI: multiplexer: the GUI did not clean up temporary files
  created when running `mkvmerge`. Fixes #2499.

## Build system changes

* Qt 5.4.0 or newer has required (up from 5.3.0) since version 30.0.0; I just
  forgot to include this entry.


# Version 30.1.0 "Forever And More" 2019-01-05

## Bug fixes

* build system: fixed building on non-UTF-8 locales. Fixes #2474.
* MKVToolNix GUI: multiplexer: implemented a workaround for drag & drop not
  working on macOS with Qt 5.12 due to a bug in Qt 5.12. Fixes #2472.
* MKVToolNix GUI: chapter editor: when opening a Matroska/WebM file that
  doesn't contain chapters and later saving chapters back to them, the editor
  was truncating the file down to a couple of KB in size. This was a
  regression introduced with the implementation of #2439 in v30.0.0 Fixes
  #2476.


# Version 30.0.0 "Interstellar" 2019-01-04

## New features and enhancements

* mkvextract: WAV extractor: mkvextract will now write W64 files instead of
  WAV files if the file name extension is `.w64` or if the final file size is
  bigger than 4 GB, the file size limit for WAV files. Implements #2458.
* MKVToolNix GUI: multiplexer: a new button was added next to the "destination
  file" controls. Clicking it shows a menu with the ten most recently used
  output directories. Selecting one of them will change the destination file
  to the selected directory keeping the file name. Implements #2468.
* MKVToolNix GUI: multiplexer (preferences): the ten most recently used values
  for the "relative output directory" and "fixed output directory" settings
  are now saved. The corresponding settings have been changed into combo boxes
  allowing quick access to those recent values.
* MKVToolNix GUI: multiplexer (preferences): the predefined split sizes and
  durations can now be customized in the preferences.
* MKVToolNix GUI: chapter editor: added an option in the "Chapter editor" menu
  for appending chapters from an existing file to the currently open editor
  tab. Part of the implementation of #2439.
* MKVToolNix GUI: chapter editor: added an action in the context menu for
  copying the selected entry and all of its children to another open editor
  tab. Part of the implementation of #2439.

## Bug fixes

* mkvmerge: all files opened for writing will now be flushed once before
  they're closed. This ensures the operating system actually writes all cached
  data to disk preventing data loss in certain situations such as power
  outages or buggy drivers in combination with suspending the computer. Fixes
  #2469.
* mkvmerge: AAC: under certain conditions 8 channel audio files were taken for
  7 channel ones.
* MKVToolNix GUI: multiplexer: removing a file added as an "additional part"
  will no longer cause a crash. Fixes #2461.
* source code: fixed compilation with Boost 1.69.0 after API-breaking change
  to the `boost::tribool` class. Fixes #2460.


# Version 29.0.0 "Like It Or Not" 2018-12-01

## Important notes

* The string formatting library used was switched from `boost::format` to
  [`fmt`](http://fmtlib.net/). See the section "Build system changes" for
  details.

## New features and enhancements

* MKVToolNix GUI: added an option in the preferences for disabling automatic
  scaling for high DPI displays. Implements #2415.
* MKVToolNix GUI: the GUI will now prevent the system from going to sleep
  while the job queue is running. This feature is implemented for macOS,
  Windows and Linux/Unix systems where the `org.freedesktop.login1.Manager`
  D-Bus interface is available. Implements #2411.

## Bug fixes

* mkvmerge: chapter generation: the start timestamps of chapters generated in
  intervals was wrong for files whose smallest video timestamp was bigger than
  0. Fixes #2432.
* mkvmerge: MP4 reader: fixed handling of atoms whose size exceeds the parent
  atom's size. Fixes #2431.
* mkvmerge, MKVToolNix GUI's chapter editor: the chapter name template will
  now also be used when reading Ogg-style chapter files with empty chapter
  names. Fixes #2441.
* mkvextract: AAC: mkvextract will now write the program config element (PCE)
  before the first AAC raw data packet if the PCE is present in the
  `AudioSpecificConfig` structure in the `Codec Private` Matroska element. The
  PCE carries vital information about the number of channels and is required
  in certain cases. Fixes #2205 and #2433.
* mkvpropedit, MKVToolNix GUI's header editor: in situations when a one-byte
  space must be covered by a new EBML void element the following element must
  be moved up instead. If that moved element is a cluster, the corresponding
  cue entries will now be updated to reflect the cluster's new position. Fixes
  #2408.
* MKVToolNix GUI: Windows: the application manifest is now included properly
  so that Windows actually recognizes it. See #2415.

## Build system changes

* Qt's D-Bus implementation is now required for building on systems other than
  macOS and Windows,
* The `boost::format` library is not used anymore.
* The [`fmt` library](http://fmtlib.net/) is now required. Versions 3 and
  newer are supported. As not all Linux distributions include packages for the
  library, its release 5.2.1 comes bundled with MKVToolNix. The `configure`
  script will check for a system version of the library and use it if present
  and recent enough. Otherwise it will fall back to the bundled version and
  link that statically.


# Version 28.2.0 "The Awakening" 2018-10-25

## Bug fixes

* mkvmerge, mkvinfo, mkvextract, mkvpropedit, MKVToolNix GUI's info tool &
  chapter editor: fixed a case of memory being accessed after it had been
  freed earlier. This can be triggered by specially crafted Matroska files and
  lead to arbitrary code execution. The vulnerability was reported as Cisco
  TALOS 2018-0694 on 2018-10-25.


# Version 28.1.0 "Morning Child" 2018-10-23

## Bug fixes

* mkvmerge: AV1 parser: fixed an error in the sequence header parser if
  neither the `reduced_still_picture_header` nor the
  `frame_id_numbers_present_flag` is set. Part of the fix for #2410.
* mkvmerge: AV1 parser: when creating the `av1C` structure for the Codec
  Private element the sequence header OBU wasn't copied completely: its common
  data (type field & OBU size among others) was missing. Part of the fix for
  #2410.
* mkvmerge: Matroska reader, AV1: mkvmerge will try to re-create the `av1C`
  data stored in Codec Private when reading AV1 from Matroska or WebM files
  created by mkvmerge v28.0.0. Part of the fix for #2410.
* MKVToolNix GUI: info tool: the tool will no longer stop scanning elements
  when an EBML Void element is found after the first Cluster element. Fixes
  #2413.


# Version 28.0.0 "Voice In My Head" 2018-10-20

## New features and enhancements

* mkvmerge: AV1 parser: updated the code for the finalized AV1 bitstream
  specification. Part of the implementation of #2261.
* mkvmerge: AV1 packetizer: updated the code for the finalized AV1-in-Matroska
  & WebM mapping specification. Part of the implementation of #2261.
* mkvmerge: AV1 support: the `--engage enable_av1` option has been removed
  again. Part of the implementation of #2261.
* mkvmerge: MP4 reader: added support for AV1. Part of the implementation of
  #2261.
* mkvmerge: DTS: implemented dialog normalization gain removal for extension
  substreams. Implements #2377.
* mkvmerge, mkvextract: simple text subtitles: added a workaround for simple
  text subtitle tracks that don't contain a duration. Implements #2397.
* mkvextract: added support for extracting AV1 to IVF. Part of the
  implementation of #2261.
* mkvextract: IVF extractor (AV1, VP8, VP9): precise values will be used for
  the frame rate numerator & denominator header fields for certain well-known
  values of the track's default duration.
* mkvmerge: VP9: mkvmerge will now create codec private data according to the
  VP9 codec mapping described in the WebM specifications. Implements #2379.
* MKVToolNix GUI: automatic scaling for high DPI displays is activated if the
  GUI is compiled with Qt ≥ 5.6.0. Fixes #1996 and #2383.
* MKVToolNix GUI: added a menu item ("Help" → "System information") for
  displaying information about the system MKVToolNix is running on in order to
  make debugging easier.
* MKVToolNix GUI: multiplexer, header editor: the user can enter a list of
  predefined track names in the preferences. She can later select from them in
  "track name" combo box. Implements #2230.


## Bug fixes

* mkvmerge: JSON identification: fixed a bug when removing invalid UTF-8 data
  from strings before they're output as JSON. Fixes #2398.
* mkvmerge: MP4/QuickTime reader: fixed handling of PCM audio with FourCC
  `in24`. Fixes #2391.
* mkvmerge: MPEG transport stream reader, teletext subtitles: the decision
  whether or not to keep frames around in order to potentially merge them with
  the following frame is made sooner. That avoids problems if there are large
  gaps between teletext subtitle frames which could lead to frames being
  interleaved too late. Fixes #2393.
* mkvextract: IVF extractor (AV1, VP8, VP8): the frame rate header fields
  weren't clamped to 16 bits properly causing wrong frame rates to be written
  in certain situations.
* mkvpropedit, MKVToolNix GUI's header editor: fixed file corruption when a
  one-byte space must be covered with a new EBML void element but all
  surrounding elements have a "size length" field that's eight bytes long
  already. Fixes #2406.


# Version 27.0.0 "Metropolis" 2018-09-26

## New features and enhancements

* mkvmerge: chapters: the timestamps of chapters read from containers or from
  chapter files can be adjusted (multiplication and addition) with the new
  `--chapter-sync` option or using the special track ID `-2` for the existing
  `--sync` option. Part of the implementation of #2358.
* MKVToolNix GUI: multiplexer: adjusted & added controls for mkvmerge's new
  feature of being able to adjust chapter timestamps. Part of the
  implementation of #2358.
* MKVToolNix GUI: multiplexer: the GUI can now ask for confirmation when the
  user is about to create a file that won't contain audio tracks. It does this
  by default if at least one source file contains an audio track. Implements
  #2380.

## Bug fixes

* mkvmerge: AC-3: dialog normalization gain removal was corrupting E-AC-3
  frames irreversibly by writing checksums in places where they didn't
  belong. Additionally only the first E-AC-3 frame in a Matroska was processed
  but not additional dependent frames in the same block. Fixes #2386.
* MKVToolNix GUI: fixed a leak of Windows font resources leading to a general
  slowdown and subsequent crash. Fixes #2372.


# Version 26.0.0 "In The Game" 2018-08-26

## New features and enhancements

* mkvmerge: chapter generation: if the name template given by
  `--generate-chapters-name-template` is empty, no names (`ChapterDisplay`
  master elements with `ChapterString`/`ChapterLanguage` children) will be
  generated for the chapter atoms. Part of the implementation of #2275.
* mkvmerge: chapters: chapter names generated from MPLS files will now use the
  name template if one is set via `--generate-chapters-name-template`. Part of
  the implementation of #2275.
* mkvmerge: mkvmerge will no longer abort with an error message if no audio,
  video and subtitle tracks should be multiplexed. This allows copying of
  chapters from non-chapter source files (e.g. Matroska or MP4 files).
* MKVToolNix GUI: the font size in the tool selector on the left will scale
  with the font size the user selects in the preferences.
* MKVToolNix GUI: the GUI will no longer automatically resize the columns in
  tree and list views to match the content size. Instead it remembers and
  restores the widths set by the user. Implements #2353.
* MKVToolNix GUI: multiplexer: the chapter name template will now be set
  automatically to the name template in the preferences' "chapter editor"
  section. Additionally the option `--generate-chapters-name-template …` will
  be passed to mkvmerge in situations when mkvmerge will generate chapters
  (either because automatic generation is enabled or if chapters are generated
  for MPLS playlists). Part of the implementation of #2275.
* MKVToolNix GUI: chapter editor: if the chapter name template is empty,
  chapters will be generated without names. Part of the implementation of
  #2275.
* MKVToolNix GUI: chapter editor: added an option to remove all chapter names
  to the "additional modifications" dialog. Part of the implementation of
  #2275.

## Bug fixes

* mkvmerge: Matroska reader: fixed wrong timestamps when appending Matroska
  files where the second Matroska file's first timestamp is bigger
  than 0. Fixes #2345.
* mkvmerge: MP4 reader: fixed division by zero errors during file
  identification if the timescale is 0 in the `MVHD` atom.
* mkvmerge: Windows Television DVR files are now recognized as an unsupported
  file type. This prevents mis-detection as MPEG-2 with an accompanying flood
  of error messages. Fixes #2347.
* MKVToolNix GUI: info tool: under certain circumstances "cues" were shown at
  the wrong level (inside the previous master element instead of on level
  1). Fixes #2361.
* MKVToolNix GUI: job queue: fixed invalid memory handling and consequent
  crashes when using the "edit in corresponding tool & remove from job queue"
  option if one of the files in that job contained attached files. Fixes
  #2368.

## Build system changes

* An AppStream metadata file will be installed in `$prefix/share/metainfo`.


# Version 25.0.0 "Prog Noir" 2018-07-12

## New features and enhancements

* mkvmerge: SRT/ASS/SSA text subtitles: for files for which no encoding has
  been specified, mkvmerge will try UTF-8 first before falling back to the
  system's default encoding. Part of the implementation of #2246.
* mkvmerge: SRT/ASS/SSA/WebVTT text subtitles: a warning is now emitted if
  invalid 8-bit characters are encountered outside valid multi-byte UTF-8
  sequences. Part of the implementation of #2246.
* mkvmerge: Matroska & MPEG transport stream readers: the encoding of text
  subtitles read from Matroska files can now be changed with the
  `--sub-charset` parameter.
* Linux: starting with release 25 an AppImage will be provided which should
  run on any Linux distribution released around the time of CentOS 7/Ubuntu
  14.04 or later.
* macOS: translations: updated the `build.sh` script to build `libiconv` and a
  complete `gettext`. Together with an additional fix to how translation files
  are located, MKVToolNix can now use all interface languages on macOS,
  too. Fixes #2110, #2307, #2323.

## Bug fixes

* mkvmerge: AVC/h.264: fixed file identification failing for certain
  elementary streams due to internal buffers not being cleared properly. Fixes
  #2325.
* mkvmerge: HEVC/h.265: fixed file identification failing for certain
  elementary streams due to internal buffers not being cleared properly. This
  is the HEVC analog to what was fixed for AVC in #2325.
* mkvmerge: MLP code: fixed various issues preventing MLP from being parsed
  correctly. Fixes #2326.
* mkvmerge: TrueHD/MLP packetizer; dialog volume normalization removal isn't
  attempted if the track is an MLP track as the operation is only supported
  for TrueHD, not MLP.
* mkvmerge: MPEG TS reader: when reading MPLS mkvmerge will now compare the
  MPLS's start and end timestamps against the transport stream's PTS instead
  of its DTS. Otherwise the first key frame of a video track might be dropped
  if it isn't the first in presentation order. Fixes #2321.
* mkvmerge: JSON identification: mkvmerge will ensure that all strings passed
  to the JSON output modules are valid UTF-8 encoded strings by replacing
  invalid bytes with placeholder characters. This avoids the JSON library
  throwing an exception and mkvmerge aborting on such data. Fixes #2327.
* mkvmerge: audio packetizers: mkvmerge will now keep discard padding values
  if they're present for packets read from Matroska files. Fixes #2296.
* mkvmerge: Ogg Opus reader: packet timestamps aren't calculated by summing up
  the duration of all packets starting with timestamp 0 anymore. Instead the
  algorithm is based on the Ogg page's granule position and which packet
  number is currently timestamped (special handling for the first and last
  packets in the stream).

  * This fixes the first timestamp if the first Ogg packet's granule position
    is larger than the number of samples in the first packet (= if the first
    sample's timestamp is bigger than 0). mkvmerge will keep those offsets now
    and inserts "discard padding" only where it's actually needed.
  * It also improves handling of invalid files where the first Ogg packet's
    granule position is smaller than the number of samples in the first packet
    (= the first sample's timestamp is smaller than 0). mkvmerge will now
    shift all timestamps up to 0 in such a case instead of inserting "discard
    padding" elements all over the place.
  * mkvmerge will no longer insert "discard padding" elements if the
    difference between a) the calculated number of samples in the packet
    according to the granule position and b) the actual number of samples as
    calculated from the bitstream is one sample or less and if the packet
    isn't the last one in the stream. This circumvents certain rounding
    errors.
  * The timestamp of the first packet after a gap in the middle of the stream
    is now calculated based on the Ogg page the packet belongs to, and not
    based on the timestamps before the gap.

  Fixes #2280.
* mkvmerge: complete rewrite of the progress handling. It's now based upon the
  total size of all source files and the current position within them instead
  of the number of frames/blocks to be processed. This simplifies calculation
  when appending files and fixes rare cases of when progress report was
  obvious wrong (e.g. stuck at 0% right until the end). Fixes #2150 and #2330.
* MKVToolNix GUI: header editor: non-mandatory elements couldn't be removed
  anymore due to a regression while fixing #2320. They can now be removed
  again. Fixes #2322.


# Version 24.0.0 "Beyond The Pale" 2018-06-10

## New features and enhancements

* mkvmerge: MP4 reader: improved the detection of edit lists consisting of two
  identical entries, each spanning the file's duration as given in the movie
  header atom. The second entry is ignored in such cases. See #2306.
* mkvmerge: JSON identification: the "display unit" video track property is
  now reported as `display_unit`. The JSON schema has been bumped to v11 for
  this change.
* mkvmerge, mkvextract: AVC/h.264: empty NALUs will now be removed.
* mkvextract: VobSub extraction: empty SPU packets will now be dropped during
  extraction as other tools such as MP4Box cannot handle them
  correctly. Implements #2293.

## Bug fixes

* mkvmerge: E-AC-3 parser: fixed determining the number of channels for
  streams that contain an AC-3 core with dependent E-AC-3 frames. Fixes #2283.
* mkvmerge: Matroska reader: fixed mkvmerge buffering the whole file if a
  video track is multiplexed that consists of only one or a few frames. Fixes
  #2304.
* mkvmerge: the "display unit" video track property will now be kept if it is
  set in the source file. Fixes #2317.
* MKVToolNix GUI: multiplexer: when scanning playlists, all playlists were
  offered for selection regardless of the value of the "minimum playlist
  duration" setting. Fixes #2299.
* MKVToolNix GUI: multiplexer: deriving track languages from file names: the
  regular sub-expressions for ISO 639-1 codes could match on empty strings,
  too, causing matches in wrong places and hence no language being recognized
  in certain situations. Fixes #2298.
* MKVToolNix GUI: header editor: fixed a crash when saving the file fails
  (e.g. because it isn't writable). Fixes #2319.
* MKVToolNix GUI: header editor: the editor was wrongfully claiming that
  mandatory elements with default values cannot be removed in the "status"
  text. Fixes #2320.
* MKVToolNix GUI: preferences: on macOS & Linux the setting "enable copying
  tracks by their type" wasn't restored on program start. Fixes #2297.

## Other changes

* Niels Lohmann's JSON library: the bundled version has been updated from
  v1.1.0 (git revision 54d3cab) to v3.1.1 (git revision g183390c1).
* pugixml library: the bundled version has been updated from v1.8 to v1.9 (git
  revision e584ea3).


# Version 23.0.0 "The Bride Said No" 2018-05-02

## New features and enhancements

* mkvmerge: input: format detection uses file-extension to improve performance
  and to give preference when several formats match.
* mkvmerge: AV1: added support for reading AV1 video from Open Bitstream Unit
  files.
* mkvmerge: AV1: adjusted the code for the AV1 bitstream format changes made
  up to 2018-05-02 (git revision d14e878).
* mkvmerge: MP4 reader: if a track has an edit list with two identical
  entries, each spanning the file's duration as given in the movie header
  atom, then the second entry will now be ignored. Improves the handling of
  files with bogus data; see #2196 and #2270.
* MKVToolNix GUI: multiplexer: added options to only enable tracks of certain
  types by default. Implements #2271.
* MKVToolNix GUI: multiplexer: added an option to enable dialog normalization
  gain removal by default for all audio tracks for which the operation is
  supported. Implements #2272.
* MKVToolNix GUI: multiplexer: when deriving track languages from the file
  names is active and the file name contains the usual season/episode pattern
  (e.g. "S02E14"), then only the part after the season/episode pattern will be
  used for detecting the language. Part of the improvements for #2267.
* MKVToolNix GUI: multiplexer: the regular expression used for deriving track
  languages from the file names can now be customized in the preferences. Part
  of the improvements for #2267.
* MKVToolNix GUI: multiplexer: the user can now customize the list of track
  languages the GUI recognizes in file names. This list defaults to a handful
  of common languages instead of the full list of supported languages. Part of
  the improvements for #2267.

## Bug fixes

* mkvmerge: MP3 packetizer: removed a memory leak growing linearly with the
  track's size.
* mkvmerge: VobSub packetizer: whenever a VobSub packet doesn't contain a
  duration on the container level, mkvmerge will now set it from the duration
  in the SPU packets. Before it was accidentally setting the SPU-level
  duration to 0 instead. Fixes #2260.
* mkvmerge: track statistics tags: if writing the `Date` element is
  deactivated via `--no-date`, the `_STATISTICS_WRITING_DATE_UTC` isn't
  written either anymore. Fixes #2286.
* mkvmerge, mkvextract, mkvpropedit: removed several small, constant-size
  memory leaks.
* mkvextract: fixed a crash when mkvextract with a non-Matroska file as the
  source file. Fixes #2281.
* MKVToolNix GUI: the central area is now scrollable, allowing the GUI to be
  resized to almost arbitrary sizes. Fixes #2265.
* MKVToolNix GUI: multiplexer: the "copy file title to destination file name"
  functionality will now replace everything in the destination file name up to
  the last period instead of only up to the first period. Fixes #2276.

## Build system changes

* build system: MKVToolNix now requires a compiler that supports the following
  features of the C++14 standard: "user-defined literals for
  `std::string`". For the GNU Compiler Collection (gcc) this means v5.x or
  newer; for clang it means v3.4 or newer.
* Windows: linking against and installing shared version of the libraries with
  MXE is now supported by setting `configure`'s `host` triplet accordingly,
  e.g. `--host=x86_64-w64-mingw32.shared`.

## Other changes

* mkvmerge: AV1: support for AV1 must be activated manually by adding
  `--engage enable_av1` as the AV1 bitstream specification hasn't been
  finalized yet.


# Version 22.0.0 "At The End Of The World" 2018-04-01

## New features and enhancements

* mkvmerge, MKVToolNix GUI multiplexer: AC-3, DTS, TrueHD: added an option for
  removing/minimizing the dialog normalization gain for all supported types of
  the mentioned codecs. Implements #1981.
* mkvmerge: AV1: added support for reading AV1 video from IVF, WebM and
  Matroska files.
* mkvmerge: FLAC: mkvmerge can now ignore ID3 tags in FLAC files which would
  otherwise prevent mkvmerge from detecting the file type. Implements #2243.
* mkvinfo: the size and positions of frames within "SimpleBlock" and
  "BlockGroup" elements are now shown the same way they're shown for other
  elements (by adding the `-v -v` and `-z` options).
* MKVToolNix GUI: multiplexer: added options for deriving the track languages
  from the file name by searching for ISO 639-1/639-2 language codes or
  language names enclosed in non-word, non-space characters (e.g. "…[ger]…"
  for German or "…+en+…" for English). Implements #1808.
* MKVToolNix GUI: info tool: implemented reading all elements in the file
  after the first cluster. Only top-level elements are shown; child elements
  are only loaded on demand. Implements the rest of #2104.
* MKVToolNix GUI: info tool: added a context menu with the option to show a
  hex dump of the element with the bytes making up the EBML ID and the size
  portion highlighted in different colors. In-depth highlighting is done for
  the data in `SimpleBlock` and `Block` elements.
* MKVToolNix GUI: chapter editor: added an option to remove all end timestamps
  to the "additional modifications" dialog. Implements #2231.

## Bug fixes

* mkvmerge: MP4 reader: fixed reading the ESDS audio header atom if it is
  located inside a "wave" atom inside the "stsd" atom.
* mkvmerge: MP4 reader: AAC audio tracks signalling eight channels in the
  track headers but only seven in the codec-specific configuration will be
  treated as having eight channels.
* mkvmerge: MPEG TS reader: fixed wrong handling of the continuity counter for
  TS packets that signal that TS payload is present but where the adaptation
  field spans the whole TS packet.
* mkvmerge: the 'document type version' and 'document type read version'
  header fields are now set depending on which elements are actually written,
  not on which features are active (e.g. if a `SimpleBlock` is never written,
  then the 'read version' won't be set to 2 anymore). Part of the fix for
  #2240.
* mkvmerge: the 'document type version' header field is now set to 4 correctly
  if any of the version 4 Matroska elements is written. Part of the fix for
  #2240.
* mkvinfo: summary mode: the file positions reported for frames in
  `BlockGroup` elements did not take the bytes used for information such as
  timestamp, track number flags or lace sizes into account. They were
  therefore too low.
* mkvpropedit, MKVToolNix GUI header editor: the 'document type version' and
  'document type read version' header fields are now updated if elements
  written by the changes require higher version numbers. Part of the fix for
  #2240.
* mkvpropedit, MKVToolNix GUI header editor: mandatory elements can now be
  deleted if there's a default value for them in the specifications. Fixes
  #2241.
* source code: fixed a compilation error on FreeBSD with clang++ 5.0. Fixes
  #2255.

## Build system changes

* A compilation database (in the form of a file `compile_commands.json`) can
  be built automatically if the variable `BUILD_COMPILATION_DATABASE` is set
  to `yes` (e.g. as `rake BUILD_COMPILATION_DATABASE=yes`).


# Version 21.0.0 "Tardigrades Will Inherit The Earth" 2018-02-24

## New features and enhancements

* mkvmerge: track statistics tags: the `TagDefault` element will not be
  written anymore as it was always set to the default value `1`
  anyway. Implements #2202.
* mkvmerge, MKVToolNix GUI: JSON files can now contain C++-style line comments
  outside of strings (e.g. something like this: `// this is ignored`). Such
  comments, even though not part of the official JSON specifications, are now
  ignored when reading JSON files.
* MKVToolNix GUI: chapter editor: opening a Matroska file without chapters in
  it will now open the file in an empty chapter editor instead of showing an
  error message. Implements #2218.
* MKVToolNix GUI: an "info" tool has been added, replacing the functionality
  of mkvinfo's GUI. The functionality is not on par yet but will be for
  release v22. Implements most of the functionality of #2104.

## Bug fixes

* build system: `configure` was treating `--disable-ubsan` and
  `--disable-addrsan` the same as `--enable-ubsan` and
  `--enable-addrsan`. Fixes #2199.
* build system: an error message is output if a command to execute is not
  found instead of silently failing.
* build system: in addition to looking for the `gettext` C function and
  library, `configure` now also verifies the presence of the `msgfmt` program
  instead of simply relying on it.
* mkvmerge: appending files with additional parts at the same time was broken
  if more than one additional part was appended (e.g. when appending files
  from DVDs with something like `'(' VTS_01_1.VOB VTS_01_2.VOB ')' + '('
  VTS_02_1.VOB VTS_02_2.VOB ')'`). In such a situation the content from files
  `VTS_02_1.VOB` and `VTS_02_2.VOB` where laid out in parallel to the content
  from the earlier files.
* mkvmerge: FLV reader: a single invalid AAC frame was written for AAC audio
  tracks with codec initialization data longer than five bytes.
* mkvmerge: FLV reader: timestamps will be normalized down to 0. Fixes #2220.
* mkvmerge: MP4 reader: if an AAC track doesn't contain an AAC-specific
  decoder configuration in the ESDS portion, then a default decoder
  configuration will be generated based on the track's header data instead of
  skipping the track. Fixes #2221.
* mkvmerge: MP4 reader: fixed reading HEVC/h.265 video tracks if they're
  stored as Annex B byte streams inside MP4. Fixes #2215.
* mkvmerge: Ogg Opus reader: mkvmerge will now emit a warning instead of
  aborting when it encounters an Ogg Opus page with no data in the
  packet. Fixes #2217.
* mkvmerge, mkvextract: Matroska parser: fixed a segmentation fault that
  occurred whenever the first level 1 element after resyncing after an error
  in the file structure isn't a cluster. Fixes #2211.
* mkvmerge, MKVToolNix GUI multiplexer & header editor: fixed a crash during
  file type detection for attachments if MKVToolNix is installed in a path
  with non-ASCII characters (e.g. German Umlauts). Fixes #2212.
* mkvinfo: the `--hex-positions` parameter did nothing in summary mode.
* mkvinfo: Windows: line endings will be written as `\r\n` (carriage return &
  line feed) again instead of just `\n` (line feed).
* mkvpropedit: adding track statistics tags: for tracks with content encoding
  (compression) mkvpropedit is now accounting the uncompressed number of
  bytes, not the encoded (compressed) number of bytes. Fixes #2200.
* MKVToolNix GUI: multiplexer: the subtitle character set can now be set for
  appended subtitle files, too. Fixes #2214.
* MKVToolNix GUI: multiplexer: when appending, all tracks appended to disabled
  tracks will start out disabled, too.

## Build system changes

* mkvinfo: the GUI portion has been removed. mkvinfo is now a pure
  command-line program again.


# Version 20.0.0 "I Am The Sun" 2018-01-15

## Important notes

* Feature removal: several deprecated features have been removed:

  * mkvmerge: the deprecated options `--identify-verbose` (and its counterpart
    `-I`), `--identify-for-gui`, `--identify-for-mmg` and
    `--identification-format verbose-text`
  * all command line tools: support for the deprecated, old, proprietary format
    used for option files
  * all command line tools: support for passing command line options via the
    deprecated environment variables `MKVTOOLNIX_OPTIONS`, `MKVEXTRACT_OPTIONS`,
    `MKVINFO_OPTIONS`, `MKVMERGE_OPTIONS` and `MKVPROPEDIT_OPTIONS`

* mkvinfo: most of its code was re-written in order to lay the groundwork for
  including its functionality in MKVToolNix GUI but with more features than
  the existing mkvinfo GUI. The result is that a lot of its output has been
  changed slightly while keeping the basic layout. Changes include but aren't
  limited to:

  * Several element names are a bit clearer (e.g. `Maximum cache` instead of
    `MaxCache`).
  * All timestamps and durations are now output as nanoseconds in formatted
    form (e.g. `01:23:45.67890123`). All additional formats (e.g. floating
    point numbers output in seconds or milliseconds) were removed.
  * Element names for chapters and tags are now translated if a translation is
    available.
  * Elements located in wrong positions within the Matroska document are
    handled better.

  While mkvinfo's output is mostly kept very stable, it is not designed to be
  parsed by other utilities. Even though I've tried hard to cram all changes
  and cleanups into this version, additional changes may be made in the next
  couple of releases depending on user feedback and bug reports.

## New features and enhancements

* mkvmerge: AVC/h.264 packetizer (framed): access unit delimiter NALUs will
  now be removed. Implements #2173.

## Bug fixes

* mkvmerge: AVC/h.264 parser: when fixing the bitstream timing information
  mkvmerge will now use exact representations of the desired field duration if
  possible. For example, when indicating 50 fields/second `num_units_in_tick`
  is set to 1 and `time_scale` to 50 instead of 5368709 and 268435456. Part of
  the fix for #1673.
* mkvmerge: AVC/h.264 parser: mkvmerge no longer assumes that encountering
  sequence parameter set or picture parameter set NALUs signal the start of a
  new frame. Fixes #2179.
* mkvmerge: AVC/h.264 packetizer (framed): when mkvmerge is told to fix the
  bitstream timing information, it will now update all SPS NALUs, not just the
  ones in the AVCC. Part of the fix for #1673.
* mkvmerge: MPEG TS reader: TS packet payloads will only be treated as PES
  packets if the payload actually starts with a PES start code. The prior
  behavior led to wrong timestamps and potentially broken frame data. Fixes
  #2193.
* mkvmerge: MPEG TS reader: mkvmerge will now drop incomplete PES packets as
  soon as an error is detected in the transport stream instead of passing the
  incomplete frame to the packetizer. An error is assumed either if the
  `transport_error_indicator` flag is set or if the value of the
  `continuity_counter` header field doesn't match the expected value. Fixes
  #2181.
* mkvmerge: Opus: when re-muxing Opus from Matroska mkvmerge will now write
  "block duration" elements for all block groups where a "discard padding" is
  set, too. Fixes #2188.
* mkvmerge: SRT reader: mkvmerge can now handle SRT files with timestamps
  without decimal places (e.g. `00:01:15` instead of `00:01:15.000`).
* mkvmerge: read buffer I/O class: the class could get out of sync regarding
  the file position of the underlying file I/O class causing wrong data to be
  returned on subsequent read operations. One result was that trying to
  identifying MPLS files that refer to very short M2TS files caused mkvmerge
  to segfault.
* mkvmerge: multiplexer core: if there's a gap in audio timestamps, a new
  block group/lace will be started for the first frame after each gap. Before
  the fix the frame after the gap was often stored in the previous block group
  causing the gap to be in the wrong place: at the end of that block
  group. Fixes #1700.
* mkvextract: AVC/h.264: if two consecutive IDR frames with the same
  `idr_pic_id` parameter and no access unit delimiters are found between them,
  mkvextract will insert an access unit delimiter in order to signal the start
  of a new access unit. Fixes #1704.
* MKVToolNix GUI: update check dialog: Markdown links will now be converted to
  clickable links. Fixes #2176.
* build system: fixed a race condition when creating new directories if `rake`
  is run with `-jN` in newer versions of Ruby/`rake`. Fixes #2194.

## Build system changes

* [cmark](https://github.com/commonmark/cmark), the CommonMark parsing and
  rendering library in C, is now required when building the GUIs.


# Version 19.0.0 "Brave Captain" 2017-12-17

## Important notes

* The MKVToolNix project now contains a
  [Code of Conduct](https://mkvtoolnix.download/doc/CODE_OF_CONDUCT.md).
* The MKVToolNix project's source code repository, bug tracker and wiki have
  been moved to [GitLab](https://gitlab.com/mbunkus/mkvtoolnix/).

## New features and enhancements

* mkvmerge: splitting by duration, by timestamps or by timestamp-based parts:
  mkvmerge will now consider the first key frame within 1ms of the requested
  value to be eligible for splitting.
* MKVToolNix GUI: the GUI will now save and restore the widths of columns in
  tree and list views. Implements #2057.
* MKVToolNix GUI: header editor: when closing or reloading a modified file,
  the GUI will now focus the first element that's been modified before asking
  the user for confirmation regarding discarding unsaved changes.

## Bug fixes

* mkvmerge: fixed reading text files encoded in UTF-16 order UTF-32 that have
  different forms of line endings (new lines, carriage returns or a mix of
  both). Fixes #2160.
* mkvmerge: MP4 reader: fixed mkvmerge's interpretation of edit list entries
  with `segment_duration == 0` when there's more than one edit list entry. In
  that case mkvmerge was reading the whole content more than once. Fixes
  #2152.
* mkvmerge, GUI's multiplexer: MIME types: added the `font` top-level media
  types from RFC 8081. This means that the following new MIME types for fonts
  can be used: `font/ttf`, `font/otf`, `font/woff` and `font/woff2`.
* mkvmerge: MPEG transport stream reader: fixed slow speed on Windows due to
  lack of buffering.
* mkvextract: fixed slow track extraction speed on Windows due to lack of
  buffering. Fixes #2166.
* MKVToolNix GUI: multiplexer: changing the "subtitle/chapter character set"
  drop-down was ignored when the selected track was a chapter track. Fixes
  #2165.
* MKVToolNix GUI: multiplexer: once a "subtitle/chapter character set" was set
  for a track it couldn't be changed back to the empty entry (=
  auto-detection) anymore.
* MKVToolNix GUI: header editor: fixed re-translating several displayed
  strings when the GUI language is changed if the language the GUI was started
  with was not English. Fixes #2159.
* MKVToolNix GUI: header editor: whenever a file did not contain a "date"
  element in its segment information section, the GUI would erroneously ask
  the user to confirm discarding unsaved changes when closing or reloading the
  tab. Fixes #2167.
* MKVToolNix GUI: job queue: jobs are now saved when their status changes in
  addition to when the program exits. Fixes #2168.


# Version 18.0.0 "Apricity" 2017-11-18

## New features and enhancements

* build system: when building with clang v3.8.0 or newer, `configure` will no
  longer restrict optimization flags to `-O1` and use `-O3` again (older
  versions of clang suffered from excessive memory usage with higher
  optimization levels).
* build system: when building with mingw 7.2.0 or newer, `configure` will no
  longer restrict optimization flags to `-O2` and use `-O3` again (older
  versions of mingw suffered from bugs such as segmentation faults with higher
  optimization levels).
* build system: stack protection is enabled when building with clang 3.5.0 or
  newer on all platforms.
* mkvmerge: AVC/h.264 & HEVC/h.265 ES parsers: performance improvements by
  copying much less memory around.
* mkvmerge: tags: reintroduced a workaround for non-compliant files with tags
  that do not contain the mandatory `SimpleTag` element. This workaround was
  removed during code refactoring in release v15.0.0.
* GUI: multiplexer: the "AAC is SBR/HE-AAC/AAC+" checkbox in the "audio
  properties" section will be disabled if the functionality is not implemented
  for the selected track's codec & container.
* GUI: multiplexer: the "reduce to core" checkbox in the "audio properties"
  section will be disabled if the functionality is not implemented for the
  selected track's codec. See #2134.

## Bug fixes

* mkvmerge: AAC ADTS parser: fixed interpretation of the
  `channel_configuration` header element for ADTS files that do not contain a
  program configuration element: value 7 means 7.1 channels. Fixes #2151.
* mkvmerge: Matroska identification: the `date_local` and `date_utc`
  attributes will only be output if the identified Matroska file actually
  contains the "date" header field.
* mkvmerge: WebVTT: mkvmerge did not recognize timestamp lines if the hours
  components were absent. Fixes #2139.
* mkvpropedit, GUI's header editor: the `date` header field won't be added
  automatically anymore whenever the segment info section is edited and the
  `date` element is either deleted or not present in the first place. Fixes
  #2143.


# Version 17.0.0 "Be Ur Friend" 2017-10-14

## Important notes

* The word "timecode" has been changed to "timestamp" everywhere it was
  used in MKVToolNix. This affects program output (including mkvinfo's), GUI
  controls, command line parameters (e.g. `mkvmerge --timestamp-scale …`) and
  file formats. All programs remain backwards compatible insofar as they still
  accept "timecode" in all those places (e.g. `mkvmerge --timecode-scale …`).

  The reason for the change is wrong usage. What both the Matroska specification
  and MKVToolNix used "timecode" for is normally called a "timestamp" in audio &
  video domains. A "timecode" on the other hand has a specific meaning. As the
  Matroska specification is moving towards implementing real timecodes, it will
  also move towards correcting the verbiage. MKVToolNix is following this
  change.

* mkvextract's command line interface has been changed to allow extraction of
  multiple items at the same time. The first argument must now be the source
  file's name. All following arguments either set the mode (e.g. `tracks`) or
  specify what to extract in the currently active mode.

  Those items that were written to the standard output (chapters, tags and cue
  sheets) are now always written to files instead. Therefore the respective
  modes require an output file name.

  For example, extracting two tracks, the chapters and the tags can be done
  with the following command:

  `mkvextract input.mkv tracks 0:video.h265 1:audio.aac chapters chapters.xml tags tags.xml`

  The old interface (specifying the mode first and the source file name
  second) remains working and supported. However, it is now deprecated and
  will be removed at the end of 2018.

## New features and enhancements

* mkvmerge: AC-3: during identification regular AC-3 and E-AC-3 tracks will
  now be identified differently for most container formats (exception: AVI,
  Real Media, Ogg/OGM). The codec will be reported as `AC-3` for regular AC-3
  and as `E-AC-3` for E-AC-3 tracks instead of the combined `AC-3/E-AC-3`.
* mkvextract: the command line interface has been changed to allow extraction
  of multiple items at the same time. See section "Important notes" for details.

## Bug fixes

* mkvmerge: AAC ADTS parser: mkvmerge will now parse the
  `program_config_element` if it is located at the start of an AAC frame in
  order to determine the actual number of channels. This overrides invalid
  channel configurations in the ADTS headers, for example. Fixes #2107.
* mkvmerge: fixed AC-3 being misdetected as encrypted MPEG program streams
  under certain conditions.
* mkvmerge: Dirac: under certain conditions (e.g. only muxing a single Dirac
  track without any other tracks) mkvmerge was always setting the pixel width
  & height to 123. The frame rate was wrong, too.
* mkvmerge: E-AC-3 in Matroska: if AC-3 cores and their corresponding E-AC-3
  extension are located in two different Matroska blocks, then mkvmerge will
  now re-assemble them into a single block and only use the first block's
  timestamp.
* mkvmerge: SRT reader: fixed calculating the duration of entries starting
  with at a negative timestamp.
* mkvmerge: VC-1: under certain conditions (e.g. only muxing a single VC-1
  track without any other tracks) mkvmerge was always setting the pixel width
  & height to 123. The frame rate was wrong, too. Fixes #2113.
* mkvmerge: command line options: an error message will be output if the
  single-value-form of the `--sync` option is used and it isn't a number
  (e.g. `--sync 0:asd`). Fixes #2121.
* mkvpropedit, GUI's header editor: both programs will now show proper error
  messages instead of crashing when certain kinds of data corruption is found
  when reading a file. Fixes #2115.


# Version 16.0.0 "Protest" 2017-09-30

## New features and enhancements

* mkvmerge: MP4 reader: added support for Vorbis. Implements #2093.

## Bug fixes

* configure: the checks for libEBML and libMatroska have been fixed to require
  libEBML 1.3.5 and libMatroska 1.4.7 as intended.
* mkvmerge: AAC reader: mkvmerge will now emit an error message for AAC files
  whose header fields imply a sampling frequency or number of channels
  of 0. See #2107.
* mkvmerge: AVC/h.264 ES parser: fixed the calculation of reference
  information for P and B frames. This also fixes some P frames being marked
  as B frames and vice versa.
* mkvmerge: AVC/h.264 ES parser: only non-key frames that have the NALU header
  field `nal_ref_idc` set to 0 will be marked as "discardable" in
  `SimpleBlock` elements. Other half of the fix for #2047.
* mkvmerge: HEVC/h.265: the generation of the HEVCC structure stored in
  `CodecPrivate` was wrong in two places: 1. the position of the number of
  sub-layers was swapped with reserved bits and 2. the VPS/SPS/PPS/SEI lists
  did not start with a reserved 1 bit.
* mkvmerge: output: the `doc type version` will be set at least to 2 if
  certain elements are written (`CodecState`, `CueCodecState`,
  `FlagInterlaced`).
* mkvmerge: output: the track header attributes `MinCache` and `MaxCache` will not be
  written anymore. Fixes #2079.
* mkvmerge: Matroska reader: the "key" and "discardable" flags of SimpleBlock
  elements will be kept as they are. Partial fix for #2047.
* mkvmerge: Matroska reader: if present in the file, the "white colour
  coordinate x" track header attribute was written to both "white colour
  coordinate x" and "white colour coordinate y" in the output file.
* mkvmerge: Opus output: mkvmerge will now put all frames with discard padding
  into their own block group. Fixes #2100.
* MKVToolNix GUI: header editor: removed the check for external modification
  when saving the file. Fixes #2097.
* MKVToolNix GUI: job queue: fixed calculation of total progress when
  automatic removal of completed is enabled. Fixes #2105.

## Build system changes

* libEBML v1.3.5 and libMatroska v1.4.8 are now required. In fact v15.0.0
  already requires libEBML v1.3.5 and libMatroska v1.4.7 but did not include
  proper version checks for them (nor was there a NEWS.md entry for the new
  libMatroska requirement). New is the requirement for libMatroska v1.4.8 due
  to it fixing writing block groups for tracks with the track number 128 (see
  #2103).


# Version 15.0.0 "Duel with the Devil" 2017-08-19

## Important notes

* mkvmerge, mkvpropedit, GUI's header and chapter editors: the programs will
  no longer add most missing Matroska elements that are mandatory but have a
  default value in the Matroska specification (e.g. the `TagLanguage` element
  with a value of `und` if it isn't present in its `SimpleTag` parent). Due to
  this change libEBML v1.3.5 is now required.

## New features and enhancements

* MKVToolNix GUI: multiplex tool: added a new entry to the "source files"
  context menu labeled "Set destination file name from selected file's
  name". It will force the GUI to consider the selected file to be the
  reference for automatically setting the file name, no matter which file was
  originally added as the first file. It will also force setting the
  destination file name once if automatic destination file name generation is
  turned off in the preferences. Implements part of #2058.
* MKVToolNix GUI: multiplex tool: added an option in the preferences on
  "Multiplexer" → "Output" labeled "Only use the first source file that
  contains a video track". If enabled, only source files containing video
  tracks will be used for setting the destination file name. Other files that
  are added are ignore. Implements the rest of #2058.
* MKVToolNix GUI: header editor: added support for editing the video colour
  attributes. Implements the second half of #2038.
* MKVToolNix GUI: header editor: added support for the "video projection"
  track header attributes. Part of the implementation of #2064.
* MKVToolNix GUI: job queue: selected jobs can now be move up and down by
  pressing the `Ctrl+Up` and `Ctrl+Down` keys. Additionally, push buttons to
  move them up & down are shown if the corresponding option is enabled in the
  preferences. Implements #2060.
* mkvmerge: added support for the "video projection" track header
  attributes. Part of the implementation of #2064.
* mkvinfo: added support for the "video projection" track header
  attributes. Part of the implementation of #2064.
* mkvpropedit: added support for editing the video colour
  attributes. Implements one half of #2038.
* mkvpropedit: added support for the "video projection" track header
  attributes. Part of the implementation of #2064.

## Bug fixes

* all: selecting the program's language (e.g. via the `--ui-language`
  command-line option or via the GUI's preferences) did not work on Linux &
  Unix if the `LANGUAGE` environment variable was set and didn't include the
  desired language. Fixes #2070.
* MKVToolNix GUI: removed the keyboard shortcuts for switching between the
  different tools (e.g. `Ctrl+Alt+1` for the multiplexer). They overlapped
  with basic functionality on keyboards that use an `AltGr` key, e.g. German
  ones, where `AltGr+7` emits `{`. As `AltGr+key` is implemented as
  `Ctrl+Alt+key` under the hood, this means that `AltGr+7` is really
  `Ctrl+Alt+7` which the GUI now took to mean "switch to the job queue"
  instead of "insert `{`". Fixes #2056.
* MKVToolNix GUI: header editor: after saving the file the GUI wasn't updating
  its internal file modification timestamp. That lead to the GUI wrongfully
  claiming that the file had been modified externally when the user wanted to
  save the file once more, requiring a reload of the file losing all
  modifications made since saving the first time.
* mkvmerge: DTS handling: some source files provide timestamps for audio
  tracks only once every `n` audio frames. In such situations mkvmerge was
  buffering too much data resulting in a single gap in the timestamps of one
  frame duration after frame number `n - 1` (the second audio timestamp read
  from the source file was used one output frame too early). Fixes #2071.
* mkvinfo: fixed a null pointer dereference if an `EbmlBinary` element's data
  pointer is a null pointer. Fixes #2072.

## Build system changes

* configure: added option `--disable-update-check`. If given, the code
  checking online for available updates will be disabled. The update check is
  enabled and included in the GUI by default.
* libEBML v1.3.5 is now required.

## Other changes

* mkvmerge: the option `--colour-matrix` has been renamed to
  `--colour-matrix-coefficients` in order to match the specification more
  closely. The old option name will continue to be recognized as well.


# Version 14.0.0 "Flow" 2017-07-23

## New features and enhancements

* mkvmerge: AAC: implemented support for AAC with 960 samples per
  frame. Implements #2031.
* mkvmerge: identification: if the encoding/character set of a text subtitle
  track is known (e.g. because a byte order mark is present in the file), then
  it will be output during identification as the `encoding`
  property. Implements mkvmerge's part of #2053.
* mkvmerge: WAV reader: added support for Wave64 files. Implements #2042.
* mkvmerge, mkvpropedit, MKVToolNix GUI (chapter editor): added support for
  chapters in WebM files that is spec-compliant by removing all tag elements
  not supported by the WebM spec. Implements #2002.
* mkvpropedit: added support for tags in WebM files that is spec-compliant by
  removing all tag elements not supported by the WebM spec.
* MKVToolNix GUI: multiplexer: if the encoding/character set of a subtitle
  track cannot be changed, the GUI will deactivate the "subtitle character
  set" drop-down box and ignore changes to it when multiple tracks are
  selected. Additionally, if the track's encoding is known and cannot be
  changed (e.g. due to a byte order mark in the file), that encoding will be
  selected in the drop-down box automatically. Both changes signal to the user
  that she doesn't have to take care of the encoding herself. Implements the
  GUI's part of
  #2053.
* MKVToolNix GUI: chapter editor: added a function to the "additional
  modifications" dialog for calculating and setting the end
  timestamps. Implements #1887.
* MKVToolNix GUI: changed the shortcuts for switching between the various
  tools from `Alt+number` (e.g. `Alt+1` for the multiplexer tool) to
  `Ctrl+Alt+number` in order to avoid clashing with Windows' input method for
  arbitrary characters (pressing and holding `Alt` and typing the codepoint on
  the number pad). Implements #2034.
* MKVToolNix GUI: added a "Window" menu and entries with shortcuts for
  selecting the next (`Ctrl+F6`) respectively previous tab (`Ctrl+Shift+F6`)
  in the current tool. Implements #1972, #2032.
* MKVToolNix GUI: on Windows the GUI will now determine the default font to
  use by querying Windows for the default UI/message box font instead of using
  the hardcoded `Segoe UI`. This might fix issues such as #2003 (unverified).
* translations: added a Romanian translation of the programs by Daniel (see
  AUTHORS).


## Bug fixes

* mkvmerge: AVC/h.264 parser: fixed wrong frame order & timestamp calculation
  in certain situations when SPS (sequence parameter sets) or PPS (picture
  parameter sets) change mid-stream. Fixes #2028.
* mkvmerge: HEVC/h.265 parser: fixed wrong frame order & timestamp calculation
  in certain situations when SPS (sequence parameter sets) or PPS (picture
  parameter sets) change mid-stream. This is the HEVC/h.265 equivalent of
  #2028.
* mkvmerge: MPEG-1/-2 video: the "remove stuffing bytes" feature introduced in
  v5.8.0 (feature request #734) was broken. In a lot of situations it did not
  detect the end of a slice correctly and removed 0 bytes that were actually
  part of the slice structure. Often there were no visual problems as decoders
  were able to ignore such errors, but in other cases there are visual
  artifacts upon decoding. As detecting the slice end properly requires
  parsing the whole slice structure, this feature has been removed
  again. Fixes #2045.
* mkvmerge: MPEG PS reader: fixed mkvmerge trying to handle an "end" code the
  same way as a "program stream map" code.
* mkvmerge: MPEG TS reader: mkvmerge won't emit warnings if the system's
  `iconv` library doesn't support the ISO 6937 character set. Fixes #2023.
* mkvmerge: when appending fails the error message details (e.g. "the number
  of channels differs: 1 and 2") were often not output. Fixes #2046.
* MKVToolNix GUI: multiplex tool: implemented a workaround for a crash that
  could occur during drag & drop if at least one of the columns is
  hidden. Fixes #2009.
* MKVToolNix GUI: multiplex tool: appended tracks can no longer be enabled
  (selected for multiplexing) if the track they're going to be appended to is
  not enabled. Fixes #2039.
* MKVToolNix GUI: multiplex tool: if the GUI is set to ensure unique output
  file names, it will now verify that right before starting to
  multiplex/adding the job to the queue, too. Fixes #2052.
* MKVToolNix GUI: fixed the total progress reverting to 0% instead of staying
  at 100% when all jobs have finished. This was introduced by the attempt at
  fixing the computation of the value of total progress bar for multiple jobs
  running. Fixes #2005.
* configure: fixed DocBook detection if `/bin/sh` is `dash`. Patch by Steve
  Dibb. Fixes #2054.

## Build system changes

* Boost: the minimum required version has been bumped to 1.49.0. Earlier
  releases fail to build on my current systems and will therefore not be
  supported anymore.
* configure: when looking for the "nlohnmann JSON" include files configure
  will now try the path "nlohmann/json.hpp" first, "json.hpp" second (only
  "json.hpp" was tried before). If neither is found, the copy included in the
  MKVToolNix sources will be used. Fixes #2048.


# Version 13.0.0 "The Juggler" 2017-06-25

## New features and enhancements

* mkvmerge: MPEG TS reader: information about multiple programs will be output
  as container properties during verbose/JSON identification. See #1990 for
  the use case.
* MKVToolNix GUI: multiplex tool: added a column "program" to the tracks
  list. Certain container types such as MPEG transport streams can contain
  multiple programs. The new column will contain the service name (think TV
  station names such as "arte HD") for such streams. Implements the GUI part
  of #1990.
* MKVToolNix GUI: multiplex tool: the dialog asking the user what to do with
  dropped files (add to current settings, add to new settings etc.) now
  remembers the previous decision and defaults to it the next time it's
  shown. Implements #1997.
* MKVToolNix GUI: tabs can now be closed by pressing the middle mouse
  button. Implements #1998.

## Bug fixes

* mkvmerge: MP4 reader: MPEG-1/2 video read from MP4 files was written with an
  invalid codec ID (e.g. `V_MPEG7`) in certain cases. Fixes #1995.
* mkvmerge: MPEG PS reader: made the file type detection less strict so that
  garbage at the start of the file doesn't prevent detection. Fixes #2008.
* mkvmerge: MPEG PS reader: (E-)AC-3 tracks were not detected if the very
  first packet for that track didn't contain a full (E-)AC-3 frame. Fixes
  #2016.
* mkvmerge: MPEG TS reader: fixed mkvmerge not detecting all tracks in MPEG
  transport streams containing multiple programs. Fixes one part of #1990.
* mkvmerge: MPEG TS reader: fixed track content being broken for some tracks
  read from MPEG transport streams containing multiple programs. Fixes another
  part of #1990.
* mkvmerge: JSON identification: the `stream_id` and `sub_stream_id` track
  properties were output as hexadecimal strings instead of unsigned
  integers. As the `ts_pid` track property was only used for MPEG transport
  streams, its value is now output as `stream_id` instead, and the `ts_pid`
  property has been removed. The JSON schema version has been bumped to 8 due
  to this change.
* mkvmerge: fixed a crash when appending video tracks where one track has a
  CodecPrivate member and the other one doesn't.
* mkvmerge: track statistics tags: the `NUMBER_OF_BYTES` tag is supposed to
  contain the number of bytes in a track before any of the content encoding
  schemes such as lossless compression is applied; however, mkvmerge was
  wrongfully using the number of bytes after the schemes had been
  applied. Fixes #2022.
* mkvmerge: CLPI & MPLS parsers: MPLS and CLPI files with version number
  `0300` as used on Ultra HD Blu-ray Discs are now accepted as well. Fixes
  #2010.
* mkvpropedit: fixed a crash when the selector used for `--tags` is invalid.
* MKVToolNix GUI: fixed computation of value of total progress bar for multiple
  jobs running. Fixes #2005.
* MKVToolNix GUI: multiplexer, adding new attachments: when the GUI checks if
  there's an attachment with the same name it will now disregard disabled
  attached files. Fixes #2001.
* Debian/Ubuntu packaging: during a `dpkg-buildpackage` run the test suite was
  failing when a non-English locale was active and MKVToolNix packages had
  already been installed. Fixes #2011.


# Version 12.0.0 "Trust / Lust" 2017-05-20

## New features and enhancements

* MKVToolNix GUI: the key combination Ctrl+Shift+Space will now toggle the
  selection of the current item in all tree views where multiple selections
  are allowed. Implements #1983.
* MKVToolNix GUI: chapter editor: added the extension `*.cue` (for cue sheet
  files) to the "open chapter file" dialog.
* mkvmerge: cue sheet parser: if the cue sheet contains a non-empty `TITLE`
  entry and if no other segment title has been set yet, then the segment title
  will be set to the cue sheet's `TITLE` value. Implements #1977.
* mkvmerge, MKVToolNix GUI (multiplexer): added an option `--no-date` that
  prevents the "date" field from being written to the segment information
  headers. Implements one half of #1964.
* mkvpropedit, MKVToolNix GUI: header editor: added support for editing the
  "date" segment information field. Implements the other half of #1964.

## Bug fixes

* MKVToolNix GUI: preferences → job actions, type "play audio file": the GUI
  will no longer clear the audio file name input if the user aborts the audio
  file selection dialog.
* MKVToolNix GUI: preferences → job actions, type "play audio file", on
  Windows: the default "play audio" action was pointing to the wrong
  directory. Existing configurations with such a wrong path will be fixed
  automatically upon starting the GUI. Fixes #1956.
* mkvmerge: HEVC/h.265 parser: fixed the superfluous copying of the
  `bitstream_restriction_flag` and its dependent flags in the VUI parameters
  of the sequence parameter sets if the timing information is present,
  too. This fixes #1924 properly, and it also fixes #1958.
* mkvmerge: MPEG TS reader, AAC parser: the MPEG TS reader will now force the
  AAC parser to use the multiplex mode that the MPEG TS reader has detected
  (e.g. LOAS/LATM). This prevents the AAC packetizer from mis-detecting it in
  its own attempt to identify the mode. Fixes #1957.
* mkvmerge: MPEG TS reader: valid MPEG transport streams that start with an
  h.264/h.265 start code (e.g. a file created by cutting at an arbitrary
  position) were not recognized as a supported file type.
* mkvmerge: MPEG TS reader: fixed a potential read access from invalid memory
  addresses in the code parsing the program map table (PMT).
* mkvmerge: MPEG TS reader: if packets are encountered that belong to a PID
  not listed in the program map table (PMT), mkvmerge will attempt to
  determine their type and codec from the content. This supported content
  types are AAC (ADTS only) and AC-3. Fixes #1980.
* mkvmerge: MP4 reader: fixed finding and parsing the `colr` atom if there are
  more than one video extension atoms and the `colr` atom is not the first
  one.
* mkvmerge: MP4 reader: the `nclx` colour type of the `colr` atom is now
  recognized, too (as defined by ISO/IEC 14496-12, "ISO base media format").
* configure: fixed configure aborting if a `moc`, `uic`, `rcc` or `qmake`
  binary is found, but the binary's version is too old. Fixes #1979.


# Version 11.0.0 "Alive" 2017-04-22

## New features and enhancements

* mkvmerge: FLAC reader: added support for handling embedded pictures as
  attachments. Implements #1942.
* mkvmerge: MP4 reader: merged pull request #1804 adding support for parsing
  the "COLR" atom and including its values as track headers.
* MKVToolNix GUI: watch jobs: the user can now have the GUI execute an action
  once as soon as the current job or the whole queue finishes. The actions are
  the same ones that can be configured to be run automatically after job or
  queue completion.
* MKVToolNix GUI: implemented several built-in actions that can be executed
  either on special events or once via the "watch jobs" tool. These are:
  playing an audio file (implemented for all operating systems); hibernating,
  sleeping and shutting down the computer (only implemented for Windows and
  for Linux systems using systemd).
* MKVToolNix GUI: multiplex tool: added a new option for what to do after
  starting to multiplex/adding to the job queue: "close current settings" will
  close the current multiplex settings without opening new ones.

## Bug fixes

* mkvmerge: AAC parser: fixed mis-detection of certain data as valid ADTS AAC
  headers resulting in memory allocation failures. Fixes #1941.
* mkvmerge: AVC/h.264 parser: mkvmerge will now ignore bogus timing
  information in the sequence parameter sets (values indicating more than
  100000 progressive frames per second). Fixes #1946.
* mkvmerge: AVC/h.264 & HEVC/h.265 parsers: all trailing zero bytes will now
  be removed from NALUs. Fixes #1952.
* mkvmerge: HEVC/h.265 parser: fixed copying the `bitstream_restriction_flag`
  and all dependent fields in the VUI parameters of the sequence parameter
  sets. Fixes #1924.
* mkvmerge: HEVC/h.265 parser: fixed the calculation of the number of
  parameter set arrays in the HEVCC data structure stored in
  CodecPrivate. Fixes the video-related part of #1938.
* mkvmerge: HEVC/h.265 parser: fixed writing superfluous and uninitialized
  bytes at the end of the HEVCC data structure stored in CodecPrivate. Another
  fix for the video-related part of #1938.
* mkvmerge: HEVC/h.265 parser: fixed the assumption that the HEVCC data
  structure always includes arrays for all parameter set types (VPS, SPS, PPS
  and SEI), and that the order is always VPS → SPS → PPS → SEI. Instead now
  only the arrays actually present are parsed, and they can be in any order.
  This fixes mkvinfo's output for Matroska files created from files such as
  the one from #1938.
* mkvmerge: AVC/h.264 packetizer: when reading a framed track (e.g. from
  Matroska or MP4 files), specifying a default duration as fields (e.g. `50i`)
  would result in double the actual duration for each frame and the track's
  default duration header field. Fixes #1916.
* mkvmerge: Matroska reader: invalid track language elements are now treated as
  if they were set to `und` = "undetermined". See #1929 for context.
* mkvmerge: MPEG TS reader, AAC: mkvmerge will now require five consecutive
  AAC headers with identical parameters before track type determination is
  considered valid. This avoids false positives and consequently wrong track
  parameters. Fixes the audio-related part of #1938.
* mkvmerge: fixed an endless loop in certain circumstances when splitting by
  `parts` or `parts-frames` and the start of the file is discarded. Fixes
  #1944.
* MKVToolNix GUI: multiplexer tool: the "show command line" dialog will no
  longer include the mkvmerge executable's location as the first argument for
  the two "MKVToolNix option files" escape modes. Fixes #1949.
* MKVToolNix GUI, header editor: empty track language elements are now treated
  the same as those set to invalid ISO 639-2 codes: as if they were set to
  `und` = "undetermined". See #1929 for context.

## Build system changes

* bug fix: configure now looks for the `strings` binary by using the
  `AC_CHECK_TOOL()` autoconf macro. That way it will be found in multiarch
  setups, too. Fixes #1923.
* bug fix: the environment variable USER_CXXFLAGS was accidentally removed
  from the compiler flags in release 9.8.0. It's been re-added. Fixes #1925.
* The `.desktop` files have been renamed to
  `org.bunkus.mkvtoolnix-gui.desktop` and `org.bunkus.mkvinfo.desktop`. This
  allows Wayland compositors to associate the correct icons with running
  applications for e.g. task switchers. Fixes #1948.
* Qt's multimedia component is required for compilation of the GUIs since
  version 11.0.0.


# Version 10.0.0 "To Drown In You" 2017-03-25

## New features and enhancements

* mkvmerge: AVC/h.264 parser: mkvmerge will now drop all frames before the
  first key frame as they cannot be decoded properly anyway. See #1908.
* mkvmerge: HEVC/h.265 parser: mkvmerge will now drop all frames before the
  first key frame as they cannot be decoded properly anyway. See #1908.
* mkvmerge: HEVC/h.265 parser: added a workaround for invalid values for the
  "default display window" in the VUI parameters of sequence parameter
  sets. Fixes #1907.

## Bug fixes

* mkvmerge: MP4 reader: fixed track offsets being wrong in certain situations
  regarding the presence or absence of edit lists ('elst' atoms) & composition
  timestamps ('ctts' atoms). Fixes #1889.
* mkvmerge: MP4 reader: offsets in "ctts" are now always treated as signed
  integers, even with version 0 atoms.
* mkvinfo: the timestamps of SimpleBlocks with negative timestamps are now
  shown correctly.
* mkvmerge: Matroska reader: fixed handling BlockGroups and SimpleBlocks with
  negative timestamps.
* mkvmerge: MP3 packetizer: the MP3 packetizer will no longer drop timestamps
  from source containers if they go backwards. This keeps A/V in sync for
  files where the source was in sync even though their timestamps aren't
  monotonic increasing. Fixes #1909.
* mkvmerge: AVC/h.264 parser: mkvmerge will now drop timestamps from the
  source container if no frame is emitted for that timestamp. Fixes #1908.
* mkvmerge: HEVC/h.265 parser: mkvmerge will now drop timestamps from the
  source container if no frame is emitted for that timestamp. Fixes the HEVC
  equivalent of the problem with AVC described in #1908.
* mkvextract: SSA/ASS: fixed extraction when the "Format" line in the
  "[Events]" section contains less fields than the default for SSA/ASS would
  indicate. Fixes #1913.


# Version 9.9.0 "Pick Up" 2017-02-19

## New features and enhancements

* GUI: chapter editor: added a character set selection in the preferences for
  text files. If a character set is selected there, it will be used instead of
  asking the user when opening text chapter files. Implements #1874.
* GUI: multiplexer: added a column "character set" to the "tracks, chapters
  and tags" list view showing the currently selected character set for that
  track. Implements #1873.
* mkvmerge: added an --engage option "all_i_slices_are_key_frames" for
  treating all I slices of an h.264/AVC stream as key frames in pathological
  streams that lack real key frames. Implements #1876.
* GUI: running programs after jobs: added a new variable
  <MTX_INSTALLATION_DIRECTORY> for the directory the MKVToolNix GUI executable
  is located in.
* mkvmerge: DVB subtitle tracks whose CodecPrivate data is only four bytes
  long will now be fixed up to the proper five bytes by adding the subtitling
  type byte.
* mkvmerge: MP4 reader: "ctts" version 1 atoms are now supported.

## Bug fixes

* mkvmerge: AC-3 handling: some source files provide timestamps for audio
  tracks only once every n audio frames. In such situations mkvmerge was
  buffering too much data resulting in a single gap in the timestamps of one
  frame duration after frame number n - 1 (the second audio timestamp read
  from the source file was used one output frame too early). Fixes #1864.
* mkvmerge: MP4 reader: mkvmerge was only reading a small part of MP4 DASH
  files where the first "moov" "mdat" atoms occur before the first "moof"
  atom. This is part of the fix for #1867.
* mkvmerge: MP4 reader: edit list ("edts" atoms) that are part of the "moof"
  atoms used in MP4 DASH files weren't parsed. Instead the edit lists from the
  main track headers inside the "moov" atom were used. This is part of the fix
  for #1867.
* mkvmerge: MP4 reader: when an MP4 DASH file contained both normal chunk
  offset table ("stco"/"co64" atoms) in their regular "moov" atoms, a
  sample-to-chunk table ("stsc" atom) whose last entry had a "samples per
  chunk" count greater than 1 and DASH "trun" atoms, then mkvmerge was
  calculating wrong positions the frame content. This is part of the fix for
  #1867.
* mkvmerge: MP4 reader: mkvmerge couldn't deal with the key frame index table
  having duplicate entries. The result was that only key frames up to and
  including the first duplicate entry were marked as key frames in the output
  file. All other frames weren't, even though some of them were referenced
  from the key frame table after the first duplicate entry. This is part of
  the fix for #1867.
* mkvmerge: MP4 reader: when an MP4 file contained more than one copy of the
  "moov" atom (the track headers etc.), mkvmerge was parsing them all adding
  tracks multiple times. Fix for #1877.
* mkvmerge: MP4 reader: fixed an integer overflow during the timestamp
  calculation leading to files with wrong timestamps. Such files could not be
  played back properly by most players. Fixes #1883.
* mkvmerge: MPEG TS reader: if the PMT lists a DVBSUB track, mkvmerge will now
  recognize it without having to find a packet for it within the probed range.
* mkvmerge: splitting by parts (both the "timestamps" and the "frames"
  variants): fixed the calculation of track statistics tags. When calculating
  the duration the skipped portions weren't taken into account leading to a
  too-high duration. As a consequence the BPS tag (bits per second) was wrong,
  too. Fixes #1885.
* mkvmerge: reading files with DVB/HDMV TextSV subtitle tracks with invalid
  CodecPrivate caused mkvmerge to abort with an error from boost::format about
  the format string not having enough arguments. Fixes #1894.
* mkvmerge: fixed misdetection of certain AC-3 files as MP3 files which led to
  an error message that "the demultiplexer could not be initialized".
* mkvmerge: fixed huge memory consumption when appending big Matroska files
  with sparse tracks (e.g. forced subtitle tracks). The Matroska reader will
  now queue at most 128 MB of data. Fixes #1893.
* mkvmerge: MP4 reader: the timestamps of all multiplexed tracks will now be
  0-based properly.
* mkvmerge: MP4 reader: the DTS-to-PTS offsets given by the "ctts" atoms are
  now applied for all tracks containing a "ctts" atom, not just h.264 & h.265
  tracks.

## Build system changes

* Up to and including release 9.8.0 the man pages and their translations came
  pre-built and bundled with the source code. Those pre-built files have now
  been removed and must be built during the build process. Therefore the tool
  "xsltproc" and the DocBook XSL stylesheets for man pages are now required
  dependencies. Additionally the tool "po4a" must be installed for the
  translated man pages to be built and installed, though this is optional.

  In order to facilitate finding the new requirements new options have been
  added to confiure: "--with-xsltproc=prog", "--with-docbook-xsl-root=dir",
  "--with-po4a=prog" and "--with-po4a-translate=prog.
* pugixml detection will be attempted via "pkg-config" first. If that fails,
  "configure" will fall back to the previous method of trying just to compile
  and link a test program with the standard include and library locations.
  Implements #1891.


# Version 9.8.0 "Kuglblids" 2017-01-22

## Important notes

* build system: the included version of the "drake" build tool has been
  removed. Since Ruby 2.1 rake has supported parallel builds, too. The
  MKVToolNix build system has been adjusted to enable parallel builds by
  default.

## New features and enhancements

* mkvmerge: VobSub in Matroska: mkvmerge will now create and use a default
  index for VobSub tracks read from Matroska files that are missing their
  CodecPrivate element (which normally contains said index). Implements #1854.
* GUI: added checks for several common problems with the installation. These
  checks will be executed when the GUI starts, and any problems will be
  reported to the user.
* mkvmerge: added the ISO 639-2 language codes "qaa" and "qad" (both are
  titled "reserved for local use") as both are used often in France. See #1848
  for more information.
* mkvmerge: the JSON identification result now includes a track's codec delay
  if set (only for Matroska source files). The JSON schema version has been
  bumped to 6.
* mkvmerge: MPEG TS: added a workaround for files where the subtitle packets
  are multiplexed properly, but where their timestamps are way off from the
  audio and video timestamps. Implements #1841.
* mkvmerge: added support for Digital Video Broadcasting (DVB) subtitles
  (CodecID `S_DVBSUB`). They can be read from MPEG transport streams and from
  Matroska files. Implements #1843.

## Bug fixes

* mkvmerge: MP4 reader: when an MP4 file contained fewer entries for
  timestamps than frames (which they never should), mkvmerge would use 0 as
  the timestamp for all the other frames. This resulted in effects such as the
  last frame of an output file having a timestamp of 0 and in split files
  having a much longer duration than they should have. Fixes #1847.
* GUI: the cache cleanup process that's run automatically when the GUI starts
  no longer blocks file identification until it is finished. Additionally the
  process will only be run once per release of MKVToolNix. Fixes #1860.
* GUI: certain failures during file identification that can be traced to
  broken installations (e.g. mkvmerge being too old) won't be stored in the
  cache anymore. Without this fix the GUI would still use the cached failed
  identification result even though the underlying might have already been
  fixed.
* mkvmerge: fixed that the error message "not enough space on disk" was shown
  twice on some operating systems. Fixes #1850.
* mkvmerge, Matroska: if a codec delay is set for a track in the input file,
  it is kept. Fixes #1849.
* GUI: multiplexer: changing default values in the preferences (e.g. the
  default track language to set) did not affect files whose identification
  results had already been cached.
* mkvmerge, MP4: fixed detection of MP3 audio when the object type ID in the
  ESDS signals MP2 and the track headers have invalid values for number of
  channels or sampling frequency. Fixes #1844.

## Build system changes

* nlohman json-cpp: configure now looks for a system-wide installed version of
  the nlohmann json-cpp header-only library. If one is found, it is used;
  otherwise the included version will be used. Implements #1858.
* If MKVToolNix is built with rake v10.0.0 or newer, its "multitask" feature
  will be turned on allowing automatic parallel builds.
* CURL is no longer used by MKVToolNix and is therefore not required
  for building anymore.

## Other changes

* GUI: the update check now uses Qt's networking classes instead of CURL.
* The command line option "--check-for-updates" has been removed, even
  though the deprecation warning in release 9.7.0 stated that it would
  be removed in 2018.


# Version 9.7.1 "Pandemonium" 2016-12-27

## Bug fixes

* MKVToolNix GUI: multiplex tool bug fix: under certain circumstances the GUI was
  creating invalid JSON files when starting to multiplex resulting in an error
  message ("JSON option files must contain a JSON array consisting solely of JSON
  strings").


# Version 9.7.0 "Numbers" 2016-12-27

## Important notes

* Deprecation warning: Several options and features are now deprecated and will be
  removed at the start of 2018. These are: - mkvmerge: the options
  "--identify-verbose", "--identify-for-gui", "--identify-for-mmg" and
  "--identification-format verbose". Please convert existing users of these
  interfaces to use mkvmerge's JSON identification output which can be invoked with
  "--identification-format json --identify …". - all command line tools: the old,
  proprietary format used for option files. Please convert users of this interface to
  the new JSON option file format introduced in this release. - all command line tools:
  the option "--check-for-updates" (the GUI will keep its online check for updates,
  though). There is and will be no equivalent interface in the tools themselves. Users
  of this interface can switch to retrieving the information about available updates
  directly from the MKVToolNix website. The information is available as JSON and XML
  files at the following URLs:
  https://mkvtoolnix.download/latest-release.json.gz
  https://mkvtoolnix.download/latest-release.xml.gz

## New features and enhancements

* mkvmerge: enhancement: added a new track property in JSON/verbose identification
  mode called "multiplexed_tracks". It's an array of track IDs that describe which of
  the tracks mkvmerge reports as separate ones were originally part of the same source
  track (e.g. TrueHD+AC-3 in a single track in MPEG transport streams). Implements
  #1835.
* mkvmerge: added support for skipping APE(v2) tags in TTA files.
* mkvextract: enhancement: added support for reporting progress in --gui-mode the
  same way mkvmerge does.
* all: new feature: all command line tools can now read JSON-formatted option files.
  Such a file's name must have an extension of ".json" (e.g. "mkvmerge
  @options.json"). Its content must be a valid JSON array consisting solely of JSON
  strings.
* MKVToolNix GUI: header editor & job output enhancement: added menu entries for
  saving or closing all open tabs.
* MKVToolNix GUI: chapter editor enhancement: added menu entries for saving or
  closing all open tabs.
* mkvmerge: MPEG TS/MPLS reader improvements: added support for subtitle tracks
  that are referenced from the MPLS file as sub-paths in other M2TS files than the main
  tracks.
* MKVToolNix GUI: multiplexer enhancement: the file identification process has
  been re-written to be properly multi-threaded. This allows the user to continue
  working with the GUI while e.g. playlists from a Blu-ray are identified.
* mkvmerge: enhancement: mkvmerge can now handle Blu-ray playlists from the
  "BACKUP" sub-directory of a Blu-ray disc.
* MKVToolNix GUI: new multiplexer feature: added a menu entry for copying the title to
  the destination file name. It will replace the destination file's base name but keep
  its path & extension.
* MKVToolNix GUI: new multiplexer feature: all positive file identification
  results will now be cached between runs. This speeds up adding the same file a lot,
  especially when scanning the same Blu-ray playlists again. Cached results are
  invalidated automatically with newer MKVToolNix releases or when the source file
  changes.
* MKVToolNix GUI: multiplexer enhancement: when the user tries to add one of the main
  Blu-ray index files (index.bdmv, MovieObject.bdmv) the GUI will automatically
  scan the Blu-ray playlist files and offer them for selection.
* MKVToolNix GUI: multiplexer enhancement: tracks, chapters, tags, attachments
  not selected for multiplexing will be displayed the same way as other disabled
  controls. Implements #1819.

## Bug fixes

* mkvmerge: bug fix: when using --track-order without specifying all tracks, the
  track numbers could end up in a way the user did not expect. Now mkvmerge will always
  assign track numbers for those tracks that are listed in --track-order first. The
  other tracks are assigned numbers afterwards. Fixes the second part of #1832.
* mkvmerge: bug fix: when reading Matroska files the movie title was always taken from
  the first Matroska source file, even if that file didn't have a title set. Fixes one
  part of #1832.
* MKVToolNix GUI: re-worked the startup code not to use lock files when trying to open a
  socket for communicating with an already-running instance. This aims to prevent
  situations with stale lock files not being cleaned up and the GUI not starting
  anymore as a result. This might fix or prevent issues like #1805.
* mkvmerge: teletext subtitle bug fix: fixed the handling of DVB teletext subtitles
  signaled with data unit ID 0x02 and that contain pages from multiple magazines.
* mkvmerge: bug fix: files smaller than 4 bytes were wrongly identified as MPEG
  transport streams.
* mkvmerge: bug fix: the MPEG transport stream reader was using an outdated format for
  the "CodecPrivate" element for HDMV TextST subtitles. This has been updated to the
  current format which only contains the "dialog style element". Existing Matroska
  files using this outdated scheme can be fixed by running them through mkvmerge
  v9.6.0 itself or any later release as the old format is automatically converted to
  the new one when it is read from Matroska files.

## Build system changes

* build system: building the GUI components of MKVToolNix now requires Qt v5.3.0 or
  newer.
* build system: MKVToolNix now requires a compiler that supports the following
  features of the C++14 standard: "std::make_unique()", "digit separators",
  "binary literals" and "generic lambdas". For the GNU Compiler Collection (gcc)
  this means v4.9.x or newer; for clang it means v3.4 or newer.


# Version 9.6.0 "Slave To Your Mind" 2016-11-29

## New features and enhancements

* mkvmerge & mkvextract: added support for HDMV TextST subtitles.
* MKVToolNix GUI: multiplexer enhancement: added a column "source file's
  directory" to the track list. Implements #1809.
* MKVToolNix GUI: multiplexer enhancement: added an option for selecting all tracks
  of the currently selected source files in the source file context menu. Inspired by
  #1809.
* MKVToolNix GUI: new feature: added options in the preferences to only show the list
  of often used languages/country codes/character sets in their respective
  selections instead of both the often used and the full list. Implements #1796.

## Bug fixes

* mkvextract: VobSub bug fix: mkvextract will add a "langidx" line to the .idx file
  upon extraction. Fixes #1810.
* MKVToolNix GUI: job output tool bug fix: the button for acknowledging warnings &
  errors wasn't properly disabled when the user used outside methods of
  acknowledging them (e.g. via the menu or via the job queue). Fixes #1802.
* mkvmerge: MPLS parser bug fix: fixed reading the "in" & "out" timestamps for "play
  items". This bug resulted in mkvmerge not reading the correct range from the
  referenced M2TS file under certain rare circumstances.
* mkvmerge: bug fix: mkvmerge was entering endless loops under certain conditions
  when appending files. This was a regression introduced with the fix to #1774 (using
  very large --sync values causing mkvmerge to abort).


# Version 9.5.0 "Quiet Fire" 2016-10-16

## New features and enhancements

* mkvmerge, mkvpropedit, MKVToolNix GUI: added support for the "field order" video
  track header element.
* mkvinfo: added support for the "field order" video track header element. Patch by
  James Almer (see AUTHORS).
* MKVToolNix GUI: merge tool enhancement: added menu entries that execute the
  "close", "save settings", "start muxing" or "add to job queue" action for all
  currently open tabs.
* MKVToolNix GUI: merge tool enhancement: when dragging & dropping directories the
  GUI will process all files within those directories recursively instead.
* mkvpropedit, MKVToolNix GUI's header editor: added options to modify the "muxing
  application" and "writing application" elements in the "segment information"
  container. Implements #1788.

## Bug fixes

* mkvmerge, mkvextract: VobSub handling bug fix: mkvmerge and mkvextract will now
  update the duration stored in the SPU bitsream with the duration from the container
  level if it differs at least 1ms. Fixes #1771.
* mkvmerge: h.264 elementary stream handling bug fix: if mkvmerge ever encounters
  changing SPS or PPS NALUs (ones where their ID has been encountered before with
  different settings) in the h.264 then it will prepend all following key frames with
  all currently active SPS and PPS NALUs. This enables playback from arbitrary key
  frames even if they require other SPS or PPS settings than the ones stored in the AVCC
  in CodecPrivate. Fixes #1711.
* mkvmerge: MPEG transport stream reader bug fix: fixed the handling of Blu-ray PCM
  audio with an odd number of channels by removing their alignment bytes.
* mkvmerge: MPEG transport stream reader bug fix: fixed mis-detection of certain
  h.264 files as MPEG transport streams.
* mkvmerge: WAV reader bug fix: the track properties (channels, sample rate) for DTS
  and AC-3 in WAV will now be derived from the decoded bitstream headers instead of the
  WAV file header as the latter is often incorrect.
* mkvmerge: WAV reader bug fix: fixed detection and merging of DTS in WAV that uses the
  14-bytes-in-16-bytes packing method.
* mkvmerge: bug fix: The Ogg/OGM reader did not recognize Opus files with comment
  headers anymore. This was broken by the fix to not require Ogg/OGM files to have
  comment headers in v9.4.0.


# Version 9.4.2 "So High" 2016-09-11

## Bug fixes

* mkvmerge: bug fix: AVC & HEVC readers: release v9.4.1 contains a change to both
  readers so that they will refuse to handle files where the detected pixel width or
  height is equal to or less than 0. This check was wrong in certain cases causing
  mkvmerge to reject a file as an unsupported file type. This has been fixed while
  keeping the constraints on width & height having to be positive.


# Version 9.4.1 "Black Rain" 2016-09-11

## Important notes

* Note: most of the bugs fixed on 2016-09-06 and 2016-09-07 for issue #1780 are
  potentially exploitable. The scenario is arbitrary code execution with
  specially-crafted files. Updating is highly recommended.

## Bug fixes

* mkvmerge: bug fix: AVC & HEVC readers: the readers will now refuse to handle files
  where the detected pixel width or height is equal to or less than 0. Before this fix the
  muxing process aborted with an assertion inside libMatroska. Fixes the last test
  case of #1780.
* mkvmerge: bug fix: HEVC parser: fixed another invalid memory access (beyond the end
  of allocated space). Fixes two test cases of #1780.
* mkvmerge: bug fix: HEVC parser: fixed another invalid memory access (beyond the end
  of a fixed-size array). Fixes several test cases of #1780.
* mkvmerge: bug fix: MP4 reader: an error message will be printed instead of an
  uncaught exception when an invalid atom chunk size is encountered during resync.
  Fixes a test case of #1780.
* mkvmerge: bug fix: AAC reader: fixed mkvmerge throwing an uncaught exception due to
  the sample rate being 0. Fixes a test case of #1780.
* mkvmerge: bug fix: MP4 reader: fixed an invalid memory access (beyond the end of
  allocated space). Fixes several test cases of #1780.
* mkvmerge: bug fix: HEVC parser: fixed an invalid memory access (beyond the end of
  allocated space). Fixes several test cases of #1780.
* mkvmerge: bug fix: fixed an invalid memory access (use after free) during global
  destruction phase. Fixes several test cases of #1780.
* mkvmerge: bug fix: using very large --sync values (several minutes) with certain
  container formats was causing mkvmerge to abort muxing. Fixes #1774.


# Version 9.4.0 "Knurl" 2016-08-22

## New features and enhancements

* mkvmerge: new feature: added support for reading Apple ProRes video from MOV/MP4
  files. Patch by Chao Chen (see AUTHORS).
* MKVToolNix GUI: merge tool enhancement: when adding attachments the GUI will check
  if there are attachments or attached files with the same name as the file to add. If so
  the GUI will tell the user and ask for confirmation.
* mkvmerge: enhancement: mkvmerge now accepts file names in square brackets for
  appending files, e.g. "mvkmerge -o out.mkv [ in1.avi in2.avi in3.avi ]" instead of
  "mkvmerge -o out.mkv in1.avi + in2.avi + in3.avi".
* MKVToolNix GUI: merge tool enhancement: the "select a play list to add" dialog does
  now contain a column with the number of chapters for each play list found.
* MKVToolNix GUI: job queue enhancement: dragging & dropping a valid .mtxcfg file
  (either a full job file or one containing only merge settings without the job
  properties) onto the job queue window will import the dropped .mtxcfg job into the
  job queue. Rest of the implementation of #1714.
* MKVToolNix GUI: merge tool enhancement: dragging & dropping a job queue .mtxcfg
  file onto the merge tool or using one as a command line parameter to the
  mkvtoolnix-gui executable will import the .mtxcfg job into the job queue. Part of
  the implementation of #1714.
* MKVToolNix GUI: merge tool enhancement: toggling the WebM mode check box will
  update the output file name's extension automatically.

## Bug fixes

* mkvpropedit: bug fix: mkvpropedit will no longer say that it's writing the changes
  if only attachment changes are specified and none of the specified attachments can
  be found.
* MKVToolNix GUI: chapter editor bug fix: overly long chapter names don't cause the
  GUI's window to become overly wide anymore. Fixes #1760.
* mkvmerge: DTS bug fix: if present mkvmerge will use an XLL extension's sample rate
  information as the sample rate to put into the track headers. Fixes #1762.
* mkvmerge: bug fix: when appending files mkvmerge wasn't starting clusters on video
  key frame anymore for the first and all following appended files. Fixes #1757.
* mkvmerge: bug fix: VP8 in Ogg: fixed dropping the first frame and the timestamp
  calculation. Fixes #1754.
* mkvmerge: bug fix: mkvmerge does no longer emit a warning if no comment header packet
  is found when reading tracks from Ogg/OGM files. See #1754.
* MKVToolNix GUI: merge tool bug fix: the automatic adjustments to the output file
  name based on the track types selected for muxing and the mechanism for keeping
  output file names unique had been broken since release v9.3.0. Fixes #1743.


# Version 9.3.1 "Mask Machine" 2016-07-14

## Bug fixes

* MKVToolNix GUI: merge tool bug fix: the GUI v9.3.0 was often creating an invalid
  syntax for the --probe-range-percentage parameter for mkvmerge due to
  uninitialized memory. Fixes #1741.


# Version 9.3.0 "Second Sight" 2016-07-13

## Important notes

* mkvmerge, mkvextract, MKVToolNix GUI: bug fix: several fixes to the handling of
  country codes. The list has been updated to reflect the currently valid top level
  domain country codes. Deprecated codes such as "gb" for "Great Britain" are now
  mapped to their updated values ("uk" for "United Kingdom" in this case). Fixes
  #1731.

## New features and enhancements

* mkvmerge, MKVToolNix GUI: new chapter generation feature: two new placeholders
  have been introduced when generating chapters for appended files, <FILE_NAME> and
  <FILE_NAME_WITH_EXT>. The former will be replaced by the appended file's name
  without its extension; the latter with its extension. Implements #1737.
* MKVToolNix GUI: merge tool enhancement: when opening a saved configuration (via
  the menu as well as via drag & drop) the current tab will be replaced if it is empty ( = in
  the same state it is in right after creating new mux settings). Implements #1738.
* mkvmerge, MKVToolNix GUI: added an option for specifying how much of a MPEG PS or TS
  file is probed for tracks (--probe-range-percentage). Implements #1734.
* mkvmerge, mkvinfo: new feature: added flags to support the Colour elements in the
  video tracks of Matroska containers. Users can use those flags to specify the colour
  space, transfer function, chromaticity coordinates etc. These properties are
  useful for correct colour reproduction of high dynamic range / wide colour gamut
  videos.
* MKVToolNix GUI: merge tool enhancement: the default track languages to set can now
  also be set whenever the language in the source file is 'undefined' ('und'). This is
  now the default and can be changed back to the old behavior (only set if the source file
  doesn't contain a language attribute) in the preferences. Implements #1697.
* MKVToolNix GUI: merge tool enhancement: menus have been added to both the "start
  muxing" and the "add to job queue" buttons. The menus let the user override the
  preferences regarding clearing merge settings after starting to mux and after
  adding a job to the queue respectively. Implements #1696.
* mkvmerge: the warning about not being able to determine whether a raw AAC file
  contains HE-AAC/AAC+/SBR has been removed. Implements #1701.
* MKVToolNix GUI: enhancement: all file names are now displayed with their native
  path separators (e.g. "C:\some\where\output.mkv" on Windows). Implements
  #1298, #1456.

## Bug fixes

* mkvmerge: bug fix: fixed overly long file type detection in some cases when text
  subtitle type probing read a lot of data due to there being no carriage returns near
  the start of the file.
* mkvmerge: WavPack4 bug fix: relaxed the stream detection criteria to only require
  the major version to be 4 and not to check the minor version. Fixes #1720.
* configure: fixed the Qt detection with Qt 5.7.0 which now requires the compiler to be
  in C++11 mode.
* mkvmerge: MP4 bug fix wrt. DTS handling: mkvmerge will re-derive parameters such as
  number of channels and sampling frequency from the DTS bitstream circumventing
  invalid values in the track headers (e.g. a channel count of 0). Fixes #1727/1728.
* mkvmerge: TrueHD bug fix: fixed detection of 96 kHz sampling frequency.
* mkvinfo's GUI: fix a crash due to wrong usage of referenced temporary objects. Fixes
  #1725.
* MKVToolNix GUI: merge tool bug fix: the GUI now takes into account whether splitting
  is activated when looking for and warning due to existing destination files. Fixes
  #1694.
* mkvmerge: bug fix: the parser for the --default-duration argument was wrongfully
  handling arguments of the form "123/456i" (only this specific syntax and only with
  "i" as the unit; other formats and units were fine). This is part of #1673.
  Additionally the parser doesn't use the "double" data type internally anymore
  fixing loss of precision and failing test cases on certain 32bit platforms. This
  fixes #1705.

## Build system changes

* build system: libEBML v1.3.4 and libMatroska v1.4.5 are now required due to several
  new elements having been specified for Matroska, and mkvmerge uses those elements.
* build system: libEBML v1.3.4 and libMatroska v1.4.5 are now required due to the
  usage of new elements introduced in libMatroska v1.4.5. The copies included in the
  MKVToolNix source code have been updated to those releases as well.

## Other changes

* mkvmerge: MPEG TS: considerable parts of the module have been rewritten. Due to its
  convoluted structure didn't buffer PES packets properly before trying to parse the
  PES header leading to invalid memory accesses in certain cases.


# Version 9.2.0 "Photograph" 2016-05-28

## New features and enhancements

* MKVToolNix GUI: merge tool enhancement: the action "select all attached files" in
  the popup menu actions for the attached files view has been split up into "enable all
  attached files" and "disable all attached files". Implements #1698.
* mkvinfo GUI: enhancement: the window title now includes the file name. Implements
  #1679.
* mkvmerge: enhancement: the "bit depth" track header field will be set for DTS tracks
  from the first DTS core header. Implements #1680.

## Bug fixes

* MKVToolNix GUI: bug fix on Windows: removing the drive letter does not cause the
  colon to be removed automatically anymore. Fixes #1692.
* MKVToolNix GUI: merge tool bug fix: it's no longer possible to select "1" as the
  maximum number of files to split into as mkvmerge doesn't accept that value. Fixes
  #1695.
* mkvmerge: bug fix: the "interval" chapter generation mode was always creating one
  chapter too many.
* mkvmerge: bug fix: if a certain number of chapters had been generated with
  --generate-chapters then mkvmerge wasn't replacing the void placerholder with
  the actual chapters. Fixes #1693.
* MKVToolNix GUI: merge tool bug fix: the track column "default track in output"
  wasn't taking into account if the track had its "default track" flag set to "no" in the
  source file. This would result in the column showing "yes" in certain situations
  even though mkvmerge would assign "no".
* mkvmerge: bug fix: fixed detection of (E-)AC-3 in MPEG TS files with unusual stream
  types (e.g. 0x87) but with (E-)AC-3 PMT descriptors. Fixes #1684.
* mkvmerge, mkvextract: bug fix: fixed handling of Big Endian PCM with a bit depth
  other than 16, 32 or 64 bits/sample. Other formats were using the Little Endian codec
  ID, but their content was actually not byte-swapped to match it. Now those other bit
  depths are byte-swapped to Little Endian, too. Fixes #1683.
* mkvmerge: bug fix: the time zone portion of the "date_local" member of the JSON and
  verbose identification formats contained the time zone's name instead of its
  offset on Windows due to the Visual C++ runtime's std::strftime not being C++11
  compliant. Additionally this resulted in errors about invalid UTF-8 strings for
  locales where the time zone's name contained non-ASCII characters.

## Other changes

* mkvinfo: the change to start the GUI by default on Windows and Mac OS has been
  reverted. Instead a separate executable (mkvinfo-gui) will be included for those
  platforms which starts the GUI by default. The newly introduced option "--no-gui"
  will remain valid but won't have any effect when used with mkvinfo.


# Version 9.1.0 "Little Earthquakes" 2016-04-23

## New features and enhancements

* mkvmerge: MPEG TS/teletext enhancement: included the teletext page number in the
  JSON/verbose identification output as track property "teletext_page".
* mkvmerge: MPEG TS/teletext enhancement: if a teletext track contains multiple
  teletext pages then mkvmerge will now recognize all of those pages as separate
  tracks to merge instead of only merging the first page. See #1662.
* mkvmerge: MPEG TS/teletext enhancement: mkvmerge will now ignore obviously bogus
  PTS values for teletext tracks and use PTS from earlier audio or video packets
  instead. See #1662.
* mkvmerge: MPEG TS reader enhancement: teletext tracks of type 5 (hearing impaired)
  are recognized as subtitles, too. Implements #1662.
* MKVToolNix GUI: merge tool enhancement: characters that aren't valid in path names
  are automatically removed from the output file name. Implements #1647.
* mkvextract: new feature: added support for extracting WebVTT subtitles.
  Implements the extraction part of #1592.
* mkvmerge: new feature: added support for reading WebVTT subtitles from WebVTT and
  Matroska files. Implements the merge part of #1592.
* mkvmerge: enhancement: when reading Matroska files not created by mkvmerge that
  contain chapters the existing edition UIDs and chapter UIDs are removed and random
  ones created. This is necessary as not only HandBrake but other tools assign
  sequential numbers starting at 1 for each file. Therefore there are two chapter
  entries with the UID 1, two with the UID 2 etc. and those should, strictly speaking, be
  treated as if they were a single chapter whereas the user expects those entries to
  stay separate entries.
* MKVToolNix GUI: new feature: added an option in the preferences ("Merge" →
  "Output") for controlling whether or not the GUI clears the "output file name" input
  upon removal of the last file.
* MKVToolNix GUI: new feature: added an option in the preferences ("Merge" → "Default
  values") for controlling whether or not the GUI clears the "file title" input upon
  removal of the last file.
* mkvmerge: enhancement: added the muxing date in both local time zone and UTC to
  verbose/JSON identification outputs (keys "date_local" and "date_utc",
  formatted after ISO 8601) when identifying Matroska files.
* mkvmerge: enhancement: added the minimum timestamp for each track in verbose/JSON
  identification outputs (key "minimum_timestamp") when identifying Matroska
  files. At most the first ten seconds are probed; if no block is found for a track within
  that range then the key is not output for the track. Also added "muxing_application"
  and "writing_application" to the "container" section of the output. Currently
  those are only set for Matroska files.

## Bug fixes

* mkvmerge: MPEG TS bug fix: the "text_subtitles" property of the JSON/verbose
  identification modes was always set to true for all subtitle tracks, even for those
  that aren't text subtitles (VobSub, PG).
* mkvmerge: MPEG TS/teletext bug fix: the language code signaled in the MPEG TS PMT is
  taken into account when selecting the character encoding to use during decoding of
  the teletext subtitles, not just the "national character set" stored in the
  teletext page headers. For example, a German teletext page may signal "national
  character set" 0 (English) whereas it's actually 4 (German). See #1662.
* mkvmerge: teletext decoding bug fix: fixed dropping of certain non-ASCII
  characters in rare circumstances due to wrong filtering of already UTF-8 encoded
  strings.
* MKVToolNix GUI: bug fix (Windows only): the GUI didn't start if the USERNAME
  environment variable contained characters that aren't allowed in file names (e.g.
  : or ?).
* mkvmerge: AVI reader bug fix: fixed reading files where the file ends in the middle of
  an audio chunk. Fixes #1657.
* mkvmerge: bug fix: mkvmerge will no longer abort reading a Matroska file with a
  structural error right before the first cluster. Fixes #1654.
* MKVToolNix: merge tool bug fix: when adding playlists the GUI won't ask the user
  whether or not to scan if there's only a single playlist in that directory.
* mkvmerge: bug fix: AVC/h.264: fixed handling of interlaced frames with bottom
  field first.
* MKVToolNix GUI: bug fix: fixed huge memory consumption (e.g. allocation of 2 GB for a
  JSON file of 650 KB) in the JSON library by updating said JSON library. Fixes #1631.

## Other changes

* MKVToolNix GUI: merge tool change: attachments from source files have been moved
  from the "Tracks, chapters, tags and attachments" list on the "sources" tab to a new
  list on the "attachments" tab. That way all existing attachments and all the ones to
  newly add will be shown in a single tab. This makes it easier to decide which
  attachments will have to be added and which can be removed.


# Version 9.0.1 "Obstacles" 2016-03-28

## Bug fixes

* mkvmerge: bug fix: regression in v9.0.0: the text subtitle packetizer was
  wrongfully assuming an encoding of UTF-8 if none was given instead of assuming the
  system's encoding. Fixes #1639.
* mkvmerge: bug fix: if too many chapters had been generated with
  --generate-chapters then mkvmerge created a bogus entry in the meta seek element
  and did not actually write the chapters to the file.
* mkvmerge: bug fix: the DTS packetizer was setting the number of channels wrong
  sometimes when reducing to the DTS core. It was using the number of channels
  including the extensions instead of the channels of the core only.


# Version 9.0.0 "Power to progress" 2016-03-26

## New features and enhancements

* all: new feature: added a new translation of both the programs and the man pages to
  Korean by Potato (see AUTHORS).
* MKVToolNix GUI: chapter editor enhancement: added a button next to the 'segment
  UID' controls that enable the user to select a Matroska file. The GUI reads that
  file's segment UID and enters its value into the input field.
* mkvinfo: change: on Windows and Mac OS mkvinfo will now launch the GUI by default
  unless the option »--no-gui« (or »-G«) has been given. This is due to the fact that on
  both OS users often use portable versions respectively disk images and launch the
  executable directly and not via start menu entries. In those situations adding
  command line options for launching the GUI is unnecessarily difficult.
* MKVToolNix GUI: merge tool (playlist selection dialog) enhancement: the playlist
  items are sorted by their position within the playlist by default.
* MKVToolNix GUI: merge tool (playlist selection dialog) enhancement:
  double-clicking on a playlist will select and add that playlist.
* mkvmerge: enhancement: added the number of bits per sample to the verbose/JSON
  identification output for FLAC files.
* mkvextract: new feature: implemented the extraction of Big Endian PCM (codec ID
  A_PCM/INT/BIG) to WAV files. The content will be byte-swapped into Little Endian
  PCM in the process.
* mkvmerge: enhancement: Big Endian PCM tracks will now be byte-swapped into Little
  Endian PCM, and the codec ID A_PCM/INT/LIT will be used. This was done due to a lot of
  players not supporting Big Endian PCM inside Matroska.
* MKVToolNix GUI: job queue enhancement: completed jobs will now be removed from the
  queue automatically on exit if the job has been added more than 14 days ago in order not
  to let the queue grow arbitrarily large. This feature can be turned off and the number
  of days can be adjusted in the preferences.
* mkvextract: enhancement: when extracting chapters in the simple format the user
  can use the new option »--simple-language …« for selecting the chapter names that
  are output. Normally the first chapter name found in each atom is used. With this
  option mkvextract looks for a chapter name whose language matches the specified
  one. Implements the feature enhancement part of #1610.
* MKVToolNix GUI: new chapter editor feature: added an option to multiply all chapter
  timecodes by a factor to the "additional modifications" dialog. Implements #1609.

## Bug fixes

* Installer: fixed support for silent installation and uninstallation.
* mkvmerge: bug fix: fixed two more issues in the conversion of teletext subtitles to
  SRT subtitles: 1. Packets belonging to pages that don't contain subtitles were used
  as valid end points for subtitles causing entries to become very short (e.g. 40ms).
  2. Sometimes the timestamps of wrong packets were used as entry's start and end
  points causing start timestamps and durations that were slightly off. Second part
  of the fix for #1623.
* mkvmerge: bug fix: MP4/QuickTime reader: audio tracks can contain two instances of
  certain header fields (channel count, bits/sample & channel, sample rate) in the
  STSD atom: one instance in the version 0 header and one in the version 2 header parts.
  So far mkvmerge has used those from the version 0 header only and ignored the ones from
  the version 2 header. This has been changed to match the behavior of other players and
  MP4 readers like ffmpeg. If the STSD atom contains a version 2 structure then the
  fields from it will be used. Otherwise the fields from the version 0 part will be used.
  Fixes #1633.
* mkvmerge: bug fix: fixed two issues in the conversion of teletext subtitles to SRT
  subtitles: 1. Consecutive teletext packets with the same content are now merged
  into a single entry instead of resulting in multiple entries. 2. The calculation of a
  packet's duration was wrong in certain situations. Part of the fix for #1623.
* mkvextract: bug fix: fixed the duplication of VPS, SPS, PPS and SEI NALUs when
  extracting h.265/HEVC tracks. See #1076 and #1621.
* mkvmerge: bug fix: reverted the patch by Vladimír Pilný that made the h.265/HEVC not
  store SEI NALUs with the frames during muxing. It was supposed to prevent having the
  SEI NALUs present twice when extracting HEVC due to some SEI information also being
  stored in the codec private data, but it dropped a lot of other SEI NALUs irrevocably.
  Fixes #1621.
* mkvmerge: bug fix: the --sub-charset option is now ignored for text subtitle files
  that start with a byte-order mark (BOM) bringing the behavior in line with the
  documentation. Fixes #1620.
* mkvmerge, MKVToolNix GUI: new feature: added switches (»--generate-chapters«
  and »--generate-chapter-name-template«) and their corresponding UI items for
  generating chapters while muxing. Two modes are currently supported:
  »when-appending« which creates one chapter at the beginning and an additional one
  each time a file is appended and »interval:…« which generates chapters in fixed
  intervals. Implements mkvmerge's and the GUI's part of #1586.
* mkvpropedit, MKVToolNix GUI's header editor: bug fix: fixed the handling of files
  where the last level 1 element has an unknown size. The programs will now either fix
  this element to have a known size or abort the process with an appropriate error
  message but without modifying the file. Fixes #1601.
* mkvextract: several issues regarding the extraction of chapters in the simple
  format have been fixed: if multiple names with different languages were present
  then an entry had been written for each name; the total number of entries written was
  wrong; the wrong entries were written. The new code only writes the first name found
  from the top-most chapter atoms of all editions. Chapters flagged as hidden or as not
  enabled are not extracted at all. Fixes the bug part of #1610.

## Build system changes

* build system: implemented support for explicit pre-compiled headers for Linux and
  Mac OS.
* build system: added an option to configure »--without-qt-pkg-config«. Normally
  configure uses pkg-config for detecting Qt and setting QT_CFLAGS and QT_LIBS. With
  this option configure won't use pkg-config and rely on the user having set both
  variables before running configure. This enables using Qt on systems where no
  pkg-config files are generated (e.g. Qt 5.6.0 on MacOS with frameworks enabled).

## Other changes

* mkvmerge: MP4/QuickTime reader: audio tracks with the FourCC 'lpcm' are muxed as
  A_PCM/INT/LIT instead of A_QUICKTIME.


# Version 8.9.0 "Father Daughter" 2016-02-21

## New features and enhancements

* MKVToolNix GUI: header editor enhancement: when the user drags & drops files on an
  open header editor tab the GUI will ask the user what to do with them: either open the
  files as new header editor tabs or add the files as new attachments to the current tab.
  The action can also be set as the default. Implements #1585.
* MKVToolNix GUI: chapter & header editor enhancement: Matroska files are initially
  opened in read-only mode and only later re-opened in read/write mode in order to
  enable reading from write-protected files. Part of the implementation of #1594.
* MKVToolNix GUI: chapter & header enhancement: the error messages shown when a
  Matroska file could not be parsed have been improved to include the most likely
  reasons. Part of the implementation of #1594.
* MKVToolNix GUI: chapter editor enhancement: added a menu entry for removing
  chapters from an existing Matroska file. Inspired by #1593.
* MKVToolNix GUI: chapter editor enhancement: it is now possible to save chapters to
  Matroska files after having removed all entries (editions and chapter atoms). This
  effectively removes the chapters from the file. Implements #1593.
* MKVToolNix GUI: job queue enhancement: added keyboard shortcuts for removing all
  completed jobs and for removing successfully completed jobs. Implements #1599.
* MKVToolNix GUI: merge tool enhancement: added icons to the context menu actions in
  the "attachments" sub-tab. Implements #1596.
* MKVToolNix GUI: merge tool enhancement: made the context menu entries in the
  "attachments" sub-tab clearer. Implements #1597.
* docs: added a Polish translation of the man pages by Daniel Kluz (see AUTHORS).
* MKVToolNix GUI: "run program after XYZ" enhancement: configurations can now be
  deactivated without having to change them. Implements #1581.
* mkvmerge: enhancement: when reading Matroska files created by HandBrake that
  contain chapters the existing edition UIDs and chapter UIDs are removed and random
  ones created. This is necessary as HandBrake assigns sequential numbers starting
  at 1 for each file. Therefore there are two chapter entries with the UID 1, two with the
  UID 2 etc. and those should, strictly speaking, be treated as if they were a single
  chapter whereas the user expects those entries to stay separate entries.
  Implements an improvement for issues such as #1561.
* MKVToolNix GUI: enhancement: the "escape for Windows' cmd.exe" mechanism will
  only escape arguments that actually need escaping in order to produce easier to read
  command lines.

## Bug fixes

* MKVToolNix GUI: bug fix: ampersands (&) in file names were shown as keyboard
  shortcuts in tab titles in various tools (merge tool, chapter and header editors,
  job output tool). Fixes #1603.
* mkvmerge: bug fix: fixed the handling of AVIs with a negative video height (which
  signals that the rows are arranged top-to-bottom).
* MKVToolNix GUI: job queue bug fix: fixed an invalid memory access in the "edit in
  corresponding tool and remove from queue" functionality.
* MKVToolNix GUI: re-write, merge tool bug fix: the file identification is now based
  on mkvmerge's JSON output instead of its verbose output. This also fixes the merge
  tool not showing names of attachments inside Matroska files properly if those names
  contain spaces (#1583).
* MKVToolNix GUI: merge tool bug fix: the "mux this" combo box was disabled if a single
  attachment was selected.
* mkvmerge: bug fix: removed spurious output generated during file identification
  in the HEVC detection code (e.g. "Error No Error").
* mkvmerge: bug fix: fixed the output of the "playlist_file" and "other_file"
  properties of the "container" entity in the JSON identification format from a
  single string to an array of strings. The format version has been bumped to 3 due to
  this change.
* mkvmerge: bug fix: fixed parsing of AAC in MP4 with a program config element with an
  empty comment field at the end of the GA specific config. Fixes #1578.
* MKVToolNix GUI: merge tool bug fix: the GUI no longer requires at least one source
  file to be present before muxing can start in order to allow creation of track-less
  files. Fixes #1576.
* mkvmerge: QuickTime/MP4 reader: fix a division by zero in the index generation for
  certain old audio codecs that have certain header fields (bytes_per_frame,
  samples_per_packet) set to 0.
* mkvinfo: bug fix: global elements (EBML void and CRC-32 elements) are now handled
  correctly if they're located inside the segment info or the chapter translate
  parents.

## Other changes

* MKVToolNix GUI: the default font size adjustment has been deactivated for the time
  being as it causes problems on high DPI displays. See #1602.


# Version 8.8.0 "Wind at my back" 2016-01-10

## New features and enhancements

* MKVToolNix GUI: "run program after XYZ" enhancement: added a button for executing
  the program right now as a test run. See #1570.
* MKVToolNix GUI: "run program after XYZ" enhancement: an error message is shown if
  the program couldn't be executed. See #1570.
* MKVToolNix GUI: "run program after XYZ" enhancement: any leading spaces in the
  executable path are removed in order to make copying & pasting less error-prone.
* mkvpropedit: enhancement: mkvpropedit will accept terminology variants of ISO
  639-2 language codes and convert them to the bibliographic variants
  automatically. Implements #1565.
* MKVToolNix GUI: enhancement: the GUI's default font's size is now scaled with the
  screen's DPI and is at least 9 points high (up from 8). Additionally on Windows "Segoe
  UI", which is Windows' default user interface font, is used instead of the default
  provided by Qt, "MS Shell Dlg 2".
* MKVToolNix GUI: enhancement: the user can select the font family and size for the GUI
  in the preferences.
* MKVToolNix GUI: merge tool enhancement: added a column to the "attachments" tab
  containing the file size.
* MKVToolNix GUI: enhancement: pressing the insert key when the focus is on the merge
  tool's source files or attachments list, on the chapter editor's chapter list or on
  the header editor's list will invoke the corresponding action for adding elements
  to that list.
* MKVToolNix GUI: new feature: implemented adding, changing and removing
  attachments in existing Matroska files as part of the header editor. Implements
  #1533.

## Bug fixes

* MKVToolNix GUI: "run program after XYZ" bug fix: the paths used in the variables and
  the executable are converted to the platforms native path separators. This fixes
  compatibility with Windows applications that don't support the use of forward
  slashes in path names like e.g. VLC. See #1570.
* mkvmerge: bug fix: fixed TrueHD detection both as raw streams as well as inside other
  contains if the stream does not start with a TrueHD sync frame.
* MKVToolNix GUI: new merge tool feature: added a layout for the track properties
  where they're on the right of the files/tracks lists in two fixed columns.
  Implements #1526.
* mkvmerge: bug fix: fixed a mis-detection of an MPEG-2 video elementary stream as a
  TrueHD file which then caused a segmentation fault. Fixes #1559.
* mkvmerge: bug fix: Matroska attachments with the same name, size and MIME type were
  not output during file identification.
* MKVToolNix GUI: merge tool bug fix: when using one of the "select all tracks (of
  type…)" actions the "properties" column didn't show the selection.


# Version 8.7.0 "All of the above" 2015-12-31

## New features and enhancements

* mkvmerge: enhancement: the MP4 reader will keep the display dimensions from the
  track header atom ("tkhd") and use them as the display width & height. See also #1547.
* MKVToolNix GUI: merge tool enhancement: the "add source files" button now has
  optional popup menu containing actions for adding/appending files and adding
  files as additional parts for easier discovery of those actions. This popup is only
  shown if the user clicks on the arrow shown on the right of the button.
* mkvmerge: new feature: TrueHD tracks that contain Dolby Atmos will be identified as
  "TrueHD Atmos". Implements #1519.
* MKVToolNix GUI: new merge tool feature: added menu options in the "Merge" menu for
  copying either the first source file's name or the current output file's name into
  the "file title" control.
* mkvpropedit: new feature: added an option for calculating statistics for all
  tracks and adding new/updating existing statistics tags in a file. Second half of
  the implementation of #1507.
* mkvpropedit: new feature: added an option for removing all existing track
  statistics tags from a file. Part of the implementation of #1507.
* mkvmerge: enhancement: added the container's internal track ID as the "number"
  attribute in verbose & JSON identification modes for several container types
  (QuickTime/MP4: the track ID from the 'tkhd' atom; MPEG program stream: the
  sub-stream ID in the upper 32 bits and the stream ID in the lower 32 bits; MPEG
  transport stream: the program ID; Ogg/OGM: the stream's serial number field;
  RealMedia: the track ID). Implements #1541.
* mkvmerge: enhancement: if JSON identification mode is active then warnings and
  errors will be output as JSON as well. They're output as arrays of strings as the keys
  "warnings" and "errors" of the main JSON object. Implements #1537.
* mkvpropedit: enhancement: when using --add-attachment, --replace-attachment
  or --update-attachment the UID can be changed with --attachment-uid. See #1532.
* mkvpropedit: new feature: added an option "--update-attachment" for updating the
  properties of existing attachments without replacing their content. Implements
  #1532.
* MKVToolNix GUI: new feature: added options for running arbitrary programs after a
  job has finished or after the queue has finished. Implements #1406.
* MKVToolNix GUI: merge tool enhancement: if files are dragged & dropped from an
  external application with the right mouse button being pressed then the GUI will
  always ask the user what to do with the files even if the user has configured the GUI not
  to ask. Implements #1508.

## Bug fixes

* mkvmerge: bug fix: fixed the handling of a PES size of 0 ( = unknown). Tracks whose PES
  packets had such a size were sometimes not detected at all, and even if they were their
  content was incomplete. Fixes #1553.
* mkvmerge: bug fix: made the MPEG 1/2 video elementary stream file type recognition
  more resilient and more flexible dropping the requirement for a file to start with an
  MPEG start code (0x00 00 01). Fixes #1462.
* mkvpropedit: bug fix: when changing the track language it is now verified to be a
  valid ISO 639-2 language code before writing it to the file. Fixes #1550.
* mkvmerge: bug fix: the Matroska reader now uses TrueHD-specific code when reading
  Matroska files. This can fix things like wrong frame type flags.
* mkvmerge: bug fix: MP4 edit lists of certain types (two entries, first entry's
  media_time is -1, second entry's segment_duration is != 0) weren't handled
  properly resulting in key frame flags being assigned to the wrong frames. Fixes
  #1547.
* mkvmerge: bug fix: the h.265/HEVC code was writing SEI NALUs twice. This had already
  been mentioned in #1076 but never fixed. Patch by Vladimír Pilný.
* mkvmerge: bug fix: the h.265/HEVC code wasn't converting slice NALUs to RBSP form
  before parsing it resulting in wrongly timestamped frames under certain
  conditions. This is a similar fix to the issues reported in #918 and #1548.
* mkvmerge: bug fix: the h.264/AVC code wasn't converting slice NALUs to RBSP form
  before parsing it resulting in wrongly timestamped frames under certain
  conditions. Fixes #918 and #1548.
* mkvmerge: bug fix: the MP4 reader can now understand the 'random access point'
  sample grouping information for marking open GOP random access points as key
  frames. Fixes #1543.
* mkvmerge: bug fix: fixed the decisions whether or not to write the last frame of a
  track as a BlockGroup or a SimpleBlock and whether or not to write a block duration for
  that frame. Fixes #1545.
* mkvmerge: bug fix: the progress calculation was sometimes outputting negative
  numbers when appending Matroska files whose timestamps don't start at 0 (e.g. if
  they were created by splitting with linking enabled). In the the GUI this resulted in
  lines like "#GUI#progress -2%" in the job's output.
* mkvmerge: bug fix: AAC with low sampling frequencies was sometimes mis-detected
  with the wrong profile preventing appending it to other AAC tracks. Fixes #1540.
* mkvmerge: bug fix: chapters were output as both "chapters" and "track_tags" in JSON
  identification mode. Fixes #1538.
* MKVToolNix GUI: bug fix: the "split mode" drop-down box got reset to "do not split"
  each time the preferences dialog was closed with the "OK" button. Fixes #1539.
* MKVToolNix GUI: enhancement: when starting the GUI with a saved settings file then
  the GUI won't contain an empty tab in the merge tool anymore. Fixes #1504.
* mkvmerge: bug fix: fixed the key frame detection for VP9 video tracks.
* MKVToolNix GUI: bug fix: relative file names given on the command line were
  interpreted as being relative to the user's home directory. Fixes #1534.

## Other changes

* all: reversion of a change: several ISO 639-2 codes of languages that are very old and
  not spoken anymore have been re-added (e.g. "English, Middle (1100-1500)") due to
  feedback from users who did have a use for such codes.
* all: reversion of a change: all of the tools will write a byte-order mark (BOM) to text
  files encoded any of the UTF-* schemes again. This reverts the change in release
  8.6.0 due to user feedback preferring the old way.
* MKVToolNix GUI: the preferences dialog has been reworked heavily in order to
  provide a better overview and to be less overwhelming.


# Version 8.6.1 "Flying" 2015-11-29

## Bug fixes

* mkvpropedit, GUI's chapter & header editors bug fix: in certain situations the
  modified file would not contain a seek head before the first cluster anymore
  resulting in most players not finding elements such as attachments or the index
  located at the end of the file anymore. Fixes #1513.
* mkvmerge: bug fix: the change to do a deeper file analysis if no seek head was found was
  causing huge increases in file type detection time as popular tools like x264 don't
  write seek heads. The way elements at the end are searched has been changed to only
  scan the last 5 MB of the file instead of iterating over every level 1 element from the
  beginning of the file.


# Version 8.6.0 "A Place In Your World" 2015-11-28

## New features and enhancements

* mkvmerge: enhancement: if no seek head is found before the first cluster when
  reading Matroska files then mkvmerge will attempt a deeper scan of all elements in
  the file in order to find track headers, attachments, chapters and tags located at
  the end of the file. See #1513 for the rationale.
* mkvmerge: enhancement: added JSON as an output format for file type
  identification. It can be activated with "--identification-format json
  --identify yourfile.ext" (or their short counterparts "-F json -i
  yourfile.ext").

## Bug fixes

* mkvmerge: Matroska reader bug fix: the info about which packetizer is used was
  output twice for each HEVC track. Fixes #1522.
* MKVToolNix GUI: bug fix: implemented a workaround for a bug in Qt which caused the GUI
  not to start anymore due to failing to detect a stale lock file if the GUI had crashed
  before on a computer with a host name that included non-ASCII characters. See
  https://bugreports.qt.io/browse/QTBUG-49640
* mkvmerge: bug fix: a track's number of bits per audio sample wasn't output in verbose
  identification mode even if it was present in the file.
* MKVToolNix GUI: header editor bug fix: the "status" description wasn't adjusting
  its height properly resulting in its text being cut off. Fixes #1517.
* MKVToolNix GUI: bug fix: the program changes its working directory to the user's
  profile/home directory on startup allowing the removal of its installation folder
  even if a program started by the GUI (e.g. a web browser) is still running. Fixes
  #1518.
* ebml_validator: bug fix: elements with an unknown size weren't handled correctly.
* build system: fixed building and linking against libEBML and libMatroska if
  they're installed in a non-standard location.
* mkvpropedit, MKVToolNix GUI's chapter and header editors: the tools were unable to
  update elements in files without a seek head present. Fixes #1516.
* mkvmerge: bug fix: fixed two issues causing mkvmerge to write invalid data when
  updating track headers caused by the fix for "Re-rendering track headers:
  data_size != 0 not implemented yet". Fixes #1498.
* MKVToolNix GUI: bug fix: the options for linking to the next/previous segment UID
  were wrong. Fixes #1511.
* mkvmerge: bug fix: the VC-1 handlig code was duplicating the first sequence headers
  with each mux. Fixes #1503.
* build system: bug fix: configure was checking for and using libintl if
  --without-gettext was used. Fixes #1501.

## Other changes

* all: change: none of the tools will write a byte-order mark (BOM) to text files
  encoded any of the UTF-* schemes anymore.
* all: MKVToolNix now requires gcc 4.8.0 or later or clang 3.4 or later for
  compilation.


# Version 8.5.2 "Crosses" 2015-11-04

## New features and enhancements

* mkvpropedit, MKVToolNix GUI header editor: enhancement: added the "codec delay"
  track header field as an editable property.

## Bug fixes

* MKVToolNix GUI: bug fix: the file/track columns aren't resized to fit their content
  when expanding/collapsing tree nodes anymore. Such expansion also happened when
  moving entries with the "move up/down" buttons. Fixes #1492.
* mkvmerge: bug fix: fixed the values of the "previous/next segment UID" elements
  when splitting by parts with segment linking enabled. Fixes #1497.
* mkvmerge: bug fix: mkvmerge no longer creates a "next segment UID" field in the last
  file when splitting and segment linking is active.
* mkvmerge: bug fix: fixed an endless loop when updating track headers caused by the
  fix for "Re-rendering track headers: data_size != 0 not implemented yet". Fixes
  #1485.


# Version 8.5.1 "Where you lead I will follow" 2015-10-21

## New features and enhancements

* MKVToolNix GUI: header editor enhancement: several track properties like name or
  language are shown as columns in the tree for easier distinction between tracks.
  They're also shown on the overview page on the right when that track's entry is
  selected in the tree. The text in the labels on this overview page can be selected with
  the mouse for copying & pasting elsewhere.

## Bug fixes

* build system: libEBML v1.3.3 and libMatroska v1.4.4 are now required due to
  important fixes for invalid memory accesses in those two releases. The copies
  included in the MKVToolNix source code have been updated to those releases as well.
* MKVToolNix GUI: bug fix: the "save file" dialogs did not have the currently entered
  file name pre-selected anymore. Fixes #1480.
* MKVToolNix GUI: bug fix: fixed a crash when loading corrupted job settings.
* MKVToolNix GUI: header editor bug fix: the tree items weren't re-translated when
  the GUI language was changed.
* mkvmerge: bug fix: updating the track headers wasn't working in some rare cases
  (corresponding error message "Re-rendering track headers: data_size != 0 not
  implemented yet").
* MKVToolNix GUI: bug fix (Linux): the function "open folder" was inserting a
  superfluous leading slash in the directory name. This causes some file managers (in
  this particular case Dolphin on Linux) to interpret a directory name like
  "//home/mosu/…" as a share called "mosu" on a Samba/Windows server called "home"
  and to prepend the whole name with the "smb://" protocol. Fixes #1479.


# Version 8.5.0 "Vanishing Act" 2015-10-17

## New features and enhancements

* MKVToolNix GUI: merge tool enhancement: when dropping files onto the GUI the last
  file's directory is remembered as the last directory a file was opened from causing
  the next open file dialog to start in that directory. Implements #1477.
* all: new feature: added a Catalan translation of the man pages by Antoni Bella Pérez
  (see AUTHORS).
* MKVToolNix GUI: chapter editor enhancement: the start and end timestamps in the
  tree are displayed with nanosecond precision. Implements #1474.
* MKVToolNix GUI: merge tool enhancement: added a column to the track list containing
  the state of the "forced track" flag. Implements #1472.
* MKVToolNix GUI: merge tool enhancement: pressing the delete key in the attachments
  list removes the selected entries. Implements #1473.
* MKVToolNix GUI: enhancement: the context menu for the status bar job status
  counters is now shown when the user clicks with any mouse button, not just the right
  one. This should make the feature easier to discover. Implements #1396.
* MKVToolNix GUI: new job queue feature: added an option in the preferences for
  resetting the warning and error counters of all jobs and the global counters in the
  status bar to 0 when exiting the program. Implements #1437.
* MKVToolNix GUI: current job output enhancement: the separator lines for warnings
  and errors ("--- Warnings emitted by Job … started on … ---") are only shown when
  warnings/errors actually occur and not for each job that's run.
* mkvmerge: enhancement: improved identification output for DTS 96/24. Implements
  #1431.
* MKVToolNix GUI: merge tool enhancement: added buttons for previewing the
  character sets for text subtitles read from SRT and SSA/ASS files as well as for
  chapter files. They're located next to the drop down boxes for the character sets on
  the input and output tabs.
* MKVToolNix GUI: merge tool enhancement: added buttons next to the 'segment UID',
  'previous segment UID' and 'next segment UID' controls that enable the user to
  select a Matroska file. The GUI reads that file's segment UID and enters its value
  into the corresponding control. Part of the implementation of #1363.
* MKVToolNix GUI: chapter editor enhancement: Added another variable to the chapter
  name templates called <START> which is replaced by the chapter's start timestamp.
  An optional format can be specified, e.g. <START:%H:%M:%S.%3n> resulting in
  something like 01:35:27.734. This can be used in the 'generate sub-chapters' or the
  'renumber sub-chapters' functionality. Implements #1445.
* MKVToolNix GUI: merge tool enhancement: implemented the optional warning before
  overwriting existing files when starting to mux or adding a job to the queue. The
  pending jobs in the queue are checked for the same destination file name as well.
  Implements #1390.
* MKVToolNix GUI: enhancement: pressing the delete key in the chapter editor and the
  job queue removes the selected entries. Implements #1454.
* MKVToolNix GUI: merge tool enhancement: dropping chapter, tag and segment info
  files from external applications will cause those file names to be added to the
  appropriate controls on the 'output' tab. Implements #1332 and 1345.
* MKVToolNix GUI: merge tool enhancement: the feature "default track language" has
  been split into track languages by type. There are now three separate settings for
  audio, video and subtitle tracks. Implements #1338.
* mkvmerge: enhancement: the verbose identification for MP4 files will now derive
  basic audio parameters of MP3 and AC3 tracks from the bitstream instead of relying on
  the values in the track headers.
* MKVToolNix GUI: new merge tool feature: implemented an optional vertical layout
  mode for the "input" tab in which the track properties are shown below the track list.
  Implements #1304.
* MKVToolNix GUI: merge tool enhancement: when browsing for chapter files on the
  "output" tab the initial directory is the first input file's directory instead of
  the directory accessed last.
* MKVToolNix GUI: enhancement: on Windows the drop down boxes were elliding overlong
  text. This has been changed to making the open combo boxes' scroll areas wide enough
  to contain the whole entries. This matches the behavior of Qt on other operating
  systems.
* MKVToolNix GUI: new merge tool feature: added context menu entries for opening the
  selected files/the source files of selected tracks in MediaInfo. Implements
  #1423.

## Bug fixes

* mkvmerge: bug fix: the cropping parameters contained the "cropping:" prefix twice
  in the verbose identification output.
* MKVToolNix GUI: enhancement: if the last directory opened doesn't exist anymore
  then default to one that does in order to prevent an error message from older Windows
  versions about a location not being available. Fixes #1438.
* MKVToolNix GUI: bug fix: the menus that are currently not shown are disabled
  properly so that they don't react to keyboard shortcuts anymore. This affected e.g.
  Alt+J with the English localization as there were three shortcuts active: the "add
  to job queue" button (if the merge tool is active), the "job queue" menu and the "job
  output" menu.
* MKVToolNix GUI: bug fix (Windows): changed some options for Qt's file dialogs in
  order to speed up access to network shares in certain situations. Fixes #1459.
* mkvmerge: bug fix: PCM tracks: if the number of samples per packet varies then no
  default duration will be written. Fixes #1426.
* mkvmerge: new feature: The three options that use segment UIDs (--segment-uid,
  --link-to-previous and --link-to-next) can now read the segment UID of an existing
  Matroska file. For this the file's name must be given as an argument prefixed with =
  (e.g. '--segment-uid =some_file.mkv'). Implements #1363.
* MKVToolNix GUI: merge tool bug fix: If there's currently no source file present when
  the user drags & drops files onto the merge tool then the GUI will no longer leave an
  empty, superfluous tab for certain drop modes. Fixes #1446.
* MKVToolNix GUI: merge tool bug fix: the "default track flag in output" column wasn't
  updated properly directly after loading settings.
* MKVToolNix GUI: merge tool bug fix: the cropping parameters were not converted into
  parameters for mkvmerge at all.
* all: fixed the spelling of the AC-3, E-AC-3 and VC-1 codec names.
* MKVToolNix GUI: bug fix: the interface language selection has been improved not to
  select wrong entries resulting in error messages from mkvmerge about unknown
  translations. Fixes #1434.
* MKVToolNix GUI: bug fix: if the Windows version of the GUI was started from a
  symbolically linked folder then it would crash when the user added a file. Fixes
  #1315.

## Other changes

* all: several ISO 639-2 codes of languages that are very old and not spoken anymore
  have been removed (e.g. "English, Middle (1100-1500)").


# Version 8.4.0 "A better way to fly" 2015-09-19

## New features and enhancements

* MKVToolNix GUI: new merge tool feature: when dragging & dropping files onto merge
  settings already containing a file the user can set more options to be always done
  instead of asking (before: only adding files to the current merge settings could be
  thus marked; now: adding to current, adding to new settings and adding each file to
  new settings can be set to perform without asking). Implements #1388.
* MKVToolNix GUI: merge tool enhancement: when dragging & dropping files onto merge
  settings already containing a file the dialog asking the user what to do has received
  a new option for creating one new merge tab for each of the dropped files. Implements
  #1380.
* MKVToolNix GUI: new merge tool feature: the "tracks" tree view contains a new column
  titled "properties" which contains basic track properties: the pixel dimensions
  for a video track and sampling frequency, number of channels and bits per sample for
  an audio track. Implements #1295.
* mkvmerge: enhancement: the verbose identification result for all audio tracks has
  been extended to include the number of channels, the sample rate and the bits per
  sample where applicable. Part of the implementation of #1295.
* mkvmerge: enhancement: the pixel width/height will be reported in verbose
  identification mode for all video tracks.
* MKVToolNix GUI: new merge tool feature: added a column in the track list showing the
  effective state of the "default track" flag. It shows the state of the flag as it will
  be in the output file. Implements #1353.
* mkvmerge: enhancement: when mkvmerge encounters garbage data in the middle of AC-3
  or MP3 tracks it will now output the timecode where the garbage occurred in order to
  make checking for audio/video sync issues easier. Implements #1420.
* MKVToolNix GUI: chapter editor enhancement: added a column in the tree with the
  edition's/chapter's flags.
* MKVToolNix GUI: new feature: the state of all columns in all list/tree views can be
  reset (both the shown/hidden state as well as their order) from the column's context
  menu. See #1268.
* MKVToolNix GUI: new feature: the column headers of all list/tree views can be
  re-ordered via drag & drop and the GUI will remember their position upon restart.
  Additionally the columns can be hidden/shown via a context menu by right-clicking
  on the column headers. Implements #1268.
* MKVToolNix GUI: new chapter editor feature: added an option for skipping chapters
  marked as "hidden" in the re-numbering dialog. Implements #1414.
* all: new feature: added a new translation to Serbian (Cyrillic) by Jay Alexander
  Fleming (see AUTHORS).
* MKVToolNix GUI: enhancement: the header editor will convert ISO 639-2 terminology
  codes used in language elements to their corresponding bibliographic variants.
  Implements #1418.
* MKVToolNix GUI: enhancement: the titles and button texts of dialogs asking
  questions have been improved to be easier understandable. For example, instead of
  using "yes/no" as the answers to the question "Do you want to close the unmodified
  file?" the choices are now "Close file/Cancel". Implements #1417.
* MKVToolNix GUI: chapter editor enhancement: when loading simple/ OGM style
  chapter files that contain non-ASCII characters and which do not start with a byte
  order mark (BOM) the GUI will let the user chose the character set to use. A preview is
  shown for the selected character set and updated when the user changes the character
  set.
* MKVToolNix GUI: merge tool enhancement: added "remove all" and "select all"
  entries to the attachments context menu. Implements #1386.
* MKVToolNix GUI: job output enhancement: the output, warnings and error text views
  are now separated by two splitters enabling the user to change their respective
  sizes. These changes are remembered over restarts. Implements #1394.
* MKVToolNix GUI: chapter editor enhancement: pressing shift+return will cause the
  next appropriate chapter control to be selected depending on where the focus
  currently is: from a chapter input (start/end time, flags, UIDs) to the next chapter
  entry's start time, from a chapter name to the next chapter name and from the last
  chapter name to the next chapter entry's first chapter name. Implements #1398 and
  complements #1358.
* MKVToolNix GUI: chapter editor enhancement: pressing return on the very last
  chapter entry will wrap and focus the first one in the tree again. Enhances #1358.
* MKVToolNix GUI: enhancement: scrolling over input elements like combo boxes,
  check boxes and radio buttons located within a scroll area will now scroll the scroll
  area instead of the element the cursor is over (e.g. a combo box). Implements #1400.

## Bug fixes

* MKVToolNix GUI: chapter editor bug fix: whenever the additional modification of
  "expanding start/end timecodes to include the minimum/maximum timecodes of their
  children" was run on an edition entry then ChapterTimeStart and sometimes
  ChapterTimeEnd nodes were inserted as direct children of the EditionEntry node
  when saving. This resulted in invalid chapters.
* mkvmerge: bug fix: the pixel dimensions reported for VC-1 in MPEG transport streams
  in verbose identification mode was 0x0.
* mkvmerge: bug fix: the number of channels and the sample rate reported for DTS in MPEG
  transport streams and MPEG program streams in verbose identification mode was 0.
* all: bug fix: parsing of strings containing negative values or timecodes was broken
  on 32bit architectures. Fixes #1425.
* MKVToolNix GUI: merge tool bug fix: if the output file name policy "last output
  directory" was used then manual changes to the output file name weren't recognized
  as changes to the last output directory. Fixes #1411.
* MKVToolNix GUI: merge tool bug fix: the "default subtitle charset" is not applied to
  text subtitles from Matroska files as those are always encoded in UTF-8. Fixes
  #1416.
* MKVToolNix GUI: chapter editor bug fix: the "shift timecodes" action in the mass
  modification dialog wasn't working at all, and selecting multiple actions in the
  dialog would result in wrong actions being executed.
* MKVToolNix GUI: bug fix: fixed the total job queue progress with respect to removing
  completed jobs (either automatically or manually). Fixes #1405.
* MKVToolNix GUI: bug fix: mkvmerge is now run in with the same interface language set
  for the GUI.
* mkvmerge: bug fix: The formula used for calculating the audio delay for garbage data
  at the start of tracks in AVI files has been fixed again. It now uses the values
  dwStart, dwScale and dwSampleSize from the AVI stream header structure instead of
  values derived from the audio packet headers. Fixes #1382 and still works correctly
  for #1137.
* all: the environment variable <TOOLNAME>_OPTIONS is now parsed for options for
  TOOL (e.g. MKVMERGE_OPTIONS for mkvmerge). MKVTOOLNIX_OPTIONS is still used for
  all programs. Fixes #1403.
* MKVToolNix GUI: bug fix: fixed the escaping of the command line for cmd.exe
  regarding the command name itself (the very first argument). Fixes #1401.
* build system: bug fix: fixed Qt platform plugin detection on MacOS.

## Build system changes

* build system: removal: the switch "--without-mkvtoolnix-gui" has been removed.
  There are only two GUIs left in the package: the Qt-enabled mkvinfo and MKVToolNix
  GUI. Both are enabled by default and can be disabled with the option "--disable-qt".
  In that case only the text-mode version of mkvinfo is built, and the MKVToolNix GUI is
  not built at all.

## Other changes

* mkvmerge: container and track properties in verbose identification mode are now
  output sorted.
* mkvmerge: the verbose identification result for the MPEG program stream, MPEG
  transport stream and WAV readers has been changed for audio tracks in order to match
  the Matroska reader's result. The old keys "channels", "sample_rate" and
  "bits_per_sample" have been replaced by "audio_channels",
  "audio_sampling_frequency" and "audio_bits_per_sample".
* Removal: all support for wxWidgets has been removed. This means that the mkvmerge
  GUI (mmg) has been removed and that mkvinfo now only supports a text-mode and a
  Qt-based interface.


# Version 8.3.0 "Over the Horizon" 2015-08-15

## New features and enhancements

* MKVToolNix GUI: chapter editor enhancement: pressing return will cause the next
  appropriate chapter control to be selected depending on where the focus currently
  is: from a chapter input to the first chapter name, from a chapter name to the next
  chapter name and from the last chapter name to the next chapter entry's start time.
  Implements #1358.
* MKVToolNix GUI: enhancement: the number of running jobs is shown in the status bar.
  Implements #1381.
* MKVToolNix GUI: new job queue feature: added a context menu for force-starting
  selected jobs. This allows for running more than one job at the same time. Implements
  #1395.
* MKVToolNix GUI: new merge tool feature: added an option for automatically setting
  the "default track" flag to "no" for all subtitle tracks when they're added.
  Implements #1339.
* MKVToolNix GUI: new merge tool feature: dragging & dropping files onto line edit
  controls that expect file names (e.g. the "chapter file" control) will set that line
  edit's text to the dropped file name. Implements #1344.
* MKVToolNix GUI: new feature: jobs in the queue can now be edited again. For that
  they're re-opened in the corresponding tool and removed from the queue. Implements
  #1296.
* MKVToolNix GUI: enhancement: the format of the setting and queue files has been
  changed from INI style to JSON documents. Reading older setting files in INI style
  remains supported, but saving will convert them to JSON. Together with the other
  three changes mentioned below this results in a noticeable reduction in the time
  needed for writing the queue files, e.g. when pressing "start muxing" or when
  quitting the application.
* MKVToolNix GUI: enhancement: the way the job queue is stored has been changed.
  Earlier all jobs were stored in the same file (or registry on Windows) as the
  preferences. Now they're stored in a sub directory called "jobQueue" with one file
  per queue entry.
* MKVToolNix GUI: enhancement: on Windows the preferences are not stored in the
  registry anymore, not even if the application has been installed. Instead they're
  stored in an INI file in the user's AppData\Local directory tree.
* MKVToolNix GUI: enhancement: the number of times the queue files are saved has been
  reduced. The queue files are also loaded only once on startup, not twice.
* MKVToolNix GUI: enhancement: if an instance is already running when the
  application is started a second time then the GUI requests that the already-running
  instance will be activated. Implements #1379.
* MKVToolNix GUI: new feature: added an option in the preferences for automatically
  switching to the job output tool whenever the user starts a job (e.g. by pressing
  "start muxing"). Implements #1376.
* MKVToolNix GUI: new job output and job queue feature: added a function for opening
  the output folder. Implements #1342.
* MKVToolNix GUI: new job output tool feature: added a way to clear the output,
  warnings and errors views. Implements #1356.
* MKVToolNix GUI: merge tool enhancement: added "Simple OGM-style chapter files
  (*.txt)" to the file selection dialog when selecting a chapter file. Implements
  #1269.

## Bug fixes

* mkvmerge: bug fix: track statistics tags can be kept with the option "--engage
  keep_track_statistics_tags". This allows outputting them in verbose
  identification mode for easier parsing. Fixes #1351.
* MKVToolNix GUI: bug fix: fixed various crashes when dragging & dropping in all of the
  tree views (merge tool: files view, tracks view, attachments view; chapter editor:
  edition/chapter tree, chapter name list; job queue). Fixes #1365.
* MKVToolNix GUI: merge tool bug fix: attachments: sometimes changing values didn't
  apply the changes to all selected attachments depending on how they were selected.
  Fixes #1373.
* MKVToolNix GUI: merge tool bug fix: the automatically suggested description for
  new jobs contained the file name twice, even in the directory portion. Fixes #1378.
* MKVToolNix GUI: merge tool bug fix: if "set output file name automatically" is
  enabled then file names ending with a number in parenthesis (e.g. "Berlin
  (1962).mkv") will keep their number in the generated output file name. Fixes #1375.
* MKVToolNix GUI: merge tool bug fix: the GUI will keep manual changes to the output
  file name even if "set output file name automatically" is enabled. Fixes #1372.
* MKVToolNix GUI: bug fix: fixed the stereoscopy drop down box not being
  re-translated when the GUI language is changed. Fixes #1224.
* MKVToolNix GUI: chapter editor bug fix: it was possible to drop chapter entries on
  the top-level reserved for editions. Fixes #1369.
* MKVToolNix GUI: bug fix: fixed compilation when building without curl support.
  Fixes #1359.

## Build system changes

* build system: stack protection is enabled when building with gcc on all platforms.
  For Windows DEP and ASLR is enabled. Implements #1370.
* build system: the Boost detection macros were updated from www.gnu.org resulting
  in better compatibility with bare-bones shells like dash.

## Other changes

* MKVToolNix GUI: Windows: if the application has been installed then its settings
  will no longer be saved in the registry but in an INI file in the user's data
  application folder (e.g.
  C:\Users\mbunkus\AppData\Local\bunkus.org\mkvtoolnix-gui).


# Version 8.2.0 "World of Adventure" 2015-07-18

## New features and enhancements

* MKVToolNix GUI: chapter editor enhancement: the template for chapter names can now
  contain a number of places for the chapter number, e.g. '<NUM:3>'. The number will be
  zero-padded if there are less places than specified.
* MKVToolNix GUI: new chapter editor feature: implemented a function for
  renumbering chapters. This allows the user to automatically assign new chapter
  names to one level of sub-chapters with ascending numbers. Implements #1355.
* MKVToolNix GUI: new feature: the position of the tab headers of all tab widgets can be
  changed in the preferences. Implements #1334.
* MKVToolNix GUI: new feature: added an option for hiding the tool selector.
* MKVToolNix GUI: new job queue feature: added menu options for stopping the queue
  either immediately or after the current job has finished. Implements #1303.
* MKVToolNix GUI: new job queue feature: added a context menu option for setting jobs
  to status "pending manual start".
* MKVToolNix GUI: new merge tool feature: added context menu options for selecting
  all tracks of a specific type (e.g. all audio tracks). Implements #1197.
* MKVToolNix GUI: merge tool enhancement: the dialog shown after dragging & dropping
  files from external applications asking if those files should be added or appended
  now has an option to always add and never to show that dialog again. For new MKVToolNix
  installations the default is now to show this dialog again until the user
  deactivates it either in the dialog or in the preferences.
* MKVToolNix GUI: new feature: added additional ways to move selected files, tracks
  and attachments around: keyboard shortcuts (Ctrl+Up and Ctrl+Down) and optional
  buttons (those have to be enabled in the preferences). Using drag & drop remains
  possible. Implements #1279.
* MKVToolNix GUI: new merge tool feature: dragging files from external applications
  now allows you to create new mux settings and add the dropped files to those if the
  "always add dropped files" option is off. Implements #1297.
* MKVToolNix GUI: new feature: added support for displaying the queue progress on the
  task bar button. Implements #1335.
* MKVToolNix GUI: new merge tool feature: implemented support for re-ordering new
  attachments via drag & drop. Implements #1276.
* docs: added a Spanish translation of the man pages by Israel Lucas Torrijos (see
  AUTHORS).
* MKVToolNix GUI: enhancement: several drop down boxes have had their options
  renamed slightly to be more consistent overall and easier to select via the
  keyboard. Implements #1309.
* MKVToolNix GUI: enhancement: position and size of the several additional windows
  are saved and restored. These include: the preferences window, the dialog for
  additional command line options, the dialog showing the command line and the dialog
  for selecting the playlist to add. Implements #1317.
* MKVToolNix GUI: enhancement: the relative sizes of all splitters are saved and
  restored. Implements #1306.

## Bug fixes

* mkvmerge, MKVToolNix GUI: bug fix: fixed the container type not being recognized
  properly by the GUI. Now the numerical container type ID is output in verbose
  identification mode by mkvmerge.
* MKVToolNix GUI: chapter editor bug fix: the file is not kept open so that you can open
  it in other applications at the same time.
* MKVToolNix GUI: bug fix: if a GUI language other than English was selected then the
  warning/error messages output by mkvmerge were not recognized properly and output
  in the wrong text views.
* MKVToolNix GUI: merge tool bug fix: fixed loading saved settings in which an
  appended file contains chapters/tags/attachments.
* mkvmerge: bug fix: fixed handling of MPEG transport streams where all PATs and PMTs
  have CRC errors. Fixes #1336.
* MKVToolNix GUI: bug fix: fixed the command line option used when the "fix bitstream
  timing info" check box is checked. Fixes #1337.
* MKVToolNix GUI: fix compilation with the upcoming Qt 5.5.0. Fixes #1328.
* MKVToolNix GUI: job queue bug fix: when re-starting a job the "date finished" field
  wasn't reset. Fixes #1323.
* MKVToolNix GUI: merge tool bug fix: the option "set output file name relative to
  first input file" caused the relative path to be applied each time a file was added
  resulting in the wrong directory. Fixes #1321.
* MKVToolNix GUI: merge tool bug fix: when adding a Blu-ray playlist and aborting the
  "select playlist to add" dialog the originally opened playlist was added even so.
* mkvmerge: bug fix: the MPEG-1/2 video code was causing an illegal memory access
  under certain conditions. Fixes #1217 and #1278.
* MKVToolNix GUI: bug fix: the "default subtitle character set" combo box required a
  selection without an option for using the system's default. An entry "– no selection
  by default –" has been added at the top.
* MKVToolNix GUI: bug fix: fixed parsing command line arguments to an
  already-running instance on Windows. Fixes #1322.
* MKVToolNix GUI: bug fix: fixed the combo boxes with languages, countries and
  character sets not being re-initialized after changes to the list of common
  languages/countries/character sets in the preferences. Fixes #1224.
* MKVToolNix GUI: chapter editor bug fix: fixed the menu entries "save to XML file" and
  "save to Matroska file" not being available after loading chapters until the tool or
  tab was changed. Fixes #1312.
* MKVToolNix GUI: bug fix: fixed labels and therefore the window becoming
  excessively wide with long file names. Fixes #1314.
* MKVToolNix GUI: merge tool bug fix: fixed the focus marker around combo boxes inside
  scroll areas not being drawn. Fixes #1310.
* MKVToolNix GUI: merge tool bug fix: fixed the stereoscopy mode being off by one.
  Fixes #1311.
* MKVToolNix GUI: merge tool bug fix: fixed the --append-to calculation if more than
  one file has been appended. Fixes #1313.


# Version 8.1.0 "Psychedelic Postcard" 2015-06-27

## Important notes

* mmg: bug fix: the deprecation warning will only be shown once. Fixes #1252.

## New features and enhancements

* MKVToolNix GUI: merge tool enhancement: moved the "output file name" controls
  below the three tabs so they're always visible. Also added an option in the
  preferences to move them back inside the "output" tab. Implements #1266.
* MKVToolNix GUI: new chapter editor feature: added a function for generating a
  certain number of evenly spaced sub-chapter. Implements #1291.
* MKVToolNix GUI: new chapter editor feature: implemented loading chapter entries
  from Blu-ray playlists.
* MKVToolNix GUI: job queue enhancement: added menu entries for acknowledging both
  warnings and errors at the same time.
* MKVToolNix GUI: new watch jobs tool feature: the first tab showing the output of the
  current job has been changed to show the output of all jobs that have been run since the
  GUI's been started. This can be turned off in the preferences so that only the output
  of the currently running job is shown again. Implements #1263.
* MKVToolNix GUI: new merge tool feature: added an option to set a directory relative
  to the first input file as the default output directory. Implements #1261.
* MKVToolNix GUI: new feature: added a check box to the
  track/chapters/tags/attachments list. This offers an additional way of toggling
  the "mux this" state of entries, same as the drop down box on the right and as
  double-clicking on the item already did. Implements #1277.
* MKVToolNix GUI: new feature: added an option for always using the suggested
  description and not asking the user when adding a job to the queue. Implements #1288.
* MKVToolNix GUI: job queue enhancement: added the shortcut Ctrl+R for the menu entry
  "start all pending jobs". Implements #1287.
* MKVToolNix GUI: new feature: added an option to always treat files dragged & dropped
  external applications as being added circumventing the question what to do with
  them (add, append or add as additional parts). This option is enabled by default
  changing the default behavior from release 8.0.0. Implements #1259.
* MKVToolNix GUI: new feature: added an optional action after starting a job or adding
  one to the queue. This can be either to create whole new settings or to only remove all
  input files. Implements #1254.
* Installer for Windows: enhancement: associated the .mtxcfg files with MKVToolNix
  GUI. Implements #1258.
* MIME and desktop files: enhancement: added file associations for .mtxcfg with
  MKVToolNix GUI. Implements #1258.
* MKVToolNix GUI: new feature: implemented command line handling. You can open
  configuration files, add files to merge jobs, open files in the chapter or header
  editors. Implements #1209.

## Bug fixes

* mmg: bug fix: fixed handling of the characters [ and ] in container and track
  properties.
* MKVToolNix GUI: merge tool bug fix: fixed various menu entries not working
  correctly after closing a tab or switching to another one. Fixes #1301.
* MKVToolNix GUI: merge tool bug fix: if "automatically set the file title" is enabled
  then the title field will be cleared after all source files have been removed.
* MKVToolNix GUI: merge tool bug fix: fixed a crash when enabling/disabling chapters
  coming from an appended file. Fixes #1293.
* MKVToolNix GUI: merge tool bug fix: it was possible to set the "default track flag" to
  "yes" for multiple tracks of the same type. Fixes #1289.
* MKVToolNix GUI: enhancement: the "open file" dialogs for the chapter and header
  editor tools will use the same directory that was last used in the merge tool. Fixes
  #1290.
* MKVToolNix GUI: bug fix: fixed reading the "default track" flag of tracks from added
  Matroska files. Fixes #1281.
* MKVToolNix GUI: merge tool bug fix: when appending files with multiple tracks of a
  type (e.g. multiple audio tracks) then all tracks of that kind would get assigned to
  the first track of that kind of the file they're appended to. Now the second audio
  track from the appended file is appended to the second audio track of the existing
  file, the third to the third etc. Fixes #1257.
* MKVToolNix GUI: merge tool bug fix: fixed automatic output file name re-generation
  when the mux status of tracks changes. Fixes #1253.
* mkvmerge: bug fix: fixed recognition of (E)AC-3 audio tracks using a FourCC of
  "ec-3". Fixes #1272.
* MKVToolNix GUI: merge tool bug fix: fixed attachments not being merged into the file
  in certain situations. Fixes #1260.
* MKVToolNix GUI: merge tool bug fix: fixed showing existing attachments present in
  source files in the "tracks, chapters, tags and attachments" list. Fixes #1256.
* MKVToolNix GUI: merge tool bug fix: the jobs created when appending files were
  incorrect resulting in an error message from mkvmerge. Fixes #1271.
* mkvpropedit: bug fix: fixed a warning about "edit specifications resolving to the
  same track" when changing the track properties and setting tags for the same track
  simultaneously. Fixes #1247.
* MKVToolNix GUI: merge tool bug fix: the output/destination file name is cleared
  when all files are removed. Fixes #1265.
* MKVToolNix GUI: merge tool bug fix: fixed command line escaping for empty
  arguments. Fixes #1270.
* MKVToolNix GUI: merge tool bug fix: fixed creating files without a title if one of the
  input files contains a file title. Fixes #1264.
* MKVToolNix GUI: bug fix: if the job removal policy is set to "remove even if there were
  warnings" then jobs that were muxed without warnings weren't removed. Fixes #1262.
* Build system: fixed inclusion of desktop files for the two GUIs for Debian/Ubuntu
  packages. Fixes #1255.


# Version 8.0.0 "Til The Day That I Die" 2015-06-19

## New features and enhancements

* MKVToolNix GUI: merge tool enhancement: drag & drop of files works even if no mux
  settings are currently open. Implements #1245.
* MKVToolNix GUI: job output enhancement: when clicking the "abort" button the GUI
  asks for confirmation before aborting. Both this check and the one when quitting the
  application can be turned off via an option in the preferences. Implements #1238.
* MKVToolNix GUI: new merge tool, header & chapter editor features: the GUI will ask
  for confirmation before closing or reloading tabs that have been modified and
  before quitting if there are modified tabs. This check can be disabled in the
  preferences. Implements #1211.
* MKVToolNix GUI: new merge tool feature: implemented an option that allows the user
  to set up a list of languages. When adding files only those tracks whose language is
  included in that list are set to be muxed by default. Implements #1227.
* MKVToolNix GUI: new feature: added an option in the preferences for disabling
  additional lossless compression for all track types. Implements #1174.
* mkvmerge, MKVToolNix GUI: new feature: added an option ("--engage
  keep_last_chapter_in_mpls") that will cause mkvmerge not to remove the last
  chapter entry from a Blu-ray play list file which mkvmerge normally does if that
  entry's timecode is within five seconds of the movie's end. Implements #1226.
* MKVToolNix GUI: new watch jobs tool feature: implemented estimating the remaining
  time for both the current job and the whole queue.
* MKVToolNix GUI: enhancement: the following dialogs can now be maximized: the
  "preferences" dialog; the "additional command line options" dialog; the dialog
  showing the command line; the dialog where the user selects the play list to add.
  Implements #1231.
* MKVToolNix GUI: merge tool enhancement: pressing delete when the source files view
  is focused will cause the selected source files to be removed. Implements #1225.
* MKVToolNix GUI: merge tool enhancement: implemented toggling of "mux this" for all
  selected tracks by either double-clicking on the tracks or pressing enter/return
  when the tracks view is currently focused. Implements #1225.
* MKVToolNix GUI: job queue enhancement: added a menu entry for starting all jobs
  pending manual start. Implements #1228.
* MKVToolNix GUI: enhancement: the text in all message box dialogs can now be selected
  & copied, even on Windows. Implements #1230.
* all: the detection whether or not the applications are installed on Windows is done
  by checking for the presence of a special file in the program folder instead of
  checking for an entry in the registry written by the installer. This enables users to
  try new portable versions without having to uninstall an installed version first as
  their settings will be kept separate now. A side effect is that compatibility with
  Windows XP should be restored. Implements #1229.
* mkvmerge: enhancement: if running in GUI mode (parameter "--gui-mode") then the
  progress will be output as the untranslated "#GUI#progress …%" in order to
  facilitate parsing of progress by GUIs.
* MKVToolNix GUI: new feature: added a "help" menu with links to several parts of the
  MKVToolNix documentation. Implements #1195.
* MKVToolNix GUI: job output tool: added a button for acknowleding the
  warnings/errors produced for the job shown. Implements #1210 and is the last part of
  the implementation of #1196.
* MKVToolNix GUI: enhancement: header editor: made the meaning of the "Reset" button
  clearer with a better label and an additional tool tip. Implements #1212.
* MKVToolNix GUI: enhancement: the tabs for the tools that haven't been implemented
  yet (extraction, info and the tag editor) are not shown anymore.
* MKVToolNix GUI: enhancement: the update check dialog showing the change log can now
  be maximized. Implements #1204.
* MKVToolNix GUI: new feature: implemented viewing the output of any job in the job
  queue.
* MKVToolNix GUI: new feature: implemented saving the job output to a file.
* MKVToolNix GUI: enhancement: language and country drop-down boxes will contain
  the common languages/countries both at the top as well as in the full list. Part of the
  implementation of #1200.
* MKVToolNix GUI: enhancement: the entry "Undefined (und)" is always shown at the top
  of the language drop-down boxes. Part of the implementation of #1200.
* MKVToolNix GUI: enhancement: the number of new warnings and errors are shown in the
  status bar. Both counters can be acknowledged via context menus on the status bar and
  in the job queue view. Part of the implementation of #1199.
* MKVToolNix GUI: enhancement: the number of jobs pending automatic/manual
  execution is listed in the status bar. Part of the implementation of #1199.
* MKVToolNix GUI: enhancement: the progress widget in the stats bar is not reset to 0
  once all the jobs have been processed in order to signal the user that the jobs have
  actually been processed. Part of the implementation of #1198.
* MKVToolNix GUI: merge tool enhancement: a short animation of a moving icon is shown
  when a job is started or added to the job queue as a clue to the user what's happening and
  where to look for output. This animation can be disabled in the preferences.
  Implements #1198.
* MKVToolNix GUI: chapter editor enhancements: when selecting a chapter the the
  chapter name closest to the previously selected chapter name (or the first if there
  wasn't a previously selected one) is selected automatically.
* MKVToolNix GUI: chapter editor enhancements: when starting a new file a single
  edition and a single chapter are added automatically.
* MKVToolNix GUI: merge tool enhancement: the "add files" button has been re-labeled
  "add source files" in order to make it clearer that it cannot be used for adding
  attachments, even if the attachments tab is the currently selected tab.
* MKVToolNix GUI: merge tool & job queue tool enhancement: short tool tips will be
  shown for the files, tracks, attachments and jobs views telling the user to
  right-click for adding files and similar actions.
* MKVToolNix GUI: new feature for the merge tool, the header and chapter editors: if no
  file is open then "new" and "open file" buttons are shown.

## Bug fixes

* MKVToolNix GUI: improved locating the mkvmerge executable on non-Windows
  systems. Fixes #1246.
* MKVToolNix GUI: chapter editor bug fix: dragging & dropping a file onto the chapter
  editor that cannot be parsed as chapters was causing a confusing warning about
  changed chapters not being saved.
* MKVToolNix GUI: merge tool bug fix: fixed several controls not changing their
  language correctly when the interface language is changed.
* mkvmerge: bug fix: fixed codec identification for MP2 audio read from MPEG
  program/transport streams. Fixes #1242.
* MKVToolNix GUI: job output bug fix: fixed displaying the estimated remaining time
  in tabs that have been opened for specific jobs. Fixes #1244.
* MKVToolNix GUI: header editor bug fix: fixed the editor assuming values were
  changed if a track is present whose language element is not present in the file. Fixes
  #1240.
* MKVToolNix GUI: bug fix: fixed the translation of the tool tip for the "close tab"
  buttons after changing the interface language. Fixes #1237.
* MKVToolNix GUI: bug fix: appended tracks will be disabled automatically when
  starting to mux if the track they're appended to has been disabled by the user.
* MKVToolNix GUI: fixed updating the number of pending jobs info in the status bar when
  manually starting jobs. Fixes #1236.
* MKVToolNix GUI: merge tool bug fix: fixed a crash when removing source files. Fixes
  #1235.
* MKVToolNix GUI: merge tool bug fix: the default settings for the "output directory
  policy" was changed to "same directory as the first input file". Fixes #1234.
* MKVToolNix GUI: bug fix: on Windows the job queue was accidentally always saved to
  and loaded from the registry even if the portable version was used.
* MKVToolNix GUI: job queue bug fix: fixed accidental duplication of lines when using
  drag & drop in certain ways. Fixes #1221.
* MKVToolNix GUI: bug fix: if a job is running when the user wants to quit requires
  confirmation from the user that the running job should be aborted. Fixes #1219.
* MKVToolNix GUI: bug fix: fixed the initial status display when viewing a job's
  output from the queue.
* MKVToolNix GUI: bug fix: running jobs cannot be removed from the job queue anymore.
  Fixes #1220.
* MKVToolNix GUI: bug fix: when starting the GUI old jobs from the queue were silently
  discarded if they included additional parts (e.g. VOBs).
* MKVToolNix GUI: bug fix: job queue: when saving the job queue jobs removed in the GUI
  were not removed from the stored settings.
* MKVToolNix GUI: bug fix: when viewing the job output of a job that hasn't been run yet
  the "save output" button was enabled.
* mkvmerge, mkvpropedit: bug fix: fixed an invalid memory access leading to a crash in
  the Base 64 decoder. Fixes #1222.
* MKVToolNix GUI: bug fix: fixed progress parsing for interface languages other than
  English.
* mkvmerge: bug fix: fixed key frame designation for video tracks in MP4 DASH files.
* mkvmerge: bug fix: the track statistics tags of Matroska source files are always
  discarded, no matter whether or not they're to be created for the output file. That
  way they won't be reported as track tags by mkvmerge's identification mode. This
  makes it easier for the user to create output files without track statistics tags as
  (s)he only has to use the option "--disable-track-statistics-tags" and not
  disable all the track tags as well anymore. Fixes #1186.
* mkvmerge, mkvinfo, mkvextract: bug fix: fixed a crash with certain types of invalid
  Matroska files. Fix for #1183.
* all: bug fix: removed some unused code thereby fixing compilation on OpenBSD
  (#1215).
* MKVToolNix GUI: bug fix: fix alignment of the tool contents with the tool selector at
  the bottom. Fixes #1194.
* MKVToolNix GUI: bug fix: header editor: fixed the track language shown if the
  element is not present in the file.
* MKVToolNix GUI: bug fix: when browsing the output file name the currently entered
  file name is pre-selected in the dialog. Fixes #1207.
* MKVToolNix GUI: job output bug fix: fixed superfluous empty lines in job
  output/warning/error output.
* MKVToolNix GUI: merge tool bug fix: when adding a VOB from a DVD the tree items for the
  additional parts (the other VOBs processed automatically) weren't shown.
* MKVToolNix GUI: merge tool: fixed the scroll bar not disappearing in the input tab if
  the window is high enough. Fixes #1193.
* build system: desktop files and icons were only installed if wxWidgets was enabled.
  Fixes #1188.
* man pages: clarify functionality of --default-duration. Fixes #1191.
* build system: bug fix: fixed running rake if no locale or one with an encoding other
  than UTF-8 is set. Fixes #1189.

## Other changes

* MKVToolNix GUI: merge tool: the "save" button has been removed in favor of the
  "save…" menu entries. This also improves consistency with the other tools.


# Version 7.9.0 "Birds" 2015-05-10

## New features and enhancements

* MKVToolNix GUI: new feature: added context menu entries for tracks: "select all",
  "enable all" and "disable all".
* MKVToolNix GUI: new feature: implemented aborting the currently running job.
* MKVToolNix GUI: new feature: implemented the "additional command line options"
  dialog.
* MKVToolNix GUI: new feature in the chapter editor: added features "set the
  language/country of the selected chapter and its sub-chapters".
* MKVToolNix GUI: new feature in the chapter editor: added features "clamping time
  stamps of sub-chapters to their parent's time stamps", "expanding time stamps of
  chapters to encompass their sub-chapters' time stamps" and "shifting start and end
  time stamps by an offset".
* MKVToolNix GUI: new feature: the main window's size, position and state
  (maximized/minimized) is saved on exit and restored on startup.
* MKVToolNix GUI: new feature: the GUI is now fully translatable. The German
  translation has been completed for the GUI, too.
* MKVToolNix GUI: new feature: implemented changing the interface language.
* MKVToolNix GUI: enhancement: implemented often used subtitle character sets.
* MKVToolNix GUI: enhancement: implemented setting a user configurable subtitle
  character set by default.
* MKVToolNix GUI: enhancement: implemented the automatic removal of jobs
  configurable by the user.
* mkvmerge: enhancement for MPEG program stream handling: mkvmerge will only look
  for additional files automatically if the source file begins with "VTS_…" and just
  if it ends in a number. E.g. when reading "video_1.mpg" another file called
  "video_2.mpg" will no longer be read automatically. Implements #1164.
* MKVToolNix GUI: enhancement: display country names in addition to country codes.
* MKVToolNix GUI: enhancement: implemented often used languages and country codes.
* MKVToolNix GUI: chapter editor enhancement: implemented defaults for the
  language and country settings for newly created chapter names.
* MKVToolNix GUI: merge tool enhancement: added controls for mkvmerge's "reduce
  audio to its core" and "force NALU size length" features.
* MKVToolNix GUI: new feature: implemented the "Preferences" dialog (not all of the
  functionality the options refer to has been implemented yet, though).
* MKVToolNix GUI: merge tool enhancement: Implemented adding and append files and
  adding files as additional parts via drag & drop from external applications.
* MKVToolNix GUI: chapter editor enhancement: implemented opening files via drag &
  drop from external applications.
* MKVToolNix GUI: enhancement: The portable Windows version will store its settings
  in a file in the same folder instead of the registry.
* MKVToolNix GUI: enhancement: Qt's "Windows Vista" style is now used on Windows
  instead of the old, Windows 98-like "Windows" style.
* MKVToolNix GUI: merge tool enhancement: The merge tool has been re-written to be
  tabbed like the header and chapter editors allowing for multiple merge job settings
  to be open at the same time.
* MKVToolNix GUI: chapter editor enhancement: implemented re-ordering chapters
  and editions with drag & drop.
* MKVToolNix GUI: new feature: implemented the chapter editor.
* MKVToolNix GUI: merge tool enhancement: implemented adding attachments via drag &
  drop from external applications.
* MKVToolNix GUI: new feature: implemented the header editor.
* mkvmerge: enhancement: Implemented proper type output during identification for
  DTS-ES (extended surround) tracks. Implements #1157.
* MKVToolNix GUI: new feature: implemented the online update check.
* mkvmerge: new feature: Implemented support for the DTS-HD container format.
* mkvmerge: new feature: Implemented support for core-less DTS streams consisting
  solely of XLL extension sub-streams.
* mkvmerge: new feature: track selection can be done by language codes as well.
  Affects the options --audio-tracks, --button-tracks, --subtitle-tracks and
  --video-tracks. Works only for containers that actually provide a language tag.
  Implements #1108.

## Bug fixes

* mkvinfo: bug fix: mkvinfo would exit with the wrong return code (0 instead of 2) if a
  non-existing file name had been given. Fixes #1182.
* all: fix compilation on Mac OS in common/command_line.cpp due to
  boost::range::filtered requiring a copyable functor. Fixes #1175.
* all: fix compilation on Mac OS in common/version.cpp due to wrong usage of
  std::stringstream and ostream operators. Fixes #1176.
* all: bug fix: fixed compilation with Boost 1.58.0. Fixes #1172.
* mkvmerge: bug fix: Fixed a segmentation fault during cleanup after Ctrl+C was
  pressed. Fixes #1173.
* mkvmerge: bug fix: fixed --sync not doing anything if --default-duration is used
  for the same track, too.
* mkvmerge: bug fix: fixed aborting file identification with an error message about
  "aac_error_protection_specific_config" that happened for some files. Fixes
  #1166.
* mkvmerge: bug fix: fixed specifying track properties like language or name for AC-3
  cores embedded in TrueHD tracks when they're read from raw thd+ac3 files. Fixes
  #1158.
* mkvmerge: bug fix: MPEG-1/2 parser: fixed a long-standing issue that prevented
  mkvmerge from recognizing certain MPEG-1/2 video tracks and files if the frame's
  sequence numbers didn't follow a certain expected pattern. Fixes #1162 and
  probably others like #1145 or #1099.
* MKVToolNix GUI: merge tool bug fix: fixed the column headers on the "attachments"
  tab.
* mkvmerge: bug fix: The calculation of the width and height of h.265/HEVC video
  tracks did not take the conformance window (cropping) into account. Fixes #1152.
* mkvmerge: bug fix: Fixed the value of the DocTypeVersion header field if any of the
  Matroska elements CodecDelay, DiscardPadding or SeekPreRoll is used. This is the
  case for Opus tracks.
* mkvmerge: bug fix: Fixed the handling of E-AC-3 tracks in M2TS files if the AC-3 core
  and the extension are stored in separate packets.
* source code: bug fix: Accidental uses of the "long double" type have been converted
  to normal "double"s. This fixes compilation on platforms which don't support the
  "long double" type in combination with Boost::Math. Fixes #1150.

## Build system changes

* build system: configure will now check for Qt by default. If at least v5.2.0 is found
  then the Qt versions of mkvinfo's GUI and the new mkvtoolnix-gui will be enabled. You
  can affect this detection with the options --disable-gui (turns off all GUIs; works
  the same as before), --disable-qt (will compile the wxWidgets GUI for mkvinfo and
  mmg) and --without-mkvtoolnix-gui (will compile the Qt version of mkvinfo's GUI
  but no mkvtoolnix-gui).

## Other changes

* mkvmerge: Two more characters are now escaped in the container and track properties
  output in verbose identification mode: [ is replaced with \b and ] with \B. This is
  needed for reliable parsing by other programs, e.g. GUIs.
* all: permanently removed the build times tamp from the version information and the
  corresponding configure option.


# Version 7.8.0 "River Man" 2015-03-27

## New features and enhancements

* mkvmerge: enhancement: File type identification will output a more detailed
  description of the DTS type for DTS audio tracks (DTS-HD Master Audio, DTS-HD High
  Resolution, DTS Express or just plain DTS). Implements #1109.
* mkvmerge: new feature: Implemented support for DTS Express.
* all: new feature: added a Swedish translation of the programs by Kristoffer
  Grundström (see AUTHORS).
* mkvinfo (Qt interface): enhancement: implemented support for opening files via
  drag & drop.
* mkvmerge: enhancement: added an option (--engage no_delay_for_garbage_in_avi)
  for disabling deriving a delay from garbage in audio tracks in AVI files. Requested
  in #1137.

## Bug fixes

* mkvmerge: bug fix: HEVC tracks which did not have an aspect ratio present in their
  sequence parameter set were copied incorrectly; the resulting sequence parameter
  set was invalid. Fixes #1081.
* mkvextract: bug fix: When extracting HEVC tracks mkvextract will use the same start
  code lengths that x265 uses (four bytes 0x00000001 for the first and
  video/picture/sequence parameter set NALs and three bytes 0x000001 for all
  others).
* mkvmerge: bug fix: The number of channels in DTS tracks with more than six channels is
  now recognized correctly by parsing the DTS HD extensions, too. Fixes #1139.
* mkvmerge: bug fix: Fixed handling of the BITIMAPINFOHEADER extra data size
  handling during merging and extraction for codecs like HuffYUV.
* mkvmerge: bug fix: When appending unframed HEVC/h.265 tracks and setting the
  default duration the second and all following source parts will use the same default
  duration as set for the first part. Fixes #1147.
* mkvmerge: bug fix: enabled the use of tags in WebM files. Tagging elements not
  supported by the WebM specs are removed. Fixes #1143.
* mkvmerge: bug fix: fixed detection of audio tracks in QuickTime files whose FourCC
  code is unknown to mkvmerge.
* mkvmerge: bug fix: fixed detection of video tracks in QuickTime files whose FourCC
  code is unknown to mkvmerge.
* mkvextract: bug fix: Fixed VobSub file naming when mkvextract is built against
  Boost::Filesystem older than 1.50.0. Fixes #1140.
* mkvmerge: bug fix: fixed detection of Cinepak video tracks in QuickTime files.
* mkvmerge: bug fix: fixed detection of PCM audio tracks in QuickTime files using the
  "raw " FourCC.
* mkvmerge: bug fix: fixed detection of tracks in Flash Video files for which the
  headers do not signal a track.
* mkvmerge: bug fix: fixed a segfault in the Flash Video file format reader.
* mkvmerge: bug fix: Fixed file type detection for MP3 files with big ID3 tags at the
  start of the file (e.g. if they contain cover images).
* mkvmerge: bug fix: The formula used for calculating the audio delay for garbage data
  at the start of tracks in AVI files has been fixed. Fixes #1137.

## Build system changes

* build system: Boost's "Math" library is now required.

## Other changes

* mmg: The change making the window wider by default has been reverted.


# Version 7.7.0 "Six Voices" 2015-02-28

## Important notes

* mkvmerge: removal: AAC: The hack for using the old codec IDs (e.g.
  A_AAC/MPEG4/LC/SBR) for AAC tracks has been removed. Those codec IDs have been
  deprecated for nearly ten years. Reading files that use those IDs will stay
  supported.

## New features and enhancements

* mmg: new feature: added a button "toggle all" that enables or disables all tracks. It
  at least one track is currently disabled then all tracks are enabled when pressing
  that button. Otherwise (if all tracks are currently enabled) then they will all be
  disabled. Implements #1130.
* mmg: new feature: added a new checkbox "reduce to audio core" on the
  "format-specific options" tab that passes the new --reduce-to-core option to
  mkvmerge if enabled. Part of the implementation of #1107.
* mkvextract: new feature: implemented a mode for extracting cue information.
* mkvmerge: enhancement: The code for determining the time codes of AAC, AC-3, DTS,
  MP3 and TrueHD packets has been completely rewritten. This improves how timecodes
  are kept if the source container provides them in many cases.
* mkvmerge: new feature: Added an option "--reduce-to-core" that tells mkvmerge not
  to copy HD extensions for DTS tracks. Part of the implementation of #1107.
* mkvmerge: new feature: mkvmerge will now recognize TrueHD tracks inside MPEG
  transport streams that contain an AC-3 core as consisting of two tracks. Instead of
  always dropping the AC-3 part the user can simply select which tracks to keep. Part of
  the implementation of #1107.
* mkvmerge: new feature: mkvmerge will now recognize TrueHD+AC-3 files as
  consisting of two tracks. Instead of always dropping the AC-3 part the user can
  simply select which tracks to keep. Part of the implementation of #1107.

## Bug fixes

* source code: Fixed the compilation on cygwin.
* documentation: The Dutch, Ukrainian and Chinese (Simplified) manual pages have
  contained only untranslated English strings since release 7.0.0. This was due to
  the files holding the translatable strings having being corrupted by a misbehaving
  tool in the build process. This has been rectified. Fixes #1134,
* mkvmerge: bug fix: Fixed reading all of the private codec data in AVIs from the 'strf'
  chunk for codecs that don't set biSize to include that data. Fixes #1129.
* mkvextract: bug fix: Fixed writing AVIs with ckSize fields that were too large.
  Fixes #1128.
* mkvmerge: bug fix: fixed determining the key frame status in certain AVIs (those
  whose dwFlags index field has more bits set than just 0x10).
* mkvinfo (Qt interface): bug fix: added WebM extensions to the known types in the
  "Open file" dialog.
* mkvextract: bug fix: Fixed writing AVIs with the wrong bit depth for video codecs
  that don't use 24 bits/pixel. Fixes #1123.
* mkvmerge: bug fix: Fixed recognition of E-AC-3 audio tracks in MPEG transport
  streams if they use the type ID 0xa1 (and the same for DTS tracks stored with type ID
  0xa2). Fixes #1126.
* mkvextract: bug fix: Fixed VobSubs being written to the wrong directory if the
  output file name given by the user didn't have an extension but one of the directories
  contained a dot. Fixes #1124.
* mkvpropedit, mmg's header editor: bug fix: if updating the file required creating
  an EBML void for a 130 bytes long gap then the void element created was one byte too
  short resulting in an invalid file structure. Fixes #1121.
* mkvmerge: bug fix: If the MP4 track headers for MP3 tracks contain invalid values
  (number of channels is 0 or the sampling rate is 0) then mkvmerge will re-derive these
  parameters from the MP3 bitstream instead of ignoring that track.
* mkvmerge: bug fix: Matroska reader: track-specific tags weren't copied for tracks
  for which the pass-through packetizer was used (e.g. those with the codec ID
  A_MS/ACM) instead of a specialized one.

## Build system changes

* build system: new feature: added configure options for building
  statically-linked binaries (--enable-static). Patches by Florent Thiéry.
  Implements #1119.

## Other changes

* mkvmerge: removal: TrueHD: The hack for merging a sync frame and all following
  normal frames into a single Matroska packet has been removed as there are no players
  that can play such merged frames anyway.


# Version 7.6.0 "Garden of Dreams" 2015-02-08

## New features and enhancements

* all: new feature: added a Serbian (Latin) translation of the programs by Danko (see
  AUTHORS).

## Bug fixes

* all programs: bug fix: Since release 7.0.0 the wrong exit code was used when warnings
  were finished (0 instead of 1). Fixes #1101.
* mkvmerge: bug fix: Appending chapters with the same chapter UID was dropping all
  sub-chapters from the one of the two merged chapters. Now the sub-chapters are
  merged recursively as well.
* mkvmerge: bug fix: The wrong Codec ID was written when reading PCM tracks from
  Matroska files in Big Endian byte order. Fixes #1113.
* mkvmerge: bug fix: If splitting was active and AC-3 tracks read from Matroska files
  were shorter than a split point then the following output file would contain an AC-3
  packet with the timecode of 00:00:00 somewhere in the middle. Fixes #1104.
* mmg: bug fix: If a chapter track from a Matroska file is selected then the "language"
  drop-down box is disabled. Fixes #1105.
* mkvmerge: bug fix: On Windows the end-of-file-reached status wasn't tracked
  correctly for certain file operations. This could manifest in e.g. mkvmerge not
  finding tracks in MPEG transport streams when probing MPLS playlist files. Fixes
  #1100.
* mmg: bug fix: When scanning for play lists the window presenting the results listed
  some properties in an unescaped way (e.g. "\s" instead of spaces).
* mmg: bug fix: When adding MPLS files mmg was only offering to scan for more playlists
  if there were at least two additional MPLS files present. This has been fixed to one
  MPLS file (in addition to the one just added).


# Version 7.5.0 "Glass Culture" 2015-01-04

## New features and enhancements

* mkvmerge: new feature: implemented support for MP4 DASH files. Implements #1038.
* mkvmerge: new feature: implemented reading MPEG-H p2/HEVC video tracks from MP4
  files. Implements #996.
* all: enhancement: improved exception messages that can occur when reading damaged
  Matroska files to make it clearer for the user what's happening. See #1089.
* mkvmerge: new feature: Added support for reading h.265/HEVC video tracks from MPEG
  transport streams. Implements #995.

## Bug fixes

* mkvmerge: bug fix: If the target drive is full then a nicer error message is output
  instead of simply crashing due to an uncaught exception.
* mkvmerge: bug fix: Fixed reading MPEG transport streams in which all PATs and/or
  PMTs have CRC errors. Fixes #1100.
* all: bug fix: Re-wrote the whole checksum calculation code. This lead to a fix for the
  Adler-32 checksum algorithm that was triggered under certain circumstances.
  Adler-32 is used in mkvinfo's output (e.g. in summary mode or if checksums are
  activated), in the h.265/HEVC bitstream and TrueAudio (TTA) file headers.
* mkvmerge: bug fix: fixed handling of HE-AACv2 with object type "parametric
  stereo".
* mkvinfo: bug fix: track statistics: the duration (and therefore the estimated
  bitrate) was wrong for files in which the frame with the maximum timecode wasn't the
  last frame in the file. Fixes #1092.
* mkvmerge: new feature: implemented support for AAC in LOAS/LATM multiplex if read
  from MPEG transport streams or raw LOAS/LATM AAC files. Implements #877 and fixes
  the underlying issue in #832.
* all: bug fix: several fixes have gone into libEBML and libMatroska that prevent
  illegal memory access (both reading from and writing to unallocated addresses).
  The bugs #1089 and #1096 have thus been fixed.
* mkvinfo: bug fix: mkvinfo will abort with a proper error message if the first element
  found is not an EBML head element. See #1089.
* mkvinfo: bug fix: Timecodes output with ms resolution are now rounded to ms instead
  of simply cut off. Fixes #1093.

## Build system changes

* build system: libEBML and libMatroska have been changed to provide pkg-config
  configuration files. Therefore MKVToolNix' build system has been switched to look
  for both libraries via pkg-config.
* build system: libMatroska v1.4.2 is now required as part of a fix for #1096.
* build system: libEBML v1.3.1 is now required as a part of a fix for #1089.


# Version 7.4.0 "Circles" 2014-12-12

## New features and enhancements

* all: new feature: added a Catalan translation of the programs by Antoni Bella Pérez
  (see AUTHORS).

## Bug fixes

* mkvmerge: bug fix: mkvmerge was sometimes dropping lines from teletext subtitles
  read from MPEG transport streams. See #773.
* mkvmerge: bug fix: The PCM packetizer was producing wrong track statistics by
  disregarding the last packet's duration when reading PCM data from packaged
  sources (Matroska, MP4 files). Fixes #1075.
* build system: enhancement: configure will look for a system version of the pugixml
  library and use that instead of the bundled version if it is found. Fixes #1090.
* mkvextract: bug fix for chapter & tag extraction: If locale is set to a non-UTF locale
  (including C or POSIX) then no XML data was output at all even if the XML data contained
  ASCII characters only. Fixes #1086. This also fixes mkvextract writing two BOMs
  when extracting tags with the "--redirect-output" option on Windows.
* mkvinfo: bug fix: summary mode: reported frame types in block groups are now derived
  from the number of references found and not by the references' values.
* mkvmerge: bug fix: Fixed muxing open GOPs after I frames in MPEG-1/2 video (patch by
  Stefan Pöschel). Fixes #1084.
* mmg: bug fix: VP9 video tracks are accepted in WebM mode.
* mkvmerge: bug fix: Cherry-picked several commits from DivX' mkvmerge fork for
  improved HEVC handling. Fixes #1076.
* mkvmerge: bug fix: Fixed the handling of Big Endian PCM tracks read from MP4 files.
  Fixes #1078.


# Version 7.3.0 "Nouages" 2014-10-22

## New features and enhancements

* mkvmerge: new feature: implemented support for reading teletext subtitles from
  MPEG transport streams. They're converted to SRT-style subtitles (CodecID
  S_TEXT/UTF8). Implements #773.
* MKVToolNix GUI: implemented drag & drop in the files pane.
* MKVToolNix GUI: implemented drag & drop in the track pane.
* mkvmerge: new feature: added support for PCM in MPEG program streams (.vob – DVDs)
  and transport streams (.ts, .m2ts – Blu-rays). Implements #763.
* MKVToolNix GUI: implemented drag & drop in the job queue.
* MKVToolNix GUI: implemented storing the job queue when the application exits and
  retrieving it when it starts again.
* MKVToolNix GUI: implemented setting the file title automatically from added files
  that already have a title.

## Bug fixes

* mkvmerge: bug fix: probing MPEG transport streams with certain types of broken
  MPEG-2 inside caused mkvmerge to exit with an error message. Such tracks are now
  ignored instead.
* mkvmerge, mmg's chapter editor: fixed the default value for the "language" element
  if it isn't present in a chapter XML file.
* mkvinfo (Qt version on Windows): bug fix: the console window is closed if the GUI is
  launched.
* mkvmerge: bug fix: Reading tracks from MPEG transport streams resulted in the track
  being cut off at points with a five minute gap in between frames. It is due to timecode
  wrap detection introduced in v6.9.0. As it affects subtitles the most the wrap
  detection has been relaxed for them.
* MKVToolNix GUI: fixed missing command line switch for audio sync/stretch.
* Installer: bug fix: the shortcut for the GUI preview on the desktop is removed upon
  uninstallation. If the user opts not to have shortcuts on the desktop then no
  shortcut is created for the GUI preview either.
* MKVToolNix GUI: fixed clearing the file/track/attachment lists when starting a
  new config or when loading an existing one.
* MKVToolNix GUI: implemented setting the output file name automatically in four
  different modes (don't set at all; place in previous output directory; place in
  fixed output directory; place in parent directory of first source file) with an
  option to make them unique by appending a running number.


# Version 7.2.0 "On Every Street" 2014-09-13

## Bug fixes

* mkvmerge: bug fix: Fixed calculating AC-3 delay from garbage data when reading AC-3
  from AVIs. This stopped working in release 5.4.0 due to commit 97cc2121.
* mkvextract: bug fix: SSA/ASS files with sections after "[Events]" in their
  CodecPrivate are now handled correctly. Fixes #1057.
* mkvmerge: bug fix: Fixed handling certain edit list types in MP4 files that are used
  for positive track delays. Fixes #1059.
* source: Fixed compilation with Boost 1.56.0 which changed the "indexed" range
  adaptor in an incompatible way.
* mkvpropedit, mmg's header editor: bug fix: when editing files with missing track
  UID elements such an element will be generated automatically instead of crashing
  and leaving the file in an unplayable state. Part of a fix for #1050.
* mkvmerge: bug fix: Reading Matroska files with missing track UID elements will no
  longer cause mkvmerge to abort with an error. A warning is printed and a new unique
  track ID generated instead. Part of a fix for #1050.

## Other changes

* MKVToolNix GUI: included a first preview version in the Windows installer and
  portable releases.


# Version 7.1.0 "Good Love" 2014-07-27

## New features and enhancements

* mkvmerge: enhancement: SSA/ASS: in addition to semicolons comments can now start
  with exclamation marks, too.

## Bug fixes

* all: bug fix: Fixed file seeking code for "seek relative to end of file" case. Fixes
  #1035.
* mmg: bug fix: Selecting the root of the chapter editor tree will disable the
  language/country inputs properly as changing those fields doesn't make sense for
  the root.
* all: bug fix: if MKVToolNix on Windows is residing in a directory containing
  non-ASCII characters then translations weren't found. This has only been fixed for
  cases where those non-ASCII characters are part of the system's active code page.
* mkvmerge: bug fix: track statistics tags are not written for WebM files anymore as
  the WebM specification doesn't allow tags.
* mkvmerge: bug fix: Fixed wrong default duration for PCM audio tracks if the source
  file provides timecodes for that track. Fixes #1001 and #1033.
* mkvextract: bug fix: Fixed a crash when opening damaged/invalid Matroska files in
  all extraction modes. Fixes #1027.


# Version 7.0.0 "Where We Going" 2014-06-09

## New features and enhancements

* mkvmerge: enhancement: In addition to the track statistics tags "BPS",
  "DURATION", "NUMBER_OF_BYTES" and "NUMBER_OF_FRAMES" mkvmerge will write two
  more tags identifying which application wrote the statistics
  ("_STATISTICS_WRITING_APP") and when the file in question was written:
  "_STATISTICS_WRITING_DATE_UTC". "_STATISTICS_WRITING_APP" will always
  contain the same string contained in the segment info header element "WritingApp".
  "_STATISTICS_WRITING_DATE_UTC" will contain the same timestamp as in the segment
  info header element "Date", though "_STATISTICS_WRITING_DATE_UTC" is actually a
  string representation instead of an integer value. Additionally a tag named
  "_STATISTICS_TAGS" is written containing the names of the tags that mkvmerge has
  set automatically. It equals the following currently: "BPS DURATION
  NUMBER_OF_BYTES NUMBER_OF_FRAMES".
* mkvmerge: new feature: Added a global option for disabling writing the tags with
  statistics for each track: --disable-track-specific-tags.
* mkvmerge: new feature: When identifying a Matroska file in verbose identification
  mode track-specific tags will be output as well. The format is "tag_<tag name in
  lower case>:<tag value>", e.g. for a tag named "BPS" with the value "224000" the
  output would be "tag_bps:224000". Enhancement for #1021.
* mkvmerge: new feature: mkvmerge will write track-specific tags with statistics
  ("BPS" for the average number of bits per second, "DURATION" for the duration,
  "NUMBER_OF_BYTES" and "NUMBER_OF_FRAMES" for the track's size in bytes and its
  number of frames/packets). Implements #1021.
* mmg: enhancement: The chapter editor will only use fast-mode parsing when loading
  chapters from Matroska files.
* mkvmerge: enhancement: The last chapter entry read from MPLS files is removed if it
  is at most five seconds long. Patch by Andrew Dvorak (see AUTHORS).
* mkvmerge: enhancement: added the attachment UID to the verbose identification
  output of Matroska files.
* mmg: enhancement: the subtitle character set cannot be set anymore for subtitle
  tracks read from Matroska files as mkvmerge ignores that setting for said container
  anyway (text subs are always encoded in UTF-8 in Matroska).
* mmg: enhancement: mmg will look for the "mkvmerge" executable in the same directory
  as the "mmg" executable is located it if the location hasn't been set by the user on all
  operating systems (before: only on Windows). Improves detection if "mkvmerge" is
  not in the $PATH.

## Bug fixes

* mkvmerge: bug fix: If a single subtitle track contains two or more entries at the same
  timecode then the cue duration and cue relative position elements written were
  wrong.
* mkvinfo: bug fix: fixed wrong progress percentage shown during saving the
  information to text files. Fixes #1016.
* mkvmerge: bug fix: Changed the file type detection order again. The text subtitle
  formats are now probed after those binary formats that can be detected quickly and
  unambiguously. This avoids some mis-detection if e.g. Matroska files as ASS text
  subtitles if they do contain such a track.
* all: bug fix: fixed invalid memory access in the cleanup procedures which only
  occurred if the output was redirected with the "--redirect-output" command line
  parameter.
* mmg: bug fix: Selecting a subtitle track correctly sets the "character set"
  drop-down box if no character set was set for this track. Fixes #1008.

## Build system changes

* build system: Boost's "date/time" library is now required.


# Version 6.9.1 "Blue Panther" 2014-14-18

## Bug fixes

* mkvmerge: bug fix: fixed huge memory usage when probing files (it was reading the
  whole file into memory for that).


# Version 6.9.0 "On Duende" 2014-04-18

## New features and enhancements

* all: new feature: added a Brazilian Portuguese translation of the programs by
  Thiago Kühn (see AUTHORS).
* mkvmerge: enhancement: improved file type detection speed for text subtitle
  formats.
* mkvmerge: enhancements: trailing zero bytes will be removed from AVC/h.264 NALUs.
  Implements #997.

## Bug fixes

* mkvpropedit, mmg's header editor: bug fix: fixed a failed assertion in libEBML when
  writing the same changes twice to certain files (those for which a seek head with a
  single entry pointing to the elements modified by mkvpropedit/mmg's header
  editor; e.g. x264 creates such files). Fixes #1007.
* mkvmerge: bug fix: reading fonts embedded in SSA/ASS files was sometimes
  truncating the attachments created from them. Fixes #1003.
* mkvmerge: bug fix: fixed display of very large IDs during attachment extraction.
* mkvextract: bug fix: during the extraction of chapters, tags or segment info XML
  files with the --redirect-output parameter the BOM (byte order mark) was written
  twice.
* mkvmerge: bug fix: MPEG TS: timestamp outliers are ignored if they differ at least
  five minutes from the last valid timestamp. Fixes #998.
* mkvmerge: bug fix: fixed timestamp assignment for AVC/h.264 videos in which
  recovery point SEIs occur in front of the second field of two interlaced fields.


# Version 6.8.0 "Theme for Great Cities" 2014-03-02

## Important notes

* mkvmerge: enhancement: The deprecated ISO 639-1 code "iw" is now recognized for
  Hebrew.

## New features and enhancements

* mkvmerge, mkvextract: new feature: added support for h.265/HEVC by merging the
  patches from DivX/Rovi Corp. So far HEVC is only supported as elementary streams and
  read from other Matroska files.
* mkvmerge: enhancements: AVI reader: audio chunks with obvious wrong size
  information (bigger than 10 MB) will be skipped.
* mkvmerge, mkvextract, mkvpropedit: enhancement: attachments in Matroska files
  with a missing FileUID element are not ignored anymore even though they violate the
  specs. mkvmerge generates a new FileUID instead.
* installer: enhancement: the architecture (32bit vs 64bit) is mentioned in the
  interface.

## Bug fixes

* mkvmerge: bug fix: The AC-3 packetizer will re-derive the sampling frequency and
  the number of channels from the bitstream. This way obviously invalid information
  from the source container like a sampling frequency of 0 Hz will be fixed.
* mkvmerge: bug fix: When reading M2TS files belonging to an MPLS playlist mkvmerge
  will now only copy packets whose timestamps lie between the "in time" and "out time"
  restrictions from the playlist's entry corresponding to that M2TS file. Fixes
  #985.
* all: Windows 64bit: fixed return value checks for opening files. Fixes #972.
* all: Windows: when redirecting the program's output with cmd.exe (e.g. "mkvinfo
  file.mkv > info.txt") the programs will no longer write two line feed characters
  (\r) per carriage return character (\n). Fixes #970.
* all: bug fix: Windows: messages written to the console (cmd.exe) are not re-encoded
  to the local charset and back to UTF-16 before they're handed over to
  ConsoleWriteW(). This fixes outputting Unicode characters to the console that are
  not part of the local charset. Fixes #971.
* extract: bug fix: using names of non-existing files in "attachments", "chapters",
  "cuesheet" or "tags" mode caused mkvextract to crash instead of emitting a proper
  error message. Fixes #964.
* mmg: bug fix: fixed the check for WebM-compatible track types for Opus.
* mkvmerge: bug fix: fixed muxing Sorenson v3 (SVQ3) video from QuickTime files.
* mmg: bug fix: mkvmerge's file identification is written to a temporary file with
  --redirect-output and from there into mmg instead of directly from mkvmerge. This
  prevents from character re-coding done by wxWidgets 3.0.0 on Windows. Fixes #959.
* installer: bug fix: the installation directory for 64bit builds will default to the
  proper directory ("C:\Program Files" instead of "C:\Program Files (x86)"). Fixes
  #956.

## Build system changes

* mkvmerge: re-built with the 64bit build for Windows with a newer compiler version in
  order to fix #957. It was due to a bug in gcc:
  http://gcc.gnu.org/bugzilla/show_bug.cgi?id=56742

## Other changes

* all: Windows: the default charset for the files created with "--redirect-output"
  has been changed from the system's local charset to UTF-8. Just like before it can be
  changed with "--output-charset". See #970.


# Version 6.7.0 "Back to the Ground" 2014-01-08

## New features and enhancements

* all: enhancement: The architecture (32bit/64bit) is mentioned in the version
  information of all programs.
* mmg: enhancement: The "additional parts" dialog will now show the files that make up
  an MPLS playlist. This is for informational purposes only and doesn't allow
  changing the playlist itself.
* mkvmerge: enhancement: unified codec names output by mkvmerge's identification
  mode for all file format readers.
* mmg: enhancement: The user can select the default subtitle character set to use for
  newly added subtitle tracks in the preferences dialog as requested in bug #948.
* mkvmerge: new feature: implemented reading DTS audio tracks from MP4 files (with
  ESDS object type ID == 0xA9 (decimal 169) or FourCC == 'DTS ' or 'dtsc').
* mkvmerge: enhancement: allowed muxing Opus to WebM files.

## Bug fixes

* build system: bug fix for 64bit builds on Windows (x86_64-w64-mingw32): use the
  correct processor architecture via separate Windows manifest files. Fixes mmg and
  mkvinfo not starting due to "error 0x0000007b".
* mkvmerge: bug fix: Fixed a potential endless loop due to an integer overflow in the
  code removing AVC/h.264 filler NALUs.
* mkvmerge: bug fix: Fixed reading uncompressed PCM audio tracks from QuickTime/MP4
  files in certain situations. Fixes #950.
* mmg: enhancement: Made the "scanned files" list box sortable by all columns. Fixes
  #954.
* mkvmerge: bug fix: Reading from an MPLS playlist file is now done as if the second and
  following files referenced in that playlist had been appended to the first file from
  that playlist. Before they were treated as if they were additional parts. Fixes
  #934.
* mmg: enhancement: a couple of fixes to tooltips: 1. Content correction for
  "splitting by chapters"; 2. no ugly re-formatting with wxWidgets 3.0.0 on Windows.
* mkvmerge: bug fix: Improved the AAC, AC-3 and MP3 header decoding error handling so
  that the corresponding parsing routines won't get stuck in endless loops when
  encountering certain garbage data patterns.
* mkvinfo: bug fix: when setting the language with --ui-language a few strings were
  still translated using the system's default language.
* mkvextract: bug fix: if the track headers were located at the end of the file (e.g.
  after modification with mkvpropedit or mmg's header editor) then mkvextract was
  writing files with a length 0 bytes.
* mmg: bug fix: the "playlist items" list box in the "select playlist file to add"
  dialog was showing the items in reversed order. Fixes #952.
* mmg: bug fix: the "select playlist file to add" dialog can now be resized, minimized
  and maximized. It also remembers its position and size during runs. Fixes #951.
* mmg: bug fix: fixed the tooltip for the subtitle character set drop-down box to match
  mkvmerge's actual behavior. Fixes #948.
* mkvmerge: bug fix: Fixed the mapping of the Opus element "seek pre-roll" and
  "pre-skip" to the Matroska elements "track seek pre-roll" and "codec delay".
  Remuxing Matroska files with Opus created with earlier versions of MKVToolNix is
  enough to fix such a file.
* mkvmerge: bug fix: fixing the bitstream timing information of h.264/AVC writes
  clean values for 25000/1001 frames per second video (e.g. de-telecined PAL @
  29.97).
* mmg: bug fix: fixed a crash in during drag & drop operations in mmg's chapter editor.

## Build system changes

* build system: Ruby 1.9.x is now required.


# Version 6.6.0 "Edge Of The In Between" 2013-12-01

## New features and enhancements

* mmg: new feature: implemented drag & drop in the chapter editor. Implements #929.
* all: integrated the Portuguese translation. Although the translation files
  themselves had been added back in 6.3.0 that translation wasn't available for
  selection due to forgetfulness on my part.

## Bug fixes

* mmg: bug fix: fixed an assertion in wxLogMessage() due to wrong format
  string/argument data types caused by changes in wxWidgets 3.0.0. See Debian bug
  #730273.
* mkvmerge: bug fix: improved resilience against MP4 files with obviously wrong
  entries in the 'sample size table' (STSZ) atom.
* mkvmerge: bug fix: improved VC-1 frame type detection so that it works even for
  streams without entry points.
* mkvinfo: bug fix: at most the lower 32bits of the track numbers and track UIDs
  elements were output, even if the element in the file used more bits. Fixes #935.
* mkvmerge: bug fix: fixed accessing invalid memory in the memory handling core
  routines. May be triggered by the code to remove filler NALUs introduced in v6.5.0.
  Fixes #931.
* mmg: bug fix: fixed the tracks list box on the input tab being invisible/0 pixels high
  with wxWidgets 2.9.x/3.x.
* mkvmerge: bug fix: The file detection code in the MPEG elementary stream reader had a
  logic error. Fixes #928. In practice this logic error didn't have any consequence.


# Version 6.5.0 "Isn't she lovely" 2013-10-19

## New features and enhancements

* mkvmerge: enhancement: filler NALUs will now be removed from framed h.264/AVC
  tracks (such as the ones read from Matroska/MP4 files) just like they have already
  been when handling unframed tracks.
* mkvextract: new feature: implemented support for extracting VP9 tracks into IVF
  files.
* mkvmerge: new feature: implemented support for VP9 read from IVF and Matroska/WebM
  files. Implements #899.
* mkvextract: enhancement: using the same track/attachment ID multiple times in
  "tracks", "attachments" or "timecodes_v2" mode will result in an error message
  instead of one empty file. Implements #914.
* documentation: Added a German translation of the man pages by Chris Leick (see
  AUTHORS).

## Bug fixes

* mmg: bug fix: With wxWidgets 2.9.x/3.0.x debug message will no longer appear as
  modal dialogs but only go to the log window.
* mkvmerge: bug fix: fixed a crash when reading empty global tag files. Fixes #921.
* build system: bug fix: fix autodetection of Boost's library path if it is installed
  in the multiarch directories (e.g. /usr/lib/i386-linux-gnu or
  /usr/lib/x86_64-linux-gnu).
* mmg: bug fix: saved window widths were growing by 1 pixel each time mmg was exited.
* mkvmerge: bug fix: Reading OGM files with chapter entries not encoded in the
  system's local character set has been fixed. During identification the number of
  chapter entries is still output by removing any non-ASCII characters from the
  chapter entries. When muxing an additional warning is output if parsing those
  chapter entries fails, e.g. due to the format being wrong or due to the charset
  guessed wrongly. Fixes #919.
* mkvmerge: bug fix: The "duration" element was calculated wrong if the first element
  in the file wasn't the one with the smallest timestamp. To be precise, it was too short
  by the difference between the first timestamp and the smallest one (e.g with video
  sequences timestamped 80ms, 0ms, 40ms, 120ms... the duration was 80ms too short).


# Version 6.4.1 "Omega Point" 2013-09-16

## Bug fixes

* mkvmerge: bug fix: fixed packet ordering regression introduced in 6.4.0 if
  --default-duration is used for a track.


# Version 6.4.0 "Pale Blue Dot" 2013-09-15

## New features and enhancements

* mkvextract: new feature: Implemented extraction of Opus tracks into OggOpus
  files.
* mkvmerge: new feature: Implemented final Opus muxing.
* mkvinfo: new feature: Added support for the new Matroska elements DiscardPadding,
  CodecDelay and SeekPreRoll.

## Bug fixes

* mkvinfo: bug fix: The track information summary enabled with -t/--track-info
  counted bytes in SimpleBlocks twice.
* mkvmerge: bug fix: CueRelativePosition was wrong for BlockGroups: it pointed to
  the Block inside the group instead of the BlockGroup itself. CueRelativePosition
  elements for SimpleBlock elements are not affected. Fixes #903.
* mmg: bug fix: The "jobs" folder will be created in the same mmg.exe is located in for
  the portable version. The installed version will still keep the folder where has
  already been (%APP_DATA%\mkvtoolnix\jobs).
* mmg: bug fix: Closing mmg's window while it was minimized caused mmg to appear hidden
  and unmovable when started the next time.
* mmg: bug fix: Fixed overly long startup time with wxWidgets 2.9.x (especially on
  Windows) by using alternative methods for initializing certain controls. Makes
  startup time on par with wxWidgets 2.8. See #893.

## Build system changes

* build system: libMatroska 1.4.1 is now required for building.


# Version 6.3.0 "You can't stop me!" 2013-06-27

## New features and enhancements

* all: enhancement (Windows only): mmg will store its settings in a file
  "mkvtoolnix.ini" in the same folder mmg.exe is located in if MKVToolNix hasn't been
  installed via its installer. If it has been installed then the settings are stored in
  the Windows registry. This way MKVToolNix is truly portable.
* mmg: new feature: mmg's windows and dialogs will remember and restore their
  positions and sizes. Implements #878.
* all: new feature: added a Portuguese translation of the programs by Ricardo
  Perdigão (see AUTHORS).

## Bug fixes

* mkvmerge: bug fix: When appending unframed AVC/h.264 tracks and setting the
  default duration the second and all following source parts will use the same default
  duration as set for the first part. Fixes #889.
* mkvmerge: bug fix: AVC/h.264 output module: fixed writing the wrong values if
  --fix-bitstream-timing-information is used. Fixes #888.
* mkvmerge: bug fix: FLV reader: Implemented deriving the video dimensions for FLV1
  type tracks from the frame content if they're not given within a script tag. Fixes
  #880.
* mkvmerge: bug fix: Fixed handling MPEG transport streams with broken PES packet
  streams. Fixes #879 and #887.
* mkvextract: bug fix: mkvextract writes the correct value for the "block alignment"
  value in the header of WAV files (mostly affects mono PCM audio tracks). Fixes #883.


# Version 6.2.0 "Promised Land" 2013-04-27

## New features and enhancements

* mkvextract: enhancement: track extraction mode: If mkvextract encounters a
  broken file structure it will output the last timecode successfully read before
  resyncing. After the resync the first cluster timecode will be reported as well.
* mkvmerge: new feature: Selecting the lowest process priority with "--priority
  lowest" will cause mkvmerge to also select an idle/background I/O priority.
  Implements #863.
* mmg: new feature: Add control for new option
  "--fix-bitstream-timing-information".
* mkvmerge: new feature: Add option for fixing the timing information in video track
  bitstreams (--fix-bitstream-timing-information).
* mkvmerge: enhancement: Matroska reader: If mkvmerge encounters a broken file
  structure it will output the last timecode successfully read before resyncing.
  After the resync the first cluster timecode will be reported as well.
* all: enhancement on Windows: all programs now determine the interface language to
  use from the user's selected interface language (C function
  "GetUserDefaultUILanguage()"), not from the locale setting. Implements #852.

## Bug fixes

* mkvmerge: bug fix: The option "--engage remove_bitstream_ar_info" will now work
  on AVC/h.264 tracks read from Matroska/MP4 files as well. Fixes #868.
* mmg: bug fix: mmg will now handle all file names given on the command line instead of
  only the first one. This allows things like opening several selected files with mmg
  in Windows, and mmg will add all of them. Fixes #867.
* mkvmerge: bug fix: The amount of memory required to store the cues during muxing has
  been reduced drastically. This is more noticeable the more video and subtitle
  tracks are muxed. Fixes #871.
* mkvmerge: bug fix: If splitting had been active then the elements "cue duration" and
  "cue relative position" were only written to the first output file.
* mkvmerge: bug fix: The "CTS offset" field of FLV files with AVC/h.264 video tracks is
  now read as a signed-integer field in accordance with the FLV specifications.
* mkvmerge: bug fix: DTS parsing: no more warnings about incompatible encoder
  revision numbers will be printed. Fixes #866.
* mkvmerge: bug fix: The parsing of the AAC AudioSpecificConfig structure (the bytes
  contained in Matroska's CodecPrivate and in MP4's "ESDS" atom) was fixed to support
  parsing the GASpecificConfig and the ProgramConfigElement if the channel
  configuration is 0. Fixes #872.
* mmg: bug fix: Loading chapters from Matroska files will open the file in read-only
  mode allowing to read from write-protected files.
* mkvmerge: bug fix: All entries in chapters imported from MPLS playlists were named
  "Chapter 0". The numbering has been fixed. Fixes #870.
* mkvmerge: bug fix: Fixed reading AVI files with audio chunks of size 0. Fixes #843.
* mkvmerge: bug fix: MPEG program stream reader: tracks with invalid video
  properties (e.g. width or height = 0) are ignored properly.
* mkvmerge: bug fix: The progress percentage was sometimes using the wrong input file
  as the reference if multiple files are read with the "additional parts" mechanism
  (on the command line: the syntax "( VTS_01_1.VOB VTS_1_2.VOB VTS_1_3.VOB )".
* mkvmerge: bug fix: Fixed one situation that could lead to mkvmerge aborting with the
  error message "Re-rendering track headers: data_size != 0 not implemented yet".
* mmg: bug fix: Using drag & drop to add playlists will no longer lock the dragging
  application (e.g. Windows Explorer) in D&D mode for the duration of the scan for
  other playlists.
* mmg: bug fix: The validation for the argument to "split by chapters" was wrongfully
  rejecting certain valid inputs (chapter number lists in which the second or any
  later chapter number was higher than 9).

## Other changes

* installer: The installation directory will no longer be added to the PATH
  environment variable.
* mkvmerge: removal: Support for BZ2 (bzlib) and LZO (lzo1x) compression has been
  removed.


# Version 6.1.0 "Old Devil" 2013-03-02

## New features and enhancements

* mmg: new feature: When a playlist file (e.g. MPLS Blu-ray playlist) is added mmg can
  optionally scan all the other files in the directory that have the same extension and
  present the user with the results (including them playback time, total size, number
  of chapters, number and types of tracks). The user can then select the actual
  playlist file to add. The user can configure the minimum playlist duration in order
  to filter out too short ones.
* mmg: new feature: Added an option for disabling making the suggested output file
  name unique by adding a running number (e.g. ' (1)'). Implements #848.
* mmg: new feature: The output file name can be auto-set to be located in the first input
  file's parent directory. Implements #849.
* documentation: Added a Dutch translation of mmg's guide by René Maassen (see
  AUTHORS).
* mkvmerge: new feature: Implemented support for reading MPLS BluRay playlist
  files. All M2TS files referenced from an MPLS file are processed. Chapter entries
  from that MPLS file are used as well. Implements #765.

## Bug fixes

* mkvmerge: bug fix: Fixed mkvmerge sometimes mistakenly detecting MPEG-1 video in
  MPEG program streams as AVC/h.264. Fixes #845.
* mkvinfo, mkvpropedit, mmg's header editor: bug fix: Fixed the description for the
  DisplayUnit element to include value 3 ("aspect ratio").
* mkvmerge: bug fix: Fixed handling chapters when splitting by parts (both
  parts/timecodes and parts/frames). Fixes #831.
* mkvmerge: bug fix: Fixed reading certain MP4 atoms with invalid length fields.
* mkvmerge: bug fix in common AAC code: Fixed wrong calculation of AAC packet size for
  malformed packets resulting in "safemalloc()" failing to allocate memory. Part of
  a fix for #832.
* mmg: bug fix: Selecting one of the pre-defined values from the "split by X" argument
  drop down box (e.g. "700M") was not leaving the selected entry in the drop down box but
  set it to empty instead.
* mkvmerge: bug fix: Fixed reading VP6 video from FlashVideo files. Fixes #836.
* mmg: bug fix: Fixed validating the argument for splitting parts by frame/field
  numbers. Fixes #835.


# Version 6.0.0 "Coming Up For Air" 2013-01-20

## Important notes

* mkvmerge: bug fix: ISO 639-2 language handling: The deprecated language codes
  "scr", "scc" and "mol" are replaced by their respective successors "hrv", "srp" and
  "rum". Fixes #803.

## New features and enhancements

* mkvmerge: new feature: Implemented splitting by parts based on frame/field
  numbers ("--split parts-frames:" in mkvmerge). Implements #819.
* mkvmerge: new feature: Implemented reading VobSubs from MP4 files if they're
  stored in the Nero Digital way (track sub-type 'mp4s', ESDS object type identifier
  0xe0). Implements #821 and the second half of #815.
* mmg: new feature: Command line options can be saved as default for new jobs by
  clicking a check box in the "add command line options" dialog.
* mkvmerge: new feature: Added experimental support for the Opus audio codec. Parts
  of an implementation of #779.
* mkvmerge, mmg: new feature: Implemented splitting by chapter numbers. Implements
  #504 and #814.
* mkvmerge: enhancement: Removed several warnings from the MPEG-2 video parser code
  about open GOPs, missing references. Those were too confusing for most users, even
  after being given additional information via email and FAQs.
* mkvextract: new feature: Implemented extraction of ALAC into Core Audio Format
  files (CAF). Implements #786.
* mkvmerge, mmg: new feature: Implemented splitting by frame/field numbers.
  Implements #771.
* mkvmerge: new feature: Implemented a reader for the Flash Video format (.flv).
  Implements #735.

## Bug fixes

* mkvmerge: bug fix: Re-writing the track headers after they'd grown a lot (to more
  than the EBML void size located after them allowed for) led to an integer underflow.
  Then mkvmerge tried to write a void element the size of that integer (e.g. nearly 4 GB
  on 32bit platforms). Fixes #822 and #828.
* mkvmerge: bug fix in the MP4 reader: Fixed language code conversion from what is used
  in MP4 to the ISO 639-2 codes used in Matroska (e.g. convert from "deu" to "ger").
* mmg: bug fix: Fixed a crash in the chapter editor if the root was selected and the user
  used the "Set values" button.
* mkvmerge: bug fix: "text"-type tracks in MP4 files are only treated as chapters if
  their track ID is listed on a "chap" atom inside a "tref" track reference atom. Fixes
  #815.
* mmg: bug fix: Fixed consistency checks when appending files and at least one track is
  disabled.
* mkvmerge: bug fix: Matroska reader: Fixed finding the "segment info" element if it
  is located behind the clusters.
* mkvmerge: bug fix: MP3 parser code: Fixed skipping ID3 tags so that the header
  directly behind the ID3 tag is recognized properly. Fixes #747.
* mkvmerge: bug fix: MP4 reader: Fixed handling of edit lists if the edit list is used to
  adjust the track's timecodes by a fixed amount (either positive or negative). Fixes
  #780.
* mkvpropedit: bug fix: Giving a non-existent file name in tags mode will result in a
  proper error message. Fixes #806.

## Build system changes

* Build system: Boost's "variant" library is now required.

## Other changes

* Source distribution: source code archives (tarballs) will be compressed with xz
  instead of bzip2 from now on. The file name's extension will therefore change from
  ".tar.bz2" to ".tar.xz". The download URL changes accordingly.
* mkvmerge, mmg: removal: The 'header removal compression' method is not turned on by
  default anymore. This affects the following track types: AC-3, AVC/h.264, Dirac,
  DTS, MP3. The setting in mmg that turned it off by default has been removed.


# Version 5.9.0 "On The Loose" 2012-12-09

## Important notes

* mkvpropedit, mmg, mkvmerge: removal: removed support for the deprecated element
  TrackTimecodeScale.

## New features and enhancements

* mkvmerge: enhancement: Dirac video code: Added four more pre-defined video types
  from Dirac spec v2.2.2 and two from Dirac Pro.
* mkvmerge, mmg: enhancement: Added options for turning off writing "CueDuration"
  elements ("--engage no_cue_duration") and "CueRelativePosition" elements
  ("--engage no_cue_relative_positions").
* mkvmerge: new feature: The element "CueRelativePosition" is written for all cue
  entries.
* mkvmerge: new feature: The element "CueDuration" will be written for all cue
  entries referring to subtitle tracks.
* mkvmerge: new feature: mkvmerge will write cues for subtitle tracks by default now.
* mkvinfo: new feature: added support for the new elements CueDuration and
  CueRelativePosition.

## Bug fixes

* mkvmerge: bug fix: Fixed reading seek position values bigger than 2 GB. Fixes #805.
* mkvmerge: bug fix: Fixed appending non-empty tracks to empty tracks. Fixes #793.
* mkvmerge: bug fix: mkvmerge will now keep timecodes of PCM tracks from source files
  if they're available. Fixes #804.
* all: bug fix: EBML void elements will be skipped when reading structures from XML
  (e.g. chapters). Fixes #802.
* all: bug fix: EBML void elements will be skipped when saving structures to XML (e.g.
  chapters). Fixes #801.
* mkvmerge: bug fix: Fixed reading linked seek heads in Matroska files.
* mmg: bug fix: Fixed reading file names containing a '%' from a .mmg settings file
  (both normally saved files and the job queue files). Fixes #795.


# Version 5.8.0 "No Sleep / Pillow" 2012-09-02

## New features and enhancements

* mkvpropedit: new feature: Added support for adding, deleting and replacing
  attachments.
* mmg: new feature: chapter editor: Added support for the edition flags "hidden",
  "default" and "ordered" as well as the chapter values "segment UID" and "segment
  edition UID". Implements ticket #736.
* documentation: Added a Basque translation of mmg's guide by Xabier Aramendi (see
  AUTHORS).
* mkvmerge: new feature: Added support for reading ALAC (Apple Lossless Audio Codec)
  from CAF (CoreAudio), MP4 and Matroska files. Implements #753.
* mkvmerge: new feature: mkvmerge will remove stuffing bytes from MPEG-1/-2 video
  streams that are used to keep the bit rate above certain levels (the 0 bytes between
  slices and the following start code). Implements #734.
* mkvmerge: enhancement: SRT files can have spaces in their timecode line's arrow
  (e.g. "-- >").
* all: new feature: Added a Basque translation by Xabier Aramendi (see AUTHORS).

## Bug fixes

* all: bug fix: Fixed a buffer overflow in the Base64 decoder routine.
* source: Various fixes for building with g++ 4.7.x and clang 3.1.
* mkvmerge: bug fix: MPEG transport streams whose timecodes wrap around/overflow
  are handled correctly. Fixes #777.
* mkvmerge: bug fix: MP2/MP3 audio tracks in MPEG program streams that contained
  garbage at the start of the very first packet caused mkvmerge to use
  uninitialized/random values for certain parameters (sample rate, number of
  channels, and therefore also during timecode calculation).
* mkvmerge: bug fix: Fixed audio/video synchronisation when reading MPEG program
  streams with MPEG-1/2 video with respect to B frames. Fixes #579.
* mkvmerge: bug fix: VC-1: mkvmerge will now only mark frames as I frames if a sequence
  header precedes them directly. Fixes #755.
* all: bug fix: The programs do not try to create directories with empty names anymore.
  This happened if the output file name for e.g. mkvmerge or mkvextract was only a file
  name without a directory component. With Boost v1.50.0 the call to
  "boost::filesystem::create_directory()" would result in an error if the name was
  empty (it didn't in earlier versions of Boost).
* mmg: bug fix: Fixed mmg not reading the very last line of mkvmerge's output. The
  result was that messages like "the cues are being written" did not show up in mmg and
  that the progress bar was not filled completely. Fixes #774.

## Build system changes

* Build system: dropped support for gcc 4.6.0.
* Build system: Boost's "bind" library is not required anymore. The C++11 features
  from "functional" are used instead.


# Version 5.7.0 "The Whirlwind" 2012-07-08

## New features and enhancements

* mkvmerge: new feature: If "splitting by parts" is active and the last split part has a
  finite end point then mkvmerge will finish muxing after the last part has been
  completed. Implements #768.
* mmg, mkvinfo's GUI, all .exes: enhancement: Added new icons by Ben Humpert based on
  the ones by Eduard Geier (see AUTHORS).

## Bug fixes

* mmg: bug fix: mmg will no longer print false warnings about a chapter UID not being
  unique. Fixes #760.
* mkvmerge, mkvpropedit, mmg: bug fix: All tools can now deal with 64bit UID values
  (chapter UIDs, edition UIDs etc).
* mkvmerge: bug fix: The DTS and TrueHD packetizers were not flushed correctly. In
  some rare circumstances this could lead to mkvmerge aborting with an error message
  about the packet queue not being empty at the end of the muxing process. Fixes #772.
* mkvmerge: bug fix: Fixed handling of tracks in QuickTime/MP4 files with a constant
  sample size. This fixes the other reason for the "constant sample size and variable
  duration not supported" error. Fixes issue 764.
* mkvmerge: bug fix: Tracks in QuickTime/MP4 files with empty chunk offset tables
  (STCO and CO64 atoms) are ignored. This fixes one of the reasons for the "constant
  sample size and variable duration not supported" error.
* mmg: bug fix: Fixed mmg's excessive CPU usage during muxing.
* mkvmerge: bug fix: Reading DTS from AVI files often resulted in the error message
  that DTS headers could not be found in the first frames. This has been fixed. Fixes
  issue 759.
* Documentation: Updated the cross-compilation guide and fixed the
  "setup_cross_compilation_env.sh" script.


# Version 5.6.0 "Kenya Kane" 2012-05-27

## New features and enhancements

* documentation: Added Spanish translation of mmg's guide by Israel Lucas Torrijos
  (see AUTHORS).
* mkvmerge: enhancement: mkvmerge was optimized to keep cluster time codes strictly
  increasing in most situations.
* all: Added a translation to Polish by Daniel (see AUTHORS).
* mmg: new feature: When adding a Matroska file that has either the "previous segment
  UID" or the "next segment UID" set then mmg will copy those two and the source file's
  segment UID into the corresponding controls on the "global" tab if they haven't been
  set before. Implements ticket 733.
* mkvmerge: new feature: The verbose identification mode for Matroska files will now
  includes the "segment UID", the "next segment UID" and "previous segment UID"
  elements.
* mkvmerge: enhancement: In "--split parts:" mode mkvmerge will use the output file
  name as it is instead of adding a running number to it if all the ranges to be kept are to
  be written into a single output file. Implements ticket 743.

## Bug fixes

* mkvmerge: bug fix: SRT subtitle entries with colons as the decimal separator are
  accepted. Fix for issue 754.
* mkvmerge: bug fix: XML tag files with <Simple> tags that only contained a name and
  nested <Simple> were wrongfully rejected as invalid. Fixes issue 752.
* mkvextract: bug fix: Extraction of AVC/h.264 was completely broken after
  2012-04-09 resulting in files with a length of 0 bytes.
* mkvextract: bug fix: mkvextract will no longer abort extracting h.264 tracks if it
  encounters a NAL smaller than its size field. Instead it will warn about it and drop
  the NAL.
* mkvmerge: bug fix: Writing more than two parts into the same file with "--split
  parts:" resulted in the time codes of the third and all following parts to be wrong.
  Fixes ticket 740.
* mkvmerge: bug fix: The "--split parts:" functionality was not taking dropped
  ranges into account when calculating the segment duration for files that more than
  one range was written to. Fixes ticket 738.
* mkvmerge: bug fix: The "--split parts:" functionality was producing a small gap
  between the first part's last packet's time code and the second part's first
  packet's time code if two parts are written to the same file. Fixes ticket 742.
* mkvmerge: bug fix: The "--split parts:" functionality was writing a superfluous
  and empty first part if the first range starts at 00:00:00. Fixes ticket 737.
* mmg, build system: Fixed building with wxWidgets 2.9.3.


# Version 5.5.0 "Healer" 2012-04-06

## New features and enhancements

* mmg: new feature: Added GUI controls for mkvmerge's "file concatenation" feature
  as "additional file parts". The user can chose which individual files are treated as
  if they were a single huge source file.
* mkvmerge, mmg: new feature: Added support for keeping only certain time code ranges
  from the source files with a new format to "--split": "--split parts:...".
  Implements ticket #518.
* mmg: new feature: Added an option in the preferences dialog called "clear jobs from
  the job queue after they've been run". Can be set to "only if run was successful",
  "even if there were warnings" and "even if there were errors". Defaults to off.
* documentation: enhancement: mkvmerge's man page has been updated with a list of
  valid XML tags for the chapters, tags and segment info XML file formats.
* mkvmerge: enhancement: Chapter XML files: mkvmerge can handle the
  "ChapterSegmentEditionUID" element.
* mkvmerge: enhancement: Segment info XML files: mkvmerge can handle the
  "SegmentFilename", "PreviousSegmentFilename" and "NextSegmentFilename"
  elements.
* mmg: enhancement: Added "mts" as yet another file extension for MPEG transport
  streams.
* mmg, mkvinfo's GUI, all .exes: enhancement: Added new icons by Eduard Geier. (see
  AUTHORS).

## Bug fixes

* mkvmerge: bug fix: The handling of the "do not read other files" options (e.g.
  "=file.vob" and "( file.vob )") was broken for MPEG program stream files.
* mkvmerge: bug fix: Fixed a wrong assertion about minimum MPEG 1/2 video start code
  lengths. Fixes ticket 728.
* mmg: bug fix: Fixed a crash due to a missing argument for a format string when clicking
  on the "Browse" button for the track-specific tags.
* mkvextract: bug fix: mkvextract sometimes wrote undefined values to a single
  reserved header field when extracting into AVI files. Patch by buguser128k. Fix for
  ticket 727.
* mkvmerge: bug fix: AVC/h.264 mkvmerge was wrongfully writing a default duration of
  60 frames/fields even if the source was signalling 60000/1001 frames/fields. The
  frame time codes have been correct already.
* mkvmerge: bug fix: Fixed time code calculation for (E)AC-3 tracks if the source
  container (e.g. MPEG transport streams) only provided time codes for some of the
  (E)AC-3 packets itself.

## Build system changes

* Build system: Boost's "lexical_cast" and "type traits" libraries are now
  required.
* Build system: removed all files and documentation related to building MKVToolNix
  with Microsoft's Visual Studio because even the most recent version of Visual C++
  does not support the C++11 features required for MKVToolNix.

## Other changes

* mkvmerge, mkvextract, mmg: Re-write of the whole XML handling code. It now uses the
  "pugixml" C++ library instead of the "expat" library. Therefore "expat" is not
  required for building MKVToolNix anymore. And neither is Boost's "property tree"
  library. "pugixml" itself is included and not an external requirement either.
* mkvmerge, mkvextract: removal: Removed support for the CorePicture file format.
  It was mostly unused and relied on old code that will be removed soon.
* all: Updated the DTD files with the newly supported elements.


# Version 5.4.0 "Piper" 2012-03-10

## New features and enhancements

* mkvinfo: new feature: mkvinfo will output the track ID that mkvmerge and mkvextract
  would use for a track. This information is shown alongside the "track number"
  element in verbose mode and in the track summary in summary mode.
* mkvmerge, mmg: enhancement: The "--default-duration" in mkvmerge and the "FPS"
  drop down box in mmg now accept "p" or "i" as a unit -- as in e.g. "25p" or "50i". Several
  commonly used values have been added to mmg's "FPS" drop down box and others removed.
* mmg: enhancement: Added the values "50", "60" and "48000/1001" to the list of
  commonly used values for the "FPS" input field.
* mkvmerge: enhancement: mkvmerge will keep the "enabled" track header flag when
  muxing. mkvmerge will also output its value in verbose identification mode as
  "enabled_track".
* mkvmerge: enhancement: MicroDVD text subtitles are recognized as an unsupported
  format instead of an unknown format.
* doc: enhancement: Updates for option file usage and supported subtitle formats.

## Bug fixes

* mkvmerge: bug fix: Fixed wrong calculation of the maximum number of ns per cluster in
  certain fringe cases if time code scale was set to "auto" mode ("--time code-scale
  -1"). Fix for bug 707.
* mkvmerge: bug fix: When using an external time code file with AVC/h.264 video the
  default duration will be set to the most-often used duration in the time code file.
* mkvmerge: bug fix: AVC/h.264 packetizer: The value given with
  "--default-duration" (after internal conversion from the unit given by the user to
  duration in nanoseconds) is now again interpreted as the duration of a frame and not
  of a field.
* mkvmerge: bug fix: SRT subtitles: time codes can contain the minus sign before any
  digit, not just before the first one.
* mkvmerge: bug fix: Sometimes non-AC-3 files were mistakenly for AC-3 after the
  re-write of the AC-3 handling code on 2012-02-26. This has been rectified. Fix for
  bug 723.
* mkvmerge: bug fix: Complete re-write of the time code handling code for AVC/h.264
  tracks. Now handles several cases correctly: interlaced video, video with
  multiple or changing SPS with different timing information. The timing
  information is extracted from the bitstream. Therefore the user doesn't have to
  specify the default duration/FPS himself anymore. Fix for bugs 434 and 688.
* mkvmerge: bug fix: Complete re-write of the (E)AC-3 parsing and handling code.
  Dependent E-AC-3 frames are now handled correctly. Fix for bug 704.
* mkvmerge: bug fix: The width and height of h.264 video tracks with a pixel format
  other than 4:2:0 are now calculated correctly. Fix for bug 649. Patch by Nicholai
  Main (see AUTHORS).
* mkvmerge: bug fix: Fixed file type recognition and frame drops for VC-1 elementary
  streams that do not start with a sequence header but with frame or field packets
  instead.
* mkvmerge: bug fix: Fixed mis-detection as unsupported DV files (happened for e.g.
  PGS subtitle files).

## Build system changes

* build system: The C++ compiler must now support the C++11 keyword 'nullptr'.
  configure checks for it. For GCC this means at least v4.6.0.
* build system: Boost's "rational" library is now required.

## Other changes

* mmg: The warning that no default duration/FPS has been given for AVC/h.264 tracks
  has been removed.


# Version 5.3.0 "I could have danced" 2012-02-09

## New features and enhancements

* mkvmerge: new feature: mkvmerge will parse and apply the audio encoder delay in MP4
  files that contain said information in the format that iTunes writes it. Fix for bug
  715.
* mkvmerge: new feature: Implemented support for treating several input files as if
  they they had been concatenated binarily into a single big input file. Snytax is
  "mkvmerge -o out.mkv ( in1.ts in2.ts in3.ts )". This feature has already been
  present since version 5.1.0 but never been mentioned in the ChangeLog. Support for
  this feature in mmg is still missing.
* mkvmerge: enhancement: Identification output for Matroska files: Added the track
  number header field as "number" to the verbose identification mode.
* mkvmerge: enhancement: Identification output for Matroska files: Added a field
  "content_encoding_algorithms" that contains a comma-separated list of encoding
  algorithm IDs used for that track. For example, "content_encoding_algorithms:3"
  would indicate that header removal compression is used.
* mkvmerge: enhancement: Identification output for Matroska files: Added several
  fields to mkvmerge's verbose identification mode for tracks: UID, CodecID, length
  and content (as a hex dump) of the codec private data.
* mkvmerge: enhancement: Added video pixel dimensions to the output of
  "--identify-verbose" for Matroska files.

## Bug fixes

* mkvmerge: bug fix: Blocks with "BlockAdditions" will no longer be muxed as
  "SimpleBlock" elements discarding the additions but instead as "BlockGroup"
  elements. This applies to e.g. WAVPACK4 tracks with correction files as the
  correction data is stored in "BlockAdditions". Fix for bug 713.
* mkvmerge: bug fix: Fixed some more issues with (E)AC-3 being misdtected as AVC
  elementary streams.
* mmg: bug fix: The header editor was sometimes creating two instances of an element if
  an element was added to the second or one of the later tracks. Fix for bug 711.
* mkvpropedit, mmg: bug fix: Trying to modify a file located in a path mounted with GVFS
  SFTP will no longer crash the programs. Instead an error message is output if an error
  occurs. Fix for bug 710.
* mkvmerge: bug fix: Fixed integer underflows in the read caching code resulting in
  invalid memory access. Happened in broken or incomplete files only. Fix for bug 709.
* mkvmerge: bug fix: Appending AVI, Matroska or MPEG program stream files with DTS
  audio tracks will not result in a warning that the appended DTS tracks might not be
  compatible. Fix for bug 705.
* mkvextract: bug fix for the "time codes_v2" mode: mkvextract will write one more
  time code than there are frames in the file. The last time code written will be the the
  sum of the last frame's time code and duration with the "last frame" being the one with
  the highest time code. Fix for bug 691.
* mkvmerge: bug fix: Fixed writing into paths on which a drive is mounted on Windows.
  Fix for bug 701.
* mkvmerge: bug fix: Fixed a segmentation fault in the DTS detection code. Fix for bug
  698.
* mkvextract: bug fix: The track IDs used in the "time codes_v2" extraction mode are
  consistent again with the IDs that mkvmerge's identification reports and that
  mkvextract's "tracks" extraction mode uses. Fix for bugs 689 and 694.


# Version 5.2.1 "A Far Off Place" 2012-01-02

## New features and enhancements

* mkvmerge: enhancement: Removed the posix_fadvise() code. The application is
  using its own caching code which caused bad performance if the OS caching provided
  via posix_fadvise() is used as well.

## Bug fixes

* mkvmerge: bug fix: Fixed certain DTS files being mis-detected as AC-3. Fix for bug
  693.
* all: bug fix: Fixed compilation if gettext/libintl is not available.
* mkvmerge: bug fix: The MPEG program stream reader was reporting wrong progress
  percentage if multiple files were used since v5.1.0.
* mkvmerge: bug fix: If an MP4 file contains chapters encoded in a different charset
  than UTF-8 and --chapter-charset is not used then the error message shown is a lot
  clearer why mkvmerge aborts muxing. Before the error message was a generic
  "mm_text_io_c::read_next_char(): invalid UTF-8 character. The first byte:..."
* mkvmerge: bug fix: MPEG program streams in which a track suddenly ends and others
  continue or in which a track has huge gaps will no longer cause mkvmerge to try to read
  the whole file at once. This could lead to excessive swapping and finally mkvmerge
  crashing if no more memory was available.
* mkvextract: bug fix: The track IDs used for extraction are consistent again with the
  IDs that mkvmerge's identification reports. Fix for bug 689.
* mkvmerge: bug fix: Fix compilation if FLAC is not available. Fix for issue #13.

## Build system changes

* build system: Added an option "--without-gettext" that allows for building
  without support for translations even if gettext itself is installed.
* build system: Added an option "--without-curl" that allows for building without
  CURL support even if CURL itself is installed.


# Version 5.2.0 "I can't explain" 2011-12-18

## New features and enhancements

* documentation: enhancement: Added a Ukrainian translation for mkvextract's man
  page.
* mkvmerge: enhancement: The VP8 output module will always re-derive frame types
  (key frame vs. non-key frame).
* mkvmerge, mkvextract: enhancement: Implemented input file buffering in mkvmerge
  and improved/implemented output file buffering in other tools.
* mmg, mkvinfo's GUI: enhancement: Added new icons based on the work of Alexandr
  Grigorcea (see AUTHORS).

## Bug fixes

* mkvmerge, mmg: bug fix: Automatic MIME type recognition for TrueType fonts will
  result in "application/x-truetype-font" again instead of
  "application/x-font-ttf". Fix for bug 682.
* mkvinfo: bug fix: Various elements used to have a space between their names and their
  value's hex dump. In v5.1.0 that space was accentally removed. It has been added
  again. Fix for bug 583.
* mkvmerge: bug fix: Turn off input file buffering for badly interleaved MP4 files.
* mkvmerge: bug fix: Changed how mkvmerge assigns IDs to tracks in source files for
  Matroska and MP4 files. That way files whose headers contain the same ID for multiple
  tracks will work correctly. Fix for bug 681.
* mkvmerge: bug fix: VP8 read from AVI could not be put into WebM compatible files.
* mkvmerge: bug fix: Fixed a rare audio type mis-detection of MP2/MP3 audio tracks in
  MPEG program streams causing mkvmerge to abort with an error message.
* mmg: bug fix: Fixed a memory leak in mmg's header editor that caused the "open file"
  function to stop working after opening a few files. Fix for bug 679.


# Version 5.1.0 "And so it goes" 2011-11-28

## New features and enhancements

* all: enhancement: Made all EXEs declare their required access level privileges for
  Windows' User Access Control.
* mmg: enhancement: Made mmg DPI-aware on Windows (tested up to 144 DPI).
* mmg: enhancement: Added "ogv" to the list of known file extensions for "Ogg/OGM
  audio/video files". Implements bug 667.
* mkvmerge: enhancement: Added support for reading AAC tracks from MPEG transport
  streams.
* mkvmerge: enhancement: The verbose identification mode will add the properties
  "default_duration", "audio_sampling_frequency" and "audio_channels" if
  appropriate and if the corresponding header elements are present.
* mkvmerge: enhancement: "Castilan" has been merged with "Spanish" into "Spanish;
  Castillan" in the ISO 639 language list as both share the same ISO 639-2 code "spa".

## Bug fixes

* mkvmerge: bug fix: Fixed more time code handling issues for video tracks in MPEG
  transport streams whose PES packets sometimes don't have a time code.
* mkvmerge: bug fix: mkvmerge will no longer create folders on drives it shouldn't
  create them on on Windows.
* mkvmerge: bug fix: Fixed bogus huge time codes sometimes occurring for AVC/h.264
  video tracks read from MPEG transport streams.
* mmg: bug fix: mmg will append ".xml" to the file name entered when saving from the
  chapter editor if no extension was given.
* mkvinfo: bug fix: Improved skipping broken data on all operating systems.
* mkvmerge, mkvextract: bug fix: Skipping broken data in Matroska file often caused
  the program to abort on Windows. This has been fixed so that processing continues
  after the broken part. Fix for bug 668.
* mkvmerge: bug fix: Fixed reading VC-1 video tracks from Matroska files that don't
  use VC-1 start markers (0x00 0x00 0x01 ...).
* mmg: bug fix: A utility function for breaking a line into multiple ones was accessing
  invalid memory in rare situations causing mmg to crash. Could happen e.g. when
  adding a job to the job queue.
* mkvmerge: bug fix: mkvmerge will use DTS instead of PTS for VC-1 video tracks read
  from MPEG transport streams.
* mkvmerge: bug fix: Fixed reading MPEG transport streams on big endian systems.
* mkvmerge: bug fix: Relaxed the compatibility checks when concatenating VP8 video
  tracks.
* mkvmerge: bug fix: Fixed PCM audio in WAV sometimes being detected as DTS.

## Build system changes

* build system: Boost's "Range" library is now required.
* build system: Boost v1.46.0 or newer is now required. As a consequence included
  copies of some of Boost's libraries have been removed (foreach, property tree).
* build system: The C++ compiler must now support several features of the C++11
  standard: initializer lists, range-based 'for' loops, right angle brackets, the
  'auto' keyword and lambda functions. configure checks for each of these. For GCC
  this means at least v4.6.0.

## Other changes

* examples: Added XSLT 2.0 stylesheets in the "examples/stylesheets" directory for
  turning Matroska chapters into cue sheet and split points for "shntool" (useful for
  situations in which you have e.g. a live recording from a concert including chapters
  and want to create one audio file per song).
* Packaging: In v5.0.1 mmg's guide was accidentally moved into the "mkvtoolnix"
  Debian/Ubuntu package. It has been moved back into "mkvtoolnix-gui" again.


# Version 5.0.1 "Es ist Sommer" 2011-10-09

## New features and enhancements

* mkvmerge: enhancement: Implemented support for yet another way of storing E-AC-3
  and DTS in MPEG transport streams.

## Bug fixes

* mkvinfo: bug fix: Track information was not reset when opening more than one file in
  the GUI.
* mkvmerge: bug fix: The PGS subtitle output module was not outputting any packet in
  certain cases due to uninitialized variables.
* mkvmerge: bug fix: Fixed mkvmerge not finding any track in TS streams whose first PMT
  packet could not be parsed (e.g. invalid CRC).
* mkvmerge: bug fix: Fixed detection of TS streams that only contain one PAT or PMT
  packet within the first few KB but no others within the first 10 MB.

## Build system changes

* build system: Updated the Debian/Ubuntu files to debhelper v7/quilt 3.0 format.


# Version 5.0.0 "Die Wahre Liebe" 2011-09-26

## New features and enhancements

* mkvmerge: new feature: MPEG TS: mkvmerge will extract the track languages from a
  corresponding clpi (clip info) file. That file is searched for in the same directory
  and in ../CLIPINF and must have the same base name but with the ".clpi" extension.
* mkvmerge: enhancement: Added new stereo mode options to match the current specs.
  The new options are "anaglyph_green_magenta" (12),
  "both_eyes_laced_left_first" (13) and "both_eyes_laced_right_first" (14).
* mkvmerge: enhancement: MPEG TS: Added support for HDMV PGS subtitles.
* mkvmerge: enhancement: MPEG TS: Added support for DTS HD Master Audio tracks.
* mkvmerge: enhancement: MPEG TS: Streams that are mentioned in the PMT but do not
  actually contain data are neither reported during identification nor muxed.
* mkvmerge: new feature: MPEG TS: Added support for reading the language code.
* mmg: enhancement: Added MPEG transport streams to the "add file" dialog file
  selector.
* mkvmerge: new feature: MPEG TS: Added support for normal DTS tracks.
* all: Added an Lithuanian translation by Mindaugas Baranauskas (see AUTHORS).
* mkvmerge: new feature: Implemented a MPEG transport stream demuxer.
* mkvmerge: enhancement: When looking for MPEG files with the same base name as a
  source file mkvmerge will be stricter what it accepts. The file name must consist of
  at least one char followed by "-" or "_" followed by a number. That will match
  VTS_01_1.VOB but not e.g. "some_series_s03e10.mpg".
* mkvmerge: enhancement: Sped up file identification by caching read operations.

## Bug fixes

* mkvmerge: bug fix: The "writing application" element will not be localized but
  always be written in English.
* mkvextract: bug fix: Fixed attachment number displayed during extraction. Fix for
  bug 663.
* mkvmerge: Tons of fixes and additions to the MPEG transport stream demuxer.
* mkvmerge: bug fix: Opening MPEG files with numbers in their name from folders with
  e.g. Cyrillic names failed on Windows.
* mkvmerge: bug fix: Several elements are not written when creating WebM compliant
  files. In the segment headers: SegmentUID, SegmentFamily, ChapterTranslate,
  PreviousSegmentUID, NextSegmentUID. In the track headers: MinCache, MaxCache
  and MaxBlockAdditionID.
* mkvmerge: bug fix: Fixed identifying QuickTime/MP4 files that start with a 'skip'
  atom.
* mkvmerge: bug fix: Fixed a crash when reading AVI files with DTS audio tracks that do
  not contain valid headers in the first couple of packets. Fix for bug 646.

## Build system changes

* build system: libEBML 1.2.2 and libMatroska 1.3.0 are required for building. If
  external versions are not found or if they're too old then the included versions will
  be used as a fallback.
* build system: configure will accept external versions of libEBML and libMatroska
  again. Minimum required versions are libEBML 1.2.1 and libMatroska 1.1.0.

## Other changes

* mkvmerge: The --stereo-mode named option "anaglyph" was renamed to
  "anaglyph_cyan_red" to match the specs. The numerical value (10) remains
  unchanged.
* All: Updated the French translation with a complete set by DenB (see AUTHORS).
* mmg: mmg respects the XDG Base Directory Specification regarding its
  configuration files (environment variable $XDG_CONFIG_HOME/mkvtoolnix if set,
  otherwise ~/.config/mkvtoolnix).


# Version 4.9.1 "Ich will" 2011-07-11

## Bug fixes

* mkvmerge: bug fix: Fixed endless loop when reading AVI files on Windows if
  MKVToolNix was compiled with a gcc mingw cross compiler v4.4.x. Fix for bug 642.
* mkvmerge: bug fix: Fixed long file identification time caused by DV detection. Fix
  for bug 641.


# Version 4.9.0 "Grüner" 2011-07-10

## New features and enhancements

* all: Added an Italian translation by Roberto Boriotti and Matteo Angelino (see
  AUTHORS).

## Bug fixes

* mkvmerge: bug fix: DV files are recognized as an unsupported container type. Fix for
  bug 630.
* mkvmerge: bug fix: Fixed handling block groups in Matroska files with a duration of
  0.
* mmg: Various compatibility fixes for use with wxWidgets 2.9.x.
* mmg: bug fix: Fixed building with Sun Studio's C compiler.
* mkvmerge: bug fix: ISO 639-2 terminology language codes are converted to the
  corresponding bibliography code upon file identification (e.g. 'deu' is
  converted to 'ger').
* mkvinfo: bug fix: The time code scale is retrieved first before applying it to the
  segment duration.
* mmg: bug fix: Fixed populating the 'compression' drop down box according to what
  mkvmerge was compiled with.
* mkvmerge: bug fix: When a DTS track is read from a source file that provides time codes
  (e.g. Matroska files) then those time codes will be preserved.
* mkvmerge: Fixed remuxing certain VC-1 video tracks from Matroska files. Fix for bug
  636.


# Version 4.8.0 "I Got The..." 2011-05-23

## New features and enhancements

* mkvmerge: enhancement: Added support for VobSub IDX files with negative "delay"
  fields.
* mkvpropedit: new feature: Added support for adding, replacing and removing
  chapters.
* mkvmerge: enhancement: File identification for tracks read from Matroska files
  with a codec ID of "A_MS/ACM" will show the track's format tag field if it is unknown to
  mkvmerge. Implements bug 624.
* mkvmerge: new feature: Track, tag and attachment selection via --audio-tracks,
  --video-tracks etc. can have their meaning reversed by prefixing the list of IDs
  with "!". If it is then mkvmerge will copy all tracks/tags/attachments but the ones
  with the IDs given to the option (e.g. "--attachments !3,6").

## Bug fixes

* mmg: bug fix (Windows): mmg will no longer convert the "mkvmerge executable" from
  just "mkvmerge" into a full path name when writing its preferences to the registry
  upon existing.
* mkvmerge: bug fix: The 'doc type read version' EBML header field is only set to 2 even
  if a stereo mode other than 'none' is used for at least one video track. Fix for bug 625.
* mkvmerge: bug fix: Reading DTS files stored in 14-to-16 mode were read partially.
* mkvmerge: enhancement: mkvmerge will rederive frame types for VC-1 video tracks
  stored in Matroska files instead of relying on the container information. This
  fixes files created by e.g. MakeMKV that mark all frames as key frames even if they
  aren't.
* mkvmerge: bug fix: Fixed detection of AAC files with ADIF headers. Fix for bug 626.
* mkvmerge: bug fix: The 'doc type version' and 'doc type read version' EBML header
  fields are only set to 3 if a stereo mode other than 'none' is used for at least one video
  track. Fix for bug 625.
* mkvmerge: bug fix: Fixed handling AVIs with AAC audio format tag 0x706d and bogus
  private data size. Fix for bug 623.

## Other changes

* All: Avoided a segmentation fault in gcc by not including a pre-compiled header if
  FLAC or CURL support is disabled.


# Version 4.7.0 "Just Like You Imagined" 2011-04-20

## New features and enhancements

* mkvmerge: enhancement: Added support for WAV and AVI files that use a
  WAVEFORMATEXTENSIBLE structure (wFormatTag == 0xfffe). Fix for bug 614.
* mkvmerge: enhancement: The EBML header values "doc type version" and "doc type read
  version" are both set to 3 if at least one of the video tracks uses the stereo mode
  parameter.

## Bug fixes

* mkvmerge: bug fix: Fixed appending time code calculation for appended subtitle
  tracks if the subtitle tracks are read from complex containers (e.g. Matroska, MP4,
  AVI etc). Fix for bug 620.
* mkvextract: bug fix: Fixed extraction of MPEG-1/2 video tracks whose sequence
  headers change mid-stream but whose key frames are not all prefixed with a sequence
  header. Fix for bug 556.
* mkvmerge: bug fix: Fixed reading AAC tracks from AVI files with 7 bytes long codec
  data. Fix for bug 613.
* mmg: bug fix: The output file name extension will be updated on each track selection
  changed as well. The extension is based on the actually selected tracks, not on the
  presence of tracks of certain types. Fix for bug 615.
* mkvmerge: bug fix: mkvmerge was dropping the last full DTS packet from a DTS files if
  that file was not encoded in "14-in-16" mode and if the file size was not divisible by
  16.
* mkvmerge: bug fix: Fixed huge slowdown when splitting by size is active with certain
  kinds of input files. Fix for bug 611.
* mkvinfo: bug fix: Fixed redirecting the output into a file with
  "--redirect-output"/"-r" and verbosity levels of 2 and higher.
* mkvpropedit, mmg header/chapter editor: bug fix: Fixed parsing Matroska files if
  mkvtoolnix is compiled with newer versions of libebml/libmatroska (SVN revisions
  after the releases of libebml 1.2.0/libmatroska 1.1.0).
* mkvmerge: bug fix: WAV files with unsupported format tags are rejected instead of
  being treated like containing PCM. Fix for bug 610.

## Build system changes

* build system: For the time being the build system will always build and link
  statically against the internal versions of libEBML and libMatroska.


# Version 4.6.0 "Still Crazy After All These Years" 2011-03-09

## New features and enhancements

* mkvmerge: enhancement: HD-DVD subtitles are recognized as being an unsupported
  file format. This makes the error message presented to the user a bit clearer. Fix for
  bug 600.
* mkvpropedit: new feature: Added support for adding, replacing and removing tags.
* all: Added a translation for the programs into Turkish by ßouЯock (see AUTHORS).

## Bug fixes

* build system: Fixed building the Qt version of mkvinfo's GUI (again). Fix for bug
  576.
* mmg: bug fix: If the header editor finds 'language' elements with ISO-639-1 codes
  (e.g. "fra" instead of "fre" for "French") then it will map the code to the
  corresponding ISO-639-2 code. Fix for bug 598.
* mmg: bug fix: Fixed one of the issues causing mmg to report that it is configured to use
  an unsupported version of mkvmerge when the reported version was actually empty.
* build: Boost 1.36.0 or newer is required (up from 1.34.0). Also fixed building with
  v3 of Boost's filesystem library, e.g. with Boost 1.46.0 Beta 1 or newer.
* build system: Fixed compilation if configure choses the internal versions of
  libebml and libmatroska while older versions are still installed in a location
  named with "-I..." or "-L..." in CFLAGS/CXXFLAGS/LDFLAGS or with configure's
  "--with-extra-includes" and "--with-extra-libs" options.


# Version 4.5.0 "Speed of Light" 2011-01-31

## New features and enhancements

* mkvinfo: new feature: Added an option "--track-info" (short: "-t") that displays
  one-line statistics about each track at the end of the output. The statistics
  include the track's total size, duration, approximate bitrate and number of
  packets/frames.
* mmg: enhancement: The output file name extension is automatically set to ".mk3d" if
  the stereo mode parameter for any video track is changed to anything else than "mono"
  or the default value.
* mmg: enhancement: Added ".mk3d" to the list of known file name extensions for
  Matroska files.
* mkvmerge, mmg: enhancement: Updated the "stereo mode" parameter to match the
  current Matroska specifications.
* mkvmerge: enhancement: If mkvmerge encounters invalid UTF-8 strings in certain
  files or command line arguments then those strings will simply be cut short. Before
  mkvmerge was exiting with an error ("Invalid UTF-8 sequence encountered").
* all: new feature: Added online update checks. The command line tools know a new
  parameter "--check-for-updates". mmg has a new menu entry ("Help" -> "Check for
  updates") and checks automatically when it starts, but at most once in 24 hours. Can
  be turned off in the preferences. This function requires libcurl and is not built if
  libcurl is not available.
* mkvmerge: new feature: Added support for reading VP8 video from Ogg files.
  Implements bug 584.
* mkvextract: enhancement: mkvextract will exit with an error if the user specifies
  track IDs that do not exist in the source file. This works in the "tracks" and "time
  codes_v2" extraction modes. Fix for bug 583.
* mkvmerge: new feature: The "default duration" header field is set for DTS audio
  tracks.

## Bug fixes

* mkvmerge: bug fix: Fixed an infinite loop when reading program stream maps in MPEG
  program streams. Part of a fix for bug 589.
* mkvinfo: bug fix: The hexdump mode was accessing invalid memory if the data to dump
  was shorter than 16 bytes. It was also outputting the values as characters instead of
  hexadecimal numbers. Patch by ykar@list.ru. Fix for bug 591.
* mkvmerge: bug fix: Avoid a crash due to invalid memory access if a source file name
  contains numbers (happens only if mkvtoolnix is built with MS Visual Studio). Fix
  for bug 585.
* docs: mkvextract's man page has been updated to match the program's expected
  command line syntax for the "time codes_v2" mode. Fix for bug 583.
* build system: Fixed building the Qt version of mkvinfo's GUI. Fix for bug 576.
* mkvmerge, mmg: bug fix: Option files could not contain options that started with '#'
  as they were interpreted as comment lines.
* mmg: bug fix: On Mac OS X the application type is set to a foreground application
  preventing issues like the GUI never getting focus.

## Build system changes

* build: Building mkvtoolnix now requires libebml v1.2.0 and libmatroska v1.1.0 or
  later.
* build: enhancement: mkvtoolnix now includes libebml and libmatroska. The
  configure script will use them if either no installed versions of them is found or if
  the installed version is too old.
* build system: mmg's guide and its images are installed into the location given by
  configure's "docdir" variable. Patch by Cristian Morales Vega (see AUTHORS).

## Other changes

* all: Made the French translation selectable in all programs.


# Version 4.4.0 "Die Wiederkehr" 2010-10-31

## New features and enhancements

* mkvmerge: new feature: If the name of an input file starts with '=' then mkvmerge will
  not try to open other files with the same name (e.g. 'VTS_01_1.VOB',
  'VTS_01_2.VOB', 'VTS_01_3.VOB') from the same directory. A single '=' as an
  argument disables this as well for the next input file. Implements bug 570.
* mmg: new feature: Added an option to disable extra compression when adding tracks by
  default.
* mkvmerge: enhancement: The warning about subtitle entries that are skipped
  because their start time is greater than their end time now includes the subtitle
  number.
* mkvmerge: enhancement: When appending two Matroska files which both contain
  chapters the chapter entries of all editions will be merged even if the edition's
  UIDs were different to begin with. This is done based on the order of the edition. If
  both files contain three editions each then the chapters from the first edition in
  the second file will be put into the first edition from the first file; the chapters
  from the second edition into the second edition and so on.
* all: Added a translation of the programs into French by Trinine (see AUTHORS).

## Bug fixes

* build system: bug fix: Installation no longer fails if xsltproc is available but the
  DocBook stylesheets aren't. Fix for bug 575.
* mkvmerge: bug fix: Made file type detection stricter for MP3, AC-3 and AAC files.
  This prevents mis-detection of other file types as one of these for certain files.
  Fix for bug 574.
* mkvmerge: bug fix: Fixed the usage of iterators with the STL "deque" template class.
  This caused mkvmerge to abort on systems which did not use the GNU implementation of
  the standard template library, e.g. OpenSolaris with the SunStudio compiler. Fix
  for bug 567.
* Build system: bug fix: 'drake install' did not work if the login shell was not POSIX
  compatible (e.g. fish). Fix for bug 559.
* mkvmerge: bug fix: The MPEG ES reader was accessing uninitialized data. This could
  lead to crashes or source files not being read correctly.
* mkvmerge: bug fix: Using "--no-video" on AVI files caused the video track to be
  mistaken for an audio track and included anyway. Fix for bug 558.


# Version 4.3.0 "Escape from the Island" 2010-09-04

## New features and enhancements

* mkvmerge: enhancement: Attachments will be rendered at the beginning of the file
  again. Fix for bug 516.
* mkvinfo: new feature: mkvinfo will show the h.264 profile and level for AVC/h.264
  tracks along with the CodecPrivate element.
* mkvextract, mkvinfo, mkvpropedit: new feature: Added the option "-q" and its long
  version "--quiet". With "--quiet" active only warnings and errors are output. Fix
  for bug 527.

## Bug fixes

* mkvmerge: bug fix: Appending tracks which would normally be compressed (e.g. with
  header removal compression) and turning off compression for those tracks with
  "--compression TID:none" (or the corresponsing option in mmg) was resulting in the
  second and all following appended tracks to be compressed all the same.
* mkvextract: bug fix: Errors such as 'file does not exist' did not cause mkvextract to
  quit. Instead it continued and exited with the result code 0.
* mkvmerge: bug fix: Certain frames in certain h.264/AVC raw tracks were handled
  wrong, e.g. files created by x264 versions starting with revision 1665. The
  situation occured if an IDR slice comes immedtiately after a non-IDR slice and the
  IDR slice has its frame_num and pic_order_count_lsb fields set to 0.
* mkvpropedit, mmg's header editor: Fixed a crash corrupting files in certain
  situations. If the updated header fields required filling exactly one byte with an
  EbmlVoid element and if the next Matroska element's "size" was already written with
  its maximum length (8 bytes) then the crash would occur. Such files are written by
  e.g. lavf. Fix for bug 536.
* All: bug fix: Fixed a couple of format strings in translations which could cause the
  programs to crash.
* mkvmerge: bug fix: Video tracks with a width or height of 0 are not read from AVI files
  anymore. Fix for bug 538.
* mkvmerge: bug fix: Fixed an error with losing packets (error message "packet queue
  not empty") when reading IVF (VP8) files using --default-duration on it.
* mkvmerge: bug fix: Fixed access to uninitialized memory in the MPEG-2 ES parser.
* mmg: bug fix: The 'total remaining time' shown by the job manager was totally wrong.
  Fix for bug 529.
* mmg header editor: bug fix: If a file was loaded that did not contain 'track language'
  elements and those elements were unchanged then they would be set to 'und' upon
  saving. Now they're left as-is, and when adding them to the file the drop-down box
  defaults to 'eng' being selected as per Matroska default value specifications. Fix
  for bug 525.
* mkvmerge: bug fix: The option "--quiet" was not working properly.
* mkgmerge: bug fix: mkvmerge was treating SSA/ASS subtitle files as audio files for
  the purpose of track selection (--no-subtitles / --no-audio). Fix for bug 526.

## Build system changes

* build system: The build system has been changed from "make" to "rake", the Ruby based
  build tool. MKVToolNix includes its own copy of it so all you need is to have Ruby
  itself installed. The build proecss has been tested with Ruby 1.8.6, 1.8.7 and
  1.9.1. Building is pretty much the same as before: "./configure", "./drake", "sudo
  ./drake install". Most of the build targets have similar if not identical names,
  e.g. "./drake install". You can override variables just like with make, e.g.
  "./drake prefix=/somewhere install".


# Version 4.2.0 "No Talking" 2010-07-28

## New features and enhancements

* mkvmerge: enhancement: Reading Matroska files: DisplayWidth & DisplayHeight
  values that are obviously not meant to represent pixels but only to be used for aspect
  ratio calculation (e.g. 16x9) are converted into proper ranges based on the track's
  PixelWidth & PixelHeight values and the quotient of DisplayWidth / DisplayHeight.
* mkvmerge: enhancement: Attachments will be rendered at the end of the file instead
  of at the beginning. The attachments will be placed after the cues but before the
  chapters. Fix for bug 516.
* mkvmerge: enhancement: Header removal compression has been enabled by default for
  MPEG-4 part 10 (AVC/h.264) video tracks with a NALU size field length of four bytes.
* mmg: enhancement: The taskbar progress is reset as soon as mkvmerge finishes/as
  soon as all jobs are done (Windows 7).
* mkvmerge: enhancement: Improved reading text files that use mixed end-of-line
  styles (DOS & Unix mixed).

## Bug fixes

* mkvmerge: bug fix: mkvmerge was accessing invalid memory In certain cases, e.g.
  when appending Matroska files that use compression while turning compression off.
* mkvmerge: bug fix: Splitting output files by size was basing its decision when to
  create a new file on an uninitialized variable. This caused effects like a lot of
  small files being created with sizes much smaller than the intended split size.
* mkvmerge: bug fix: The speed with which mkvmerge skips garbage in DTS tracks has been
  greatly improved.
* mkvmerge: bug fix: Header removal compression has been deactivated for MPEG-4 part
  2 (aka DivX/Xvid) video tracks due to incompatibility with packed bitstreams.
* mkvmerge: bug fix: Fixed reading AVC/h.264 tracks from AVI files if they're stored
  without NALUs inside the AVI. Was broken by a fix for handling AVC/h.264 in NALUs
  inside AVI.
* mkvmerge: bug fix: All readers that only handled file formats which do not contain
  more than one track did not respect the "--no-audio / --no-video / --no-subtitles"
  options. This applied to the following readers: AAC, AC-3, AVC/h.264,
  CorePicture, Dirac, DTS, FLAC, IVF, MP3, MPEG ES, PGS/SUP, SRT, SSA, TrueHD, TTA,
  VC-1, WAV and WavPack.
* mkvmerge: bug fix: Fixed invalid memory access in the PCM packetizer. Fix for bug
  510.
* mmg: bug fix: When mmg starts it will check the entries in the file and chapter menu's
  list of recently used files and remove those entries that no longer exist. Fix for bug
  509.
* mkvmerge: bug fix: Fixed a crash when reading Matroska files that were damaged in a
  certain way.


# Version 4.1.1 "Bouncin' Back" 2010-07-03

## Bug fixes

* mkvmerge: bug fix: Fixed invalid memory access in the header removal compressor.
  Fix for bug 508.
* mmg: bug fix: mmg will no longer add .mmg files opened by the job runner to the file
  menu's list of recently opened files. Fix for bug 509.


# Version 4.1.0 "Boiling Point" 2010-07-01

## New features and enhancements

* mkvmerge: enhancement: mkvmerge will report if it finds data errors in a Matroska
  file (e.g. due to storage failure or bad downloads). The position is reported as well
  as a periodic update as long as mkvmerge re-syncs to the next Matroska element.
* mmg: enhancement: The "compression" drop down box is enabled for all track types.
  That way "no compression" can be forced for those tracks mkvmerge uses "header
  removal" compression for.
* mkvmerge: enhancement: mkvmerge will start a new cluster before a key frame of the
  first video track. Fix for bug 500.
* mkvmerge: enhancement: The default cluster length has been increased to five
  seconds (up from two seconds).
* mkvmerge: enhancement: Implemented write caching resulting in faster muxes
  especially on Windows writing to network shares.
* mkvmerge: new feature: Added support for reading PGS subtitles from PGS/SUP files.
* mkvmerge: enhancement: mkvmerge uses header removal compression by default for
  AC-3, DTS, MP3, Dirac and MPEG-4 part 2 tracks.
* all: Added a translation of the programs into Spanish by Isra Lucas (see AUTHORS).
* docs: Added a Dutch translation for the man pages by René Maassen (see AUTHORS).

## Bug fixes

* mkvmerge: bug fix: Fixed reading AVC/h.264 tracks from AVI files if they're stored
  in NALUs inside the AVI.
* mmg: bug fix: Matroska files read from/written to by the header and chapter editors
  will no longer be kept opened and locked. Fix for bug 498.
* mmg: bug fix: If mmg was called with "--edit-headers filename.mkv" then it crashed
  when the header editor was closed.
* mkvmerge: bug fix: mkvmerge will no longer report nonsensical progress reports
  (e.g. -17239182%) when reading Matroska files with all the flags "--no-audio
  --no-video --no-subtitles" enabled. Fix for bug 505.
* mmg: bug fix: Fixed a crash in the job runner when the total time was very big due to a
  division by zero.
* mkvmerge: bug fix: Specifying an FPS with "--default-duration" for AVC/h264
  tracks in AVI files did not work. Fix for bug 492.
* mkvmerge: bug fix: Fixed an invalid memory access possibly causing a crash in the
  AC-3 detection code.
* mmg: bug fix: Changing mmg's interface language did not change the entries in the
  "command line options" dialog if that dialog had been opened prior to the language
  change.
* mkvmerge: bug fix: Fixed access to uninitialized memory when reading DTS tracks
  from AVI and Matroska files.
* mkvmerge: bug fix: The Matroska reader will use the MPEG audio packetizer for MP2
  tracks instead of the passthrough packetizer.
* mkvmerge: bug fix: The Matroska reader did not handle compressed tracks correctly
  if the passthrough packetizer was used.
* mkvmerge: bug fix: The handling of Matroska files in which the 'default track flag'
  is not present has been fixed.

## Build system changes

* Build system: enhancement: Improved the error reporting if certain Boost
  libraries are not found.

## Other changes

* all: Added desktop files for mmg/mkvinfo, a MIME type file for .mmg files and icons to
  the installation procedure on Linux. Most files were contributed by Cristian
  Morales Vega (see AUTHORS).


# Version 4.0.0 "The Stars were mine" 2010-06-05

## New features and enhancements

* mmg: new feature: Added the estimated remaining time to the mux and job dialogs.
* all: Added a translation of the programs into Dutch by René Maassen (see AUTHORS).
* mmg: enhancement: The "mkvmerge executable" input in the preferences dialog is not
  read-only anymore. Final part of a fix for bug 490.
* mkvmerge: Added support for reading IVF files with VP8 video tracks.
* mkvextract: Added support for extracting VP8 video tracks into IVF files.
* mkvinfo: new feature: Added an option ("-z" / "--size") for displaying each
  element's size property. Elements with an unknown/infinite size are shown as "size
  is unknown".
* mmg: new feature: Added a checkbox for enabling "WebM" mode. This will also enable
  the same limitations that mkvmerge enables: Only VP8 and Vorbis tracks, no
  chapters, no tags. The output file name extension will be changed to ".webm" upon
  enabling the mode.
* mkvmerge: new feature: Neither chapters nor tags will be written to WebM compliant
  files. Warnings are issued if chapters or tags are found and not disabled.
* mmg: enhancement: Added "WebM" with the extension "webm" to the list of known file
  types.
* mkvmerge: new feature: Added support for muxing VP8 video tracks.
* mkvmerge: enhancement: mkvmerge will no longer put all clusters into a meta seek
  element resulting in smaller file size. The parameter
  "--no-clusters-in-meta-seek" has been renamed to "--clusters-in-meta-seek"
  and its meaning reverted.
* mkvmerge: enhancement: WebM compatibility mode will be turned on automatically if
  the output file name's extension is '.webm', '.webma' or '.webmv'.
* mkvinfo GUI: enhancement: Added "webm" to the list of known file name extensions for
  WebM files both for the "Open file" dialog and the drag & drop support.
* mkvmerge: new feature: Added options "--webm"/"--web-media" that enable the WebM
  compatibility mode. In this mode only Vorbis audio tracks and VP8 video tracks are
  allowed. Neither chapters nor tags are allowed. The DocType element is set to
  "webm".

## Bug fixes

* all command line tools: bug fix: Fixed the output of eastern languages like Japanese
  or Chinese under cmd.exe on Windows.
* mkvmerge: bug fix: Fixed support for reading FLAC tracks from Ogg files following
  the FLAC-in-Ogg-mapping established with FLAC v1.1.1. Fix for bug 488.
* mmg: bug fix: mmg will output a warning if it is used with a mkvmerge executable whose
  version differs from mmg's version. Part of a fix for bug 490.
* mmg: bug fix: If adding a file fails mkvmerge's error message will be shown in a
  scrollable dialog instead of a normal message box. Part of a fix for bug 490.
* mkvmerge, mkvinfo, mkvextract: bug fix: Fixed handling of clusters missing a
  cluster time code element.
* mkvinfo GUI: bug fix: Frames for simple blocks were shown at the wrong place in the
  element tree.
* mkvmerge, mkvextract: Fixed handling of clusters with an unknown size.
* mkvinfo: bug fix: Fixed handling clusters with an unknown size.
* mkvmerge: bug fix: Matroska files without clusters are accepted as valid input
  files again.
* mkvinfo GUI: bug fix: Opening more than one file without restarting mkvinfo GUI
  could result in wrong time codes due to variables not being reinitialized.
* mkvinfo: bug fix: Binary elements shorter than 10 bytes were not output correctly.
* Build system: bug fix: The man page installation process only installed the English
  originals instead of the Japanese and Chinese translations.

## Build system changes

* build: Building mkvtoolnix now requires libebml v1.0.0 and libmatroska v1.0.0 or
  later.

## Other changes

* mkvextract: feature removal: Removed support for extracting FLAC tracks into Ogg
  FLAC files. Instead they're always written into raw FLAC files. The option
  "--no-ogg" has been removed as well.
* mkvmerge: feature removal: Removed support for the FLAC library older than v1.1.1.
* mmg: Added 'IVF' files to the list of known input file types.
* mkvmerge: change: mkvmerge will not write track header elements whose actual value
  equals their default value anymore.


# Version 3.4.0 "Rapunzel" 2010-05-14

## New features and enhancements

* all: Added a translation into Ukrainian by Serj (see AUTHORS).
* Windows installer: Added the choice to run the installer in the same languages that
  the GUIs support. Patch by Serj (see AUTHORS) with modifications by myself.
* all: Added a translation into Russian by Serj (see AUTHORS).

## Bug fixes

* mkvmerge: bug fix: Fixed the handling of non-spec compliant AVC/h.264 elementary
  streams in Matroska files with the CodecID V_ISO/MPEG4/AVC. Fix for bug 486.
* mkvmerge: bug fix: mkvmerge will not output a message that it has extracted the
  display dimensions from AVC/h.264 bitstream if the source container (e.g.
  Matroska) overrides that setting. Fix for bug 485.
* mmg's header editor, mkvpropedit: Fixed crashes with files created by Haali's GS
  Muxer containing "content encoding" header elements.
* mkvextract: bug fix: Extracting SSA/ASS files which miss the "Text" column
  specifier in the "Format:" line are handled correctly. Fix for bug 483.
* mkvmerge: bug fix: Fixed a segfault when reading Matroska files containing level 1
  elements other than clusters with a size of 0.
* mkvmerge: bug fix: Fixed a tiny memory leak. Fix for bug 481.

## Build system changes

* build: Building mkvtoolnix now requires libebml v0.8.0 and libmatroska v0.9.0 or
  later.
* Build system: The LINGUAS environment variable determines which man page and guide
  translations will be installed.


# Version 3.3.0 "Language" 2010-03-24

## New features and enhancements

* mkvmerge: enhancement: Added a message in verbosity level 2 to the splitting code.
  It reports before which time code and after what file size a new file is started.
* All: enhancement: Added support for old Mac-style line endings (only '\r' without
  '\n') in text files.
* mmg: enhancement: Added the values "4483M" and "8142M" to the "split after this
  size" drop down box.
* mkvmerge, mkvextract: enhancement: Improved the error resilience when dealing
  with damaged Matroska files. When a damaged part is encountered reading will
  continue at the next cluster.
* mkvmerge: enhancement: Some Matroska files contain h.264/AVC tracks lacking
  their CodecPrivate element (e.g. files created by gstreamer's muxer). For such
  tracks the CodecPrivate element (the AVCC) is re-created from the bitstream. Fix
  for bug 470.

## Bug fixes

* mkvmerge: bug fix: Fixed the default duration for interlaced MPEG-1/2 video
  tracks. Also added the 'interlaced' flag for such tracks. Patches by Xavier Duret
  (see AUTHORS). Fix for bug 479.
* mkvmerge: bug fix: Specifying a FourCC with spaces at the end will not result in an
  error anymore. Fix for bug 480.
* mkvmerge: bug fix: Time Codes for MPEG-1/2 tracks are calculated properly,
  especially for B frames. Patch by Xavier Duret (see AUTHORS). Fix for bug 475.
* mkvmerge: bug fix: Fixed a crash when reading Matroska files that contain Vorbis
  audio with in MS compatibility mode (CodecID A_MS/ACM). Fix for bug 477.
* mmg: bug fix: Fixed compilation if gettext is not available.
* Build system: Added project files and fixes for compilation with Microsoft Visual
  Studio 8. Patches by David Player (see AUTHORS).
* Installer: bug fix: A couple of start menu links to pieces of the documentation were
  broken. Added missing start menu links to translations of the documentation.
* mkvmerge: bug fix: The SRT reader skips empty lines at the beginning of the file.
* Build system: bug fix: Fixed the configure script and compilation on OpenSolaris.
* Installer: bug fix: The "jobs" directory in the application data folder is removed
  during uninstallation if the user requests it. Fix for bug 474.
* mkvextract: bug fix: Fixed granulepos calculation when extracting Vorbis tracks
  into Ogg files. Fix for bug 473.
* All: bug fix: The programs will no longer abort with an error message if a selected
  interface translation is not available. The "C" locale is used instead. Fix for bug
  472.
* mkvmerge: bug fix: Fixed the handling of UTF-16 encoded chapter names in MP4/MOV
  files.
* mkvmerge: bug fix: MP4 files that do contain edit lists but whose edit lists do not
  span the entire file are processed properly. Such files are created by current x264
  builds. Fix for bug 469.
* Build system: Fixed configure for systems on which 'echo' does not support the '-n'
  parameter (e.g. Mac OS).

## Build system changes

* Build system: Sped up builds by using pre-compiled headers. Patches by Steve Lhomme
  (see AUTHORS) and myself.

## Other changes

* All: A lot of changes preparing mkvtoolnix for use with the upcoming
  libebml2/libmatroska2 versions were applied. Patches by Steve Lhomme (see
  AUTHORS).


# Version 3.2.0 "Beginnings" 2010-02-11

## New features and enhancements

* docs: Added a Chinese Simplified translation for the man pages by Dean Lee (see
  AUTHORS).
* mmg: enhancement: Added an input field for the segment info XML file (mkvmerge's
  "--segmentinfo" option) on the "global" tab.
* mmg: enhancement: Changing the interface language does not require a restart
  anymore.
* mkvinfo: enhancement: Added the "EBML maximum size length" element to the list of
  known elements. Fix for bug 464.
* mmg: new feature: Added a control for mkvmerge's "--cropping" parameter.
* mmg: enhancement: Added the file extensions ".dtshd", ".dts-hd", ".truehd" and
  ".true-hd" to mmg's"'add/append file" dialogs.

## Bug fixes

* mmg, mkvpropedit: Fixed another bug causing a crash writing chapters/other
  elements to existing Matroska files.
* Build system: bug fix: Improved detection of Boost::Filesystem for newer Boost
  versions.
* mkvmerge: bug fix: Outputting error messages about invalid XML files will not cause
  mkvmerge to crash on Windows anymore.
* mmg: bug fix: The jobs will be saved in the 'mkvtoolnix/jobs' sub-directory of the
  'application data' folder instead of the 'jobs' folder in the current directory. On
  Windows this is the special 'application data' folder inside the user's profile
  directory, e.g. 'C:\Users\mbunkus\AppData\mkvtoolnix'. On non-Windows
  systems this is the folder '.mkvtoolnix' in the user's home directory. mmg's
  configuration file has also been moved from ~/.mkvmergeGUI to
  ~/.mkvtoolnix/config on non-Windows systems. Fix for bug 466.
* mkvextract: bug fix: Files are only opened for reading, not for writing, so that
  mkvextract will work on files the user only has read-only permissions for.
* mkvextract: bug fix: Modes 'attachments', 'chapters', 'tags' and 'cuesheet':
  mkvextract will output an error message if the file cannot be opened (e.g. because it
  does not exist or due to lack of access).
* mkvmerge: bug fix: Reading VOB files bigger than 4 GB was broken in v3.1.0 on 32bit
  platforms.
* mmg: bug fix: Tooltips were not word-wrapped on Windows.
* mkvextract: bug fix: "mkvextract --version" was only writing an empty string. Fix
  for bug 463.

## Build system changes

* Build requirements changed: The GUIs for mkvtoolnix now require wxWidgets 2.8.0 or
  newer.


# Version 3.1.0 "Happy Up Here" 2010-01-19

## New features and enhancements

* documentation: Added a Chinese Simplified translation for mmg's guide by Dean Lee
  (see AUTHORS).
* documentation: Added a Japanese translation for the man pages by Katsuhiko
  Nishimra (see AUTHORS).
* mmg: enhancement: After muxing the "abort" button is changed to "open folder" which
  opens the explorer on the output file's folder. This only happens on Windows.
* mmg: enhancement: When constructing the output file name mmg will only suggest
  names that don't exist already by appending a number to the file name (e.g. resulting
  in "/path/file (1).mkv").
* mkvmerge: new feature: Added support for reading chapters from MP4 files that are
  stored in tracks with subtype 'text'. Such files are used e.g. on iPods/iPhones and
  can be created by HandBrake. Fix for bug 454.
* mkvmerge: enhancement: SRT files with negative time codes will are not rejected
  anymore. Negative time codes will be adjusted to start at 00:00:00.000.
* mkvextract: new feature: Added support for extracting Blu-Ray subtitles (CodecID
  "S_HDMV/PGS").
* mkvmerge/mmg: enhancement: Added an option '--segment-uid' for specifying the
  segment UIDs to use instead of having to use an XML file and '--segmentinfo'.

## Bug fixes

* mkvmerge: bug fix: If the first input file was a Quicktime/MP4 file and all tracks
  from that file were deselected for muxing then mkvmerge would crash. Fix for bug 458.
* mmg: bug fix: The option 'AAC is SBR/HE-AAC' was not honored for appended AAC tracks.
  This could lead to mkvmerge aborting with an error that the track parameters did not
  match if it itself could not detect HE-AAC in the second file.
* mmg: bug fix: The output file name is checked for invalid characters before the
  muxing process is started. Fix for bug 455.
* mkvpropedit, mmg: bug fix: Files with an infinite segment size are handled
  correctly now. Fix for bug 438.
* mkvmerge: bug fix: Matroska files which have its 'tracks' element located behind
  the clusters are read correctly now.
* mmg: bug fix: The "tags" input box on the "general track options" tab was not updated
  when a track was selected. Fix for bug 453.
* mkvmerge, mmg: new feature: If a MPEG-2 program stream file is added to mkvmerge
  whose base name ends in a number then mkvmerge will automatically read and process
  all other files in the same directory with the same base name, same extension and
  different numbers. Those files are treated as if they were a single big file. This
  applies e.g. to VOB files from DVD images that are named VTS_01_1.VOB,
  VTS_01_2.VOB, VTS_01_3.VOB etc. mmg will output an error message if the user tries
  to add or append one of the other files that mkvmerge will process automatically
  (e.g. if the user has added VTS_01_1.VOB already and tries to append VTS_01_2.VOB).
  This also fixes bug 437.
* mkvmerge: bug fix: Zero-length frames in Theora bitstreams as created by libtheora
  v1.1 and later were dropped. Fix for bug 450.
* mmg: bug fix: On Windows 2000/XP the 'add/append file' dialog was not showing files
  with certain extensions (e.g. ".srt" or ".mp4") if the option "all supported media
  files" was selected. Fix for bug 448.

## Build system changes

* Build requirements changed: mkvtoolnix requires Boost v1.34.0 or later. It
  requires the Boost::Filesystem library (with all Boost versions) and the
  Boost::System libraries (starting with Boost v1.35.0).

## Other changes

* Installer: The installer will no longer offer to run mmg after it has been installed.
  On Windows setups where a normal user account doesn't have administrator
  privileges this caused mmg to be run as the user "Administrator" instead of the
  normal user account causing confusion and some things not to work, e.g. drag & drop.


# Version 3.0.0 "Hang Up Your Hang-Ups" 2009-12-12

## New features and enhancements

* mmg: enhancement: Added support for showing the muxing progress for both normal
  muxes and the job manager in Windows 7's taskbar.
* all: enhancement for Windows platforms: If one of the mkvtoolnix components is run
  without having been installed before then translations will be read from the
  directory the .exe is run from.
* configure: Added an option ('--without-build-timestamp') that omits the build
  timestamp from all version information so that two builds of mkvtoolnix can be
  byte-identical.

## Bug fixes

* all: bug fix: The charset for output in cmd.exe for non-English interface languages
  has been fixed on Windows Vista and Windows 7.
* mkvpropedit, mmg: bug fix: Editing headers of files created by HandBrake would
  cause crashes and/or corrupted files after saving. Fix for bug 445.


# Version 2.9.9 "Tutu" 2009-11-25

## New features and enhancements

* mkvmerge: enhancement: A single '+' causes the next file to be appended just like
  putting the '+' in front of the file name.
* mmg: enhancement: The file dialogs for 'add file'/'append file' will show files
  with extensions in all uppercase as well. This only applies to file systems that
  distinguish between case (e.g. most of the non-Windows, non-FAT world). Fix for bug
  433.

## Bug fixes

* mmg: bug fix: The warning that no FPS has been entered for AVC/h.264 elementary
  streams is not shown anymore for appended tracks (only once for the first track that
  they're appended to).
* mkvmerge: bug fix: The pixel cropping parameters were not kept when muxing from
  Matroska files.
* mkvmerge: bug fix: The display width/height parameters were not kept when muxing
  from Matroska files if the bitstream of the track contained different aspect ratio
  information. Now the order is "command line" first, "source container" second and
  "bitstream" last.
* mkvmerge: bug fix: Fixed the subtitle track selection for AVI files.
* mkvmerge: bug fix: The integrated help ('--help') contained wrong information
  about the '--sync' option. Fix for bug 435.
* mkvmerge: bug fix: Missing ChapterLanguage elements were assumed to be set to
  "und". They're now assumed to be "eng" in accordance with the Matroska
  specifications.

## Other changes

* Added a new program 'mkvpropedit' that can modify certain properties of existing
  Matroska files. It is mmg's header editor, just for the command line.


# Version 2.9.8 "C'est le bon" 2009-08-13

## New features and enhancements

* mmg/header editor: enhancement: Implemented a considerable speedup in the
  processing of large files.
* mkvinfo: enhancement: Implemented speed-ups of up to 50% for middle to larger
  files.

## Bug fixes

* mmg: bug fix: The inputs for time code files and the language are deactivated if the
  user has selected a track that will be appended to another track. Fix for bug 432.
* mmg: bug fix: mmg will handle multiple consecutive spaces in the options given with
  "add command line options" properly and not cause mkvmerge to exit with misleading
  error messages anymore. The "add command line options" dialog has been reordered,
  and the drop down box in it is now read-only. Fixes for bug 429.
* doc, mmg: bug fix: The tooltip and documentation for the 'delay' option was
  misleading. Fix for bug 420.
* mmg/header editor: bug fix: The header editor copes better with files that do not
  contain all mandatory header fields. The missing ones are added or assumed to be
  default values.
* all: bug fix: Selecting the translations with the "--ui-language" option did not
  work on Mac OS X.
* mkvmerge: bug fix: Fixed an invalid memory access in the VobSub reader module. Fix
  for bug 426.
* mmg: bug fix: Tracks added from Matroska files did not get their 'default track' drop
  down box set correctly if the flag was 'off' in the source file and no other track of its
  kind hat its 'default track' flag set.
* mkvmerge: bug fix: mkvmerge was wrongly turning the 'default track' flag back on for
  the first subtitle track muxed from a Matroska file if none of the subtitle tracks
  muxed had their 'default track' flag set in their source files and if the user didn't
  use the '--default-track' option for setting that flag explicitely.
* mmg: bug fix: Running mkvmerge on Windows from an installation directory with two
  spaces in the path (e.g. "C:\Program Files\Video tools\mkvtolnix") crashed mmg
  when the user started muxing. Fix for bug 419.
* mmg: bug fix: Files for which all tracks were disabled were left out from mkvmerge's
  command line so that tags, chapters and attachments were not copied from such files
  either.
* mkvmerge: bug fix: Appending MPEG4 part 2 video tracks from Matroska files which
  contain aspect ratio information will not result in an error message "connected_to
  > 0" anymore. Fix for bug 427.
* mkvmerge: bug fix: Fixed the audio sync for tracks read from AVI files containing
  garbage at the beginning. Fix for bug 421.
* mmg: bug fix: Trying to save chapters that contain editions without a single chapter
  entry does no longer result in a crash but a descriptive error message instead.
  Saving empty chapters to a Matroska file will remove all chapters contained in the
  file instead of not doing anything. Fixes for bug 422.
* mkvmerge: bug fix: Fixed reading AVC/h.264 video tracks from OGM files. Fix for bug
  418.
* mmg: bug fix: The chapter language for chapters copied from source files (e.g.
  Matroska, MP4 or OGM files) is only changed if the user has selected any language
  other than "und". Fix for bug 420.
* mmg: bug fix: mmg will no longer show an error message if the user has not selected a
  country in the "chapters" tab of the preferences dialog.


# Version 2.9.7 "Tenderness" 2009-07-01

## New features and enhancements

* mmg: new feature: The list of common languages can be configured by the user via the
  'preferences' dialog.
* mkvmerge, mmg: new feature: The language for chapters read from files such as OGM and
  MP4 files can be chosen with the --chapter-language command line option. Fix for bug
  399.
* mkvmerge, mmg: new feature: Chapter and tag information contained in source files
  are now shown in the "track" selection box and can be toggled individually. The user
  can set the charset for chapters if the source file's chapters are not encoded in
  UTF-8 (e.g. in some OGM/MP4 files). The old file specific checkboxes "no tags" and
  "no chapters" have been removed. Fix for bug 400.
* mmg: enhancement: If the user selects the option "Verify" from the "Chapters" menu
  then a message will be shown even if the validation succeeded. Fix for bug 410.

## Bug fixes

* mkvmerge: bug fix: The handling of NVOPs in native MPEG4 part 2 video storage has been
  improved. NVOPs are dropped again both from packed and non-packed bitstreams, and
  time codes are adjusted to match the number of dropped frames.
* mkvmerge: bug fix: The I frame detection for AVC/h.264 video has been fixed.
  Sometimes a single I frame was recognized as two or more consecutive I frames
  resulting in garbled display and wrong timestamps. Fix for bug 415.
* all: bug fix: The programs do not try to close iconv handles -1 anymore which resulted
  in segfault during uninitialization on some platforms (e.g. FreeBSD, Mac OS X). Fix
  for bug 412.
* mkvmerge: bug fix: Complete rewrite of the code for the native storage mode for MPEG4
  part 2 video tracks. Fix for bug 298.
* mkvmerge: bug fix: Made the detection rules for raw MP3, AC-3 and AAC audio files more
  strict. This avoids a mis-detection of certain files, e.g. AVC/h.264 ES files being
  misdetected as MP3 files. Fix for bug 414.
* mkvmerge: bug fix: Appending MP4 or OGM files with chapters will merge the chapters
  from all appended files and not just take the chapters from the first file and discard
  the chapters from the following files. Fix for bug 397.
* mmg: bug fix: The chapter editor was corrupting Matroska files if the chapters were
  saved to a file twice in a row without reloading them from the Matroska file.
* mmg/mkvmerge: bug fix: Adding MP4 files with chapter entries that are not encoded in
  UTF-8 will not result in mkvmerge aborting with a message about invalid UTF-8
  sequences anymore. Fix for bug 408.

## Other changes

* mmg: The "preferences" dialog has been split up into several tabs. Some other
  preferences available in other dialogs have been merged into the "preferences"
  dialog.


# Version 2.9.5 "Tu es le soul" 2009-06-06

## New features and enhancements

* mkvmerge: new feature: Improved the control over which tags get copied from a source
  file to the output file. The old option "--no-tags" was replaced with the new options
  "--no-global-tags" which causes global tags not to be copied and
  "--no-track-tags" which causes track specific tags to not be copied. The new option
  "--track-tags" can be used to select tracks for which tags will be copied. The
  default is still to copy all existing tags.
* mkvmerge: new feature: Included chapters, global and track specific tracks in the
  output of mkvmerge's identification mode.
* mkvmerge: new feature: Added support for the FourCCs ".mp3" and "XVID" in QuickTime
  files.

## Bug fixes

* mkvmerge: bug fix: The handling of TrueHD/MLP audio in MPEG program streams was
  broken resulting in many dropped packets.
* all: bug fix: There was the possibility that invalid memory access occured and e.g.
  mkvmerge crashed on systems with the posix_fadvise() function (mainly Linux) if
  mkvmerge was opening several files from certain file systems (e.g. VFAT). Apart
  from obvious crashes the only other side effect was that the posix_fadvise()
  function would not be used resulting in slightly worse I/O performance.
* mkvmerge: bug fix: The sequence header of MPEG-1/2 video tracks is put into the
  CodecPrivate again while still leaving sequence headers in the bitstream as well.
  This is more compatible with some existing parsers.
* mmg: bug fix: Removed the check if the user has added tracks and files before starting
  mkvmerge because mkvmerge itself is able to create track-less files (e.g. chapters
  only). Fix for bug 402.
* mkvmerge: bug fix: Improved the handling of consecutive AC-3 packets with the same
  time code (e.g. if AC-3 is read from MP4 files). Fix for bug 403.
* mkvmerge: bug fix: Fixed an endless loop in the TrueHD code occuring when the TrueHD
  stream is damaged somewhere.
* mkvmerge: bug fix: Fixed the detection of MPEG transport streams with other packet
  sizes than 188 bytes (e.g. 192 and 204 bytes).
* mkvmerge: bug fix: The detection of invalid padding packet lengths in the MPEG
  program stream reader was improved to not produce as many false positives. Patch by
  Todd Schmuland (see AUTHORS). Fix for bug 393.
* mmg: bug fix: Pressing 'return' in the job dialog will close the dialog on Windows,
  too. Fix for bug 392.
* mmg: bug fix: Fixed the behaviour of how mmg sets the output file name automatically
  if the option is enabled. If the user adds more than one file then the extension of the
  output file name is set each time a file is added and not only when the first one is. The
  full file name and path will only be set when the first file is added. Fix for bug 391.

## Other changes

* mkvmerge: Renamed a couple of command line options to make the command line
  interface more consistent: "--no-audio", "--no-video", "--no-subtitles",
  "--no-buttons", "--audio-tracks", "--video-tracks", "--subtitle-tracks",
  "--button-tracks". The old versions of these options "--noaudio", "--novideo",
  "--nosubs", "--nobuttons", "--atracks", "--vtracks", "--stracks" and
  "--btracks" still work.


# Version 2.9.0 "Moanin'" 2009-05-22

## New features and enhancements

* all: Added a translation to Traditional Chinese by Dean Lee (see AUTHORS).
* mkvmerge: new feature: Added a hack ('vobsub_subpic_stop_cmds') that causes
  mkvmerge to add 'stop display' commands to VobSub subtitle packets that do not have a
  duration field. Patch by Todd Schmuland (see AUTHORS).
* mmg: enhancement: Changed how mmg sets the output file name automatically if the
  option is enabled. If the user adds more than one file then the output file name is set
  each time a file is added and not only when the first one is unless the user has changed
  the output file name manually. Fix for bug 229.
* mkvmerge: enhancement: Improved support for QuickTime audio tracks with version 2
  of the STSD sound descriptor.
* mkvmerge: enhancement: The MPEG program stream reader will now detect invalid
  padding packets and skip only to the next 2048 byte packet boundary instead of
  skipping several good packets. Patch by Todd Schmuland (see AUTHORS).
* mmg: enhancement: The "no chapters" checkbox can now be used for QuickTime/MP4
  files and OGM files as well.
* mkvmerge: enhancement: The OGM reader will only print the warning that no chapter
  charset has been set by the user if the title or the chapter information contained in
  the OGM file is actually used and not overwritten with '--title ...' or
  '--no-chapters'.
* mkvmerge: new feature: Added support for handling MPEG-1/-2 video in AVI files. Fix
  for bug 388.
* mkvmerge: enhancement: Implemented small speedups for some common memory
  operation (affects e.g. the MPEG program stream parser).
* mkvmerge: new feature: Added support for reading chapters from MP4 files. Fix for
  bug 385.

## Bug fixes

* mkvmerge: bug fix: mkvmerge was not handling dropped frames well when converting
  from VfW-mode MPEG-4 part 2 to native mode MPEG-4 part 2 (with '--engage
  native_mpeg4'). This resulted in time codes being to low which in turn resulted in
  the loss of audio/video synchronization. Fix for bug 236.
* mkvextract: bug fix: The modes 'chapters', 'cuesheet' and 'tags' did not honor the
  '--redirect-output' option and where always writing to the standard output.
* mmg: bug fix: The "remove all" button was sometimes disabled even though there were
  still files left to be removed.
* mkvextract: bug fix: The VobSub extraction was made more compatible with most
  applications. Fix for bug 245. Patch by Todd Schmuland (see AUTHORS).
* mkvmerge: bug fix: Fixed support for Windows systems that use code pages that are not
  supported by the iconv library (e.g. code page 720). mkvmerge was exiting with
  warnings causing mmg to report that file identification had failed. Fix for bug 376.
* all: bug fix: Global variables are deconstructed in a pre-defined way no longer
  causing segfaults when the programs are about to exit.
* mkvmerge: bug fix: Fixed potential and actual segmentation faults occuring when
  appending VC-1 video tracks, Dirac video tracks and DTS audio tracks.
* mmg: bug fix: The header and chapter editors will no longer crash the application if
  the user wants to open a file that's locked by another process and show an error
  message instead.
* mkvmerge: enhancement: Invalid VobSub packets whose internal SPU length field
  differs from its actual length are patched so that the SPU length field matches the
  actual length. This fixes playback issues with several players and filters. Fix for
  bug 383.


# Version 2.8.0 "The Tree" 2009-05-09

## New features and enhancements

* all: Added a translation to Chinese (simplified) by Dean Lee (see AUTHORS).
* mkvmerge: enhancement: Added support for handling AC-3 audio in MP4 files with the
  FourCC "sac3" (as created by e.g. Nero Recode v3/4). Fix for bug 384.
* mkvmerge, mmg: enhancement: Made mmg's "FPS" input field available for all video
  tracks. mkvmerge's corresponding option "--default-duration" now not only
  modifies the track header field but affects the frame time codes as well.
* mmg: enhancement: Added "60000/1001" as a pre-defined option to the "FPS"
  drop-down box.
* mmg: new feature: Added an option for clearing all inputs after a successful muxing
  run.

## Bug fixes

* mkvmerge: bug fix: The VobSub reader was dropping the very last MPEG packet possibly
  resulting in the very last subtitle entry being garbled or discarded completely.
  Patch by Todd Schmuland.
* mmg (header editor): bug fix: The header editor controls on the right stopped
  responding after the second file had been loaded or the "reload file" feature had
  been used. Fix for bug 372.
* mkvmerge: bug fix: Made the AAC detection code stricter in what it accepts. This
  results in fewer mis-detections. Fix for bugs 373 and 374.
* mkvmerge: bug fix: Splitting without the option "--engage no_simpleblocks"
  resulted in broken files: all frames were marked as B frames. Fix for bug 371.
* mkvinfo: bug fix: Time Codes of SimpleBlock elements that were output formatted in
  summary mode were too small by a factor of 1000000.
* mkvmerge: bug fix: The duration of subtitle frames was overwritten with the
  difference between the next frame's time code and the current frame's time code if a
  time code file was used for that track. Fix for bug 286.
* mmg: bug fix: Removed the option "always use simple blocks" from the preferences
  dialog as this option was already removed from mkvmerge. Fix for bug 370.


# Version 2.7.0 "Do It Again" 2009-04-14

## New features and enhancements

* mkvmerge, mmg: new feature: Added support for the "forced track" flag. Fix for bug
  128.
* mmg: new feature: Added drag & drop support for the header editor (files can be opened
  by dropping them on the header editor).
* mkvmerge: new feature: Added support for reading the track language from
  QuickTime/MP4 files. Thanks to Eduard Bloch for the code for unpacking the language
  string.
* mkvmerge, mkvextract: new feature: Added support for MLP audio.
* mkvmerge, mkvetract: new feature: Added support for TrueHD audio (read from raw
  streams with or without embedded AC-3 frames, MPEG program streams).

## Bug fixes

* mmg: bug fix: The header editor and chapter editor will not write zero bytes anymore
  if there's not enough space to write an EbmlVoid element when saving to Matroska
  files.
* mkvmerge: bug fix: Fixed the aspect ratio extraction for AVC/h.264 video by adding
  three more pre-defined sample aspect ratios. Mkvmerge also only assumes "free
  aspect ratio" if the aspect ratio type information indicates it and not if the type
  information is unknown.
* mmg: bug fix: All arguments are shell escaped and quoted instead of only those with
  spaces in them. Only applies to the menu options "show command line", "save command
  line to file" and "copy command line to clipboard". Fix for bug 364.
* mmg: bug fix: When adding a file with colons in the segment title all colons were
  replaced with the letter 'c'.
* mmg: bug fix: The job manager's status output was garbled if mmg was run with another
  language as English.
* mmg: bug fix: The progress bar for each individual job in the job dialog wasn't
  updated if mmg was run with another language as English.
* mmg: bug fix: The time codes in the job queue editor were off by one month. The "added
  job on" was additionally off by an amount depending on the user's time zone. Fix for
  bug 362.
* mkvmerge: bug fix: The MPEG program stream (VOB/EVO) reader was sometimes reading
  the time codes wrong resulting in bad audio/video synchronization. Fix for bug 337.

## Build system changes

* Build requirements changed: mkvtoolnix requires Boost v1.32.0 or later.

## Other changes

* mkvmerge: mkvmerge will now use SimpleBlock elements instead of normal BlockGroup
  elements by default.
* mkvmerge: By default mkvmerge keeps the aspect ratio information in AVC/h.264
  video bitstreams now (equivalent to specifying "--engage
  keep_bitstream_ar_info" in earlier versions). A new option "--engage
  remove_bitstream_ar_info" is available that restores the previous behaviour.


# Version 2.6.0 "Kelly watch the Stars" 2009-03-24

## New features and enhancements

* mmg: new feature: Added a header editor for Matroska files.
* all: Added a Japanese translation by Hiroki Taniura (see AUTHORS).
* mkvinfo: enhancement: If mkvinfo is started in GUI mode on Windows then the console
  that was started with it will be closed.

## Bug fixes

* mkvextract: bug fix: The "simple" chapter extraction mode (OGM style chapter
  output) outputs strings converted to the system's current charset by default now
  instead of always converting to UTF-8. This can be overridden with the
  "--output-charset" command line option. Fix for bug 359.
* mkvmerge: bug fix: QuickTime audio tracks will be stored with the CodecID
  "A_QUICKTIME". The CodecPrivate element contains the full "STSD" element from the
  QuickTime file (just like V_QUICKTIME). This method is used for all audio tracks
  which don't have a well-defined storage spec for Matroska (e.g. AAC, AC-3, MP2/3 are
  still stored as A_AC3, A_AAC etc). Hopefully a fix for bugs 354 and 357.
* mkvmerge: bug fix: The CodecPrivate element for QuickTime video tracks like
  Sorenson Video Codecs contained wrong data. Fix for bug 355.
* mkvmerge: bug fix: Fixed detection of little endian PCM tracks in MOV files. Fix for
  bug 356.
* mkvextract: bug fix: The charset for text output was not initialized correctly
  resulting in garbled display of non-ASCII characters in non-UTF-8 locales.
* all: bug fix: A couple of translated strings were converted from the wrong locale
  when they were displayed.
* all: bug fix: The tools use the API call "GetOEMCP()" on Windows instead of
  "GetACP()". This should make messages output in cmd.exe come out correctly for
  Windows versions for which cmd.exe uses a different code page than the rest (e.g. on
  German Windows).
* mkvinfo: bug fix: Chapter names and tag elements were recoded to the wrong charset
  resulting in garbled output. Fix for bug 353.

## Other changes

* mmg, mkvinfo: The GUIs now require an Unicode-enabled version of wxWidgets.


# Version 2.5.3 "Boogie" 2009-03-07

## Bug fixes

* mkvmerge, mkvextract, mmg: bug fix: If the environment variables LANG, LC_ALL,
  LC_MESSAGES contained a locale that was supported by the system but for which
  mkvtoolnix did not contain a translation (e.g. fr_FR, it_IT, en_AU) then the
  programs would abort with an error message. Fix for bug 338.
* mkvmerge: bug fix: Appending raw AVC/ES files resulted in segmentation faults. Fix
  for bug 344.
* mkvmerge: bug fix: When mkvmerge was run with the --attachments option to copy only
  some of the attachments in a Matroska file then any attachment with an ID larger than
  the first skipped attachment ID was not copied into the new file. Fix for bug 346.


# Version 2.5.2 "Stranger in your Soul" 2009-02-28

## New features and enhancements

* mmg: new feature: Added two buttons "enable all" and "disable all" to the list of
  attached files that enable / disable all attached files.
* mkvinfo: new feature: Made mkvinfo's GUI translatable. Added a German translation
  for the GUI.

## Bug fixes

* installer: bug fix: If the installer is run in silent mode (switch "/S") then it will
  not ask the user whether or not to place a shortcut on the desktop, and that shortcut
  will not be created. Fix for bug 345.
* mmg: bug fix: The action "File" -> "New" did not clear the internal list of attached
  files resulting in unexpected behaviour if files with attachments where added
  afterwards.
* mmg: bug fix: The button "remove all files" did not clear the list of attached files.
* mmg: On Linux wxWidgets 2.8.0 and newer uses the GTK combo boxes which suck. A lot.
  Therefore mmg uses wxBitmapComboBoxes for wxWidgets >= 2.8.0 on Linux and normal
  wxComboBoxes in all other cases. wxBitmapComboBoxes are still drawn by wxWidgets
  itself (just like wxComboBoxes before 2.8.0) and offer much better functionality.
  Fix for bug 339.
* mkvmerge, mmg: bug fix: The MIME type autodetection for attachments was broken for
  paths with non-ASCII characters on non-UTF-8 encoded systems (mostly on Windows).
  Fix for bug 340.
* source: various fixes for compilation with wxWidgets 2.9.
* all programs: bug fix: The locale was not detected properly often resulting in the
  program aborting with the message that "the locale could not be set properly". Fix
  for bug 338.


# Version 2.5.1 "He Wasn't There" 2009-02-22

## Bug fixes

* mmg: bug fix: Fixed the selection of the translation to use if the LC_MESSAGES
  environment variable has been set on Windows.


# Version 2.5.0 "Back To The Start" 2009-02-21

## New features and enhancements

* mkvmerge, mkvinfo, mkvextract, mmg: Made all those programs nearly completly
  translatable. Added a German translation for all four programs (only for the
  programs, not for the static documentation: man pages, the guide to mmg etc).
* mkvmerge, mmg: new feature: Added options ('-m' / '--attachments' and its
  counterparts '-M' / '--no-attachments') to mkvmerge for selecting which
  attachments to copy and which to skip and the corresponding controls to mmg.

## Bug fixes

* mmg: bug fix: Fixed a crash during the check if files could be overwritten by the next
  mux. Possible fix for bugs 335 and 336.
* mkvmerge: bug fix: Fixed the detection of AAC files whose first AAC header does not
  start on the first byte of the file.
* mmg: bug fix: It was possible to crash mmg by clicking onto the root element in the
  chapter editor.
* mkvextract: bug fix: During time code extraction mkvextract wrote large time codes
  in scientific notation.


# Version 2.4.2 "Oh My God" 2009-01-17

## New features and enhancements

* mkvmerge, mmg: enhancement: Implemented MIME type detection for attachments with
  libmagic on Windows.
* mkvmerge: enhancement: Decreased the time mkvmerge needs for parsing
  Quicktime/MP4 header fields.
* mkvmerge: new feature: Added support for reading the pixel aspect ratio from Theora
  video tracks.

## Bug fixes

* mkvmerge: bug fix: If subtitle files are appended to separate video files (e.g. two
  AVI and two SRT files) then the subtitle time codes of the second and all following
  subtitle files were based on the last time code in the first subtitle file instead of
  the last time code in the first video file. Fix for bug 325.
* mkvmerge: bug fix: Due to uninitialized variables mkvmerge would report OGM files
  as having arbitrary display dimensions. Fix for bug 326.
* mkvmerge: bug fix: If a Matroska file containing attachments was used as an input
  file and splitting was enabled then the attachments were only written to the first
  output file. Now they're written to each output file. Partial fix for bug 324.
* mkvmerge: bug fix: The parser for the simple chapter format (CHAPTERxx=...) can now
  handle more than 100 chapters. Fix for bug 320.
* mmg: bug fix: The commands "Save command line" and "Create option file" did not save
  mmg's current state but the state it was in when the command "Show command line" was
  last used or when mmg was started.
* mkvmerge: bug fix: Fixed a crash (segfault) with MPEG-4 part 2 video if "--engage
  native_mpeg4" is used. Fix for bug 318.
* Windows installer: The installer cleans up leftovers from old installations
  during an upgrade. It doesn't write registry entries for an exe called
  "AppMainExe.exe" anymore. It asks whether or not the user wants a shortcut on the
  desktop. It does not install the document for base64tool anymore because
  base64tool itself isn't installed anymore either. Fixes for bugs 314, 315, 316 and
  317.
* mmg: bug fix: Fixed a compilation problem with non-Unicode enabled wxWidgets. Fix
  for bug 313.


# Version 2.4.1 "Use Me" 2008-12-04

## New features and enhancements

* mkvmerge: new feature: Added support for reading SRT and SSA/ASS subtitles from AVI
  files (fix for bug 64).

## Bug fixes

* Build system: bug fix: Configure does not use "uname -m" for the detection of the
  Boost libraries anymore but configure's "$target" environment variable. This
  fixes the Boost detection for cross compilation builds. Fix for bug 311. Patch by
  Dominik Mierzejewski (see AUTHORS).
* mkvmerge: bug fix: PCM audio tracks bigger than approximately 8 GB were cut off after
  approximately 8 GB.
* mkvmerge: bug fix: mkvmerge recognizes SRT subtitle files with time codes that
  contain spaces between the colons and the digits and time codes whose numbers are not
  exactly two or three digits long.
* mmg: bug fix: mmg processes window events much more often during muxing.
* mmg: bug fix: Split time codes with more than three decimals were not allowed even
  though the docs say that they are. They are now, as mkvmerge supports such time codes.
* mkvmerge: bug fix: Changed the way mkvmerge calculates the time codes when
  appending files. Should result in better audio/video synchronization.
* mkvmerge: bug fix: mkvmerge's LZO compressor would segfault if mkvmerge was
  compiled against v2 of the LZO library and the v1 LZO headers were not present.
* mkvmerge: bug fix: SRT subtitle files are also handled correctly if the time code
  lines do not have spaces around the arrow between the start and end time codes.
* mkvextract: bug fix: Matroska elements with binary data were output as garbage in
  XML files.

## Other changes

* all: Updated the language code list from the offical ISO 639-2 standard.


# Version 2.4.0 "Fumbling Towards Ecstasy" 2008-10-11

## New features and enhancements

* mkvmerge: enhancement: mkvmerge will use the time codes provided by the MPEG
  program stream source file for VC-1 video tracks.
* mkvextract: new feature: Added support for handling SimpleBlocks for time code
  extraction.
* mkvmerge: new feature: Added support for Dirac video tracks.
* mmg: enhancement: Added the extensions "evo", "evob" and "vob" to mmg's "add file"
  dialog.
* mkvmerge: new feature: Added support for muxing VC-1 video tracks read from MPEG
  program streams (EVOBs) or raw VC-1 elementary streams (e.g. as produced by
  EVODemux).
* mkvmerge: new feature: Added support for 7.1 channel E-AC-3 files. Fix for bug 301.
* mkvextract: new feature: Added support for extracting Theora video tracks into Ogg
  files. Fix for bug 298.

## Bug fixes

* mmg: bug fix: The chapter editor's function "save to Matroska file" was corrupting
  the target file in some cases. Fix for bug 307.
* mkvmerge: bug fix: mkvmerge was only writing one reference block for real B frames.
  Patch by Daniel Glöckner. Fix for bug 306.
* all: bug fix: The Windows uninstaller was not removing all start menu entries during
  uninstallation on Windows Vista. The installer now creates the start menu entries
  for all users instead of the current user only. Fix for bug 305.
* mmg: bug fix: The "language" drop down box contained some entries twice or more. Fix
  for bug 304.
* mkvmerge: bug fix: Incorrect usage of the iconv library caused some conversions to
  omit the last character of each converted entry (e.g. for the conversion from Hebrew
  to UTF-8). Fix for bug 302.
* mkvmerge: bug fix: Reading EVOBs with multiple VC-1 video tracks was broken (all
  packets where put into a single video track).
* mkvmerge: bug fix: Reading raw (E)AC-3 files bigger than 2 GB was broken.
* mkvmerge: bug fix: Improved the detection of MPEG-1/-2 and AVC/h.264 video tracks
  in MPEG program streams (VOBs/EVOBs).
* mkvmerge: bug fix: Fixed reading DTS audio tracks from MPEG program streams
  (VOBs/EVOBs).
* mkvmerge: bug fix: Revision 3831 (the change to the "--delay" and "--sync" options)
  caused mkvmerge to no longer respect the delay caused by garbage at the beginning of
  MP3 and AC-3 audio tracks in AVI files. The time codes of such tracks are now delayed
  appropriately again. Fix for bug 300.
* mkvmerge: bug fix: Unknown stream types in Ogg files (e.g. skeleton tracks) don't
  cause mkvmerge to abort anymore. They're simply ignored. Fix for bug 299.
* mkvmerge: bug fix: Fixed the frame type (key or non-key frame) detection for Theora
  tracks.

## Other changes

* mkvmerge: all: On Unix/Linux rpath linker flags have been removed again (they were
  actually removed before the release of v2.3.0).


# Version 2.3.0 "Freak U" 2008-09-07

## New features and enhancements

* mkvmerge: new feature: Added support for Vorbis in AVI (format tag 0x566f). Fix for
  bug 271.
* mkvmerge: new feature: Added support for PCM tracks with floating point numbers
  (CodecID A_PCM/FLOAT/IEEE). Patch by Aurelien Jacobs (see AUTHORS).
* mkvmerge: new feature: Added support for Ogg Kate subtitles. Patch by
  ogg.k.ogg.k@googlemail.com.
* mkvmerge: enhancement: mmg outputs a more informative error message for known but
  unsupported input file types (e.g. ASF, FLV, MPEG TS) instead of the cryptic "file
  identification failed".
* mkvmerge: new feature: Improved support for WAV files bigger than 4 GB which only
  contain a single DATA chunk and a wrong length field for this DATA chunk (e.g. eac3to
  creates such files).
* mkvmerge: all: On Unix/Linux rpath linker flags are added for library paths given in
  LDFLAGS and configure's "--with-extra-libs" options.
* mkvmerge: new feature: Added support for skipping ID3 tags in AAC and AC-3 files. Fix
  for bug 204.
* mkvmerge: new feature: Added support for DTS-HD (both "master audio" and "high
  resolution").

## Bug fixes

* mkvmerge: bug fix: improved the time code calculation for MP3 tracks read from MP4
  files. Another part of the fix for bug 165.
* mkvmerge: bug fix: mkvmerge honors the time code offsets of all streams in a MPEG
  program stream (e.g. VOB file) fixing audio/video desynchronization. Fix for bug
  295.
* mkvmerge: bug fix: DTS-in-WAV handling (14 to 16 bit expansion) was flawed. Fix for
  bug 288.
* mkvmerge: bug fix: The fix to the time code handling for AVC tracks in MP4 files from
  2008-04-16 caused certain other MP4 files to not be read correctly. The video tracks
  were found, but no frames were read. Fix for bug 294.
* mkvmerge, mmg: The option "--delay" was removed. The option "--sync" now only
  modifies the time codes of a given track. mkvmerge does not pad audio tracks with
  silence. "--sync" works with all track types now, but using a stretch factor other
  than 1 with audio tracks might not work too well during playback. mmg's inputs for
  "Delay" and "Stretch by" can be used with all track types. Fix for bug 287.
* mkvmerge: bug fix: The VobSub reader would sometimes read too many bytes for a single
  SPU packet. Part of a fix for bug 245.
* mkvmerge: bug fix: Using BZIP2 compression resulted in broken streams. Patch by
  Aurelien Jacobs (see AUTHORS).
* mkvmerge: bug fix: Certain Matroska files with dis-continuous streams (e.g.
  subtitles) caused huge memory consumption. Fix for bug 281.
* mkvmerge: bug fix: mkvmerge will output a proper error message if it is called with
  ASF/WMV files instead of detecting other kinds of streams (e.g. AVC ES streams). Fix
  for bug 280.
* mkvmerge: bug fix: Fixed an assertion in the OGM reader occuring for OGM files with
  embedded chapters. Fix for bug 279.
* mkvmerge: bug fix: Fixed wrong time codes for MP4 files that contain video tracks
  with B frames and edit lists. Fix for bug 277. Patch by Damiano Galassi (see AUTHORS).
* mkvmerge: bug fix: mkvmerge will not strip leading spaces in SRT subtitles anymore.
* mkvmerge: bug fix: Tuned the file type detection for MPEG ES streams. Fix for bug 265.
* mkvmerge: bug fix: Fixed writing to UNC paths on Windows. Fix for bug 275.

## Other changes

* mkvmerge: Switched from the PCRE regular expression library to Boost's RegEx
  library.


# Version 2.2.0 "Turn It On Again" 2008-03-04

## New features and enhancements

* mkvmerge: new feature: Added support for handling AC-3 in WAV in ACM mode.
* mkvmerge: new feature: Added support for reading AC-3 from QuickTime/MP4 files.
  Fix for bug 254.
* mkvmerge: new feature: Added support for handling AC-3 in WAV in IEC 61937
  compatible streams (aka SPDIF mode).
* mkvmerge: new feature: Added support for WAV files with multiple data chunks.
* mkvmerge: new feature: Added support for AAC-in-AVI with CodecID 0x706d as created
  by mencoder. Fix for bug 266.
* mkvmerge: enhancement: SRT files that contain coordinates in the time code line are
  supported. The coordinates are discarded automatically (as S_TEXT/SRT doesn't
  support them), and a warning is shown.

## Bug fixes

* mkvmerge: bug fix: Fixed a cause for the error message "no data chunk found" by fixing
  the skipping of 'fmt ' chunks.
* mkvmerge: bug fix: Rewrote the OGM reader code. Another part of a fix for bug 267.
* mkvmerge: bug fix: Rewrote the time code application code. Additionally force the
  "previous cluster time codes" that libmatroska uses to the current time code. This
  seems to get rid of libmatroska's assertions about the local time code being to
  small/big to fit into an int16. It also seems to get rid of some of mkvmerge's errors
  about the packet queue not being empty, and it fixes a couple of crashes with file
  splitting.
* mkvmerge: bug fix: OGM files with non-Theora video tracks caused mkvmerge to fail
  since 2.1.0, or the resulting file was unplayable. Fix for bug 267.
* mkvmerge: bug fix: Accept other Theora header versions than 3.2.0 as long as the
  major version is 3 and the minor 2. Fix for bug 262.
* mkvmerge: bug fix: MPEG PS reader: Fixed the resyncing mechanism during normal
  reads. Another fix for bug 259.
* mkvmerge: bug fix: MPEG PS reader: mkvmerge tries to resync to the next MPEG start
  code in case of error during stream detection. Fix for bug 259.
* mkvmerge: SVQ1 video tracks read from QuickTime files are output as
  V_MS/VFW/FOURCC tracks and not as V_QUICKTIME tracks. Fix for bug 257.
* avilib: bug fix: Fixed a segmentation fault if reading the first part of an index
  failed but a second/other index part is present. Fix for bug 256.


# Version 2.1.0 "Another Place To Fall" 2007-08-19

## New features and enhancements

* mkvmerge: enhancement: Added support for reading MP2 audio tracks from OGM files.
  Patch by Mihail Zenkov (see AUTHORS).
* mkvextract: enhancement: Added support for extracting Dolby Digital Plus
  (E-AC-3) tracks.
* mmg: enhancement: Added another option how mmg choses the directory if automatic
  output filename creation is on. Implements all suggestions as listed in bug 248.
* mmg: enhancement: Moved the complete 'settings' tab to its own dialog accessible
  via the 'Settings' option in the 'File' menu.
* mmg: new feature: Added a buton 'remove all' which removes all input files and tracks
  leaving all other options as they are. Fix for bug 248.
* mmg: new feature: Added an option for setting the default output directory if the
  automatic setting of the output file name is turned on. Fix for bug 248.
* mkvmerge: enhancement: DTS code: Some tools (e.g. Surcode) can create DTS files
  which are padded with zero bytes after each DTS frame. These zero bytes are now
  skipped without printing a warning.
* mmg: enhancement: mmg can now be called with any file name as an argument. If it ends
  with 'mmg' then the file will be loaded as a 'mmg settings file'. Otherwise mmg will
  'add' it. Fix for bug 243.
* mkvmerge: enhancement: The OGM reader now uses the AVC/h.264 video packetizer for
  AVC/h.264 tracks so that the aspect ratio can be extracted from it.
* mkvmerge: new feature: Added better checks if two tracks can be appended to the
  passthrough packetizer so that tracks that are otherwise not known to mkvmerge can
  still be appended (e.g. V_VC1). Fix for bug 244.
* mkvextract: new feature: Added support for the 'header removal' encoding scheme.
* mkvmerge: new feature: The NALU size length of an AVC/h.264 track can now be changed
  even if the source is not an elementary stream (e.g. for MP4 and Matroska files).
* mkvmerge: enhancement: Added support for RealAudio v3 in RealMedia files. Patch by
  Aurelian Jacobs. Fix for bug 246.
* mkvmerge: enhancement: The SRT reader allows "." as the decimal separator as well as
  ",".
* mkvmerge: enhancement: Implemented a major speed-up for reading MPEG-1/2 and
  AVC/h.264 tracks from MPEG program streams.
* mkvmerge: new feature: Added support for handling AVC/h.264 tracks in MPEG program
  streams.
* mkvmerge: new feature: Added support for E-AC-3 tracks in MPEG program streams.
* mkvmerge: new feature: Added support for E-AC-3/DD+ (Dolby Digital Plus) files and
  tracks (raw E-AC-3 files or inside Matroska with CodecID A_EAC-3).

## Bug fixes

* mkvmerge: bug fix: SPS and PPS NALUs are no longer removed from AVC/h.264 streams.
  Hopefully a fix for bug 231.
* mkvmerge: enhancement: Fixed SSA/ASS detection for files produced by Aegis Sub
  which doesn't include a line with '[script info]' in the file.
* mkvmerge: bug fix: The OGM reader uses the OGM's timestamps for video tracks. Before
  it would just use the current frame number multiplied by the FPS.
* mkvmerge: bug fixes: Fixed a couple of memory leaks.
* mkvmerge: bug fix: The 'default track' flag was set to 'yes' for tracks read from
  Matroska files even if 'no' was specified on the command line.
* mkvmerge: bug fix: Another bug fix for handling various AC-3 and E-AC-3 files in MPEG
  program streams.
* mkvmerge: bug fix: Added support for handling SEI NALUs in AVC/h.264 elementary
  streams so that "key frames" can be detected even if no IDR slices are present.
* mkvmerge: bug fix: Fixed the VobSub reader so that "delay:" lines with negative time
  codes are accepted. Fix for bug 241.
* mkvmerge: bug fix: Improved the file type detection code for MPEG transport
  streams.
* mkvmerge: bug fix: Fixed a problem reading normal AC-3 tracks from MPEG program
  streams.
* mkvmerge: bug fix: Fixed an issue with negative/huge time codes after splitting
  AVC/h.264 video.
* mkvmerge: bug fix: Fixed a problem with concatenating more than two subtitle files.
* mkvmerge: enhancement: Fixed the MPEG PS reader so that it will just skip blocks
  whose headers it cannot parse instead of aborting.

## Other changes

* mmg: The NALU size length can now be chosen for all AVC tracks, not only for those that
  are handled by the 'AVC ES packetizer'.
* mmg: Moved the command line to a separate dialog and reduced the main window's
  height.
* mkvmerge: The MPEG program stream reader will now sort the tracks it finds first by
  their type (video > audio > subs) and then by their stream ID.
* mkvmerge: Disabled the support for DTS tracks in MPEG program streams because DTS HD
  is not supported yet.


# Version 2.0.2 "You're My Flame" 2007-02-21

## New features and enhancements

* mkvmerge: enhancement: Added support for DTS files which use only 14 out of every 16
  bits and which are not stored inside a WAV file.
* mkvmerge, mmg: new feature: Extended the option "--default-track" so that it can be
  forced to "off" allowing the user to create a file for which no track has its "default"
  flag set. Fix for bug #224.
* mkvmerge, mkvextract, mkvinfo: new feature: Added support for using CodecState
  for signaling changes to CodecPrivate. It is used for MPEG-1/-2 video if it is turned
  on with "--engage use_codec_state".

## Bug fixes

* mkvmerge: bug fix: Fixed suppoert for DTS-in-WAV files which are encoded with 14
  bits per word.
* mkvmerge: bug fix: File type detection for Qt/MP4 files which start with a "wide"
  atom has been fixed.
* mmg: bug fix: The "NALU size length" drop down box is now also enabled for h.264 tracks
  read from AVIs and for h.264 tracks stored in "VfW compatibility mode" in Matroska
  files.
* mkvmerge: bug fix: Fixed the wrong "default duration" if the user used
  "--default-duration ...23.976fps".
* mkvmerge: bug fix: The AVC/h.264 ES reader was losing frames if the file size was an
  exact multiple of 1048576 bytes.
* mkvmerge: bug fix: The AVC/h.264 ES packetizer produced invalid CodecPrivate data
  if the AVCC did not contain the aspect ratio information. Fix for Bugzilla bug #225.
* mkvmerge: bug fix: The Matroska reader passes the correct track number down to the
  AVC/h.264 ES packetizer in the case of "AVC in Matroska stored in VfW mode".
* mkvmerge: bug fix: Fixed a crash (segmentation fault) in the AVC/h.264 ES handling
  code.

## Other changes

* mkvmerge: Reintroduced the "--engage allow_avc_in_vfw_mode" hack.
* mkvmerge, mmg: Changed the default for the "NALU size length" to "4" and added a
  warning if "3" is used.


# Version 2.0.0 "After The Rain Has Fallen" 2007-01-13

## New features and enhancements

* mmg: new feature: Added another tab for each track in which the user can add arbitrary
  track options.
* mkvextract: enhancement: mkvextract will now also print which container format it
  uses for each track.
* mkvextract: new feature: Added support for extracting MPEG-1/2 video to MPEG-1/2
  program streams.
* mkvmerge: enhancement: mkvmerge now handles the first frames in AVC/h.264 ES
  streams properly, especially for files for which it did not find a key frame at the
  beginning in earlier versions.
* mkvmerge: enhancement: Improved the detection of AVC/h.264 ES streams with
  garbage at the beginning.
* mmg: enhancements to the job management dialog: There's a minimum width for the
  columns. The "up" and "down" buttons are disabled if all entries are selected.
  Pressing "Ctrl-A" selects all entries.
* mmg: enhancements: "File -> New" will also focus the "input" tab.
* mmg: enhancements: The job manager can be opened with "Ctrl-J". The last directory
  from which a file is added is saved even if the file identification failed. The
  automatically generated output file name uses the extension ".mka" if no video
  track is found and ".mks" if neither a video nor an audio track is found in the first
  file.
* mmg: enhancement: Added an input for the new "NALU size length" parameter.
* mkvmerge: enhancement: Added "x264" to the list of recognized FourCCs for
  AVC/h.264 video in AVI and Matroska files.
* mkvmerge: new feature: Added support for proper muxing of AVC/h.264 tracks in
  Matroska files that were stored in the MS compatibility mode (CodecID
  V_MS/VFW/FOURCC instead of V_MPEG4/ISO/AVC).
* mkvmerge: new feature: Added support for proper muxing of AVC/h.264 tracks in AVI
  files.
* mkvmerge: new feature: Added support for reading AVC/h.264 elementary streams.
* mmg: enhancement: All inputs and controls are cleared and deactivated if the user
  select "File -> New".
* mmg: enhancement: The user can switch between the "generic" and "format specific
  options" pages even if no track is selected.

## Bug fixes

* mkvmerge: bug fix: Fixed the file type detection for MPEG-1/2 ES files with a single
  frame inside.
* mkvmerge: bug fix: MPEG-1/2 video: The sequence and GOP headers are not removed from
  the bitstream anymore. This should fix the blockiness if the sequence headers
  change mid-stream. Fix for Bugzilla bug #167.
* mkvmerge: bug fix: Fixed the aspect ratio extraction for raw AVC/h.264 ES tracks.
* mkvmerge: bug fix: If a raw AVC/h.264 ES file does not start with a key frame then all
  the frames before the first key frame are skipped, and mkvmerge does not abort
  anymore.
* mkvmerge: bug fix: AVC/h.264 ES parser: Fixed wrong NALU size length information in
  the AVCC.
* mkvmerge: bug fix: AVC/h.264 ES parser: Fixed the decision if a NALU belongs to a
  previous frame or starts a new one.
* mkvmerge: bug fix: The NALU size length can be overridden for AVC/h.264 elementary
  streams. It defaults to 2 which might not be enough for larger frames/slices.
* mkvmerge: bug fix: Support for AVC/h.264 elementary streams with short markers
  (0x00 0x00 0x01 instead of 0x00 0x00 0x00 0x01).
* mkvmerge: bug fix: Fixed invalid memory access in the AVC ES parser.
* mkvmerge: bug fix: mkvmerge would not write frame durations if "--engage
  use_simpleblock" was used resulting in unplayable and unextractable subtitle
  tracks.
* mkvmerge: bug fix: Added a workaround for RealAudio tracks for which the key frame
  flag is never set.
* mmg: bug fix: Fixed a segfault that occured if the user had a track selected and its the
  file the track was read from is removed.
* mmg: bug fix: Fixed the behaviour of a couple of ComboBoxes on Windows after
  selecting "File -> New". E.g. if the user selected "700M" in the "split after this
  size" ComboBox, selected "File -> New" and selected "700M" again, then it would not
  show up in the command line window until he selected another option and returned to
  the "700M" afterwards.

## Other changes

* mkvmerge: Removed the "--engage allow_avc_in_vfw_mode" hack.


# Version 1.8.1 "Little By Little" 2006-11-25

## New features and enhancements

* configure: new feature: Allow the user to tell configure which "wx-config"
  executable to use ("--with-wx-config=...").
* mkvmerge/mmg: new feature: If ATDS AAC tracks are added to mmg and the AAC track's
  sample rate is <= 24000 Hz then mkvmerge and mmg assume that the AAC is a SBR track and
  mmg will check the "AAC is SBR" checkbox automatically.
* mmg: new feature: Made the "set the delay input field from the file name" feature
  disengageable.

## Bug fixes

* mmg: bug fix: Some input controls (like "subtitle charset") where disabled for
  appended tracks even though the user can and sometimes has to change those settings.
  Fixes Anthill bug 216.
* mmg: bug fix: The "aspect ratio" box was losing its input when the user switched
  tracks.
* mkvmerge: bug fix: Quicktime/MP4 files with AVC video tracks and missing CTTS atoms
  caused mkvmerge to crash after the recent changes to the Quicktime/MP4 time code
  handling.
* mkvmerge: bug fix: Fixed a segfault if the file specified with "--attach-file" does
  not exist. Bugfix for Anthill bug 213 and Debian bug 393984.
* mmg: bug fix: Fixed a crash on loading XML chapters after having saved XML chapters.


# Version 1.8.0 "Wise Up" 2006-11-10

## New features and enhancements

* mkvmerge: new feature: Added support for the "stereo mode" flag for video tracks.
* mkvmerge: Added support for API changes in the upcoming FLAC library v1.1.3. Patch
  by Josh Coalson (see AUTHORS).
* mmg: new feature: Added an option for always using simple blocks.
* mmg: new feature: Pre-set the "delay" input field for audio tracks if the file name
  contains something like "DELAY XX" where XX is a number.

## Bug fixes

* mkvmerge: bug fix: For MP4 files with certain CTTS contents mkvmerge would use
  negative time codes for a couple of frames. Those frames were dropped and mkvmerge
  often ended up eating huge amounts of memory and crashing afterwards.
* mkvmerge: bug fix: AAC-in-MP4 with the LC profile was sometimes misdetected as
  having a SBR extension and an output sampling frequency of 96000 Hz. Fixes Anthill
  bug 210.
* mkvmerge: bug fix: Fixed the random number generation on Windows. On Windows 9x/ME
  mkvmerge would simply hang. On newer versions the function was accessing invalid
  memory and was generally buggy.
* mkvmerge: bug fix: SSA/ASS subtitles with comments before the "[script info]" line
  were not identified.
* mkvmerge: bug fix: Made the checks for SRT time codes a bit less strict (e.g. allow
  fewer than three digits after the comma).
* mkvmerge: bug fix: Comments in OGM files were not handled if mkvmerge was called in
  identification mode. One obvious result was that neither the track language nor the
  file title was imported into mmg.
* mmg: bug fix: The "stretch" input box was not accepting the same syntax that
  mkvmerge's "--sync" parameter accepts.
* mkvmerge: bug fix: PCM audio data with 4 bits per Sample caused mkvmerge to allocate
  all available memory. Patch by Robert Millan (see AUTHORS).
* mmg: bug fix: Mixed up two tool tips on the "settings" tab.
* Build system: bug fix: Moved some @...@ style variables from configure.in to
  Makefile.in where they belong (very recent autoconf versions were choking on
  those).
* mkvmerge: bug fix: mkvmerge will no longer create empty files (meaning neither
  input files nor things like chapters etc have been added).

## Other changes

* mkvmerge: Changed the CodecID for AAC audio tracks to "A_AAC" by default. The
  CodecPrivate contains the same initialization data that are stored in the ESDS in
  MP4 files for AAC tracks. The old CodecIDs (e.g. "A_AAC/MPEG4/SBR") can be turned on
  again with "--engage old_aac_codecid".
* mmg: Reworked the "input tab" and split track options into two sub-pages. Also added
  an input for the "stereo mode" parameter for video tracks.
* mmg: enchancement: After adding files with drag&drop the next "open file" dialog
  will start in the directory the last file came from.


# Version 1.7.0 "What Do You Take Me For" 2006-04-28

## New features and enhancements

* mkvmerge: enhancement: Added support for MIME type detection via libmagic (patch
  by Robert Millan with heavy modifications by myself).
* mmg: enhancement: The 'adjust time codes' function accepts time codes like
  'XXXXXunit' with 'unit' being 'ms', 'us', 'ns' or 's'.
* mkvmerge: enhancement: mkvmerge will no longer refuse to concatenate files with
  differing Codec Private contents and only issue a warning in such cases.
* mkvmerge: new feature: Added support for the "Delay:" feature and for negative time
  codes in VobSub IDX files.
* mmg: new feature: If mmg is set to automatically fill in the output file name then it
  will clear the output file name once all input files have been removed.

## Bug fixes

* mkvmerge: bug fix: Theora headers were not handled correctly.
* mkvmerge: bug fix: The WavPack reader was broken on 64bit systems (e.g. AMD64).
* mkvmerge: bug fix: The Theora time code handling was broken, and Ogg/Theora files
  were not identified correctly (they showed up as "unknown" in mmg).
* mkvmerge: bug fix: Quicktime/MP4 reader: Added support for version 1 media headers
  ('mdhd' atom) with 64bit fields. Fixed the duration of the last packet passed
  downstream. Fixed overflow issues during re-scaling from the Quicktime/MP4's
  time scale to nano seconds used by mkvmerge.
* mkvmerge: bug fix: Muxing wasn't working Windows 9x/ME because mkvmerge was trying
  to use Unicode file access functions when determining which directories to create.
  Fixes Anthill bug #177.
* mmg: bug fix: Fixed a crash that occured if the user removed an attachment and clicked
  somewhere in the empty space in the attachment list. Occured only on Windows.
* mmg: bug fix: Re-added Chinese to the list of popular languages (those are listed
  first in the language drop down boxes).
* mkvmerge: bug fix: The last change to the ISO 639 language handling broke the VobSub
  reader so that it reported the wrong language codes. This also caused mmg to not
  display the correct language after adding a VobSub file.


# Version 1.6.5 "Watcher of the Skies" 2005-12-07

## New features and enhancements

* source: new feature: Added support for linking against liblzo2 (same compression
  algorithm, just a new library version). Patch by Diego Pettenò (see AUTHORS).
* mkvextract: new feature: attachment extraction mode: Made the output file name
  optional. If it is missing (e.g. "mkvextract attachments source.mkv 92385:
  124981:") then the name of the attachment inside the Matroska file is chosen
  instead. Patch by Sergey Hakobyan (see AUTHORS).
* mkvmerge: new feature: If an output file name contains directories that don't exist
  then they're created. Patch by Sergey Hakobyan (see AUTHORS) with modifications by
  myself.
* mkvmerge, mmg: new feature: The names of attached files can be set with a new option
  --attachment-name or on mmg's "Attachments" page.
* mkvmerge: new feature: Added support for Ogg/Theora.
* mkvinfo: new feature: The sub elements of the EBML head are now shown.
* mkvinfo: new feature: Added support for the new SimpleBlock.
* mkvextract: new feature: Added support for the new SimpleBlock.

## Bug fixes

* source: bug fix: Changed the list of ISO 639 languages so that the terminology
  versions are converted into the bibliography versions of the 639-2 codes (e.g. use
  "ger" instead of "deu" for the German language). Converted almost all pieces of
  mkvmerge and mmg to accept ISO 639-1, 639-2 codes (both bibliography and
  terminology versions) and the languages' English names. Those will always be
  converted to the 639-2 code. Fixes Anthill bug #171.
* mkvmerge: bug fix: The country code used in XML chapter files was checked against the
  list of ISO 639-1 codes and not against the list of ccTLDs. Partial bugfix for Anthill
  bug #171.
* mkvmerge: bug fix: When appending tracks and using time codes the time codes were
  only used for the first track in a chain of tracks. This has been changed so that you
  must specify only one time code file in such cases (e.g. "mkvmerge ... --time codes
  0:my_time codes.txt part1.avi +part2.avi"). mmg has already been working like
  this. Fixes Anthill bug #162.
* mkvmerge: new feature: Added a workaround for files created by Gabest's DirectShow
  Matroska muxer with slightly broken frame references. Fixes Anthill bug #172.
* mkvmerge: bug fix: Don't abort reading a Matroska if the next element is not a
  cluster. This is the case for e.g. files produced by Haali's muxer which writes the
  segment tracks element in intervals. Fixes Anthill bug #169.
* mmg: bug fix: Fixed a problem with the selection of language codes for chapters in the
  chapter editor.
* mkvmerge: bug fix: If at least or more attachments were present and the user used
  --attachment-name for each of them (as mmg does) then mkvmerge was wrongly
  outputting a warning about multiple uses of --attachment-name for a single
  attachment.
* mkvmerge: new feature: Added limited support for edit lists in MP4/QuickTime
  files. Fixes Anthill bug #151.
* mkvmerge: bug fix: MP4/QuickTime files which contain another atom before the
  'avcC' atom in the video track headers weren't correctly remuxed.
* mkvmerge: bug fix: mkvmerge will now refuse to append AVC/h.264 video tracks whose
  codec initialization data blocks do not match. Invalidates Anthill bug #163.
* mkvmerge: bug fix: Fixed a crash If the granulepos (the time codes) reset in the
  middle of an Ogg/OGM file. Fixes Anthill bug #166.
* mkvmerge: bug fix: Fixed a division-by-zero error in the RealMedia demuxer. Fixes
  Anthill bug #161.
* mkvmerge: bug fix: Fixed a couple of potential (and actual) segmentation faults by
  accessing invalid memory addresses. Initial patch for the VobSub reader by Issa on
  Doom9's forum.
* mkvmerge: bug fix: Fixed another bug when appending AVC/h.264 tracks that would
  mkvmerge cause to die with "bref_packet == NULL". Fixes Anthill bug #160.
* mmg: bug fix: When the user saved the muxing output in a log file that file didn't use
  Windows line endings (CR LF) on Windows.
* mmg: bug fix: Appending tracks was broken because the track numbers in the command
  line were incorrect. Fixes Anthill bug #160.
* mkvmerge, mmg: new feature: Added support for the new SimpleBlock instead of
  BlockGroups (only available via "--engage use_simpleblock" for now). Patch by
  Steve Lhomme (see AUTHORS) with fixes by myself.

## Other changes

* mkvmerge: Changed the CodecID for AAC audio tracks to "A_AAC" by default The
  CodecPrivate contains the same initialization data that are stored in the ESDS in
  MP4 files for AAC tracks. The old CodecIDs (e.g. "A_AAC/MPEG4/SBR") can be turned on
  again with "--engage old_aac_codecid".


# Version 1.6.0 "Ist das so" 2005-10-14

## New features and enhancements

* mkvmerge: new feature: Implemented the new header removal compression:
  compression for native MPEG-4 part 2, decompression for all types (don't use it yet,
  folks!).

## Bug fixes

* mkvextract: bug fix: Extra codec data wasn't written to AVI files if it was present
  (e.g. for the HuffYuv codec). Fixes bug 157.
* mkvmerge: bug fix: mkvmerge was choking on AVIs with a single frame inside. Fixes bug
  156.
* mkvmerge: bug fix: Changed how AVC video frames are referenced. This is not ideal
  yet, but at least references are kept intact when reading AVC from Matroska files.
  Should fix bug 154.
* mkvmerge: bug fix: Appending AVC video tracks was broken if they contained aspect
  ratio information that the user overwrote on the command line.
* mmg: bug fix: If a video track was selected that was appended to another track then the
  aspect ratio drop down box was still active.
* mkvmerge: new feature: MPEG-4 part 2 streams are searched for the pixel
  width/height values. Those are taken if they differ from those values in the source
  container. This is a work-around for buggy muxers, e.g. broken video camera
  firmwares writing bad MP4 files. Fixes bug 149.
* mkvmerge: bug fix: Appending files with RealVideo was broken.
* mkvmerge, mkvextract: bug fix: ASS files sometimes use a column called 'Actor'
  instead of 'Name', but both should be mapped to the 'name' column in a Matroska file.


# Version 1.5.6 "Breathe Me" 2005-09-07

## New features and enhancements

* mkvmerge: new feature: mkvmerge will remove the aspect ratio information from a
  AVC/h.264 video track bitstream and put it into the display dimensions (until now
  the AR information was kept on the bitstream level). The reason is that in Matroska
  the container AR is supposed to take precedence over bitstream AR, but some decoder
  programmers ignore the container AR in favour of bitstream AR.

## Bug fixes

* mmg: bug fix: If the user selected an aspect ratio for a video track, then chose "File
  -> new", added a file, selected another video track and chose the same aspect ratio as
  before then it wasn't added to the command line. Fixes Anthill bugs 132 and 146.
* mkvmerge: bug fix: Support Qt/MP4 files with 64bit offset tables ('co64' atom
  instead of 'stco' atom).
* mkvinfo: bug fix: The GUI couldn't open files with non-ASCII chars in the file name.
* mkvmerge: bug fix: Display dimensions were reported for all tracks, even if they
  weren't present. In that case they allegedly were "0x0" which caused mmg to add
  "--display-dimensions ...:0x0" for each track read from a Matroska file, even if
  the tracks were not video tracks.
* mkvextract: bug fix: The extracted time codes were wrong for blocks with laced
  frames.
* mkvmerge: bug fix: If a Matroska file with a MPEG-4 part 2 video track was muxed into a
  Matroska file and the source file did not contain the display width/height elements
  for that track then the aspect ratio was extracted from the video data itself which
  clashes with the Matroska specs which say that display width/height default to the
  pixel width/height if they're not present.
* mkvmerge: bug fix: Native MPEG-4 ASP storage was still bugged: time codes were
  assigned twice, frames referenced themselves.
* mkvmerge: bug fix: Embedded fonts and pictures in a SSA/ASS file are not discarded
  any longer. They are converted to Matroska attachments instead. Other sections
  that were discarded are added to the CodecPrivate data as are "Comment:" lines in the
  "[Events]" section. Those comment lines still lose their association for which
  "Dialogue:" line they were meant, but that cannot be changed.
* mkvmerge: bug fix: --delay was not working at all.
* mkvmerge: bug fix: Single digit numbers followed by 's' were not recognized as valid
  numbers with a unit (e.g. in '--delay 0:9s').


# Version 1.5.5 "Another White Dash" 2005-08-21

## New features and enhancements

* mkvextract: new feature: Added a new extraction mode for outputting time codes in a
  time code v2 format file. It is called "time code_v2" and takes the same arguments as
  the "tracks" extraction mode.
* mkvinfo: new feature: Added a command line switch "--output-charset" which sets
  the charset that strings read from Matroska files are output in (e.g. if you want the
  output in UTF-8 and not your system's local charset).
* mkvinfo: new feature: Added a command line switch "-o" for redirecting the output to
  a file (for systems which re-interpret stdout).
* mkvextract: new feature: Added support for extracting h.264 / AVC tracks into
  proper h.264 ES streams supported by e.g. MP4Box. Patch by Matt Rice (see AUTHORS).

## Bug fixes

* mkvtoolnix: bug fix: On Windows the command line output was terminated with CR CR NL
  instead of just CR NL.
* mkvmerge: bug fix: The Quicktime/MP4 reader wasn't skipping unknown elements
  correctly.
* mkvmerge: bug fix: The combination of using external time code files and video
  tracks with B frames was not working as intended. The user had to order the time codes
  in the time code file just like the frames were ordered (meaning the time codes for a
  IPBBP sequence with 25 FPS had to be "0", "120", "40, "80"...). This has been fixed.
  They have to be ascending again and mkvmerge will assign them properly.
* mkvinfo: bug fix: Files with non-ASCII chars weren't opened because conversion to
  UTF-8 was done before the charset routines were initialized.
* mkvmerge: bug fix: Fixed a crash if a track in a MP4/QuickTime file did not contain a
  STCO atom (chunk table) but a STSC atom (chunk map table).
* mkvmerge: bug fix: Very large values were not kept correctly for a lot of elements
  (meaning they were truncated to 16 or 32 bits).
* mkvinfo: bug fix: Very large values were not displayed correctly for a lot of
  elements (meaning they were truncated to 16 or 32 bits prior to displaying).
* mkvmerge: bug fix: AVC/H.264 references were wrong, and muxing of AVC from Matroska
  files with proper references resulted in unplayable files.
* mkvmerge: bug fix: Fixed support for USF subtitles stored in UTF-16 and UTF-32.
  Added support for USF subtitles stored in UTF-8 without a BOM.

## Other changes

* mkvtoolnix: Disabled storing AVC/h.264 video tracks in VfW mode.


# Version 1.5.0 "It's alright, Baby" 2005-07-01

## New features and enhancements

* mmg: new feature: Added an input box for mkvmerge's new "split after these time
  codes" feature.
* mkvmerge: new feature: Added splitting after specific time codes.
* mkvextract: new feature: Implemented the extraction of USF subtitles.
* mkvmerge: new feature: Implemented the muxing of USF subtitles.
* mkvmerge: new feature: Added support for the ChapterSegmentUID element.

## Bug fixes

* mkvmerge: bug fix: The track language read from old Matroska files was wrongfully
  set to "und" if it was not written although the specs say that "eng" is the default
  value.
* mkvmerge: bug fix: USF subtitles: If identical tags were nested (e.g. "font") and
  both were closed right after each other then the result looked like "</font/>".
* mkvmerge: bug fix: Native MPEG-4 was not working if read from OGM files.
* mkvmerge: bug fixes: Improved the native MPEG-4 generation a lot (thanks to Haali
  for testing and pushing me). The codec version string inside the MPEG-4
  initialization data is now checked if it indicates "DivX packed bitstream" and
  changed to not indicate it anymore.
* mmg: bug fix: If mmg was minimized when it was closed (e.g. with Windows' "Show
  desktop" function) then it didn't show up after a restart and could only be shown by
  maximizing it.
* mkvmerge: bug fix: If a OGM style chapter file contains empty chapter names
  ('CHAPTER01NAME=' without something after the '=') then this chapter's time code
  is used as the name instead of aborting.
* mkvmerge, mkvinfo, mkvextract: bug fix: Inifite sized segments were not handled
  correctly.
* mmg: bug fix: On Windows mmg could be crashed by adding a file and clicking into the
  empty space in the "track" selection box. Fixes Anthill bug 133.
* mkvextract: bug fix: The MPEG packets are now padded to 2048 byte boundaries as some
  programs require them to be. Patch by Mike Matsnev (see AUTHORS).
* mkvinfo: bug fix: Removed the restriction of max. ten levels of nested elements.
* mmg: bug fix: If splitting was enabled and "splitting by time" selected and the user
  chose "new" from the "File" menu then "splitting by time" was not selectable
  anymore. This happened only on Windows. Fixes Anthill bug 131.
* mkvextract: bug fix: Use the native newline style when extracting text subtitles
  (\r\n on Windows and \n on all other systems).
* mkvextract: bug fix: SSA/ASS text was missing in the output if the "Format=" line
  contained newlines at the end of the CodecPrivate data (e.g. our old Mew Mew sample
  file).
* mkvmerge: bug fix: Support WAV files that use other RIFF chunks than the usual "fmt "
  followed by "data".
* mkvmerge: bug fix: Remuxing MPEG1/2 tracks resulted in a failing "assert(0)".


# Version 1.4.2 "Jimi Thing" 2005-04-16

## New features and enhancements

* mkvextract: new feature: Added the extraction of the raw data with the "--raw" and
  "--fullraw" flags. Patch by Steve Lhomme (see AUTHORS).

## Bug fixes

* mkvmerge: bug fix: In rare occasions involving B frames mkvmerge freed data too
  early. In such a case it was eating more and more memory finally exiting with a message
  about not finding a packet for a "bref".
* all: bug fix: My output functions did not work on AMD64 systems. Fixes Anthill bug
  120.
* mkvextract: bug fix: WAVPACK extraction did not update the "number of samples"
  header field. Patch by Steve Lhomme (see AUTHORS).
* mkvmerge: bug fix: RealMedia files contain a "FPS" field in their track headers.
  Unfortunately this field does not always contain the actual FPS of a video track but
  the maximum number of FPS that the encoder has output or should output. Therefore
  mkvmerge does not use a "default duration" element for RealVideo tracks anymore.
  Fixes Anthill bug 113.
* mkvmerge: bug fix: Failing calls to posix_fadvise upon adding a file to mmg caused
  mmg to think that the file identification failed. Now warnings for posix_fadvise
  are not output anymore, and posix_fadvise is silently switched off for that file.
  Fixes Anthill bug 123.
* mkvmerge: bug fix: Appending files that were created with mkvmerge's "--link"
  option was broken. The time codes for both the chapters and the actual media data
  blocks were not adjusted correctly. Fixes Anthill bugs 115 and 116.
* mkvmerge: bug fix: If chapters are present in several appended files and there were
  atoms who shared the same UID then those entries were present multiple times in the
  output files. Now such entries are merged into one chapter entry. Fixes the second
  part of Anthill bug 122.
* mkvmerge: bug fix: If chapters were present and splitting was enabled then mkvmerge
  would not treat chapters correctly that spanned across several files. Now the
  spanning chapters are kept in all files, and their start time codes are adjusted
  accordingly. Fixes the first part of Anthill bug 122.
* mkvinfo: bug fix: On Windows mkvinfo was linked without the console subsystem
  resulting in no output at all if run without the GUI (-g). Fixes Anthill Bug 118.
* mkvmerge: bug fix: Due to the compiler doing some strange number conversion
  mkvextract seemed to hang on Windows with certain files.
* mkvmerge: bug fix: Appending VobSubs with more than one track in a .idx file and video
  files at the same time was broken resulting in parts of some of the VobSub tracks not
  ending up in the resulting Matroska file. Fixes Anthill bug 114.
* mkvmerge: bug fix: The track numbers were assigned wrongly when appending tracks
  (this is more or less cosmetic).
* mkvmerge: bug fix: Splitting by time was broken for audio-only files. Fixes Anthill
  bug 112.
* mkvmerge: bug fix: The --fourcc switch was not working.
* mmg: bug fix: Tracks that were not selected on saving the settings file were selected
  after loading a settings file.


# Version 1.4.1 "Cherry Lips" 2005-03-15

## Bug fixes

* mkvmerge: bug fix: AC-3 detection was broken in rare cases.
* mmg: bug fix: If the TEMP environment variable contains spaces then the calls to
  mkvmerge when adding files failed.
* mkvmerge: bug fix: Extracting the FPS from some AVC MP4 files did not work.
* mkvmerge: bug fix: Appending + splitting was segfaulting if used together and at
  least one split occured after a track has been appended.
* mkvmerge: bug fix: A failing call to posix_fadvise will only turn its usage off for
  that one file and not abort mkvmerge completely.
* mmg: bug fix: When "appending" a file all tracks where added to the end of the track
  list making it unnecessarily difficult to concatenate similar structured files.
  Now the tracks from the "appended" files are inserted into the track list after their
  counterparts from the file this new one is appended to.
* mmg: bug fix: An "appended" file could not be removed if there were two tracks that we
  not separated by a track from another file in the track list box.
* mmg: bug fix: The check whether or not a file might be overwritten while splitting is
  active has been fixed.
* mmg: bug fix: Improved the word wrapping of the tooltips on Windows.
* mmg: bug fix: It was possible to select a file for appending even though no file was
  added first.
* mkvmerge: bug fix: mkvmerge was wrongly outputting large numbers of warnings when
  Remuxing AVC/h.264 video from a Matroska file.
* mmg: bug fix: The job queue was not loaded on startup on Windows Unicode builds
  (another wxWidgets 2.5.3 problem).
* mmg: bug fix: The job status in the job runner dialog was broken on Unicode builds on
  all systems.
* mmg: bug fix: "Splitting by time" was not selectable on Windows Unicode builds
  (problem with wxWidgets 2.5.3).
* mmg: bug fix: mkvmerge's output during muxing was not converted from UTF-8.
* mmg: bug fix: The default extension added when the user doesn't give one is different
  in wxWidgets 2.4.x and 2.5.x. It should always be .mkv and not .mka.

## Other changes

* mkvmerge: Added more descriptive error messages if two tracks cannot be
  concatenated because "their parameters do not match".


# Version 1.4.0 "Cornflake Girl" 2005-02-26

## New features and enhancements

* mmg: new feature: The "default track" checkboxes are set properly when a Matroska
  file is added.
* mmg: new feature: Added a warning right before the muxing starts if the chapter
  editor contains entries but no chapter file has been selected (can be turned off).
* mkvextract: new feature: Added VobSub extraction based on Mike Matsnev's code.

## Bug fixes

* mkvmerge: bug fix: Track names could not be set to be empty.


# Version 1.0.2 "Elephant's Foot" 2005-02-06

## Important notes

* mkvmerge: bug fix: mkvmerge did not accept XML chapter files created with older
  mkvtoolnix versions due to deprecated chapter elements. Such elements are now
  skipped.

## New features and enhancements

* mkvmerge: new feature: Use the posix_fadvise function on *nix systems. This
  results in a considerable speed up for the whole muxing process. As the function call
  seems to be buggy on at least Linux kernels 2.4.x it can be disabled completely during
  configure. It will only be used on Linux with a kernel from the 2.6.x series or newer.
* mkvmerge: new feature: Added some more possible formats for binary data in XML files
  besides Base64 encoded data: hex encoded and ASCII "encoded".
* mkvmerge: new feature: Hex values accept more formats (like optional white space
  between numbers or the "0x" prefix).
* mmg: new feature: Made the mkvmerge GUI guide available by pressing F1 or selecting
  "Help" from the "Help" menu.
* mmg: new feature: Added support for mkvmerge's new "appending tracks" feature.
* mkvmerge: new feature: Added support for reading the pixel aspect ratio from
  AVC/h264 video data.
* mkvmerge: new feature: Added AVC/h264 muxing from MP4.
* mkvmerge: new feature: Added a MPEG PS demuxer.
* mkvinfo: new feature: Added a couple new elements (silent tracks). Patch by Steve
  Lhomme (see AUTHORS).
* mkvextract: new feature: Added WAVPACK4 extraction. Patch by Steve Lhomme (see
  AUTHORS).
* mkvmerge: new feature: Added WAVPACK4 muxing. Patch by Steve Lhomme (see AUTHORS).
* mkvmerge: new feature: Added VobButton muxing. Patch by Steve Lhomme (see
  AUTHORS).

## Bug fixes

* mkvmerge: bug fix: Empty video frames in AVIs right at the beginning were breaking
  the MPEG-4 aspect ratio extraction and caused problems in other parts, too.
* mmg: bug fix: It was possible to create chapter entries with invalid or even empty
  language entries. Not only are those invalid, such XML files can also not be loaded by
  mmg.
* mmg: bug fix: Overwriting a chapter file did not erase the previous file. So if the
  previous file was bigger than the current chapters then garbage remained at the end
  of the file.
* mmg: bug fix: The "stretch" input box tooltip was wrong. The resulting command line
  was broken, too.
* mkvextract: bug fix: ASS/SSA extraction was broken in some rare cases.
* mmg: bug fix: Again the window handling. Hopefully this is better than the other
  attempts.
* mmg: bug fix: One was able to crash mmg by pressing 'ok' in the muxing dialog right
  after muxing finished, especially if the 'abort' button was hit before. This mostly
  happened on Linux.
* mkvmerge: bug fix: Fixed negative audio displacement for a couple of formats.

## Other changes

* mkvmerge: Changed the AVC/h.264 time code handling to include the time code offsets
  from the CTTS atom.
* mmg: Reformatted the HTML guide and updated the screenshots. It should be more
  readable for those whose desktop is not 1200 pixels wide.


# Version 1.0.1 "October Road" 2004-12-13

## New features and enhancements

* mmg: new feature: The window position is saved and restored when mmg is started the
  next time.
* mkvmerge: Implemented concatenating files with chapters.

## Bug fixes

* mmg: Fixed some layout issues with wxWidgets 2.5.3 and newer.
* mmg: bug fix: Fixed a crash/memory corruption showing weird characters in the input
  boxes. This happened when the user removed a file from mmg while mmg was updating the
  command line.
* mmg: bug fix: mmg now has an icon associated with it while it is running instead of the
  generic Windows application icon (Windows only).
* mmg: bug fix: The main window is now minimized during muxing. This allows to hide both
  of the windows while muxing is running and restoring them later, even if they were
  iconized when muxing finished (Windows only).
* mkvmerge: bug fix: The first packet of an AAC track read from Real containers might
  not start at the time code 0. This offset was ignored by mkvmerge.
* mmg: bug fix: Made the muxing dialog ("mkvmerge is running") modal all the time. This
  prevents the user from hitting the main window's minimize button. On Windows this
  makes mmg stuck in iconized mode if it was iconized when muxing finished.
* mkvmerge: bug fix: Fixed a buffer overflow in the UTF-8 file reading routines.

## Other changes

* mkvmerge: Changed the "progress" output. It's now correct for file concatenation,
  too.


# Version 1.0 "Soul Food To Go" 2004-11-17

## New features and enhancements

* mkvmerge: new feature: Concatenating/appending files is now possible. A lot of
  things aren't tested, and others simply don't work yet (chapter merging, duplicate
  tag elimination, proper progress report, support in mmg just to name a few), but the
  basic functionality seems to work.
* mkvmerge: new feature: Added reading DTS from AVIs and from Matroska files.
* mmg: enhancement: Made mmg's main window properly resizable.

## Bug fixes

* mkvmerge: bug fix: The Matroska reader doesn't insist on having a default duration (
  = FPS) for video tracks in the "AVI compatibility mode" ( = with the CodecID
  "V_MS/VFW/FOURCC"). This enables re-muxing of Matroska files created from MP4
  files.
* mmg: bug fix: File names with non-ASCII characters were not working if mmg was
  compiled against a Unicode enabled wxWidgets.
* mkvmerge: bug fix: A variable initialization was missing which very recent gcc
  versions (3.4.2) did not like very much. Also fixed a small compilation bug.
* mkvmerge: bug fix: The handling of external time code files was still not correct but
  should be OK now.
* mkvmerge: bug fix: If LFE is on for DTS then the number of channels is one more than what
  the DTS frame header says.
* mkvmerge: bug fix: Time Codes for Vorbis were wrong on rare occasions (when reading
  laced Vorbis from a Matroska file and changing the lacing, e.g. when splitting for
  the second and all following files).
* mkvmerge/mkvinfo/mkvextract: bug fix: The chapter and tag element tables were not
  always intialized correctly depending on the compiler and the optimization flags
  used.
* mkvmerge: bug fix: The OGM reader was broken if at least one track was not to be copied
  from the file (happened between 0.9.5 and 0.9.6).
* mmg: bug fix: After loading saved mmg settings the track input box listed the tracks
  always coming from the last input file and not from the one they really came from.
* mkvmerge: Rewrote the code for the external time code files. This also fixes bug 99:
  The durations for the individual tracks were not correct for those tracks for which
  --time codes was used.
* mmg: bug fix: Crash when saving chapters from the chapter editor. Same as the mkvinfo
  issue below but on all OS.
* mkvinfo: bug fix: The chapter and tag element tables were not initialized on Windows
  resulting in a crash when one of those elements was encountered.

## Other changes

* mmg: Added an error message if the user selects 'mmg' as the 'mkvmerge executable'
  because that would lead to an infinite number of 'mmg's being spawned.


# Version 0.9.6 "Every Little Kiss" 2004-10-07

## Important notes

* mkvmerge, mkvextract, mkvinfo: feature removed: Dropped support for the very old
  and deprecated tagging system. No one used it anyway.

## New features and enhancements

* mkvmerge: enhancement: Converted the raw FLAC reader to use another interface to
  the FLAC libraries. This results in a speedup of up to 50%. Thanks to Josh Coalson for
  telling me about its existence.
* mkvmerge: new feature: Added two warnings. One about invalid track IDs that were
  used on the command line but that don't correspond to an available track in a file and
  one if no track will be copied from a source file. Both warnings hint at bad command
  line arguments.
* mkvmerge: new feature: The CUE sheet parser now accepts INDEX lines with indices
  from 00 up to 99 and implements the Red Book specification for audio CDs that way.
  Patch by Vegard Pettersen <vegard_p at broadpark adot no>.
* mkvmerge: new feature: Added a new parameter --aspect-ratio-factor.
* mkvmerge: new feature: Added support for MP2 (and maybe MP3) audio in MP4
  containers.
* mkvmerge: new feature: The chapter and tag parsers accept XML element attributes
  instead of sub-elements for those sub-elements that only contain data. Example for
  a "simple tag": <Simple Name="ARTIST" String="Tori Amos"/>
* mkvmerge, mkvinfo: new feature: Added the four new PixelCrop elements.
* mkvmerge, mkvextract, mkvinfo: new feature: Added 'TargetTypeValue' as a
  supported tagging element.
* mkvmerge: new feature: Allow the use of two-letter ISO639-1 country codes in for the
  '--language' parameter. Those will be converted to the corresponding ISO639-2
  language code automatically.
* mkvmerge, mkvinfo, mkvextract: new feature: Added support for the 'TargetType'
  tag element (which I meant to add before the 0.9.5 release...).

## Bug fixes

* mkvextract: bug fix: The track extraction was creating the output file twice if the
  Matroska file contained a copy of the track headers. This resulted in the first
  extracted file being overwritten at the end of extraction.
* mmg: bug fix: If the file title is read from an input file, not modified by the user and
  that input file is removed again then the file title will be unset.
* mkvmerge, mkvextract: bug fix: ASS was handled like SSA which is not correct in each
  case, especially when extracting it.
* mkvextract: bug fix: The WAV writer was not endian safe.
* mkvmerge: bug fix: The charset was not set correctly on Solaris.
* mkvmerge: bug fix: mkvmerge crashed when reading Matroska files that contain an
  empty tag list.
* mkvmerge: bug fix: Some Matroska files that e.g. have had their time codes offset
  with the Matroska Stream Editor or other means may contain time codes that caused
  mkvmerge to print a warning about "time code < last_time code". A new fix implements a
  workaround and a warning message with a proper explanation for this case.
* mkvmerge: bug fix: Older Matroska files containing chapters caused mkvmerge to
  abort muxing.
* mkvmerge: bug fix: mkvmerge was only copying the last tag of a list of tags applying to
  a track from a Matroska file.
* mkvmerge: bug fix: mkvmerge will show a nice warning if the entries in a SRT have
  non-continuous timestamps. It'll also sort the entries by their start timestamp
  instead of throwing the generic "time code < previous time code" warning.
* mmg: bug fix: The 'Matroska file analysis' window that occurs when reading chapters
  from a Matroska file did not disappear if it was minimized when the process finished.
* mkvinfo: bug fix: Strings from chapters and tags were shown in UTF-8 instead of the
  local charset. This bug was introduced around 2004-08-28.
* mkvmerge: bug fix: Not all chapter elements were copied correctly from a source
  Matroska file.
* mkvmerge: bug fix: The Matroska reader was not handling very big cluster time codes
  correctly. Those can occur when the time code scale factor is very small.
* mkvmerge: bug fix: Empty clusters in Matroska files no longer make mkvmerge think
  that file has been read completely.
* mkvmerge: bug fix: The automatic MIME type detection based on the file name
  extension was using the file name extension as the MIME type.
* mkvmerge: bug fix: The MP3 handling was broken on weird and rare occasions when
  reading MP3 from a Matroska file.
* mkvmerge: bug fix: Removed a bogus warning about an attachment's MIME type having
  been given more than once.

## Other changes

* mkvmerge: Only write the segment duration as a 64bit float if there is no video track
  present. This way users won't have to update their DirectShow filter/apps for most
  files. Only audio-only files need this precision anyway.
* mkvmerge: Changed the Ogg/OGM reader to use the stream number and not its serial
  number as the track ID (meaning the track IDs will be 0, 1, 2... etc. instead of the
  random numbers oggenc uses as the serial numbers).
* mkvextract: Sped up the extraction of attachments, chapters, cuesheets and tags by
  using the seek head information and not parsing the full file each time.
* mkvmerge, mkvextract, mkvinfo: Complete rewrite of the chapter and tag parsing and
  output functions. Additions will be much easier now.


# Version 0.9.5 "The Na Na Song" 2004-08-21

## New features and enhancements

* mkvmerge, mkvinfo, mkvextract: new feature: Added support for the new
  'EditionFlagHidden', 'EditionFlagDefault' and 'EditionManaged' elements.
* mkvmerge, mkvinfo, mkvextract: Added support for the new tag elements ('tag
  language' and 'default/original language').
* mkvmerge: new feature: If there was no MIME type given for an attachment then
  mkvmerge will try to guess it based on the file's extension just like mmg.
* mkvmerge/mkvextract: new feature: Use the new EditionUID entries when convert CUE
  sheets to chapters and tracks. This is in preparation for 'multiple CDs to single
  Matroska file' conversions.
* mkvmerge: new feature: Abort muxing if the output file name is the same as the name of
  one of the input files.
* mkvmerge: new feature: Implemented sample-precision for timestamps and
  durations on audio only files.
* mkvextract: new feature: Limited support for extracting chapters as CUE sheets
  that haven't been created by using a CUE sheet with mkvmerge's "--chapters" option.

## Bug fixes

* mkvmerge: bug fix: WAV files which contained a 'PAD ' chunk before the 'data' chunk
  were not processed at all.
* mkvmerge: bug fix: Use 'setjmp' and 'longjmp' Instead of throwing a C++ exception
  during the chapter parsing stage. Otherwise libexpat will abort with a
  non-descriptive error message on Windows.
* mkvmerge: bug fix: SSA/ASS subs with the old codec ID 'S_SSA' and 'S_ASS' were
  accepted, but their codec ID was kept. It is now correctly changed to 'S_TEXT/SSA'
  and 'S_TEXT/ASS'.
* mkvmerge: Added 'EditionUID' to valid elements below '<Targets>' in XML tags.
  Fixed the creation of the 'Targets' with --global-tags and --tags.
* mmg: bug fix: The 'down' button on the 'input' tab was not working correctly in all
  cases.
* mkvinfo, mmg: bug fix: Fixed compilation with Unicode enabled versions of
  wxWidgets.
* mkvmerge: bug fix: Try to guess whether tags read from OGM files (for automatic
  language tag setting and for copying chapter information) are already in UTF-8 or
  not. If not try to convert them from the current system's charset.
* mkvmerge: bug fix: use the same UID for the EditionUID in the chapters and in the tag
  targets when parsing a CUE sheet.
* mkvextract: bug fix: The CUE extraction wrote UTF-8 characters but no UTF-8 BOM
  (byte order marker) at the beginning.
* mkvmerge: bug fix: Handle TTA files with ID3 tags correctly ( = skip the ID3 tags).
* mkvmerge: bug fix: There was an illegal free() in the OGM reader.
* mkvextract: bug fix: The subtitle track extraction used the wrong duration in
  0.9.4.
* mkvmerge: bug fix: Block durations with 0s length (e.g. entries in a SSA file) were
  not written.
* mkvmerge: bug fix: The FLAC packetizer gets the duration from the FLAC packet
  itself.
* mkvmerge: bug fix: The word 'TAG' occuring in e.g. SRT subs caused the ID3/MP3 frame
  detection to be stuck n an endless loop.

## Other changes

* mkvmerge, mkvextract, mmg: Changes to the chapter handling. EditionUIDs are
  always created. mkvextract outputs EditionUIDs and ChapterUIDs normally.
  mkvmerge tries to keep EditionUIDs and ChapterUIDs but replaces them if they aren't
  unique.


# Version 0.9.4 2004-07-25

## New features and enhancements

* mkvextract: new feature: Added support for extracting TTA tracks to TTA files.
* mkvextract: new feature: Implemented the extraction of chapter information and
  tags as a CUE sheet which is the reverse operation to using a CUE sheet with mkvmerge's
  '--chapters' parameter.
* mmg: new feature: Added support for the two flags 'hidden' and 'enabled' in the
  chapter editor.
* mkvmerge: new feature: The pregap from a CUE sheet is converted into two
  sub-chapters (one for "INDEX 00", one for "INDEX 01"). These sub-chapters have
  their 'hidden' flag set.

## Bug fixes

* mkvmerge: bug fix: SRT file recognition failed if the file contained spaces at the
  end of the first line.
* mkvmerge: bug fix: Broken VobSub .idx files which contain timestamps going
  backwards no longer crash mkvmerge. A warning will be printed for such
  inconsistencies.
* mkvmerge: bug fix: The Matroska reader contained a nice little illegal memory
  access (introduced in 0.9.3 with the fixes to the 'default track' handling).
* mkvmerge: bug fix: The SSA reader was segfaulting if a line contained an empty text
  field.
* mkvinfo: bug fix: Fixed compilation for MATROSKA_VERSION = 2.
* mkvinfo: bug fix: Fixed compilation with gcc 3.2.
* mkvmerge: bug fix: The CUE sheet parser interpreted a timestamp as HH:MM:SS (hours,
  minutes, seconds). The correct spec is HH:MM:FF (hours, minutes, frames with 1
  frame = 1/75 second).


# Version 0.9.3 2004-07-18

## New features and enhancements

* mkvmerge: new feature: When using a CUE sheet as a chapter file mkvmerge will
  automatically convert some of the entries to tags.
* mkvmerge: new feature: Added support for TTA lossless audio files.

## Bug fixes

* mmg: bug fix: The 'default track' checkbox was broken.
* mkvmerge: bug fix: Using '--cues ...:all' was broken for audio tracks that use
  lacing.
* mkvmerge: bug fix: The latest OpenDML AVI files generated by mencoder were not read
  correctly. Only the first RIFF chunk was processed.
* mkvmerge: bug fix: The default track feature did not work correctly with the new
  --track-order.

## Other changes

* mkvmerge: If the user does not specify a --language for a track 'und' ('undefined')
  will now be used instead of 'eng'. The user can use the new option
  '--default-language' to change that.


# Version 0.9.2 2004-06-29

## New features and enhancements

* mmg: new feature: Added 'minimize' buttons to the two 'mkvmerge is running'
  dialogs.
* mmg: new feature: Added an option for automatically calling 'File -> new' after a job
  has been added to the job queue.
* mkvmerge, mmg: new feature: --track-order now controls the track creation order
  globally, meaning that it isn't used for each file but only once. This allows the
  tracks to be created in ANY order (before it was first ordered by file, then by track).
  For mmg this means that the track list contains all available tracks and that there
  are no 'up' and 'down' buttons in the file list anymore.
* mmg: new feature: Line wrap the tooltips on Windows.
* mmg: new feature: Suggest a name for a new job based on the output file name.
* mmg: new feature: Temporarily disaable 'always on top' if the muxing or the job
  dialog are visible.
* mmg: new feature: Ask for confirmation before adding a job if there's already an old
  job with the same description.
* mkvmerge: new feature: You can specifiy the time after which to split with ms
  precision.

## Bug fixes

* mkvextract: bug fix: Video extraction was not working correctly on big endian
  systems.
* mmg: bug fix: The job manager did not always catch all of mkvmerge's output,
  especially if a job failed.
* mmg: bug fix: The functions 'move up', 'move down' and 'delete' in the 'job' dialog
  were not working correctly on Windows.
* mkvmerge: bug fix: Fixed more of that 'garbage at the beginning of MP3 streams'
  issue.
* mmg: bug fix: The 'always on top' option was ignored when starting mmg.
* mkvmerge: bug fix: Reading of broken / unfinished AVI files was broken on Windows.

## Other changes

* mmg: Updated the mkvmerge GUI guide to reflect changes and additions.


# Version 0.9.1 2004-06-13

## New features and enhancements

* mmg: new feature: The action 'delete job' in the job manager will also delete the file
  in the 'jobs' subdirectory.
* mmg: new feature: Added an option to make mmg stay always on top (only on Windows).
* mkvmerge: new feature: mmg will set the 'display dimensions' automatically for AVI
  files whose video track is MPEG4 and has the pixel aspect ratio stored in the
  bitstream.
* mmg: new feature: Added a dialog for adding arbitrary command line options which
  includes a list of advanced options to chose from.
* mkvmerge: new feature: Added support for the audio/video synchronization method
  used by NanDub (garbage at the beginning of audio tracks inside an AVI) for AC-3 and
  MPEG audio tracks. In other words: If an AVI is read and an audio track contains
  garbage right at the beginning then the corresponding audio delay is calculated and
  used instead of simply discarding the garbage.
* mkvmerge: new feature: Enabled reading MPEG4 video from MP4 files (nope, they're
  not stored in Matroska's native mode yet).

## Bug fixes

* mmg: bug fix: The job manager did not handle the conversion of non-ASCII characters
  correctly.
* mkvmerge: bug fix: The improved MP3 garbage detection was broken resulting in an
  error message from mkvmerge in some weird situations.
* mkvmerge: bug fix: Matroska tracks can use lacing (several frames inside one
  Matroska block with only one time code for the whole block). mkvmerge did not
  recreate the time codes for the frames 1..n in the lacing correctly.
* mkvmerge: bug fix: The OGM fix in 0.9.0 broke handling for non-broken OGM files a bit.

## Other changes

* mkvmerge: Dropped supoprt for 'aviclasses' (one of the two libraries for accessing
  AVI files). This mostly affects the Windows users as I've used aviclasses and not
  avilib on Windows so far. The 0.9.0-pre-builds so far haven't shown any problems,
  though, so I hope this doesn't break anything.
* mkvmerge: feature removed: Dropped support for 'time slices'. They were not used,
  didn't offer the player any additional value and caused massive increase in
  overhead.


# Version 0.9.0 2004-05-31

## Bug fixes

* mkvmerge: bug fix: Improved handling for OGM files. Streams that are lacking the
  comment packet are handled better.
* mkvmerge: bug fix: Some MP3 streams are padded in the front with trash (mostly those
  in AVI files). This trash might contain valid MP3 headers which do not match the
  remaining headers for the actual track. Both the MP3 reader and the MP3 packetizer
  can now skip up to one of those bogus headers in the trash.
* mmg: bug fix: On some occasions the chapter editor thought there was no language
  associated with a chapter name and complained about that.
* mkvmerge: bug fix: The OGM reader was not endian safe.
* mmg: bug fix: The chapter editor did not honor the values selected for 'country' and
  'language'.
* mkvmerge: bug fix: Audio sync for Vorbis was partially broken for positive offsets.
* mmg: Fix for compilation with wxWindows < 2.4.2.

## Other changes

* mmg: Removed the 'advanced' tab. Those options shouldn't be used anyway.
* mkvmerge: Rewrite of the VobSub handling code.


# Version 0.8.9 2004-05-06

## New features and enhancements

* mmg: new feature: mmg will ask for confirmation before overwriting a file. This can
  be turned off on the settings tab.
* mmg: new feature: Implement drag'n'drop of files onto the input, attachment and
  chapter tabs. For the input and attachment tabs it works like pressing the 'add'
  button. On the chapters tab it works like calling 'Chapter Editor -> Open'.

## Bug fixes

* mkvmerge: Fixes for compilation with gcc 3.4.
* mkvmerge: bug fix: Some strings read from RealMedia files were not zero-terminated
  resulting in broken track recognition for some files.

## Other changes

* mkvinfo/mmg: Enabled compilation with wxWidgets 2.5 and Unicode enabled builds of
  wxWidgets.
* all: Increased the precision for time codes in chapter files to nanoseconds
  (optionally, you can still use fewer digits after the '.').


# Version 0.8.8 2004-04-23

## New features and enhancements

* mmg: new feature: When adding Matroska files the video track's display dimensions
  are displayed as well.
* mkvmerge: new feature: Implemented reading AAC from AVIs.

## Bug fixes

* mkvinfo: bug fix: mkvinfo was forcing libmatroska not to handle unknown elements
  and crashed on those.
* mkvmerge: bug fix: The Flac packetizer was accessing uninitialized memory
  resulting in a crash on Windows.
* avilib: bug fix: Fixed compilation on big endian systems.
* mkvmerge: bug fix: Fixed the handling of RealMedia files with 'multirate' tracks
  (again).
* mkvmerge: bug fix: On some rare occasions chapters were not written correctly when
  splitting was active.
* mmg: bug fix: On non-Windows systems some combinations of wxWindows and GTK caused
  continuous 100% CPU usage after a special call to wxExecute.

## Other changes

* mkvtoolnix now depends on libebml 0.7.0 and libmatroska 0.7.0.


# Version 0.8.7 2004-04-05

## New features and enhancements

* mkvinfo: new feature: Added a terse output format via '-s'.
* mkvmerge: new feature: If using MPEG4 video and no aspect ratio or display
  dimensions are given mkvmerge will extract the aspect ratio information from the
  stream and automatically set the display dimensions accordingly.
* mkvextract: new feature: Added extraction of RealAudio and RealVideo tracks to
  RealMedia files.
* mmg: new feature: Added a 'job queue'. The current settings can be added as a new job,
  and all pending jobs can be started for batch processing without user interaction.

## Bug fixes

* mkvmerge: bug fix: Using audio sync on AC-3 tracks read from Matroska files did not
  work.


# Version 0.8.6 2004-03-13

## New features and enhancements

* mkvmerge: new feature: Tags are being kept when reading Matroska files.
* mmg: new feature: Automatically set the output file name when the first file is added
  to the same name but with a '.mkv' extension if it hasn't been set yet. Can be disabled
  on the 'settings' page.
* mkvmerge/mmg: new feature: Made the process priority selectable on the 'settings'
  page and default to 'normal' again (was 'lower' before).
* mmg: new feature: mmg will ask for confirmation before overwriting an existing
  output file.

## Bug fixes

* mkvmerge: bug fix: OGMs created by Cyrius OGMuxer are missing comment packets for
  some streams which mkvmerge choked on.
* mkvmerge/mmg: bug fix: The LANGUAGE and TITLE comments from OGM files were not set in
  the GUI when adding such files.
* mmg: bug fix: If the FourCC was set for one track it had been used for each track you
  selected as well.
* mkvmerge: bug fix: Large values for --sync (over 2100) would cause an integer
  overflow resulting in no sync being done at all.
* mkvmerge: bug fix: The VobSub handling was broken if the .idx file contains an entry
  for a track ("id: en") but no "timestamp:" entries for such a track.
* mkvmerge: bug fix: The segment UID was not generated if splitting was off.
* mkvmerge: bug fix: More of the non-ASCII character fixes (in --tags and --chapters
  this time).
* mkvmerge: bug fix: No memory was allocated for the --attachment-description
  resulting in weird descriptions or mkvmerge aborting with 'invalid UTF-8
  characters'.
* mkvmerge: bug fix: More of the non-ASCII characters fixes.
* mkvmerge: bug fix: File names with non-ASCII characters like Umlaute are handled
  correctly.
* mkvmerge: bug fix: Some RealMedia files contain several tracks for multirate stuff
  which are now ignored. Only tracks with known MIME types (audio/x-pn-realaudio and
  video/x-pn-realvideo) are used.

## Other changes

* mmg: Added a list of 'popular' languages on top of all language drop down boxes.


# Version 0.8.5 2004-02-22

## Bug fixes

* mkvmerge: bug fix: segfault in the RealMedia reader.
* mmg: bug fix: When adding a Matroska file that contains a track name or a title with
  non-ASCII characters those would be displayed as UTF-8 in the appropriate input
  boxes. This has been changed, but obviously it won't work if you add files with
  Japanese characters on a system with a different locale. For full Unicode support
  you'll have to wait quite a bit longer.
* mmg: bug fix: For some 'browse file' buttons the default directory was not set to the
  last directory a file was selected from.
* mmg: new feature: Added a function for adjusting the chapter time codes by a fixed
  amount.
* mkvmerge: bug fix: Splitting by size would sometimes abort directly after opening
  the second file.
* mkvmerge: bug fix: Splitting by time was broken.

## Other changes

* all: A couple of changes that allow compilation on MacOS X.
* avilib: synchronized with transcode's current CVS version.


# Version 0.8.4 2004-02-11

## Bug fixes

* mkvmerge: bug fix: When reading Matroska files the durations attached to blocks
  were lost (e.g. for subtitle tracks).


# Version 0.8.3 2004-02-09

## New features and enhancements

* mkvmerge: new feature: The LANGUAGE, TITLE tags and chapters are being kept when
  reading OGM files.
* mkvmerge: new feature: Enabled reading of AAC from OGMs.

## Bug fixes

* mkvmerge: bug fix: VobSub durations were not converted from ms to ns precision
  resulting in VERY short packets :)
* mkvmerge: bug fix: The change from ms to ns precision broke subtitle handling from
  OGM.
* mkvmerge: bug fix: Segfault when using external time code v1 files.
* mkvmerge: bug fix: The AAC-in-Real stuff again.
* mkvmerge: bug fix: Fixed a couple of memory leaks, especially in the QuickTime/MP4
  parser.
* mkvmerge: bug fix: Proper handling for AAC read from RealMedia files (sample
  rate/output sample rate were not assigned correctly).

## Other changes

* mkvmerge: Changed the meaning of '--global-tags'. They now apply to the complete
  file.
* mkvmerge: Made "do not link files when splitting" the default, just like in mmg.
* mkvmerge: The VobSub reader will not discard packets that exceed a certain size
  (64KB) anymore.
* mkvmerge: Improved some internal memory freeing decisions. This should help with
  files/sections in which are only few keyframes.
* mkvmerge: Changed the two-pass splitting into a one-pass splitting. The resulting
  files will always be a little bit larger than the desired size/length, but this
  shouldn't matter.
* mmg: Rewrote the chapter editor. It now makes a lot more sense: You can have multiple
  names for one chapter entry, and for each name there's only one language/country
  association.
* mkvmerge: Changed the complete time code handling from ms precision to ns
  precision. Expect some things to be broken by this change.
* mmg: Added some more extensions for RealMedia files.


# Version 0.8.2 2004-01-21

## New features and enhancements

* mkvmerge: new feature: The track headers will be rendered completely including the
  elements that are set to their default values. Causes less confusion and allows the
  setting of e.g. the track language without having to remux the file completely.
* mmg: new feature: Automatically pre-set the attachment's MIME type if the file has a
  known extension (e.g. 'text/plain' for '.txt').
* mkvmerge: new feature: Unknown/unsupported track types can be copied 1:1 from
  Matroska input files.
* mkvmerge: new feature: Added proper support for AAC-inside-RealMedia files.
* mkvmerge: new feature: Write cues for audio-only files as well (not more than one cue
  entry during a two seconds period).
* mkvmerge: new feature: Added the two new chapter flags 'hidden' and 'enabled'.
* mkvmerge: new feature: Added a new format for the external time code files.

## Bug fixes

* mkvmerge: bug fix: The PCM handling was broken resulting in packets that did not end
  on sample boundaries.
* mkvmerge: bug fix: AVIs with uncompressed sound were leading to buffer overflows.
* mkvmerge: bug fix: If remuxing a file that contains frames with a reference to the
  same time code those references were lost turning such P frames into I frames. This
  was the case for some RealAudio stuff.
* mkvmerge: bug fix: The default track flags could not be overriden on the command line
  when reading Matroska files.
* mkvmerge: bug fix: The VobSub handling was on occasion putting SPU packets for the
  wrong MPEG stream into the current stream resulting in that particular entity not
  being displayed.

## Other changes

* mkvmerge/mmg: allow the track names to be empty so that you can remove them when
  muxing Matroska files. Same for the file title.
* Windows binaries after v0.8.1 require a new runtime DLL archive. Please download it
  from https://mkvtoolnix.download/ Thanks.


# Version 0.8.1 2004-01-06

## Bug fixes

* mkvmerge: bug fix: The I/O classes were not initialized correctly on Windows
  resulting in spontaneous strange error messages, especially when muxing VobSubs.
* mkvmerge: bug fix: For some special atom sizes in Quicktime and MP4 files the size was
  not read correctly. This affected e.g. files created by Nero Digital.
* mkvmerge: bug fix: Segfault when muxing some video formats due to unchecked data
  (includes RealVideo).


# Version 0.8.0 2004-01-01

## New features and enhancements

* mkvmerge, mkvextract, mkvinfo: Added support for the new tagging system.

## Bug fixes

* mmg: bug fix: Fixed the "write chapters to Matroska file" feature.
* mmg: bug fix: Made mmg not abort but only display an error message when malformed XML
  chapter files should be loaded.
* mkvmerge: bug fix: The timescodes for Vorbis were calculated one packet too early
  (meaning that the first packet did not start at 0).
* mmg: bug fix: The default values for the chapter language and chapter country are now
  applied when loading simple (OGM) style chapter files as well.
* mkvmerge: bug fix: The VobSub packetizer will assume MPEG2 if no MPEG version
  identifier was found ("Unsupported MPEG version: 0x00...").
* mkvextract: bug fix: Wrong display output and illegal memory access when
  extracting FLAC files.
* mmg: bug fix: If one added a Matroska file and the track name or language of a track
  consisted of only blanks then mmg would segfault.
* mmg: bug fix: The chapter editor did not properly escape the chapter names resulting
  in invalid XML files if the special characters &, < or > were used.
* mkvmerge: bug fix: If splitting was active then a wrong CodecID was written to the
  second and all following files for MP2 tracks.

## Other changes

* mmg: Made "don't link" ON by default because some players might have problems with
  the second and all following files if they don't expect them not to start at 0.
* mkvmerge: There are MP4 files that actually contain HE-AAC but don't have the 5 byte
  identifier. mkvmerge will also assume SBR if there's only the 2 byte identifier with
  a sampling frequency < 44100Hz.
* mmg: The input box will automatically select the first track when a file is selected.
  Upon track selection the input focus is set to the track name input box.
* mmg: The chapter editor automatically focuses the chapter name input box whenever a
  chapter entry is selected.


# Version 0.7.9 2003-12-11

## New features and enhancements

* mmg: new feature: Added "up" and "down" buttons for the tracks, too.
* mmg: new feature: Added a menu option, 'set output file', that can be used as an
  alternative to the "browse" button at the bottom (for those poor users with nothing
  more than 800x600 ;)).
* mkvmerge: new feature: The user can alter the order in which the tracks for an input
  file are put into the output file with the new "--track-order" option.
* mmg: new feature: Added buttons for moving input files up and down in the input file
  box.

## Bug fixes

* mmg: bug fix: Removed the Ctrl-v and Ctrl-c accelerators that I used for mmg
  functions which overrode the usual 'paste' and 'copy' functionality.
* mkvmerge: bug fix: Negative track IDs in Ogg files were reported incorrectly for
  mkvmerge -i (which affected the GUI).
* mkvmerge: bug fix: Internal changes had messed up the --language and --track-name
  functionality.
* mmg: bug fix: The "AAC is SBR" check box was grayed out for AAC inside MP4 files.
* mmg: bug fix: The "load settings" function did not load all settings, and some
  strings were not allocated at all resulting in a crash when a track was removed after
  loading these settings.
* mkvmerge: bug fix: The AAC packetizer was not working if packets were being read from
  a raw AAC file (it worked fine from MP4 and Matroska files).
* mkvmerge: bug fix: Avoid deadlocks when parsing broken SPU packets from VobSubs.

## Other changes

* mkvmerge: Set the thread priority to BELOW_NORMAL on Windows (mkvmerge was already
  nice(2)'d on Unix systems).
* mmg: Command line arguments are put into an option file which is then handed over to
  mkvmerge. This allows really long command lines, even on Windows.


# Version 0.7.8 2003-12-02

## New features and enhancements

* mmg: new feature: You can set the values for the language and/or country codes for a
  chapter and all its children with the push of one button (the new "Set values"
  button).
* mmg: new feature: You can set default values for the language and the country codes in
  the chapter editor (Chapter menu -> Set default values).
* mkvmerge: new feature: Added an option '--display-dimensions' which allows the
  direct setting of the display dimensions. It is mutually exclusive with
  '--aspect-ratio', of course.
* mkvmerge: Added an option for dumpig all split points including file size and
  timestamp information after the first splitting pass.

## Bug fixes

* mkvmerge: bug fix: Display dimensions were sometimes off by one, e.g. 640x479
  instead of 640x480. This should not happen anymore for sane pixel dimensions.
* mmg: bug fix: The language combo box was not correctly set on Windows.
* mmg: bug fix: Quotes were missing if the time code file's name contained spaces.
* everything: Committed a lot of cross-OS compatibility fixes (thanks to Haali and
  thedj).

## Other changes

* mkvmerge: Changed the options '--fourcc' and '--aspect-ratio'. They now take a
  track ID just like all the other track specific options.
* mkvmerge: Rewrote the SPU packet parsing code. It should not abort anymore.


# Version 0.7.7 2003-11-16

## New features and enhancements

* mkvmerge: Added full support for FLAC (both raw FLAC and OggFLAC are supported, even
  though raw FLAC is very slow).
* mmg: Added an input field for the 'time codes' file to the track options.
* mkvmerge: new feature: CUE sheets can be used for chapters.
* mkvmerge: Added support for --sync for VobSub tracks.
* mmg: When a file is being added then some information from it (languages, track
  names, file title) are kept, and the appropriate input boxes are pre-set with these
  values. Works only for formats that support such information (Matroska, VobSub).

## Bug fixes

* mkvmerge: bug fix: Reworked the audio sychronization which did not work correctly
  for Matroska source files.
* mkvmerge: bug fix: Increased the size of the space reserved for the first meta seek
  element (see mkvmerge.1 for an explanation). In some situations (with tags,
  chapters, attachements and very big file) it might not have been enough in order to
  contain all elements.
* mkvmerge: bug fix: When reading MP3 audio tracks from a Matroska file with the
  A_MS/ACM CodecID (MS compatibility mode) the layer was not identified correctly.
* mkvmerge: Implemented a lot of fixes for big endian systems and processors that
  don't allow non-aligned memory access for word or bigger sized objects.
* mkvmerge: bug fix: If running in identification mode (-i, used by mmg a lot) then
  don't output any warnings or mmg will not accept this file.

## Other changes

* mkvextract: Added extraction of FLAC to raw FLAC or OggFLAC files.
* mmg: Added an input field for the 'CUE sheet to chapter name' conversion format.
* mkvmerge: Improved the file type detection for AC-3 and AAC files a bit.
* mmg: Made mmg accept return codes of 1 when 'mkvmerge -i' is run when an input file is
  added. This way mmg won't reject mkvmerge's output if mkvmerge only printed some
  warnings which will result in a return code of 1 instead of 0.
* mkvtoolnix: Re-worked the configure script. Removed all the lib specific
  --with-...-include and --with-...-lib options. The --with-extra-includes and
  --with-extra-libs options can be used instead.
* mkvmerge: Sped up the reading of VobSub .idx files.


# Version 0.7.5 2003-11-05

## New features and enhancements

* mkvmerge: new feature: Added the ability to read time codes from text files which
  override the time codes mkvmerge calculates normally.
* mmg: new feature: Added a new menu entry "File -> new" which will clear all the current
  muxing settings.
* mmg: Added support for VobSub subtitles including their compression options.
  Added the .m4a extension to the 'add file' dialog.
* mkvmerge: new feature: Implemented generic support for frame compression (mostly
  useful for VobSub subtitles but could also be used for others) and the complete
  framework for handling content encodings in the Matroska reader.
* mkvinfo: new feature: Dump unknown elements recursively.

## Bug fixes

* mkvmerge: bug fix: The VobSub .idx parser was dividing by 0 if a track only contained
  one entry.
* mkvmerge: Fixed the time code reader code and made it a bit more flexible. Added more
  documentation for this feature along with an example file (examples/example-time
  codes.txt).
* mmg: bug fix: When 'default track' is selected then all other tracks of the same type
  will have their 'default track' flag cleared.
* mkvextract: bug fix: Add all the mandatory elements when extracting chapters so
  that the resulting XML can always be used directly with mkvmerge again without
  having to manually add e.g. ChapterLanguage.
* mkvmerge: bug fix; Handle audio tracks from Matroska files with the CodecID
  A_MS/ACM correctly.
* mkvmerge: bug fix: The VobSub .idx parser was mis-calculating the subtitle entry
  frame sizes.
* mkvmerge: bug fix: The Vorbis packetizer was miscalculating the number of samples
  to add/remove when using audio sync.
* mmg: bug fix: Made the input boxes for file names (tags and chapters) editable so that
  their contents can be deleted.
* mkvmerge: bug fix: Made the SRT reader more tolerant regarding empty lines.

## Other changes

* mkvmerge: SPU packets belonging to the same time code are grouped together, and the
  duration is extracted directly from the SPU stream.
* mkvmerge: The VobSubs are now stripped of the MPEG program stream, and only the SPU
  packets are kept.
* mkvmerge, mkvextract: The Matroska reader and the OGM reader (mkvmerge) as well as
  mkvextract will discard empty or 'cleaning only' subtitle packets as they are
  appear in OGMs in order to mark the end of an entry.
* mkvmerge: Changes to use libmatroska's new lacing code.
* mkvmerge: Adjusted the compression handling to the final content encoding specs.


# Version 0.7.2 2003-10-14

## New features and enhancements

* mkvmerge: Implemented some speedups for a couple of container formats and track
  types (mainly AVI reader, MP3/AC-3/AAC packetizers). Especially noticeable when
  splitting is active as well.

## Bug fixes

* mkvmerge: bug fix: If 'no linking' and splitting was active mkvmerge would abort on
  the start of the second output file due to time codes that were calculated
  incorrectly.
* mkvextract: bug fix: Support for extracting SBR AAC (previous 'fix' did not
  actually fix this).
* mkvextract: bug fix: All extracted subtitles where written to the first output file
  given, not to the one they were supposed to be written to.
* mmg: bug fix: The 'abort' button was doing nothing under Windows.
* mmg: bug fix: Audio, video and subtitle track selection was translated into the
  wrong command line options.

## Other changes

* mkvmerge: Replaced the avilib based AVI reading functions with AVI classes from
  Cyrius.


# Version 0.7.1 2003-10-03

## New features and enhancements

* mkvmerge: new feature: Attachments are kept when reading Matroska files.
* mmg: new feature: Added a (nearly) full-featured chapter editor.
* mkvmerge: new feature: RealAudio can be read from Matroska files.

## Bug fixes

* mkvmerge: bug fix: XML chapters were not parsed correctly.
* mkvmerge: bug fix/new feature: Rewrote the complete MP3 handling. Now files with
  ID3 tags (both v1 and v2) are handled correctly. All MPEG-1 audio files (all layers)
  should be handled correctly now.
* mkvextract: new feature: Support for extract HE-AAC tracks to .aac files. Bug fix:
  Missing elements (default values) are handled correctly for audio tracks.
* mkvmerge: bugfix: If attachments were given with path components then the path
  component wasn't discarded for the attachment's description on Windows (normally
  only the file name should be used as the attachment's name).
* mmg: Fixed wrong order of the options --chapters, --chapter-language and
  --chapter-charset.
* mmg: bugfix: Moved the aspect ratio and FourCC input fields from the global tab to the
  input tab where they belong to.
* mkvmerge: bugfix: RealVideo was not read correctly from Matroska files.
* mkvmerge: bugfix: The SRT reader would abort if there was more than one empty line
  between subtitle entries line.
* mkvextract: bugfix: Proper BOMs are written according to the desired charset when
  extracting text subtitles.

## Other changes

* Added a guide for mmg including some pictures.
* mkvmerge: Changed the lacing strategy again. New defaults are NOT to write duration
  elements for all blocks, NOT to use time slices and to USE lacing for most audio
  tracks. This will save some space. The downside is that the laced frames 'lose' their
  precise time code information. Current demuxers don't care and will work
  nevertheless. More sophisticated applications that make use of these advanced
  information (duration elements, time slices) are not available at the moment. All
  these options can be toggled by the user with the new/modified options
  --disable-lacing, --enable-durations and --enable-timeslices.
* mmg: Added a lot of checks on the data given by the user so that invalid data is reported
  by mmg and not by mkvmerge.
* mmg: Made the app a GUI app which gets rid of the "DOS box" on Windows.


# Version 0.7.0 2003-09-16

## New features and enhancements

* mkvmerge: Implemented an experimental VobSub reader and packetizer. No specs
  exist for these yet, though.
* mkvmerge: Added support for XML based chapter files.

## Other changes

* mkvextract: Add an UTF-8 BOM to extracted SSA/ASS and SRT subtitle files. Print
  warnings for missing durations for text subtitle tracks.
* Added a complete GUI for mkvmerge, mkvmergeGUI (mmg) based on the work of Florian
  Wagner.
* mkvmerge: Support for setting the track names.
* mkvmerge: For Matroska source files: If the source contains chapters then these are
  kept unless the user specified chapters with --chapters.
* mkvmerge: Improved the support for Matroska files with tracks with big gaps between
  entries, e.g. subtitle tracks whose entries are a minute or more apart.
* mkvmerge: When splitting is active and the source is a Matroska file then
  splitpoints were borked, and the first pass was slow as your average mole.
* mkvmerge: The track UIDs are kept when reading Matroska files even when splitting is
  active.
* mkvmerge: Added a QuickTime/MP4 reader. Can handle several QuickTime video and
  QuickTime audio formats as well as AAC (both 'normal' AAC and SBR AAC).
* mkvmerge: DisplayWidth and DisplayHeight are kept intact when reading from a
  Matroska file but can be overridden with --aspect-ratio.
* Wrote documentation, XML examples and the DTD for the XML chapter files.
* mkvinfo: Rewrote mkvinfo to use libebml's Read() function instead of manually
  reading each and every element.


# Version 0.6.5 2003-08-29

## Bug fixes

* mkvmerge: On Windows the 'isspace()' function used to trim leading and trailing
  white spaces from tags considered some parts of valid UTF-8 character sequences to
  be white spaces as well. Fixed by replacing 'isspace()' with 'isblank()'. Reported
  by Liisachan.
* mkvmerge: Real reader: For RV40 the actual dimensions were also used for the aspect
  ratio/display dimensions. This has been fixed: the actual dimensions are used for
  PixelWidth/PixelHeight, the dimensions stored in the RM container are used for the
  aspect ratio/DisplayWidth & DisplayHeight. Reported by Karl Lillevold.

## Other changes

* mkvmerge: Support for chosing the charset and language used in simple chapter
  files. Suggestion by Liisachan.
* Rewrote the UTF-8 conversion routines. They should now handle U+8000 characters
  correctly. Reported by Liisachan.


# Version 0.6.4 2003-08-27

## New features and enhancements

* mkvmerge: Meta seek element is split into two elements. The first's located at the
  start of the file containing only a small number of level 1 elements. The clusters are
  referenced in a second meta seek element located at the end of the file. Removed the
  options "--meta-seek-size" and "--no-meta-seek". Added the option to disable
  that second meta seek entry, "--no-clusters-in-meta-seek".
* mkvinfo: Added support for the following elements: KaxPrevFilename,
  KaxNextFilename, KaxTrackFlagEnabled, KaxTrackName, KaxCodecName,
  KaxCodecSettings, KaxCodecInfoURL, KaxCodecDownloadURL, KaxCodecDecodeAll,
  KaxTrackOverlay, KaxAudioPosition, KaxAudioOutputSamplingFreq,
  KaxVideoDisplayUnit, KaxVideoColourSpace, KaxVideoGamma,
  KaxVideoFlagInterlaced, KaxVideoStereoMode, KaxVideoAspectRatio,
  KaxClusterPosition, KaxClusterPrevSize, KaxBlockVirtual,
  KaxBlockAdditions, KaxBlockMore, KaxBlockAddID, KaxBlockAdditional,
  KaxReferenceVirtual, KaxSliceBlockAddID, KaxChapters, KaxEditionEntry,
  KaxChapterAtom, KaxChapterUID, KaxChapterTimeStart, KaxChapterTimeEnd,
  KaxChapterTrack, KaxChapterTrackNumber, KaxChapterDisplay,
  KaxChapterString, KaxChapterLanguage, KaxChapterCountry

## Bug fixes

* mkvmerge: Fixed some missing default values in the Matroska reader (e.g. mono audio
  files). Reported by Liisachan.
* mkvmerge: Bugfix: If a subtitle packet was the last packet in a cluster then its
  duration was not written resulting in a broken file.

## Other changes

* mkvextract: Support for re-creating dropped frames when extracting video to an
  AVI. Works only well if the frame durations in the source file are multiples of the
  frame rate, of course.
* mkvmerge: The MP3 packetizer did not start at 0 with its time codes. It does now.
  Reported by alexnoe.
* mkvmerge: Proper support for dropped frames when reading AVI files. Reported by
  alley_cat, Horváth István.
* mkvmerge: Improved all command line parsing error messages.
* mkvmerge: Improved the error message for the XML tag file parser if an invalid
  &-sequence is found.
* mkvextract: Strings are postprocessed so that the special characters &, <, >, " are
  replaced by their HTML equivalents &amp;, &lt, &gt; and &quot;.
* mkvmerge: Disabled lacing by default and renamed --no-lacing to --enable-lacing.
  With all the proper info about the laced frames lacing is actually producing larger
  files than without lacing.
* mkvextract: Backwards compatibility: Accepts S_SSA and S_ASS as valid CodecIDs
  (new CodecIDs are S_TEXT/SSA and S_TEXT/ASS).


# Version 0.6.3 2003-08-20

## New features and enhancements

* mkvmerge: Implemented a switch that has to be used for SBR AAC / AAC+ / HE-AAC if the
  source file is an AAC file and the AAC file contains SBR AAC data (no automatic
  detection possible in this case!).

## Bug fixes

* Windows versions: Fixed a bug with files bigger than 2GB not being recognized. The
  accompanying error message was "File NAME has unknown type. Please have a look at the
  supported file types..."
* all tools: Fixed a bug which would only allow Matroska files up to 4GB to be read. The
  accompanying error message was "No segment found" or something similar.

## Other changes

* mkvmerge: The Real reader accepts incomplete video packets and tries to
  re-assemble them instead of aborting with 'die: len != total'.
* mkvmerge: Low bitrate AC-3 tracks from Real's DNET are identified as A_AC3/BSID9 or
  A_AC3/BSID10.
* mkvmerge: The RealMedia reader takes the number of packets into account when
  reading which results in better end-of-file detection.
* mkvinfo: Unknown elements are properly skipped now.
* mkvmerge: For RV40 (RealVideo 9) the actual video dimensions are decoded from the
  first video frame.


# Version 0.6.2 2003-08-11

## Other changes

* mkvmerge: Video aspect ratio was set wrong if the user did not specify any.


# Version 0.6.1 2003-08-11

## New features and enhancements

* mkvinfo: Added Adler-32 calculation and display for frame contents with the -c
  option.

## Bug fixes

* mkvmerge: Fixed support for reading MultiComment tags from XML tag files.
* mkvmerge: Fixed a bug with chapters and splitting which would crash mkvmerge if no
  chapter belonged into the output file.

## Other changes

* mkvmerge: RealVideo: Support for all kinds of frames including "short" and
  "merged" frames (results are identical to Gabest's output).
* mkvmerge: The aspect ratio setting will only cause upscaling of the current video
  dimensions which are then put into KaxVideoDisplayWidth and
  KaxVideoDisplayHeight.
* mkvextract: Changed how the global elements are handled by taking the parent's size
  into account. This re-enables processing of files produced with the latest
  VirtualDubMod.
* mkvmerge: Changed how the Matroska reader handles global elements by taking the
  parent's size into account. This re-enables processing of files produced with the
  latest VirtualDubMod.
* mkvinfo: Changed how mkvinfo handles global elements by taking the parent's size
  into account. Hopefully this is now correct.
* mkvextract: Support for MultiComment tags.
* mkvmerge: Allow some slightly broken Matroska files to be processed correctly if
  the reference blocks are off by at most 1ms.
* mkvmerge: MP3: Better support for other MPEG versions and layers (number of samples
  per packet).
* mkvmerge: RealAudio: "dnet" is actually byte-swapped AC-3 and is being treated as
  such (re-swapped and output as AC-3).
* Changes for compilation with gcc 2.95.


# Version 0.6.0 2003-08-04

## New features and enhancements

* mkvmerge: Added support for simple chapter files (CHAPTER01=...,
  CHAPTER01NAME=Hello World etc).
* mkvmerge: Added support tags based on XML files.
* mkvextract: Rewrote the command line syntax. Added extracting attachments and
  tags as new options.
* mkvmerge: Added support for the "SegmentTitle" (general title of the file
  written).
* mkvmerge: Added support for UTF-8 and UTF-16 encoded text files for the SRT and
  SSA/ASS readers.
* mkvmerge: Added support for attaching files to the output file(s).
* mkvinfo: Added support for the rest of the tags: KaxTagMultiComment,
  KaxTagMultiCommentName, KaxTagMultiCommentComments and
  KaxTagMultiCommentLanguage. Almost all tags have been successfully tested.
* mkvinfo: Added support for allmost all tags (totally untested): KaxTag
  KaxTagArchivalLocation KaxTagAudioEncryption KaxTagAudioGain
  KaxTagAudioGenre KaxTagAudioPeak KaxTagAudioSpecific KaxTagBibliography
  KaxTagBPM KaxTagCaptureDPI KaxTagCaptureLightness
  KaxTagCapturePaletteSetting KaxTagCaptureSharpness KaxTagChapterUID
  KaxTagCommercial KaxTagCropped KaxTagDate KaxTagDiscTrack KaxTagEncoder
  KaxTagEncodeSettings KaxTagEntity KaxTagEqualisation KaxTagFile
  KaxTagGeneral KaxTagGenres KaxTagIdentifier KaxTagImageSpecific
  KaxTagInitialKey KaxTagKeywords KaxTagLanguage KaxTagLegal KaxTagMood
  KaxTagMultiCommercial KaxTagMultiCommercialAddress
  KaxTagMultiCommercialEmail KaxTagMultiCommercialType
  KaxTagMultiCommercialURL KaxTagMultiDate KaxTagMultiDateDateBegin
  KaxTagMultiDateDateEnd KaxTagMultiDateType KaxTagMultiEntity
  KaxTagMultiEntityAddress KaxTagMultiEntityEmail KaxTagMultiEntityName
  KaxTagMultiEntityType KaxTagMultiEntityURL KaxTagMultiIdentifier
  KaxTagMultiIdentifierBinary KaxTagMultiIdentifierString
  KaxTagMultiIdentifierType KaxTagMultiLegal KaxTagMultiLegalAddress
  KaxTagMultiLegalType KaxTagMultiLegalURL KaxTagMultiPrice
  KaxTagMultiPriceAmount KaxTagMultiPriceCurrency KaxTagMultiPricePriceDate
  KaxTagMultiTitle KaxTagMultiTitleAddress KaxTagMultiTitleEdition
  KaxTagMultiTitleEmail KaxTagMultiTitleLanguage KaxTagMultiTitleName
  KaxTagMultiTitleSubTitle KaxTagMultiTitleType KaxTagMultiTitleURL
  KaxTagOfficialAudioFileURL KaxTagOfficialAudioSourceURL
  KaxTagOriginalDimensions KaxTagOriginalMediaType KaxTagPlayCounter
  KaxTagPopularimeter KaxTagProduct KaxTagRating KaxTagRecordLocation KaxTags
  KaxTagSetPart KaxTagSource KaxTagSourceForm KaxTagSubGenre KaxTagSubject
  KaxTagTargets KaxTagTitle KaxTagTrackUID KaxTagVideoGenre
* mkvmerge: Implemented time slice durations , default block duration and block
  durations for slices where necessary.

## Bug fixes

* mkvmerge: Fixed a bug in the SRT reader which would not always handle Unix/DOS style
  new line cases correctly.
* mkvmerge: Fixed some infinite-reading-from-a-file bug that occured on Windows
  when reading SSA/ASS files.
* mkvmerge: Fixed a bug which would mostly appear with subtitles that have very long ( >
  60s) gaps between entries. Here the cluster would not been rendered properly
  leaving mkvmerge either comatose ( = endless loop) or just plain dead ( = crashing).

## Other changes

* base64tool: Added a tool for Base64 encoding/decoding needed for binary elements
  in the tags.
* mkvextract: Support for extracting chapter information.
* mkvmerge: The SSA/ASS reader ignored the --sub-charset option and always used the
  current charset to recode the subtitles.
* mkvinfo: Support for the elements dealing with attachments (KaxAttachments,
  KaxAttached, KaxFileDescription, KaxFileName, KaxMimeType, KaxFileData).
* mkvmerge: Changed the RealVideo packaging method: Subpackets are assembled into
  complete packets so the demuxer does not have to do that anymore.
* mkvmerge: DisplayWidth and DisplayHeight, which form the display aspect ratio,
  are now always written to ease changing them later without having to completely
  remux the file.
* Added a RealMedia demuxer that can handle both RealVideo and RealAudio (all
  codecs).
* mkvmerge: Support for handling native video tracks (e.g. B frames) when reading
  Matroska files.
* mkvinfo: Support for KaxSegmentFilename, KaxTitle, KaxSlices, KaxTimeSlice,
  KaxSliceLaceNumber, KaxSliceFrameNumber, KaxSliceDelay and
  KaxSliceDuration.


# Version 0.5.0 2003-06-22

## Important notes

* Made the AAC reader automatically recognize if a MPEG4 AAC file contains the
  emphasis header (deprecated) or not (current standard).

## Bug fixes

* Fixed a double free() on cleanup (after writing the cues) which resulted in a
  segfault sometimes.

## Other changes

* Added 'ReferencePriority' element to the known elements for mkvinfo.
* Removed "(mkvinfo) " from mkvinfo's output in order to improve readability and save
  space.
* --sub-charset now also needs a track ID.
* Modified the verbosity levels for mkvinfo: The seek head subentries and cue
  subentries will only be shown at level 2 to make the output easier to read.
* The language and default track settings are now kept again if not overridden when
  reading from Matroska files.
* Added mkvextract which can extract tracks from a Matroska file into other files.
* Switched from cygwin to MinGW32 for the Windows binaries.
* Added a SSA/ASS reader.
* Support for reading text subtitles from Matroska files.


# Version 0.4.4 2003-06-15

## New features and enhancements

* Added an option for identifying input files and their track types.

## Other changes

* Several options now need an explicity track ID to specify which tracks of an input
  file the option should be applied to. These options include --atracks, --vtracks,
  --stracks, --sync, --default-track, --cues and --language.
* The Matroska reader now handles track selection correctly.


# Version 0.4.3 2003-06-12

## New features and enhancements

* Added support for splitting output files by size or by time and limiting the number of
  output files.
* Added support for the segment UID/next segment UID/previous segment UID.
* Implemented stricter content based file type identification for MP3 and AC-3 files
  so that those won't be mis-identified.
* Some improvements to the mkvinfo GUI (thanks to jcsston for the patch/the ideas).

## Other changes

* Support for proper linking of segments via the segment UIDs. The first and last files
  created can be manually linked to given UIDs.
* A lot of changes to comply with libmatroska/libebml 0.4.4.


# Version 0.4.2 2003-05-29

## Bug fixes

* Fixed a segfault in the Matroska reader.

## Other changes

* Support for some more tags in both mkvmerge and mkvinfo.
* Removed the '--sub-type' switch as all text subtitles will be stored in UTF-8
  format. Made iconv mandatory in the configure checks for this very reason.
* Added a GUI to mkvinfo.


# Version 0.4.1 2003-05-23

## Other changes

* A lot of changes regarding file I/O. Files bigger than 2GB should now be handled
  correctly on both Linux and Windows.
* Added checks for MP4/Quicktime files which will abort mkvmerge.
* Support for reading AAC tracks from Matroska files.


# Version 0.4.0 2003-05-22

## New features and enhancements

* Some internal changes and enhancements. Code requires libebml and libmatroska
  0.4.3 now.
* Added support for AAC files (only those with ADTS headers at the moment).

## Bug fixes

* Fixed a bug with mono MP3 files.

## Other changes

* ADTS headers are stripped from the AAC streams. This is what I'd call 'proper AAC
  support'.
* Better support for DTS streams in general and for DTS-in-WAV in particular (patch by
  Peter Niemayer <niemayer AT isg.de>).
* Renamed '--no-utf8-subs' to '--sub-type utf8'. Polished the man page regarding
  subtitle handling.


# Version 0.3.3 2003-05-15

## Bug fixes

* Fixed a bug with the AC-3 time code calculation (patch by Peter Niemayer <niemayer AT
  isg.de>).

## Other changes

* If an error occurs while writing to the destination file the error is reported and
  mkvmerge aborts with a non-zero exit code.
* The OGM reader reported I frames as P frames and vice versa round making seeking not
  really nice ;)
* Support for reading DTS files & putting them into Matroska (main patch by Peter
  Niemayer <niemayer AT isg.de>, a few things by me).


# Version 0.3.2 2003-05-11

## New features and enhancements

* Added support for aspect ratio.

## Bug fixes

* Fixed the huge memory need if reading from AVI files (introduced on 2003-05-06 with
  the internal changes).

## Other changes

* Proper handling of the 'default track' flag and the language for the Matroska
  reader.
* Proper handling of the 'default track' flag for all the packetizers.
* Made mkvtoolnix compile under cygwin.
* Subtitle charsets can be specified with --sub-charset and do not rely on the current
  locale anymore.
* For the last packet of each track its duration is now stored.
* A lot of internal changes - I hope nothing has broken... (See ChangeLog.cvs for
  details.)
* The matroska reader calculated wrong header lengths for Vorbis tracks.
* mkvinfo reports the FourCC for video tracks with a CodecID of V_MS/VFW/FOURCC and
  the format tag for audio tracks with a CodecID of A_MS/ACM.


# Version 0.3.1 2003-05-03

## New features and enhancements

* Added support for EbmlVoid everywhere to mkvinfo.

## Other changes

* Tracks read from a Matroska file will keep their UID if it hasn't been used yet.
* Support for reading text subtitle streams from OGM files.
* Support for KaxTrackLanguage and ISO639 languages.


<!-- Local Variables: -->
<!-- fill-column: 78 -->
<!-- End: -->
