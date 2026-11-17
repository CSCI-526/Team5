"""Generate independent timber gate prefabs from the project's static primitive format."""
import math
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOWERS = ROOT / 'Assets/Rebuild/Prefabs/LevelBuilding/Towers'
MATERIALS = ROOT / 'Assets/Rebuild/Materials/SingleCellTower'


def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'team5/wooden-gate/' + name).hex


def write_asset(path, content, importer):
    path.write_text(content, encoding='utf-8')
    path.with_suffix(path.suffix + '.meta').write_text(
        f'fileFormatVersion: 2\nguid: {guid(path.name)}\n{importer}:\n  externalObjects: {{}}\n' +
        ('  mainObjectFileID: 2100000\n' if importer == 'NativeFormatImporter' else '') +
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')


material_template = (MATERIALS / 'WallStone.mat').read_text(encoding='utf-8')
for name, color in [('GateTimber', (.43, .22, .08)), ('GateTimberLight', (.58, .34, .14)), ('GateIron', (.16, .14, .12))]:
    mat = re.sub(r'm_Name: WallStone', 'm_Name: ' + name, material_template)
    for prop in ('_BaseColor', '_Color'):
        mat = re.sub(r'(' + prop + r': )\{[^}]+\}', rf'\g<1>{{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}', mat)
    write_asset(MATERIALS / (name + '.mat'), mat, 'NativeFormatImporter')

template = (TOWERS / 'Wall_3x1_X.prefab').read_text(encoding='utf-8')
header, rest = template.split('--- !u!1 &1100000', 1)
part_template = '--- !u!1 &1100000' + rest.split('--- !u!1 &1100010', 1)[0]
parts = []
for x in (-1.35, 1.35):
    parts.append(('Gate Post', (x, .85, 0), (.25, 1.7, .65), 'GateTimber', 0))
parts.append(('Gate Lintel', (0, 1.72, 0), (2.95, .2, .65), 'GateTimber', 0))
for i in range(8):
    parts.append(('Vertical Timber Plank', (-1.08 + i * .309, .81, 0), (.295, 1.58, .24), 'GateTimberLight' if i % 2 else 'GateTimber', 0))
for side in (-1, 1):
    for y in (.38, 1.22):
        parts.append(('Horizontal Gate Rail', (0, y, side * .17), (2.45, .14, .12), 'GateTimber', 0))
    parts.append(('Diagonal Timber Brace', (0, .8, side * .19), (2.5, .12, .13), 'GateTimberLight', 23))
    for x in (-1.16, 1.16):
        for y in (.38, 1.22):
            parts.append(('Iron Hinge', (x, y, side * .25), (.26, .1, .07), 'GateIron', 0))

for axis in ('X', 'Z'):
    name = 'WoodenGate_3x1_' + axis
    root = header.replace('m_Name: Wall_3x1_X', 'm_Name: ' + name)
    root = root.replace('  isWall: 1', '  isWall: 1\n  isWoodenGate: 1').replace('  wallAlongZ: 0', '  wallAlongZ: ' + str(int(axis == 'Z')))
    children = ''.join(f'  - {{fileID: {1100001 + i * 10}}}\n' for i in range(len(parts)))
    root = re.sub(r'  m_Children:\n.*?  m_Father:', '  m_Children:\n' + children + '  m_Father:', root, flags=re.S)
    output = root
    for i, (label, pos, scale, material, angle) in enumerate(parts):
        part = part_template
        # Replace IDs simultaneously so subsequent substitutions cannot collide.
        part = re.sub(r'110000[0-3]', lambda m: str(int(m.group()) + i * 10), part)
        part = part.replace('m_Name: Wall Brick 1', 'm_Name: ' + label + ' ' + str(i + 1))
        if axis == 'Z':
            pos = (pos[2], pos[1], -pos[0])
            # Rotate each visual primitive; the prefab root stays at identity.
            a = math.radians(angle / 2)
            rotation = (math.sin(a) / math.sqrt(2), math.cos(a) / math.sqrt(2), math.sin(a) / math.sqrt(2), math.cos(a) / math.sqrt(2))
        else:
            rotation = (0, 0, math.sin(math.radians(angle / 2)), math.cos(math.radians(angle / 2)))
        part = re.sub(r'  m_LocalPosition: .*', f'  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}', part)
        part = re.sub(r'  m_LocalScale: .*', f'  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}', part)
        part = re.sub(r'  m_LocalRotation: .*', '  m_LocalRotation: {' + ', '.join(f'{k}: {v}' for k, v in zip('xyzw', rotation)) + '}', part)
        part = part.replace('bb0c20a72b88484a9878f86f8faee1d5', guid(material + '.mat'))
        output += part
    write_asset(TOWERS / (name + '.prefab'), output, 'PrefabImporter')
print('Generated timber gate prefabs (X/Z) and three independent materials.')
