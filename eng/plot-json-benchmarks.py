"""Render the performance page's SVG charts from BenchmarkDotNet JSON exports."""

import argparse
import json
import math
from datetime import date
from html import escape
from pathlib import Path


SCENARIOS = {
    "primitives": [
        ("Int32", "int: 42"),
        ("String", "string: Hello, PolyType!"),
    ],
    "collections": [
        ("Int32[]", "int[]: 256 elements"),
        ("List<Int32>", "List<int>: 256 elements"),
        ("Dictionary<String, Int32>", "Dictionary<string, int>: 64 entries"),
    ],
    "pocos": [
        ("MyPoco", "Small POCO: 4 properties, parameterized constructor"),
        ("LargePoco", "Large POCO: 24 properties, nested objects and collections"),
    ],
    "megabyte": [
        ("CitmCatalog[]", "CITM event catalogs: 5 catalogs, 2.39 MiB JSON"),
        ("LargePoco[]", "Large POCO array: 4,096 records, 2.64 MiB JSON"),
    ],
}

METHODS = {
    "StjReflection": ("STJ reflection", "#64748b"),
    "StjSourceGen": ("STJ source-gen metadata", "#2563eb"),
    "StjSourceGen_FastPath": ("STJ source-gen fast path", "#7c3aed"),
    "PolyTypeReflection": ("PolyType reflection", "#b45309"),
    "PolyTypeSourceGen": ("PolyType source-gen", "#047857"),
}


def read_reports(directory, measured_on):
    data = {"measured_on": measured_on, "host": None, "benchmarks": []}
    for path in sorted(directory.glob("*-report-full-compressed.json")):
        report = json.loads(path.read_text())
        host = report["HostEnvironmentInfo"]
        if data["host"] is not None and data["host"] != host:
            raise ValueError(f"Inconsistent benchmark environments: {path}")
        data["host"] = host
        for benchmark in report["Benchmarks"]:
            operation, method = benchmark["Method"].split("_", 1)
            scenario = benchmark["Type"].split("<", 1)[1][:-1]
            data["benchmarks"].append({
                "operation": operation,
                "scenario": scenario,
                "method": method,
                "job": benchmark["DisplayInfo"],
                "statistics": {
                    name: benchmark["Statistics"][name]
                    for name in ("N", "OriginalValues", "Mean", "StandardError", "StandardDeviation", "ConfidenceInterval")
                } if benchmark["Statistics"] is not None else None,
                "allocated_bytes": benchmark["Memory"]["BytesAllocatedPerOperation"],
            })

    return data


def validate(data):
    expected = {
        (operation, scenario, method)
        for operation in ("Serialize", "Deserialize")
        for group in SCENARIOS.values()
        for scenario, _ in group
        for method in METHODS
        if operation == "Serialize" or method != "StjSourceGen_FastPath"
    }
    records = {}
    for benchmark in data["benchmarks"]:
        key = tuple(benchmark[name] for name in ("operation", "scenario", "method"))
        if key in records:
            raise ValueError(f"Duplicate benchmark: {key}")
        stats = benchmark["statistics"]
        if stats is None or stats["N"] < 3:
            raise ValueError(f"Missing measurements or fewer than three iterations: {key}")
        if not math.isfinite(stats["Mean"]) or stats["Mean"] <= 0:
            raise ValueError(f"Invalid mean: {key}")
        if not math.isfinite(stats["StandardError"]) or stats["StandardError"] < 0:
            raise ValueError(f"Invalid standard error: {key}")
        if not math.isfinite(benchmark["allocated_bytes"]) or benchmark["allocated_bytes"] < 0:
            raise ValueError(f"Invalid allocation measurement: {key}")
        records[key] = benchmark
    if records.keys() != expected:
        raise ValueError(f"Unexpected benchmark set; missing {expected - records.keys()}, extra {records.keys() - expected}")
    if not data["host"] or not data["measured_on"]:
        raise ValueError("Missing benchmark environment or measurement date")

    return records


def format_time(nanoseconds):
    for divisor, unit in ((1_000_000, "ms"), (1_000, "us"), (1, "ns")):
        if nanoseconds >= divisor:
            return f"{nanoseconds / divisor:.2f} {unit}"
    return f"{nanoseconds:.2f} ns"


def format_bytes(value):
    for divisor, unit in ((1_048_576, "MiB"), (1_024, "KiB")):
        if value >= divisor:
            return f"{value / divisor:.2f} {unit}"
    return f"{value:g} B"


def chart(records, operation, group_name, scenarios):
    methods = [method for method in METHODS if operation == "Serialize" or method != "StjSourceGen_FastPath"]
    group_height = 60 + 32 * len(methods)
    height = 116 + group_height * len(scenarios)
    left, plot_width = 258, 420
    upper = max(
        (records[operation, scenario, method]["statistics"]["Mean"]
         + records[operation, scenario, method]["statistics"]["StandardError"])
        / records[operation, scenario, "StjReflection"]["statistics"]["Mean"]
        for scenario, _ in scenarios for method in methods
    )
    axis_max = math.ceil(max(1.25, upper + 0.1) * 4) / 4
    title = f"{'Serialization' if operation == 'Serialize' else 'Deserialization'}: {group_name}"
    svg = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="1100" height="{height}" viewBox="0 0 1100 {height}" role="img" aria-labelledby="title desc">',
        f"<title id=\"title\">{escape(title)}</title>",
        '<desc id="desc">Mean time relative to STJ reflection for each payload; lower is better. Whiskers show one standard error. Each bar is labelled with its ratio, mean time, and allocated bytes per operation.</desc>',
        '<rect width="100%" height="100%" fill="#ffffff"/>',
        '<g font-family="Arial, sans-serif" font-size="14" fill="#0f172a">',
    ]

    def text(x, y, value, **attrs):
        attributes = " ".join(f'{name.replace("_", "-")}="{value}"' for name, value in attrs.items())
        svg.append(f'<text x="{x}" y="{y}" {attributes}>{escape(value)}</text>')

    text(20, 30, title, font_size="22", font_weight="bold")
    text(20, 54, "Lower is better | normalized to STJ reflection per payload | whiskers: +/-1 standard error")
    text(740, 79, "Ratio | Mean | Allocated / operation", font_size="13")
    for tick in range(int(axis_max * 4) + 1):
        ratio = tick / 4
        x = left + ratio / axis_max * plot_width
        svg.append(f'<line x1="{x:.2f}" y1="90" x2="{x:.2f}" y2="{height - 35}" stroke="#e2e8f0"/>')
        text(f"{x:.2f}", height - 15, f"{ratio:g}x", text_anchor="middle", font_size="12")
    baseline_x = left + plot_width / axis_max
    svg.append(f'<line x1="{baseline_x:.2f}" y1="90" x2="{baseline_x:.2f}" y2="{height - 35}" stroke="#475569" stroke-dasharray="4 4"/>')

    for index, (scenario, label) in enumerate(scenarios):
        y = 110 + index * group_height
        text(20, y, label, font_weight="bold", font_size="16")
        baseline = records[operation, scenario, "StjReflection"]["statistics"]["Mean"]
        for method_index, method in enumerate(methods):
            record = records[operation, scenario, method]
            stats = record["statistics"]
            ratio = stats["Mean"] / baseline
            bar_y = y + 14 + method_index * 32
            width = ratio / axis_max * plot_width
            error = stats["StandardError"] / baseline / axis_max * plot_width
            endpoint = left + width
            name, color = METHODS[method]
            text(left - 12, bar_y + 16, name, text_anchor="end")
            svg.append(f'<rect x="{left}" y="{bar_y}" width="{width:.2f}" height="22" fill="{color}"/>')
            svg.append(f'<path d="M {endpoint - error:.2f} {bar_y + 11} H {endpoint + error:.2f} M {endpoint - error:.2f} {bar_y + 6} V {bar_y + 16} M {endpoint + error:.2f} {bar_y + 6} V {bar_y + 16}" stroke="#0f172a" fill="none"/>')
            text(740, bar_y + 16, f'{ratio:.2f}x | {format_time(stats["Mean"])} | {format_bytes(record["allocated_bytes"])}')
    svg.extend(["</g>", "</svg>", ""])

    return "\n".join(svg)


def print_gaps(records):
    for operation in ("Serialize", "Deserialize"):
        for scenarios in SCENARIOS.values():
            for scenario, _ in scenarios:
                stj = [record for (op, case, method), record in records.items()
                       if op == operation and case == scenario and method.startswith("Stj")]
                fastest = min(stj, key=lambda record: record["statistics"]["Mean"])
                for method in ("PolyTypeReflection", "PolyTypeSourceGen"):
                    mean = records[operation, scenario, method]["statistics"]["Mean"]
                    matched = "StjReflection" if method == "PolyTypeReflection" else "StjSourceGen"
                    matched_mean = records[operation, scenario, matched]["statistics"]["Mean"]
                    fastest_mean = fastest["statistics"]["Mean"]
                    if mean > min(matched_mean, fastest_mean):
                        print(f"{operation} {scenario} {method}: "
                              f"{(mean / matched_mean - 1) * 100:+.1f}% time vs {matched}, "
                              f"{(mean / fastest_mean - 1) * 100:+.1f}% vs fastest STJ ({fastest['method']})")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="BDN results directory, or the committed results.json")
    parser.add_argument("--output", type=Path, default=Path("docs/images/performance"))
    parser.add_argument("--date", default=date.today().isoformat(), help="Measurement date for BDN imports")
    args = parser.parse_args()
    data = read_reports(args.input, args.date) if args.input.is_dir() else json.loads(args.input.read_text())
    records = validate(data)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "results.json").write_text(json.dumps(data, indent=2, allow_nan=False) + "\n")
    for group, scenarios in SCENARIOS.items():
        for operation in ("Serialize", "Deserialize"):
            (args.output / f"{operation.lower()}-{group}.svg").write_text(chart(records, operation, group, scenarios))
    print_gaps(records)


if __name__ == "__main__":
    main()
