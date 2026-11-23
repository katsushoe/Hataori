from pathlib import Path

from PIL import Image, ImageDraw

out = Path(__file__).resolve().parent
black, white = "#000", "#fff"

# Design A "front view loom": posts, warp threads, warp beam, cloth beam and a shuttle.
# The same geometry drives the editable SVG and the Windows icon frames (64x64 units).
STROKE = 4.5
WARP = 4
SHUTTLE = ((4, 30), (17, 21), (47, 21), (60, 30), (47, 39), (17, 39))  # two cubic curves, closed

shapes = [
    ("line", ((11, 8), (11, 60)), STROKE),             # left post
    ("line", ((53, 8), (53, 60)), STROKE),             # right post
    ("line", ((22, 12), (22, 50)), WARP),              # warp threads
    ("line", ((32, 12), (32, 50)), WARP),
    ("line", ((42, 12), (42, 50)), WARP),
    ("rect", (3, 4, 61, 13), 4.5, STROKE),             # warp beam
    ("rect", (6, 47, 58, 56), 4.5, STROKE),            # cloth beam
    ("shuttle", SHUTTLE, STROKE),                      # shuttle
    ("line", ((27, 30), (37, 30)), STROKE),            # bobbin slot
]


def fmt(value: float) -> str:
    return f"{value:g}"


def render_svg() -> str:
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="Hataori loom icon">',
           '  <title>Hataori — Loom</title>',
           f'  <g fill="{white}" stroke="{black}" stroke-linecap="round" stroke-linejoin="round">']
    for kind, geom, *style in shapes:
        if kind == "line":
            (x, y), (xx, yy) = geom
            (width,) = style
            svg.append(f'    <path d="M{fmt(x)} {fmt(y)}L{fmt(xx)} {fmt(yy)}" fill="none" stroke-width="{fmt(width)}"/>')
        elif kind == "rect":
            x, y, right, bottom = geom
            radius, width = style
            svg.append(f'    <rect x="{fmt(x)}" y="{fmt(y)}" width="{fmt(right-x)}" height="{fmt(bottom-y)}" '
                       f'rx="{fmt(radius)}" stroke-width="{fmt(width)}"/>')
        else:
            (p0, c1, c2, p1, c3, c4) = geom
            (width,) = style
            pt = lambda p: f"{fmt(p[0])} {fmt(p[1])}"
            svg.append(f'    <path d="M{pt(p0)}C{pt(c1)} {pt(c2)} {pt(p1)}C{pt(c3)} {pt(c4)} {pt(p0)}Z" '
                       f'stroke-width="{fmt(width)}"/>')
    return "\n".join([*svg, "  </g>", "</svg>", ""])


def cubic(p0, p1, p2, p3, steps=64):
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        yield (u**3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t**3 * p3[0],
               u**3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t**3 * p3[1])


def stroke_polyline(draw: ImageDraw.ImageDraw, points, width: int) -> None:
    draw.line(points, fill=black, width=width, joint="curve")
    half = width / 2
    for x, y in points:
        draw.ellipse((x - half, y - half, x + half, y + half), fill=black)


def render_icon() -> Image.Image:
    scale = 16
    canvas = Image.new("RGBA", (64 * scale, 64 * scale))
    draw = ImageDraw.Draw(canvas)
    for kind, geom, *style in shapes:
        if kind == "line":
            (width,) = style
            points = [(x * scale, y * scale) for x, y in geom]
            stroke_polyline(draw, points, round(width * scale))
        elif kind == "rect":
            radius, width = style
            left, top, right, bottom = [value * scale for value in geom]
            half = width * scale / 2
            draw.rounded_rectangle((left - half, top - half, right + half, bottom + half),
                                   radius * scale + half, fill=black)
            draw.rounded_rectangle((left + half, top + half, right - half, bottom - half),
                                   max(0, radius * scale - half), fill=white)
        else:
            (width,) = style
            p0, c1, c2, p1, c3, c4 = geom
            outline = [*cubic(p0, c1, c2, p1), *list(cubic(p1, c3, c4, p0))[1:]]
            points = [(x * scale, y * scale) for x, y in outline]
            draw.polygon(points, fill=white)
            stroke_polyline(draw, points, round(width * scale))
    return canvas


if __name__ == "__main__":
    (out / "hataori.svg").write_text(render_svg(), encoding="utf-8")
    source = render_icon()
    icon = source.resize((256, 256), Image.Resampling.LANCZOS)
    icon.save(out / "hataori.png")
    sizes = [(edge, edge) for edge in (16, 20, 24, 32, 40, 48, 64, 128, 256)]
    source.save(out / "hataori.ico", sizes=sizes)
    with Image.open(out / "hataori.ico") as generated:
        assert set(sizes) == generated.ico.sizes()
