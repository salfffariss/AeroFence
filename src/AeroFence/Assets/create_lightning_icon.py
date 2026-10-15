import math
from PIL import Image, ImageDraw, ImageFilter

def create_lightning_icon(output_png, output_ico, canvas_size=1024):
    # Supersampling factor for crisp anti-aliased edges
    scale = 2
    size = canvas_size * scale
    
    # 1. Base transparent image
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    
    # Lightning bolt polygon vertices (proportioned within [0, size])
    # Sleek, energetic Windows 11 style lightning bolt
    # Centered nicely on the canvas
    pts = [
        (int(size * 0.58), int(size * 0.08)),  # Top right tip
        (int(size * 0.28), int(size * 0.50)),  # Left waist outer
        (int(size * 0.50), int(size * 0.50)),  # Inner notch right
        (int(size * 0.40), int(size * 0.92)),  # Bottom sharp tip
        (int(size * 0.72), int(size * 0.44)),  # Right waist outer
        (int(size * 0.50), int(size * 0.44)),  # Inner notch left
    ]
    
    # 2. Render Soft Ambient Drop Shadow
    shadow_mask = Image.new("L", (size, size), 0)
    shadow_draw = ImageDraw.Draw(shadow_mask)
    # Stroke expansion for shadow
    shadow_draw.polygon(pts, fill=255)
    
    # Expand slightly for the white border width
    border_width = int(size * 0.04) # ~40px on 1024, 80px on 2048
    
    # Draw thicker polygon for the white border mask
    # To get a smooth outer stroke, we draw the polygon with rounded line joints
    border_img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    border_draw = ImageDraw.Draw(border_img)
    
    # Draw outer stroke in pure white
    border_draw.polygon(pts, fill=(255, 255, 255, 255))
    border_draw.line(pts + [pts[0]], fill=(255, 255, 255, 255), width=border_width * 2, joint="round")
    
    # Extract alpha mask of the border
    outer_mask = border_img.split()[3]
    
    # Shadow from outer mask
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 110))
    shadow.putalpha(outer_mask)
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=int(size * 0.025)))
    
    # Offset shadow down slightly
    shadow_offset = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow_offset.paste(shadow, (0, int(size * 0.02)))
    
    # 3. Create the Gradient Fill for the inner Lightning Bolt
    # From electric cyan / sky blue (#38BDF8 / #00F0FF) to royal deep blue (#1D4ED8 / #0A2F82)
    gradient = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    grad_draw = ImageDraw.Draw(gradient)
    
    # Top color: Light cyan-blue (RGB: 56, 189, 248) -> #38BDF8
    # Mid color: Vibrant azure (RGB: 37, 99, 235) -> #2563EB
    # Bottom color: Deep royal blue (RGB: 15, 47, 138) -> #0F2F8A
    
    min_y = int(size * 0.08)
    max_y = int(size * 0.92)
    span_y = max_y - min_y
    
    for y in range(min_y, max_y + 1):
        ratio = (y - min_y) / span_y
        # Smooth ease-in-out curve for gradient
        ratio = ratio * ratio * (3 - 2 * ratio)
        
        # Color interpolation
        if ratio < 0.5:
            t = ratio / 0.5
            r = int(56 + (37 - 56) * t)
            g = int(210 + (99 - 210) * t)
            b = int(255 + (235 - 255) * t)
        else:
            t = (ratio - 0.5) / 0.5
            r = int(37 + (15 - 37) * t)
            g = int(99 + (47 - 99) * t)
            b = int(235 + (145 - 235) * t)
            
        grad_draw.line([(0, y), (size, y)], fill=(r, g, b, 255))
        
    # Mask the gradient with the inner lightning polygon
    inner_mask = Image.new("L", (size, size), 0)
    inner_draw = ImageDraw.Draw(inner_mask)
    inner_draw.polygon(pts, fill=255)
    inner_draw.line(pts + [pts[0]], fill=255, width=int(border_width * 0.4), joint="round")
    
    gradient.putalpha(inner_mask)
    
    # 4. Subtle Specular Highlight on upper bevel for glossy acrylic/aero feel
    highlight = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    hl_draw = ImageDraw.Draw(highlight)
    # Upper left edges
    hl_pts = [
        pts[0], pts[1], pts[2]
    ]
    hl_draw.line([pts[0], pts[1]], fill=(255, 255, 255, 160), width=int(size * 0.012), joint="round")
    hl_draw.line([pts[2], pts[3]], fill=(255, 255, 255, 120), width=int(size * 0.008), joint="round")
    highlight = highlight.filter(ImageFilter.GaussianBlur(radius=int(size * 0.005)))
    
    # 5. Composite Layers:
    # Layer A: Shadow
    img.alpha_composite(shadow_offset)
    # Layer B: White Outer Border
    img.alpha_composite(border_img)
    # Layer C: Gradient Inner Fill
    img.alpha_composite(gradient)
    # Layer D: Specular Highlight
    img.alpha_composite(highlight)
    
    # 6. Downscale with high-quality Lanczos resampling
    final_img = img.resize((canvas_size, canvas_size), Image.Resampling.LANCZOS)
    
    # Save PNG
    final_img.save(output_png, format="PNG")
    print(f"Saved PNG to {output_png}")
    
    # 7. Generate Windows Multi-Resolution ICO
    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
    final_img.save(output_ico, format="ICO", sizes=sizes)
    print(f"Saved ICO to {output_ico} with sizes {sizes}")

if __name__ == "__main__":
    create_lightning_icon(
        "E:/RE-Anything/AeroFence/Assets/test_lightning.png",
        "E:/RE-Anything/AeroFence/Assets/test_lightning.ico"
    )
