# NLEvent Language Spec — v0.1

Status: prototype / draft. This is the first, deliberately small implementation of the
"NLEvents" idea described in [`nl.txt`](../nl.txt) (section 3). The real vision describes a
much richer, streamer-authored rules language; v0.1 exists only to prove the core mechanic —
*a streamer writes rules, and events get allowed/blocked/warned accordingly* — end to end.

This is **not** Python/JSON/etc. It's a tiny, purpose-built, line-oriented DSL that only
understands the handful of constructs below. Anything else is a parse error.

## File extension

`.nle` (NLEvent config).

## Lexical rules

- Indentation-based blocks, like Python: a line ending in `:` opens a block; subsequent lines
  indented deeper belong to it; dedenting closes it. Indentation must be spaces (tabs are a
  lexer error in v0.1, to keep the implementation simple).
- Comments start with `#` and run to end of line.
- Blank lines are ignored.
- String literals use double quotes: `"stay within the zone"`.
- Numbers are integers or decimals: `0`, `100`, `3.5`.
- Identifiers: letters, digits, `_`, and `.` (for property access like `player.health`).

## Grammar (informal)

```
config          := { hotkeyDecl | eventBlock }

hotkeyDecl      := "hotkey" STRING ":" IDENT NEWLINE

eventBlock      := "event" IDENT ":" NEWLINE INDENT { statement } DEDENT

statement       := actionStmt | ifStmt

actionStmt      := ("block" | "allow" | "deny") NEWLINE
                 | "warn" STRING NEWLINE

ifStmt          := "if" conditionExpr ":" NEWLINE INDENT { statement } DEDENT
                   [ "else" ":" NEWLINE INDENT { statement } DEDENT ]

conditionExpr   := simpleCondition { ("and" | "or") simpleCondition }

simpleCondition := operand COMPARATOR operand

operand         := IDENT | NUMBER | STRING

COMPARATOR      := ">" | "<" | ">=" | "<=" | "==" | "!="
```

Notes:

- `hotkeyDecl` and `eventBlock` may be freely interleaved at the top level. The `hotkey`
  clause binds a keyboard combo to an action name; the combo is a quoted string such as
  `"Ctrl+Alt+0"`. The daemon validates the combo at startup, not at parse time, so `NL.Core`
  stays free of any Windows-specific dependency.
- Duplicate hotkey combos (case-insensitive) are not a parse error — the parser emits a
  warning and keeps only the first declaration. Duplicate `action` names in different `hotkey`
  lines are fine (you can bind two combos to the same action).
- One `event` block per event name per file. Duplicate event names are a parse error.
- `block` and `deny` are aliases in v0.1 (both mean "reject the action"); `deny` is kept
  because it reads better for some sentences in the original idea doc. `allow` explicitly
  permits the action. `warn "<message>"` does not by itself allow or block — it attaches a
  message to whatever the *next* action statement in the same block decides. A block with only
  `warn` and no following action statement defaults to `allow` with that warning attached.
- `if` / `else` bodies must end in an action statement (directly, or via a nested `if`/`else`
  that itself always terminates in one). The parser does not attempt full reachability
  analysis in v0.1 — if a branch falls through with no action, evaluation returns `allow` with
  no message, and the engine surfaces this as a warning to the streamer at load time.
- `and`/`or` may chain multiple simple comparisons in one `if` line. Joins are left-associative
  and all have equal precedence: `A and B or C` evaluates as `(A and B) or C`. Evaluation
  short-circuits (`and` stops on first false, `or` on first true). Parentheses are not supported
  in v0.1. See `samples/configs/compound-conditions.nle` for practical examples.

## Event and property vocabulary (mocked, v0.1)

Recognized event names: `shoot`, `respawn`, `leaveBoundary`, `useItem`.

Recognized properties on the implicit `player` object: `player.health` (number),
`player.hasItem` (0/1 stand-in for boolean, since v0.1 has no boolean literal).

These are intentionally tiny placeholders for `MockGameEventSource` in `NL.Simulator`. Real
game integration (Phase 3 of [`ROADMAP.md`](../ROADMAP.md)) will replace this fixed vocabulary
with a proper event/property registry.

## Example

```nle
# No PvP shooting allowed during this event.
event shoot:
    block

# Respawning is fine if you're already dead; otherwise it's a rule-break.
event respawn:
    if player.health > 0:
        block
    else:
        allow

# Leaving the marked play area is discouraged but not run-ending.
event leaveBoundary:
    warn "stay within the zone"
    block
```

## Explicitly out of scope for v0.1

- Boolean literals; parenthesised grouping in compound conditions.
- User-defined variables, functions, loops.
- Anything referencing SP roles, NLServer state, or moderation — those depend on later phases.
- Encryption/closed-source packaging described in `nl.txt` — samples here are plain text on
  purpose, for easy prototyping and testing.
