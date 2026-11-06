"""Round 34: the store preview video (1080×1920, ~30 s) — highlights cut from a recording of the Windows build.

Recording (game window only, with the game's sound):
  RhythmSlashRunner.exe -rsrAutoplay -rsrTier 4 -rsrStage 10 -rsrLang ja   while
  ffmpeg -filter_complex "gfxcapture=window_exe=RhythmSlashRunner\\.exe:capture_cursor=0:max_framerate=30,hwdownload,format=bgra,fps=30,format=yuv420p[v]"
         -f dshow -i audio="<stereo mix>" -map "[v]" -map 0:a -t 150 raw.mp4
Usage: python tools/store/make_video.py <raw.mp4> <lang> [start-end ...]   → store/video/preview_<lang>.mp4
"""
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "unity_project" / "Assets" / "RhythmRunner" / "Resources" / "Art"
FONT = ROOT / "unity_project" / "Assets" / "RhythmRunner" / "Resources" / "Fonts" / "MPLUSRounded1c-ExtraBold.ttf"
OUT = ROOT / "store" / "video"
W, H = 1080, 1920
TEXT = {"ja": ("音に合わせて 斬りまくれ！", "今すぐ プレイ！"), "en": ("Slice to the beat!", "Play now!")}


def card(path, background, line, logo_y):
    img = Image.open(background).convert("RGB").resize((W, H), Image.LANCZOS) if background else Image.new("RGB", (W, H), (214, 46, 128))
    shade = Image.new("RGBA", (W, H), (20, 5, 30, 90))
    img = Image.alpha_composite(img.convert("RGBA"), shade)
    logo = Image.open(ART / "logo.png").convert("RGBA")
    logo.thumbnail((int(W * .9), 600), Image.LANCZOS)
    img.alpha_composite(logo, ((W - logo.width) // 2, logo_y))
    d = ImageDraw.Draw(img)
    size = 92
    font = ImageFont.truetype(str(FONT), size)
    while d.textlength(line, font=font) > W - 120 and size > 40:  # always fits the width
        size -= 4
        font = ImageFont.truetype(str(FONT), size)
    tw = d.textlength(line, font=font)
    d.text(((W - tw) / 2, logo_y + logo.height + 60), line, font=font, fill="white", stroke_width=8, stroke_fill=(90, 20, 60))
    img.convert("RGB").save(path)


def main():
    raw, lang = Path(sys.argv[1]), sys.argv[2]
    spans = [tuple(float(x) for x in s.split("-")) for s in sys.argv[3:]] or [(14, 20), (40, 51.4), (67, 70), (84, 90), (97.5, 99.4)]
    OUT.mkdir(parents=True, exist_ok=True)
    intro, outro = OUT / f"intro_{lang}.png", OUT / f"outro_{lang}.png"
    card(intro, ART / "launch_bg.png", TEXT[lang][0], 260)
    card(outro, None, TEXT[lang][1], 520)
    inputs = ["-loop", "1", "-t", "1.6", "-i", str(intro)]
    for a, b in spans:
        inputs += ["-ss", str(a), "-t", f"{b - a:.2f}", "-i", str(raw)]
    inputs += ["-loop", "1", "-t", "2.2", "-i", str(outro), "-f", "lavfi", "-t", "1.6", "-i", "anullsrc=r=48000:cl=stereo",
               "-f", "lavfi", "-t", "2.2", "-i", "anullsrc=r=48000:cl=stereo"]
    n = len(spans)
    silent1, silent2 = n + 2, n + 3
    parts, labels = [], []
    parts.append(f"[0:v]scale={W}:{H},fps=30,format=yuv420p,setsar=1,fade=t=in:st=0:d=0.3[v0]")
    labels.append(f"[v0][{silent1}:a]")
    for i in range(n):
        k = i + 1
        dur = spans[i][1] - spans[i][0]
        parts.append(f"[{k}:v]scale={W}:{H}:flags=lanczos,fps=30,format=yuv420p,setsar=1[v{k}]")
        parts.append(f"[{k}:a]aresample=48000,afade=t=in:st=0:d=0.15,afade=t=out:st={dur - .2:.2f}:d=0.2[a{k}]")
        labels.append(f"[v{k}][a{k}]")
    last = n + 1
    parts.append(f"[{last}:v]scale={W}:{H},fps=30,format=yuv420p,setsar=1,fade=t=out:st=1.8:d=0.4[v{last}]")
    labels.append(f"[v{last}][{silent2}:a]")
    parts.append("".join(labels) + f"concat=n={n + 2}:v=1:a=1[v][a]")
    out = OUT / f"preview_{lang}.mp4"
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"] + inputs + ["-filter_complex", ";".join(parts), "-map", "[v]", "-map", "[a]",
           "-c:v", "libx264", "-preset", "slow", "-crf", "24", "-maxrate", "6M", "-bufsize", "12M", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", str(out)]
    subprocess.run(cmd, check=True)
    print(out)


if __name__ == "__main__":
    main()
