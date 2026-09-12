import pathlib
p = pathlib.Path(r"c:\Zombera\scripts\stylematch_run_297_309.py")
t = p.read_text(encoding="utf-8")
t = t.replace(
    '    p = subprocess.run(\n        ["npx", "unity-mcp-cli", "run-tool", "script-execute", "--path", PROJ, "--input-file", ip],\n        capture_output=True, text=True,\n    )',
    '    cmd = f"npx unity-mcp-cli run-tool script-execute --path {PROJ} --input-file {ip}"\n    p = subprocess.run(cmd, capture_output=True, text=True, shell=True)',
)
t = t.replace('for n in range(297, 310):', 'for n in range(301, 310):')
t = t.replace('for n in [295, 296]:', 'for n in [295, 296, 297, 298, 299, 300]:')
p.write_text(t, encoding="utf-8")
print('patched')
