import { describe, expect, it } from "vitest";
import type { NotificationsPort } from "../src/index.js";

describe("smoke", () => {
  it("exports NotificationsPort as a type", () => {
    const shape: NotificationsPort = {
      example: async (input: string) => input,
    };
    expect(typeof shape.example).toBe("function");
  });
});
