"""Export the generated RGBA artwork to a multi-resolution Windows icon.

Input: Assets/source/app-generated-v2-original.png, preserving the returned alpha.
This script only processes existing artwork; it does not call an image API.
"""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent / "Assets"
SIZES = (16, 20, 24, 32, 48, 64, 256)


def main():
    source = Image.open(ROOT / "source" / "app-generated-v2-original.png").convert("RGBA")
    bbox = source.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Artwork has no visible pixels")
    artwork = source.crop(bbox)
    artwork.thumbnail((960, 960), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (1024, 1024))
    canvas.alpha_composite(artwork, ((1024 - artwork.width) // 2, (1024 - artwork.height) // 2))
    canvas.save(ROOT / "app-generated-v2.png")
    canvas.resize((256, 256), Image.Resampling.LANCZOS).save(ROOT / "app-generated-v2-256.png")
    canvas.save(ROOT / "app-generated-v2.ico", sizes=[(s, s) for s in SIZES])

    preview = Image.new("RGB", (800, 390), "#eef2f7")
    draw = ImageDraw.Draw(preview)
    for row, background in enumerate(("#f7f9fc", "#20242c")):
        y = row * 195
        draw.rectangle((0, y, 800, y + 194), fill=background)
        text = "#334155" if row == 0 else "#e2e8f0"
        large = canvas.resize((160, 160), Image.Resampling.LANCZOS)
        preview.paste(large, (24, y + 16), large)
        x = 222
        for size in SIZES[:-1]:
            small = canvas.resize((size, size), Image.Resampling.LANCZOS)
            preview.paste(small, (x + (64 - size) // 2, y + 58 + (64 - size) // 2), small)
            draw.text((x + 16, y + 137), f"{size}px", fill=text)
            x += 92
    preview.save(ROOT / "app-generated-v2-preview.png")
    ico = Image.open(ROOT / "app-generated-v2.ico")
    assert ico.ico.sizes() == {(s, s) for s in SIZES}
    for size in SIZES:
        frame = ico.ico.getimage((size, size)).convert("RGBA")
        assert frame.getchannel("A").getextrema() == (0, 255)
    print("Validated ICO sizes:", ", ".join(map(str, SIZES)))


if __name__ == "__main__":
    main()
