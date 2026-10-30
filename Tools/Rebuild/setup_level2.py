"""One-time Scene import; old Team5 and the preserved backup are read-only."""
from pathlib import Path
import re
import uuid
import hashlib

root = Path(__file__).resolve().parents[2]
assert root.name == 'Team5-Rebuild-Clean'
scenes = root / 'Assets/Rebuild/Scenes'
level2 = scenes / 'Level2GridBattle.unity'
settlement = scenes / 'LevelSettlement.unity'
assert not level2.exists() and not settlement.exists(), 'Do not overwrite edited scenes.'
protected = [scenes / 'Level1GridBattle.unity', scenes / 'UpgradeScene.unity',
             root / 'Logs/CompactLevel1Backup/Level1GridBattle.unity']
hashes = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}

def meta(path):
    guid = uuid.uuid4().hex
    Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\n')
    return guid

text = protected[-1].read_text()
text = text.replace('  - component: {fileID: 884410001}\n', '')
text = re.sub(r'(?ms)^--- !u!114 &884410001\n.*?(?=^--- !u!|\Z)', '', text)
text = text.replace('c7fea7eb60294bfa83cb342d4b00af06', '57e190ca22f2491db6c91c21a8dcb6a4')
text = text.replace('  useSceneRosterOnly: 1', '  useSceneRosterOnly: 0')
text = text.replace('  displayedLevelNumber: 1', '  displayedLevelNumber: 2\n  levelTimeMultiplier: 1')
text = text.replace('Level 1', 'Level 2')
# The authored HUD level title is updated before the first frame as well.
text = text.replace('m_text: LEVEL 1', 'm_text: LEVEL 2')
level2.write_text(text)
level_guid = meta(level2)

script = root / 'Assets/Rebuild/Scripts/Presentation/Minimal/LegacyUI/LegacyLevelSettlement.cs'
script_guid = meta(script)
text = (root.parent / 'Team5/Assets/Scenes/Level2Intro.unity').read_text()
text = text.replace('bbc02109d0338d4459d0d90fbd3a4a6f', script_guid)
text = text.replace('Assembly-CSharp::Level2IntroUI',
    'Team5.Rebuild.Presentation::Team5.Rebuild.Presentation.LegacyUI.LegacyLevelSettlement')
text = text.replace('Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null',
    'Team5.Rebuild.Presentation, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null')
text = text.replace('Level2IntroUI, Assembly-CSharp',
    'Team5.Rebuild.Presentation.LegacyUI.LegacyLevelSettlement, Team5.Rebuild.Presentation')
text = re.sub(r'^  offenseData:.*\n  completedLevel:.*\n', '  missionHeading: {fileID: 739551403}\n', text, flags=re.M)
# No combat currency or shared legacy OffenseData is imported.
text = text.replace('m_text: LEVEL 1 REWARD', 'm_text: LEVEL 1 COMPLETE')
text = text.replace('m_text: "+100 GOLD\\n+1 TANK UNIT"', 'm_text: LEVEL 2')
text = text.replace('m_text: Complete Level 1 to earn these rewards.',
    'm_text: Choose units from storage and prepare your squad for the next level.')
settlement.write_text(text)
settlement_guid = meta(settlement)

settings = root / 'ProjectSettings/EditorBuildSettings.asset'
text = settings.read_text()
extra = ''.join(f'  - enabled: 1\n    path: Assets/Rebuild/Scenes/{p.name}\n    guid: {g}\n'
                for p, g in [(level2, level_guid), (settlement, settlement_guid)])
text = text.replace('  m_configObjects:', extra + '  m_configObjects:')
settings.write_text(text)
compile_project = root / 'Logs/WarehouseChecks/Compile.csproj'
text = compile_project.read_text().replace('  <ItemGroup>',
    '  <ItemGroup>\n    <Compile Include="../../Assets/Rebuild/Scripts/Presentation/Minimal/LegacyUI/LegacyLevelSettlement.cs" />', 1)
compile_project.write_text(text)
for p, digest in hashes.items():
    assert hashlib.sha256(p.read_bytes()).hexdigest() == digest, p
print('Created Level2 and imported settlement. Level1, UpgradeScene and backup unchanged.')
