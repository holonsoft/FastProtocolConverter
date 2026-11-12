# Changelog

All notable changes to holonsoft.FastProtocolConverter and holonsoft.FastProtocolConverter.Abstractions.
The project follows [Semantic Versioning](https://semver.org/).

## [5.0.0] - 2026-11-12

### Breaking

- **Dropped .NET 8 and .NET 9.** Both are out of support since 10 November 2026. Projects that
  target `net8.0` or `net9.0` get NuGet error NU1202 for 5.0. Stay on **4.0.x**, which has the same
  API and features, until you move to .NET 10 or later.

### Added

- **.NET 11 target** (`net11.0`). Supported targets are now `net10.0` and `net11.0`.
- The `net11.0` build has no package dependency on `Microsoft.Extensions.Logging.Abstractions`,
  because .NET 11 ships it in the shared framework.

### Behavior on .NET 11

- .NET 11 converts `double`/`float` to `decimal` close to exactly (before: rounded to 15 or 7
  significant digits). The converter never makes this conversion: decimals are written and read as
  their four component integers, range limits are parsed from strings. Frames are byte identical on
  .NET 10 and .NET 11, which the tests check with exact bits and scale on both runtimes.
- .NET 11 makes unchecked floating point casts to 8 and 16 bit integers saturating. The converter
  makes no such casts, floating point values go on the wire as their raw bits.

### Internal

- The test suite now runs on every target framework of the package (before: only on net10.0).
- Runtime async (`runtime-async=on`) is switched on for net11.0. There are no async methods in the
  code base yet, so this changes nothing at the moment.
- Package validation compares against 4.0.0. The only suppressed differences are the two dropped
  target frameworks.

## [4.0.0] - 2026-09-14

Rewrite of both conversion paths: 50% to 85% faster, no allocation per message when instance and
buffer are reused, thread safe, properties as protocol members, `ReadOnlySpan<byte>` and
`IBufferWriter<byte>` overloads, working `decimal` and `sbyte`. Contains breaking changes, see
[Upgrading from 3.x to 4.0](README.md#upgrading-from-3x-to-40) in the README.
