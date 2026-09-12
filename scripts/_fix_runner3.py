import pathlib
p = pathlib.Path(r"c:\Zombera\scripts\stylematch_run_297_309.py")
t = p.read_text(encoding="utf-8")
if not t.rstrip().endswith("}\n"):
    t = t.replace('    }\n""".replace("None", "null")', '    }\n}\n""".replace("None", "null")')
t = t.replace('for n in range(301, 310):', 'for n in range(305, 310):')
p.write_text(t, encoding="utf-8")
print('fixed brace')
