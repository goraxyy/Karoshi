"""Buffer's GraphQL API (https://api.buffer.com, `Authorization: Bearer <BUFFER_API_KEY>`): find
the channels, add a video post to a channel's queue, and read a post's status.

Videos go by public URL (Buffer fetches them when the post goes out: km/media.publish). Without
BUFFER_API_KEY every request is written to <root>/logs/buffer_requests/ instead of sent.
"""
from __future__ import annotations

import datetime as dt
import json
import time
from pathlib import Path

from . import env

URL = "https://api.buffer.com"

ORGANIZATIONS = "query { account { organizations { id name } } }"
CHANNELS = """query Channels($input: ChannelsInput!) {
  channels(input: $input) { id name service displayName isDisconnected isLocked }
}"""
CREATE_POST = """mutation CreatePost($input: CreatePostInput!) {
  createPost(input: $input) {
    ... on PostActionSuccess { post { id status dueAt } }
    ... on MutationError { message }
  }
}"""
POST = """query Post($input: PostInput!) { post(input: $input) { id status dueAt externalLink error { message } } }"""


class BufferError(Exception):
    """Buffer refused a request, or answered with an error."""


class Buffer:
    def __init__(self, root: Path):
        self.root = root
        self.key = env.get("BUFFER_API_KEY")
        self.live = bool(self.key)
        if self.live:
            import httpx
            self.http = httpx.Client(timeout=60, headers={"Authorization": f"Bearer {self.key}"})

    def gql(self, query: str, variables: dict | None = None) -> dict:
        body = {"query": query, "variables": variables or {}}
        if not self.live:
            out = self.root / "logs" / "buffer_requests" / f"{dt.datetime.now():%Y%m%d_%H%M%S_%f}.json"
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_text(json.dumps(body, indent=1, ensure_ascii=False), encoding="utf-8")
            return {}
        for attempt in range(3):
            r = self.http.post(URL, json=body)
            if r.status_code == 429 and attempt < 2:
                time.sleep(int(r.headers.get("retry-after", "30")))
                continue
            if r.status_code == 401:
                raise BufferError("Buffer refused BUFFER_API_KEY: check the key in tools/marketing/.env")
            if r.status_code >= 400:
                raise BufferError(f"Buffer answered {r.status_code}: {r.text[:300]}")
            data = r.json()
            if data.get("errors"):
                raise BufferError("; ".join(e.get("message", "?") for e in data["errors"])[:500])
            return data.get("data") or {}
        raise BufferError("Buffer kept answering 'too many requests'")

    def channels(self) -> dict[str, dict]:
        """The connected channels by service (youtube, instagram, twitter, …): the first usable one of each."""
        out: dict[str, dict] = {}
        for org in (self.gql(ORGANIZATIONS).get("account") or {}).get("organizations", []):
            for ch in self.gql(CHANNELS, {"input": {"organizationId": org["id"]}}).get("channels", []):
                if not ch.get("isDisconnected") and not ch.get("isLocked"):
                    out.setdefault(ch["service"], ch)
        return out

    def queue_video(self, channel_id: str, service: str, text: str, video_url: str, extra: dict) -> str | None:
        """Adds a video post to a channel's queue; returns the post's id (None on a dry run)."""
        data = {"channelId": channel_id, "text": text, "schedulingType": "automatic", "mode": "addToQueue",
                "needsApproval": False, "assets": [{"video": {"url": video_url}}]}
        meta = post_metadata(service, extra)
        if meta:
            data["metadata"] = meta
        result = self.gql(CREATE_POST, {"input": data}).get("createPost")
        if result is None:
            return None
        if "post" not in result:
            raise BufferError(result.get("message", "createPost failed"))
        return result["post"]["id"]

    def status(self, post_id: str) -> dict:
        return self.gql(POST, {"input": {"id": post_id}}).get("post") or {}


def post_metadata(service: str, extra: dict) -> dict | None:
    """What each service needs besides the text: YouTube a title and category, Instagram the reel type."""
    if service == "youtube":
        yt = extra["youtube"]
        return {"youtube": {"title": extra["title"][:100], "categoryId": yt["categoryId"], "privacy": yt["privacy"],
                            "madeForKids": yt["madeForKids"], "notifySubscribers": yt["notifySubscribers"]}}
    if service == "instagram":
        ig = extra["instagram"]
        return {"instagram": {"type": ig["type"], "shouldShareToFeed": ig["shouldShareToFeed"]}}
    return None


def text_for(service: str, block: dict) -> str:
    """The post's text from package.json's block for one language."""
    if service == "youtube":
        return block["youtube"]["description"]
    if service in ("instagram", "tiktok"):
        part = block[service]
        return (part["caption"] + "\n\n" + " ".join(part["hashtags"])).strip()
    if service == "twitter":
        return block["x"]
    if service == "bluesky":
        return block["bluesky"]
    return block["x"]
