/* eslint-disable */
const path = require("path");
const fs = require("fs");
const esbuild = require("esbuild");

async function main() {
  const root = path.resolve(__dirname, "..");
  const outDir = path.join(root, "dist", "QREVENT", "vendor");
  const outFile = path.join(outDir, "zxing.bundle.js");

  fs.mkdirSync(outDir, { recursive: true });

  await esbuild.build({
    entryPoints: [path.join(root, "build", "zxing-entry.js")],
    bundle: true,
    format: "esm",
    platform: "browser",
    sourcemap: false,
    minify: true,
    outfile: outFile
  });

  console.log("ZXing bundle listo:", outFile);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
