#!/usr/bin/env python3
"""Deterministic synthetic C++ corpus generator for ADR-001 trial S2.
usage: gen_corpus.py OUTDIR NFILES [SEED]"""
import os, random, sys
out, n = sys.argv[1], int(sys.argv[2])
rng = random.Random(int(sys.argv[3]) if len(sys.argv) > 3 else 42)
os.makedirs(out, exist_ok=True)
types = ["int", "double", "std::string", "std::vector<int>", "const char*", "unsigned long", "bool"]
# Non-ASCII text in comments/strings so UTF-8 byte offsets differ from UTF-16 char offsets.
uni = ["café", "日本語", "naïve", "αβγ"]

def members(w, ind, count, cls):
    for i in range(count):
        t = rng.choice(types)
        r = rng.random()
        if r < 0.35:
            w.append(f"{ind}{t} field{i}_;")
        elif r < 0.7:
            w.append(f"{ind}{t} Method{i}(int a, {rng.choice(types)} b) const;")
        elif r < 0.8:
            w.append(f"{ind}virtual void Handle{i}() = 0;")
        elif r < 0.9:
            w.append(f"{ind}static {t} Make{i}() {{ return {t}(); }}")
        else:
            w.append(f"{ind}template <typename U> void Apply{i}(U u);")

def one_class(w, name, bases_pool, idx):
    kw = rng.choice(["class", "struct"])
    tmpl = rng.random() < 0.2
    if tmpl:
        w.append("template <typename T, int N = 4>")
    bases = rng.sample(bases_pool, k=min(len(bases_pool), rng.choice([0, 0, 1, 1, 2])))
    bs = ""
    if bases:
        bs = " : " + ", ".join(f"{rng.choice(['public','protected','private'])} {b}" for b in bases)
    macro = "EXPORT_API " if rng.random() < 0.6 else ""
    w.append(f"// {rng.choice(uni)} {name}")
    w.append(f"{kw} {macro}{name}{bs} {{")
    if kw == "class":
        w.append("public:")
    members(w, "  ", rng.randint(1, 5), name)
    w.append(f"  struct Nested{idx} {{ int x; int y; }};")
    w.append(f"  enum class Mode{idx} {{ A, B, C }};")
    w.append("protected:")
    members(w, "  ", rng.randint(0, 3), name)
    w.append("private:")
    members(w, "  ", rng.randint(1, 4), name)
    w.append("};")
    w.append("")
    return name

pool = ["IBase", "Widget", "Shape", "Node"]
for i in range(n):
    is_hdr = (i % 5) != 4
    ns = f"ns{i % 17}"
    w = []
    if is_hdr:
        w += ["#pragma once", "#include <string>", "#include <vector>", "",
              "#ifdef _WIN32", "#define EXPORT_API __declspec(dllexport)", "#else",
              "#define EXPORT_API __attribute__((visibility(\"default\")))", "#endif", ""]
    else:
        w += [f'#include "file{i-1}.h"', ""]
    w += [f"namespace {ns} {{", ""]
    names = []
    for j in range(rng.randint(2, 5)):
        names.append(one_class(w, f"Type{i}_{j}", pool + names[-2:], j))
    if not is_hdr:
        for nm in names[:2]:
            w.append(f"int {nm}::Method0(int a, int b) const {{ return a + b; }}")
    w += [f"}} // namespace {ns}", ""]
    ext = ".h" if is_hdr else ".cpp"
    with open(os.path.join(out, f"file{i}{ext}"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(w))
print("wrote", n, "files to", out)
