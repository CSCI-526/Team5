"""Replace only Level1-3 tower presentation; preserve authored combat and map data.

Uses current saved base/variant models, including user-added variant objects.
Produces visual-only prefabs so the original scene BattleTower remains authoritative.
"""
from pathlib import Path
import copy
import hashlib
import json
import math
import re
import sys

from create_level_prefab_kit import ROOT, parse, serialize, field, children, set_children, owner, metadata

FOLDER = ROOT / 'Assets/Rebuild/Prefabs/TowerVisuals'
REPORT = ROOT / 'Logs/CampaignTowerVisuals'
BASE = ROOT / 'Assets/Rebuild/Prefabs/LevelBuilding/Towers/Tower_Attack_1x1.prefab'
SQUARE = BASE.with_name('Tower_Attack_1x1_Square.prefab')
OLD = ROOT / 'Assets/Rebuild/Prefabs/Tower 1 (3x3).prefab'
SCENES = [ROOT / f'Assets/Rebuild/Scenes/Level{i}GridBattle.unity' for i in (1, 2, 3)]
MATCH = re.compile(r'(?ms)^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)')


def guid(path):
    return re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]


def ref(body, key):
    return int(re.search(r'^  ' + key + r': \{fileID: (\d+)\}', body, re.M)[1])


def modifications(body):
    return re.findall(r'- target: \{fileID: (\d+), guid: (\w+), type: 3\}\n'
                      r'      propertyPath: ([^\n]+)\n      value: ([^\n]*)\n'
                      r'      objectReference: (\{[^\n]+\})', body)


def apply_override(docs, key, prop, value, reference):
    kind, body = docs[key]
    prop = prop.strip("'")
    if prop == 'm_Materials.Array.data[0]':
        body, count = re.subn(r'(?m)^  - \{fileID: 2100000, guid: \w+, type: 2\}', '  - ' + reference, body)
    elif '.' in prop:
        name, axis = prop.split('.')
        assert name in ('m_LocalPosition', 'm_LocalScale', 'm_LocalRotation', 'm_LocalEulerAnglesHint')
        assert axis in 'xyzw'
        body, count = re.subn(r'(?m)(^  ' + name + r': \{[^\n]*?\b' + axis + r': )[^,}]+', lambda m: m[1] + value, body)
    else:
        body, count = re.subn(r'(?m)^  ' + prop + ':.*$', lambda m: '  ' + prop + ': ' + value, body)
    assert count == 1, (key, prop)
    docs[key] = kind, body


def resolve_square():
    docs = parse(BASE.read_text())
    text = SQUARE.read_text()
    variant = parse(text)
    instance = next(body for kind, body in variant.values() if kind == 1001)
    assert f'guid: {guid(BASE)}' in instance
    for key, source_guid, prop, value, reference in modifications(instance):
        assert source_guid == guid(BASE)
        apply_override(docs, int(key), prop, value, reference)
    aliases = {}
    stripped = set()
    for match in MATCH.finditer(text):
        if ' stripped\n' in match[0].split('Transform:', 1)[0]:
            key = int(match[2])
            aliases[key] = int(re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+)', match[3])[1])
            stripped.add(key)
    for key, (kind, body) in variant.items():
        if kind == 1001 or key in stripped:
            continue
        assert key not in docs
        body = re.sub(r'fileID: (\d+)', lambda m: 'fileID: ' + str(aliases.get(int(m[1]), int(m[1]))), body)
        docs[key] = kind, body
    additions = re.findall(r'targetCorrespondingSourceObject: \{fileID: (\d+), guid: (\w+), type: 3\}\n'
                           r'      insertIndex: (-?\d+)\n      addedObject: \{fileID: (\d+)\}', instance)
    for parent, source_guid, index, added in additions:
        parent, added, index = int(parent), int(added), int(index)
        assert source_guid == guid(BASE) and docs[added][0] == 4
        roots = children(docs[parent][1])
        roots.insert(index if index >= 0 else len(roots), added)
        docs[parent] = 4, set_children(docs[parent][1], roots)
    assert 'm_RemovedComponents: []' in instance and 'm_RemovedGameObjects: []' in instance
    return docs


def visual_prefab(docs, name):
    docs = copy.deepcopy(docs)
    root_go, root_transform = 1000000, 1000001
    tower_ids = [key for key, (_, b) in docs.items() if 'Team5.Rebuild.World.BattleTower' in b]
    assert len(tower_ids) == 1
    component = tower_ids[0]
    kind, body = docs[root_go]
    body = re.sub(r'^  - component: \{fileID: ' + str(component) + r'\}\n', '', body, flags=re.M)
    docs[root_go] = kind, field(body, 'm_Name', name)
    del docs[component]
    docs[root_transform] = 4, field(docs[root_transform][1], 'm_LocalPosition', '{x: 0, y: 0, z: 0}')
    assert not any(kind == 114 for kind, _ in docs.values())
    path = FOLDER / (name + '.prefab')
    assert not path.exists()
    path.write_text(serialize(docs), encoding='utf-8', newline='\n')
    metadata(path)
    return path


def make_instance(path, instance_id, transform_id, parent, yaw):
    source_guid = guid(path)
    body = ('PrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n'
            '  m_Modification:\n    serializedVersion: 3\n'
            f'    m_TransformParent: {{fileID: {parent}}}\n    m_Modifications:\n')
    values = [(1000000, 'm_Name', path.stem)]
    for axis in 'xyz':
        values.append((1000001, 'm_LocalPosition.' + axis, '0'))
    quat = (0, math.sin(math.radians(yaw) / 2), 0, math.cos(math.radians(yaw) / 2))
    values += [(1000001, 'm_LocalRotation.' + axis, f'{v:.9g}') for axis, v in zip('xyzw', quat)]
    values += [(1000001, 'm_LocalEulerAnglesHint.' + axis, str(yaw if axis == 'y' else 0)) for axis in 'xyz']
    for target, prop, value in values:
        body += (f'    - target: {{fileID: {target}, guid: {source_guid}, type: 3}}\n'
                 f'      propertyPath: {prop}\n      value: {value}\n'
                 '      objectReference: {fileID: 0}\n')
    body += ('    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n'
             '    m_AddedGameObjects: []\n    m_AddedComponents: []\n'
             f'  m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}\n')
    stripped = ('Transform:\n'
                f'  m_CorrespondingSourceObject: {{fileID: 1000001, guid: {source_guid}, type: 3}}\n'
                f'  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n')
    return f'--- !u!1001 &{instance_id}\n{body}--- !u!4 &{transform_id} stripped\n{stripped}'


def main():
    assert not FOLDER.exists(), 'Already migrated; refusing to overwrite visual prefabs.'
    REPORT.mkdir(parents=True, exist_ok=True)
    protected = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in (BASE, SQUARE, OLD)}
    FOLDER.mkdir()
    metadata(FOLDER, folder=True)
    direct = visual_prefab(parse(BASE.read_text()), 'DirectionalTower_Visual')
    square = visual_prefab(resolve_square(), 'SquareTower_Visual')
    old_docs = parse(OLD.read_text())
    reports = []
    for level, scene in enumerate(SCENES, 1):
        original = scene.read_text()
        (REPORT / scene.name).write_bytes(scene.read_bytes())
        docs = parse(original)
        edits, additions, allow_changed = {}, [], set()
        session = next(b for _, b in docs.values() if 'Team5.Rebuild.Session::Team5.Rebuild.Session.BattleSession' in b)
        tower_section = re.search(r'^  towers:(.*?)^  unitCount:', session, re.M | re.S)[1]
        tower_ids = [int(v) for v in re.findall(r'fileID: (\d+)', tower_section) if v != '0']
        tower_reports = []
        for index, tower_id in enumerate(tower_ids):
            _, tower = docs[tower_id]
            prefab_id = ref(tower, 'm_PrefabInstance')
            instance_id = 1600000000000 + level * 10000 + index * 10
            visual_transform = instance_id + 1
            assert instance_id not in docs and visual_transform not in docs
            if prefab_id:
                # Level1's original authored tower is already an instance of the old 3x3 prefab.
                instance = docs[prefab_id][1]
                assert f'm_SourcePrefab: {{fileID: 100100000, guid: {guid(OLD)}, type: 3}}' in instance
                effective = copy.deepcopy(old_docs)
                for key, source_guid, prop, value, reference in modifications(instance):
                    assert source_guid == guid(OLD)
                    apply_override(effective, int(key), prop, value, reference)
                source_tower = int(re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+)', tower)[1])
                tower = effective[source_tower][1]
                go = owner(tower)
                source_transform = next(k for k, (t, b) in effective.items() if t == 4 and owner(b) == go)
                parent = next(k for k, (t, b) in docs.items() if t == 4 and
                              f'm_CorrespondingSourceObject: {{fileID: {source_transform},' in b and
                              f'm_PrefabInstance: {{fileID: {prefab_id}}}' in b)
                transform = effective[source_transform][1]
                old_child = children(transform)
                assert len(old_child) == 1
                mesh_go = owner(effective[old_child[0]][1])
                override = (f'    - target: {{fileID: {mesh_go}, guid: {guid(OLD)}, type: 3}}\n'
                            '      propertyPath: m_IsActive\n      value: 0\n      objectReference: {fileID: 0}\n')
                updated = instance.replace('    m_RemovedComponents:', override + '    m_RemovedComponents:', 1)
                assert '    m_AddedGameObjects: []' in updated
                updated = updated.replace('    m_AddedGameObjects: []',
                    f'    m_AddedGameObjects:\n    - targetCorrespondingSourceObject: {{fileID: {source_transform}, guid: {guid(OLD)}, type: 3}}\n'
                    f'      insertIndex: -1\n      addedObject: {{fileID: {visual_transform}}}')
                edits[prefab_id] = updated
                allow_changed.add(prefab_id)
            else:
                go = owner(tower)
                parent = next(k for k, (t, b) in docs.items() if t == 4 and owner(b) == go)
                transform = docs[parent][1]
                old_child = children(transform)
                assert len(old_child) == 1
                mesh_go = owner(docs[old_child[0]][1])
                edits[mesh_go] = field(docs[mesh_go][1], 'm_IsActive', '0')
                edits[parent] = set_children(transform, old_child + [visual_transform])
                allow_changed.update((mesh_go, parent))
            assert '  usesMacroFootprint: 1\n' in tower
            area = int(re.search(r'^  attackArea: (\d+)', tower, re.M)[1])
            yaw = {0: 0, 1: 180, 2: 0, 3: 90, 4: -90}[area]
            chosen = square if area == 0 else direct
            additions.append(make_instance(chosen, instance_id, visual_transform, parent, yaw))
            parameters = {key: re.search(r'^  ' + key + r': (.*)$', tower, re.M)[1] for key in
                          ('range', 'damage', 'attackInterval', 'targetRule', 'usesMacroFootprint', 'maxHp', 'attackArea', 'attackWidth')}
            tower_reports.append(dict(towerId=tower_id, parent=parent, visual=chosen.stem, yaw=yaw, parameters=parameters,
                                      position=re.search(r'm_LocalPosition: (.*)', transform)[1], instanceId=instance_id))
        assert len(tower_ids) == {1: 1, 2: 2, 3: 7}[level]
        def replace(match):
            key = int(match[2])
            if key not in edits:
                return match[0]
            return match[0][:match[0].find('\n')+1] + edits[key]
        changed = MATCH.sub(replace, original)
        changed = changed.rstrip() + '\n' + ''.join(additions)
        after = parse(changed)
        assert all(after[k] == v for k, v in docs.items() if k not in allow_changed)
        assert all(after[k] == docs[k] for k in tower_ids)
        for local in re.findall(r'\{fileID: (-?\d+)\}', changed):
            assert int(local) == 0 or int(local) in after, ('Broken local reference', scene.name, local)
        for info in tower_reports:
            assert f'm_TransformParent: {{fileID: {info["parent"]}}}' in after[info['instanceId']][1]
            assert '      propertyPath: m_LocalPosition.y\n      value: 0' in after[info['instanceId']][1]
        assert len(MATCH.findall(changed)) == len(after)
        scene.write_text(changed, encoding='utf-8', newline='\n')
        reports.append(dict(scene=scene.name, towers=tower_reports, changedExistingBlocks=sorted(allow_changed),
                            preservedExistingBlocks=len(docs)-len(allow_changed), beforeHash=hashlib.sha256(original.encode()).hexdigest(),
                            afterHash=hashlib.sha256(changed.encode()).hexdigest()))
    assert all(hashlib.sha256(p.read_bytes()).hexdigest() == digest for p, digest in protected.items())
    report = dict(levels=reports, sourceHashes={str(p.relative_to(ROOT)): digest for p, digest in protected.items()},
                  squareVisualRenderers=sum(t == 23 for t, _ in parse(square.read_text()).values()),
                  directionalVisualRenderers=sum(t == 23 for t, _ in parse(direct.read_text()).values()))
    (REPORT / 'Validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    for level in reports:
        print('PASS', level['scene'], len(level['towers']), 'tower visuals; combat components, map, route, camera and UI unchanged.')
    print('PASS source model prefabs untouched; user-added Square badges included; all scene references resolve.')


if __name__ == '__main__':
    main()
