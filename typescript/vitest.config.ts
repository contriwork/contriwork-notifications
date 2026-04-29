import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["tests/**/*.test.ts"],
    environment: "node",
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov"],
      include: ["src/**/*.ts"],
    },
  },
  resolve: {
    // Map NodeNext-style ".js" imports inside the source tree back to ".ts"
    // so Vitest can resolve them to TypeScript source. tsc/tsup keep the
    // ".js" extension in build output (required by NodeNext module
    // resolution at runtime); Vitest does not transparently rewrite them.
    alias: [
      {
        find: /^(\..*)\.js$/,
        replacement: "$1.ts",
      },
    ],
  },
});
