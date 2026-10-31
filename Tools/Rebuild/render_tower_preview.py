"""Render actual Cube geometry/materials offline for model review (not a Unity screenshot)."""
from pathlib import Path
import re
import sys

import numpy as np
from PIL import Image, ImageDraw
from create_level_prefab_kit import parse, owner, ROOT
from create_single_cell_tower import TARGET, MATERIALS, resolve_square_variant

square = '--square' in sys.argv
blocks = resolve_square_variant() if square else parse(TARGET.read_text())
transforms = {owner(body): body for kind, body in blocks.values() if kind == 4}
materials = {}
for path in MATERIALS.glob('*.mat'):
    guid = re.search(r'guid: (\w+)', Path(str(path) + '.meta').read_text())[1]
    text = path.read_text()
    rgb = np.array([float(v) for v in re.search(r'_BaseColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+)', text).groups()])
    materials[guid] = rgb, path.stem == 'AreaEnergy'

W = H = 1200
pixels = np.zeros((H, W, 3), dtype=float)
for y in range(H):
    pixels[y, :, :] = np.array([.075, .095, .13]) + (1 - y / H) * .035
zbuffer = np.full((H, W), -np.inf)
view = np.array([-4., 3., -5.]); view /= np.linalg.norm(view)
right = np.cross(np.array([0., 1., 0.]), view); right /= np.linalg.norm(right)
up = np.cross(view, right)
light = np.array([-3., 7., -4.]); light /= np.linalg.norm(light)
center, scale = np.array([0., .76, 0.]), H * .46
corners = np.array([[-1,-1,-1], [1,-1,-1], [1,1,-1], [-1,1,-1],
                    [-1,-1,1], [1,-1,1], [1,1,1], [-1,1,1]], dtype=float)
faces = [([0,3,2,1], [0,0,-1]), ([4,5,6,7], [0,0,1]),
         ([0,4,7,3], [-1,0,0]), ([1,2,6,5], [1,0,0]),
         ([0,1,5,4], [0,-1,0]), ([3,7,6,2], [0,1,0])]


def triangle(vertices, color):
    low = np.maximum(np.floor(vertices[:, :2].min(axis=0)).astype(int), [0, 0])
    high = np.minimum(np.ceil(vertices[:, :2].max(axis=0)).astype(int), [W-1, H-1])
    if np.any(low > high):
        return
    xx, yy = np.meshgrid(np.arange(low[0], high[0]+1) + .5, np.arange(low[1], high[1]+1) + .5)
    a, b, c = vertices
    determinant = (b[1]-c[1])*(a[0]-c[0]) + (c[0]-b[0])*(a[1]-c[1])
    if abs(determinant) < 1e-8:
        return
    p = ((b[1]-c[1])*(xx-c[0]) + (c[0]-b[0])*(yy-c[1])) / determinant
    q = ((c[1]-a[1])*(xx-c[0]) + (a[0]-c[0])*(yy-c[1])) / determinant
    r = 1-p-q
    depth = p*a[2] + q*b[2] + r*c[2]
    local_depth = zbuffer[low[1]:high[1]+1, low[0]:high[0]+1]
    mask = (p >= -1e-8) & (q >= -1e-8) & (r >= -1e-8) & (depth > local_depth)
    local_depth[mask] = depth[mask]
    pixels[low[1]:high[1]+1, low[0]:high[0]+1][mask] = color


for kind, body in blocks.values():
    if kind != 23:
        continue
    transform = transforms[owner(body)]
    position = np.array([float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', transform).groups()])
    size = np.array([float(v) for v in re.search(r'm_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', transform).groups()])
    rgb, emissive = materials[re.search(r'fileID: 2100000, guid: (\w+)', body)[1]]
    world = corners * size / 2 + position - center
    vertices = np.stack([W/2 + world@right*scale, H*.51 - world@up*scale, world@view], axis=1)
    for face, normal in faces:
        normal = np.array(normal)
        if normal @ view <= 0:
            continue
        shade = (.83 + .17 * max(0, normal@light)) if emissive else (.57 + .43 * max(0, normal@light))
        color = np.clip(rgb * shade, 0, 1)
        triangle(vertices[face[:3]], color)
        triangle(vertices[[face[0], face[2], face[3]]], color)

image = Image.fromarray(np.uint8(np.clip(pixels, 0, 1)*255)).resize((720, 720), Image.Resampling.LANCZOS)
draw = ImageDraw.Draw(image)
draw.text((24, 24), 'SQUARE ENERGY TOWER' if square else 'TOWER_ATTACK_1X1', fill=(210,220,235), font_size=24)
draw.text((24, 684), 'Offline geometry preview | 1 cell | Square attack area' if square else
          'Offline geometry preview | 1 cell | height 1.58', fill=(155,173,193), font_size=16)
output = ROOT / 'Logs/SingleCellTower' / ('SquarePreview.png' if square else 'Preview.png')
output.parent.mkdir(parents=True, exist_ok=True)
image.save(output)
print(output)
