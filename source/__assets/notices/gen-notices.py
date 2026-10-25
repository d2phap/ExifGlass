"""Writes ExifGlass.Lib/Assets/Notices: one text per component the app ships, from the licence files of its packages, and index.tsv for About.

Run from source/ after a package update: python __assets/notices/gen-notices.py (FewerPhotos's generator, for ExifGlass's packages)
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(SOURCE, 'ExifGlass.Lib', 'Assets', 'Notices')
NUGET = os.path.join(os.path.expanduser('~'), '.nuget', 'packages')

MIT = '''MIT License

{copyright}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
'''


def read(*parts):
    with open(os.path.join(*parts), encoding='utf-8-sig') as f:
        return f.read().replace('\r\n', '\n').strip() + '\n'


def package(name, version, file):
    return read(NUGET, name, version, file)


# a list item or a rule starts a line of its own, and a rule ends one
LIST = re.compile(r'^\s*(\(?[0-9]{1,2}[.)]\s|\([a-zA-Z]{1,4}\)\s|[a-z][.)]\s|[-*•]\s|\[)')
RULE = re.compile(r'^\s*[=_*-]{3,}\s*$')


def indent(line):
    return len(line) - len(line.lstrip())


def heading(line):
    words = re.sub(r'[^A-Za-z]', '', line)
    return words.isupper() and len(line.strip()) < 45


def reflow(text):
    """Joins a paragraph's hard-wrapped lines, so About wraps it at its own width; blank lines, lists, headings and rules stay."""
    out = []
    start = 0
    commented = text.lstrip().startswith('//')
    for line in text.split('\n'):
        # a notice copied from a source file keeps its comment marks
        line = re.sub(r'^\s*//\s?', '', line) if commented else line.rstrip()
        prev = out[-1] if out else ''
        joinable = (prev.strip() and line.strip() and indent(line) == start and not LIST.match(line) and not RULE.match(line) and not RULE.match(prev)
                    and not heading(prev) and not prev.rstrip().endswith(':'))
        if joinable:
            out[-1] = prev.rstrip() + ' ' + line.strip()
            continue

        out.append(line)
        start = indent(line)
    return '\n'.join(out).strip() + '\n'


def write(slug, text):
    with open(os.path.join(OUT, slug + '.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(reflow(text))


def main():
    os.makedirs(OUT, exist_ok=True)
    for old in os.listdir(OUT):
        if old.endswith('.txt') or old == 'index.tsv':
            os.remove(os.path.join(OUT, old))

    index = []

    def add(slug, name, licence, text):
        write(slug, text)
        index.append((slug, name, licence))

    # the runtime NativeAOT compiles in, and what it carries (the SDK folder's LICENSE.txt is the SDK's own terms)
    add('dotnet', '.NET', 'MIT', MIT.format(copyright='Copyright (c) .NET Foundation and Contributors') + '\n\n' + package('runtime.win-x64.microsoft.dotnet.ilcompiler', '11.0.0-rc.2.26504.105', 'THIRD-PARTY-NOTICES.TXT'))
    add('avalonia', 'Avalonia UI', 'MIT', MIT.format(copyright='Copyright 2013-2026 © The AvaloniaUI Project'))
    add('exiftool', 'ExifTool', 'Artistic-1.0-Perl or GPL-1.0+', read(SOURCE, '__assets', 'exiftool', 'LICENSE')
        + '\nOn Windows, ExifTool runs on the Strawberry Perl it comes with; the licences of Perl and its libraries ship beside it, in exiftool_files. On macOS it runs on the system Perl; on Linux, as one self-contained program.\n')
    add('prodatagrid', 'ProDataGrid', 'MIT', MIT.format(copyright='Copyright (c) 2026 Wiesław Šoltés'))
    add('skiasharp', 'SkiaSharp and Skia', 'MIT, BSD-3-Clause', package('skiasharp', '4.153.1', 'LICENSE.txt') + '\n\n' + package('skiasharp.nativeassets.win32', '4.153.1', 'THIRD-PARTY-NOTICES.txt'))
    add('harfbuzzsharp', 'HarfBuzzSharp', 'MIT, Old MIT', package('harfbuzzsharp', '8.3.1.3', 'LICENSE.txt') + '\n\n' + package('harfbuzzsharp.nativeassets.win32', '8.3.1.3', 'THIRD-PARTY-NOTICES.txt'))
    add('angle', 'ANGLE (Windows)', 'BSD-3-Clause', package('avalonia.angle.windows.natives', '2.1.27548.20260419', 'LICENSE'))
    add('communitytoolkit-mvvm', 'CommunityToolkit.Mvvm', 'MIT', MIT.format(copyright='Copyright (c) .NET Foundation and Contributors'))
    add('microcom', 'MicroCom.Runtime', 'MIT', MIT.format(copyright='Copyright 2021 (c) Nikita Tsukanov'))
    add('tmds-dbus', 'Tmds.DBus.Protocol (Linux)', 'MIT', MIT.format(copyright='Copyright (c) Tom Deseyn'))
    add('inter', 'Inter (macOS and Linux)', 'OFL-1.1', read(HERE, 'OFL-Inter.txt'))
    add('imageglass-sdk', 'ImageGlass.SDK', 'MIT', MIT.format(copyright='Copyright (c) 2026 Duong Dieu Phap'))
    add('imageglass-tools', 'ImageGlass.Tools (Windows)', 'MIT', MIT.format(copyright='Copyright (c) 2023 - 2025 Duong Dieu Phap'))

    with open(os.path.join(OUT, 'index.tsv'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(''.join(f'{slug}\t{name}\t{licence}\n' for slug, name, licence in index))

    print(f'{len(index)} notices in {OUT}')


if __name__ == '__main__':
    main()
