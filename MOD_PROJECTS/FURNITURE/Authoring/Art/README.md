# Furniture revision artwork

Generated with the built-in image_gen tool on 2026-11-14. `prompts.json` records every final prompt and PNG hash. `frames.json` records the measured frame crops and in-game draw dimensions used by `CrimsonCardinalArt.cs`.

The 20 cardinal atlases contain N/E/S/W across columns, OFF/ON across rows. Throne east/west frames are reordered in the registration to preserve the usable face. North exposes the usable face to the south; east exposes it to the west. Shelves retain their intentionally double-sided fronts.

The refrigerator wallpapers are selected once per building and saved. The refrigerator and IV stand mirror flag is also saved. Image files are in `CrimsonSOS2/1.6/Textures/Crimson/Decor/Revision/`.

Metal palette correction and signal emission are separate runtime passes. Signal masks compare ON against OFF so upholstery does not become an emissive surface. Floor footprints are distinct from projected upright sprite sizes.
