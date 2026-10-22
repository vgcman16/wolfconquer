from io import BytesIO
import json
import unittest

from PIL import Image, ImageColor

from tools.parity.markdown import render_markdown
from tools.parity.model import parse
from tools.parity.render import (BAR_BOTTOM, BAR_LEFT, BAR_RIGHT, BAR_TOP, FRAME_COUNT,
                                 FRAME_DURATION_MS, JADE, METADATA_KEY, SHINE, TRACK, render_images)
from tools.parity.tests.fixture import COMMIT, encoded, record


class RenderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.status = parse(encoded())
        cls.gif, cls.png = render_images(cls.status, COMMIT)

    def test_real_gif_frames_duration_size_and_static_png_match(self):
        with Image.open(BytesIO(self.gif)) as animated, Image.open(BytesIO(self.png)) as static:
            self.assertEqual("GIF", animated.format)
            self.assertEqual("PNG", static.format)
            self.assertEqual((760, 580), animated.size)
            self.assertEqual(animated.size, static.size)
            self.assertEqual(24, animated.n_frames)
            self.assertEqual(animated.convert("RGB").tobytes(), static.convert("RGB").tobytes())
            self.assertLess(len(self.gif), 500000)
            duration = 0
            for index in range(animated.n_frames):
                animated.seek(index)
                duration += animated.info["duration"]
            self.assertEqual(1920, duration)

    def test_fixed_bar_extent_in_every_frame_never_simulates_completion_growth(self):
        end = BAR_LEFT + (BAR_RIGHT - BAR_LEFT) * 55 // 100
        filled_colors = {ImageColor.getrgb(JADE), ImageColor.getrgb(SHINE)}
        with Image.open(BytesIO(self.gif)) as animated:
            for index in range(FRAME_COUNT):
                animated.seek(index)
                frame = animated.convert("RGB")
                row = (BAR_TOP + BAR_BOTTOM) // 2
                for x in range(BAR_LEFT, BAR_RIGHT):
                    if x < end:
                        self.assertIn(frame.getpixel((x, row)), filled_colors)
                    else:
                        self.assertEqual(ImageColor.getrgb(TRACK), frame.getpixel((x, row)))

    def test_output_metadata_and_markdown_keep_data_dates_and_full_boundary_details(self):
        with Image.open(BytesIO(self.png)) as static, Image.open(BytesIO(self.gif)) as animated:
            metadata = json.loads(static.info[METADATA_KEY])
            prefix, gif_metadata = animated.info["comment"].split(b"\n", 1)
            self.assertEqual(METADATA_KEY.encode("ascii"), prefix)
            self.assertEqual(metadata, json.loads(gif_metadata))
        self.assertEqual((55, 40, 70, "low"), tuple(metadata[key] for key in ("percent", "low", "high", "confidence")))
        self.assertEqual("2026-10-19", metadata["estimateAsOf"])
        self.assertEqual("2026-10-22", metadata["reviewedAt"])
        self.assertEqual(COMMIT, metadata["sourceCommit"])
        self.assertEqual(record()["milestones"], metadata["milestones"])
        markdown = render_markdown(self.status, COMMIT).decode("utf-8")
        for text in ("55%", "40-70%", "low confidence", "2026-10-19", "2026-10-22", COMMIT):
            self.assertIn(text, markdown)
        for milestone in record()["milestones"]:
            self.assertIn(milestone["title"], markdown)
            self.assertIn(milestone["detail"], markdown)

    def test_same_inputs_produce_identical_bytes(self):
        self.assertEqual((self.gif, self.png), render_images(parse(encoded()), COMMIT))
        self.assertEqual(render_markdown(self.status, COMMIT), render_markdown(parse(encoded()), COMMIT))

    def test_zero_percent_still_has_real_shine_frames_and_no_fill(self):
        raw = record()
        raw["estimate"].update(percent=0, low=0, high=10)
        gif, _ = render_images(parse(encoded(raw)), COMMIT)
        with Image.open(BytesIO(gif)) as animated:
            self.assertEqual(FRAME_COUNT, animated.n_frames)
            self.assertEqual(FRAME_DURATION_MS, animated.info["duration"])
            for index in range(animated.n_frames):
                animated.seek(index)
                self.assertEqual(ImageColor.getrgb(TRACK), animated.convert("RGB").getpixel((BAR_LEFT + 20, 194)))

    def test_card_shortening_does_not_remove_complete_markdown_detail(self):
        raw = record()
        raw["milestones"][0]["detail"] = "Qualified proof only. " + "x" * 450
        status = parse(encoded(raw))
        self.assertIn(raw["milestones"][0]["detail"], render_markdown(status, COMMIT).decode("utf-8"))


if __name__ == "__main__":
    unittest.main()
