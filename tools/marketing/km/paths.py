"""Where things are: the repo's marketing folder, and the working folder outside it."""
from __future__ import annotations

import os
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent          # tools/marketing
SCHEMAS = HERE / "schemas"
BRAND = HERE / "brand.json"
REPO = HERE.parent.parent

# Media lives outside the repo, in the working folder: shots/, audio/, drafts/, assets/...
DEFAULT_ROOT = Path.home() / "TokenLimit" / "marketing"

# Google Drive, through rclone (Q7: the drive.file scope, so rclone only sees what it made).
DRIVE_REMOTE = os.environ.get("KEHAI_RCLONE_REMOTE", "gdrive")
DRIVE_FOLDER = "TokenLimit Marketing"


def root(override: str | os.PathLike | None = None) -> Path:
    """The working folder: --root, else KEHAI_MARKETING, else ~/TokenLimit/marketing."""
    if override:
        return Path(override).expanduser().resolve()
    env = os.environ.get("KEHAI_MARKETING")
    return Path(env).expanduser().resolve() if env else DEFAULT_ROOT
