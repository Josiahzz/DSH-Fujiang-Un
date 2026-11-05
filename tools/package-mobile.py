#!/usr/bin/env python3
"""Package the self-contained DSHA bundle without any desktop installer files."""
import hashlib
import json
import zipfile
from pathlib import Path

repo = Path(__file__).resolve().parent.parent
manifest = json.loads((repo / 'package.json').read_text(encoding='utf-8'))
version = manifest['version']
out = repo / 'outputs'
out.mkdir(exist_ok=True)
archive = out / f'DSH-Fujiang-DSHA-v{version}.zip'
files = {'package.json', 'mobile/cordis.patch.yml', 'mobile/README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md'}
files.add('payload/dsh-ws-helper-preset/subagent-list-compat.mjs')
for root in ['payload/skills', 'payload/dsh-ws-helper-preset/collaboration-skills']:
    files.update(p.relative_to(repo).as_posix() for p in (repo / root).rglob('*') if p.is_file())
assert all(not name.endswith(('.exe', '.cmd', '.dll')) for name in files)
assert not any(name.startswith(('src/', 'dist/', 'scripts/')) for name in files)
contents = {name: (repo / name).read_bytes() for name in files}
production_manifest = {key: value for key, value in manifest.items() if key not in ('scripts', 'devDependencies')}
contents['package.json'] = (json.dumps(production_manifest, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
checksums = ''.join(hashlib.sha256(contents[name]).hexdigest().upper() + '  ' + name + '\n' for name in sorted(files))
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for name in sorted(files):
        z.writestr(name, contents[name])
    z.writestr('SHA256SUMS.txt', checksums)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert json.loads(z.read('package.json'))['dsh']['bundle']['patch'] == './mobile/cordis.patch.yml'
print(f'MOBILE_ZIP_PASS: {len(files)+1} files; {archive.stat().st_size} bytes; SHA256 {hashlib.sha256(archive.read_bytes()).hexdigest().upper()}')
print(archive)
