import type { Adapter, AdapterDeliverResult } from "../adapter";
import { AdapterStatus } from "../adapter";
import type { Payload } from "../payload";
import type { Severity } from "../severity";

interface RecordedCall {
  readonly severity: Severity;
  readonly payload: Payload;
  readonly silent: boolean;
}

/**
 * In-memory test/reference adapter driven by a fixed outcome sequence.
 *
 * Each call to `deliver` consumes the next entry in `behaviors`; once the
 * sequence is exhausted, the last entry repeats (matches the contract-tests
 * fixture schema). Every invocation is recorded in `calls` so unit tests can
 * assert on the observed severity, payload, and silent flag.
 */
export class InMemoryAdapter implements Adapter {
  readonly name: string;
  readonly behaviors: ReadonlyArray<AdapterDeliverResult>;
  readonly calls: RecordedCall[] = [];
  available = true;
  private index = 0;

  constructor(name = "memory", behaviors: Iterable<AdapterDeliverResult> = []) {
    this.name = name;
    this.behaviors = [...behaviors];
  }

  async isAvailable(): Promise<boolean> {
    return this.available;
  }

  async deliver(
    severity: Severity,
    payload: Payload,
    silent: boolean,
  ): Promise<AdapterDeliverResult> {
    this.calls.push({ severity, payload, silent });
    if (this.behaviors.length === 0) {
      return { status: AdapterStatus.Delivered };
    }
    const idx = Math.min(this.index, this.behaviors.length - 1);
    this.index += 1;
    // idx is clamped into [0, behaviors.length-1] above, so the lookup is
    // always defined.
    // eslint-disable-next-line security/detect-object-injection
    return this.behaviors[idx]!;
  }
}
