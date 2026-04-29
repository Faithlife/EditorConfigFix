# editorconfig-fix

`editorconfig-fix` applies selected EditorConfig settings to one specified file.

```pwsh
editorconfig-fix file-path [options]
```

The tool is intentionally not recursive. To process many files, invoke it from recursive shell features.

## Options

At least one fix option must be specified.

### Fix Options

- `--all`: apply all fix options.
- `--eol`: apply `end_of_line`.
- `--bom`: remove a UTF-8 BOM when `charset = utf-8`.
- `--trim`: apply `trim_trailing_whitespace = true`.
- `--final`: apply `insert_final_newline = true` to non-empty files.

### Matching Options

- `--any`: allow settings that come only from a matching `[*]` section.
- `--git-root`: stop looking for `.editorconfig` files in parent directories when a git repository root is reached.

### Reporting Options

- `--dry-run`: report whether changes would be made without writing the file.
- `--verify`: like `--dry-run`, but returns exit code `1` if a change would be made.

## Encoding Support

Only UTF-8 and UTF-8-BOM are supported. Files with invalid UTF-8 bytes or an explicit non-UTF-8 `charset` are skipped.
