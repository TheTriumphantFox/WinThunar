"""Compose captioned promotional media from actual WinThunar release UI renders.

Requires Pillow and FFmpeg. No artwork or application UI is synthesized.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

from PIL import Image, ImageDraw, ImageFont, ImageOps


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--captures', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--icon', type=Path, required=True)
    parser.add_argument('--ffmpeg', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    result = json.loads((args.captures / 'capture-result.json').read_text(encoding='utf-8'))
    if result.get('failure'):
        raise RuntimeError(result['failure'])
    version = result['source'].removeprefix('v')

    fonts = Path('C:/Windows/Fonts')
    regular = lambda size: ImageFont.truetype(str(fonts / 'segoeui.ttf'), size)
    bold = lambda size: ImageFont.truetype(str(fonts / 'segoeuib.ttf'), size)
    bg, ink, muted, accent = '#f0f3f6', '#182434', '#536479', '#1464b5'

    # Preserve the actual app renders as separate, unaltered screenshots.
    names = {
        '01-overview': 'winthunar-overview',
        '02-split-panes': 'winthunar-split-panes',
        '04-bulk-rename': 'winthunar-bulk-rename',
        '07-powershell-panel': 'winthunar-powershell',
    }
    for original, dest in names.items():
        shutil.copyfile(args.captures / (original + '.png'), args.output / (dest + '.png'))
    shutil.copyfile(args.captures / 'capture-result.json', args.output / 'capture-result.json')

    steps = [
        ('01-overview', 'A classic layout. Your folders, in view.', 'Tabs keep folder navigation close at hand.', 4),
        ('02-split-panes', 'Open a second pane.', 'Browse Incoming and Organized side by side.', 4),
        ('03-selected', 'Select the files to organize.', 'This walkthrough uses three sample documents.', 3),
        ('04-bulk-rename', 'Preview every name before applying it.', 'Replace draft- with project- in a single bulk rename.', 5),
        ('05-renamed', 'The new names are ready.', 'The app has applied the previewed rename plan.', 3),
        ('06-organized', 'Move the group into its destination.', 'The renamed documents now appear in Organized.', 3),
        ('07-powershell-panel', 'Keep a command panel beside your files.', 'WinThunar / Windows 11 x64 / Free and open source', 3),
    ]
    frames_dir = args.output / '.composition'
    frames_dir.mkdir(exist_ok=True)
    frames, durations = [], []
    for idx, (source, title, subtitle, seconds) in enumerate(steps, 1):
        canvas = Image.new('RGB', (1400, 1032), bg)
        draw = ImageDraw.Draw(canvas)
        draw.text((38, 15), 'WinThunar', font=bold(26), fill=accent)
        draw.text((38, 51), title, font=bold(30), fill=ink)
        draw.text((1362, 29), f'{idx} / {len(steps)}', anchor='ra', font=regular(21), fill=muted)
        screenshot = Image.open(args.captures / (source + '.png')).convert('RGBA')
        # Dialog renders can contain transparent space; preserve the real pixels and
        # composite transparency onto a neutral background rather than inventing UI.
        area = (1360, 840)
        fitted = ImageOps.contain(screenshot, area, Image.Resampling.LANCZOS)
        x, y = (1400 - fitted.width) // 2, 108 + (840 - fitted.height) // 2
        canvas.paste(fitted, (x, y), fitted)
        draw.text((38, 970), subtitle, font=regular(25), fill=ink)
        draw.text((38, 1007), f'Edited walkthrough of the released {version} interface; sample files only.', font=regular(15), fill=muted)
        canvas.save(frames_dir / f'{idx:02}.png')
        frames.append(canvas)
        durations.append(seconds * 1000)

    gif_frames = [ImageOps.contain(frame, (1000, 738)).quantize(colors=160, method=Image.Quantize.MEDIANCUT) for frame in frames]
    gif_frames[0].save(args.output / 'winthunar-demo.gif', save_all=True, append_images=gif_frames[1:], duration=durations, loop=0, optimize=True)

    # Feed repeated stills directly to FFmpeg: 25 seconds, H.264, no audio, web-friendly.
    command = [str(args.ffmpeg), '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24',
               '-s', '1400x1032', '-r', '10', '-i', '-', '-an', '-c:v', 'libx264', '-preset', 'medium',
               '-crf', '20', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(args.output / 'winthunar-demo.mp4')]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    try:
        for frame, duration in zip(frames, durations):
            payload = frame.tobytes()
            for _ in range(duration // 100):
                process.stdin.write(payload)
    finally:
        process.stdin.close()
    if process.wait() != 0:
        raise RuntimeError('FFmpeg could not encode the demo.')

    social = Image.new('RGB', (1280, 640), bg)
    draw = ImageDraw.Draw(social)
    icon = Image.open(args.icon).convert('RGBA')
    icon.thumbnail((82, 82), Image.Resampling.LANCZOS)
    social.paste(icon, (35, 25), icon)
    draw.text((135, 18), 'WinThunar', font=bold(47), fill=ink)
    draw.text((138, 78), 'A classic file manager experience for Windows 11.', font=regular(25), fill=muted)
    draw.rounded_rectangle((35, 138, 1245, 559), radius=16, fill='#ffffff', outline='#d3dce5', width=2)
    screenshot = Image.open(args.output / 'winthunar-split-panes.png').convert('RGB')
    screenshot = screenshot.crop((0, 0, screenshot.width, min(480, screenshot.height)))
    fitted = ImageOps.contain(screenshot, (1180, 397), Image.Resampling.LANCZOS)
    social.paste(fitted, ((1280 - fitted.width) // 2, 150))
    draw.text((36, 583), 'Tabs  /  Split panes  /  Bulk rename', font=bold(25), fill=ink)
    draw.text((1242, 584), 'Free + open source (MIT)', anchor='ra', font=regular(24), fill=accent)
    social.save(args.output / 'winthunar-social.png', optimize=True)

    for path in sorted(args.output.glob('*')):
        if path.is_file():
            print(f'{path.name}: {path.stat().st_size:,} bytes')
    print(f"Demo duration: 25 seconds. App source: {result['source']}.")


if __name__ == '__main__':
    main()
