import pathlib
p = pathlib.Path(r"c:\Zombera\scripts\stylematch_run_297_309.py")
t = p.read_text(encoding="utf-8")
if "FOOTER.format" in t:
    t = t.replace(
        "FOOTER = \"\"\"\n        if (!StyleMatchPerfCampaign.RunCampaignIteration({n}, opts, out var r, out var err))\n            return \"FAIL: \" + err;\n        return \"started {n}\";\n    }\n\"\"\" + HELPERS",
        "FOOTER_HEAD = \"\"\"\n        if (!StyleMatchPerfCampaign.RunCampaignIteration(ITER, opts, out var r, out var err))\n            return \"FAIL: \" + err;\n        return \"started ITER\";\n    }\n\"\"\" + HELPERS",
    )
    t = t.replace(
        "code = HEADER + BODIES[n] + FOOTER.format(n=n)",
        "code = HEADER + BODIES[n] + FOOTER_HEAD.replace(\"ITER\", str(n))",
    )
    p.write_text(t, encoding="utf-8")
    print("patched")
else:
    print("already patched or pattern missing")
