# npm Production Strategy

The ContriWork roadmap (`PACKAGES_ROADMAP.md §3.5`) lists five possible
strategies for shipping a package on npm. This file documents which one
this package uses and **why**.

## Decision

**Strategy A — pure-TS reimplementation** (default).

The orchestrator, retry/backoff loop, quiet-hours window evaluator,
sliding-window rate limiter, payload validator, and every adapter that
talks HTTP (Pushover, Telegram, Slack, Discord) are written from scratch
in TypeScript. They share the cross-language `contract-tests/test_cases.json`
fixture suite with the Python and C# implementations and pass it
identically — no shared binary or compiled artefact crosses the language
boundary.

The SMTP adapter delegates wire-protocol details to **nodemailer**, an
npm runtime dependency. This is a normal Node-ecosystem dependency, not
a prebuilt binary, WASM module, sidecar process, or HTTP service —
nodemailer ships pure JavaScript and is itself a Strategy A library.
Wrapping it does not change the shipping classification.

## Alternatives considered

| ID | Name | When to pick | Trade-off |
|----|------|--------------|-----------|
| A | **Pure-TS reimplementation** | Pure-logic package (parsers, validators, encoders, algorithms). Zero native deps. | Must maintain three code lines. Behaviour parity enforced only by `contract-tests`. |
| B | **WASM** (compile Rust/Go/C++ to WebAssembly) | Perf-critical core shared between runtimes. | Toolchain complexity; binary size; restricted syscalls. |
| C | **N-API / node-gyp native addon** | Existing C/C++ codebase, must match bit-for-bit. | Cross-platform build pain; prebuilt-binary hosting; sandbox/security surface. |
| D | **Sidecar** (bundled binary / subprocess) | Large Python/.NET runtime, not worth rewriting. | Process management, startup cost, platform-matrix of binaries. |
| E | **HTTP client** (pointing at a hosted service) | Package wraps a SaaS or centralised service the org runs. | Requires infra; not usable offline; introduces network as dependency. |

## Rationale

The notifications port is pure logic plus thin HTTP/SMTP transport:

- The **orchestrator** (multicast, retry, quiet hours, rate limit) has
  no I/O of its own — it is a small state machine over the adapter
  list. Reimplementing it three times costs less than carrying a
  shared compiled core, and the cross-language contract fixtures pin
  parity precisely.
- HTTP adapters use **`globalThis.fetch`** (Node 24+ stable). No
  binary, no WASM, no native module.
- SMTP uses **nodemailer**, which is itself Strategy A on npm. The
  alternative — re-implementing RFC 5321/5322 in TypeScript — is well
  beyond what the package needs and would introduce more risk than the
  delegation does.
- iMessage (in the Python tree) is **out of scope on npm** —
  documented in `docs/SCOPE.md`. There is no compelling reason to ship
  a Node-side iMessage adapter (no parity benefit, macOS-only).

Strategies B–E would introduce binary distribution / native-build /
sidecar-management costs that this package does not need. Revisit only
if a future feature genuinely cannot be expressed in pure TypeScript
plus the existing runtime deps.

## Revisiting this decision

A strategy change is a **minor bump** at minimum and likely a **major**
because consumers see different install-time artefacts (prebuilt
binaries, WASM glue, postinstall scripts). Do not switch strategies
silently — open an ADR-style issue first and link it from this file.
