from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path


class CliSessionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.package_root = Path(__file__).resolve().parent.parent
        self.temp_dir = tempfile.TemporaryDirectory()
        self.state_dir = Path(self.temp_dir.name) / "state"
        self.fake_child = Path(self.temp_dir.name) / "fake_child.py"
        self.fake_child.write_text(FAKE_CHILD, encoding="utf-8")
        self.env = dict(os.environ)
        python_path = os.pathsep.join(
            [
                str(self.package_root),
                str(self.package_root / "src"),
                self.env.get("PYTHONPATH", ""),
            ]
        )
        self.env["PYTHONPATH"] = python_path
        self.env["PYTHONUTF8"] = "1"

    def tearDown(self) -> None:
        self._run_cli("shutdown-manager", check=False)
        self.temp_dir.cleanup()

    def test_start_send_read_and_stop_session(self) -> None:
        started = self._run_cli(
            "start",
            "--id",
            "main",
            "--",
            sys.executable,
            str(self.fake_child),
            "default",
        )

        self.assertEqual(started["state"], "ready")
        self.assertIn("started:default", started["stdout"])
        self.assertTrue(started["stdout"].endswith("defaultエージェント> "))

        sent = self._run_cli("send", "--id", "main", "hello")

        self.assertEqual(sent["state"], "ready")
        self.assertIn("echo:hello", sent["stdout"])

        listed = self._run_cli("list")
        self.assertEqual([session["id"] for session in listed["sessions"]], ["main"])

        stopped = self._run_cli("stop", "--id", "main")
        self.assertEqual(stopped["state"], "exited")
        self.assertIn("bye", stopped["stdout"])

    def test_approval_and_denial_reason_states(self) -> None:
        self._start_session("main")

        approval = self._run_cli("send", "--id", "main", "ask")
        self.assertEqual(approval["state"], "approval")
        self.assertTrue(approval["stdout"].endswith("実行しますか? YES/NO: "))

        denial_reason = self._run_cli("send", "--id", "main", "NO")
        self.assertEqual(denial_reason["state"], "denial_reason")
        self.assertTrue(denial_reason["stdout"].endswith("NO の理由 (任意): "))

        ready = self._run_cli("send", "--id", "main", "not now")
        self.assertEqual(ready["state"], "ready")
        self.assertIn("denied:not now", ready["stdout"])

    def test_multiple_session_ids_keep_independent_processes(self) -> None:
        self._start_session("one", child_name="one")
        self._start_session("two", child_name="two")

        one = self._run_cli("send", "--id", "one", "alpha")
        two = self._run_cli("send", "--id", "two", "beta")

        self.assertIn("echo:alpha", one["stdout"])
        self.assertIn("oneエージェント> ", one["stdout"])
        self.assertIn("echo:beta", two["stdout"])
        self.assertIn("twoエージェント> ", two["stdout"])

    def test_timeout_reports_running_and_later_read_returns_prompt(self) -> None:
        self._start_session("slow")

        timed_out = self._run_cli("send", "--id", "slow", "--timeout", "0.1", "sleep")
        self.assertEqual(timed_out["state"], "running")
        self.assertTrue(timed_out["timed_out"])

        ready = self._run_cli("read", "--id", "slow", "--timeout", "3")
        self.assertEqual(ready["state"], "ready")
        self.assertIn("awake", ready["stdout"])

    def _start_session(self, session_id: str, *, child_name: str = "default") -> dict:
        return self._run_cli(
            "start",
            "--id",
            session_id,
            "--",
            sys.executable,
            str(self.fake_child),
            child_name,
        )

    def _run_cli(self, *args: str, check: bool = True) -> dict:
        completed = subprocess.run(
            [
                sys.executable,
                "-m",
                "cli_session",
                "--state-dir",
                str(self.state_dir),
                *args,
            ],
            cwd=str(self.package_root),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=self.env,
            timeout=15,
            check=False,
        )
        if check and completed.returncode != 0:
            self.fail(
                "cli-session failed\n"
                f"args={args}\n"
                f"stdout={completed.stdout}\n"
                f"stderr={completed.stderr}"
            )

        if not completed.stdout.strip():
            return {}

        return json.loads(completed.stdout)


FAKE_CHILD = textwrap.dedent(
    """
    from __future__ import annotations

    import sys
    import time

    name = sys.argv[1] if len(sys.argv) > 1 else "default"
    prompt = f"{name}エージェント> "
    waiting_for_reason = False

    sys.stdout.write(f"started:{name}\\n{prompt}")
    sys.stdout.flush()

    for raw_line in sys.stdin:
        line = raw_line.rstrip("\\n")
        if waiting_for_reason:
            waiting_for_reason = False
            sys.stdout.write(f"denied:{line}\\n{prompt}")
            sys.stdout.flush()
            continue

        if line.lower() == "exit":
            sys.stdout.write("bye\\n")
            sys.stdout.flush()
            break

        if line == "ask":
            sys.stdout.write("\\nAI がコマンド実行を要求しています:\\nrun thing\\n実行しますか? YES/NO: ")
            sys.stdout.flush()
            continue

        if line == "NO":
            waiting_for_reason = True
            sys.stdout.write("NO の理由 (任意): ")
            sys.stdout.flush()
            continue

        if line == "YES":
            sys.stdout.write(f"approved\\n{prompt}")
            sys.stdout.flush()
            continue

        if line == "sleep":
            time.sleep(0.5)
            sys.stdout.write(f"awake\\n{prompt}")
            sys.stdout.flush()
            continue

        sys.stdout.write(f"echo:{line}\\n{prompt}")
        sys.stdout.flush()
    """
)


if __name__ == "__main__":
    unittest.main()
