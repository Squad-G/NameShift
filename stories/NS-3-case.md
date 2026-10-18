# NS-3: Case rule

Status: In Review
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
- What is a word? **Decision:** a run of letters, digits, combining marks and apostrophes; anything else separates words, so `my_holiday-photo` gives `My_Holiday-Photo` and `don't` gives `Don't` (tech-lead, 2026-10-18).
- Where does a sentence start? **Decision:** at the start of the name and after a `.` followed by whitespace; the first letter-or-digit of the sentence is capitalised if it is a letter, so `2nd try` stays `2nd try` (tech-lead, 2026-10-18).
- How are exceptions matched? **Decision:** whole words, ignoring case (ordinal); written as listed; the first listed form wins on duplicates (tech-lead, 2026-10-18).
- Do exceptions apply outside title mode? **Decision:** no, the exceptions list applies in title mode only (po, 2026-10-18).
- A listed exception as the first word? **Decision:** an exception that contains at least one upper-case letter (`iPhone`, `USA`) is always written exactly as listed, even as the first word (`iphone photos` with `iPhone` gives `iPhone Photos`); an all-lower-case exception (`the`) is still capitalised as the first word (`the lord of the usa` with `the, of, USA` gives `The Lord of the USA`) (po, 2026-10-18).
- What ends a sentence in sentence mode? **Decision:** only `. ` (a period followed by whitespace); `!` and `?` do not (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-3-case (builder).
- 2026-10-18: In Review, PR opened (builder).
