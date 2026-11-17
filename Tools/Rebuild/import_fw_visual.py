"""Selectively copy FW assets, preserving GUIDs and refusing conflicting files."""
import json
import pathlib
import tarfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
PACKAGE = pathlib.Path.home() / 'AppData/Roaming/Unity/Asset Store-5.x/freeworkStudio/Textures Materials2D Characters/FW2D Animated Cute Girl Full SD Character.unitypackage'

def main():
    pending = []
    with tarfile.open(PACKAGE, 'r:gz') as archive:
        members = {m.name: m for m in archive.getmembers()}
        for name, member in members.items():
            if not name.endswith('/pathname'):
                continue
            path = archive.extractfile(member).read().decode('utf-8-sig').splitlines()[0].strip('\x00')
            if not (path == 'Assets/FW_MIHO' or path.startswith('Assets/FW_MIHO/')):
                continue
            target = ROOT / path
            if not target.resolve().is_relative_to((ROOT / 'Assets/FW_MIHO').resolve()):
                raise RuntimeError('Invalid package path: ' + path)
            prefix = name.rsplit('/', 1)[0]
            for suffix, destination in [('asset', target), ('asset.meta', pathlib.Path(str(target) + '.meta'))]:
                entry = members.get(prefix + '/' + suffix)
                if entry is None or entry.isdir():
                    continue
                data = archive.extractfile(entry).read()
                if destination.exists() and (destination.is_dir() or destination.read_bytes() != data):
                    raise RuntimeError('Refusing to overwrite: ' + str(destination))
                pending.append((destination, data))
    # Check every conflict before writing anything. Never import the package's manifest.
    for destination, data in pending:
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    manifest = ROOT / 'Packages/manifest.json'
    data = json.loads(manifest.read_text(encoding='utf-8-sig'))
    for package, version in [('com.unity.2d.animation', '13.0.6'), ('com.unity.2d.psdimporter', '12.0.2')]:
        existing = data['dependencies'].get(package)
        if existing and existing != version:
            raise RuntimeError('Unexpected existing dependency: ' + package)
        data['dependencies'][package] = version
    manifest.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    print(f'Copied {len(pending)} FW asset/meta files. Added two dependencies; project settings untouched.')

if __name__ == '__main__':
    main()
