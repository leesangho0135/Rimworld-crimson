"""Stream command output to a local log and emit only a bounded summary."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding='utf-8')

ROOT = Path(__file__).resolve().parents[1]


def decode(data):
    if data.startswith((b'\xff\xfe', b'\xfe\xff')): return data.decode('utf-16', errors='replace')
    try: return data.decode('utf-8-sig')
    except UnicodeDecodeError: return data.decode('cp949', errors='replace')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--name', required=True)
    parser.add_argument('--project')
    parser.add_argument('--limit', type=int, default=3000)
    parser.add_argument('--tail-lines', type=int, default=12)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,63}', args.name): parser.error('Use a simple log name without directory components.')
    if not 256 <= args.limit <= 20000: parser.error('limit must be between 256 and 20000 characters.')
    if not 1 <= args.tail_lines <= 100: parser.error('tail-lines must be between 1 and 100.')
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command: parser.error('Provide a command after --.')
    cwd = ROOT
    if args.project:
        catalog = json.loads((ROOT / 'MOD_PROJECTS/projects.json').read_text(encoding='utf-8-sig'))
        entry = next((p for p in catalog['projects'] if p['name'] == args.project.upper()), None)
        if entry is None: parser.error('Unknown project.')
        cwd = (ROOT / 'MOD_PROJECTS' / entry['path']).resolve()
        if not cwd.is_relative_to((ROOT / 'MOD_PROJECTS').resolve()) or not cwd.is_dir():
            parser.error('Project working directory must remain inside MOD_PROJECTS.')
    directory = (ROOT / '.artifacts/WorkLogs').resolve()
    if not directory.is_relative_to(ROOT): parser.error('Log directory escaped repository.')
    directory.mkdir(parents=True, exist_ok=True)
    log = directory / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '-' + args.name + '.log')
    try:
        with log.open('wb') as output:
            flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
            child = subprocess.Popen(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT, shell=False, creationflags=flags)
            print(json.dumps({'pid': child.pid, 'log': str(log)}, ensure_ascii=False, separators=(',', ':')), flush=True)
            code = child.wait()
    except OSError as error:
        print(json.dumps({'exitCode': 127, 'log': str(log), 'error': str(error)}, ensure_ascii=False))
        return 127
    with log.open('rb') as stream:
        stream.seek(0, 2)
        size = stream.tell()
        stream.seek(max(0, size - 65536))
        data = stream.read(65536)
    lines = decode(data).splitlines()
    tail = '\n'.join(lines[-args.tail_lines:])
    truncated = size > len(data) or len(tail) > args.limit or len(lines) > args.tail_lines
    tail = tail[-args.limit:]
    print(json.dumps({'exitCode': code, 'log': str(log), 'truncated': truncated, 'tail': tail}, ensure_ascii=False, separators=(',', ':')))
    return code if code >= 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
