"""Register generated cutouts and write lossless layered PSD/PSB (no art repainting)."""
from pathlib import Path
import argparse
import hashlib
import json
import struct
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Art/WhiteCatgirlFront'
SIZE = (1190, 1322)
# Bottom to top. Screen directions describe the viewer's left/right.
# Cell index, semantic name, registration bounds on the common canvas.
LAYERS = [
    (1, 'F_TAIL_STATIC', (210, 825, 482, 1140)),
    (0, 'F_B_HAIR', (90, 290, 743, 1010)),
    (3, 'F_LEG_SCREEN_L', (438, 935, 598, 1265)),
    (4, 'F_LEG_SCREEN_R', (604, 935, 841, 1265)),
    (5, 'F_PELVIS', (506, 837, 729, 994)),
    (7, 'F_SLEEVE_SCREEN_R', (620, 658, 778, 1018)),
    (8, 'F_SLEEVE_SCREEN_L', (348, 655, 550, 1050)),
    (6, 'F_BODY', (465, 635, 713, 855)),
    (13, 'F_EAR_SCREEN_L', (330, 220, 525, 394)),
    (14, 'F_EAR_SCREEN_R', (647, 196, 807, 370)),
    (9, 'F_FACE', (431, 339, 743, 664)),
    (11, 'F_SIDE_HAIR_SCREEN_L', (348, 367, 485, 947)),
    (12, 'F_SIDE_HAIR_SCREEN_R', (686, 470, 809, 1012)),
    (10, 'F_HAIR_FRONT', (356, 289, 778, 538)),
]

def pack(fmt, *values):
    return struct.pack('>' + fmt, *values)

def write_photoshop(path, layers, merged, version):
    """Raw channel data; PSD v1 and true PSB v2, not renamed PNGs."""
    length = 'Q' if version == 2 else 'I'
    records, pixels = [], []
    # Photoshop serializes the uppermost layer first.
    for name, im, left, top, visible in reversed(layers):
        w, h = im.size
        bands = im.split()
        channels = [(-1, bands[3]), (0, bands[0]), (1, bands[1]), (2, bands[2])]
        record = pack('iiiiH', top, left, top + h, left + w, 4)
        for channel_id, band in channels:
            data = pack('H', 0) + band.tobytes()
            record += pack('h', channel_id) + pack(length, len(data))
            pixels.append(data)
        name_bytes = name.encode('ascii')
        pascal = bytes([len(name_bytes)]) + name_bytes
        pascal += b'\0' * (-len(pascal) % 4)
        extra = pack('II', 0, 0) + pascal
        # Persistent unique layer IDs help PSD importers identify cutouts.
        layer_id = int(hashlib.sha256(name_bytes).hexdigest()[:7], 16)
        extra += b'8BIMlyid' + pack('II', 4, layer_id)
        record += b'8BIMnorm' + bytes([255, 0, 0 if visible else 2, 0])
        record += pack('I', len(extra)) + extra
        records.append(record)
    info = pack('h', -len(layers)) + b''.join(records) + b''.join(pixels)
    info += b'\0' * (len(info) % 2)
    mask = pack(length, len(info)) + info + pack('I', 0)
    header = b'8BPS' + pack('H', version) + b'\0' * 6
    header += pack('HIIHH', 4, merged.height, merged.width, 8, 3)
    with path.open('wb') as f:
        f.write(header + pack('II', 0, 0) + pack(length, len(mask)) + mask)
        f.write(pack('H', 0))
        for band in merged.split():
            f.write(band.tobytes())

def validate_photoshop(path, expected, merged):
    """Decode every raw channel back from disk and verify bounds, names and pixels."""
    data = path.read_bytes()
    pos = 4
    def read(fmt):
        nonlocal pos
        count = struct.calcsize('>' + fmt)
        value = struct.unpack_from('>' + fmt, data, pos)
        pos += count
        return value[0] if len(value) == 1 else value
    version = read('H')
    assert data[:4] == b'8BPS' and version in (1, 2)
    pos += 6
    channels, h, w, depth, mode = read('HIIHH')
    assert (channels, w, h, depth, mode) == (4, *SIZE, 8, 3)
    for _ in range(2):
        amount = read('I')
        pos += amount
    length = 'Q' if version == 2 else 'I'
    section_size = read(length)
    section_end = pos + section_size
    info_size = read(length)
    count = read('h')
    assert count == -len(expected)
    records = []
    for _ in range(abs(count)):
        top, left, bottom, right, n = read('iiiiH')
        descriptors = [(read('h'), read(length)) for _ in range(n)]
        assert data[pos:pos+8] == b'8BIMnorm'
        pos += 8
        opacity, clipping, flags, filler = read('BBBB')
        extra_size = read('I')
        extra_end = pos + extra_size
        for _ in range(2):
            amount = read('I')
            pos += amount
        name_len = read('B')
        name = data[pos:pos+name_len].decode('ascii')
        pos = extra_end
        records.append((name, left, top, right-left, bottom-top, not bool(flags & 2), descriptors))
    decoded = []
    for name, left, top, lw, lh, visible, descriptors in records:
        bands = {}
        for channel_id, amount in descriptors:
            assert read('H') == 0 and amount == 2 + lw * lh
            bands[channel_id] = Image.frombytes('L', (lw, lh), data[pos:pos+amount-2])
            pos += amount - 2
        im = Image.merge('RGBA', [bands[x] for x in (0, 1, 2, -1)])
        decoded.append((name, im, left, top, visible))
    for actual, wanted in zip(reversed(decoded), expected):
        assert actual[0] == wanted[0] and actual[2:] == wanted[2:]
        assert actual[1].tobytes() == wanted[1].tobytes()
    pos = section_end
    assert read('H') == 0
    area = w * h
    flattened = Image.merge('RGBA', [Image.frombytes('L', (w,h), data[pos+i*area:pos+(i+1)*area]) for i in range(4)])
    assert flattened.tobytes() == merged.tobytes()
    assert len(data) == pos + 4 * area
    return {'file': path.name, 'version': version, 'visible_parts': sum(layer[4] for layer in expected), 'hidden_reference_layers': sum(not layer[4] for layer in expected), 'all_channel_pixels_verified': True, 'bytes': len(data)}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference')
    parser.add_argument('--atlas')
    args = parser.parse_args()
    for folder in ('Source', 'Parts', 'AlignedLayers'):
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    if args.reference:
        (OUT / 'Source/Reference_Original.png').write_bytes(Path(args.reference).read_bytes())
    if args.atlas:
        (OUT / 'Source/Generated_PartsAtlas.png').write_bytes(Path(args.atlas).read_bytes())
    reference = Image.open(OUT / 'Source/Reference_Original.png').convert('RGBA')
    assert reference.size == SIZE
    atlas = Image.open(OUT / 'Source/Generated_PartsAtlas.png').convert('RGBA')
    merged = Image.new('RGBA', SIZE)
    layers = [('REFERENCE_ORIGINAL_HIDDEN', reference, 0, 0, False)]
    meta = []
    for index, name, box in LAYERS:
        col, row = index % 4, index // 4
        cell = atlas.crop((round(col*atlas.width/4), round(row*atlas.height/4), round((col+1)*atlas.width/4), round((row+1)*atlas.height/4)))
        # Explicit corrected cutouts take precedence over the historical atlas.
        correction = OUT / 'Source/Corrections' / (name + '.png')
        if correction.exists():
            cell = Image.open(correction).convert('RGBA')
        bounds = cell.getchannel('A').point(lambda a: 255 if a > 32 else 0).getbbox()
        assert bounds
        piece = cell.crop(bounds).resize((box[2]-box[0], box[3]-box[1]), Image.Resampling.LANCZOS)
        piece.save(OUT / 'Parts' / (name + '.png'))
        aligned = Image.new('RGBA', SIZE)
        aligned.paste(piece, box[:2])
        aligned.save(OUT / 'AlignedLayers' / (name + '.png'))
        merged = Image.alpha_composite(merged, aligned)
        layers.append((name, piece, box[0], box[1], True))
        meta.append({'name': name, 'atlas_cell_zero_based': index, 'order_bottom_to_top': len(meta), 'bounds_px': box, 'visible': True, 'animation_candidate': name != 'F_TAIL_STATIC', 'notes': 'Static decoration; attach to body, no dedicated tail animation.' if name == 'F_TAIL_STATIC' else 'Continuous closed sleeve; no visible hand.' if 'SLEEVE' in name else 'Continuous thigh/calf/boot; future mesh skinning, no detached knee.' if 'LEG_' in name else ''})
    merged.save(OUT / 'WhiteCatgirl_Front_preview.png')
    background = Image.new('RGBA', SIZE, '#667379')
    Image.alpha_composite(background, merged).convert('RGB').save(OUT / 'WhiteCatgirl_Front_review.jpg', quality=94)
    report = []
    for version, extension in ((1, 'psd'), (2, 'psb')):
        path = OUT / ('WhiteCatgirl_Front.' + extension)
        write_photoshop(path, layers, merged, version)
        report.append(validate_photoshop(path, layers, merged))
    # Independent Pillow reader verifies the PSD flattened RGB output too.
    with Image.open(OUT / 'WhiteCatgirl_Front.psd') as check:
        assert check.convert('RGB').tobytes() == merged.convert('RGB').tobytes()
    (OUT / 'layers.json').write_text(json.dumps({'canvas_px': SIZE, 'root_px': [626,1265], 'suggested_pixels_per_unit': 1000, 'coordinate_system': 'Pixel origin top left; y increases down. Unity root at foot baseline.', 'hands_visible': False, 'tail_animated': False, 'rigged': False, 'layers': meta}, ensure_ascii=False, indent=2), encoding='utf-8')
    (OUT / 'validation.json').write_text(json.dumps({'formats': report, 'independent_pillow_psd_flattened_rgb_match': True, 'unity_import_tested': False, 'photoshop_open_tested': False}, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))

if __name__ == '__main__':
    main()
