# editorconfig-fix CLI

## Status

Proposed.

## Purpose

Create a .NET command-line tool named `editorconfig-fix` that applies selected EditorConfig settings to one specified file.

The tool is intentionally file-scoped. It does not walk directories or expand globs; callers that want recursive behavior can use their shell, `find`, `fd`, `git ls-files`, or similar tools to invoke it once per file.

## Goals

- Package a .NET tool with the command name `editorconfig-fix`.
- Accept exactly one file path argument and a set of explicit fix options.
- Use the `editorconfig` NuGet package to resolve settings for the specified file.
- Leave the file untouched when the selected settings already comply.
- Leave the file untouched when no selected setting is applicable.
- Support dry-run and verify modes without modifying the file.
- Avoid changing binary files unless `--force` is specified.
- Keep behavior deterministic, idempotent, and easy to test with temporary files and repositories.

## Non-Goals

- Recursively processing directories.
- Formatting language syntax or performing language-aware indentation.
- Applying every EditorConfig property.
- Rewriting `.editorconfig` files.
- Adding interactive prompts.
- Restoring removed trailing whitespace or final newlines when the EditorConfig property is absent.

## Repository Shape

Follow the RepoConventions project shape:

- `EditorConfigFix.slnx`
- `Directory.Build.props`
- `Directory.Packages.props`
- `global.json`
- `src/EditorConfigFix/EditorConfigFix.csproj`
- `tests/EditorConfigFix.Tests/EditorConfigFix.Tests.csproj`
- `README.md`
- `ReleaseNotes.md`
- `build.ps1`

Project expectations:

- Use `System.CommandLine` for parsing.
- Reference `editorconfig` for EditorConfig parsing and matching support.
- Use NUnit for tests.
- Enable nullable reference types, implicit usings, analyzers, warnings as errors, and central package management.
- Pack `src/EditorConfigFix` as a .NET tool with `AssemblyName` set to `editorconfig-fix`.
- Target the same supported frameworks as RepoConventions unless a package constraint requires narrowing them.

## CLI Surface

```pwsh
editorconfig-fix file-path [options]
```

Options:

- `--any-file`: allow settings that come only from a matching `[*]` section.
- `--force`: allow files detected as binary to be processed.
- `--git-root`: stop searching parent directories for `.editorconfig` when a git repository root is reached.
- `--dry-run`: report whether changes would be made, but do not write the file.
- `--verify`: like `--dry-run`, but return exit code `1` if any selected fix would change the file.
- `--indent`: apply `indent_style` and `indent_size` behavior.
- `--end-of-line`: apply `end_of_line` behavior.
- `--strip-bom`: remove a UTF-8 BOM when `charset` resolves to `utf-8`.
- `--trailing-whitespace`: apply `trim_trailing_whitespace` behavior.
- `--final-newline`: apply `insert_final_newline` behavior.

Validation:

- `file-path` is required and must name an existing file.
- Directories are rejected.
- At least one fix option must be specified: `--indent`, `--end-of-line`, `--strip-bom`, `--trailing-whitespace`, or `--final-newline`.
- `--verify` implies dry-run behavior. If both `--dry-run` and `--verify` are supplied, use verify exit semantics.

## Exit Codes

- `0`: completed successfully. This includes already-compliant files, skipped files, successful writes, and dry-run runs where changes would be made.
- `1`: `--verify` completed successfully and at least one change would be made.
- Non-zero other than `1`: invalid command line, missing file, decode failure, I/O failure, or an unexpected processing error.

## EditorConfig Resolution

Use `EditorConfig.Core.EditorConfigParser` from the `editorconfig` package for final setting resolution.

The package exposes:

- `EditorConfigParser.Parse(fileName, editorConfigFiles)` for resolving a file against a supplied list of parsed `.editorconfig` files.
- `EditorConfigFile.Parse(path)` for parsing individual files.
- `FileConfiguration` properties for `Charset`, `EndOfLine`, `IndentStyle`, `IndentSize`, `TabWidth`, `TrimTrailingWhitespace`, and `InsertFinalNewline`.
- `GlobMatcher` and `GlobMatcherOptions` for matching section globs when the tool needs section-level metadata.

Do not rely directly on `EditorConfigParser.GetConfigurationFilesTillRoot` because `--git-root` adds a stopping rule that the package does not implement. Instead:

1. Resolve `file-path` to a full path.
2. Starting at the file's directory, walk parent directories.
3. Collect existing `.editorconfig` files.
4. Stop when an encountered `.editorconfig` has `root = true`.
5. If `--git-root` is set, also stop after checking the directory that contains `.git` as a directory or file.
6. Reverse the collected files so they are passed to `EditorConfigParser.Parse` from outermost to innermost.

## `--any-file` Behavior

By default, a file should not be affected when it is matched only by `[*]` sections.

Implementation:

- While resolving settings, also compute the set of matching `ConfigSection` entries from the discovered `.editorconfig` files.
- Reuse the package's matching behavior with `GlobMatcherOptions { MatchBase = true, Dot = true, AllowWindowsPaths = true }`.
- Treat a matching section with `Glob` exactly equal to `*` as the catch-all section.
- Without `--any-file`, skip the file unless at least one matching section has a glob other than `*`.
- With `--any-file`, allow settings from `[*]` alone to drive fixes.

This means a file matched by both `[*]` and `[*.cs]` is eligible without `--any-file`, and the resolved combined configuration still decides the selected settings.

## Binary File Detection

Binary detection should happen after EditorConfig resolution and before text decoding or transformation.

Proposed heuristic:

- Read the first 8 KiB of the file.
- Treat the file as text if it starts with a known text BOM: UTF-8, UTF-16 LE, or UTF-16 BE.
- Otherwise, classify the file as binary if the sampled bytes contain `0x00`.
- Otherwise, classify the file as text.

Without `--force`, binary files are skipped and left untouched. With `--force`, the binary check is bypassed, but text transformations still require the file to be decoded successfully according to the configured or detected encoding. `--force` should not silently corrupt undecodable data.

## Encoding Model

Represent the file as `OriginalFileBytes` plus decoded text and encoding metadata.

Encoding selection:

- If `charset` is `utf-8`, decode as UTF-8 and write without adding a BOM unless the original BOM is intentionally preserved by a non-`--strip-bom` run.
- If `charset` is `utf-8-bom`, decode as UTF-8 and preserve an existing UTF-8 BOM. Do not add a BOM unless charset writing is explicitly added in a future feature.
- If `charset` is `utf-16le` or `utf-16be`, decode with the corresponding Unicode encoding.
- If `charset` is `latin1`, decode as ISO-8859-1.
- If `charset` is absent, detect UTF BOMs first, otherwise decode as strict UTF-8.

Use strict decoders where available. If decoding fails, return a clear error instead of writing a guessed transformation.

Do not treat this tool as a general charset fixer. Except for `--strip-bom`, preserve the original encoding shape as much as possible.

## Transform Semantics

Each selected fix should be a pure transformation from the original decoded text and resolved settings to candidate output bytes. The final write happens only when candidate bytes differ from the original bytes.

### `--strip-bom`

- Only acts when `FileConfiguration.Charset` resolves to `UTF8`.
- If the original file begins with a UTF-8 BOM, remove it.
- If charset is absent, `UTF8BOM`, or another charset, do nothing.

### `--end-of-line`

- Only acts when `end_of_line` is present.
- Normalize all line break sequences to the resolved value: `lf`, `crlf`, or `cr`.
- Treat existing `\r\n`, lone `\n`, and lone `\r` as line breaks.
- Preserve whether the file ended with a line break unless `--final-newline` is also selected.

### `--trailing-whitespace`

- Only acts when `trim_trailing_whitespace` resolves to `true`.
- Remove trailing horizontal whitespace before each line break and at end of file.
- Limit the first implementation to spaces and tabs unless tests demonstrate a need for broader Unicode whitespace.
- If the property resolves to `false` or is absent, do nothing.

### `--final-newline`

- Only acts when `insert_final_newline` is present.
- When true, ensure the file ends with exactly one final line break.
- When false, remove all terminal line break sequences.
- Use the configured `end_of_line` value when available; otherwise use the first existing line ending in the file, falling back to `Environment.NewLine` only when the file has no line endings.

### `--indent`

- Acts when `indent_style` is present.
- Normalize only leading whitespace before the first non-whitespace character on each line.
- Do not attempt language-aware reindentation or syntax parsing.
- Use visual-column conversion so existing indentation depth is preserved as closely as possible.
- For `indent_style = space`, emit spaces for leading indentation. Use numeric `indent_size` when present; if `indent_size = tab`, use `tab_width`; if neither is available, default to `4`.
- For `indent_style = tab`, emit tabs for whole indentation units and spaces for any remainder. Use `tab_width` when present; otherwise use numeric `indent_size`; otherwise default to `4`.
- Blank lines should be handled by the trailing-whitespace option, not by indentation normalization.

Because indentation cannot be made fully semantic without language parsers, document the behavior as indentation-character normalization rather than code formatting.

## Write Behavior

- Compute candidate bytes in memory.
- If candidate bytes are byte-for-byte identical to the original bytes, do not open the file for writing.
- In dry-run and verify modes, do not open the file for writing.
- For real writes, write to a temporary file in the same directory and then replace the original file.
- Preserve file attributes where practical.
- Let file system errors surface with clear messages.

## Console Output

Keep output short and script-friendly.

Suggested messages:

- `unchanged <path>` when selected fixes produce no byte changes.
- `changed <path>` after writing.
- `would change <path>` for `--dry-run` or `--verify` when candidate bytes differ.
- `skipped <path>: binary file` when skipped because `--force` was not supplied.
- `skipped <path>: only [*] matched` when skipped because `--any-file` was not supplied.
- `skipped <path>: no selected settings apply` when the requested fix options have no corresponding resolved EditorConfig settings.

Avoid verbose per-line reporting in the first version.

## Suggested Internal Shape

- `Program`: builds the `System.CommandLine` root command and invokes the runner.
- `FixOptions`: immutable option model created by the CLI handler.
- `EditorConfigResolver`: discovers `.editorconfig` files, applies `--git-root`, parses settings, and computes matching sections.
- `ResolvedEditorConfig`: contains `FileConfiguration`, matching section metadata, and skip reasons.
- `BinaryFileDetector`: implements the sampled NUL-byte heuristic.
- `TextFileLoader`: handles BOM detection, charset-aware decoding, and encoding metadata.
- `EditorConfigFixer`: orchestrates eligibility checks and transformations.
- `TextTransforms`: pure helpers for line endings, trailing whitespace, final newline, indentation, and BOM handling.
- `FileWriter`: handles dry-run, verify, byte comparison, and atomic replacement.

Keep the transformation helpers independent of command-line parsing and file-system writes so tests can cover most behavior without spawning the tool.

## Test Plan

Use temporary directories and files, with integration-style tests where file system behavior matters.

CLI validation:

- Missing file path fails.
- Directory path fails.
- No fix option fails.
- Unknown options fail through `System.CommandLine`.

EditorConfig resolution:

- Nested `.editorconfig` files are applied from outermost to innermost.
- `root = true` stops parent discovery.
- `--git-root` stops parent discovery at a directory containing `.git`.
- Settings are not applied when only `[*]` matched and `--any-file` is absent.
- Settings are applied from `[*]` when `--any-file` is present.
- Settings are applied when both `[*]` and a more specific section match.

Binary handling:

- File with sampled NUL bytes is skipped without `--force`.
- Same file is attempted with `--force`.
- UTF-8 and UTF-16 BOM text files are not misclassified as binary.

Transform behavior:

- `--strip-bom` removes a UTF-8 BOM only when charset is `utf-8`.
- `--end-of-line` converts LF, CRLF, and CR to the configured style.
- `--trailing-whitespace` removes spaces and tabs before line breaks and EOF only when configured true.
- `--final-newline` handles both true and false.
- `--indent` converts leading tabs to spaces and leading spaces to tabs according to resolved settings.
- Multiple selected fixes compose deterministically.

No-touch guarantees:

- Already-compliant file keeps the same contents and write timestamp.
- Skipped binary file keeps the same contents and write timestamp.
- Skipped `[*]`-only file keeps the same contents and write timestamp.
- Dry-run and verify keep the same contents and write timestamp.

Exit behavior:

- `--dry-run` returns `0` whether or not a change would be made.
- `--verify` returns `0` when no change would be made.
- `--verify` returns `1` when a change would be made.

## Documentation Changes

- Add README installation and usage examples.
- Document that the tool processes exactly one file per invocation.
- Document `--any-file`, especially the default skip for files matched only by `[*]`.
- Document binary detection and `--force`.
- Document verify-mode exit code `1`.
- Document the limited, non-language-aware indentation behavior.

## Suggested Delivery Order

1. Scaffold the solution, projects, package references, build script, README, and release notes.
2. Implement command-line parsing and validation with stubbed runner behavior.
3. Implement EditorConfig discovery, `--git-root`, setting resolution, and `--any-file` eligibility.
4. Implement binary detection, text loading, and no-touch byte comparison.
5. Implement `--strip-bom`, `--end-of-line`, `--trailing-whitespace`, and `--final-newline`.
6. Implement `--indent` after the line-based transforms are stable.
7. Add integration tests for dry-run, verify, atomic writes, and timestamp preservation.
8. Fill in README examples and run the full build.

## Questions And Concerns

- `indent_size` cannot be made fully semantic without language-specific parsers. The first version should document and test visual-column normalization rather than promising formatter-like reindentation.
- UTF-16 files without a BOM may be classified as binary by the NUL-byte heuristic unless `--force` is used or `charset` is explicitly configured. This should be documented if the heuristic remains simple.
- `insert_final_newline = false` is uncommon but supported by the plan; confirm that removing all terminal line breaks is the desired interpretation before implementation.