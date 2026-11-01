"""One-time LevelTemplate scene authoring from saved Level2; never overwrite scenes."""
from pathlib import Path
import hashlib
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Rebuild/Scenes/Level2GridBattle.unity'
TARGET = SOURCE.with_name('LevelTemplate.unity')
assert not TARGET.exists() and not Path(str(TARGET) + '.meta').exists(), 'Template already exists.'
before = SOURCE.read_bytes()
text = SOURCE.read_text()
text = re.sub(r'(?m)^(  m_Name: .*?)Level 2', r'\g<1>Template', text)
text, count = re.subn(r'(?m)^  displayedLevelNumber: 2$', '  displayedLevelNumber: 1', text)
assert count == 1 and '  autoStart: 1\n' in text
assert 'TestLevelControls' not in text and 'LevelOneTutorial' not in text
TARGET.write_text(text, encoding='utf-8', newline='\n')
Path(str(TARGET) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n', encoding='utf-8', newline='\n')
assert SOURCE.read_bytes() == before
report = ROOT / 'Logs/LevelAuthoringStage1'
report.mkdir(parents=True, exist_ok=True)
(report / 'TemplateSource.json').write_text(json.dumps({
    'source': str(SOURCE.relative_to(ROOT)),
    'sourceSHA256': hashlib.sha256(before).hexdigest(),
    'template': str(TARGET.relative_to(ROOT)),
    'changes': 'Object display names and displayed level number only; camera, map and combat configuration preserved.'
}, indent=2), encoding='utf-8')
print('Created standalone LevelTemplate from Level2; existing scene and camera data unchanged.')
