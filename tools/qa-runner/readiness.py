"""Opt-in QA003: bounded, same-scale Pillow template detection; no desktop/input API."""
from dataclasses import dataclass
import hashlib
import math
from pathlib import Path

MAX_FRAME_SIZE = (1920, 1080)
MAX_CANDIDATES = 4096


@dataclass(frozen=True)
class ReadyOptions:
    reference: Path
    timeout_seconds: float = 10.0
    interval_seconds: float = 0.2
    tolerance: float = 12.0
    consistent_count: int = 3

    def __post_init__(self):
        object.__setattr__(self, "reference", Path(self.reference).resolve())
        for name, low, high in (("timeout_seconds", 0.05, 60),
                                ("interval_seconds", 0.02, 5), ("tolerance", 1, 20)):
            try:
                value = float(getattr(self, name))
            except (TypeError, ValueError, OverflowError) as exc:
                raise ValueError(f"{name} must be finite in [{low}, {high}]") from exc
            if not math.isfinite(value) or not low <= value <= high:
                raise ValueError(f"{name} must be finite in [{low}, {high}]")
            object.__setattr__(self, name, value)
        if (isinstance(self.consistent_count, bool) or not isinstance(self.consistent_count, int)
                or not 2 <= self.consistent_count <= 10):
            raise ValueError("consistent_count must be an integer in [2, 10]")


def initial_report(options=None):
    report = {"state": "not_checked" if options is None else "blocked", "requested": options is not None,
              "attempts": 0, "score": None, "location": None, "streak": 0,
              "time_to_ready_seconds": None, "evidence_path": None}
    if options is not None:
        report.update(reference_path=str(options.reference), timeout_seconds=options.timeout_seconds,
                      interval_seconds=options.interval_seconds, tolerance=options.tolerance,
                      consistent_count=options.consistent_count, error="Readiness has not completed.")
    return report


class Detector:
    def __init__(self, path, tolerance=12):
        from PIL import Image
        # Reuse validation bounds even for direct offline detector callers.
        self.tolerance = ReadyOptions(path, tolerance=tolerance).tolerance
        path = Path(path)
        with path.open("rb") as stream:
            payload = stream.read(1024 * 1024 + 1)
        if len(payload) > 1024 * 1024:
            raise ValueError("reference exceeds 1 MiB limit")
        self.sha256 = hashlib.sha256(payload).hexdigest()
        from io import BytesIO
        with Image.open(BytesIO(payload)) as image:
            width, height = image.size
            if (image.format != "PNG" or not 16 <= width <= 256 or not 8 <= height <= 128
                    or width * height > 32768):
                raise ValueError("reference must be PNG, 16..256 x 8..128, at most 32768 pixels")
            image.verify()
        with Image.open(BytesIO(payload)) as image:
            if image.convert("RGBA").getchannel("A").getextrema() != (255, 255):
                raise ValueError("reference must be opaque")
            self.reference = image.convert("RGB")
        gray = self.reference.convert("L")
        low, high = gray.getextrema()
        if high - low < 80:
            raise ValueError("blank/low-information reference: contrast below 80")
        self.threshold = (low + high) // 2
        self.mask = gray.point(lambda p: 255 if p > self.threshold else 0)
        self.foreground = self.mask.histogram()[255]
        if not 32 <= self.foreground <= width * height * 0.5 or self.foreground < width * height * 0.03:
            raise ValueError("low-information reference: insufficient foreground/background")
        rows = [self.mask.crop((0, y, width, y + 1)).tobytes() for y in range(height)]
        transitions = [sum(a != b for a, b in zip(row, row[1:])) for row in rows]
        if max(transitions) < 8 or sum(t >= 8 for t in transitions) < 3:
            raise ValueError("low-information reference: insufficient pattern detail")
        self.anchor_y = max(range(height), key=lambda y: transitions[y])
        self.anchor = rows[self.anchor_y]

    def match(self, image, *, check):
        """Return one unambiguous location or no match. `check` enforces live deadline/safety."""
        from PIL import ImageChops, ImageStat
        check()
        width, height = image.size
        rw, rh = self.reference.size
        if width > MAX_FRAME_SIZE[0] or height > MAX_FRAME_SIZE[1] or width < rw or height < rh:
            raise ValueError("frame outside bounded supported dimensions (reference..1920x1080)")
        frame = image.convert("RGB")
        mask = frame.convert("L").point(lambda p: 255 if p > self.threshold else 0)
        check()
        candidates, matches = 0, []
        best = None
        for y in range(height - rh + 1):
            check()
            row = mask.crop((0, y + self.anchor_y, width, y + self.anchor_y + 1)).tobytes()
            x = row.find(self.anchor)
            while x >= 0:
                check()
                candidates += 1
                if candidates > MAX_CANDIDATES:
                    raise ValueError("template candidate budget exceeded; ambiguous/repetitive frame")
                box = (x, y, x + rw, y + rh)
                difference = ImageChops.difference(frame.crop(box), self.reference)
                whole = max(ImageStat.Stat(difference).mean)
                foreground = max(ImageStat.Stat(difference, self.mask).mean)
                peak = max(high for low, high in difference.getextrema())
                mismatch = ImageChops.difference(mask.crop(box), self.mask).histogram()[255]
                score = max(whole, foreground)
                best = score if best is None else min(best, score)
                if score <= self.tolerance and peak <= self.tolerance * 3 and mismatch <= max(1, self.foreground * 0.02):
                    matches.append([x, y])
                    if len(matches) > 1:
                        check()
                        return {"location": None, "score": best, "candidates": candidates, "ambiguous": True}
                x = row.find(self.anchor, x + 1)
        check()
        return {"location": matches[0] if matches else None, "score": best,
                "candidates": candidates, "ambiguous": False}


def wait_ready(control):
    """Use only the demo's guarded client screenshots. Never sends/releases keys."""
    options, report = control.options.readiness, control.report["readiness"]
    start = control.clock.monotonic()
    end = min(start + options.timeout_seconds,
              control.deadline - control.options.hold_seconds - control.options.capture_delay_seconds)
    last = None
    previous = None

    def check():
        control.guard()
        control.gui.failSafeCheck()
        if control.clock.monotonic() >= end:
            raise RuntimeError("readiness timeout within original observation deadline")

    try:
        check()
        detector = Detector(options.reference, options.tolerance)
        report["reference_sha256"] = detector.sha256
        check()
        while True:
            check()
            report["attempts"] += 1
            image, region = control.acquire_image({}, client=True)
            captured = control.clock.monotonic()
            # This is historical, guard-validated evidence even if analysis subsequently fails.
            last = (image, region, captured)

            def frame_check():
                check()
                if control.windows.client_region(control.hwnd) != region:
                    raise RuntimeError("client geometry changed during readiness analysis")
                control.guard()

            result = detector.match(image, check=frame_check)
            frame_check()
            location = result["location"]
            identity = (region, location) if location is not None else None
            stable = identity is not None and identity == previous
            report["streak"] = report["streak"] + 1 if stable else int(identity is not None)
            previous = identity
            report.update(score=result["score"], location=location, candidates=result["candidates"],
                          ambiguous=result["ambiguous"])
            if report["streak"] >= options.consistent_count:
                report.update(state="ready", time_to_ready_seconds=control.clock.monotonic() - start)
                report.pop("error", None)
                return
            wait_end = min(control.clock.monotonic() + options.interval_seconds, end)
            while control.clock.monotonic() < wait_end:
                check()
                control.clock.sleep(max(0, min(0.02, wait_end - control.clock.monotonic())))
    except (Exception, KeyboardInterrupt) as exc:
        report.update(state="blocked", error=f"{type(exc).__name__}: {exc}")
        # No fresh capture on failure. Saving a previous safe frame gives no current authority.
        if last is None:
            report["evidence_error"] = "No previously guard-validated readiness frame available."
        elif control.clock.monotonic() >= control.deadline:
            report["evidence_error"] = "Global observation deadline reached; diagnostic save not started."
        else:
            image, region, captured = last
            diagnostic = {}
            try:
                control.save_image(image, region, diagnostic, purpose="readiness_diagnostic",
                                   captured_at=captured, historical=True)
                report["evidence_path"] = control.report["evidence"][-1]["path"]
            except (Exception, KeyboardInterrupt) as evidence_exc:
                report["evidence_error"] = f"{type(evidence_exc).__name__}: {evidence_exc}"
                if "discarded_evidence_path" in diagnostic:
                    report["discarded_evidence_path"] = diagnostic["discarded_evidence_path"]
                if isinstance(evidence_exc, KeyboardInterrupt):
                    raise
        raise


DEADLINE_MARGIN = 0.1


def wait_stable_state(control, detector, *, present, timeout_seconds, interval_seconds=0.2,
                      consistent_count=3):
    """Wait for the reference to be stably present/absent in guarded client frames.

    Returns {status, frames, location, frame}: `observed` when the expected state was
    stable, `contradicted` when the opposite state was the last stable one at timeout,
    else `unavailable`. Guard/deadline failures raise; never sends or releases keys.
    """
    clock = control.clock
    start = clock.monotonic()
    end = min(start + timeout_seconds, control.deadline - DEADLINE_MARGIN)
    if end <= start:
        raise RuntimeError("insufficient time before observation deadline")
    expected = "present" if present else "absent"
    frames, streak, previous, last_stable, location, frame = 0, 0, None, None, None, None

    def check():
        control.guard()
        control.gui.failSafeCheck()

    while True:
        check()
        image, region = control.acquire_image({}, client=True)
        frames += 1

        def frame_check():
            check()
            if control.windows.client_region(control.hwnd) != region:
                raise RuntimeError("client geometry changed during visual wait")

        result = detector.match(image, check=frame_check)
        frame_check()
        location, frame = result["location"], (image, region)
        if result["ambiguous"]:
            state, identity = None, None
        elif location is None:
            state, identity = "absent", (region, None)
        else:
            state, identity = "present", (region, tuple(location))
        streak = streak + 1 if state is not None and identity == previous else int(state is not None)
        previous = identity
        if streak >= consistent_count:
            last_stable = state
            if state == expected:
                return {"status": "observed", "frames": frames, "location": location, "frame": frame}
        if clock.monotonic() >= end:
            break
        wait_end = min(clock.monotonic() + interval_seconds, end)
        while clock.monotonic() < wait_end:
            check()
            clock.sleep(max(0, min(0.02, wait_end - clock.monotonic())))
    return {"status": "contradicted" if last_stable is not None else "unavailable",
            "frames": frames, "location": location, "frame": frame}
