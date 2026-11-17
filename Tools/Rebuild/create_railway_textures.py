"""Create original transparent flat railway artwork; no external images or models."""
from pathlib import Path
import math
from PIL import Image, ImageDraw

SIZE = 1024
OUTPUT = Path(__file__).resolve().parents[2] / 'Assets/Rebuild/RailwayPath'


def pixel(x, z):
    return ((x + .5) * SIZE, (.5 - z) * SIZE)


def sleeper(draw, center, radial, tangent):
    x, z = center
    points = [pixel(x + a * radial[0] + b * tangent[0], z + a * radial[1] + b * tangent[1])
              for a, b in [(-.42, -.052), (.42, -.052), (.42, .052), (-.42, .052)]]
    draw.polygon([(u + 4, v + 6) for u, v in points], fill=(28, 22, 16, 85))
    draw.polygon(points, fill=(75, 45, 25, 255))
    inset = [pixel(x + a * radial[0] + b * tangent[0], z + a * radial[1] + b * tangent[1])
             for a, b in [(-.40, -.034), (.40, -.034), (.40, .034), (-.40, .034)]]
    draw.polygon(inset, fill=(151, 100, 53, 255))
    draw.line([pixel(x - .37 * radial[0], z - .37 * radial[1]),
               pixel(x + .37 * radial[0], z + .37 * radial[1])], fill=(182, 131, 76, 255), width=5)


def rail(draw, points):
    points = [pixel(*p) for p in points]
    draw.line([(x + 4, y + 5) for x, y in points], fill=(20, 25, 27, 80), width=55)
    draw.line(points, fill=(38, 44, 49, 255), width=48)
    draw.line(points, fill=(101, 116, 125, 255), width=31)
    draw.line([(x - 5, y - 3) for x, y in points], fill=(201, 214, 218, 255), width=9)


def create(corner):
    image = Image.new('RGBA', (SIZE, SIZE))
    draw = ImageDraw.Draw(image)
    if corner:
        for i in range(4):
            a = i * math.pi / 6
            radial = (math.cos(a), math.sin(a))
            sleeper(draw, (-.5 + .5 * radial[0], -.5 + .5 * radial[1]), radial, (-radial[1], radial[0]))
        for radius in (.25, .75):
            rail(draw, [(-.5 + radius * math.cos(i * math.pi / 256),
                         -.5 + radius * math.sin(i * math.pi / 256)) for i in range(129)])
    else:
        for i in range(5):
            sleeper(draw, (0, -.5 + i * .25), (1, 0), (0, 1))
        for x in (-.25, .25):
            rail(draw, [(x, -.55), (x, .55)])
    return image.resize((256, 256), Image.Resampling.LANCZOS)


if __name__ == '__main__':
    OUTPUT.mkdir(parents=True, exist_ok=True)
    create(False).save(OUTPUT / 'RailStraight.png')
    create(True).save(OUTPUT / 'RailCorner.png')
    print(OUTPUT)
