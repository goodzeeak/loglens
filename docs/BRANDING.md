# LogLens identity

Original Goodwin Labs mark: a magnifying lens revealing three diagnostic log entries. Navy `#101D30`, mint `#83E6CC`, near-white `#F0F5FC`. It is intended to remain recognizable at small Windows icon sizes and on both themes.

- `branding/LogLens.svg`: editable resolution-independent source.
- `branding/LogLens-1024.png`: transparent-corner raster mark for documentation.
- `branding/LogLens.ico`: 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixel Windows icon frames.
- `src/LogLens.App/Assets/`: embedded WPF and executable assets.

Rebuild raster/icon assets on Windows with `pwsh ./scripts/brand.ps1`. No external image downloads, fonts or third-party artwork are required. Preserve proportions and breathing room; do not stretch the symbol. The design is included under this repository's MIT license. No trademark clearance claim is made.
