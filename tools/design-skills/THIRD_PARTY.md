# Design tools: source and licenses

These are optional development tools, not frontend/runtime dependencies. Their installed code lives in ignored `.cursor/skills/` and `.cursor/agents/impeccable-*.md`; CookShare does not redistribute that installed code in Git or application publish output.

| Tool | Reviewed source | License |
| --- | --- | --- |
| Taste Skill (`design-taste-frontend`) | [Leonxlnx/taste-skill, commit 6010f91a9935f1c167dfc500fb1a475681892e6b](https://github.com/Leonxlnx/taste-skill/tree/6010f91a9935f1c167dfc500fb1a475681892e6b/skills/taste-skill) | [MIT](licenses/Taste-MIT.txt), copyright 2026 Leonxlnx |
| Impeccable skill 4.7.0 / engine 0.1.16 | [pbakaus/impeccable, skill-v4.7.0](https://github.com/pbakaus/impeccable/releases/tag/skill-v4.7.0); installer source inspected at [bdeeeec723426d4dc2945e420e849e98abe6cda2](https://github.com/pbakaus/impeccable/tree/bdeeeec723426d4dc2945e420e849e98abe6cda2) | [Apache 2.0](licenses/Impeccable-Apache-2.0.txt), [upstream notice](licenses/Impeccable-NOTICE.md) |

The lock records the exact Taste commit, Impeccable release-bundle SHA-256, and normalized hashes of installed text files. The npm launcher version and the downloaded skill version are separate; pinning npm alone would not pin the skill bundle. Setup supplies the checked release ZIP through `IMPECCABLE_BUNDLE_PATH`.

Original license/notice text is copied beside each local skill. No photos, fonts, icons, examples or generated output from these tools are added to CookShare by setup. If future work copies third-party assets/code into the application, review each asset's own license and retain its notices before redistribution; the tool's license does not license every asset it can discover.
