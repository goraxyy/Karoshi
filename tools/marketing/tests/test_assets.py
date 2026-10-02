"""The asset library: nothing gets in without a licence, music must be cleared everywhere, and
what does get in is copied, listed and waits for Drive."""
import json

import pytest

from km import assets

LICENCE = "CC0 1.0"


@pytest.fixture
def root(tmp_path, monkeypatch):
    monkeypatch.setattr(assets, "drive_ready", lambda: False)
    return tmp_path / "marketing"


def make(tmp_path, name, content=b"RIFF....WAVEfmt test"):
    f = tmp_path / name
    f.write_bytes(content)
    return f


def req(file, kind="sfx", **kw):
    kw.setdefault("licence", LICENCE)
    kw.setdefault("source", "https://example.org/sound")
    return assets.Request(file=file, kind=kind, **kw)


@pytest.mark.parametrize("licence", ["", "unknown", "?", "  TBD "])
def test_no_licence_no_entry(tmp_path, root, licence):
    with pytest.raises(assets.Refused, match="licence"):
        assets.add(root, req(make(tmp_path, "a.wav"), licence=licence))
    assert not (root / "assets").exists()


def test_no_source_no_entry(tmp_path, root):
    with pytest.raises(assets.Refused, match="source"):
        assets.add(root, req(make(tmp_path, "a.wav"), source=""))


@pytest.mark.parametrize("cleared", [(), ("youtube",), ("youtube", "instagram")])
def test_music_must_be_cleared_for_all_three_platforms(tmp_path, root, cleared):
    with pytest.raises(assets.Refused, match="YouTube, TikTok and Instagram"):
        assets.add(root, req(make(tmp_path, "song.mp3"), kind="music", cleared=cleared))


def test_cleared_music_gets_in_copied_and_listed(tmp_path, root):
    r = assets.add(root, req(make(tmp_path, "Night Song.mp3"), kind="music", cleared=("youtube", "tiktok", "instagram"), mood=["dark"]))
    assert r.added
    e = r.entry
    assert e["id"] == "night-song" and e["file"] == "assets/music/night-song.mp3"
    assert (root / e["file"]).read_bytes() == (tmp_path / "Night Song.mp3").read_bytes()
    assert e["cleared"] == {"youtube": True, "tiktok": True, "instagram": True}
    assert e["drive"] == {"status": "pending"}
    manifest = json.loads((root / "assets" / "manifest.json").read_text())
    assert [a["id"] for a in manifest["assets"]] == ["night-song"]
    assert any("Drive: not set up" in a for a in r.actions)


def test_the_same_file_twice_is_one_entry_and_names_never_clash(tmp_path, root):
    f = make(tmp_path, "hit.wav")
    assets.add(root, req(f))
    again = assets.add(root, req(f))
    assert not again.added
    other = assets.add(root, req(make(tmp_path, "sub/hit.wav".replace("/", "_"), b"different"), id="hit"))
    assert other.entry["id"] == "hit-2"


def test_the_kind_decides_the_file_type(tmp_path, root):
    with pytest.raises(assets.Refused, match="not .png"):
        assets.add(root, req(make(tmp_path, "a.png"), kind="gif"))
    with pytest.raises(assets.Refused, match="not a Lottie"):
        assets.add(root, req(make(tmp_path, "a.json", b'{"hello": 1}'), kind="lottie"))


def test_a_dry_run_touches_nothing(tmp_path, root):
    r = assets.add(root, req(make(tmp_path, "a.wav")), dry_run=True)
    assert r.added and all(a.startswith("(dry run)") for a in r.actions)
    assert not (root / "assets").exists()


def test_sync_waits_for_drive(tmp_path, root):
    assets.add(root, req(make(tmp_path, "a.wav")), upload=False)
    lines = assets.sync(root)
    assert "not set up yet (1 asset(s) wait)" in lines[0]
