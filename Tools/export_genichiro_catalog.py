#!/usr/bin/env python3
"""Parse GenichiroMoveTable.asset and emit GenichiroMoveCatalog.cs."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / "Assets/SO/Boss/GenichiroMoveTable.asset"
OUT = ROOT / "Assets/Scripts/Boss/GenichiroMoveCatalog.cs"

LAYER = {0: "Active", 1: "Kengeki", 2: "Interrupt"}
EXTRA = {0: "None", 1: "HpBelow75", 2: "PostureLow", 3: "ConsecutiveParry2", 4: "PlayerKnockedDown"}
PERIL = {0: "None", 1: "Thrust", 2: "Sweep", 3: "Grab", 4: "JumpThrust"}
GRADE = {0: "Light", 1: "Mid", 2: "Heavy"}
SLOT = {0: "Weapon", 1: "Elbow", 2: "Kick"}


def fnum(v):
    s = f"{float(v):.9g}"
    if not s.endswith("f"):
        s += "f"
    return s


def coerce(v):
    v = v.strip()
    try:
        if "." in v or "e" in v.lower():
            return float(v)
        return int(v)
    except ValueError:
        return v


def parse_moves(text):
    m = re.search(r"  moves:\n", text)
    if not m:
        return []
    parts = re.split(r"(?=  - id: )", text[m.end():])
    return [parse_move(p) for p in parts if p.strip().startswith("- id:")]


def parse_entry_field(block, key):
    matches = list(re.finditer(rf"    {key}: ([^\n]+)", block))
    return coerce(matches[-1].group(1)) if matches else None


def parse_move(block):
    move = {
        "layer": parse_entry_field(block, "layer"),
        "baseDamage": parse_entry_field(block, "baseDamage"),
        "postureDamage": parse_entry_field(block, "postureDamage"),
        "knockback": parse_entry_field(block, "knockback"),
        "hitGrade": parse_entry_field(block, "hitGrade"),
        "perilous": parse_entry_field(block, "perilous"),
        "minRange": parse_entry_field(block, "minRange"),
        "maxRange": parse_entry_field(block, "maxRange"),
        "weight": parse_entry_field(block, "weight"),
        "cooldown": parse_entry_field(block, "cooldown"),
        "extra": parse_entry_field(block, "extra"),
    }
    move["id"] = re.search(r"id: (\S+)", block).group(1)
    move["sequences"] = parse_sequences(block)
    move["windows"] = parse_entry_windows(block)
    return move


def parse_entry_windows(block):
    base = block.split("\n    baseDamage:")[0]
    matches = list(re.finditer(r"\n    windows:\n", base))
    if not matches:
        return []
    return parse_windows(base[matches[-1].end():], "    - ")


def parse_sequences(block):
    base = block.split("\n    baseDamage:")[0]
    seq_m = re.search(r"    sequences:\n", base)
    if not seq_m:
        return []
    win_matches = list(re.finditer(r"\n    windows:\n", base))
    if not win_matches:
        return []
    body = base[seq_m.end():win_matches[-1].start()]
    seqs = []
    for chunk in re.findall(r"    - states:(.*?)(?=    - states:|\Z)", body, re.S):
        part = chunk.split("windows:")[0]
        states = [x.group(1) for x in re.finditer(r"^\s+- (\S+)", part, re.M)]
        wins = []
        if "windows:" in chunk:
            wbody = chunk.split("windows:", 1)[1]
            wins = parse_windows(wbody, "      - ")
        seqs.append({"states": states, "windows": wins})
    return seqs


def parse_windows(body, prefix):
    wins = []
    chunks = re.split(rf"\n(?={re.escape(prefix)}hitStartTime:)", body)
    for chunk in chunks:
        chunk = chunk.rstrip()
        if not chunk:
            continue
        if not chunk.lstrip().startswith("hitStartTime:") and f"{prefix}hitStartTime:" not in chunk:
            if chunk.lstrip().startswith("- hitStartTime:"):
                chunk = prefix + chunk.lstrip()
            else:
                continue
        if not chunk.startswith(prefix):
            chunk = prefix + chunk.lstrip()
        wins.append(parse_window(chunk, prefix))
    return wins


def parse_window(text, prefix):
    leading = len(prefix) - len(prefix.lstrip(" "))
    sub_indent = " " * (leading + 2)
    pulse_prefix = sub_indent + "- "
    pulse_field = sub_indent + "  "

    def field(name):
        for pat in (rf"^{re.escape(prefix)}{name}: ([^\n]+)", rf"^{sub_indent}{name}: ([^\n]+)"):
            m = re.search(pat, text, re.M)
            if m:
                return coerce(m.group(1))
        return None

    w = {
        "hitStartTime": field("hitStartTime"),
        "recoverStart": field("recoverStart"),
        "comboWindowEnd": field("comboWindowEnd"),
        "stateDuration": field("stateDuration"),
        "rotateEnd": field("rotateEnd") or 0.35,
        "transitionDuration": field("transitionDuration") or 0.1,
        "perilous": field("perilous") or 0,
        "hitboxSlot": field("hitboxSlot") or 0,
        "overrideCombat": field("overrideCombat") or 0,
        "baseDamage": field("baseDamage") or 0,
        "postureDamage": field("postureDamage") or 0,
        "knockback": field("knockback") or 0,
        "hitGrade": field("hitGrade") or 0,
        "waitAnimEnd": field("waitAnimEnd") or 0,
        "hitPulses": [],
        "arrowCues": [],
    }
    for pm in re.finditer(rf"^{re.escape(pulse_prefix)}start: ([^\n]+)\n{re.escape(pulse_field)}end: ([^\n]+)", text, re.M):
        p = {"start": float(pm.group(1)), "end": float(pm.group(2))}
        chunk = pm.group(0)
        for k in ("overrideCombat", "baseDamage", "postureDamage", "knockback", "hitGrade"):
            km = re.search(rf"{re.escape(pulse_field)}{k}: ([^\n]+)", chunk)
            if km:
                p[k] = coerce(km.group(1))
        w["hitPulses"].append(p)
    for am in re.finditer(rf"^{re.escape(pulse_prefix)}time: ([^\n]+)", text, re.M):
        a = {"time": float(am.group(1))}
        chunk = am.group(0)
        for k in ("overrideCombat", "baseDamage", "postureDamage", "knockback", "hitGrade"):
            km = re.search(rf"{re.escape(pulse_field)}{k}: ([^\n]+)", chunk)
            if km:
                a[k] = coerce(km.group(1))
        w["arrowCues"].append(a)
    return w


def q(s):
    return f'"{s}"'


def emit_pulse(p):
    s = f"P({fnum(p['start'])}, {fnum(p['end'])}"
    g = p.get("hitGrade", 0)
    if p.get("overrideCombat") or g:
        s += f", HitGrade.{GRADE[g]}"
    if p.get("overrideCombat"):
        s += f", ov: true, dmg: {p.get('baseDamage', 0)}, posture: {fnum(p.get('postureDamage', 0))}"
    return s + ")"


def emit_arrow(a):
    s = f"A({fnum(a['time'])}"
    g = a.get("hitGrade", 0)
    if a.get("overrideCombat") or g:
        s += f", HitGrade.{GRADE[g]}"
    if a.get("overrideCombat"):
        s += f", ov: true, dmg: {a.get('baseDamage', 0)}, posture: {fnum(a.get('postureDamage', 0))}"
    return s + ")"


def emit_window(w):
    args = [fnum(w["hitStartTime"]), fnum(w["recoverStart"]), fnum(w["comboWindowEnd"]), fnum(w["stateDuration"])]
    pulses, arrows = w.get("hitPulses") or [], w.get("arrowCues") or []
    named = []
    if pulses:
        named.append("new[] { " + ", ".join(emit_pulse(p) for p in pulses) + " }")
    if arrows:
        if not pulses:
            named.append("null")
        named.append("new[] { " + ", ".join(emit_arrow(a) for a in arrows) + " }")
    opt = []
    if w.get("perilous", 0):
        opt += [f"PerilousType.{PERIL[w['perilous']]}", f"AttackHitboxSlot.{SLOT[w.get('hitboxSlot', 0)]}"]
    elif w.get("hitboxSlot", 0):
        opt += ["PerilousType.None", f"AttackHitboxSlot.{SLOT[w['hitboxSlot']]}"]
    if w.get("rotateEnd", 0.35) != 0.35:
        opt.append(f"rotateEnd: {fnum(w['rotateEnd'])}")
    if w.get("transitionDuration", 0.1) != 0.1:
        opt.append(f"transition: {fnum(w['transitionDuration'])}")
    if w.get("hitGrade", 0) or w.get("overrideCombat"):
        opt.append(f"grade: HitGrade.{GRADE[w.get('hitGrade', 0)]}")
    if w.get("overrideCombat"):
        opt += ["ov: true", f"dmg: {w.get('baseDamage', 0)}", f"posture: {fnum(w.get('postureDamage', 0))}"]
    if w.get("knockback"):
        opt.append(f"knockback: {fnum(w['knockback'])}")
    if w.get("waitAnimEnd"):
        opt.append("waitAnimEnd: true")
    if opt:
        if not pulses and not arrows:
            named += ["null", "null"]
        elif not arrows:
            named.append("null")
        named += opt
    return "Win(" + ", ".join(args + named) + ")"


def emit_sequences(seqs):
    out = []
    for s in seqs:
        states = ", ".join(q(x) for x in s["states"])
        if s.get("windows"):
            wins = ", ".join(emit_window(w) for w in s["windows"])
            out.append(f"SeqWin(new[] {{ {states} }}, new[] {{ {wins} }})")
        else:
            out.append(f"Seq({states})")
    return ", ".join(out)


def emit_move(m, last):
    lines = [
        f'            Move({q(m["id"])}, BossMoveLayer.{LAYER[m["layer"]]}, '
        f'{fnum(m["minRange"])}, {fnum(m["maxRange"])}, {fnum(m["weight"])}, {fnum(m["cooldown"])},',
        f'                new[] {{ {emit_sequences(m["sequences"])} }},',
        f'                new[] {{ {", ".join(emit_window(w) for w in m["windows"])} }}',
    ]
    extras = []
    if m.get("perilous", 0):
        extras.append(f"PerilousType.{PERIL[m['perilous']]}")
    else:
        extras.append("PerilousType.None")
    extras.append(f"BossMoveExtra.{EXTRA[m.get('extra', 0)]}")
    if m.get("baseDamage", 10) != 10 or m.get("postureDamage", 10) != 10 or m.get("hitGrade", 0):
        extras += [f"dmg: {m.get('baseDamage', 10)}", f"posture: {fnum(m.get('postureDamage', 10))}", f"grade: HitGrade.{GRADE[m.get('hitGrade', 0)]}"]
    if len(extras) > 2:
        lines[-1] += ","
        lines.append("                " + ", ".join(extras))
    else:
        lines[-1] += ", " + ", ".join(extras)
    lines[-1] += ")" + ("" if last else ",")
    return "\n".join(lines)


HELPERS = Path(__file__).with_name("_catalog_helpers.cs").read_text(encoding="utf-8")
POST = Path(__file__).with_name("_catalog_post.cs").read_text(encoding="utf-8")


def main():
    text = ASSET.read_text(encoding="utf-8")
    meta = {k: coerce(m.group(1)) for k in (
        "kengekiMaxRange", "postureLowThreshold", "air5HeavyInterruptChance",
        "jumpThrustLife2SweepWeight", "jumpThrustLife2ThrustWeight"
    ) if (m := re.search(rf"  {k}: ([^\n]+)", text))}
    moves = parse_moves(text)
    out = ["using UnityEngine;\n\n",
           "// 弦一郎默认招式表。由 Editor 按钮写入 BossMoveTable，不要运行时调用。\n",
           "// 与 Assets/SO/Boss/GenichiroMoveTable.asset 同步：菜单 ARPG/Sync GenichiroMoveCatalog from Move Table\n",
           "public static class GenichiroMoveCatalog\n{\n", HELPERS,
           "\n    public static void Apply(BossMoveTable t)\n    {\n",
           f"        t.kengekiMaxRange = {fnum(meta['kengekiMaxRange'])};\n",
           f"        t.postureLowThreshold = {fnum(meta['postureLowThreshold'])};\n",
           f"        t.air5HeavyInterruptChance = {fnum(meta['air5HeavyInterruptChance'])};\n",
           f"        t.jumpThrustLife2SweepWeight = {fnum(meta['jumpThrustLife2SweepWeight'])};\n",
           f"        t.jumpThrustLife2ThrustWeight = {fnum(meta['jumpThrustLife2ThrustWeight'])};\n",
           "        t.moves = new[]\n        {\n"]
    for i, m in enumerate(moves):
        out.append(emit_move(m, i == len(moves) - 1) + "\n")
    out.append("        };\n        ApplyCombatNumbers(t);\n    }\n")
    out.append(POST)
    out.append("\n}\n")
    OUT.write_text("".join(out), encoding="utf-8")
    print(f"Wrote {OUT} ({len(moves)} moves)")


if __name__ == "__main__":
    main()
