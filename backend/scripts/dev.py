"""Canonical local backend commands; credentials stay in an ignored, private file."""
import argparse
import os
from pathlib import Path
import secrets
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parent


def configuration():
    path = ROOT / '.env'
    if not path.exists():
        raise SystemExit('Run python3 backend/scripts/dev.py setup first.')
    values = dict(line.split('=', 1) for line in path.read_text().splitlines() if line and not line.startswith('#'))
    env = os.environ.copy()
    env.update(values)
    env.setdefault('ConnectionStrings__Database', f"Host=localhost;Port=55432;Database=companion;Username=companion;Password={values['POSTGRES_PASSWORD']}")
    env.setdefault('ASPNETCORE_ENVIRONMENT', 'Development')
    env.setdefault('DOTNET_ENVIRONMENT', 'Development')
    env.setdefault('ASPNETCORE_URLS', 'http://localhost:5080')
    env.setdefault('Storage__Root', str(ROOT / '.local'))
    env.setdefault('Ai__Python', str(ROOT / '.venv/bin/python'))
    env.setdefault('Ai__Bridge', str(ROOT / 'ai/bridge.py'))
    env.setdefault('DOTNET_CLI_TELEMETRY_OPTOUT', '1')
    env.setdefault('DOTNET_NOLOGO', '1')
    env.setdefault('DOTNET_PROCESSOR_COUNT', '2')
    local_sdk = Path.home() / '.dotnet'
    if local_sdk.exists():
        env.setdefault('DOTNET_ROOT', str(local_sdk))
        env['PATH'] = str(local_sdk) + os.pathsep + env['PATH']
    return env


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['setup', 'up', 'down', 'migrate', 'serve', 'import', 'admin', 'test', 'index', 'evaluate', 'evaluate-study', 'evaluate-practice', 'worker', 'seed-community'])
    parser.add_argument('--email')
    parser.add_argument('--cases', help='Optional comma-separated gold IDs for local study evaluation.')
    args = parser.parse_args()
    if args.command == 'setup':
        path = ROOT / '.env'
        if not path.exists():
            with path.open('x') as file:
                os.chmod(path, 0o600)
                file.write('POSTGRES_PASSWORD=' + secrets.token_urlsafe(32) + '\n')
        print('Local configuration ready. Credentials are stored in backend/.env (ignored by git).')
        return
    env = configuration()
    if args.command in {'up', 'down'}:
        cmd = ['docker', 'compose', '--env-file', str(ROOT / '.env'), '-f', str(ROOT / 'compose.yaml')]
        cmd += ['up', '-d', '--wait'] if args.command == 'up' else ['stop']
    else:
        dotnet = shutil.which('dotnet', path=env['PATH'])
        if not dotnet:
            raise SystemExit('Install the .NET 10 SDK first (see backend/README.md).')
        if args.command == 'test':
            cmd = [dotnet, 'test', 'UniCompanion.sln', '--no-restore', '--disable-build-servers', '-m:1']
        else:
            cmd = [dotnet, 'run', '--disable-build-servers', '--no-launch-profile', '--project', 'src/UniCompanion.Worker' if args.command == 'worker' else 'src/UniCompanion.Api', '--']
            if args.command == 'evaluate-practice': cmd += ['--evaluate-practice', str(ROOT / 'tests/practice-evaluation.json'), str(ROOT / ('.local/evidence/next/practice-evaluation-focused.json' if args.cases else '.local/evidence/next/practice-evaluation.json'))] + ([args.cases] if args.cases else [])
            if args.command == 'evaluate-study': cmd += ['--evaluate-study', str(ROOT / 'tests/retrieval-evaluation.json'), str(ROOT / ('.local/study-evaluation-focused.json' if args.cases else '.local/study-evaluation.json'))] + ([args.cases] if args.cases else [])
            if args.command == 'evaluate': cmd += ['--evaluate', str(ROOT / 'tests/retrieval-evaluation.json')]
            if args.command == 'seed-community': cmd += ['--seed-community']
            if args.command == 'index': cmd += ['--index']
            if args.command == 'migrate': cmd += ['--migrate']
            if args.command == 'import': cmd += ['--import', str(REPO / 'data/university-import/catalogue.json')]
            if args.command == 'admin':
                if not args.email: raise SystemExit('Supply --email for an existing, confirmed account.')
                env['Bootstrap__Email'] = args.email
                cmd += ['--bootstrap-admin']
    try:
        raise SystemExit(subprocess.call(cmd, cwd=ROOT, env=env))
    except KeyboardInterrupt:
        raise SystemExit(130)


if __name__ == '__main__':
    main()
