"""Export the canonical, pixel-aligned Hataori SVG to PNG and Windows ICO."""

from pathlib import Path
from xml.etree import ElementTree

from PIL import Image


OUT = Path(__file__).resolve().parent
SIZES = [(edge, edge) for edge in (16, 20, 24, 32, 40, 48, 64, 128, 256)]


def main() -> None:
    svg = ElementTree.parse(OUT / "hataori.svg").getroot()
    if svg.attrib.get("viewBox") != "0 0 16 16":
        raise ValueError("The source icon must use the 16-pixel grid")

    image = Image.new("RGBA", (16, 16))
    for pixel in svg.findall("{http://www.w3.org/2000/svg}rect"):
        x, y = (int(pixel.attrib[axis]) for axis in ("x", "y"))
        value = {"#000": 0, "#fff": 255}[pixel.attrib["fill"]]
        image.putpixel((x, y), (value, value, value, 255))

    image.save(OUT / "hataori.png")
    frames = [image.resize(size, Image.Resampling.NEAREST) for size in SIZES]
    frames[-1].save(OUT / "hataori.ico", sizes=SIZES, append_images=frames[:-1])


if __name__ == "__main__":
    main()
