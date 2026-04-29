/**
 * Notification payload. CONTRACT.md §Payload schema.
 *
 * Validation rules (enforced by NotificationClient before any adapter is
 * invoked):
 *  - title: non-empty, <= 200 chars after trim
 *  - body: non-empty, <= 2000 chars after trim
 *  - url: optional; if present, MUST start with `https://`
 *  - urlTitle: optional; <= 100 chars; ignored when url is undefined
 *  - metadata: optional; opaque to the port, passed through to adapters
 */
export interface Payload {
  readonly title: string;
  readonly body: string;
  readonly url?: string;
  readonly urlTitle?: string;
  readonly metadata?: Readonly<Record<string, string>>;
}

const TITLE_MAX = 200;
const BODY_MAX = 2000;
const URL_TITLE_MAX = 100;
const HTTPS_PREFIX = "https://";

/**
 * Returns null when the payload is valid, otherwise a short reason string.
 * The reason is informational; the public surface always reports the failure
 * as the stable error code INVALID_PAYLOAD and never echoes the reason.
 */
export function validatePayload(payload: Payload): string | null {
  const title = payload.title?.trim() ?? "";
  if (!title) return "title is empty";
  if (title.length > TITLE_MAX) return `title exceeds ${TITLE_MAX} char cap`;

  const body = payload.body?.trim() ?? "";
  if (!body) return "body is empty";
  if (body.length > BODY_MAX) return `body exceeds ${BODY_MAX} char cap`;

  if (payload.url !== undefined && !payload.url.startsWith(HTTPS_PREFIX)) {
    return "url must use https scheme";
  }

  if (payload.urlTitle !== undefined && payload.urlTitle.length > URL_TITLE_MAX) {
    return `url_title exceeds ${URL_TITLE_MAX} char cap`;
  }

  return null;
}
