#!/usr/bin/env bash
#
# CodeBrix.Imaging.Drawing.NoSkia.Svg - non-public-surface pass.
#
# Run this ONCE after re-vendoring the SVG scene compiler (and after adding any file to the
# folders listed below). It does two things to every type this assembly does not publish:
#
#   1. Top-level "public" type declarations become "internal". Only the eight types listed
#      in KEEP_PUBLIC stay public - the DrawingSvg facade and the font registry. Nested and
#      member accessibility is left alone: a public member of an internal type is not
#      exported, so there is nothing to change and the diff against upstream stays small.
#
#   2. XML doc comments ("///") become plain comments ("//"). The comments stay in the file
#      for maintainers; the compiler simply stops emitting them. This is REQUIRED, not
#      cosmetic: the compiler writes .xml entries for INTERNAL types too, and an entry's
#      name attribute carries the member's parameter and return types - so a documented
#      ToDrawingShader(SKShader) would put "ShimSkiaSharp.SKShader" into the packaged
#      documentation file no matter how its prose reads. The NoSkia package promises a
#      developer never meets the word "Skia"; this is how the promise is kept at the source,
#      which is where XML doc comments must be correct. (Post-processing the generated .xml
#      at pack time was considered and rejected: build output must match the source.)
#
# The pass is idempotent - running it again changes nothing - so it is safe to re-run after
# any partial re-vendor.
#
set -euo pipefail

cd "$(dirname "$0")"

# The folders whose types are not part of this assembly's public surface: the vendored
# scene compiler and shim (ShimSkiaSharp, Model, SceneGraph) and the repository's own
# rendering internals (Rendering).
FOLDERS=(ShimSkiaSharp Model SceneGraph Rendering)

# The only file in those folders whose type stays public. Everything the package exposes
# other than this lives in the assembly root (DrawingSvg and friends), which this pass
# never touches.
KEEP_PUBLIC="Rendering/NoSkiaFontRegistry.cs"

internalized=0
undocumented=0

while IFS= read -r file; do
    if [[ "$file" == "./$KEEP_PUBLIC" ]]; then continue; fi

    internalized=$(( internalized + $(grep -c '^public ' "$file" || true) ))
    undocumented=$(( undocumented + $(grep -c '^[[:space:]]*///' "$file" || true) ))

    perl -pi -e 's{^public }{internal }; s{^(\s*)///}{$1//};' "$file"
done < <(find "${FOLDERS[@]}" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print | sed 's|^|./|')

echo "internalize-surface: $internalized type declarations made internal, $undocumented doc-comment lines demoted."
