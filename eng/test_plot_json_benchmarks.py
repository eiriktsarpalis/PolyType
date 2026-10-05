import copy
import importlib.util
import json
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


spec = importlib.util.spec_from_file_location("plotter", Path(__file__).with_name("plot-json-benchmarks.py"))
plotter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(plotter)


class PlotJsonBenchmarksTests(unittest.TestCase):
    def setUp(self):
        self.data = {"measured_on": "2026-10-02", "host": {"ProcessorName": "test"}, "benchmarks": []}
        for operation in ("Serialize", "Deserialize"):
            for scenarios in plotter.SCENARIOS.values():
                for scenario, _ in scenarios:
                    for method in plotter.METHODS:
                        if operation == "Deserialize" and method == "StjSourceGen_FastPath":
                            continue
                        self.data["benchmarks"].append({
                            "operation": operation,
                            "scenario": scenario,
                            "method": method,
                            "job": "test",
                            "statistics": {"N": 3, "OriginalValues": [99, 100, 101], "Mean": 100,
                                           "StandardError": 1, "StandardDeviation": 1,
                                           "ConfidenceInterval": {"Lower": 90, "Upper": 110}},
                            "allocated_bytes": 0,
                        })

    def test_complete_dataset_and_all_charts(self):
        records = plotter.validate(self.data)
        self.assertEqual(len(records), 81)
        for group, scenarios in plotter.SCENARIOS.items():
            for operation, method_count in (("Serialize", 5), ("Deserialize", 4)):
                with self.subTest(group=group, operation=operation):
                    svg = plotter.chart(records, operation, group, scenarios)
                    root = ET.fromstring(svg)
                    self.assertEqual(root.attrib["role"], "img")
                    bars = root.findall("{http://www.w3.org/2000/svg}g/{http://www.w3.org/2000/svg}rect")
                    self.assertEqual(len(bars), len(scenarios) * method_count)
                    self.assertTrue(all(float(bar.attrib["width"]) > 0 for bar in bars))
                    self.assertIn("1.00x | 100.00 ns | 0 B", svg)

    def test_rejects_missing_duplicate_and_unknown_cases(self):
        for kind in ("missing", "duplicate", "unknown"):
            with self.subTest(kind=kind):
                data = copy.deepcopy(self.data)
                if kind == "missing":
                    data["benchmarks"].pop()
                elif kind == "duplicate":
                    data["benchmarks"].append(data["benchmarks"][0])
                else:
                    data["benchmarks"][0]["scenario"] = "Unknown"
                with self.assertRaises(ValueError):
                    plotter.validate(data)

    def test_rejects_invalid_measurements(self):
        for field, value in (("N", 2), ("Mean", 0), ("Mean", float("nan")),
                             ("StandardError", -1), ("StandardError", float("inf"))):
            with self.subTest(field=field, value=value):
                data = copy.deepcopy(self.data)
                data["benchmarks"][0]["statistics"][field] = value
                with self.assertRaises(ValueError):
                    plotter.validate(data)

    def test_import_preserves_samples_environment_and_methods(self):
        benchmarks = [{
            "Type": f"Json{record['operation']}Benchmark<{record['scenario']}>",
            "Method": f"{record['operation']}_{record['method']}",
            "DisplayInfo": record["job"],
            "Statistics": record["statistics"],
            "Memory": {"BytesAllocatedPerOperation": record["allocated_bytes"]},
        } for record in self.data["benchmarks"]]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "test-report-full-compressed.json"
            path.write_text(json.dumps({"HostEnvironmentInfo": self.data["host"], "Benchmarks": benchmarks}))
            imported = plotter.read_reports(Path(directory), self.data["measured_on"])
        self.assertEqual(imported, self.data)
        self.assertEqual(len(plotter.validate(imported)), 81)


if __name__ == "__main__":
    unittest.main()
