import type { QuietHoursConfig } from "../config";

function parseHhMm(value: string): number {
  const [h, m] = value.split(":");
  return Number(h ?? "0") * 60 + Number(m ?? "0");
}

/** Returns true when `now` (or wall-clock now) falls inside the window. */
export function isQuiet(config: QuietHoursConfig, now: Date = new Date()): boolean {
  const fmt = new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: config.timezone,
  });
  const parts = fmt.formatToParts(now);
  const hour = parts.find((p) => p.type === "hour")?.value ?? "00";
  const minute = parts.find((p) => p.type === "minute")?.value ?? "00";
  const nowMinutes = Number(hour) * 60 + Number(minute);

  const startMin = parseHhMm(config.start);
  const endMin = parseHhMm(config.end);

  if (startMin <= endMin) {
    return nowMinutes >= startMin && nowMinutes < endMin;
  }
  return nowMinutes >= startMin || nowMinutes < endMin;
}
