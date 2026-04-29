// @ts-check
import js from "@eslint/js";
import tseslint from "@typescript-eslint/eslint-plugin";
import tsparser from "@typescript-eslint/parser";
import security from "eslint-plugin-security";
import prettier from "eslint-config-prettier";

export default [
  {
    ignores: ["dist/**", "node_modules/**", "coverage/**"],
  },
  js.configs.recommended,
  {
    files: ["src/**/*.ts", "tests/**/*.ts"],
    languageOptions: {
      parser: tsparser,
      parserOptions: {
        project: "./tsconfig.eslint.json",
      },
      globals: {
        // Node 24 globals used in tests and runtime entry points.
        URL: "readonly",
        URLSearchParams: "readonly",
        console: "readonly",
        process: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
        Buffer: "readonly",
        globalThis: "readonly",
        fetch: "readonly",
        Request: "readonly",
        Response: "readonly",
        Headers: "readonly",
        AbortController: "readonly",
        AbortSignal: "readonly",
        BodyInit: "readonly",
      },
    },
    plugins: {
      "@typescript-eslint": tseslint,
      security,
    },
    rules: {
      ...tseslint.configs.recommended.rules,
      ...security.configs.recommended.rules,
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],
      "@typescript-eslint/explicit-function-return-type": "warn",
      // The TypeScript pattern `const X = {...} as const; type X = ...`
      // looks like a redeclaration to the base ESLint rule but is valid
      // TS (separate value + type namespaces). Use the TS-aware variant.
      "no-redeclare": "off",
      "@typescript-eslint/no-redeclare": "error",
      "no-console": ["warn", { allow: ["warn", "error"] }],
    },
  },
  {
    // Tests load a fixture by a computed path; the path is derived from
    // __dirname at compile time, not from user input, so detect-non-literal-fs-filename
    // is a false positive here.
    files: ["tests/**/*.ts"],
    rules: {
      "security/detect-non-literal-fs-filename": "off",
      // Tests build configs and adapter outcomes from JSON fixtures; the
      // index/key values come from the controlled test_cases.json file,
      // not from user input.
      "security/detect-object-injection": "off",
    },
  },
  prettier,
];
