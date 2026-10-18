# NS-3: Case rule

Status: In Progress
Depends on: NS-1
Area: engine

## Why
Change the capitalisation of names.

## Acceptance criteria
1. Given `IMG_001.JPG`, mode lower, then `img_001.JPG` (extension untouched).
2. Given `my file.txt`, mode upper, then `MY FILE.txt`.
3. Given `the lord of the rings.txt`, mode title, then `The Lord Of The Rings.txt`.
4. Given `HELLO world. second SENTENCE.txt`, mode sentence, then `Hello world. Second sentence.txt` (only the first letter of the name and the first letter after `. ` are capitalised; the rest is lower-case).
5. Given mode same, then the name is unchanged.
6. Given an exceptions list `of, the, USA` and mode title, then `the lord of the usa.txt` becomes `The Lord of the USA.txt` (an exception word is written exactly as listed, but the first word of the name is always capitalised, even when it is listed, and a listed word that is already all upper-case such as `USA` is kept as listed).
7. Given accented letters, `ÉCOLE.txt` lower gives `école.txt` (culture-invariant Unicode casing, never the user's locale, so Turkish `I` does not become `ı`).

## Out of scope
- Anything not listed above.

## Decisions
- Locale-aware casing? **Decision:** no, invariant casing, so the same batch gives the same result on every PC (po, 2026-10-18).
- Is the extension changed? **Decision:** no, that is the Extension rule's job (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-3-case (builder).
