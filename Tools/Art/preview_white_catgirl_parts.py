"""Preview the CURRENT Parts PNGs without regenerating or modifying source art."""
from pathlib import Path
from PIL import Image
import hashlib
import json

root = Path(__file__).resolve().parents[2] / 'Art/WhiteCatgirlFront'
layout = json.loads((root / 'layers.json').read_text(encoding='utf-8'))
canvas = Image.new('RGBA', tuple(layout['canvas_px']))
parts = []
for layer in sorted(layout['layers'], key=lambda x: x['order_bottom_to_top']):
    if not layer.get('visible', True):
        continue
    path = root / 'Parts' / (layer['name'] + '.png')
    piece = Image.open(path).convert('RGBA')
    left, top, right, bottom = layer['bounds_px']
    # User edits may change dimensions. Keep native pixels and the old top-center
    # anchor rather than stretching the edited drawing back to an obsolete size.
    x = round((left + right - piece.width) / 2)
    canvas.alpha_composite(piece, (x, top))
    parts.append({'name': layer['name'], 'position_px': [x, top], 'size_px': list(piece.size),
                  'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
out = root / 'Previews'
out.mkdir(exist_ok=True)
version = 1
while (out / f'WhiteCatgirl_CurrentParts_v{version}.png').exists():
    version += 1
stem = out / f'WhiteCatgirl_CurrentParts_v{version}'
canvas.save(stem.with_suffix('.png'))
Image.alpha_composite(Image.new('RGBA', canvas.size, '#667379'), canvas).convert('RGB').save(stem.with_suffix('.jpg'), quality=96)
stem.with_suffix('.json').write_text(json.dumps({'source': 'Current Parts/*.png, not the atlas or correction copies',
    'placement': 'Existing layer order and top-center anchors; edited PNG dimensions preserved without stretching',
    'parts': parts}, ensure_ascii=False, indent=2), encoding='utf-8')
print(stem.with_suffix('.jpg'))
