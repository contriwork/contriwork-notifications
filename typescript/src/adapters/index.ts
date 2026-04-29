/**
 * Concrete adapters bundled with the package.
 *
 * Per-adapter modules live next to this file. Adapters are explicit, opt-in
 * imports — the package never auto-discovers or instantiates them.
 */

export { DiscordWebhookAdapter, type DiscordWebhookOptions } from "./discord";
export { InMemoryAdapter } from "./memory";
export { PushoverAdapter, type PushoverOptions } from "./pushover";
export { SlackWebhookAdapter, type SlackWebhookOptions } from "./slack";
export { SmtpAdapter, type SmtpOptions, type SmtpTransporter } from "./smtp";
export { TelegramAdapter, type TelegramOptions } from "./telegram";
