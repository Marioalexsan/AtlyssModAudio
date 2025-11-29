import os
import glob
import json
from pathlib import Path

FOLDER_PATH = 'E:\\AtlyssExport\\112025.a1\\Assets\\Resources'

files = []
files += glob.glob("**\\*.ogg", root_dir=FOLDER_PATH, recursive=True)
files += glob.glob("**\\*.wav", root_dir=FOLDER_PATH, recursive=True)
files += glob.glob("**\\*.mp3", root_dir=FOLDER_PATH, recursive=True)

with open('data.cs', 'w') as f:
  f.write('public static partial class VanillaClipNames\n')
  f.write('{\n')
  f.write('    public static readonly string[] Paths = [\n')
  f.writelines(['        "' + file.replace("\\", "/") + '",\n' for file in files])
  f.write('    ];\n')
  f.write('}\n')