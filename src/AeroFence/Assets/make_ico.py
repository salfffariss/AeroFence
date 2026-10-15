import sys
from PIL import Image

def convert_to_ico(input_path, output_path):
    img = Image.open(input_path)
    img = img.convert("RGBA")
    
    # Standard Windows ICO sizes
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    
    img.save(
        output_path,
        format='ICO',
        sizes=sizes
    )
    print(f"Successfully converted {input_path} to {output_path} with sizes {sizes}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python make_ico.py <input_png> <output_ico>")
        sys.exit(1)
    convert_to_ico(sys.argv[1], sys.argv[2])
