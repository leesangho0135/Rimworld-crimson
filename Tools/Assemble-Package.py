import argparse
import hashlib
import json
from pathlib import Path
import shutil

parser = argparse.ArgumentParser()
parser.add_argument('--root', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
root, output = Path(args.root).resolve(), Path(args.output).resolve()
if not output.is_relative_to(root / '.artifacts'):
    raise SystemExit('Package output must remain inside .artifacts')
output.mkdir(parents=True, exist_ok=True)
catalog = json.loads((root / 'MOD_PROJECTS/projects.json').read_text(encoding='utf-8-sig'))
seen = set()
for project in catalog['projects']:
    folder = root / 'MOD_PROJECTS' / project['path']
    manifest = json.loads((folder / 'Project.json').read_text(encoding='utf-8-sig'))
    # New assets and definitions in the selected project's Content folder are
    # discovered directly; the migration index is not a second editable list.
    content = folder / 'Content'
    for source in sorted(content.rglob('*')):
        if not source.is_file():
            continue
        target = source.relative_to(content).as_posix()
        destination = output / target
        if not destination.resolve().is_relative_to(output):
            raise SystemExit('Content destination escaped package')
        if target.lower() in seen:
            raise SystemExit('Duplicate package target: ' + target)
        seen.add(target.lower())
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
print('Assembled classified project content; DefNames and texture paths retained.')
