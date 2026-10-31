"""Export a reusable level kit from saved Level2 objects; never edits source scenes.

Run with --validate to check existing outputs without writing files.
Existing output directories are refused during creation.
"""
from pathlib import Path
import copy
import hashlib
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
KIT = ROOT / 'Assets/Rebuild/Prefabs/LevelBuilding'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
BLOCK = re.compile(r'(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)')


def parse(text):
    result = {}
    for match in BLOCK.finditer(text):
        key = int(match[2])
        assert key not in result, ('Duplicate fileID', key)
        result[key] = (int(match[1]), match[3].rstrip() + '\n')
    return result


def field(body, name, value):
    body, count = re.subn(r'(?m)^  ' + re.escape(name) + r':.*$', f'  {name}: {value}', body)
    assert count == 1, (name, count)
    return body


def children(body):
    section = re.search(r'(?ms)^  m_Children:(.*?)^  m_Father:', body)[1]
    return [int(v) for v in re.findall(r'fileID: (\d+)', section)]


def set_children(body, ids):
    lines = '\n' + ''.join(f'  - {{fileID: {key}}}\n' for key in ids) if ids else ' []\n'
    return re.sub(r'(?ms)^  m_Children:.*?(?=^  m_Father:)', '  m_Children:' + lines, body)


def owner(body):
    return int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body)[1])


def subtree(docs, transform):
    selected = {}
    def visit(t):
        go = owner(docs[t][1])
        selected[go] = docs[go]
        for component in re.findall(r'component: \{fileID: (\d+)\}', docs[go][1]):
            key = int(component)
            selected[key] = docs[key]
        for child in children(docs[t][1]):
            visit(child)
    visit(transform)
    return selected


def change(docs, key, name, value):
    kind, body = docs[key]
    docs[key] = kind, field(body, name, value)


def serialize(docs):
    return HEADER + ''.join(f'--- !u!{kind} &{key}\n{body}' for key, (kind, body) in docs.items())


def remap(docs, start):
    mapping = {old: start + index for index, old in enumerate(docs)}
    def ref(match):
        old = int(match[1])
        return 'fileID: ' + str(mapping.get(old, old))
    return {mapping[key]: (kind, re.sub(r'fileID: (-?\d+)', ref, body))
            for key, (kind, body) in docs.items()}, mapping


def group(name):
    return {
        1000000: (1, 'GameObject:\n  m_ObjectHideFlags: 0\n'
                  '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
                  '  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n'
                  '  m_Component:\n  - component: {fileID: 1000001}\n  m_Layer: 0\n'
                  f'  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n'
                  '  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'),
        1000001: (4, 'Transform:\n  m_ObjectHideFlags: 0\n'
                  '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
                  '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 1000000}\n'
                  '  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n'
                  '  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n'
                  '  m_ConstrainProportionsScale: 0\n  m_Children: []\n'
                  '  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n')}


def place(docs, transform, name, position=(0, 0, 0), parent=0):
    change(docs, owner(docs[transform][1]), 'm_Name', name)
    change(docs, transform, 'm_Father', f'{{fileID: {parent}}}')
    x, y, z = position
    change(docs, transform, 'm_LocalPosition', f'{{x: {x}, y: {y}, z: {z}}}')


def metadata(path, folder=False):
    guid = uuid.uuid4().hex
    text = f'fileFormatVersion: 2\nguid: {guid}\n'
    text += ('folderAsset: yes\nDefaultImporter:\n' if folder else
             'PrefabImporter:\n' if path.suffix == '.prefab' else 'DefaultImporter:\n')
    text += '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    Path(str(path) + '.meta').write_text(text, encoding='utf-8', newline='\n')


def write_prefab(relative, docs):
    path = KIT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(serialize(docs), encoding='utf-8', newline='\n')
    metadata(path)


def build():
    assert not KIT.exists(), 'Kit already exists. Use --validate; never overwrite user edits.'
    protected = [* (ROOT / 'Assets/Rebuild/Scenes').glob('*.unity'),
                 * (ROOT / 'Assets/Rebuild/Prefabs').glob('*.prefab')]
    hashes = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    source = parse((ROOT / 'Assets/Rebuild/Scenes/Level2GridBattle.unity').read_text(encoding='utf-8-sig'))
    scene_root = next(k for k, (t, b) in source.items() if t == 4 and 'm_Father: {fileID: 0}' in b)
    source_cell = next(b for _, b in source.values() if 'Team5.Rebuild.World.BattleCell' in b and '  kind: 3\n' in b)
    cell_go = owner(source_cell)
    cell_transform = next(k for k, (t, b) in source.items() if t == 4 and owner(b) == cell_go)
    base_cell = subtree(source, cell_transform)
    cell_component = next(k for k, (_, b) in base_cell.items() if 'Team5.Rebuild.World.BattleCell' in b)
    material_holder = next(k for k, (t, _) in base_cell.items() if t == 23)
    kinds = ['Ground', 'Road', 'Obstacle', 'TowerPlatform', 'Spawn', 'Goal']
    macro_docs = {}
    for kind, name in enumerate(kinds):
        cell = copy.deepcopy(base_cell)
        place(cell, cell_transform, name + '_Cell')
        change(cell, cell_component, 'kind', kind)
        mat = (ROOT / f'Assets/Rebuild/Materials/Minimal/{name}.mat.meta').read_text()
        guid = re.search(r'guid: (\w+)', mat)[1]
        t, body = cell[material_holder]
        body, count = re.subn(r'(?m)^(  - \{fileID: 2100000, guid: )\w+', r'\g<1>' + guid, body)
        assert count == 1
        cell[material_holder] = t, body
        write_prefab(f'Tiles/{name}_Cell.prefab', cell)
        block = group(name + '_3x3')
        roots = []
        for slot in range(9):
            clone, mapping = remap(cell, 1100000 + slot * 100)
            root = mapping[cell_transform]
            x, z = slot % 3 - 1, slot // 3 - 1
            place(clone, root, f'{name} ({x},0,{z})', (x, 0, z), 1000001)
            roots.append(root)
            block.update(clone)
        block[1000001] = 4, set_children(block[1000001][1], roots)
        macro_docs[name] = block
        write_prefab(f'Blocks/{name}_3x3.prefab', block)

    tower_component = 8000009101
    tower_go = owner(source[tower_component][1])
    tower_transform = int(re.search(r'component: \{fileID: (\d+)\}', source[tower_go][1])[1])
    for area, direction in enumerate(['Square', 'PositiveX', 'NegativeX', 'PositiveZ', 'NegativeZ']):
        tower = subtree(source, tower_transform)
        name = 'Tower_' + direction
        place(tower, tower_transform, name)
        change(tower, tower_component, 'attackArea', area)
        change(tower, tower_component, 'range', 3.5 if area == 0 else 9)
        write_prefab(f'Towers/{name}.prefab', tower)
        combined = copy.deepcopy(macro_docs['TowerPlatform'])
        change(combined, 1000000, 'm_Name', name + '_WithPlatform')
        clone, mapping = remap(tower, 1200000)
        root = mapping[tower_transform]
        place(clone, root, name, (0, 0.25, 0), 1000001)
        combined[1000001] = 4, set_children(combined[1000001][1], children(combined[1000001][1]) + [root])
        combined.update(clone)
        write_prefab(f'TowerPlatforms/{name}_WithPlatform.prefab', combined)

    starter = subtree(source, scene_root)
    place(starter, scene_root, 'LevelStarter_3D')
    session = next(k for k, (_, b) in starter.items() if 'Team5.Rebuild.Session::Team5.Rebuild.Session.BattleSession' in b)
    change(starter, session, 'displayedLevelNumber', 4)
    change(starter, owner(starter[session][1]), 'm_Name', 'BattleSession')
    write_prefab('Maps/LevelStarter_3D.prefab', starter)

    rig = copy.deepcopy(starter)
    grid = 2033248477
    tower_container = 176265812
    border_container = 347706729
    for transform in (grid, tower_container, border_container):
        rig[transform] = 4, set_children(rig[transform][1], [])
    route = 1034538724
    for component, key, next_key in ((session, 'towers', 'unitCount'), (route, 'cells', 'requiredWidth')):
        t, body = rig[component]
        body, count = re.subn(r'(?ms)^  ' + key + r':.*?(?=^  ' + next_key + r':)', '  ' + key + ': []\n', body)
        assert count == 1
        rig[component] = t, body
    place(rig, scene_root, 'BattleRig_3D')
    write_prefab('Systems/BattleRig_3D.prefab', subtree(rig, scene_root))

    border_child = children(source[border_container][1])[0]
    decorative = subtree(source, border_child)
    place(decorative, border_child, 'DecorativeBorder_Cell')
    write_prefab('Decoration/DecorativeBorder_Cell.prefab', decorative)
    for folder in [KIT, *sorted(p for p in KIT.rglob('*') if p.is_dir())]:
        metadata(folder, folder=True)
    assert all(hashlib.sha256(p.read_bytes()).hexdigest() == digest for p, digest in hashes.items())
    from create_single_cell_tower import create
    create()
    print('Created 27 prefabs; all existing scenes and prefabs unchanged.')


def validate():
    outputs = sorted(KIT.rglob('*.prefab'))
    assert len(outputs) == 27, len(outputs)
    guid_paths = {}
    for path in (ROOT / 'Assets').rglob('*.meta'):
        guid = re.search(r'(?m)^guid: (\w+)', path.read_text(encoding='utf-8-sig'))[1]
        assert re.fullmatch(r'[a-f0-9]{32}', guid), path
        assert guid not in guid_paths, ('Duplicate GUID', path, guid_paths.get(guid))
        guid_paths[guid] = path
    package_guids = None
    for path in outputs:
        if path.stem == 'Tower_Attack_1x1_Square':
            from create_single_cell_tower import validate_square_variant
            validate_square_variant()
            print('PASS', path.relative_to(ROOT), '(Prefab Variant)')
            continue
        text = path.read_text()
        docs = parse(text)
        assert len(docs) == len(BLOCK.findall(text))
        for local in re.findall(r'\{fileID: (-?\d+)\}', text):
            assert int(local) == 0 or int(local) in docs, (path, 'Broken local reference', local)
        roots = [k for k, (t, b) in docs.items() if t in (4, 224) and 'm_Father: {fileID: 0}' in b]
        assert len(roots) == 1, (path, roots)
        assert len(subtree(docs, roots[0])) == len(docs), ('Disconnected objects', path)
        for guid in set(re.findall(r'guid: ([a-f0-9]{32})', text)):
            if guid.startswith('0000000000000000') or guid in guid_paths:
                continue
            if package_guids is None:
                package_guids = {m[1] for p in (ROOT / 'Library/PackageCache').rglob('*.meta')
                                 if (m := re.search(r'(?m)^guid: (\w+)', p.read_text(encoding='utf-8-sig')))}
            assert guid in package_guids, (path, 'Missing external resource', guid)
        cell_entries = [(k, b) for k, (_, b) in docs.items() if 'Team5.Rebuild.World.BattleCell' in b]
        if path.parent.name in ('Tiles', 'Blocks', 'TowerPlatforms'):
            expected = 1 if path.parent.name == 'Tiles' else 9
            assert len(cell_entries) == expected, path
            cells = []
            for _, body in cell_entries:
                go = owner(body)
                transform = next(b for t, b in docs.values() if t == 4 and owner(b) == go)
                pos = tuple(float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', transform).groups())
                assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in transform
                assert 'm_LocalRotation: {x: 0, y: 0, z: 0, w: 1}' in transform
                cells.append(pos)
                if path.parent.name == 'TowerPlatforms':
                    assert '  kind: 3\n' in body
            assert set(cells) == ({(0, 0, 0)} if expected == 1 else {(x, 0, z) for x in (-1, 0, 1) for z in (-1, 0, 1)}), path
        if path.parent.name in ('Maps', 'Systems'):
            validate_level(docs, path.parent.name == 'Systems')
        if path.parent.name in ('Towers', 'TowerPlatforms'):
            towers = [b for _, b in docs.values() if 'Team5.Rebuild.World.BattleTower' in b]
            assert len(towers) == 1, path
            if path.stem == 'Tower_Attack_1x1':
                assert '  usesMacroFootprint: 0\n' in towers[0], path
                from create_single_cell_tower import validate as validate_single_cell
                validate_single_cell()
                print('PASS', path.relative_to(ROOT), f'({len(docs)} serialized objects)')
                continue
            assert '  usesMacroFootprint: 1\n' in towers[0], path
            direction = path.stem.removeprefix('Tower_').removesuffix('_WithPlatform')
            area = ['Square', 'PositiveX', 'NegativeX', 'PositiveZ', 'NegativeZ'].index(direction)
            assert f'  attackArea: {area}\n' in towers[0], path
        print('PASS', path.relative_to(ROOT), f'({len(docs)} serialized objects)')
    print('PASS all 27 assets: closed hierarchy, unique IDs/GUIDs, local/resource bindings, tile types, footprints, coordinates and variant inheritance.')


def validate_level(docs, blank):
    def component(name):
        return next((k, b) for k, (_, b) in docs.items() if f'::{name}\n' in b)
    def reference(body, key):
        return int(re.search(r'^  ' + key + r': \{fileID: (\d+)\}', body, re.M)[1])
    def sequence(body, key, end):
        section = re.search(r'^  ' + key + r':(.*?)^  ' + end + ':', body, re.M | re.S)[1]
        return [int(v) for v in re.findall(r'fileID: (\d+)', section)]
    def position(body):
        return tuple(float(v) for v in re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', body).groups())
    transforms = {owner(b): b for t, b in docs.values() if t in (4, 224)}
    def world_position(go):
        body = transforms[go]
        p = position(body)
        parent = reference(body, 'm_Father')
        if parent == 0:
            return p
        ancestor = docs[parent][1]
        assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in ancestor
        assert 'm_LocalRotation: {x: 0, y: 0, z: 0, w: 1}' in ancestor
        q = world_position(owner(ancestor))
        return tuple(a + b for a, b in zip(p, q))
    session_id, session = component('Team5.Rebuild.Session.BattleSession')
    grid_id, _ = component('Team5.Rebuild.World.BattleGrid')
    route_id, route = component('Team5.Rebuild.World.SceneRoute')
    _, presentation = component('Team5.Rebuild.Presentation.BattlePresentation')
    _, hud = component('Team5.Rebuild.Presentation.LegacyUI.LegacyBattleHud')
    assert reference(session, 'grid') == grid_id and reference(session, 'route') == route_id
    assert reference(hud, 'session') == reference(presentation, 'session') == session_id
    assert 'UnityEngine.UI.Slider' in docs[reference(hud, 'speedSlider')][1]
    # Current HUD permits the old Edit Formation button to be removed;
    # squad/skill controls are created by LegacyBattleHud at runtime.
    edit_button = reference(hud, 'editButton')
    if edit_button:
        assert 'UnityEngine.UI.Button' in docs[edit_button][1]
    assert 'unitPrefab: {fileID: 0}' not in presentation
    assert '  formationSize: 3\n' in session and '  squadCombat: 1\n' in session
    assert '  requiredWidth: 3\n' in route and '  displayedLevelNumber: 4\n' in session
    assert sum(t == 20 for t, _ in docs.values()) == 1
    assert not any('LevelOneTutorial' in b for _, b in docs.values())
    assert not any('BattleSceneFlow' in b for _, b in docs.values())
    cell_refs, occupancy = {}, {}
    for key, (_, body) in docs.items():
        if 'Team5.Rebuild.World.BattleCell' not in body:
            continue
        p = world_position(owner(body))
        assert p[0].is_integer() and (p[1] * 2).is_integer() and p[2].is_integer()
        assert p not in occupancy
        kind = int(re.search(r'^  kind: (\d+)', body, re.M)[1])
        occupancy[p] = kind
        cell_refs[key] = p
    road = [cell_refs[k] for k in sequence(route, 'cells', 'requiredWidth')]
    tower_ids = sequence(session, 'towers', 'unitCount')
    if blank:
        assert not occupancy and not road and not tower_ids
        return
    assert len(occupancy) == 280 and len(road) == 24 and len(tower_ids) == 2
    assert len(set(road)) == len(road)
    assert occupancy[road[0]] == 4 and occupancy[road[-1]] == 5
    for p in road:
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                assert occupancy.get((p[0] + dx, p[1], p[2] + dz)) in (1, 4, 5)
    for a, b in zip(road, road[1:]):
        assert a[1] == b[1] and abs(a[0] - b[0]) + abs(a[2] - b[2]) == 1
    footprints = set()
    for key in tower_ids:
        body = docs[key][1]
        assert 'Team5.Rebuild.World.BattleTower' in body
        x, y, z = world_position(owner(body))
        assert x % 3 == z % 3 == 1 and y == 0.25
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                p = (x + dx, 0, z + dz)
                assert p not in footprints and occupancy.get(p) == 3
                footprints.add(p)


if __name__ == '__main__':
    if '--validate' not in sys.argv:
        build()
    validate()
