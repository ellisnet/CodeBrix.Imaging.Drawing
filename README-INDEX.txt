================================================================================
README-INDEX: CodeBrix.Imaging.Drawing
Map of the README files in this repository
================================================================================

If you are an AI coding agent: find the NuGet package you are consuming below
and read its AGENT-README file in full. Read MAINTAINER-README.txt only if you
are changing this repository itself.

This repository produces TWO packages that are EITHER/OR alternatives - they
declare the same types in the same namespaces, so an application references
one or the other, never both. Only
CodeBrix.Imaging.Drawing.ApacheLicenseForever is published on nuget.org;
CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever is a companion package,
not yet published.

AGENT-README FILES (consumer documentation, one per NuGet package)
------------------------------------------------------------------
  AGENT-README-SKIA.txt
      CodeBrix.Imaging.Drawing.ApacheLicenseForever — stroke-based drawing,
      painting and highlighting on SkiaSharp canvases, with calibrated
      resolution-independent strokes, layers, geometric primitives and
      PNG/JPEG export.
  AGENT-README-NOSKIA.txt
      CodeBrix.Imaging.Drawing.NoSkia.ApacheLicenseForever (a companion
      package, not yet published) — the same drawing-session API with zero
      native dependencies, plus a fully managed drawing engine and a managed
      SVG renderer.

MAINTAINER AND EXTRAS
---------------------
  MAINTAINER-README.txt
      Building, testing, packaging, versioning and provenance notes for
      maintainers.
  EXTRAS-README.txt
      Samples, tools and other non-package content in this repository.

GENERAL
-------
  README.md
      Human-facing overview shown on GitHub and nuget.org.
  README-INDEX.txt
      This file.
  THIRD-PARTY-NOTICES.txt
      What came from where, and under which licences. Ships inside both
      packages.
  src/CodeBrix.Imaging.Drawing.NoSkia.Svg/VENDORED-NOTICES.txt
      The notices carried forward with the vendored SVG scene compiler.
