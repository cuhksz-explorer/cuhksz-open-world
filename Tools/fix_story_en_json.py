"""Repair story_main_en.json: fix double-escaped \\u choices and Chinese speaker names."""
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

CHOICE_MAP = {
    '礼貌回应': 'Respond politely',
    '冷淡回应': 'Respond coolly',
    '追求逻辑效率': 'Pursue logical efficiency',
    '寻找人脉捷径': 'Seek connections',
    '抬头发现风景': 'Look up and take in the view',
    '追求绩点安全性': 'Prioritize GPA security',
    '追求未知可能性': 'Pursue the unknown',
    '默许捷径': 'Allow the shortcut',
    '坚守原则': 'Stick to your principles',
    '选择求助': 'Ask for help',
    '选择硬抗': 'Push through alone',
    '我就是这套系统的胜者': 'I am the winner of this system',
    '我只为我自己的好奇心负责': 'I answer only to my curiosity',
    '我会打破这面镜子': 'I will break this mirror',
}

SPEAKER_MAP = {
    '林渡': 'Lin Du',
    '旁白': 'Narrator',
    '迎新学长': 'Orientation Senior',
    '报到老师': 'Registration Staff',
    '沈青禾': 'Shen Qinghe',
    '沈': 'Shen',
    '系统': 'System',
    '系统提示': 'System notice',
    '【系统提示': '[System notice]',
    '【序章': '[Prologue]',
    '【第一章': '[Chapter One]',
    '宿管阿姨': 'Dorm manager',
    '周砚': 'Zhou Yan',
    '顾澄': 'Gu Cheng',
    '苏沐': 'Su Mu',
    '赵梓璇': 'Zhao Zixuan',
    '梁嘉一': 'Liang Jiayi',
    '林见鹿': 'Lin Jianlu',
    '夏林': 'Xia Lin',
    '张教授': 'Professor Zhang',
    '陈教授': 'Professor Chen',
    '王老师': 'Teacher Wang',
    '王教授': 'Professor Wang',
    '赵承勋': 'Zhao Chengxun',
    '苏然': 'Su Ran',
    '楚天': 'Chu Tian',
    '教授': 'Professor',
    '均分幻影': 'GPA phantom',
    '镜像': 'Mirror',
    '镜像林渡': 'Mirror Lin Du',
    '林渡笑笑': 'Lin Du',
    '屏幕顶端浮现出一行字': 'A line appears at the top of the screen',
    '帖子里只有一句话': 'The post contains only one line',
    '倒计时归零。09': 'Countdown hits zero. 09',
    '她第一次深刻地意识到': 'For the first time she truly realized',
}


def unescape_unicode_literals(text: str) -> str:
    if not text or '\\u' not in text:
        return text
    return re.sub(
        r'\\u([0-9a-fA-F]{4})',
        lambda m: chr(int(m.group(1), 16)),
        text,
    )


def fix_choice(text: str) -> str:
    text = unescape_unicode_literals((text or '').strip())
    return CHOICE_MAP.get(text, text)


def fix_speaker(text: str) -> str:
    text = (text or '').strip()
    if not text:
        return text
    if text in SPEAKER_MAP:
        return SPEAKER_MAP[text]
    if re.search(r'[\u4e00-\u9fff]', text):
        return SPEAKER_MAP.get(text, text)
    return text


def repair_file(path: str) -> None:
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    removed = []
    choice_fixes = 0
    speaker_fixes = 0
    keys = list(data.keys())
    for key in keys:
        if '\\u' in key or key.startswith('"'):
            removed.append(key)
            del data[key]
            continue

        doc = data[key]
        for page in doc.get('pages', []):
            before = page.get('speakerName', '')
            after = fix_speaker(before)
            if after != before:
                page['speakerName'] = after
                speaker_fixes += 1

        choices = doc.get('choices', [])
        for i, label in enumerate(choices):
            after = fix_choice(label)
            if after != label:
                choices[i] = after
                choice_fixes += 1

    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write('\n')

    print(f'{path}: choices={choice_fixes}, speakers={speaker_fixes}, removed_keys={len(removed)}')


def main():
    targets = [
        os.path.join(ROOT, 'Assets', 'Resources', 'Localization', 'story_main_en.json'),
        os.path.join(ROOT, 'Assets', 'Localization', 'story_main_en.json'),
        os.path.join(ROOT, 'Assets', 'Resources', 'Localization', 'story_side_en.json'),
        os.path.join(ROOT, 'Assets', 'Localization', 'story_side_en.json'),
    ]
    for path in targets:
        if os.path.exists(path):
            repair_file(path)


if __name__ == '__main__':
    main()
