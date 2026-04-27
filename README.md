# editorconfig-fix

`editorconfig-fix` applies selected EditorConfig settings to one specified file.

```pwsh
editorconfig-fix file-path [options]
```

The tool is intentionally not recursive. To process many files, invoke it from shell features such as `git ls-files`, `find`, `fd`, or PowerShell pipelines.

## Options

- `--any-file`: allow settings that come only from a matching `[*]` section.
- `--force`: attempt files detected as binary, while still requiring UTF-8 decoding before any write.
- `--git-root`: stop looking for `.editorconfig` files in parent directories when a git repository root is reached.
- `--dry-run`: report whether changes would be made without writing the file.
- `--verify`: like `--dry-run`, but returns exit code `1` if a change would be made.
- `--fix-all`: apply all fix options.
- `--end-of-line`: apply `end_of_line`.
- `--strip-bom`: remove a UTF-8 BOM when `charset = utf-8`.
- `--trailing-whitespace`: apply `trim_trailing_whitespace = true`.
- `--final-newline`: apply `insert_final_newline = true` to non-empty files.

At least one fix option must be specified.

## Encoding Scope

The first version supports only UTF-8 and UTF-8 BOM files. Files with invalid UTF-8 bytes or an explicit non-UTF-8 `charset` are skipped unless `--force` is supplied. With `--force`, the tool still fails rather than writing a file it cannot decode safely.

`insert_final_newline = false` and indentation settings are out of scope for this version.
