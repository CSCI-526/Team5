"""Copy the Paint Squad Wars art (owner's own project, ~/TactileWars) into Assets/Rebuild/PaintSquad.

Reads the source project from disk only. Copies each asset with its .meta so import settings, material
remaps and GUID references survive. The three VFX prefabs are rebuilt on the way: their Paint Squad Wars
MonoBehaviours (PooledVfx, PaintProjectile) are removed, the root is renamed PaintSquad*, and their
particle systems play on awake so they work without a pool script. Run with python3 -I from any folder:

    python3 -I Tools/Rebuild/port_paint_squad_art.py [path/to/TactileWars]
"""
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(sys.argv[1]).expanduser() if len(sys.argv) > 1 else Path.home() / 'TactileWars'
SRC = SOURCE / 'Assets'
DST = ROOT / 'Assets/Rebuild/PaintSquad'

CHARACTERS = ['Soldier', 'SoldierHeavy', 'Hero']
ENVIRONMENT = ['Tree_A', 'Tree_B', 'Tree_C', 'Bush_A', 'Bush_B', 'Rock_A', 'Rock_B', 'Rock_C', 'GrassTuft',
               'Flower_A', 'Flower_B', 'Fence', 'Crate', 'Barrel', 'Log']
SHADERS = ['TWToon.shader', 'TWToonOutline.shader', 'TWToonCommon.hlsl', 'TWDecal.shader', 'TWParticle.shader']
FONTS = ['Fredoka-SemiBold.ttf', 'Fredoka SDF.asset', 'Fredoka SDF Outline.mat', 'LilitaOne-Regular.ttf',
         'LilitaOne SDF.asset', 'LilitaOne SDF Outline.mat', 'LilitaOne SDF Outline Thin.mat', 'OFL_Fredoka.txt',
         'OFL_LilitaOne.txt', 'LICENSES.md']
SLOTS = ['TeamColor', 'TeamColorDark', 'Skin', 'Dark', 'Metal', 'Accent', 'Paint', 'Wood', 'Leaf', 'LeafLight',
         'Trunk', 'Rock', 'Sand', 'Canvas', 'Stone']
# Outlined unit variants: every M_U_<Slot> on TW/ToonOutline (M_U_Glass uses TW/Bubble, not ported).
MATERIALS = (['M_' + s for s in SLOTS] + ['M_U_' + s for s in SLOTS] +
             ['M_BlobShadow', 'M_Flash', 'M_P_Soft', 'M_P_Droplet', 'M_Team_Red_Dot'])
TEXTURES = ['T_SoftCircle.png', 'T_PaintDot.png']
VFX = {'PaintBall': 'PaintSquadPaintBall', 'VFX_Muzzle': 'PaintSquadMuzzleFlash', 'VFX_Hit': 'PaintSquadHitSplash'}


def plan():
    pairs = [(f'Art/Models/Characters/{n}.fbx', f'Models/Characters/{n}.fbx') for n in CHARACTERS]
    for n in CHARACTERS:
        pairs += [(f'Art/Animations/{n}.controller', f'Animations/{n}.controller'),
                  (f'Art/Animations/{n}_Upper.mask', f'Animations/{n}_Upper.mask')]
    pairs += [(f'Art/Models/Environment/{n}.fbx', f'Models/Environment/{n}.fbx') for n in ENVIRONMENT]
    pairs.append(('Art/Models/Structures/Tent.fbx', 'Models/Environment/Tent.fbx'))
    pairs += [(f'Art/Shaders/{n}', f'Shaders/{n}') for n in SHADERS]
    pairs += [(f'Art/Fonts/{n}', f'Fonts/{n}') for n in FONTS]
    pairs += [(f'Art/Materials/{n}.mat', f'Materials/{n}.mat') for n in MATERIALS]
    pairs += [(f'Art/Textures/{n}', f'Textures/{n}') for n in TEXTURES]
    pairs.append(('Art/Meshes/M_Quad.asset', 'Meshes/M_Quad.asset'))
    for folder in ('Shapes', 'Icons'):
        for f in sorted((SRC / 'Art/UI' / folder).glob('*.png')):
            pairs.append((f'Art/UI/{folder}/{f.name}', f'UI/{folder}/{f.name}'))
    return pairs


def rebuild_vfx(text, old_name, new_name):
    docs = re.split(r'(?m)^(?=--- !u!)', text)
    dropped, kept = [], []
    for doc in docs:
        match = re.match(r'--- !u!114 &(-?\d+)', doc)
        if match and 'TactileWars' in doc:
            dropped.append(match.group(1))
        else:
            kept.append(doc)
    text = ''.join(kept)
    for file_id in dropped:
        text = re.sub(r'(?m)^  - component: \{fileID: %s\}\n' % file_id, '', text)
    text = re.sub(r'(?m)^  playOnAwake: 0$', '  playOnAwake: 1', text)
    text = text.replace(f'  m_Name: {old_name}\n', f'  m_Name: {new_name}\n')
    assert 'TactileWars' not in text and 'm_Script' not in text, old_name
    return text


def main():
    copied = 0
    for src, dst in plan():
        target = DST / dst
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(SRC / (src + '.meta'), target.with_name(target.name + '.meta'))
        shutil.copy2(SRC / src, target)
        copied += 1
    for old, new in VFX.items():
        target = DST / 'VFX' / (new + '.prefab')
        target.parent.mkdir(parents=True, exist_ok=True)
        source = SRC / 'Prefabs/VFX' / (old + '.prefab')
        target.write_text(rebuild_vfx(source.read_text(encoding='utf-8'), old, new), encoding='utf-8')
        shutil.copy2(source.with_name(source.name + '.meta'), target.with_name(target.name + '.meta'))
        copied += 1
    print(f'copied {copied} assets into {DST}')


if __name__ == '__main__':
    main()
