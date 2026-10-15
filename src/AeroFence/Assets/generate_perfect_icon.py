import math
from PIL import Image, ImageDraw, ImageFilter

def create_fluent_lightning_icon(output_png, output_ico):
    # Working resolution for crisp rendering
    res = 512
    
    # 1. Base image (transparent)
    base = Image.new("RGBA", (res, res), (0, 0, 0, 0))
    
    # Inner Lightning Bolt Coordinates (scaled to [0, res])
    # Well balanced, sleek, energetic ⚡
    # Centered: bounds are approx x: [140, 380], y: [45, 470]
    pts = [
        (295, 45),   # Top tip
        (145, 260),  # Left elbow outer
        (245, 260),  # Inner waist notch (left)
        (195, 470),  # Bottom sharp tip
        (380, 220),  # Right elbow outer
        (275, 220),  # Inner waist notch (right)
    ]
    
    # Render inner mask at resolution (res x res)
    inner_mask = Image.new("L", (res, res), 0)
    idraw = ImageDraw.Draw(inner_mask)
    idraw.polygon(pts, fill=255)
    
    # Smooth inner mask
    inner_mask_smooth = inner_mask.filter(ImageFilter.GaussianBlur(1.0))
    
    # Create outer mask (concentric expansion by ~18 pixels)
    # Using 2-pass fast box/max filter or downscaled MaxFilter
    # Render low-res for instant dilation
    low_res = 256
    scale = low_res / res
    pts_low = [(p[0] * scale, p[1] * scale) for p in pts]
    
    m_low = Image.new("L", (low_res, low_res), 0)
    md = ImageDraw.Draw(m_low)
    md.polygon(pts_low, fill=255)
    
    # Dilation for white border (border radius = 9 low_res px = 18 res px)
    m_expanded_low = m_low.filter(ImageFilter.MaxFilter(19))
    outer_mask = m_expanded_low.resize((res, res), Image.Resampling.LANCZOS)
    
    # Smooth outer mask
    outer_mask_smooth = outer_mask.filter(ImageFilter.GaussianBlur(1.2))
    
    # 2. Ambient Drop Shadow (beneath the white border)
    shadow = Image.new("RGBA", (res, res), (0, 0, 0, 0))
    shadow_color = Image.new("RGBA", (res, res), (15, 23, 42, 120)) # Dark slate shadow
    shadow.paste(shadow_color, (0, 0), outer_mask_smooth)
    shadow = shadow.filter(ImageFilter.GaussianBlur(10.0))
    
    # 3. Outer White Border
    white_border = Image.new("RGBA", (res, res), (255, 255, 255, 255))
    white_border.putalpha(outer_mask_smooth)
    
    # 4. Inner Gradient Fill: Light Cyan (#00E5FF / #38BDF8) down to Royal Deep Blue (#1D4ED8 / #172554)
    gradient = Image.new("RGBA", (res, res), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(gradient)
    
    min_y = 45
    max_y = 470
    span_y = max_y - min_y
    
    # 3-stop rich Fluent gradient
    for y in range(min_y, max_y + 1):
        t = (y - min_y) / span_y
        # Ease curve for smooth transition
        t_curved = t * t * (3 - 2 * t)
        
        if t_curved < 0.45:
            f = t_curved / 0.45
            # Stop 1: #00F0FF (0, 240, 255) to Stop 2: #2563EB (37, 99, 235)
            r = int(0 + (37 - 0) * f)
            g = int(240 + (99 - 240) * f)
            b = int(255 + (235 - 255) * f)
        else:
            f = (t_curved - 0.45) / 0.55
            # Stop 2: #2563EB (37, 99, 235) to Stop 3: #0F172A (15, 23, 42)
            r = int(37 + (15 - 37) * f)
            g = int(99 + (23 - 99) * f)
            b = int(235 + (80 - 235) * f)
            
        gdraw.line([(0, y), (res, y)], fill=(r, g, b, 255))
        
    gradient.putalpha(inner_mask_smooth)
    
    # 5. Inner Specular Glass Reflection (Top edge highlight)
    highlight = Image.new("RGBA", (res, res), (0, 0, 0, 0))
    hdraw = ImageDraw.Draw(highlight)
    # Upper edges
    hdraw.line([(pts[0][0], pts[0][1]), (pts[1][0], pts[1][1])], fill=(255, 255, 255, 140), width=6)
    hdraw.line([(pts[2][0], pts[2][1]), (pts[3][0], pts[3][1])], fill=(255, 255, 255, 100), width=4)
    highlight = highlight.filter(ImageFilter.GaussianBlur(3.0))
    # Clip highlight to inner mask
    hl_clipped = Image.new("RGBA", (res, res), (0, 0, 0, 0))
    hl_clipped.paste(highlight, (0, 0), inner_mask_smooth)
    
    # 6. Composite All Layers
    # Layer 1: Drop Shadow (offset down by 10px)
    base.paste(shadow, (0, 10), shadow)
    # Layer 2: White Outer Contour
    base.alpha_composite(white_border)
    # Layer 3: Vibrant Gradient Fill
    base.alpha_composite(gradient)
    # Layer 4: Glass Reflection Highlight
    base.alpha_composite(hl_clipped)
    
    # Save high-res PNG (512x512)
    base.save(output_png, format="PNG")
    print(f"Saved {output_png}")
    
    # Save Windows Multi-Resolution ICO
    sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
    base.save(output_ico, format="ICO", sizes=sizes)
    print(f"Saved {output_ico}")

if __name__ == "__main__":
    create_fluent_lightning_icon(
        "E:/RE-Anything/AeroFence/Assets/lightning_fluent.png",
        "E:/RE-Anything/AeroFence/Assets/lightning_fluent.ico"
    )
