/**
 * Concrete adapters bundled with the package.
 *
 * Per-adapter modules live next to this file. Adapters are explicit, opt-in
 * imports — the package never auto-discovers or instantiates them.
 */

export { InMemoryAdapter } from "./memory";
export { PushoverAdapter, type PushoverOptions } from "./pushover";
