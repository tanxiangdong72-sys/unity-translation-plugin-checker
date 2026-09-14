# Asset Store release-candidate gates

Status: NOT READY FOR SUBMISSION. This is a release-preparation branch, not an approved product.

## Required gates
- [x] Unity 6000.0.32f1 clean local UPM resolution, compile and EditMode regression tests.
- [x] Unity 6000.5.10f1 regression tests for the same candidate package.
- [x] Implement saved locale-to-font profiles, window editing and scan request integration.
- [ ] Interactive font workflow acceptance (create/edit/Undo/save/reopen/scan). Batch tests do not exercise window drawing.
- [ ] Test install, update and removal using the exact distributable, not a copied Editor folder.
- [ ] English quick start, dependency matrix, limitations, changelog and license review.
- [ ] Include clearly marked original synthetic examples; no third-party game projects, logs, auto-run preview scripts or user data in the product.
- [ ] Verify all files/folders have unique .meta GUIDs.
- [ ] Run official Asset Store Publishing Tools validation.
- [ ] Review screenshots, feature claims, third-party notices and applicable AI-content disclosures against current submission rules.
- [ ] Reconcile remote GitHub main with local history before publishing. Never overwrite remote main from this local branch without comparison.

## Confirmed baseline findings (2026-09-13)
- Local HEAD 1286807; package declares Unity 6000.0 but TmpFontAssetResolver uses EntityId/GetEntityId unavailable in 6000.0.32f1.
- Release branch replaces API-dependent identity sets with TMP_FontAsset object sets; adds same-name/different-object regression coverage. Existing tests cover cycles and duplicates.
- Metroidvania demo was a modified source-copy integration, not final package acceptance.
- The three synthetic reference defects are three cases of one rule, not coverage of all rules.

## Validation paths
- Candidate checkout: E:/unity-release-candidate
- Clean Unity 6.0 project: E:/unity-rc-validation-6000
- Test log/result: E:/unity-rc-validation-6000/Logs/EditMode.log and EditMode.xml

## Official references
- https://marketplace.unity.com/publishing/submission-guidelines
- https://docs.unity.com/en-us/asset-store/publishing/asset-packages/upload

## 2026-09-14 executed validation
Both clean projects resolved the candidate via a local UPM file dependency with testables enabled, not by copying the Editor directory.
- Unity 6000.0.32f1: 172 total, 171 passed, 0 failed, 1 ignored (graphics-only smoke test in batch mode). Result: E:/unity-rc-validation-6000/Logs/EditMode-run2.xml.
- Unity 6000.5.10f1: 172 total, 171 passed, 0 failed, 1 ignored (graphics-only smoke test in batch mode). Result: E:/unity-rc-validation-6500/Logs/EditMode.xml.
- Initial Unity 6.0 test compilation exposed Scene.handle.GetRawData calls on int. A test-only int extension now preserves all 32 handle bits; newer native instance methods take precedence. Original test coverage was not disabled.
- Package metadata preflight: 0 missing .meta files, 0 duplicate GUID groups.
- Added package-level English README with explicit rule scope and font-mapping limitation.
These baseline results are not official Asset Store validator approval, GUI acceptance or final distributable installation/update/removal verification.

## Font profile candidate validation (2026-09-14)
- Added AuditFontProfile and window create/select/edit/save workflow; configured paths now reach AuditRequest rather than an always-empty dictionary.
- Seven new tests cover blank/unassigned rows, duplicate codes, missing code, unsaved font, save/reimport, mapping snapshots and window-state request integration/reset.
- First regression exposed a test fixture issue: Unity drops transient object references when serializing a persistent asset. The unsaved-font guard test now uses a transient profile and exercises the intended invalid reference. No test disabled.
- Final Unity 6000.0.32f1: 179 total, 178 passed, 0 failed, 1 skipped. Evidence: E:/unity-rc-validation-6000/Logs/FontProfile-final.xml.
- Final Unity 6000.5.10f1: 179 total, 178 passed, 0 failed, 1 skipped. Evidence: E:/unity-rc-validation-6500/Logs/FontProfile-final.xml.
- Both final runs use the candidate after alignment with remote main bba4b95672f5617b0d4832d45c5b5e662644be45. Preserve remote metadata and existing source formatting/test-message differences; do not replace remote-only documentation.
- Distribution target is release/asset-store-readiness, not a force-update of main. Publication is to be verified via the GitHub API.
- Not claimed: GUI acceptance, final Git/Asset Store distributable install/update/removal, complete gameplay verification, marketplace approval or a finalized license.
