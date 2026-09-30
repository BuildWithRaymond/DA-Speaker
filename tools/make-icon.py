"""Render the app's original speech mark into a Windows icon."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
assets = root / "src" / "DASpeaker" / "Assets"
assets.mkdir(parents=True, exist_ok=True)
scale = 4
image = Image.new("RGBA", (256 * scale, 256 * scale))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((0, 0, 255 * scale, 255 * scale), 52 * scale, fill="#D5A746")
points = [(55, 59), (201, 59), (201, 161), (119, 161), (76, 199), (76, 161), (55, 161), (55, 59)]
draw.line([(x * scale, y * scale) for x, y in points], fill="#0C0C0E", width=9 * scale, joint="curve")
draw.line((88 * scale, 95 * scale, 168 * scale, 95 * scale), fill="#0C0C0E", width=8 * scale)
draw.line((88 * scale, 125 * scale, 145 * scale, 125 * scale), fill="#0C0C0E", width=8 * scale)
image = image.resize((256, 256), Image.Resampling.LANCZOS)
image.save(assets / "Speaker.ico", sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
