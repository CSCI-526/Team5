"""Create an isolated title/preparation/empty-battle scene set; preserve source scenes."""
from pathlib import Path
import hashlib
import json
import re
import sys
import uuid

from create_level_prefab_kit import ROOT, parse, serialize, field

SCENES = ROOT / 'Assets/Rebuild/Scenes'
CONTROL = ROOT / 'Assets/Rebuild/Scripts/Presentation/Minimal/TestLevelControls.cs'
REPORT = ROOT / 'Logs/TestSceneFlow'


def write_meta(path, script=False):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    guid = uuid.uuid4().hex
    text = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if script:
        text += ('MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
                 '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    meta.write_text(text, encoding='utf-8', newline='\n')
    return guid


def main():
    names = ['TestStartScene', 'TestUpdateScene', 'TestLevel']
    assert all(not (SCENES / (n + '.unity')).exists() and not (SCENES / (n + '.unity.meta')).exists() for n in names)
    protected = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in SCENES.glob('*.unity')}
    control_guid = write_meta(CONTROL, script=True)
    start = (SCENES / 'StartGame.unity').read_text()
    start = start.replace("  m_text: 'Reverse Tower Defense", "  m_text: 'Test Mode")
    start = start.replace("  m_text: 'Start Game", "  m_text: 'Start Test")
    (SCENES / 'TestStartScene.unity').write_text(start, encoding='utf-8', newline='\n')
    (SCENES / 'TestUpdateScene.unity').write_bytes((SCENES / 'UpgradeScene.unity').read_bytes())
    rig = parse((ROOT / 'Assets/Rebuild/Prefabs/LevelBuilding/Systems/BattleRig_3D.prefab').read_text())
    source = parse((SCENES / 'Level2GridBattle.unity').read_text())
    globals = {k: v for k, v in source.items() if v[0] in (29, 104, 157, 196, 1660057539)}
    session_id, session = next((k, b) for k, (_, b) in rig.items() if 'Team5.Rebuild.Session::Team5.Rebuild.Session.BattleSession' in b)
    session = field(session, 'autoStart', '0')
    session = field(session, 'displayedLevelNumber', '1')
    rig[session_id] = 114, session
    session_go = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', session)[1])
    rig[1499248550] = 1, field(rig[1499248550][1], 'm_Name', 'TestLevel')
    rig[session_go] = 1, field(rig[session_go][1], 'm_Name', 'Test BattleSession')
    new_component = 1700001234
    assert new_component not in rig
    kind, body = rig[session_go]
    body = body.replace(f'  - component: {{fileID: {session_id}}}\n',
                        f'  - component: {{fileID: {session_id}}}\n  - component: {{fileID: {new_component}}}\n')
    rig[session_go] = kind, body
    rig[new_component] = 114, ('MonoBehaviour:\n  m_ObjectHideFlags: 0\n'
        '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
        f'  m_GameObject: {{fileID: {session_go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        f'  m_Script: {{fileID: 11500000, guid: {control_guid}, type: 3}}\n  m_Name: \n'
        '  m_EditorClassIdentifier: Team5.Rebuild.Presentation::Team5.Rebuild.Presentation.TestLevelControls\n'
        f'  session: {{fileID: {session_id}}}\n')
    globals.update(rig)
    (SCENES / 'TestLevel.unity').write_text(serialize(globals), encoding='utf-8', newline='\n')
    settings = ROOT / 'ProjectSettings/EditorBuildSettings.asset'
    entries = ''
    for name in names:
        path = SCENES / (name + '.unity')
        guid = write_meta(path)
        docs = parse(path.read_text())
        for local in re.findall(r'\{fileID: (-?\d+)\}', path.read_text()):
            assert int(local) == 0 or int(local) in docs, (name, local)
        entries += f'  - enabled: 1\n    path: Assets/Rebuild/Scenes/{name}.unity\n    guid: {guid}\n'
    settings.write_text(settings.read_text().replace('  m_configObjects:', entries + '  m_configObjects:'), encoding='utf-8', newline='\n')
    assert all(hashlib.sha256(p.read_bytes()).hexdigest() == digest for p, digest in protected.items())
    assert not any('Team5.Rebuild.World.BattleCell' in b or 'Team5.Rebuild.World.BattleTower' in b for _, b in rig.values())
    assert '  cells: []' in rig[1034538724][1] and '  towers: []' in session and '  autoStart: 0' in session
    REPORT.mkdir(parents=True, exist_ok=True)
    (REPORT / 'OriginalSceneHashes.json').write_text(json.dumps({str(p.relative_to(ROOT)): d for p, d in protected.items()}, indent=2), encoding='utf-8')
    print('PASS three test scenes created; TestLevel has no cells, route nodes or towers; all existing scenes unchanged.')


if __name__ == '__main__':
    main()
