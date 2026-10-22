from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
import os
from pathlib import Path
import tempfile
import unittest

from tools.parity.generate import generate, main
from tools.parity.model import StatusError
from tools.parity.tests.fixture import COMMIT, encoded


class CliTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="wolf-parity-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "WolfConquer.sln").write_text("synthetic repository marker\n", encoding="utf-8")
        self.folder = self.root / "docs" / "parity"
        self.folder.mkdir(parents=True)
        (self.folder / "status.json").write_bytes(encoded())

    def _main(self, *extra):
        with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
            return main(["--root", str(self.root), "--commit", COMMIT, *extra])

    def _outputs(self):
        return {name: ((self.folder / name).read_bytes(), (self.folder / name).stat().st_mtime_ns)
                for name in ("progress.gif", "progress.png", "PROGRESS.md")}

    def test_generate_writes_only_named_outputs_and_check_is_byte_exact_read_only(self):
        source = (self.folder / "status.json").read_bytes()
        self.assertEqual(0, self._main())
        self.assertEqual({"status.json", "progress.gif", "progress.png", "PROGRESS.md"}, {path.name for path in self.folder.iterdir()})
        before = self._outputs()
        self.assertEqual(0, self._main("--check"))
        self.assertEqual(before, self._outputs())
        self.assertEqual(source, (self.folder / "status.json").read_bytes())
        (self.folder / "progress.png").write_bytes(b"stale output")
        stale = self._outputs()
        self.assertEqual(1, self._main("--check"))
        self.assertEqual(stale, self._outputs())

    def test_invalid_input_fails_before_replacing_any_existing_output(self):
        for name in ("progress.gif", "progress.png", "PROGRESS.md"):
            (self.folder / name).write_bytes(b"retained original")
        before = self._outputs()
        (self.folder / "status.json").write_bytes(encoded().replace(b'"percent":55', b'"percent":101'))
        self.assertEqual(2, self._main())
        self.assertEqual(before, self._outputs())

    def test_missing_check_outputs_and_bad_commit_cannot_create_files(self):
        self.assertEqual(2, self._main("--check"))
        with self.assertRaises(StatusError):
            generate(self.root, "HEAD")
        self.assertEqual({"status.json"}, {path.name for path in self.folder.iterdir()})

    def _link(self, link, target, directory=False):
        try:
            os.symlink(target, link, target_is_directory=directory)
        except (OSError, NotImplementedError) as error:
            self.skipTest(f"Owned symlink fixture unavailable on this host: {type(error).__name__}")

    def test_output_symlink_is_refused_without_touching_destination(self):
        outside = self.root / "owned-outside.txt"
        outside.write_bytes(b"preserve owned destination")
        self._link(self.folder / "progress.png", outside)
        with self.assertRaises(StatusError):
            generate(self.root, COMMIT)
        self.assertEqual(b"preserve owned destination", outside.read_bytes())
        self.assertFalse((self.folder / "progress.gif").exists())

    def test_input_directory_reparse_is_refused_before_output_creation(self):
        original = self.root / "owned-input-directory"
        self.folder.rename(original)
        self._link(self.folder, original, directory=True)
        with self.assertRaises(StatusError):
            generate(self.root, COMMIT)
        self.assertEqual({"status.json"}, {path.name for path in original.iterdir()})


if __name__ == "__main__":
    unittest.main()
