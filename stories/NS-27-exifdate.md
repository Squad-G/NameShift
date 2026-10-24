# NS-27: Read the photo (EXIF) date from a JPEG

Status: Ready
Depends on: NS-1
Area: engine

## Why
Name photos by the date they were taken. This story only reads the date from a file's bytes; using it in the Auto date
rule is NS-62.

## Acceptance criteria
A public static class `PhotoDate` in `NameShift.Core` with `DateTime? Read(Stream stream)`: the date and time the
camera stored as DateTimeOriginal (EXIF tag 0x9003), or null. It reads from the stream's current position, only
forward, so it also works on a stream that cannot seek; it never reads more than the first 256 KiB; it does not close
the stream. It is a small reader of the JPEG and EXIF byte layout written for NameShift, with no new package and no
`System.Drawing` (Core has no UI dependency). The date is returned as stored: the camera's clock, no time zone
conversion, `DateTimeKind.Unspecified`. Bad data (a damaged, truncated, odd or non-JPEG stream) always gives null,
never an exception; an `IOException` raised by the stream itself is not caught (the caller handles disk errors).
The tests build their JPEG bytes in a small helper (no binary fixture files).
1. Given a JPEG made of the start marker, one APP1 segment with `Exif\0\0`, a little-endian TIFF header (`II`), an IFD0
   holding the Exif pointer (tag 0x8769) and an Exif IFD holding DateTimeOriginal `2023:07:01 10:00:00`, then the result
   is 2023-07-01 10:00:00. The same file built big-endian (`MM`) gives the same date.
2. Given a JPEG with an APP0 (JFIF) segment before the Exif APP1, and another APP1 segment that is not Exif (an XMP
   header) before it, then the date is still found.
3. Given a JPEG without an APP1 Exif segment, or with an Exif block that has no tag 0x9003 (only DateTime 0x0132 in IFD0),
   then the result is null (only DateTimeOriginal counts).
4. Given the stored text `0000:00:00 00:00:00`, an empty text, `2023:13:40 10:00:00` or any text that is not
   `yyyy:MM:dd HH:mm:ss`, then the result is null. Trailing NUL or space characters after the 19 characters are ignored.
5. Given an empty stream, a PNG header, plain text, or a stream of 1,000 random bytes (fixed seed), then the result is
   null and nothing is thrown.
6. Given a valid file cut at every length from 0 to its full length, then each call returns null or the date and never
   throws (one test loops).
7. Given offsets that point outside the segment or the stream (the Exif pointer, the value offset, an entry count larger
   than the segment), then the result is null, with no exception and no endless loop.
8. Given a stream that cannot seek (a wrapper stream), then criteria 1 and 2 still give the date.
9. Given a JPEG with 16 filler APP2 segments of 65,533 bytes each (about 1 MiB) before its Exif segment, then the result
   is null and a counting wrapper shows at most 256 KiB were read.
10. Given the same stream passed twice after being rewound, then both calls return the same date (the reader keeps no
    state); given a null stream, then it throws `ArgumentNullException`.

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
- Fallback or error when there is no date? **Decision:** moved to NS-62, where the rule uses the date; the first answer
  (fall back to the modified date and mark it) stays the leading option (po, 2026-10-24).

## Log
- 2026-10-18: created as "Auto date rule: photo (EXIF) date" (po).
- 2026-10-24: re-scoped to the reader only; the rule part moved to NS-62; no new dependency needed, set Ready (po).
