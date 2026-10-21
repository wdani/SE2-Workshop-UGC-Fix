# SE2 Workshop UGC Fix v0.3.0 (source preview, pending Windows build)

- Added independent **DESCRIPTION PREFLIGHT** modal: paste planned text and estimate conditional future developer metadata payload size using UTF-8 and two JSON string-encoding models.
- Reuses last scanned Steam UGC JSON as a template when available; otherwise marks 1,479-byte known-case baseline as a *reference only*.
- Warning thresholds: projected conservative payload >=4,500 bytes; danger >=5,000 bytes. Never claims public description alone triggers failure.
- Read-only estimator: no Steam queries, uploads, publishing, saved pasted descriptions or mutation of scanned originals.
- Added 7 C# test cases for the preflight behavior, including long ASCII, Unicode escapes, existing description and ambiguous/invalid templates. Existing metadata repair safety tests unchanged.
- Added a controlled A/B reproduction guide, separate public Steam website vs ModHUB import/update procedures, with ready-made test descriptions and strict use of a disposable test item only.
- No changes to Steam metadata repair methods, account ownership, consent prompts, backups or post-submit verification.
- Windows build and GUI runtime still need validation on owner PC; don't distribute to others as tested binary before that.
