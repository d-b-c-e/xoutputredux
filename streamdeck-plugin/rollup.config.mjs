import resolve from "@rollup/plugin-node-resolve";
import typescript from "@rollup/plugin-typescript";

export default {
  input: "src/plugin.ts",
  output: {
    file: "com.xoutputrenew.sdPlugin/bin/plugin.js",
    format: "es",
    sourcemap: true,
  },
  plugins: [
    resolve({
      browser: false,
      preferBuiltins: true,
    }),
    typescript(),
  ],
  external: ["child_process", "path", "os", "fs", "events", "util"],
};
