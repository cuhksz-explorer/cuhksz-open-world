import re
import urllib.request

PAGES = [
    "impact-sounds",
    "sci-fi-sounds",
    "digital-audio",
    "music-jingles",
    "rpg-audio",
]

for slug in PAGES:
    page = f"https://kenney.nl/assets/{slug}"
    try:
        req = urllib.request.Request(page, headers={"User-Agent": "Mozilla/5.0"})
        html = urllib.request.urlopen(req, timeout=30).read().decode("utf-8", "ignore")
        zips = re.findall(r"https://kenney\.nl/media/pages/assets/[^\"']+\.zip", html)
        print(slug, zips[0] if zips else "NONE")
    except Exception as ex:
        print(slug, "ERR", ex)
