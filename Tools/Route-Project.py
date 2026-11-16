"""Select compact project scopes without loading source, assets, or history."""
import argparse
import json
from pathlib import Path
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')

ROOT = Path(__file__).resolve().parents[1]
ALIASES = {
    'CORE': ['core'],
    'SOS2_ADDON': ['sos2 addon', 'sos2_addon', '선체벽', '에어록', '엔진', '함선무기', 'ship', 'hull'],
    'FURNITURE': ['furniture', 'funiture', '침대', '소파', '책장', 'dining', 'chair', 'refrigerator'],
    'RECREATION': ['recreation', 'recriation', '티비', '텔레비전', '포커', '피아노', 'television'],
    'SANITARY': ['sanitary', '화장실', '배관', '목욕', 'hygiene'],
    'MEDICAL': ['medical', '약장', '의료 침대', '병원 침대', 'medicine'],
    'DECORATION': ['decoration', '벽등', '천장등', '커튼', 'lighting', 'lamp'],
    'PRODUCTION': ['production', '수경재배', '제작대', 'smelter'],
    'EQUIPMENT': ['equipment', '작업장비', '보병무기', 'armor', 'apparel'],
}


def select(query='', explicit=None, path=None):
    catalog = json.loads((ROOT / 'MOD_PROJECTS/projects.json').read_text(encoding='utf-8-sig'))
    projects = {entry['name']: entry for entry in catalog['projects']}
    selected, matches = [], {}
    if path:
        candidate = Path(path)
        if not candidate.is_absolute(): candidate = ROOT / candidate
        candidate = candidate.resolve()
        for name, entry in projects.items():
            if candidate.is_relative_to((ROOT / 'MOD_PROJECTS' / entry['path']).resolve()):
                selected = [name]
                break
        if not selected:
            return {'status': 'outside_project', 'projects': [], 'message': 'Choose an editable MOD_PROJECTS folder.'}, 2
    elif explicit:
        aliases = {alias.lower(): name for name, values in ALIASES.items() for alias in values}
        for value in explicit.split(','):
            value = value.strip()
            if value.upper() == 'ALL':
                selected = list(projects)
                break
            name = value.upper() if value.upper() in projects else aliases.get(value.lower())
            if not name:
                return {'status': 'unknown_project', 'projects': [], 'choices': list(projects)}, 2
            if name not in selected: selected.append(name)
    else:
        hits = []
        for name, entry in projects.items():
            for word in entry.get('keywords', []) + ALIASES.get(name, []):
                pattern = r'\s*'.join(re.escape(part) for part in word.split())
                if re.fullmatch(r'[A-Za-z0-9_ ]+', word):
                    pattern = r'(?<![A-Za-z0-9])' + pattern + r'(?![A-Za-z0-9])'
                for found in re.finditer(pattern, query, re.IGNORECASE):
                    hits.append((found.start(), found.end(), name, word))
        # A specific phrase such as 의료침대 owns its contained generic 침대 hit.
        hits = [hit for hit in hits if not any(
            other[0] <= hit[0] and other[1] >= hit[1] and other[1] - other[0] > hit[1] - hit[0]
            for other in hits)]
        for start, end, name, word in sorted(hits):
            if name not in selected: selected.append(name)
            matches.setdefault(name, set()).add(word)
    if not selected:
        return {'status': 'unresolved', 'projects': [], 'choices': list(projects)}, 0
    result = []
    for name in selected:
        entry = projects[name]
        folder = 'MOD_PROJECTS/' + entry['path']
        first = ['Source/Main', 'Content/1.6/Defs']
        if (ROOT / folder / 'PROJECT.md').is_file(): first.insert(0, 'PROJECT.md')
        result.append({'name': name, 'root': folder, 'workspace': folder + '/' + name + '.code-workspace',
                       'matched': sorted(matches.get(name, [])), 'readFirst': first})
    return {'status': 'selected', 'projects': result, 'sourceFilesRead': 0, 'historyRead': False}, 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--query', default='')
    parser.add_argument('--project')
    parser.add_argument('--path')
    args = parser.parse_args()
    result, code = select(args.query, args.project, args.path)
    print(json.dumps(result, ensure_ascii=False, separators=(',', ':')))
    raise SystemExit(code)
