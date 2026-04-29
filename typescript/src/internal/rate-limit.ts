import type { RateLimitPolicy } from "../config";

class SlidingWindow {
  private readonly maxCount: number;
  private readonly windowMs: number;
  private readonly timestamps: number[] = [];

  constructor(maxCount: number, windowSeconds: number) {
    this.maxCount = maxCount;
    this.windowMs = windowSeconds * 1000;
  }

  allow(nowMs: number): boolean {
    while (this.timestamps.length > 0) {
      const head = this.timestamps[0];
      if (head === undefined || nowMs - head <= this.windowMs) break;
      this.timestamps.shift();
    }
    if (this.timestamps.length >= this.maxCount) return false;
    this.timestamps.push(nowMs);
    return true;
  }
}

/** Per-adapter sliding-window rate limiter, indexed by adapter name. */
export class RateLimiter {
  private readonly policies: Map<string, RateLimitPolicy>;
  private readonly windows = new Map<string, SlidingWindow>();

  constructor(policies: Readonly<Record<string, RateLimitPolicy>> | undefined) {
    this.policies = new Map(Object.entries(policies ?? {}));
  }

  policyFor(adapterName: string): RateLimitPolicy | undefined {
    return this.policies.get(adapterName);
  }

  allow(adapterName: string, nowMs: number): boolean {
    const policy = this.policies.get(adapterName);
    if (!policy) return true;
    if (policy.maxCount <= 0) return false;
    let window = this.windows.get(adapterName);
    if (!window) {
      window = new SlidingWindow(policy.maxCount, policy.windowSeconds);
      this.windows.set(adapterName, window);
    }
    return window.allow(nowMs);
  }
}
