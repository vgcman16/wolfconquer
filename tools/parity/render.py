"""Original bitmap card. Embedded bitmap lettering avoids system-font dependencies."""
from __future__ import annotations

from io import BytesIO
import json
import textwrap
import unicodedata

import PIL
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin

from .model import BOUNDARY_IDS, Status, StatusError, validate_commit

PILLOW_VERSION = "12.3.0"
WIDTH, HEIGHT = 760, 580
FRAME_COUNT, FRAME_DURATION_MS = 24, 80
BAR_LEFT, BAR_RIGHT, BAR_TOP, BAR_BOTTOM = 32, 728, 184, 204
BACKGROUND, PANEL, EDGE = "#08130f", "#10261b", "#244b36"
TEXT, MUTED, JADE, SHINE, TRACK, CAUTION = "#d7ebdd", "#8fa997", "#4add99", "#b5ffd4", "#193c2a", "#e8c77e"
METADATA_KEY = "wolfconquer.parity"
COLORS = (BACKGROUND, PANEL, EDGE, TEXT, MUTED, JADE, SHINE, TRACK, CAUTION)


def _ascii(value: str) -> str:
    value = value.replace("—", "-").replace("–", "-").replace("·", "/").replace("…", "...")
    return unicodedata.normalize("NFKD", value).encode("ascii", errors="replace").decode("ascii")


def _text(image: Image.Image, xy: tuple[int, int], value: str, color: str = TEXT, scale: int = 1) -> None:
    value = _ascii(value)
    font = ImageFont.load_default_imagefont()  # Pillow's pinned embedded bitmap, not OS/FreeType fonts.
    bounds = font.getbbox(value)
    mask = Image.new("L", (max(1, bounds[2] - bounds[0]), max(1, bounds[3] - bounds[1])))
    ImageDraw.Draw(mask).text((-bounds[0], -bounds[1]), value, font=font, fill=255)
    if scale != 1:
        mask = mask.resize((mask.width * scale, mask.height * scale), Image.Resampling.NEAREST)
    image.paste(color, xy, mask)


def _display_lines(value: str, width: int, maximum: int) -> list[str]:
    lines = textwrap.wrap(_ascii(value), width=width, break_long_words=True, break_on_hyphens=False)
    if len(lines) > maximum:
        lines = lines[:maximum]
        lines[-1] = lines[-1][:width - 3].rstrip() + "..."
    return lines


def provenance(status: Status, commit: str) -> bytes:
    estimate = status.estimate
    value = {"schemaVersion": 1, "percent": estimate.percent, "low": estimate.low, "high": estimate.high,
             "confidence": estimate.confidence, "estimateAsOf": estimate.as_of, "reviewedAt": status.reviewed_at,
             "scope": status.scope, "sourceCommit": validate_commit(commit),
             "milestones": [{"id": item.id, "title": item.title, "status": item.status, "detail": item.detail}
                            for item in status.milestones]}
    return json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(",", ":")).encode("ascii")


def _base_card(status: Status, commit: str) -> Image.Image:
    image = Image.new("RGB", (WIDTH, HEIGHT), BACKGROUND)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((8, 8, WIDTH - 9, HEIGHT - 9), radius=18, fill=BACKGROUND, outline=EDGE, width=2)
    draw.line((32, 65, 210, 65), fill=JADE, width=2)
    _text(image, (32, 28), "WOLFCONQUER", scale=3)
    _text(image, (500, 38), "PROJECT PLANNING", MUTED)
    for index, line in enumerate(_display_lines(status.scope, 110, 2)):
        _text(image, (32, 77 + index * 12), line, MUTED)
    estimate = status.estimate
    _text(image, (32, 105), f"{estimate.percent}%", JADE, scale=5)
    _text(image, (230, 111), f"{estimate.low}-{estimate.high}% / {estimate.confidence.upper()} CONFIDENCE", CAUTION, scale=2)
    _text(image, (230, 145), "Planning estimate, not a measured completion score.", MUTED)
    draw.rounded_rectangle((BAR_LEFT, BAR_TOP, BAR_RIGHT - 1, BAR_BOTTOM - 1), radius=7, fill=TRACK)
    filled = (BAR_RIGHT - BAR_LEFT) * estimate.percent // 100
    if filled:
        draw.rectangle((BAR_LEFT, BAR_TOP, BAR_LEFT + filled - 1, BAR_BOTTOM - 1), fill=JADE)
    for bound in (estimate.low, estimate.high):
        x = min(BAR_RIGHT - 1, BAR_LEFT + (BAR_RIGHT - BAR_LEFT) * bound // 100)
        draw.line((x, BAR_TOP - 5, x, BAR_TOP - 1), fill=CAUTION)
        draw.line((x, BAR_BOTTOM, x, BAR_BOTTOM + 4), fill=CAUTION)
    _text(image, (32, 219), f"Estimate dated {estimate.as_of} | status reviewed {status.reviewed_at}", MUTED)
    audit = status.source_audit
    _text(image, (32, 240), f"SOURCE {commit[:12]} | AUDIT {audit.files:,} files / {audit.lines:,} lines | {audit.target_comparison}", MUTED)
    draw.line((32, 261, 728, 261), fill=EDGE)
    by_id = {item.id: item for item in status.milestones}
    for index, key in enumerate(BOUNDARY_IDS):
        item = by_id[key]
        top = 276 + index * 67
        draw.rounded_rectangle((32, top, 728, top + 59), radius=7, fill=PANEL, outline=EDGE)
        title = _display_lines(item.title, 40, 1)[0]
        _text(image, (44, top + 7), title, TEXT, scale=2)
        badge = JADE if item.status == "verified" else CAUTION if item.status == "in-progress" else MUTED
        _text(image, (620, top + 10), item.status.upper(), badge)
        for line_index, line in enumerate(_display_lines(item.detail, 110, 2)):
            _text(image, (44, top + 31 + line_index * 12), line, MUTED)
    _text(image, (32, 554), "Source-backed planning. Green builds are not full or live-client parity.", MUTED)
    return image


def _palette() -> Image.Image:
    image = Image.new("P", (1, 1))
    values = [int(color[index:index + 2], 16) for color in COLORS for index in (1, 3, 5)]
    image.putpalette(values + [0] * (768 - len(values)))
    return image


def render_images(status: Status, commit: str) -> tuple[bytes, bytes]:
    if PIL.__version__ != PILLOW_VERSION:
        raise StatusError(f"deterministic rendering requires Pillow {PILLOW_VERSION}")
    commit = validate_commit(commit)
    base = _base_card(status, commit)
    palette = _palette()
    frames = []
    filled = (BAR_RIGHT - BAR_LEFT) * status.estimate.percent // 100
    for frame in range(FRAME_COUNT):
        image = base.copy()
        draw = ImageDraw.Draw(image)
        # A decorative border glint keeps all 24 frames distinct even at 0%.
        glint = BAR_LEFT + frame * (BAR_RIGHT - BAR_LEFT - 25) // (FRAME_COUNT - 1)
        draw.line((glint, 9, glint + 24, 9), fill=SHINE)
        if filled:
            shine_width = min(40, filled)
            left = BAR_LEFT + frame * max(0, filled - shine_width) // (FRAME_COUNT - 1)
            draw.rectangle((left, BAR_TOP + 2, left + shine_width - 1, BAR_BOTTOM - 3), fill=SHINE)
        frames.append(image.quantize(palette=palette, dither=Image.Dither.NONE))
    metadata = provenance(status, commit)
    png_info = PngImagePlugin.PngInfo()
    png_info.add_text(METADATA_KEY, metadata.decode("ascii"))
    png = BytesIO()
    frames[0].convert("RGB").save(png, format="PNG", pnginfo=png_info, compress_level=9, optimize=False)
    gif = BytesIO()
    frames[0].save(gif, format="GIF", save_all=True, append_images=frames[1:], duration=FRAME_DURATION_MS,
                   loop=0, disposal=1, optimize=False, comment=METADATA_KEY.encode("ascii") + b"\n" + metadata)
    if len(gif.getvalue()) > 500000:
        raise StatusError("animated card exceeded its 500 KB size bound")
    return gif.getvalue(), png.getvalue()
