"""The schemas agree with what writes to them: the sample edits, brand.json and GameNames.cs."""
import json
import re

import pytest

from km import paths, schemas

SAMPLES = sorted((paths.HERE / "editor" / "samples").glob("*.json"))


def load(p):
    return json.loads(p.read_text(encoding="utf-8"))


def test_every_schema_is_a_valid_schema():
    for kind in schemas.KINDS:
        schemas.validator(kind)   # raises if not


@pytest.mark.parametrize("sample", SAMPLES, ids=lambda p: p.name)
def test_the_samples_are_valid_edits(sample):
    assert schemas.errors("edit", load(sample)) == []
    assert schemas.guess_kind(sample, load(sample)) == "edit"


def test_the_python_and_editor_file_lists_agree_on_the_samples():
    edit = load(paths.HERE / "editor" / "samples" / "short_catch.json")
    files = schemas.edit_files(edit)
    assert "assets/music/sample-night-shift.wav" in files
    assert "shots/sample/v_m1_mind.webm" in files
    assert all(not f.startswith("/") and ".." not in f for f in files)


def test_brand_json_is_valid_and_names_the_game_as_the_game_does():
    brand = load(paths.BRAND)
    assert schemas.errors("brand", brand) == []
    names = dict(re.findall(r'public const string (\w+) = "([^"]+)";', (paths.REPO / "Assets/!_Project/_Core/Scripts/Runtime/GameNames.cs").read_text(encoding="utf-8")))
    assert brand["game"]["name"] == names["Game"]
    assert brand["game"]["japanese"] == names["GameJapanese"]
    assert brand["antagonist"]["name"] == names["Antagonist"]
    assert brand["antagonist"]["japanese"] == names["AntagonistJapanese"]
    assert brand["studio"] == names["Studio"]


def test_a_broken_edit_says_where():
    edit = load(SAMPLES[0])
    edit["scenes"][0]["visual"]["src"] = "/Users/someone/shot.mp4"
    edit["fps"] = 29
    problems = schemas.errors("edit", edit)
    assert any(p.startswith("/fps") for p in problems)
    assert any("/scenes/0/visual" in p for p in problems)


def test_kinds_are_guessed_from_names_and_shapes(tmp_path):
    assert schemas.guess_kind(tmp_path / "shift_03_x.markers.json", {}) == "markers"
    assert schemas.guess_kind(tmp_path / "shift_03_x.path.json", {}) == "shot-path"
    assert schemas.guess_kind(tmp_path / "shot.json", {"frames": 1, "shot": "chase"}) == "shot"
    assert schemas.guess_kind(tmp_path / "x.json", {"a": 1}) is None
