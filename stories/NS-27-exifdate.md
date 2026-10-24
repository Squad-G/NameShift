# NS-27: Read the photo (EXIF) date from a JPEG

Status: Ready
Depends on: NS-1
Area: engine

## Why
Name photos by the date they were taken. This story only reads the date from a file's bytes; using it in the Auto date
rule is NS-62. The bytes are untrusted (any file can be named `.jpg`), so every bound below has a test.

## Acceptance criteria
A public static class `PhotoDate` in `NameShift.Core` with `DateTime? Read(Stream stream)`: the date and time the camera
stored as DateTimeOriginal (EXIF tag 0x9003), or null. It is a small reader of the JPEG and EXIF byte layout written for
NameShift. `NameShift.Core.csproj` is unchanged: no package, no `System.Drawing` (Core has no UI dependency; the reviewer
checks the diff). The date is returned as stored (the camera's clock, no time zone conversion,
`DateTimeKind.Unspecified`). Bad data of any kind gives null, never an exception; an `IOException` raised by the stream
itself is not caught (the caller handles disk errors). A null stream throws `ArgumentNullException`. The tests build their
JPEG bytes in a small helper (little- and big-endian, no binary fixture files).

How it reads (the rules the tests below check):
- It starts at the stream's current position, never goes back, never calls `Seek`, `Length` or `Position`, and never
  closes the stream. It reads with a loop until the bytes it asked for arrive or `Read` returns 0 (a short read is
  normal; 0 before the end of what it needs is the end of the stream, giving null).
- The cap: it consumes at most 262,144 bytes (256 KiB), counting every byte read or skipped from the start position
  (markers, lengths and skipped payloads included). A segment that would end past the cap gives null; a segment that
  ends exactly at the cap is allowed.
- The JPEG walk: the first two bytes must be `FF D8`, else null. Then, repeatedly: a marker is `FF` followed by any number
  of further `FF` fill bytes and then the marker code; a byte other than `FF` where a marker must start, or the code
  `00`, gives null. The codes `01`, `D0` to `D8` have no length and are skipped. `D9` (EOI) and `DA` (SOS) stop the walk
  with null (entropy-coded data cannot be skipped by length). Any other marker has a 2-byte big-endian length L that
  counts itself: L below 2 gives null (the walk never stands still or goes back); the payload is L - 2 bytes. Only a
  payload of an APP1 segment starting with `Exif\0\0` is read into memory (a buffer of at most 65,533 bytes, sized from
  the segment length and from no other length in the file); every other payload is skipped. The first Exif segment
  decides: its result, a date or null, is final.
- The TIFF part (offsets count from the byte after `Exif\0\0`, "TIFF data"; all of them are 32-bit unsigned numbers,
  checked in `long` arithmetic so `0xFFFFFFFF` cannot wrap): the byte order is `II` or `MM`, else null; the next 2
  bytes are 42, else null; the IFD0 offset is at least 8 and the IFD it names (a 2-byte entry count, then 12 bytes per
  entry) fits in the TIFF data, else null. The reader visits only IFD0 and the one Exif IFD named by the pointer; it never
  follows a next-IFD link; when a tag appears twice in an IFD the first entry wins; it reads at most `entry count` entries (up to 65,535), each inside the TIFF data. The Exif
  pointer is tag 0x8769 of type LONG (4) with count 1; its offset is at least 8, differs from the IFD0 offset, and the Exif
  IFD fits in the TIFF data. DateTimeOriginal is tag 0x9003 of type ASCII (2) with a count of at least 19; its value is an
  offset (a count above 4 is never stored in the entry) with offset at least 8 and offset + count within the TIFF data.
- The text: the first 19 bytes are `yyyy:MM:dd HH:mm:ss` with ASCII digits `0` to `9` only, `:` and one space in the
  fixed places, and a real calendar date and time; any byte above 0x7F or any other character in them gives null. The
  bytes from the 20th to the end of the count must each be NUL or a space, else null.
1. Given a JPEG made of the start marker, one APP1 segment with `Exif\0\0`, a little-endian TIFF header (`II`), an IFD0
   holding the Exif pointer (tag 0x8769) and an Exif IFD holding DateTimeOriginal `2023:07:01 10:00:00`, then the result
   is 2023-07-01 10:00:00. The same file built big-endian (`MM`) gives the same date. A count of 19 (no NUL) and of 20 (one
   NUL) both work.
2. Given a JPEG with an APP0 (JFIF) segment before the Exif APP1, and another APP1 segment that is not Exif (an XMP header)
   before it, then the date is still found. Given RST markers, `FF` fill bytes between segments, and a repeated `FF D8`
   before the Exif segment, then the date is found.
3. Given a JPEG without an APP1 Exif segment, or with an Exif block that has no tag 0x9003 (only DateTime 0x0132 in
   IFD0), then the result is null (only DateTimeOriginal counts). Given an SOS (`FF DA`) or EOI (`FF D9`) marker before
   the Exif segment, then null.
4. Given the stored text `0000:00:00 00:00:00`, an empty text, `2023:13:40 10:00:00`, `2023:02:30 10:00:00`, `2023-07-01
   10:00:00`, a text with a byte above 0x7F (`2023:07:01 10:00:0\xB2`) or with Arabic-Indic digits, or a non-NUL, non-space
   byte after the 19th, then the result is null. Trailing NUL or spaces are accepted.
5. Given the marker walk: a stream not starting with `FF D8`; a segment length of 0 or 1 (must not loop or go backwards); a
   first byte other than `FF` where a marker must start; the marker code `00`; a segment whose length runs past the end
   of the stream; then each gives null at once.
6. Given the TIFF header: a bad byte order (`XX`), a magic other than 42, an IFD0 offset of 0, 4, 7 or past the TIFF data,
   an Exif pointer count of 0 or 2, an Exif IFD offset of 0, 4 or 7, an APP1 payload shorter than 6 bytes as the
   only APP1 segment, and `Exif\0` cut short, then each gives null.
7. Given 32-bit values: the DateTimeOriginal count `0xFFFFFFFF`, `0x80000000` and 18; its offset `0xFFFFFFFF` and
   `0xFFFFFFF0`; an offset plus count that lies one byte past the TIFF data; the Exif pointer value `0xFFFFFFFF`; a
   0x9003 entry of type SHORT (3) or with count 0; a 0x8769 entry of type ASCII; then each gives null, with no exception
   and no large allocation.
8. Given loops: an Exif pointer equal to the IFD0 offset, a next-IFD link that points back at IFD0, and an entry count of
   65,535 on a segment of a few hundred bytes, then each gives null at once.
9. Given the cap: a JPEG with filler APP2 segments (65,533 payload bytes each, one smaller to adjust) so that the valid
   Exif segment ends exactly at byte 262,144, then the date is found; one byte later, then null; a valid Exif segment
   after 16 filler segments (about 1 MiB), then null. A counting wrapper shows that no call consumed more than 262,144
   bytes from the start position.
10. Given an endless stream (a wrapper that returns `FF` forever, and another that repeats empty APP1 segments `FF E1 00
    02` forever), then the result is null and the wrapper served at most 262,144 bytes. A test that would hang fails by
    timeout, not by hanging the run: the wrapper itself throws once it has served more than 1 MiB.
11. Given short reads, a wrapper returning one byte per `Read` call gives the same date as criterion 1 and 2; a wrapper that
    returns 0 (the end) before the segment is complete gives null without an exception.
12. Given a stream that cannot seek and has no `Length` (a wrapper whose `CanSeek` is false and whose `Length` and
    `Position` throw `NotSupportedException`), then criteria 1 and 2 still give the date.
13. Given a stream with 10 junk bytes in front and `Position` set to 10, then the date is found; after the call, `CanRead`
    is still true (not closed) and the position is not more than 262,144 bytes past the start. Given the same stream
    rewound and read twice, then both calls return the same date (the reader keeps no state).
14. Given a valid file cut at every length from 0 to its full length, then every length shorter than the end of the Exif
    APP1 segment gives null and every longer or equal length gives the date (one test loops, big- and little-endian).
15. Given an empty stream, a PNG header, plain text, and 1,000 random bytes (fixed seed), then null and no exception.
16. Given a valid file (both byte orders), then for every byte position, replacing that byte with `00`, with `FF` and
    with `80` gives null or a date with no exception (fixed list, no randomness); and 5,000 mutations of 1 to 4 random bytes
    with a fixed seed do the same. The loops finish in a few seconds.

17. Given duplicate entries, the first one in the IFD wins and later ones are ignored: two 0x8769 entries, the first
    pointing at an Exif IFD with the date `2023:07:01 10:00:00` and the second at one with another date, then the first date;
    two 0x9003 entries with two dates, then the first; a first 0x9003 entry that is invalid (type SHORT) followed by a valid
    one, then null (the reader does not search on).
18. Given the promises of the preamble: a null stream throws `ArgumentNullException`; the returned date has
    `Kind == DateTimeKind.Unspecified` and the exact clock values stored; a stream whose `Read` throws an `IOException`
    makes `Read` throw that same exception (it is not caught, and not turned into null).

## Out of scope
- Using the date in a rule (NS-62), reading it from the disk in the window, any cache.
- RAW, HEIC, PNG, TIFF and video metadata; the tags DateTimeDigitized and DateTime; time zone offsets.
- Writing EXIF.

## Decisions
- New dependency? **Decision:** none. A reader for the one tag we need is a few dozen lines, testable on every runtime
  with byte arrays, and `System.Drawing` cannot be used in `NameShift.Core` (netstandard2.0, no UI dependency) (po,
  2026-10-24).
- Which tag? **Decision:** DateTimeOriginal only, the moment the photo was taken; the other date tags are often the
  edit time (po, 2026-10-24).
- Bad file? **Decision:** null, never an exception: a damaged photo must not stop a preview (po, 2026-10-18; the shape of
  the API 2026-10-24).
- Untrusted bytes? **Decision:** every length and offset is checked before use, nothing is allocated from a number in
  the file except a JPEG segment length, and only two IFDs are visited, so a hostile file cannot hang or exhaust the app
  (po, 2026-10-24, from the PR #39 review).
- Fallback or error when there is no date? **Decision:** moved to NS-62, where the rule uses the date; the first answer
  (fall back to the modified date and mark it) stays the leading option (po, 2026-10-24).

## Log
- 2026-10-18: created as "Auto date rule: photo (EXIF) date" (po).
- 2026-10-24: re-scoped to the reader only; the rule part moved to NS-62; no new dependency needed, set Ready (po).
- 2026-10-24: bounds, loops, cap, short reads and fuzz criteria added after the PR #39 review (po).
- 2026-10-24: In Progress (builder).
