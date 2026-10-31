"""Author a one-cell tower using editable Cube children and shared URP materials."""
from pathlib import Path
import re
import uuid

from create_level_prefab_kit import (
    ROOT, KIT, parse, subtree, remap, group, place, change, children, set_children, serialize, metadata,
)

NAME = 'Tower_Attack_1x1'
TARGET = KIT / 'Towers' / (NAME + '.prefab')
SQUARE_VARIANT = KIT / 'Towers/Tower_Attack_1x1_Square.prefab'
MATERIALS = ROOT / 'Assets/Rebuild/Materials/SingleCellTower'
PALETTE = {
    'Foundation': (0.19, 0.24, 0.31),
    'BlueStone': (0.22, 0.40, 0.66),
    'Trim': (0.64, 0.72, 0.80),
    'Gunmetal': (0.085, 0.12, 0.17),
    'Brass': (0.85, 0.64, 0.29),
    'Window': (0.18, 0.75, 0.94),
}


def create():
    assert not TARGET.exists() and not Path(str(TARGET) + '.meta').exists(), 'Tower already exists; refusing to overwrite.'
    assert not MATERIALS.exists(), 'Material directory already exists; refusing to overwrite.'
    source = parse((KIT / 'Towers/Tower_Square.prefab').read_text(encoding='utf-8-sig'))
    material_source = parse((ROOT / 'Assets/Rebuild/Materials/Minimal/Tower.mat').read_text(encoding='utf-8-sig'))
    material_body = next(body for kind, body in material_source.values() if kind == 21)
    MATERIALS.mkdir()
    metadata(MATERIALS, folder=True)
    guids = {}
    for name, color in PALETTE.items():
        body = re.sub(r'(?m)^  m_Name: .*$', '  m_Name: SingleCellTower_' + name, material_body)
        value = '{r: %s, g: %s, b: %s, a: 1}' % color
        body = re.sub(r'(?m)^(    - _(?:BaseColor|Color): ).*$', lambda m: m[1] + value, body)
        path = MATERIALS / (name + '.mat')
        path.write_text(serialize({2100000: (21, body)}), encoding='utf-8', newline='\n')
        guid = uuid.uuid4().hex
        Path(str(path) + '.meta').write_text(
            f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n'
            '  externalObjects: {}\n  mainObjectFileID: 2100000\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')
        guids[name] = guid

    docs = group(NAME)
    tower = next(body for _, body in source.values() if 'Team5.Rebuild.World.BattleTower' in body)
    tower = re.sub(r'(?m)^  m_GameObject: .*$', '  m_GameObject: {fileID: 1000000}', tower)
    tower = re.sub(r'(?m)^  usesMacroFootprint: 1$', '  usesMacroFootprint: 0', tower)
    docs[1000002] = 114, tower
    kind, body = docs[1000000]
    body = body.replace('  - component: {fileID: 1000001}\n',
                        '  - component: {fileID: 1000001}\n  - component: {fileID: 1000002}\n')
    docs[1000000] = kind, body
    mesh_transform = next(key for key, (kind, body) in source.items()
                          if kind == 4 and 'm_LocalScale: {x: 2.8, y: 1.2, z: 2.8}' in body)
    cube = subtree(source, mesh_transform)
    parts = [
        ('Stone Plinth', (0, .08, 0), (.90, .16, .90), 'Foundation'),
        ('Stepped Footing', (0, .21, 0), (.76, .10, .76), 'Trim'),
        ('Tower Shaft', (0, .69, 0), (.56, .86, .56), 'BlueStone'),
        ('Lower Stone Course', (0, .40, 0), (.60, .07, .60), 'Foundation'),
        ('Upper Stone Course', (0, .89, 0), (.60, .06, .60), 'Foundation'),
        ('Upper Cornice', (0, 1.16, 0), (.76, .12, .76), 'Trim'),
        ('Turret Deck', (0, 1.24, 0), (.64, .06, .64), 'Foundation'),
        ('Cannon Housing', (0, 1.39, 0), (.40, .24, .38), 'BlueStone'),
        ('Cannon Cap', (0, 1.54, 0), (.44, .08, .42), 'Brass'),
        ('Cannon Barrel', (-.30, 1.40, 0), (.28, .14, .14), 'Gunmetal'),
        ('Muzzle Rim', (-.45, 1.40, 0), (.08, .20, .20), 'Brass'),
        ('Muzzle Opening', (-.492, 1.40, 0), (.004, .115, .115), 'Gunmetal'),
        ('Front Arrow Slit', (-.287, .69, 0), (.018, .25, .13), 'Gunmetal'),
        ('Front Window', (-.298, .69, 0), (.008, .17, .045), 'Window'),
        ('Side Arrow Slit', (0, .69, -.287), (.13, .25, .018), 'Gunmetal'),
        ('Side Window', (0, .69, -.298), (.045, .17, .008), 'Window'),
    ]
    for x in (-.27, .27):
        for z in (-.27, .27):
            parts.append((f'Battlement {x} {z}', (x, 1.355, z), (.17, .27, .17), 'Trim'))
    roots = []
    for index, (name, position, scale, material) in enumerate(parts):
        clone, mapping = remap(cube, 1100000 + index * 10)
        transform = mapping[mesh_transform]
        place(clone, transform, name, position, 1000001)
        change(clone, transform, 'm_LocalScale', '{x: %s, y: %s, z: %s}' % scale)
        renderer = next(key for key, (kind, _) in clone.items() if kind == 23)
        kind, body = clone[renderer]
        body = re.sub(r'(?m)^(  - \{fileID: 2100000, guid: )\w+', r'\g<1>' + guids[material], body)
        body = body.replace('  m_CastShadows: 0', '  m_CastShadows: 1')
        body = body.replace('  m_ReceiveShadows: 0', '  m_ReceiveShadows: 1')
        clone[renderer] = kind, body
        roots.append(transform)
        docs.update(clone)
    docs[1000001] = 4, set_children(docs[1000001][1], roots)
    TARGET.write_text(serialize(docs), encoding='utf-8', newline='\n')
    metadata(TARGET)
    validate()
    print(f'Created {TARGET.relative_to(ROOT)}: {len(parts)} editable mesh parts, one-cell footprint.')
    create_square_variant()


def square_parts():
    # Same twenty editable parts as the base, repurposed into an omnidirectional energy tower.
    return [
        ('Square Plinth', (0, .08, 0), (.90, .16, .90), 'Foundation'),
        ('Reactor Footing', (0, .20, 0), (.76, .08, .76), 'Gunmetal'),
        ('Reactor Pedestal', (0, .54, 0), (.48, .60, .48), 'BlueStone'),
        ('Lower Armor Band', (0, .36, 0), (.56, .07, .56), 'Foundation'),
        ('Upper Armor Band', (0, .83, 0), (.60, .10, .60), 'Trim'),
        ('Emitter Deck', (0, .94, 0), (.80, .12, .80), 'Gunmetal'),
        ('Energy Distribution Plate', (0, 1.02, 0), (.66, .04, .66), 'AreaEnergy'),
        ('Central Energy Core', (0, 1.28, 0), (.26, .46, .26), 'AreaEnergy'),
        ('Core Crown', (0, 1.54, 0), (.32, .07, .32), 'Brass'),
        ('Emitter Negative X', (-.35, 1.16, 0), (.20, .18, .22), 'AreaEnergy'),
        ('Emitter Positive X', (.35, 1.16, 0), (.20, .18, .22), 'AreaEnergy'),
        ('Emitter Negative Z', (0, 1.16, -.35), (.22, .18, .20), 'AreaEnergy'),
        ('Emitter Positive Z', (0, 1.16, .35), (.22, .18, .20), 'AreaEnergy'),
        ('Square Frame Negative Z', (0, 1.41, -.37), (.86, .055, .06), 'Brass'),
        ('Square Frame Positive Z', (0, 1.41, .37), (.86, .055, .06), 'Brass'),
        ('Square Frame Negative X', (-.40, 1.41, 0), (.06, .055, .68), 'Brass'),
        ('Square Frame Positive X', (.40, 1.41, 0), (.06, .055, .68), 'Brass'),
        ('Core Socket', (0, 1.08, 0), (.34, .06, .34), 'Gunmetal'),
        ('Square Badge X', (-.249, .55, 0), (.018, .19, .19), 'AreaEnergy'),
        ('Square Badge Z', (0, .55, -.249), (.19, .19, .018), 'AreaEnergy'),
    ]


def create_square_variant(restyle=False):
    if not restyle:
        assert not SQUARE_VARIANT.exists() and not Path(str(SQUARE_VARIANT) + '.meta').exists(), 'Variant already exists; refusing to overwrite.'
    else:
        assert SQUARE_VARIANT.exists(), 'Restyle requires the existing Square variant.'
    energy_path = MATERIALS / 'AreaEnergy.mat'
    if not energy_path.exists():
        energy = (MATERIALS / 'Window.mat').read_text()
        energy = re.sub(r'(?m)^  m_Name: .*$', '  m_Name: SingleCellTower_AreaEnergy', energy)
        energy = energy.replace('  m_ValidKeywords: []', '  m_ValidKeywords:\n  - _EMISSION')
        energy = re.sub(r'(?m)^(    - _(?:BaseColor|Color): ).*$', r'\g<1>{r: 0.10, g: 0.85, b: 1, a: 1}', energy)
        energy = re.sub(r'(?m)^(    - _EmissionColor: ).*$', r'\g<1>{r: 0.15, g: 1.275, b: 1.5, a: 1}', energy)
        energy_path.write_text(energy, encoding='utf-8', newline='\n')
        guid = uuid.uuid4().hex
        Path(str(energy_path) + '.meta').write_text(
            f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n'
            '  externalObjects: {}\n  mainObjectFileID: 2100000\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')
    base_guid = re.search(r'guid: (\w+)', Path(str(TARGET) + '.meta').read_text())[1]
    base = parse(TARGET.read_text())
    body = ('PrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n'
            '  m_Modification:\n    serializedVersion: 3\n'
            '    m_TransformParent: {fileID: 0}\n    m_Modifications:\n')
    overrides = [
        (1000000, 'm_Name', SQUARE_VARIANT.stem),
        (1000002, 'attackArea', '0'),
        (1000002, 'usesMacroFootprint', '0'),
    ]
    for transform, (name, position, scale, material) in zip(children(base[1000001][1]), square_parts(), strict=True):
        go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', base[transform][1])[1])
        renderer = next(key for key, (kind, b) in base.items() if kind == 23 and f'm_GameObject: {{fileID: {go}}}' in b)
        overrides.append((go, 'm_Name', name))
        for axis, p, s in zip('xyz', position, scale):
            overrides.extend(((transform, f'm_LocalPosition.{axis}', str(p)),
                              (transform, f'm_LocalScale.{axis}', str(s))))
        material_guid = re.search(r'guid: (\w+)', (MATERIALS / (material + '.mat.meta')).read_text())[1]
        overrides.append((renderer, 'm_Materials.Array.data[0]', material_guid))
    for target, property_name, value in overrides:
        material = property_name == 'm_Materials.Array.data[0]'
        reference = f'{{fileID: 2100000, guid: {value}, type: 2}}' if material else '{fileID: 0}'
        body += (f'    - target: {{fileID: {target}, guid: {base_guid}, type: 3}}\n'
                 f'      propertyPath: {property_name}\n      value: {"" if material else value}\n'
                 f'      objectReference: {reference}\n')
    body += ('    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n'
             '    m_AddedGameObjects: []\n    m_AddedComponents: []\n'
             f'  m_SourcePrefab: {{fileID: 100100000, guid: {base_guid}, type: 3}}\n')
    SQUARE_VARIANT.write_text(serialize({100100001: (1001, body)}), encoding='utf-8', newline='\n')
    if not Path(str(SQUARE_VARIANT) + '.meta').exists():
        metadata(SQUARE_VARIANT)
    validate_square_variant()


def resolve_square_variant():
    text = SQUARE_VARIANT.read_text()
    docs = parse(text)
    assert len(docs) == 1 and next(iter(docs.values()))[0] == 1001
    base_guid = re.search(r'guid: (\w+)', Path(str(TARGET) + '.meta').read_text())[1]
    assert f'm_SourcePrefab: {{fileID: 100100000, guid: {base_guid}, type: 3}}' in text
    base = parse(TARGET.read_text())
    changes = re.findall(r'- target: \{fileID: (\d+), guid: (\w+), type: 3\}\n'
                         r'      propertyPath: ([^\n]+)\n      value: ([^\n]*)\n'
                         r'      objectReference: (\{[^\n]+\})', text)
    assert len(changes) == 163
    for key, guid, property_name, value, reference in changes:
        assert guid == base_guid and int(key) in base
        kind, body = base[int(key)]
        if property_name == 'm_Materials.Array.data[0]':
            material_guid = re.search(r'guid: (\w+)', reference)[1]
            assert any(f'guid: {material_guid}' in p.read_text() for p in MATERIALS.glob('*.mat.meta'))
            body, count = re.subn(r'(?m)^  - \{fileID: 2100000, guid: \w+, type: 2\}', '  - ' + reference, body)
        elif '.' in property_name:
            field_name, axis = property_name.split('.')
            assert field_name in ('m_LocalPosition', 'm_LocalScale') and axis in 'xyz'
            body, count = re.subn(r'(?m)(^  ' + field_name + r': \{[^\n]*?\b' + axis + r': )[^,}]+', lambda m: m[1] + value, body)
        else:
            body, count = re.subn(r'(?m)^  ' + property_name + ':.*$', '  ' + property_name + ': ' + value, body)
        assert count == 1, (key, property_name)
        base[int(key)] = kind, body
    assert all(f'    {key}: []' in text for key in ('m_RemovedComponents', 'm_RemovedGameObjects', 'm_AddedGameObjects', 'm_AddedComponents'))
    return base


def validate_square_variant():
    docs = resolve_square_variant()
    assert '  attackArea: 0\n' in docs[1000002][1] and '  usesMacroFootprint: 0\n' in docs[1000002][1]
    assert '  m_Name: Tower_Attack_1x1_Square\n' in docs[1000000][1]
    names = {re.search(r'^  m_Name: (.*)$', b, re.M)[1] for t, b in docs.values() if t == 1}
    assert all('Emitter ' + direction in names for direction in ('Negative X', 'Positive X', 'Negative Z', 'Positive Z'))
    assert 'Central Energy Core' in names and not any('Muzzle' in name or 'Cannon' in name for name in names)
    for body in (b for t, b in docs.values() if t == 4 and 'm_Father: {fileID: 1000001}' in b):
        p = [float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups()]
        s = [float(v) for v in re.search(r'm_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups()]
        assert all(abs(p[axis]) + s[axis] / 2 <= .5 for axis in (0, 2))
        assert p[1] - s[1] / 2 >= -1e-6
    validate()
    print('PASS Square Prefab Variant: four emitters, central core, square frame, valid visual overrides; Square rules and one-cell bounds preserved.')


def validate():
    text = TARGET.read_text()
    docs = parse(text)
    assert len(docs) == 83
    assert '  usesMacroFootprint: 0\n' in text and '  attackArea: 0\n' in text
    assert not any(kind in (65, 135, 136) for kind, _ in docs.values()), 'Unexpected Collider'
    for local in re.findall(r'\{fileID: (\d+)\}', text):
        assert int(local) == 0 or int(local) in docs
    transforms = [body for kind, body in docs.values() if kind == 4 and 'm_Father: {fileID: 1000001}' in body]
    minimum, maximum = [float('inf')] * 3, [float('-inf')] * 3
    for body in transforms:
        p = [float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups()]
        s = [float(v) for v in re.search(r'm_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups()]
        for axis in range(3):
            minimum[axis] = min(minimum[axis], p[axis] - s[axis] / 2)
            maximum[axis] = max(maximum[axis], p[axis] + s[axis] / 2)
    assert minimum[0] >= -.5 and maximum[0] <= .5 and minimum[2] >= -.5 and maximum[2] <= .5
    assert abs(minimum[1]) < 1e-6 and maximum[1] > 1.5
    print('PASS single-cell tower geometry bounds:', minimum, maximum)


if __name__ == '__main__':
    import sys
    if '--restyle-square' in sys.argv:
        create_square_variant(restyle=True)
    elif '--square-variant' in sys.argv:
        create_square_variant()
    elif '--validate' in sys.argv:
        validate()
        if SQUARE_VARIANT.exists():
            validate_square_variant()
    else:
        create()
