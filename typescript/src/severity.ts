/**
 * Severity enum. CONTRACT.md §Severity. Five levels, fixed order, names and
 * order frozen for contract revision v1. Values are the SCREAMING_SNAKE_CASE
 * wire strings used by the cross-language contract fixtures.
 */
export enum Severity {
  Debug = "DEBUG",
  Info = "INFO",
  Warn = "WARN",
  Error = "ERROR",
  Critical = "CRITICAL",
}

const ICONS: Readonly<Record<Severity, string>> = {
  [Severity.Debug]: "🔍",
  [Severity.Info]: "ℹ️",
  [Severity.Warn]: "⚠️",
  [Severity.Error]: "❌",
  [Severity.Critical]: "⛔",
};

/** Stable icon for the severity (CONTRACT.md severity table). */
export function severityIcon(severity: Severity): string {
  // severity is a controlled enum value (not user input); the
  // detect-object-injection signature on this lookup is a false positive.
  // eslint-disable-next-line security/detect-object-injection
  return ICONS[severity];
}
