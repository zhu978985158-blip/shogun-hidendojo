"""Exercise file operations in a temporary game tree; never touch a Steam install."""
from pathlib import Path
import os
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]


class InstallationTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.base = Path(self.sandbox.name)
        self.game = self.base / "game"
        (self.game / "ShogunShowdown.app/Contents/Resources/Data/Managed").mkdir(parents=True)
        self.core = self.game / "BepInEx/core"
        self.core.mkdir(parents=True)
        for name in ["BepInEx.dll", "0Harmony.dll"]:
            (self.core / name).write_bytes(b"dependency fixture")
        self.plugins = self.game / "BepInEx/plugins"
        self.plugins.mkdir()
        self.target = self.plugins / "HidenDojo.dll"
        self.target.write_bytes(b"old plugin fixture")
        self.other = self.plugins / "OtherMod.dll"
        self.other.write_bytes(b"keep other mod")
        self.config = self.game / "BepInEx/config/tong.shogunshowdown.hidendojo.cfg"
        self.config.parent.mkdir()
        self.config.write_text("keep user settings")
        self.launcher = self.game / "run_bepinex.sh"
        self.launcher.write_text("keep loader")
        self.source = self.base / "new.dll"
        self.source.write_bytes(b"new plugin fixture")
        self.commands = self.base / "commands"
        self.commands.mkdir()
        self.pgrep = self.commands / "pgrep"
        self.pgrep.write_text("#!/bin/sh\nexit 1\n")
        self.pgrep.chmod(0o755)
        self.env = dict(os.environ, STEAM_SHOGUN_ROOT=str(self.game),
                        PATH=str(self.commands) + os.pathsep + os.environ["PATH"])

    def run_script(self, name, *args, success=True):
        result = subprocess.run(["/bin/bash", str(ROOT / name), *map(str, args)],
                                env=self.env, capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)
        return result

    def assert_other_files_unchanged(self):
        self.assertEqual(self.other.read_bytes(), b"keep other mod")
        self.assertEqual(self.config.read_text(), "keep user settings")
        self.assertEqual(self.launcher.read_text(), "keep loader")

    def test_update_then_uninstall_preserves_both_versions_and_other_mods(self):
        self.run_script("install.sh", self.source)
        self.assertEqual(self.target.read_bytes(), b"new plugin fixture")
        self.run_script("restore.sh")
        self.assertFalse(self.target.exists())
        backups = list((self.game / "HidenDojo-backups").glob("*/HidenDojo.dll"))
        self.assertEqual(sorted(p.read_bytes() for p in backups),
                         [b"new plugin fixture", b"old plugin fixture"])
        self.run_script("restore.sh")
        self.assert_other_files_unchanged()

    def test_missing_loader_stops_before_replacing_plugin(self):
        (self.core / "0Harmony.dll").unlink()
        self.run_script("install.sh", self.source, success=False)
        self.assertEqual(self.target.read_bytes(), b"old plugin fixture")
        self.assert_other_files_unchanged()

    def test_running_game_stops_install_and_uninstall(self):
        self.pgrep.write_text("#!/bin/sh\nexit 0\n")
        self.run_script("install.sh", self.source, success=False)
        self.run_script("restore.sh", success=False)
        self.assertEqual(self.target.read_bytes(), b"old plugin fixture")
        self.assert_other_files_unchanged()

    def test_symlink_target_is_rejected(self):
        self.target.unlink()
        self.target.symlink_to(self.other)
        self.run_script("install.sh", self.source, success=False)
        self.run_script("restore.sh", success=False)
        self.assertTrue(self.target.is_symlink())
        self.assert_other_files_unchanged()

    def test_process_check_error_stops_without_changing_files(self):
        self.pgrep.write_text("#!/bin/sh\nexit 2\n")
        self.run_script("install.sh", self.source, success=False)
        self.run_script("restore.sh", success=False)
        self.assertEqual(self.target.read_bytes(), b"old plugin fixture")
        self.assert_other_files_unchanged()


if __name__ == "__main__":
    unittest.main()
