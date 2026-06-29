"""Local inference server: loads the model once at startup and serves the viewer plus extraction endpoints."""

from __future__ import annotations

import base64
import os
import io
import tempfile
from contextlib import asynccontextmanager
from pathlib import Path

import cairosvg
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from PIL import Image  # Added to process incoming raster images

from buildingcv.extract_polygons import PolygonExtractor
from buildingcv.svg_render import filtered_svg_bytes

# Default location for downloaded weights — clean clone-and-go path. Local
# training writes `.pt` to `runs/<version>/<timestamp>/`; export with
# `scripts/export_safetensors.py` and point the env var here.
DEFAULT_RUN_DIR = "weights"
DEFAULT_CKPT = "best.safetensors"
DEFAULT_DEVICE = "auto"

# CubiCasa SVGs go up to a few MB. 16 MB ceiling is generous and keeps the
# server from being a free upload sink if it ever leaves localhost.
MAX_UPLOAD_BYTES = 16 * 1024 * 1024

# Repo root, derived once at import time. Used to resolve the viewer HTML
# and the sample SVGs without depending on the cwd uvicorn was launched from.
REPO_ROOT = Path(__file__).resolve().parent.parent
VIEWER_HTML = REPO_ROOT / "viewer" / "index.html"

# Curated samples shown as "Try a sample" buttons in the viewer. Keys are
# short ids (used in the URL); values are dataset-relative paths. The set
# is small on purpose — three plans of contrasting shape are enough to
# show the viewer handles different aspect ratios and complexities.
SAMPLES: dict[str, str] = {
    "1191": "high_quality_architectural/1191",
    "4068": "high_quality_architectural/4068",
    "3676": "high_quality/3676",
}


@asynccontextmanager
async def lifespan(app: FastAPI):
    run_dir = Path(os.environ.get("BUILDINGCV_RUN_DIR", DEFAULT_RUN_DIR))
    ckpt = os.environ.get("BUILDINGCV_CKPT", DEFAULT_CKPT)
    device = os.environ.get("BUILDINGCV_DEVICE", DEFAULT_DEVICE)
    extractor = PolygonExtractor(run_dir=run_dir, ckpt=ckpt, device=device)
    print(f"[server] loaded {run_dir/ckpt} on {extractor.device} (epoch {extractor.epoch})")
    app.state.extractor = extractor
    yield


app = FastAPI(title="BuildingCV inference", lifespan=lifespan)

# Vite's dev server runs on a different port than this one (usually 5173).
# Permissive CORS is fine on localhost — the server isn't reachable
# off-host. Tighten this if you ever expose it.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/healthz")
def healthz() -> dict:
    extractor: PolygonExtractor = app.state.extractor
    return {
        "ok": True,
        "device": str(extractor.device),
        "epoch": extractor.epoch,
        "image_size": list(extractor.image_size),
        "letterbox": extractor.letterbox,
    }


@app.get("/")
def index() -> FileResponse:
    """Serve the viewer HTML at the root URL."""
    return FileResponse(VIEWER_HTML)


# The viewer loads its sample plans from `./demos/{key}.json` so it works
# unchanged on the static Pages deploy. Mount the same files here so local
# dev hits the prebuilt JSON (no inference per click) — regenerate with
# `python scripts/build_demos.py` after retraining.
DEMOS_DIR = REPO_ROOT / "viewer" / "demos"
if DEMOS_DIR.is_dir():
    app.mount("/demos", StaticFiles(directory=DEMOS_DIR), name="demos")


def _attach_input_image(svg_path: Path, result: dict) -> dict:
    """Render a transparent-background PNG of the structural SVG or resize the input image
    at content size and inline it as base64 on `result`.
    """
    _, _, inner_w, inner_h = result["content_rect"]
    suffix = svg_path.suffix.lower()
    
    if suffix in (".png", ".jpg", ".jpeg"):
        # Load and resize raster image directly using Pillow
        with Image.open(svg_path) as img:
            img_rgba = img.convert("RGBA")
            img_resized = img_rgba.resize((inner_w, inner_h), Image.BILINEAR)
            buffer = io.BytesIO()
            img_resized.save(buffer, format="PNG")
            png = buffer.getvalue()
    else:
        # Standard SVG render
        png = cairosvg.svg2png(
            bytestring=filtered_svg_bytes(svg_path),
            output_width=inner_w,
            output_height=inner_h,
        )
    
    result["input_image_b64"] = base64.b64encode(png).decode("ascii")
    return result


@app.get("/sample/{key}")
def sample(key: str) -> dict:
    """Run extraction on a server-known dataset SVG."""
    if key not in SAMPLES:
        raise HTTPException(status_code=404, detail=f"unknown sample: {key}")
    data_dir = Path(os.environ.get("BUILDINGCV_DATA_DIR", REPO_ROOT / "data" / "cubicasa5k"))
    svg_path = data_dir / SAMPLES[key] / "model.svg"
    if not svg_path.exists():
        raise HTTPException(status_code=404, detail=f"sample SVG not found: {svg_path}")
    extractor: PolygonExtractor = app.state.extractor
    return _attach_input_image(svg_path, extractor.extract(svg_path))


@app.post("/extract")
async def extract(
    svg: UploadFile = File(None),
    file: UploadFile = File(None),
    image: UploadFile = File(None)
) -> dict:
    """Run the model on the uploaded file (SVG, PNG, JPG, or JPEG) and return the polygon JSON.

    Accepts file upload under form field names: svg, file, or image.
    """
    uploaded_file = svg or file or image
    if not uploaded_file:
        raise HTTPException(status_code=400, detail="no file uploaded")
        
    raw = await uploaded_file.read()
    if not raw:
        raise HTTPException(status_code=400, detail="empty upload")
    if len(raw) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"upload exceeds {MAX_UPLOAD_BYTES} bytes")

    filename = uploaded_file.filename.lower() if uploaded_file.filename else ""

    extractor: PolygonExtractor = app.state.extractor
    
    # Determine the temp file extension based on input format
    if filename.endswith(".png"):
        suffix = ".png"
    elif filename.endswith((".jpg", ".jpeg")):
        suffix = ".jpg"
    else:
        suffix = ".svg"
        
    with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as tmp:
        # For both raster and vector files, we write raw bytes directly
        tmp.write(raw)
        tmp.flush()
        tmp_path = Path(tmp.name)

    try:
        try:
            result = extractor.extract(tmp_path)
        except Exception as e:
            # Catch bad parse states cleanly and prevent core system lockups
            raise HTTPException(status_code=422, detail=f"failed to extract: {e}") from e
        return _attach_input_image(tmp_path, result)
    finally:
        # Wipe temporary asset from the system volume storage array cleanly
        tmp_path.unlink(missing_ok=True)